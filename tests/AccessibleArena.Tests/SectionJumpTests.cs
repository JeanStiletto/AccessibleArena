using System.Collections.Generic;
using NUnit.Framework;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class SectionJumpTests
    {
        // Sections:      [0..2]=1, [3..4]=2, [5]=7, [6..8]=9
        private static readonly List<long> Keys = new List<long> { 1, 1, 1, 2, 2, 7, 9, 9, 9 };

        [Test]
        public void SectionStart_MidRun_ReturnsRunStart()
        {
            Assert.AreEqual(0, SectionJump.SectionStart(Keys, 2));
            Assert.AreEqual(3, SectionJump.SectionStart(Keys, 4));
            Assert.AreEqual(5, SectionJump.SectionStart(Keys, 5));
            Assert.AreEqual(6, SectionJump.SectionStart(Keys, 8));
        }

        [Test]
        public void Next_JumpsToFollowingRunStart()
        {
            Assert.AreEqual(3, SectionJump.Next(Keys, 0));
            Assert.AreEqual(3, SectionJump.Next(Keys, 2));
            Assert.AreEqual(5, SectionJump.Next(Keys, 3));
            Assert.AreEqual(6, SectionJump.Next(Keys, 5));
        }

        [Test]
        public void Next_InLastSection_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, SectionJump.Next(Keys, 6));
            Assert.AreEqual(-1, SectionJump.Next(Keys, 8));
        }

        [Test]
        public void Previous_MidRun_GoesToOwnRunStart()
        {
            Assert.AreEqual(0, SectionJump.Previous(Keys, 1));
            Assert.AreEqual(3, SectionJump.Previous(Keys, 4));
            Assert.AreEqual(6, SectionJump.Previous(Keys, 8));
        }

        [Test]
        public void Previous_AtRunStart_GoesToPreviousRunStart()
        {
            Assert.AreEqual(0, SectionJump.Previous(Keys, 3));
            Assert.AreEqual(3, SectionJump.Previous(Keys, 5));
            Assert.AreEqual(5, SectionJump.Previous(Keys, 6));
        }

        [Test]
        public void Previous_AtVeryFirstStart_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, SectionJump.Previous(Keys, 0));
        }

        [Test]
        public void SingleSection_HasNoTargets()
        {
            var uniform = new List<long> { 4, 4, 4 };
            Assert.AreEqual(-1, SectionJump.Next(uniform, 1));
            Assert.AreEqual(0, SectionJump.Previous(uniform, 2));
            Assert.AreEqual(-1, SectionJump.Previous(uniform, 0));
        }

        [Test]
        public void EmptyOrNull_ReturnsMinusOne()
        {
            var empty = new List<long>();
            Assert.AreEqual(-1, SectionJump.Next(empty, 0));
            Assert.AreEqual(-1, SectionJump.Previous(empty, 0));
            Assert.AreEqual(-1, SectionJump.SectionStart(empty, 0));
            Assert.AreEqual(-1, SectionJump.Next(null, 0));
            Assert.AreEqual(-1, SectionJump.Previous(null, 0));
        }

        [Test]
        public void OutOfRangeIndex_IsClamped()
        {
            Assert.AreEqual(3, SectionJump.Next(Keys, -5));
            Assert.AreEqual(6, SectionJump.Previous(Keys, 99));
            Assert.AreEqual(0, SectionJump.SectionStart(Keys, -1));
        }

        [Test]
        public void SingleElementList_BehavesLikeOneSection()
        {
            var one = new List<long> { 3 };
            Assert.AreEqual(-1, SectionJump.Next(one, 0));
            Assert.AreEqual(-1, SectionJump.Previous(one, 0));
            Assert.AreEqual(0, SectionJump.SectionStart(one, 0));
        }
    }
}
