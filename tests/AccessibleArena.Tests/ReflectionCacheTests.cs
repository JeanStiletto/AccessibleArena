using System;
using System.Reflection;
using NUnit.Framework;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class ReflectionCacheTests
    {
        private class SampleHandles
        {
            public FieldInfo RequiredField;
            public PropertyInfo RequiredProp;
            public MethodInfo OptionalMethod;
        }

        // Target types the builder looks up against — mimics a game type.
        private class TargetBase
        {
#pragma warning disable 0414, 0169
            private int _baseField = 0;
#pragma warning restore 0414, 0169
        }

        private class Target : TargetBase
        {
#pragma warning disable 0414, 0169
            private int _privateField = 42;
#pragma warning restore 0414, 0169
            public int PublicProp { get; set; }
            public void OptionalMethod() { }
        }

        private class TargetMissingMethod : TargetBase
        {
#pragma warning disable 0414, 0169
            private int _privateField = 1;
#pragma warning restore 0414, 0169
            public int PublicProp { get; set; }
            // no OptionalMethod — used to prove optional nulls don't fail validation.
        }

        private class TargetMissingRequired : TargetBase
        {
            public int PublicProp { get; set; }
            // no _privateField — required handle null, should fail validation.
        }

        [SetUp]
        public void SetUp() => Log.Reset();

        // -- EnsureInitialized success paths --

        [Test]
        public void EnsureInitialized_AllRequiredPresent_ReturnsTrueAndSetsHandles()
        {
            var cache = BuildCache();
            Assert.IsTrue(cache.EnsureInitialized(typeof(Target)));
            Assert.IsTrue(cache.IsInitialized);
            Assert.IsNotNull(cache.Handles);
            Assert.IsNotNull(cache.Handles.RequiredField);
            Assert.IsNotNull(cache.Handles.RequiredProp);
            Assert.IsNotNull(cache.Handles.OptionalMethod);
        }

        [Test]
        public void EnsureInitialized_OptionalNull_RequiredPresent_StillSucceeds()
        {
            var cache = BuildCache();
            Assert.IsTrue(cache.EnsureInitialized(typeof(TargetMissingMethod)));
            Assert.IsTrue(cache.IsInitialized);
            Assert.IsNull(cache.Handles.OptionalMethod);
        }

        // -- EnsureInitialized failure paths --

        [Test]
        public void EnsureInitialized_RequiredNull_ReturnsFalseAndLeavesUninitialized()
        {
            var cache = BuildCache();
            Assert.IsFalse(cache.EnsureInitialized(typeof(TargetMissingRequired)));
            Assert.IsFalse(cache.IsInitialized);
            Assert.IsNull(cache.Handles);
        }

        [Test]
        public void EnsureInitialized_RequiredNull_LogsNullHandleNames()
        {
            var cache = BuildCache();
            cache.EnsureInitialized(typeof(TargetMissingRequired));

            var entries = Log.GetRecentEntries(10);
            string joined = string.Join("\n", entries);
            StringAssert.Contains("[Subj]", joined);
            StringAssert.Contains("Could not resolve required handles for TestSubject", joined);
            StringAssert.Contains("RequiredField", joined);
        }

        [Test]
        public void EnsureInitialized_NullType_ReturnsFalseWithoutLogging()
        {
            var cache = BuildCache();
            Assert.IsFalse(cache.EnsureInitialized(null));
            Assert.IsFalse(cache.IsInitialized);
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);
        }

        [Test]
        public void EnsureInitialized_BuilderThrows_LogsErrorAndReturnsFalse()
        {
            var cache = new ReflectionCache<SampleHandles>(
                builder: _ => throw new InvalidOperationException("boom"),
                validator: _ => true,
                logTag: "Subj",
                logSubject: "TestSubject");

            Assert.IsFalse(cache.EnsureInitialized(typeof(Target)));
            var entries = Log.GetRecentEntries(5);
            string joined = string.Join("\n", entries);
            StringAssert.Contains("Failed to initialize TestSubject reflection", joined);
            StringAssert.Contains("boom", joined);
        }

        // -- Idempotency --

        [Test]
        public void EnsureInitialized_CalledTwice_BuilderRunsOnce()
        {
            int builderRuns = 0;
            var cache = new ReflectionCache<SampleHandles>(
                builder: t =>
                {
                    builderRuns++;
                    return new SampleHandles
                    {
                        RequiredField = t.GetField("_privateField", ReflectionUtils.PrivateInstance),
                        RequiredProp = t.GetProperty("PublicProp", ReflectionUtils.PublicInstance),
                        OptionalMethod = t.GetMethod("OptionalMethod", ReflectionUtils.PublicInstance),
                    };
                },
                validator: h => h.RequiredField != null && h.RequiredProp != null,
                logTag: "Subj",
                logSubject: "TestSubject");

            Assert.IsTrue(cache.EnsureInitialized(typeof(Target)));
            Assert.IsTrue(cache.EnsureInitialized(typeof(Target)));
            Assert.AreEqual(1, builderRuns);
        }

        // -- Clear --

        [Test]
        public void Clear_ResetsToUninitialized()
        {
            var cache = BuildCache();
            cache.EnsureInitialized(typeof(Target));
            Assert.IsTrue(cache.IsInitialized);

            cache.Clear();
            Assert.IsFalse(cache.IsInitialized);
            Assert.IsNull(cache.Handles);
        }

        [Test]
        public void Clear_AllowsReinitialization()
        {
            int builderRuns = 0;
            var cache = new ReflectionCache<SampleHandles>(
                builder: t =>
                {
                    builderRuns++;
                    return new SampleHandles
                    {
                        RequiredField = t.GetField("_privateField", ReflectionUtils.PrivateInstance),
                        RequiredProp = t.GetProperty("PublicProp", ReflectionUtils.PublicInstance),
                    };
                },
                validator: h => h.RequiredField != null && h.RequiredProp != null,
                logTag: "Subj",
                logSubject: "TestSubject");

            cache.EnsureInitialized(typeof(Target));
            cache.Clear();
            cache.EnsureInitialized(typeof(Target));
            Assert.AreEqual(2, builderRuns);
        }

        // -- Log shape preservation --

        [Test]
        public void EnsureInitialized_Success_LogsCanonicalInitializedLine()
        {
            var cache = BuildCache();
            cache.EnsureInitialized(typeof(Target));

            var entries = Log.GetRecentEntries(5);
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("[Subj] TestSubject reflection initialized", entries[0]);
        }

        private static ReflectionCache<SampleHandles> BuildCache() =>
            new ReflectionCache<SampleHandles>(
                builder: t => new SampleHandles
                {
                    RequiredField = t.GetField("_privateField", ReflectionUtils.PrivateInstance),
                    RequiredProp = t.GetProperty("PublicProp", ReflectionUtils.PublicInstance),
                    OptionalMethod = t.GetMethod("OptionalMethod", ReflectionUtils.PublicInstance),
                },
                validator: h => h.RequiredField != null && h.RequiredProp != null,
                logTag: "Subj",
                logSubject: "TestSubject");
    }

    [TestFixture]
    public class ReflectionWalkTests
    {
        private class WalkBase
        {
#pragma warning disable 0414, 0169
            private int _baseOnlyField = 99;
            private string BaseOnlyProp { get; set; } = "x";
            private void BaseOnlyMethod() { }
#pragma warning restore 0414, 0169
        }

        private class WalkDerived : WalkBase
        {
#pragma warning disable 0414, 0169
            private int _derivedField = 1;
#pragma warning restore 0414, 0169
        }

        [Test]
        public void FindField_OnBase_FoundViaWalk()
        {
            var fi = ReflectionWalk.FindField(typeof(WalkDerived), "_baseOnlyField", ReflectionUtils.PrivateInstance);
            Assert.IsNotNull(fi);
            Assert.AreEqual(typeof(WalkBase), fi.DeclaringType);
        }

        [Test]
        public void FindField_OnDerived_StillFound()
        {
            var fi = ReflectionWalk.FindField(typeof(WalkDerived), "_derivedField", ReflectionUtils.PrivateInstance);
            Assert.IsNotNull(fi);
            Assert.AreEqual(typeof(WalkDerived), fi.DeclaringType);
        }

        [Test]
        public void FindField_Missing_ReturnsNull()
        {
            var fi = ReflectionWalk.FindField(typeof(WalkDerived), "_nope", ReflectionUtils.PrivateInstance);
            Assert.IsNull(fi);
        }

        [Test]
        public void FindProperty_OnBase_FoundViaWalk()
        {
            var pi = ReflectionWalk.FindProperty(typeof(WalkDerived), "BaseOnlyProp", ReflectionUtils.PrivateInstance);
            Assert.IsNotNull(pi);
            Assert.AreEqual(typeof(WalkBase), pi.DeclaringType);
        }

        [Test]
        public void FindMethod_OnBase_FoundViaWalk()
        {
            var mi = ReflectionWalk.FindMethod(typeof(WalkDerived), "BaseOnlyMethod", ReflectionUtils.PrivateInstance);
            Assert.IsNotNull(mi);
            Assert.AreEqual(typeof(WalkBase), mi.DeclaringType);
        }

        [Test]
        public void FindField_NullType_ReturnsNull()
        {
            Assert.IsNull(ReflectionWalk.FindField(null, "x", ReflectionUtils.PrivateInstance));
        }
    }
}
