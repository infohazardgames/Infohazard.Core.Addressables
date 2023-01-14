using System;
using Infohazard.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Infohazard.Core.Addressables {
    [Serializable]
    public abstract class AddressableSpawnBase {
        [SerializeField] protected AssetReferenceGameObject _assetReference;

        public static class FieldNames {
            public const string AssetReference = nameof(_assetReference);
        }}
    
    [Serializable]
    public abstract class AddressableSpawnBase<T> : AddressableSpawnBase where T : Object {
        private AddressablePoolHandler _handler;

        public bool Valid => _assetReference?.RuntimeKeyIsValid() == true;
        public bool Loaded => _handler is { State: AddressablePoolHandler.LoadState.Loaded };

        public virtual void Load(Action loadSucceeded = null, Action loadFailed = null) {
            if (_handler != null) {
                Debug.LogError($"Trying to load already-loaded {GetType().Name}.");
                return;
            }

            if (PoolManager.Instance.TryGetPoolHandler(_assetReference.AssetGUID, out IPoolHandler handler)) {
                if (handler is AddressablePoolHandler aHandler) {
                    _handler = aHandler;
                } else {
                    Debug.LogError($"A handler already exists for key {_assetReference.AssetGUID}.");
                    return;
                }
            } else {
                _handler = new AddressablePoolHandler(_assetReference.AssetGUID, PoolManager.Instance.PoolTransform);
                PoolManager.Instance.AddPoolHandler(_assetReference.AssetGUID, _handler);
            }

            _handler.Retain(() => {
                if (ValidatePrefab(_handler.Prefab)) {
                    loadSucceeded?.Invoke();
                } else {
                    loadFailed?.Invoke();
                    Debug.LogError($"Loaded object {_handler.Prefab} does not contain {nameof(T)}.");
                    Release();
                }
            }, loadFailed);
        }

        public virtual void Release() {
            if (_handler == null) {
                Debug.LogError($"Trying to release non-loaded {GetType().Name}.");
                return;
            }

            _handler.Release();
            _handler = null;
        }

        public T Spawn(Vector3? position = null, Quaternion? rotation = null, Transform parent = null,
                       bool inWorldSpace = false, ulong persistedInstanceID = 0, in Scene? scene = null) {
            if (_handler == null) {
                Debug.LogError($"Trying to spawn non-loaded {GetType().Name}.");
                return null;
            }

            return PoolManager.Instance.SpawnFromKey(_assetReference.AssetGUID, position, rotation, parent,
                                                     inWorldSpace, persistedInstanceID, scene).GetComponent<T>();
        }

        protected abstract bool ValidatePrefab(Spawnable obj);
    }

    [Serializable]
    public class AddressableSpawn : AddressableSpawnBase<GameObject> {
        protected override bool ValidatePrefab(Spawnable obj) {
            return true;
        }
    }

    [Serializable]
    public class AddressableSpawn<T> : AddressableSpawnBase<T> where T : Component {
        protected override bool ValidatePrefab(Spawnable obj) {
            return obj.TryGetComponent(out T _);
        }
    }
}