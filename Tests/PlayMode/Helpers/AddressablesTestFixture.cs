// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.AddressableAssets.ResourceLocators;
using AddressablesAPI = UnityEngine.AddressableAssets.Addressables;
using Object = UnityEngine.Object;

namespace Infohazard.Core.Addressables.Tests.PlayMode {
    /// <summary>
    /// Base class for Addressables tests. Registers an in-memory <see cref="TestPrefabProvider"/> and locator,
    /// captures Addressables and UniTask exceptions instead of logging them, cleans up the PoolManager,
    /// and checks that every load was released.
    /// </summary>
    public abstract class AddressablesTestFixture {
        private readonly List<Object> _tracked = new();
        private ResourceLocationMap _locator;
        private Action<AsyncOperationHandle, Exception> _savedExceptionHandler;

        // Addressables caches providers by ID, so a single provider is registered once and reset between tests.
        private static TestPrefabProvider _sharedProvider;

        protected TestPrefabProvider Provider => _sharedProvider;

        /// <summary>
        /// Exceptions reported by the Addressables ResourceManager during the test.
        /// </summary>
        protected List<Exception> ResourceManagerExceptions { get; } = new();

        /// <summary>
        /// Exceptions from UniTasks that were forgotten (e.g. by Retain()) and failed during the test.
        /// </summary>
        protected List<Exception> UnobservedExceptions { get; } = new();

        /// <summary>
        /// Set to false in a test that deliberately leaves a load unreleased.
        /// </summary>
        protected bool CheckLoadsReleased { get; set; }

        [SetUp]
        public async Task SetUpAddressables() {
            DestroyPoolManager();
            CheckLoadsReleased = true;
            ResourceManagerExceptions.Clear();
            UnobservedExceptions.Clear();

            await AddressablesAPI.InitializeAsync().Task;

            _savedExceptionHandler = ResourceManager.ExceptionHandler;
            ResourceManager.ExceptionHandler = (_, exception) => ResourceManagerExceptions.Add(exception);
            UniTaskScheduler.UnobservedTaskException += OnUnobservedException;

            if (_sharedProvider == null ||
                !AddressablesAPI.ResourceManager.ResourceProviders.Contains(_sharedProvider)) {
                _sharedProvider = new TestPrefabProvider();
                AddressablesAPI.ResourceManager.ResourceProviders.Add(_sharedProvider);
            }

            _sharedProvider.Reset();
            _locator = new ResourceLocationMap("InfohazardTestLocator");
            AddressablesAPI.AddResourceLocator(_locator);
        }

        /// <summary>
        /// Wait for Addressables to finish deferred work. Addressables holds an extra reference on an operation until
        /// its completion callbacks run, and UniTask continuations run inside those callbacks, so a release made
        /// right after awaiting a load only reaches the provider after the current callback returns.
        /// </summary>
        protected static async Task SettleAsync() {
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
        }

        /// <summary>
        /// Assert how many times a key has been released by Addressables, after letting deferred releases finish.
        /// </summary>
        protected async Task AssertReleaseCountAsync(string key, int expected, string message = null) {
            await SettleAsync();
            Assert.That(Provider.GetReleaseCount(key), Is.EqualTo(expected), message);
        }

        [TearDown]
        public async Task TearDownAddressables() {
            for (int i = _tracked.Count - 1; i >= 0; i--) {
                if (_tracked[i]) Object.DestroyImmediate(_tracked[i]);
            }

            _tracked.Clear();

            // Destroy the PoolManager first, so handlers see their pooled instances destroyed.
            DestroyPoolManager();
            await SettleAsync();

            AddressablesAPI.RemoveResourceLocator(_locator);
            UniTaskScheduler.UnobservedTaskException -= OnUnobservedException;
            ResourceManager.ExceptionHandler = _savedExceptionHandler;

            if (CheckLoadsReleased) {
                foreach (KeyValuePair<string, int> pair in Provider.LoadedCounts) {
                    Provider.ReleaseCounts.TryGetValue(pair.Key, out int released);
                    Assert.That(released, Is.EqualTo(pair.Value),
                                $"Location {pair.Key} was loaded {pair.Value} time(s) but released {released} time(s).");
                }
            }
        }

        private void OnUnobservedException(Exception exception) => UnobservedExceptions.Add(exception);

        private static void DestroyPoolManager() {
            foreach (PoolManager manager in Object.FindObjectsByType<PoolManager>(FindObjectsInactive.Include,
                                                                                  FindObjectsSortMode.None)) {
                Object.DestroyImmediate(manager.gameObject);
            }

            PoolManager.ResetStaticState();
        }

        /// <summary>
        /// Register an in-memory prefab under the given key. The prefab is inactive, has a Spawnable unless
        /// <paramref name="withSpawnable"/> is false, and is destroyed after the test.
        /// </summary>
        protected GameObject RegisterPrefab(string key, TestPrefabProvider.Behavior behavior =
                                                TestPrefabProvider.Behavior.Succeed, bool withSpawnable = true) {
            var prefab = new GameObject($"Prefab_{key}");
            prefab.SetActive(false);
            if (withSpawnable) prefab.AddComponent<Spawnable>();
            _tracked.Add(prefab);

            string internalId = Provider.Register(key, prefab, behavior);
            _locator.Add(key, new ResourceLocationBase(key, internalId, Provider.ProviderId, typeof(GameObject)));
            return prefab;
        }

        protected GameObject Create(string name, bool active = true) {
            var obj = new GameObject(name);
            obj.SetActive(active);
            _tracked.Add(obj);
            return obj;
        }

        protected T Track<T>(T obj) where T : Object {
            _tracked.Add(obj);
            return obj;
        }

        protected static void SetField(object target, string fieldName, object value) {
            for (Type type = target.GetType(); type != null; type = type.BaseType) {
                FieldInfo field = type.GetField(fieldName,
                                                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }

            throw new ArgumentException($"Field {fieldName} not found on {target.GetType()}.");
        }

        /// <summary>
        /// Unity-aware null check (a destroyed object compares equal to null).
        /// </summary>
        protected static void AssertDestroyed(Object obj, string message = null) {
            Assert.That(obj == null, Is.True, message ?? $"Expected {obj} to be destroyed.");
        }
    }
}
