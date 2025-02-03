using System;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Infohazard.Core.Addressables {
    /// <summary>
    /// Only used internally.
    /// </summary>
    [Serializable]
    public abstract class AddressableSpawnRefBase {
        [SerializeField]
        [Tooltip("Reference to the addressable prefab to spawn.")]
        protected AssetReferenceGameObject _assetReference;

        /// <summary>
        /// Reference to the addressable prefab to spawn.
        /// </summary>
        public AssetReferenceGameObject AssetReference => _assetReference;

        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public static class PropNames {
            public const string AssetReference = nameof(_assetReference);
        }
    }

    /// <summary>
    /// Similar to <see cref="SpawnRef"/>, but for spawning addressable prefabs.
    /// </summary>
    /// <typeparam name="T">The type of object to be referenced.</typeparam>
    [Serializable]
    public abstract class AddressableSpawnRefBase<T> : AddressableSpawnRefBase where T : Object {
        private AddressablePoolHandler _handler;
        private T _prefab;

        /// <summary>
        /// Whether there is a valid asset reference.
        /// </summary>
        public bool IsValid => _assetReference?.RuntimeKeyIsValid() == true;

        /// <summary>
        /// Whether the referenced asset has been loaded successfully and the
        /// <see cref="AddressableSpawnRef"/> is retained at least once.
        /// </summary>
        public bool Loaded => _prefab != null && _handler is { State: AddressablePoolHandler.LoadState.Loaded };

        /// <summary>
        /// Number of users of the <see cref="AddressableSpawnRef"/>
        /// </summary>
        public int RetainCount { get; private set; }

        /// <summary>
        /// Prefab to be spawned, if it is loaded.
        /// </summary>
        public T Prefab => _prefab;

        /// <summary>
        /// Default constructor (needed for Unity serialization).
        /// </summary>
        public AddressableSpawnRefBase() { }

        /// <summary>
        /// Construct with a given asset reference.
        /// </summary>
        /// <param name="assetReference">The prefab to be spawned.</param>
        public AddressableSpawnRefBase(AssetReferenceGameObject assetReference) {
            _assetReference = assetReference;
        }

        /// <inheritdoc/>
        public override string ToString() {
            string prefabString = Prefab ? Prefab.name : "null";
            return $"{GetType().Name} ({AssetReference.AssetGUID}) ({prefabString})";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Used in debug builds to ensure <see cref="AddressableSpawnRef"/>s are released fully.
        /// </summary>
        ~AddressableSpawnRefBase() {
            if (RetainCount != 0) {
                Debug.LogError($"{this} left with nonzero retain count {RetainCount}!");
            }
        }
#endif

        /// <summary>
        /// Add a user to the <see cref="AddressableSpawnRef"/>,
        /// creating the <see cref="AddressablePoolHandler"/> if necessary.
        /// If not already loaded, the prefab will be loaded asynchronously.
        /// </summary>
        /// <remarks>
        /// If the prefab is already loaded, this will return synchronously.
        /// </remarks>
        public virtual UniTask RetainAndWaitAsync() {
            RetainCount++;

            _handler ??= AddressableUtil.GetOrCreatePoolHandler(_assetReference.RuntimeKey);
            _handler.Retain();

            return WaitUntilLoadedAsync();
        }

        /// <summary>
        /// If the prefab is currently loading, wait until it is done asynchronously.
        /// </summary>
        /// <remarks>
        /// This will not start loading the prefab if it is not already loading.
        /// </remarks>
        public virtual async UniTask WaitUntilLoadedAsync() {
            await _handler.WaitUntilLoadedAsync();
            ValidateLoadedHandler();
        }

        /// <summary>
        /// Add a user to the <see cref="AddressableSpawnRef"/>,
        /// creating the <see cref="AddressablePoolHandler"/> if necessary.
        /// If not already loaded, the prefab will be loaded and block.
        /// </summary>
        public virtual void RetainAndWait() {
            RetainCount++;

            _handler ??= AddressableUtil.GetOrCreatePoolHandler(_assetReference.RuntimeKey);
            _handler.Retain();

            WaitUntilLoaded();
        }

        /// <summary>
        /// If the prefab is currently loading, wait until it is done synchronously.
        /// </summary>
        /// <remarks>
        /// This will not start loading the prefab if it is not already loading.
        /// </remarks>
        public virtual void WaitUntilLoaded() {
            _handler.WaitUntilLoaded();
            ValidateLoadedHandler();
        }

        /// <summary>
        /// Add a user to the <see cref="AddressableSpawnRef"/>,
        /// creating the <see cref="AddressablePoolHandler"/> if necessary.
        /// </summary>
        /// <remarks>
        /// Does not wait for the loading to finish.
        /// </remarks>
        public virtual void Retain() {
            RetainAndWaitAsync().Forget();
        }

        protected virtual bool ValidateLoadedHandler() {
            if (_prefab) return true;
            if (_handler.State == AddressablePoolHandler.LoadState.Failed) return false;

            if (!ValidateObject(_handler.Prefab, out _prefab)) {
                Debug.LogError($"Loaded object {_handler.Prefab} does not contain {nameof(T)}.");
                _prefab = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Remove a user from the <see cref="AddressableSpawnRef"/>, in turn releasing the <see cref="IPoolHandler"/>.
        /// </summary>
        public virtual void Release() {
            if (_handler == null) {
                Debug.LogError($"Trying to release non-loaded {GetType().Name}.");
                return;
            }

            if (RetainCount < 1) {
                Debug.LogError($"Releasing {this} more times than it was retained.");
                return;
            }

            RetainCount--;
            _handler.Release();

            if (RetainCount == 0) {
                _prefab = null;
            }
        }

        /// <summary>
        /// Spawn an instance of <see cref="Prefab"/>. The <see cref="AddressableSpawnRef"/> MUST be retained.
        /// </summary>
        /// <remarks>
        /// If the addressable has not yet finished loading, it will block until done.
        /// For this reason, you should <see cref="Retain"/> the <see cref="AddressableSpawnRef"/>
        /// well in advance of needing to spawn.
        /// </remarks>
        /// <param name="spawnParams">Additional spawn info.</param>
        /// <returns>The spawned object.</returns>
        public T Spawn(in SpawnParams spawnParams = default) {
            if (_handler == null) {
                Debug.LogError($"Trying to spawn non-loaded {GetType().Name}.");
                return null;
            }

            ValidateObject(PoolManager.Instance.SpawnFromKey(_assetReference.AssetGUID, spawnParams), out T result);
            return result;
        }

        protected abstract bool ValidateObject(Spawnable obj, out T result);
    }

    /// <summary>
    /// <see cref="AddressableSpawnRef"/> for spawning a GameObject directly.
    /// </summary>
    [Serializable]
    public class AddressableSpawnRef : AddressableSpawnRefBase<GameObject> {
        /// <summary>
        /// Default constructor (needed for Unity serialization).
        /// </summary>
        public AddressableSpawnRef() { }

        /// <summary>
        /// Construct with a given asset reference.
        /// </summary>
        /// <param name="assetReference">The prefab to be spawned.</param>
        public AddressableSpawnRef(AssetReferenceGameObject assetReference) : base(assetReference) { }

        protected override bool ValidateObject(Spawnable obj, out GameObject result) {
            result = obj.gameObject;
            return true;
        }
    }

    /// <summary>
    /// <see cref="AddressableSpawnRef"/> for spawning a GameObject and returning one of its components.
    /// </summary>
    [Serializable]
    public class AddressableSpawnRef<T> : AddressableSpawnRefBase<T> where T : Component {
        /// <summary>
        /// Default constructor (needed for Unity serialization).
        /// </summary>
        public AddressableSpawnRef() { }

        /// <summary>
        /// Construct with a given asset reference.
        /// </summary>
        /// <param name="assetReference">The prefab to be spawned.</param>
        public AddressableSpawnRef(AssetReferenceGameObject assetReference) : base(assetReference) { }

        protected override bool ValidateObject(Spawnable obj, out T component) {
            return obj.TryGetComponent(out component);
        }
    }
}
