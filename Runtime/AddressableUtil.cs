using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infohazard.Core.Addressables {
    public static class AddressableUtil {
        /// <summary>
        /// Spawn an addressable prefab with the given key asynchronously.
        /// </summary>
        /// <remarks>
        /// If no <see cref="AddressablePoolHandler"/> exists for the given key, a new one will be created.
        /// If the addressable is not loaded, the load operation will be awaited asynchronously.
        /// If the addressable is already loaded, this method completes synchronously.
        /// The addressable prefab MUST have a <see cref="Spawnable"/> script.
        /// If Spawnable.<see cref="Spawnable.Pooled"/> is true, the spawn will use pooling.
        /// </remarks>
        /// <param name="key">Key of the addressable to spawn (path or GUID).</param>
        /// <param name="spawnParams">Additional spawn info.</param>
        /// <returns>The spawned object.</returns>
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
        
        /// <summary>
        /// Spawn an addressable prefab with the given key synchronously.
        /// </summary>
        /// <remarks>
        /// If no <see cref="AddressablePoolHandler"/> exists for the given key, a new one will be created.
        /// If the addressable is not loaded, the load operation will block.
        /// The addressable prefab MUST have a <see cref="Spawnable"/> script.
        /// If Spawnable.<see cref="Spawnable.Pooled"/> is true, the spawn will use pooling.
        /// </remarks>
        /// <param name="key">Key of the addressable to spawn (path or GUID).</param>
        /// <param name="spawnParams">Additional spawn info.</param>
        /// <returns>The spawned object.</returns>
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
        
        /// <summary>
        /// Spawn an addressable prefab with a given component type and the given key asynchronously.
        /// </summary>
        /// <remarks>
        /// If no <see cref="AddressablePoolHandler"/> exists for the given key, a new one will be created.
        /// If the addressable is not loaded, the load operation will be awaited asynchronously.
        /// If the addressable is already loaded, this method completes synchronously.
        /// If the addressable prefab does not have the given script, it will not be spawned.
        /// The addressable prefab MUST have a <see cref="Spawnable"/> script.
        /// If Spawnable.<see cref="Spawnable.Pooled"/> is true, the spawn will use pooling.
        /// </remarks>
        /// <param name="key">Key of the addressable to spawn (path or GUID).</param>
        /// <param name="spawnParams">Additional spawn info.</param>
        /// <returns>The component of the given type on the spawned object.</returns>
        public static async UniTask<T> SpawnAddressableAsync<T>(object key, SpawnParams spawnParams = default) where T : class {
            AddressablePoolHandler handler = GetOrCreatePoolHandler(key);

            await handler.RetainAndWaitAsync();
            try {
                if (handler.State == AddressablePoolHandler.LoadState.Failed) {
                    return null;
                }

                if (!handler.Prefab.TryGetComponent(out T _)) {
                    Debug.LogError($"Loaded object {handler.Prefab} does not contain {typeof(T).Name}.");
                    return null;
                }
                
                GameObject obj = PoolManager.Instance.SpawnFromKey(key, spawnParams).gameObject;
                T component = obj.GetComponent<T>();
                return component;
            } finally {
                handler.Release();
            }
        }
        
        /// <summary>
        /// Spawn an addressable prefab with a given component type and the given key synchronously.
        /// </summary>
        /// <remarks>
        /// If no <see cref="AddressablePoolHandler"/> exists for the given key, a new one will be created.
        /// If the addressable is not loaded, the load operation will block.
        /// If the addressable prefab does not have the given script, it will not be spawned.
        /// The addressable prefab MUST have a <see cref="Spawnable"/> script.
        /// If Spawnable.<see cref="Spawnable.Pooled"/> is true, the spawn will use pooling.
        /// </remarks>
        /// <param name="key">Key of the addressable to spawn (path or GUID).</param>
        /// <param name="spawnParams">Additional spawn info.</param>
        /// <returns>The component of the given type on the spawned object.</returns>
        public static T SpawnAddressable<T>(object key, SpawnParams spawnParams = default) where T : class {
            AddressablePoolHandler handler = GetOrCreatePoolHandler(key);

            handler.RetainAndWait();
            try {
                if (handler.State == AddressablePoolHandler.LoadState.Failed) {
                    return null;
                }

                if (!handler.Prefab.TryGetComponent(out T _)) {
                    Debug.LogError($"Loaded object {handler.Prefab} does not contain {typeof(T).Name}.");
                    return null;
                }
                
                GameObject obj = PoolManager.Instance.SpawnFromKey(key, spawnParams).gameObject;
                T component = obj.GetComponent<T>();
                return component;
            } finally {
                handler.Release();
            }
        }

        /// <summary>
        /// Gets the <see cref="AddressablePoolHandler"/> for the given addressable key, creating a new one if needed.
        /// </summary>
        /// <remarks>
        /// If an <see cref="IPoolHandler"/> of a different type has been registered for the given key,
        /// an error will be logged and null will be returned.
        /// </remarks>
        /// <param name="key">Key of the addressable to spawn (path or GUID).</param>
        /// <returns>An <see cref="AddressablePoolHandler"/> for that addressable.</returns>
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