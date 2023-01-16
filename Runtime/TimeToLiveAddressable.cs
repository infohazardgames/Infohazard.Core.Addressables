using System;
using UnityEngine;

namespace Infohazard.Core.Addressables {
    /// <summary>
    /// Extension to <see cref="TimeToLive"/> which can spawn an addressable prefab.
    /// </summary>
    /// <remarks>
    /// Compatible with the pooling system. You can still use the base TimeToLive on an addressable prefab,
    /// but if you assign a <see cref="TimeToLive._spawnOnDeath"/> value to it,
    /// that prefab may be unloaded with the addressable.
    /// </remarks>
    public class TimeToLiveAddressable : TimeToLive {
        [SerializeField] private AddressableSpawnRef _spawnOnDeathAddress;

        protected override void Awake() {
            base.Awake();
            if (_spawnOnDeathAddress.Valid) {
                _spawnOnDeathAddress.Retain();
            }
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            if (_spawnOnDeathAddress.Valid) {
                _spawnOnDeathAddress.Release();
            }
        }
        
        protected override void DestroySelf() {
            if (_spawnOnDeathAddress.Valid) {
                _spawnOnDeathAddress.Spawn(new SpawnParams {
                    Position = transform.position,
                    Rotation = transform.rotation,
                    Scene = gameObject.scene,
                });
            }

            base.DestroySelf();
        }
    }
}