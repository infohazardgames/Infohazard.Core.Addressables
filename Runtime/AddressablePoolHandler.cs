using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Infohazard.Core.Addressables {
    public class AddressablePoolHandler : DefaultPoolHandler {
        public object Key { get; }

        public LoadState State { get; private set; }

        public int ReferenceCount { get; private set; }

        public AsyncOperationHandle<GameObject> LoadOperation { get; private set; }

        private readonly Action<Spawnable> _spawnedObjectDestroyedDelegate;

        public AddressablePoolHandler(object key, Transform transform) : base(null, transform) {
            Key = key;
            State = LoadState.NotLoaded;
            _spawnedObjectDestroyedDelegate = SpawnedObjectDestroyed;
        }

        public override string ToString() {
            string prefabString = Prefab ? Prefab.name : "null";
            return $"{GetType().Name} ({Key}) ({prefabString})";
        }

        protected virtual void LoadCompleted(AsyncOperationHandle<GameObject> operation) {
            if (State != LoadState.Loading) return;
            
            if (operation.Status == AsyncOperationStatus.Succeeded &&
                operation.Result.TryGetComponent(out Spawnable spawnable)) {
                State = LoadState.Loaded;
                Prefab = spawnable;
                
                // If everyone releases the handler before loading completes, just immediately discard.
                CheckClear();
                return;
            }

            if (operation.Status == AsyncOperationStatus.Succeeded) {
                Debug.LogError($"Asset loaded by {this} must have a Spawnable component.");
                UnityEngine.AddressableAssets.Addressables.Release(operation);
            }

            State = LoadState.Failed;
            LoadOperation = default;
            Prefab = null;
        }

        protected virtual bool EnsureLoaded() {
            if (State == LoadState.NotLoaded) {
                Debug.LogError($"{this} has a nonzero ref count but state is not loaded (this should not happen).");
                return false;
            }

            if (State == LoadState.Loading) {
                WaitUntilLoaded();
            }

            if (State == LoadState.Failed) {
                Debug.LogError($"Attempting to instantiate using failed {this}.");
                return false;
            }

            return true;
        }

        protected override Spawnable Instantiate() {
            if (RetainCount < 1) {
                Debug.LogError($"Instantiate called on {this} before Retain() was called.");
                return null;
            }

            if (!EnsureLoaded()) return null;

            Spawnable result = base.Instantiate();
            result.Destroyed += _spawnedObjectDestroyedDelegate;
            return result;
        }

        protected override void Destroy(Spawnable obj) {
            obj.Destroyed -= _spawnedObjectDestroyedDelegate;
            base.Destroy(obj);
        }

        public override Spawnable Spawn() {
            if (!EnsureLoaded()) return null;
            
            Spawnable instance = base.Spawn();
            ReferenceCount++;
            return instance;
        }

        public override void Despawn(Spawnable instance) {
            if (ReferenceCount < 1) {
                Debug.LogError($"Reference count for {this} trying to go negative (this should not happen).");
                return;
            }
            
            base.Despawn(instance);
            ReferenceCount--;
            CheckClear();
        }

        protected virtual void SpawnedObjectDestroyed(Spawnable spawnable) {
            if (spawnable.IsSpawned) {
                if (ReferenceCount < 1) {
                    Debug.LogError($"Reference count for {this} trying to go negative (this should not happen).");
                    return;
                }
                ReferenceCount--;
                CheckClear();
            } else {
                Pool.Remove(spawnable);
            }
        }

        public UniTask RetainAndWaitAsync() {
            base.Retain();
            
            if (State == LoadState.NotLoaded) {
                LoadOperation = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(Key);
                State = LoadState.Loading;
            }

            return WaitUntilLoadedAsync();
        }

        public async UniTask WaitUntilLoadedAsync() {
            if (State != LoadState.Loading) return;
            await LoadOperation;
            LoadCompleted(LoadOperation);
        }

        public void RetainAndWait() {
            base.Retain();

            if (State == LoadState.NotLoaded) {
                LoadOperation = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(Key);
                State = LoadState.Loading;
            }
            
            WaitUntilLoaded();
        }

        public void WaitUntilLoaded() {
            if (State != LoadState.Loading) return;
            LoadOperation.WaitForCompletion();
            LoadCompleted(LoadOperation);
        }

        public override void Retain() {
            base.Retain();
            RetainAndWaitAsync().Forget();
        }

        protected override bool ShouldClear() => base.ShouldClear() && ReferenceCount == 0;

        protected override void Clear() {
            base.Clear();

            // If not loaded or failed, nothing to do.
            // If loading, the following will be taken care of in the LoadCompleted callback.
            if (State != LoadState.Loaded) return;
            
            UnityEngine.AddressableAssets.Addressables.Release(LoadOperation);
            LoadOperation = default;
            Prefab = null;
            State = LoadState.NotLoaded;
        }

        public enum LoadState {
            NotLoaded,
            Loading,
            Loaded,
            Failed,
        }
    }
}