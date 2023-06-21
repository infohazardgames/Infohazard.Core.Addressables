using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Infohazard.Core.Addressables {
    /// <summary>
    /// An <see cref="IPoolHandler"/> that loads an addressable from a key.
    /// </summary>
    public class AddressablePoolHandler : DefaultPoolHandler {
        /// <summary>
        /// Key of the addressable to spawn (path or GUID).
        /// </summary>
        public object Key { get; }

        /// <summary>
        /// State of loading the given addressable.
        /// </summary>
        public LoadState State { get; private set; }

        /// <summary>
        /// Number of active objects spawned from this <see cref="AddressablePoolHandler"/>.
        /// </summary>
        /// <remarks>
        /// The addressable can be unloaded if <see cref="ReferenceCount"/> AND
        /// <see cref="DefaultPoolHandler.RetainCount"/> both reach zero.
        /// </remarks>
        public int ReferenceCount { get; private set; }

        /// <summary>
        /// Loading operation for the addressable if it is loading or loaded.
        /// </summary>
        public AsyncOperationHandle<GameObject> LoadOperation { get; private set; }

        private readonly Action<Spawnable> _spawnedObjectDestroyedDelegate;

        /// <summary>
        /// Construct <see cref="AddressablePoolHandler"/> given addressable key and parent transform.
        /// </summary>
        /// <param name="key">Key of the addressable to spawn (path or GUID).</param>
        /// <param name="transform">Transform to parent inactive pooled objects to.</param>
        public AddressablePoolHandler(object key, Transform transform) : base(null, transform) {
            Key = key;
            State = LoadState.NotLoaded;
            _spawnedObjectDestroyedDelegate = SpawnedObjectDestroyed;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override void Destroy(Spawnable obj) {
            obj.Destroyed -= _spawnedObjectDestroyedDelegate;
            base.Destroy(obj);
        }

        /// <inheritdoc/>
        public override Spawnable Spawn() {
            if (!EnsureLoaded()) return null;
            
            Spawnable instance = base.Spawn();
            ReferenceCount++;
            return instance;
        }

        /// <inheritdoc/>
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
        
        /// <summary>
        /// Add a user to the <see cref="AddressablePoolHandler"/>.
        /// If not already loaded, the prefab will be loaded asynchronously.
        /// </summary>
        /// <remarks>
        /// If the prefab is already loaded, this will return synchronously.
        /// </remarks>
        public UniTask RetainAndWaitAsync() {
            base.Retain();
            
            if (State == LoadState.NotLoaded) {
                LoadOperation = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(Key);
                State = LoadState.Loading;
            }

            return WaitUntilLoadedAsync();
        }

        /// <summary>
        /// If the prefab is currently loading, wait until it is done asynchronously.
        /// </summary>
        /// <remarks>
        /// This will not start loading the prefab if it is not already loading.
        /// </remarks>
        public async UniTask WaitUntilLoadedAsync() {
            if (State != LoadState.Loading) return;
            await LoadOperation;
            LoadCompleted(LoadOperation);
        }

        /// <summary>
        /// Add a user to the <see cref="AddressablePoolHandler"/>.
        /// If not already loaded, the prefab will be loaded and block.
        /// </summary>
        public void RetainAndWait() {
            base.Retain();

            if (State == LoadState.NotLoaded) {
                LoadOperation = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(Key);
                State = LoadState.Loading;
            }
            
            WaitUntilLoaded();
        }
        
        /// <summary>
        /// If the prefab is currently loading, wait until it is done synchronously.
        /// </summary>
        /// <remarks>
        /// This will not start loading the prefab if it is not already loading.
        /// </remarks>
        public void WaitUntilLoaded() {
            if (State != LoadState.Loading) return;
            LoadOperation.WaitForCompletion();
            LoadCompleted(LoadOperation);
        }
        
        /// <summary>
        /// Add a user to the <see cref="AddressablePoolHandler"/>.
        /// </summary>
        /// <remarks>
        /// Does not wait for the loading to finish.
        /// </remarks>
        public override void Retain() {
            base.Retain();
            RetainAndWaitAsync().Forget();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Unlike a <see cref="DefaultPoolHandler"/>, an <see cref="AddressablePoolHandler"/> should only clear
        /// if both the <see cref="DefaultPoolHandler.RetainCount"/> (users) and <see cref="ReferenceCount"/>
        /// (spawned objects) are zero. This is because unloading the addressable will also unload any assets
        /// needed by the spawned instances, causing missing materials/meshes/textures, etc.
        /// </remarks>
        /// <returns>Whether the pooled instances should be cleared and the addressable unloaded.</returns>
        protected override bool ShouldClear() => base.ShouldClear() && ReferenceCount == 0;

        /// <summary>
        /// Destroy all pooled instances and unload the addressable if it is loaded.
        /// </summary>
        protected override void Clear() {
            base.Clear();

            // If unloaded or failed, nothing to do.
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