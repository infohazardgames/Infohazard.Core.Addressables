// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using LoadState = Infohazard.Core.Addressables.AddressablePoolHandler.LoadState;

namespace Infohazard.Core.Addressables.Tests.PlayMode {
    [Timeout(10000)]
    public class UnitTest_AddressableSpawnRef : AddressablesTestFixture {
        private string _guid;

        [SetUp]
        public void SetUpGuid() {
            _guid = Guid.NewGuid().ToString("N");
        }

        private AssetReferenceGameObject Reference(string guid = null) => new(guid ?? _guid);

        private static AddressablePoolHandler HandlerFor(string key) =>
            PoolManager.Instance.TryGetPoolHandler(key, out IPoolHandler handler)
                ? (AddressablePoolHandler) handler
                : null;

        #region Validity

        [Test]
        public void IsValid_ReflectsReference() {
            Assert.That(new AddressableSpawnRef().IsValid, Is.False);
            Assert.That(new AddressableSpawnRef(new AssetReferenceGameObject("")).IsValid, Is.False);
            Assert.That(new AddressableSpawnRef(Reference()).IsValid, Is.True);
        }

        [Test]
        public void ToString_WithoutReference_DoesNotThrow() {
            Assert.That(() => new AddressableSpawnRef().ToString(), Throws.Nothing);
            Assert.That(new AddressableSpawnRef(Reference()).ToString(), Does.Contain(_guid));
        }

        #endregion

        #region Retain and Release

        [Test]
        public async Task RetainAndWait_LoadsPrefabAndReleaseUnloads() {
            GameObject prefab = RegisterPrefab(_guid);
            var spawnRef = new AddressableSpawnRef(Reference());

            spawnRef.RetainAndWait();

            Assert.That(spawnRef.Prefab, Is.SameAs(prefab));
            Assert.That(spawnRef.Loaded, Is.True);
            Assert.That(spawnRef.RetainCount, Is.EqualTo(1));

            spawnRef.Release();
            Assert.That(spawnRef.RetainCount, Is.Zero);
            Assert.That(spawnRef.Prefab, Is.Null);
            Assert.That(spawnRef.Loaded, Is.False);
            await AssertReleaseCountAsync(_guid, 1, "Releasing the only retain should unload the asset.");
        }

        [Test]
        public async Task RetainAndWaitAsync_LoadsPrefab() {
            GameObject prefab = RegisterPrefab(_guid, TestPrefabProvider.Behavior.Delayed);
            var spawnRef = new AddressableSpawnRef(Reference());

            Task task = spawnRef.RetainAndWaitAsync().AsTask();
            Provider.Complete(_guid);
            await task;

            Assert.That(spawnRef.Prefab, Is.SameAs(prefab));
            spawnRef.Release();
            await AssertReleaseCountAsync(_guid, 1);
        }

        [Test]
        public async Task Retain_ThenWaitUntilLoadedAsync_Loads() {
            RegisterPrefab(_guid);
            var spawnRef = new AddressableSpawnRef(Reference());

            spawnRef.Retain();
            await spawnRef.WaitUntilLoadedAsync();

            Assert.That(spawnRef.Loaded, Is.True);
            spawnRef.Release();
            await AssertReleaseCountAsync(_guid, 1);
        }

        [Test]
        public async Task TwoRefs_SameAsset_ShareHandlerAndLoad() {
            RegisterPrefab(_guid);
            var a = new AddressableSpawnRef(Reference());
            var b = new AddressableSpawnRef(Reference());

            a.RetainAndWait();
            b.RetainAndWait();

            Assert.That(HandlerFor(_guid).RetainCount, Is.EqualTo(2));
            Assert.That(Provider.GetProvideCount(_guid), Is.EqualTo(1));

            a.Release();
            Assert.That(HandlerFor(_guid).State, Is.EqualTo(LoadState.Loaded));
            b.Release();
            await AssertReleaseCountAsync(_guid, 1);
        }

        [Test]
        public void Release_BeforeRetain_LogsError() {
            var spawnRef = new AddressableSpawnRef(Reference());
            LogAssert.Expect(LogType.Error, new Regex("Trying to release non-loaded"));
            spawnRef.Release();
        }

        #endregion

        #region Spawning

        [Test]
        public async Task Spawn_ReturnsInstanceWithSpawnParams() {
            GameObject prefab = RegisterPrefab(_guid);
            var spawnRef = new AddressableSpawnRef(Reference());
            spawnRef.RetainAndWait();

            GameObject instance = spawnRef.Spawn(new SpawnParams { Position = new Vector3(1, 2, 3) });

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.Not.SameAs(prefab));
            Assert.That(instance.GetComponent<Spawnable>().IsSpawned, Is.True);
            Assert.That(instance.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));

            Spawnable.Despawn(instance);
            spawnRef.Release();
            await AssertReleaseCountAsync(_guid, 1);
        }

        [Test]
        public async Task GenericRef_SpawnReturnsComponent() {
            GameObject prefab = RegisterPrefab(_guid);
            prefab.AddComponent<BoxCollider>();
            var spawnRef = new AddressableSpawnRef<BoxCollider>(Reference());
            spawnRef.RetainAndWait();

            Assert.That(spawnRef.Prefab, Is.SameAs(prefab.GetComponent<BoxCollider>()));
            BoxCollider instance = spawnRef.Spawn();

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.gameObject, Is.Not.SameAs(prefab));
            Spawnable.Despawn(instance.gameObject);
            spawnRef.Release();
            await AssertReleaseCountAsync(_guid, 1);
        }

        [Test]
        public void Spawn_BeforeRetain_LogsErrorAndReturnsNull() {
            var spawnRef = new AddressableSpawnRef(Reference());
            LogAssert.Expect(LogType.Error, new Regex("Trying to spawn non-loaded"));
            Assert.That(spawnRef.Spawn(), Is.Null);
        }

        [Test]
        public async Task GenericRef_PrefabMissingComponent_LogsTypeNameAndSpawnsNothing() {
            RegisterPrefab(_guid);
            var spawnRef = new AddressableSpawnRef<SphereCollider>(Reference());

            LogAssert.Expect(LogType.Error, new Regex("does not contain SphereCollider"));
            spawnRef.RetainAndWait();
            Assert.That(spawnRef.Prefab, Is.Null);

            LogAssert.Expect(LogType.Error, new Regex("Spawned object .* does not contain SphereCollider"));
            Assert.That(spawnRef.Spawn(), Is.Null);
            Assert.That(HandlerFor(_guid).ReferenceCount, Is.Zero,
                        "An instance without the component should not be left spawned.");

            spawnRef.Release();
            await AssertReleaseCountAsync(_guid, 1);
        }

        [Test]
        public void Spawn_AfterFailedLoad_ReturnsNullWithoutThrowing() {
            RegisterPrefab(_guid, TestPrefabProvider.Behavior.Fail);
            var spawnRef = new AddressableSpawnRef(Reference());
            Assert.That(() => spawnRef.RetainAndWait(), Throws.Exception);

            LogAssert.Expect(LogType.Error, new Regex("Attempting to instantiate using failed"));
            GameObject instance = null;
            Assert.That(() => instance = spawnRef.Spawn(), Throws.Nothing);
            Assert.That(instance, Is.Null);

            spawnRef.Release();
        }

        #endregion
    }
}
