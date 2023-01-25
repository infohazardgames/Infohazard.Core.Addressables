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

        public AssetReferenceGameObject AssetReference => _assetReference;

        public static class FieldNames {
            public const string AssetReference = nameof(_assetReference);
        }
    }

    [Serializable]
    public abstract class AddressableSpawnRefBase<T> : AddressableSpawnRefBase where T : Object {
        private AddressablePoolHandler _handler;

        public bool IsValid => _assetReference?.RuntimeKeyIsValid() == true;
        public bool Loaded => _prefab != null && _handler is { State: AddressablePoolHandler.LoadState.Loaded };
        public int RetainCount { get; private set; }
        
        private T _prefab;
        public T Prefab => _prefab;

        public AddressableSpawnRefBase() { }

        public AddressableSpawnRefBase(AssetReferenceGameObject assetReference) {
            _assetReference = assetReference;
        }

        public override string ToString() {
            string prefabString = Prefab ? Prefab.name : "null";
            return $"{GetType().Name} ({AssetReference.AssetGUID}) ({prefabString})";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ~AddressableSpawnRefBase() {
            if (RetainCount != 0) {
                Debug.LogError($"{this} left with nonzero retain count {RetainCount}!");
            }
        }
#endif

        public virtual UniTask RetainAndWaitAsync() {
            RetainCount++;

            _handler ??= AddressableUtil.GetOrCreatePoolHandler(_assetReference.RuntimeKey);
            _handler.Retain();
            
            return WaitUntilLoadedAsync();
        }

        public virtual async UniTask WaitUntilLoadedAsync() {
            await _handler.WaitUntilLoadedAsync();
            ValidateLoadedHandler();
        }

        public virtual void RetainAndWait() {
            RetainCount++;
            
            _handler ??= AddressableUtil.GetOrCreatePoolHandler(_assetReference.RuntimeKey);
            _handler.Retain();
            
            WaitUntilLoaded();
        }

        public virtual void WaitUntilLoaded() {
            _handler.WaitUntilLoaded();
            ValidateLoadedHandler();
        }

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

    [Serializable]
    public class AddressableSpawnRef : AddressableSpawnRefBase<GameObject> {
        public AddressableSpawnRef() { }

        public AddressableSpawnRef(AssetReferenceGameObject assetReference) : base(assetReference) { }

        protected override bool ValidateObject(Spawnable obj, out GameObject result) {
            result = obj.gameObject;
            return true;
        }
    }

    [Serializable]
    public class AddressableSpawnRef<T> : AddressableSpawnRefBase<T> where T : Component {
        public AddressableSpawnRef() { }

        public AddressableSpawnRef(AssetReferenceGameObject assetReference) : base(assetReference) { }

        protected override bool ValidateObject(Spawnable obj, out T component) {
            return obj.TryGetComponent(out component);
        }
    }
}