using System;
using NUnit.Framework;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class LogTests
    {
        [SetUp]
        public void SetUp() => Log.Reset();

        // -- Msg (unconditional info) --

        [Test]
        public void Msg_WhenEnabled_AppearsInRecentEntries()
        {
            Log.Msg("Tag", "hello world");
            var entries = Log.GetRecentEntries(1);
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("[Tag] hello world", entries[0]);
        }

        [Test]
        public void Msg_WhenDisabled_DoesNotAppear()
        {
            Log.Enabled = false;
            Log.Msg("Tag", "should not appear");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);
        }

        [Test]
        public void Msg_FormatIsTagBracketedPlusMessage()
        {
            Log.Msg("Navigator", "moved to index 3");
            Assert.AreEqual("[Navigator] moved to index 3", Log.GetRecentEntries(1)[0]);
        }

        // -- MsgIf (explicit gating) --

        [Test]
        public void MsgIf_CategoryFalse_DoesNotAppear()
        {
            Log.MsgIf(false, "Tag", "should not appear");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);
        }

        [Test]
        public void MsgIf_CategoryTrue_AppearsInEntries()
        {
            Log.MsgIf(true, "Cat", "category=true message");
            Assert.AreEqual("[Cat] category=true message", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void MsgIf_MasterDisabled_DoesNotAppearEvenWithCategoryTrue()
        {
            Log.Enabled = false;
            Log.MsgIf(true, "Tag", "should not appear");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);
        }

        // -- Category sugar --

        [Test]
        public void Nav_GatedOnLogNavigation()
        {
            Log.LogNavigation = false;
            Log.Nav("T", "blocked");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);

            Log.LogNavigation = true;
            Log.Nav("T", "allowed");
            Assert.AreEqual("[T] allowed", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void Activation_GatedOnLogActivation()
        {
            Log.LogActivation = false;
            Log.Activation("T", "blocked");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);

            Log.LogActivation = true;
            Log.Activation("T", "allowed");
            Assert.AreEqual("[T] allowed", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void Focus_GatedOnLogFocusTracking()
        {
            Log.LogFocusTracking = false;
            Log.Focus("T", "blocked");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);

            Log.LogFocusTracking = true;
            Log.Focus("T", "allowed");
            Assert.AreEqual("[T] allowed", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void Patch_GatedOnLogPatches()
        {
            Log.LogPatches = false;
            Log.Patch("T", "blocked");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);

            Log.LogPatches = true;
            Log.Patch("T", "allowed");
            Assert.AreEqual("[T] allowed", Log.GetRecentEntries(1)[0]);
        }

        // -- Warn / Error --

        [Test]
        public void Warn_AppearsInRingBuffer()
        {
            Log.Warn("Tag", "trouble ahead");
            Assert.AreEqual("[Tag] trouble ahead", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void Warn_WithException_AppendsExceptionMessage()
        {
            var ex = new InvalidOperationException("bad state");
            Log.Warn("Tag", "op failed", ex);
            Assert.AreEqual("[Tag] op failed: bad state", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void Warn_WhenDisabled_DoesNotAppear()
        {
            Log.Enabled = false;
            Log.Warn("Tag", "muted");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);
        }

        [Test]
        public void Error_AppearsInRingBuffer()
        {
            Log.Error("Tag", "oops");
            Assert.AreEqual("[Tag] oops", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void Error_WithException_AppendsExceptionMessage()
        {
            var ex = new InvalidOperationException("kaboom");
            Log.Error("Tag", "op failed", ex);
            Assert.AreEqual("[Tag] op failed: kaboom", Log.GetRecentEntries(1)[0]);
        }

        [Test]
        public void Error_WhenDisabled_DoesNotAppear()
        {
            Log.Enabled = false;
            Log.Error("Tag", "muted");
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);
        }

        // -- Ring buffer --

        [Test]
        public void RingBuffer_Wraps_ReturnsLastN()
        {
            for (int i = 0; i < 25; i++)
                Log.Msg("T", $"msg{i}");

            var last5 = Log.GetRecentEntries(5);
            Assert.AreEqual(5, last5.Length);
            Assert.AreEqual("[T] msg20", last5[0]);
            Assert.AreEqual("[T] msg24", last5[4]);
        }

        [Test]
        public void RingBuffer_MixesSeverities()
        {
            Log.Msg("T", "info");
            Log.Warn("T", "warning");
            Log.Error("T", "error");

            var entries = Log.GetRecentEntries(3);
            Assert.AreEqual(3, entries.Length);
            Assert.AreEqual("[T] info",    entries[0]);
            Assert.AreEqual("[T] warning", entries[1]);
            Assert.AreEqual("[T] error",   entries[2]);
        }

        [Test]
        public void GetRecentEntries_EmptyBuffer_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, Log.GetRecentEntries(5).Length);
        }

        [Test]
        public void GetRecentEntries_FewerThanRequested_ReturnsAll()
        {
            Log.Msg("T", "only one");
            Assert.AreEqual(1, Log.GetRecentEntries(10).Length);
        }

        [Test]
        public void GetRecentEntries_ReturnsOldestFirst()
        {
            Log.Msg("T", "first");
            Log.Msg("T", "second");
            Log.Msg("T", "third");

            var entries = Log.GetRecentEntries(3);
            Assert.AreEqual("[T] first",  entries[0]);
            Assert.AreEqual("[T] second", entries[1]);
            Assert.AreEqual("[T] third",  entries[2]);
        }
    }
}
