// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Infohazard.Core.Addressables.Tests.PlayMode {
    /// <summary>
    /// An in-memory Addressables resource provider for tests. Each key is backed by a GameObject created by the test,
    /// and can be configured to succeed, fail, or complete only when the test says so.
    /// </summary>
    /// <remarks>
    /// Addressables caches the provider it finds for a provider ID, so one instance is registered for the whole run
    /// and <see cref="Reset"/> between tests. Locations use unique internal IDs per test (see <see cref="Register"/>),
    /// so Addressables' per-location operation cache can't leak loads between tests either.
    /// Counts are looked up by the key the test registered.
    /// </remarks>
    public class TestPrefabProvider : ResourceProviderBase {
        public enum Behavior {
            Succeed,
            Fail,
            Delayed,
        }

        private class Entry {
            public GameObject Prefab;
            public Behavior Behavior;
            public readonly List<ProvideHandle> Pending = new();
        }

        private readonly Dictionary<string, Entry> _entries = new();
        private readonly Dictionary<string, string> _internalIds = new();

        /// <summary>
        /// Number of times each key has been loaded (Provide called).
        /// </summary>
        public Dictionary<string, int> ProvideCounts { get; } = new();

        /// <summary>
        /// Number of times each key has been loaded successfully. Only successful loads are released by Addressables.
        /// </summary>
        public Dictionary<string, int> LoadedCounts { get; } = new();

        /// <summary>
        /// Number of times each key has been released (Release called).
        /// </summary>
        public Dictionary<string, int> ReleaseCounts { get; } = new();

        public TestPrefabProvider() {
            m_ProviderId = typeof(TestPrefabProvider).FullName;
        }

        /// <summary>
        /// Register a prefab for a key, and return the unique internal ID its location must use.
        /// </summary>
        public string Register(string key, GameObject prefab, Behavior behavior = Behavior.Succeed) {
            string internalId = $"{key}#{Guid.NewGuid():N}";
            _internalIds[key] = internalId;
            _entries[internalId] = new Entry { Prefab = prefab, Behavior = behavior };
            return internalId;
        }

        /// <summary>
        /// Forget all registrations and counts. Pending delayed loads from earlier tests are abandoned.
        /// </summary>
        public void Reset() {
            _entries.Clear();
            _internalIds.Clear();
            ProvideCounts.Clear();
            LoadedCounts.Clear();
            ReleaseCounts.Clear();
        }

        private string Id(string key) => _internalIds.TryGetValue(key, out string id) ? id : key;

        public int GetProvideCount(string key) => ProvideCounts.TryGetValue(Id(key), out int count) ? count : 0;
        public int GetLoadedCount(string key) => LoadedCounts.TryGetValue(Id(key), out int count) ? count : 0;
        public int GetReleaseCount(string key) => ReleaseCounts.TryGetValue(Id(key), out int count) ? count : 0;

        public bool HasPending(string key) =>
            _entries.TryGetValue(Id(key), out Entry entry) && entry.Pending.Count > 0;

        /// <summary>
        /// Complete all pending loads of a <see cref="Behavior.Delayed"/> key.
        /// </summary>
        public void Complete(string key) {
            Entry entry = _entries[Id(key)];
            ProvideHandle[] pending = entry.Pending.ToArray();
            entry.Pending.Clear();
            foreach (ProvideHandle handle in pending) {
                CompleteSuccess(Id(key), handle, entry);
            }
        }

        public override void Provide(ProvideHandle provideHandle) {
            string key = provideHandle.Location.InternalId;
            ProvideCounts[key] = GetProvideCount(key) + 1;

            if (!_entries.TryGetValue(key, out Entry entry)) {
                provideHandle.Complete<GameObject>(null, false, new Exception($"No test prefab registered for {key}."));
                return;
            }

            switch (entry.Behavior) {
                case Behavior.Succeed:
                    CompleteSuccess(key, provideHandle, entry);
                    break;
                case Behavior.Fail:
                    provideHandle.Complete<GameObject>(null, false, new Exception($"Test load failure for {key}."));
                    break;
                case Behavior.Delayed:
                    entry.Pending.Add(provideHandle);
                    provideHandle.SetWaitForCompletionCallback(() => {
                        if (entry.Pending.Remove(provideHandle)) {
                            CompleteSuccess(key, provideHandle, entry);
                        }

                        return true;
                    });
                    break;
            }
        }

        private void CompleteSuccess(string key, ProvideHandle handle, Entry entry) {
            LoadedCounts[key] = GetLoadedCount(key) + 1;
            handle.Complete(entry.Prefab, true, null);
        }

        public override void Release(IResourceLocation location, object obj) {
            string key = location.InternalId;
            ReleaseCounts[key] = GetReleaseCount(key) + 1;
        }
    }
}
