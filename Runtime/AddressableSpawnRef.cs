using System;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Infohazard.Core.Addressables {
    [Serializable]
    public abstract class AddressableSpawnRefBase {
        [SerializeField] protected AssetReferenceGameObject _assetReference;

        public static class FieldNames {
            public const string AssetReference = nameof(_assetReference);
        }
    }

    [Serializable]
    public abstract class AddressableSpawnRefBase<T> : AddressableSpawnRefBase where T : Object {
        private AddressablePoolHandler _handler;

        public bool Valid => _assetReference?.RuntimeKeyIsValid() == true;
        public bool Loaded => _prefab != null && _handler is { State: AddressablePoolHandler.LoadState.Loaded };
        public T Prefab => _prefab;

        private T _prefab;

        public AddressableSpawnRefBase() { }

        public AddressableSpawnRefBase(AssetReferenceGameObject assetReference) {
            _assetReference = assetReference;
        }

        public virtual async UniTask RetainAsync() {
            _handler ??= AddressableUtil.GetOrCreatePoolHandler(_assetReference.RuntimeKey);
            if (_handler.State != AddressablePoolHandler.LoadState.NotLoaded) return;

            await _handler.RetainAsync();

            _prefab = null;
            if (_handler.State == AddressablePoolHandler.LoadState.Loaded &&
                !ValidatePrefab(_handler.Prefab, out _prefab)) {
                Debug.LogError($"Loaded object {_handler.Prefab} does not contain {nameof(T)}.");
                _prefab = null;
            }
        }

        public virtual void Retain(Action loadSucceeded = null, Action loadFailed = null) {
            _handler ??= AddressableUtil.GetOrCreatePoolHandler(_assetReference.RuntimeKey);

            _prefab = null;
            _handler.Retain(() => {
                if (ValidatePrefab(_handler.Prefab, out _prefab)) {
                    loadSucceeded?.Invoke();
                } else {
                    loadFailed?.Invoke();
                    Debug.LogError($"Loaded object {_handler.Prefab} does not contain {nameof(T)}.");
                }
            }, loadFailed);
        }

        public virtual void Release() {
            if (_handler == null) {
                Debug.LogError($"Trying to release non-loaded {GetType().Name}.");
                return;
            }

            _handler.Release();
        }

        public T Spawn(in SpawnParams spawnParams = default) {
            if (_handler == null) {
                Debug.LogError($"Trying to spawn non-loaded {GetType().Name}.");
                return null;
            }

            return PoolManager.Instance.SpawnFromKey(_assetReference.AssetGUID, spawnParams).GetComponent<T>();
        }

        protected abstract bool ValidatePrefab(Spawnable obj, out T result);
    }

    [Serializable]
    public class AddressableSpawnRef : AddressableSpawnRefBase<GameObject> {
        public AddressableSpawnRef() { }

        public AddressableSpawnRef(AssetReferenceGameObject assetReference) : base(assetReference) { }

        protected override bool ValidatePrefab(Spawnable obj, out GameObject result) {
            result = obj.gameObject;
            return true;
        }
    }

    [Serializable]
    public class AddressableSpawnRef<T> : AddressableSpawnRefBase<T> where T : Component {
        public AddressableSpawnRef() { }

        public AddressableSpawnRef(AssetReferenceGameObject assetReference) : base(assetReference) { }

        protected override bool ValidatePrefab(Spawnable obj, out T component) {
            return obj.TryGetComponent(out component);
        }
    }
}