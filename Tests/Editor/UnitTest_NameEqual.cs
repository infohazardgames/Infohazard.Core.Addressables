// This file is part of the Infohazard.Core.Addressables package.
// Copyright (c) 2026-present Val Miller (Infohazard Games).

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Infohazard.Core.Addressables.Tests.Editor {
    public class UnitTest_NameEqual {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown() {
            foreach (GameObject obj in _created) {
                if (obj) Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        private GameObject Create(string name) {
            var obj = new GameObject(name);
            _created.Add(obj);
            return obj;
        }

        [Test]
        public void SameReference_IsEqual() {
            GameObject a = Create("A");
            Assert.That(AddressableUtil.NameEqual(a, a), Is.True);
        }

        [Test]
        public void BothNull_IsEqual() {
            Assert.That(AddressableUtil.NameEqual<GameObject>(null, null), Is.True);
        }

        [Test]
        public void OneNull_IsNotEqual() {
            GameObject a = Create("A");
            Assert.That(AddressableUtil.NameEqual(a, null), Is.False);
            Assert.That(AddressableUtil.NameEqual(null, a), Is.False);
        }

        [Test]
        public void DestroyedObject_EqualsNull() {
            GameObject a = Create("A");
            Object.DestroyImmediate(a);
            Assert.That(AddressableUtil.NameEqual(a, null), Is.True);
        }

        [Test]
        public void DestroyedObject_DoesNotEqualLiveObject() {
            GameObject a = Create("A");
            GameObject b = Create("A");
            Object.DestroyImmediate(a);
            Assert.That(AddressableUtil.NameEqual(a, b), Is.False);
        }

        [Test]
        public void DifferentInstancesWithSameName_AreEqual() {
            Assert.That(AddressableUtil.NameEqual(Create("Same"), Create("Same")), Is.True);
        }

        [Test]
        public void DifferentNames_AreNotEqual() {
            Assert.That(AddressableUtil.NameEqual(Create("A"), Create("B")), Is.False);
        }
    }
}
