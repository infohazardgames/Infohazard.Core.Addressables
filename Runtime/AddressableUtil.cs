using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infohazard.Core.Addressables {
    public static class AddressableUtil {
        public static async UniTask<GameObject> SpawnAddressableAsync(object key, SpawnParams spawnParams = default) {
            AddressablePoolHandler handler = GetOrCreatePoolHandler(key);

            await handler.RetainAndWaitAsync();
            try {
                if (handler.State == AddressablePoolHandler.LoadState.Failed) {
                    return null;
                }
                
                GameObject result = PoolManager.Instance.SpawnFromKey(key, spawnParams).gameObject;
                return result;
            } finally {
                handler.Release();
            }
        }
        
        public static GameObject SpawnAddressable(object key, SpawnParams spawnParams = default) {
            AddressablePoolHandler handler = GetOrCreatePoolHandler(key);

            handler.RetainAndWait();
            try {
                if (handler.State == AddressablePoolHandler.LoadState.Failed) {
                    return null;
                }
                
                GameObject result = PoolManager.Instance.SpawnFromKey(key, spawnParams).gameObject;
                return result;
            } finally {
                handler.Release();
            }
        }
        
        public static async UniTask<T> SpawnAddressableAsync<T>(object key, SpawnParams spawnParams = default) where T : Component {
            AddressablePoolHandler handler = GetOrCreatePoolHandler(key);

            await handler.RetainAndWaitAsync();
            try {
                if (handler.State == AddressablePoolHandler.LoadState.Failed) {
                    return null;
                }

                if (!handler.Prefab.TryGetComponent(out T _)) {
                    Debug.LogError($"Loaded object {handler.Prefab} does not contain {nameof(T)}.");
                    return null;
                }
                
                GameObject obj = PoolManager.Instance.SpawnFromKey(key, spawnParams).gameObject;
                T component = obj.GetComponent<T>();
                return component;
            } finally {
                handler.Release();
            }
        }
        
        public static T SpawnAddressable<T>(object key, SpawnParams spawnParams = default) where T : Component {
            AddressablePoolHandler handler = GetOrCreatePoolHandler(key);

            handler.RetainAndWait();
            try {
                if (handler.State == AddressablePoolHandler.LoadState.Failed) {
                    return null;
                }

                if (!handler.Prefab.TryGetComponent(out T _)) {
                    Debug.LogError($"Loaded object {handler.Prefab} does not contain {nameof(T)}.");
                    return null;
                }
                
                GameObject obj = PoolManager.Instance.SpawnFromKey(key, spawnParams).gameObject;
                T component = obj.GetComponent<T>();
                return component;
            } finally {
                handler.Release();
            }
        }

        public static AddressablePoolHandler GetOrCreatePoolHandler(object key) {
            AddressablePoolHandler result = null;
            if (PoolManager.Instance.TryGetPoolHandler(key, out IPoolHandler handler)) {
                if (handler is AddressablePoolHandler aHandler) {
                    result = aHandler;
                } else {
                    Debug.LogError($"A handler already exists for key {key}.");
                    return null;
                }
            } else {
                result = new AddressablePoolHandler(key, PoolManager.Instance.PoolTransform);
                PoolManager.Instance.AddPoolHandler(key, result);
            }

            return result;
        }
    }
}