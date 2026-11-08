// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoadState = Infohazard.Core.Addressables.AddressablePoolHandler.LoadState;

namespace Infohazard.Core.Addressables.Tests.PlayMode {
    [Timeout(10000)]
    public class UnitTest_AddressableUtil : AddressablesTestFixture {
        private const string Key = "test/util";

        private static AddressablePoolHandler Handler() => AddressableUtil.GetOrCreatePoolHandler(Key);

        [Test]
        public void GetOrCreatePoolHandler_ReturnsSameHandlerForKey() {
            AddressablePoolHandler handler = Handler();
            Assert.That(handler, Is.Not.Null);
            Assert.That(Handler(), Is.SameAs(handler));
        }

        [Test]
        public void GetOrCreatePoolHandler_KeyOwnedByOtherHandler_LogsErrorAndReturnsNull() {
            GameObject prefab = Create("Plain", active: false);
            var otherHandler = new DefaultPoolHandler(prefab.AddComponent<Spawnable>(), prefab.transform);
            PoolManager.Instance.AddPoolHandler(Key, otherHandler);

            LogAssert.Expect(LogType.Error, new Regex("A handler already exists for key"));
            Assert.That(Handler(), Is.Null);
        }

        [Test]
        public async Task SpawnAddressable_ReturnsSpawnedInstanceAndKeepsAssetUntilDespawn() {
            RegisterPrefab(Key);

            GameObject instance = AddressableUtil.SpawnAddressable(Key, new SpawnParams { Position = Vector3.one });

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.GetComponent<Spawnable>().IsSpawned, Is.True);
            Assert.That(instance.transform.position, Is.EqualTo(Vector3.one));
            Assert.That(Handler().RetainCount, Is.Zero, "The helper should release its own retain.");
            Assert.That(Handler().State, Is.EqualTo(LoadState.Loaded), "The live instance keeps the asset loaded.");

            Spawnable.Despawn(instance);
            await AssertReleaseCountAsync(Key, 1);
        }

        [Test]
        public async Task SpawnAddressableAsync_ReturnsSpawnedInstance() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Delayed);

            Task<GameObject> task = AddressableUtil.SpawnAddressableAsync(Key).AsTask();
            Provider.Complete(Key);
            GameObject instance = await task;

            Assert.That(instance, Is.Not.Null);
            Spawnable.Despawn(instance);
            await AssertReleaseCountAsync(Key, 1);
        }

        [Test]
        public async Task SpawnAddressableGeneric_ReturnsComponentOrInterface() {
            GameObject prefab = RegisterPrefab(Key);
            prefab.AddComponent<BoxCollider>();

            BoxCollider collider = AddressableUtil.SpawnAddressable<BoxCollider>(Key);
            IEnumerable transformAsEnumerable = await AddressableUtil.SpawnAddressableAsync<IEnumerable>(Key);

            Assert.That(collider, Is.Not.Null);
            Assert.That(transformAsEnumerable, Is.InstanceOf<Transform>());
            Spawnable.Despawn(collider.gameObject);
            Spawnable.Despawn(((Transform) transformAsEnumerable).gameObject);
            await AssertReleaseCountAsync(Key, 1);
        }

        [Test]
        public async Task SpawnAddressableGeneric_MissingComponent_LogsErrorReturnsNullAndUnloads() {
            RegisterPrefab(Key);

            LogAssert.Expect(LogType.Error, new Regex("does not contain SphereCollider"));
            Assert.That(AddressableUtil.SpawnAddressable<SphereCollider>(Key), Is.Null);

            // Let the first load's release finish, so the second call loads again instead of reusing it.
            await SettleAsync();
            LogAssert.Expect(LogType.Error, new Regex("does not contain SphereCollider"));
            Assert.That(await AddressableUtil.SpawnAddressableAsync<SphereCollider>(Key), Is.Null);

            Assert.That(Handler().State, Is.EqualTo(LoadState.NotLoaded));
            await AssertReleaseCountAsync(Key, 2);
        }

        [Test]
        public async Task SpawnAddressable_FailedLoad_ThrowsAndCleansUp() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Fail);

            Assert.That(() => AddressableUtil.SpawnAddressable(Key), Throws.Exception);
            Assert.That(Handler().RetainCount, Is.Zero);
            Assert.That(Handler().State, Is.EqualTo(LoadState.NotLoaded));

            Exception asyncException = null;
            try {
                await AddressableUtil.SpawnAddressableAsync(Key);
            } catch (Exception e) {
                asyncException = e;
            }

            Assert.That(asyncException, Is.Not.Null);
            Assert.That(Handler().RetainCount, Is.Zero);
        }

        [Test]
        public void SpawnAddressable_UnknownKey_Throws() {
            Assert.That(() => AddressableUtil.SpawnAddressable("test/unknown"), Throws.Exception);
        }

        [Test]
        public void SpawnAddressable_KeyOwnedByOtherHandler_LogsErrorAndReturnsNull() {
            GameObject prefab = Create("Plain", active: false);
            PoolManager.Instance.AddPoolHandler(Key, new DefaultPoolHandler(prefab.AddComponent<Spawnable>(),
                                                                            prefab.transform));

            LogAssert.Expect(LogType.Error, new Regex("A handler already exists for key"));
            Assert.That(AddressableUtil.SpawnAddressable(Key), Is.Null);
        }
    }
}
