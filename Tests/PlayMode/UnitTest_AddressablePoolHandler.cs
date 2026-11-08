// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoadState = Infohazard.Core.Addressables.AddressablePoolHandler.LoadState;
using Object = UnityEngine.Object;

namespace Infohazard.Core.Addressables.Tests.PlayMode {
    [Timeout(10000)]
    public class UnitTest_AddressablePoolHandler : AddressablesTestFixture {
        private const string Key = "test/prefab";

        private static AddressablePoolHandler GetHandler(string key = Key) =>
            AddressableUtil.GetOrCreatePoolHandler(key);

        private static Spawnable SpawnFromKey(string key = Key) => PoolManager.Instance.SpawnFromKey(key);

        private static async Task<Exception> CatchAsync(Func<UniTask> action) {
            try {
                await action();
            } catch (Exception e) {
                return e;
            }

            return null;
        }

        #region Loading

        [Test]
        public void NewHandler_IsNotLoaded() {
            AddressablePoolHandler handler = GetHandler();

            Assert.That(handler.Key, Is.EqualTo(Key));
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded));
            Assert.That(handler.Prefab, Is.Null);
            Assert.That(handler.RetainCount, Is.Zero);
            Assert.That(handler.ReferenceCount, Is.Zero);
            Assert.That(PoolManager.Instance.TryGetPoolHandler(Key, out IPoolHandler registered), Is.True);
            Assert.That(registered, Is.SameAs(handler));
        }

        [Test]
        public async Task RetainAndWaitAsync_LoadsPrefab() {
            GameObject prefab = RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();

            await handler.RetainAndWaitAsync();

            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            Assert.That(handler.Prefab, Is.SameAs(prefab.GetComponent<Spawnable>()));
            Assert.That(handler.RetainCount, Is.EqualTo(1));
            Assert.That(Provider.GetProvideCount(Key), Is.EqualTo(1));

            handler.Release();
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded));
            Assert.That(handler.Prefab, Is.Null);
            await AssertReleaseCountAsync(Key, 1);
        }

        [Test]
        public void RetainAndWait_LoadsSynchronously() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();

            handler.RetainAndWait();

            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            handler.Release();
        }

        [Test]
        public void RetainAndWait_DelayedLoad_CompletesSynchronously() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Delayed);
            AddressablePoolHandler handler = GetHandler();

            handler.RetainAndWait();

            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            handler.Release();
        }

        [Test]
        public async Task RetainAndWaitAsync_DelayedLoad_WaitsForCompletion() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Delayed);
            AddressablePoolHandler handler = GetHandler();

            UniTask task = handler.RetainAndWaitAsync();
            Assert.That(handler.State, Is.EqualTo(LoadState.Loading));
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));

            Provider.Complete(Key);
            await task;

            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            handler.Release();
        }

        [Test]
        public void WaitUntilLoaded_NotLoaded_Throws() {
            AddressablePoolHandler handler = GetHandler();
            Assert.Throws<InvalidOperationException>(() => handler.WaitUntilLoaded());
            Assert.Throws<InvalidOperationException>(() => handler.WaitUntilLoadedAsync());
        }

        [Test]
        public async Task Retain_IncrementsRetainCountOnce() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();

            handler.Retain();
            Assert.That(handler.RetainCount, Is.EqualTo(1));
            await handler.WaitUntilLoadedAsync();

            handler.Release();
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded), "One Release should balance one Retain.");
        }

        [Test]
        public async Task MultipleRetains_StayLoadedUntilLastRelease() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();
            await handler.RetainAndWaitAsync();
            await handler.RetainAndWaitAsync();

            handler.Release();
            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            Assert.That(Provider.GetProvideCount(Key), Is.EqualTo(1));

            handler.Release();
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded));
        }

        #endregion

        #region Load Failures

        [Test]
        public async Task FailingLoad_ThrowsAndEntersFailedState() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Fail);
            AddressablePoolHandler handler = GetHandler();

            Exception exception = await CatchAsync(() => handler.RetainAndWaitAsync());

            Assert.That(exception, Is.Not.Null);
            Assert.That(handler.State, Is.EqualTo(LoadState.Failed));
            Assert.That(handler.Prefab, Is.Null);

            handler.Release();
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded));
        }

        [Test]
        public void FailingLoad_Synchronous_Throws() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Fail);
            AddressablePoolHandler handler = GetHandler();

            Assert.That(() => handler.RetainAndWait(), Throws.Exception);
            Assert.That(handler.State, Is.EqualTo(LoadState.Failed));
            handler.Release();
        }

        [Test]
        public async Task UnknownKey_ThrowsAndEntersFailedState() {
            AddressablePoolHandler handler = GetHandler("test/unknown");

            Exception exception = await CatchAsync(() => handler.RetainAndWaitAsync());

            Assert.That(exception, Is.Not.Null);
            Assert.That(handler.State, Is.EqualTo(LoadState.Failed));
            handler.Release();
        }

        [Test]
        public async Task PrefabWithoutSpawnable_ThrowsAndReleasesLoad() {
            RegisterPrefab(Key, withSpawnable: false);
            AddressablePoolHandler handler = GetHandler();

            Exception exception = await CatchAsync(() => handler.RetainAndWaitAsync());

            Assert.That(exception, Is.InstanceOf<MissingComponentException>());
            Assert.That(handler.State, Is.EqualTo(LoadState.Failed));
            await AssertReleaseCountAsync(Key, 1, "The loaded asset should be released.");
            handler.Release();
        }

        [Test]
        public async Task FailedHandler_ReleasedAndRetainedAgain_RetriesLoad() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Fail);
            AddressablePoolHandler handler = GetHandler();
            await CatchAsync(() => handler.RetainAndWaitAsync());
            handler.Release();
            await SettleAsync();

            await CatchAsync(() => handler.RetainAndWaitAsync());

            Assert.That(Provider.GetProvideCount(Key), Is.EqualTo(2),
                        "A new load should be attempted rather than reusing the failed operation.");
            handler.Release();
        }

        [Test]
        public async Task Spawn_AfterFailedLoad_LogsErrorAndReturnsNull() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Fail);
            AddressablePoolHandler handler = GetHandler();
            await CatchAsync(() => handler.RetainAndWaitAsync());

            LogAssert.Expect(LogType.Error, new Regex("Attempting to instantiate using failed"));
            Assert.That(SpawnFromKey(), Is.Null);
            handler.Release();
        }

        [Test]
        public void Spawn_BeforeRetain_LogsErrorAndReturnsNull() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();

            LogAssert.Expect(LogType.Error, new Regex("state is not loaded"));
            Assert.That(handler.Spawn(), Is.Null);
        }

        #endregion

        #region Spawning and Lifetime

        [Test]
        public async Task SpawnAndDespawn_TrackReferenceCountAndReuseInstances() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();
            await handler.RetainAndWaitAsync();

            Spawnable instance = SpawnFromKey();
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.IsSpawned, Is.True);
            Assert.That(handler.ReferenceCount, Is.EqualTo(1));

            Spawnable.Despawn(instance);
            Assert.That(handler.ReferenceCount, Is.Zero);
            Assert.That(instance.gameObject.activeSelf, Is.False);

            Assert.That(SpawnFromKey(), Is.SameAs(instance), "Retained handlers pool their instances.");
            Spawnable.Despawn(instance);
            handler.Release();
        }

        [Test]
        public async Task Spawn_WhileLoading_WaitsForLoad() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Delayed);
            AddressablePoolHandler handler = GetHandler();
            UniTask task = handler.RetainAndWaitAsync();

            Spawnable instance = SpawnFromKey();

            Assert.That(instance, Is.Not.Null);
            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            await task;
            Spawnable.Despawn(instance);
            handler.Release();
        }

        [Test]
        public async Task Release_WithLiveInstance_KeepsAssetUntilLastDespawn() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();
            await handler.RetainAndWaitAsync();
            Spawnable instance = SpawnFromKey();

            handler.Release();
            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            await AssertReleaseCountAsync(Key, 0);

            Spawnable.Despawn(instance);
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded));
            await AssertReleaseCountAsync(Key, 1);
            AssertDestroyed(instance, "The pooled instance should be destroyed when the pool clears.");
        }

        [Test]
        public async Task DestroyingLiveInstance_DecrementsReferenceCountAndClears() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();
            await handler.RetainAndWaitAsync();
            Spawnable instance = SpawnFromKey();
            handler.Release();

            Object.DestroyImmediate(instance.gameObject);

            Assert.That(handler.ReferenceCount, Is.Zero);
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded));
            await AssertReleaseCountAsync(Key, 1);
        }

        [Test]
        public async Task ReleaseWhileLoading_CancelsWaitersAndReleasesLoad() {
            RegisterPrefab(Key, TestPrefabProvider.Behavior.Delayed);
            AddressablePoolHandler handler = GetHandler();
            UniTask retainTask = handler.RetainAndWaitAsync();
            UniTask waitTask = handler.WaitUntilLoadedAsync();

            handler.Release();
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded));

            Provider.Complete(Key);

            Assert.That(await CatchAsync(() => retainTask), Is.InstanceOf<OperationCanceledException>());
            Assert.That(await CatchAsync(() => waitTask), Is.InstanceOf<OperationCanceledException>());
            Assert.That(handler.State, Is.EqualTo(LoadState.NotLoaded),
                        "A load that completes after everything was released should be discarded.");
        }

        [Test]
        public async Task RetainAfterClear_LoadsAgain() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();
            await handler.RetainAndWaitAsync();
            handler.Release();
            await SettleAsync();

            await handler.RetainAndWaitAsync();

            Assert.That(handler.State, Is.EqualTo(LoadState.Loaded));
            Assert.That(Provider.GetProvideCount(Key), Is.EqualTo(2));
            handler.Release();
        }

        [Test]
        public async Task DestroyingPoolManager_WithPooledInstances_LogsNoError() {
            RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();
            await handler.RetainAndWaitAsync();
            Spawnable.Despawn(SpawnFromKey());

            // The handler is still retained, so its asset is deliberately left loaded when its PoolManager goes away.
            CheckLoadsReleased = false;
            Object.DestroyImmediate(PoolManager.Instance.gameObject);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ToString_IncludesKeyAndPrefab() {
            GameObject prefab = RegisterPrefab(Key);
            AddressablePoolHandler handler = GetHandler();
            Assert.That(handler.ToString(), Does.Contain(Key).And.Contain("null"));

            await handler.RetainAndWaitAsync();
            Assert.That(handler.ToString(), Does.Contain(prefab.name));
            handler.Release();
        }

        #endregion
    }
}
