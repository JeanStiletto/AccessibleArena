using NUnit.Framework;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class DebugConfigTests
    {
        [SetUp]
        public void SetUp() => DebugConfig.Reset();

        [Test]
        public void Log_WhenEnabled_AppearsInRecentEntries()
        {
            DebugConfig.Log("Tag", "hello world");
            var entries = DebugConfig.GetRecentEntries(1);
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("[Tag] hello world", entries[0]);
        }

        [Test]
        public void Log_WhenDisabled_DoesNotAppear()
        {
            DebugConfig.DebugEnabled = false;
            DebugConfig.Log("Tag", "should not appear");
            Assert.AreEqual(0, DebugConfig.GetRecentEntries(5).Length);
        }

        [Test]
        public void LogIf_CategoryFalse_DoesNotAppear()
        {
            DebugConfig.LogIf(false, "Tag", "category=false message");
            Assert.AreEqual(0, DebugConfig.GetRecentEntries(5).Length);
        }

        [Test]
        public void LogIf_CategoryTrue_AppearsInEntries()
        {
            DebugConfig.LogIf(true, "Cat", "category=true message");
            var entries = DebugConfig.GetRecentEntries(1);
            Assert.AreEqual("[Cat] category=true message", entries[0]);
        }

        [Test]
        public void RingBuffer_Wraps_ReturnsLastN()
        {
            // Fill past the 20-entry ring buffer capacity
            for (int i = 0; i < 25; i++)
                DebugConfig.Log("T", $"msg{i}");

            var last5 = DebugConfig.GetRecentEntries(5);
            Assert.AreEqual(5, last5.Length);
            Assert.AreEqual("[T] msg20", last5[0]);
            Assert.AreEqual("[T] msg24", last5[4]);
        }

        [Test]
        public void GetRecentEntries_EmptyBuffer_ReturnsEmptyArray()
        {
            var entries = DebugConfig.GetRecentEntries(5);
            Assert.AreEqual(0, entries.Length);
        }

        [Test]
        public void GetRecentEntries_FewerThanRequested_ReturnsAll()
        {
            DebugConfig.Log("T", "only one");
            var entries = DebugConfig.GetRecentEntries(10);
            Assert.AreEqual(1, entries.Length);
        }

        [Test]
        public void EntryFormat_IsTagBracketedPlusMessage()
        {
            DebugConfig.Log("Navigator", "moved to index 3");
            string entry = DebugConfig.GetRecentEntries(1)[0];
            Assert.AreEqual("[Navigator] moved to index 3", entry);
        }

        [Test]
        public void GetRecentEntries_ReturnsOldestFirst()
        {
            DebugConfig.Log("T", "first");
            DebugConfig.Log("T", "second");
            DebugConfig.Log("T", "third");

            var entries = DebugConfig.GetRecentEntries(3);
            Assert.AreEqual("[T] first",  entries[0]);
            Assert.AreEqual("[T] second", entries[1]);
            Assert.AreEqual("[T] third",  entries[2]);
        }
    }
}
