using System;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Infohazard.Core.Addressables {
    public class AddressablePoolHandler : DefaultPoolHandler {
        public object Key { get; }

        public int ReferenceCount { get; private set; }

        public int RetainCount { get; private set; }

        public LoadState State { get; private set; }

        public AsyncOperationHandle<GameObject> LoadOperation { get; private set; }

        private readonly Action<Spawnable> _spawnedObjectDestroyedDelegate;
        private Action _loadSucceeded;
        private Action _loadFailed;

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
            ReferenceCount++;
            result.Destroyed += _spawnedObjectDestroyedDelegate;
            return result;
        }

        protected virtual void SpawnedObjectDestroyed(Spawnable spawnable) {
            if (ReferenceCount < 1) {
                Debug.LogError($"Reference count for {this} trying to go negative (this should not happen).");
                return;
            }

            ReferenceCount--;
            CheckReleaseAsset();
        }

        public void Retain(Action loadSucceeded = null, Action loadFailed = null) {
            RetainCount++;

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

        public void Release() {
            if (RetainCount < 1) {
                Debug.LogError($"Releasing {this} more times than it was retained.");
                return;
            }

            RetainCount--;
            CheckReleaseAsset();
        }

        protected virtual void CheckReleaseAsset() {
            if (RetainCount + ReferenceCount > 0 || State != LoadState.Loaded) return;

            UnityEngine.AddressableAssets.Addressables.Release(LoadOperation);
            LoadOperation = default;
        }

        public enum LoadState {
            NotLoaded,
            Loading,
            Loaded,
            Failed,
        }
    }
}