using NUnit.Framework;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class CodexTextSplitterTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  \n \r\n ")]
        public void Split_Empty_ReturnsNoBlocks(string text)
        {
            Assert.IsEmpty(CodexTextSplitter.Split(text));
        }

        [Test]
        public void Split_BlankLineSeparatesParagraphs()
        {
            var blocks = CodexTextSplitter.Split("First short line.\n\nSecond short line.");
            Assert.AreEqual(new[] { "First short line.", "Second short line." }, blocks);
        }

        [Test]
        public void Split_LongLineStandsAlone()
        {
            string paragraph = new string('a', CodexTextSplitter.LongLineLength);
            var blocks = CodexTextSplitter.Split("Heading\n" + paragraph + "\nAfter");
            Assert.AreEqual(new[] { "Heading", paragraph, "After" }, blocks);
        }

        [Test]
        public void Split_ShortLinesGroupWithSpeechPauses()
        {
            // Credits style: a heading followed by names, one per line.
            var blocks = CodexTextSplitter.Split("Lead Designer\nAlice Example\nBob Sample");
            Assert.AreEqual(new[] { "Lead Designer. Alice Example. Bob Sample" }, blocks);
        }

        [Test]
        public void Split_ExistingPunctuationIsNotDoubled()
        {
            var blocks = CodexTextSplitter.Split("Step one.\nStep two:\nStep three");
            Assert.AreEqual(new[] { "Step one. Step two: Step three" }, blocks);
        }

        [Test]
        public void Split_GroupedBlockRespectsMaxLength()
        {
            var lines = new string[20];
            for (int i = 0; i < lines.Length; i++) lines[i] = "Name number " + i.ToString("00") + " here";  // 23 chars each
            var blocks = CodexTextSplitter.Split(string.Join("\n", lines));

            Assert.Greater(blocks.Count, 1);
            foreach (var block in blocks)
                Assert.LessOrEqual(block.Length, CodexTextSplitter.MaxBlockLength);
            Assert.IsTrue(blocks[0].StartsWith("Name number 00 here"));
            Assert.IsTrue(blocks[blocks.Count - 1].EndsWith("Name number 19 here"));
        }

        [Test]
        public void Split_BrTagsAndWindowsLineEndingsAreLineBreaks()
        {
            var blocks = CodexTextSplitter.Split("One<br>Two<br/>\r\n\r\nThree");
            Assert.AreEqual(new[] { "One. Two", "Three" }, blocks);
        }

        [Test]
        public void Split_CollapsesInnerWhitespace()
        {
            var blocks = CodexTextSplitter.Split("  Too   many \t spaces  ");
            Assert.AreEqual(new[] { "Too many spaces" }, blocks);
        }
    }
}
