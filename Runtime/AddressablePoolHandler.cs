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

        private Action _loadSucceeded;
        private Action _loadFailed;
        
        private readonly Action<Spawnable> _spawnedObjectDestroyedDelegate;

        public AddressablePoolHandler(object key, Transform transform) : base(null, transform) {
            Key = key;
            State = LoadState.NotLoaded;
            _spawnedObjectDestroyedDelegate = SpawnedObjectDestroyed;
        }

        public override string ToString() {
            return $"{GetType().Name} ({Key})";
        }

        protected virtual void LoadCompleted(AsyncOperationHandle<GameObject> operation) {
            if (operation.Status == AsyncOperationStatus.Succeeded &&
                operation.Result.TryGetComponent(out Spawnable spawnable)) {
                State = LoadState.Loaded;
                Prefab = spawnable;
                _loadSucceeded?.Invoke();
                _loadFailed = _loadSucceeded = null;
                return;
            }

            if (operation.Status == AsyncOperationStatus.Succeeded) {
                Debug.LogError($"Asset loaded by {this} must have a Spawnable component.");
            }

            State = LoadState.Failed;
            UnityEngine.AddressableAssets.Addressables.Release(operation);
            LoadOperation = default;
            _loadFailed?.Invoke();
            _loadFailed = _loadSucceeded = null;
        }

        protected override Spawnable Instantiate() {
            if (RetainCount < 1) {
                Debug.LogError($"Instantiate called on {this} before Retain() was called.");
                return null;
            }

            if (State == LoadState.NotLoaded) {
                Debug.LogError($"{this} has a nonzero ref count but state is not loaded (this should not happen).");
                return null;
            }

            if (State == LoadState.Loading) {
                LoadOperation.WaitForCompletion();
            }

            if (State == LoadState.Failed) {
                Debug.LogError($"Attempting to instantiate using failed {this}.");
                return null;
            }

            Spawnable result = base.Instantiate();
            result.Destroyed += _spawnedObjectDestroyedDelegate;
            return result;
        }

        protected override void Destroy(Spawnable obj) {
            obj.Destroyed -= _spawnedObjectDestroyedDelegate;
            base.Destroy(obj);
        }

        public override Spawnable Spawn() {
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

        public async UniTask RetainAsync() {
            base.Retain();

            if (State != LoadState.NotLoaded) return;
            
            LoadOperation = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(Key);
            State = LoadState.Loading;
            await LoadOperation;
            LoadCompleted(LoadOperation);
        }

        public override void Retain() {
            Retain(null);
        }

        public void Retain(Action loadSucceeded, Action loadFailed = null) {
            base.Retain();

            if (State == LoadState.NotLoaded) {
                LoadOperation = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(Key);
                LoadOperation.Completed += LoadCompleted;
                State = LoadState.Loading;
            }

            switch (State) {
                case LoadState.Loading:
                    _loadSucceeded += loadSucceeded;
                    _loadFailed += loadFailed;
                    break;
                case LoadState.Loaded:
                    _loadSucceeded?.Invoke();
                    break;
                case LoadState.Failed:
                    _loadFailed?.Invoke();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override bool ShouldClear() => base.ShouldClear() && ReferenceCount == 0;

        protected override void Clear() {
            base.Clear();
            
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