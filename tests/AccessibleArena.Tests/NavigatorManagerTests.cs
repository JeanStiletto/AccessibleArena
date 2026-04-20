using System.Collections.Generic;
using NUnit.Framework;
using NSubstitute;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class NavigatorManagerTests
    {
        private NavigatorManager _mgr;

        [SetUp]
        public void SetUp() => _mgr = new NavigatorManager();

        private static IScreenNavigator MakeNav(string id, int priority, bool activeAfterUpdate = false)
        {
            var nav = Substitute.For<IScreenNavigator>();
            nav.NavigatorId.Returns(id);
            nav.Priority.Returns(priority);
            nav.IsActive.Returns(false);
            nav.GetNavigableGameObjects().Returns(new List<GameObject>());
            if (activeAfterUpdate)
                nav.When(n => n.Update()).Do(_ => nav.IsActive.Returns(true));
            return nav;
        }

        // --- Register / priority sorting ---

        [Test]
        public void Register_SortsByPriorityDescending()
        {
            var low = MakeNav("low", 10);
            var high = MakeNav("high", 100);
            var mid = MakeNav("mid", 50);
            _mgr.Register(low);
            _mgr.Register(high);
            _mgr.Register(mid);

            var all = _mgr.GetAllNavigators();
            Assert.AreEqual("high", all[0].NavigatorId);
            Assert.AreEqual("mid", all[1].NavigatorId);
            Assert.AreEqual("low", all[2].NavigatorId);
        }

        [Test]
        public void RegisterAll_AddsAll()
        {
            _mgr.RegisterAll(MakeNav("a", 1), MakeNav("b", 2), MakeNav("c", 3));
            Assert.AreEqual(3, _mgr.GetAllNavigators().Count);
        }

        // --- Activation via Update() ---

        [Test]
        public void Update_NoActive_ActivatesFirstReadyNavigator()
        {
            var hi = MakeNav("hi", 100, activeAfterUpdate: true);
            var lo = MakeNav("lo", 10, activeAfterUpdate: true);
            _mgr.RegisterAll(hi, lo);

            _mgr.Update();

            Assert.AreSame(hi, _mgr.ActiveNavigator);
            lo.DidNotReceive().Update();
        }

        [Test]
        public void Update_NoActive_NoneReady_LeavesActiveNull()
        {
            _mgr.RegisterAll(MakeNav("a", 10), MakeNav("b", 5));
            _mgr.Update();
            Assert.IsNull(_mgr.ActiveNavigator);
        }

        // --- Preemption ---

        [Test]
        public void Update_HigherPriorityNavigatorPreemptsActive()
        {
            var lowActive = MakeNav("low", 10, activeAfterUpdate: true);
            _mgr.Register(lowActive);
            _mgr.Update(); // lowActive becomes active
            Assert.AreSame(lowActive, _mgr.ActiveNavigator);

            // Now add a high-priority navigator that becomes active on next Update
            var high = MakeNav("high", 100, activeAfterUpdate: true);
            _mgr.Register(high);

            _mgr.Update();

            lowActive.Received().Deactivate();
            Assert.AreSame(high, _mgr.ActiveNavigator);
        }

        [Test]
        public void Update_LowerPriorityDoesNotPreempt()
        {
            var high = MakeNav("high", 100, activeAfterUpdate: true);
            _mgr.Register(high);
            _mgr.Update();
            Assert.AreSame(high, _mgr.ActiveNavigator);

            var low = MakeNav("low", 10, activeAfterUpdate: true);
            _mgr.Register(low);
            _mgr.Update();

            // Low-priority nav should never be polled while a higher one is active
            low.DidNotReceive().Update();
            Assert.AreSame(high, _mgr.ActiveNavigator);
        }

        [Test]
        public void Update_ActiveDeactivatesItself_ClearsActive()
        {
            var nav = Substitute.For<IScreenNavigator>();
            nav.NavigatorId.Returns("nav");
            nav.Priority.Returns(10);
            nav.GetNavigableGameObjects().Returns(new List<GameObject>());
            bool shouldBeActive = true;
            nav.When(n => n.Update()).Do(_ => nav.IsActive.Returns(shouldBeActive));

            _mgr.Register(nav);
            _mgr.Update();
            Assert.AreSame(nav, _mgr.ActiveNavigator);

            // Next Update(): nav's own Update flips itself inactive
            shouldBeActive = false;
            _mgr.Update();

            Assert.IsNull(_mgr.ActiveNavigator);
        }

        // --- OnSceneChanged ---

        [Test]
        public void OnSceneChanged_NotifiesAllNavigatorsAndUpdatesCurrentScene()
        {
            var a = MakeNav("a", 10);
            var b = MakeNav("b", 20);
            _mgr.RegisterAll(a, b);

            _mgr.OnSceneChanged("DuelScene");

            Assert.AreEqual("DuelScene", _mgr.CurrentScene);
            a.Received(1).OnSceneChanged("DuelScene");
            b.Received(1).OnSceneChanged("DuelScene");
        }

        [Test]
        public void OnSceneChanged_ActiveNavigatorInactive_ClearsActive()
        {
            var nav = MakeNav("nav", 10, activeAfterUpdate: true);
            _mgr.Register(nav);
            _mgr.Update(); // active
            // When scene changes, stub nav will flip back to inactive
            nav.IsActive.Returns(false);

            _mgr.OnSceneChanged("MenuScene");

            Assert.IsNull(_mgr.ActiveNavigator);
        }

        // --- DeactivateCurrent ---

        [Test]
        public void DeactivateCurrent_CallsDeactivateAndClearsActive()
        {
            var nav = MakeNav("nav", 10, activeAfterUpdate: true);
            _mgr.Register(nav);
            _mgr.Update();

            _mgr.DeactivateCurrent();

            nav.Received(1).Deactivate();
            Assert.IsNull(_mgr.ActiveNavigator);
        }

        // --- RequestActivation ---

        [Test]
        public void RequestActivation_UnknownId_ReturnsFalse()
        {
            _mgr.Register(MakeNav("known", 10));
            Assert.IsFalse(_mgr.RequestActivation("unknown"));
        }

        [Test]
        public void RequestActivation_DeactivatesCurrentAndActivatesTarget()
        {
            var current = MakeNav("current", 10, activeAfterUpdate: true);
            _mgr.Register(current);
            _mgr.Update();
            Assert.AreSame(current, _mgr.ActiveNavigator);

            var target = MakeNav("target", 5, activeAfterUpdate: true);
            _mgr.Register(target);

            bool ok = _mgr.RequestActivation("target");

            Assert.IsTrue(ok);
            current.Received().Deactivate();
            Assert.AreSame(target, _mgr.ActiveNavigator);
        }

        [Test]
        public void RequestActivation_TargetFailsToActivate_RestoresPrevious()
        {
            var previous = MakeNav("prev", 10, activeAfterUpdate: true);
            _mgr.Register(previous);
            _mgr.Update();

            // Target that never activates, even after Update()
            var dead = MakeNav("dead", 5);
            _mgr.Register(dead);

            bool ok = _mgr.RequestActivation("dead");

            Assert.IsFalse(ok);
            // Previous reactivates because its Update() still returns IsActive = true
            Assert.AreSame(previous, _mgr.ActiveNavigator);
        }

        // --- Lookup helpers ---

        [Test]
        public void GetNavigator_ById_ReturnsMatch()
        {
            var a = MakeNav("alpha", 1);
            _mgr.Register(a);
            Assert.AreSame(a, _mgr.GetNavigator("alpha"));
            Assert.IsNull(_mgr.GetNavigator("missing"));
        }

        [Test]
        public void IsNavigatorActive_ReturnsTrueOnlyForActiveOne()
        {
            var nav = MakeNav("nav", 10, activeAfterUpdate: true);
            _mgr.Register(nav);
            Assert.IsFalse(_mgr.IsNavigatorActive("nav"));
            _mgr.Update();
            Assert.IsTrue(_mgr.IsNavigatorActive("nav"));
            Assert.IsFalse(_mgr.IsNavigatorActive("other"));
        }

        [Test]
        public void HasActiveNavigator_ReflectsState()
        {
            Assert.IsFalse(_mgr.HasActiveNavigator);
            var nav = MakeNav("nav", 10, activeAfterUpdate: true);
            _mgr.Register(nav);
            _mgr.Update();
            Assert.IsTrue(_mgr.HasActiveNavigator);
        }
    }
}
