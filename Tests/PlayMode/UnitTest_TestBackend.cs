// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;

namespace Infohazard.Core.Addressables.Tests.PlayMode {
    /// <summary>
    /// Smoke tests for the in-memory Addressables backend used by the other tests.
    /// </summary>
    [Timeout(10000)]
    public class UnitTest_TestBackend : AddressablesTestFixture {
        [Test]
        public async Task LoadAndRelease_UsesTestProvider() {
            GameObject prefab = RegisterPrefab("smoke");

            AsyncOperationHandle<GameObject> handle = AddressablesAPI.LoadAssetAsync<GameObject>("smoke");
            GameObject loaded = await handle.Task;

            Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            Assert.That(loaded, Is.SameAs(prefab));
            Assert.That(Provider.GetProvideCount("smoke"), Is.EqualTo(1));

            AddressablesAPI.Release(handle);
            Assert.That(Provider.GetReleaseCount("smoke"), Is.EqualTo(1));
        }

        [Test]
        public void DelayedLoad_CompletesOnWaitForCompletion() {
            GameObject prefab = RegisterPrefab("delayed", TestPrefabProvider.Behavior.Delayed);

            AsyncOperationHandle<GameObject> handle = AddressablesAPI.LoadAssetAsync<GameObject>("delayed");
            Assert.That(handle.IsDone, Is.False);

            Assert.That(handle.WaitForCompletion(), Is.SameAs(prefab));
            AddressablesAPI.Release(handle);
        }

        [Test]
        public async Task FailingLoad_ReportsExceptionWithoutLogging() {
            RegisterPrefab("fail", TestPrefabProvider.Behavior.Fail);

            AsyncOperationHandle<GameObject> handle = AddressablesAPI.LoadAssetAsync<GameObject>("fail");
            await handle.Task;

            Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Failed));
            Assert.That(ResourceManagerExceptions, Is.Not.Empty);
            AddressablesAPI.Release(handle);
        }
    }
}
