// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LoadState = Infohazard.Core.Addressables.AddressablePoolHandler.LoadState;
using Object = UnityEngine.Object;

namespace Infohazard.Core.Addressables.Tests.PlayMode {
    [Timeout(10000)]
    public class UnitTest_TimeToLiveAddressable : AddressablesTestFixture {
        private static readonly Vector3 Position = new(3, 4, 5);
        private static readonly Quaternion Rotation = Quaternion.Euler(0, 90, 0);

        private string _guid;

        [SetUp]
        public void SetUpGuid() {
            _guid = Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Create an active TimeToLiveAddressable whose serialized fields are set before Awake.
        /// </summary>
        private TimeToLiveAddressable CreateTimeToLive(float timeToLive, AddressableSpawnRef spawnOnDeath) {
            GameObject obj = Create("TimeToLive", active: false);
            obj.transform.SetPositionAndRotation(Position, Rotation);
            var ttl = obj.AddComponent<TimeToLiveAddressable>();
            SetField(ttl, "_timeToLive", timeToLive);
            SetField(ttl, "_spawnOnDeathAddress", spawnOnDeath);
            obj.SetActive(true);
            return ttl;
        }

        private AddressableSpawnRef SpawnRef() => new(new AssetReferenceGameObject(_guid));

        private AddressablePoolHandler Handler() =>
            PoolManager.Instance.TryGetPoolHandler(_guid, out IPoolHandler handler)
                ? (AddressablePoolHandler) handler
                : null;

        private Spawnable FindSpawnedInstance() =>
            Object.FindObjectsByType<Spawnable>(FindObjectsSortMode.None)
                  .FirstOrDefault(s => s.IsSpawned && s.name.Contains(_guid));

        [Test]
        public async Task Expiry_SpawnsAddressableAtObjectPose() {
            RegisterPrefab(_guid);
            TimeToLiveAddressable ttl = CreateTimeToLive(0.05f, SpawnRef());

            await Awaitable.WaitForSecondsAsync(0.2f);

            AssertDestroyed(ttl);
            Spawnable instance = FindSpawnedInstance();
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.transform.position, Is.EqualTo(Position));
            Assert.That(Quaternion.Angle(instance.transform.rotation, Rotation), Is.LessThan(0.01f));
            Assert.That(instance.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()),
                        "The instance should be spawned into the TimeToLive's scene.");
            Assert.That(instance.transform.parent, Is.Null);

            Spawnable.Despawn(instance);
            await AssertReleaseCountAsync(_guid, 1, "The asset should unload once the TimeToLive and instance are gone.");
        }

        [Test]
        public async Task Expiry_WhileStillLoading_WaitsAndSpawns() {
            RegisterPrefab(_guid, TestPrefabProvider.Behavior.Delayed);
            CreateTimeToLive(0.05f, SpawnRef());
            Assert.That(Handler().State, Is.EqualTo(LoadState.Loading));

            await Awaitable.WaitForSecondsAsync(0.2f);

            Spawnable instance = FindSpawnedInstance();
            Assert.That(instance, Is.Not.Null, "Spawning should wait for the in-progress load.");
            Spawnable.Despawn(instance);
            await AssertReleaseCountAsync(_guid, 1);
        }

        [Test]
        public async Task Destroy_BeforeExpiry_ReleasesRetain() {
            RegisterPrefab(_guid);
            TimeToLiveAddressable ttl = CreateTimeToLive(10, SpawnRef());
            await Handler().WaitUntilLoadedAsync();
            Assert.That(Handler().RetainCount, Is.EqualTo(1));

            Object.DestroyImmediate(ttl.gameObject);

            Assert.That(Handler().RetainCount, Is.Zero);
            await AssertReleaseCountAsync(_guid, 1);
            Assert.That(FindSpawnedInstance(), Is.Null, "Nothing should spawn when destroyed before expiring.");
        }

        [Test]
        public async Task EmptyReference_ExpiresWithoutSpawning() {
            TimeToLiveAddressable ttl = CreateTimeToLive(0.05f, new AddressableSpawnRef());

            await Awaitable.WaitForSecondsAsync(0.2f);

            AssertDestroyed(ttl);
            Assert.That(Handler(), Is.Null, "No handler should be created for an empty reference.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AddedFromCode_WithNothingAssigned_DoesNotThrow() {
            GameObject obj = Create("TimeToLive");

            var ttl = obj.AddComponent<TimeToLiveAddressable>();
            Object.DestroyImmediate(ttl);

            LogAssert.NoUnexpectedReceived();
        }
    }
}
