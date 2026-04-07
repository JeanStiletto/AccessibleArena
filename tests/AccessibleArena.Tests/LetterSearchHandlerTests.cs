using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class LetterSearchHandlerTests
    {
        private LetterSearchHandler _handler;

        [SetUp]
        public void SetUp()
        {
            Time.time = 0f;
            _handler = new LetterSearchHandler();
        }

        private static readonly IReadOnlyList<string> Fruits = new[]
        {
            "Apple", "Apricot", "Banana", "Cherry", "Strawberry", "Stone Fruit"
        };

        [Test]
        public void SingleLetter_FindsFirstMatch_CaseInsensitive()
        {
            int idx = _handler.HandleKey('b', Fruits, 0);
            Assert.AreEqual(2, idx); // "Banana"
        }

        [Test]
        public void SingleLetter_NoMatch_ReturnsMinus1()
        {
            int idx = _handler.HandleKey('z', Fruits, 0);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void SameLetter_Repeated_CyclesToNextMatch()
        {
            // 'A' → index 0 (Apple)
            int first = _handler.HandleKey('a', Fruits, 0);
            Assert.AreEqual(0, first);

            // 'A' again → index 1 (Apricot)
            int second = _handler.HandleKey('a', Fruits, 0);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void TwoLetters_WithinTimeout_BuildsPrefix()
        {
            // 'S' → finds "Strawberry" at 4
            _handler.HandleKey('s', Fruits, 0);

            // 'T' within timeout → now searching "ST" → "Strawberry" still, "Stone Fruit" is 5
            int idx = _handler.HandleKey('t', Fruits, 0);
            // "Strawberry" starts with "ST", so index 4
            Assert.AreEqual(4, idx);
        }

        [Test]
        public void BufferTimeout_ResetsToSingleChar()
        {
            // Press 'S' at t=0
            _handler.HandleKey('s', Fruits, 0);

            // Advance time past timeout
            Time.time = 2.0f;

            // Press 'T' — buffer should reset, searching "T" alone (no match in Fruits)
            int idx = _handler.HandleKey('t', Fruits, 0);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void BufferPreserved_WithinTimeout()
        {
            _handler.HandleKey('s', Fruits, 0);
            Time.time = 0.5f; // within 1s timeout
            int idx = _handler.HandleKey('t', Fruits, 0);
            // "ST" prefix → Strawberry (4) or Stone Fruit (5)
            Assert.AreEqual(4, idx);
        }

        [Test]
        public void Clear_ResetsBuffer()
        {
            _handler.HandleKey('a', Fruits, 0); // buffer = "A"
            _handler.Clear();
            Assert.AreEqual("", _handler.Buffer);

            // Next key starts fresh
            int idx = _handler.HandleKey('b', Fruits, 0);
            Assert.AreEqual(2, idx); // "Banana"
        }

        [Test]
        public void EmptyList_ReturnsMinus1_NoException()
        {
            var empty = new string[0];
            Assert.DoesNotThrow(() =>
            {
                int result = _handler.HandleKey('a', empty, 0);
                Assert.AreEqual(-1, result);
            });
        }

        [Test]
        public void WrapAround_FindsMatchPastEndOfList()
        {
            var items = new[] { "Zebra", "Apple", "Banana" };
            // Start at index 2 (Banana), search 'A' → should wrap to index 1 (Apple)
            int idx = _handler.HandleKey('a', items, 2);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void SameLetter_CycleWrapsAroundList()
        {
            var items = new[] { "Alpha", "Beta", "Alpha2" };
            // First 'A' → index 0 (Alpha)
            _handler.HandleKey('a', items, 0);
            // Second 'A' → index 2 (Alpha2, cycling from index 1)
            int idx = _handler.HandleKey('a', items, 0);
            Assert.AreEqual(2, idx);
        }

        [Test]
        public void NullItemInList_Skipped_ReturnsNextNonNullMatch()
        {
            var items = new[] { null, "Apple", "Apricot" };
            int idx = _handler.HandleKey('a', items, 0);
            Assert.AreEqual(1, idx);
        }
    }
}
