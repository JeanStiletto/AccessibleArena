using System.Collections.Generic;
using NUnit.Framework;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    /// <summary>
    /// Tests for LocaleManager.ParseFlatJson — the hand-written JSON parser.
    /// </summary>
    [TestFixture]
    public class LocaleManagerJsonTests
    {
        private static Dictionary<string, string> Parse(string json)
        {
            var dict = new Dictionary<string, string>();
            LocaleManager.ParseFlatJson(json, dict);
            return dict;
        }

        [Test]
        public void Parse_SimpleKeyValue_Succeeds()
        {
            var d = Parse("{\"hello\": \"world\"}");
            Assert.That(d["hello"], Is.EqualTo("world"));
        }

        [Test]
        public void Parse_MultipleKeys_AllPresent()
        {
            var d = Parse("{\"a\": \"1\", \"b\": \"2\", \"c\": \"3\"}");
            Assert.That(d["a"], Is.EqualTo("1"));
            Assert.That(d["b"], Is.EqualTo("2"));
            Assert.That(d["c"], Is.EqualTo("3"));
        }

        [Test]
        public void Parse_EscapedQuote_InValue()
        {
            var d = Parse("{\"msg\": \"say \\\"hi\\\"\"}");
            Assert.That(d["msg"], Is.EqualTo("say \"hi\""));
        }

        [Test]
        public void Parse_EscapedNewline_InValue()
        {
            var d = Parse("{\"lines\": \"one\\ntwo\"}");
            Assert.That(d["lines"], Is.EqualTo("one\ntwo"));
        }

        [Test]
        public void Parse_UnicodeEscape_InValue()
        {
            var d = Parse("{\"char\": \"\\u0041\"}"); // \u0041 = 'A'
            Assert.That(d["char"], Is.EqualTo("A"));
        }

        [Test]
        public void Parse_EmptyObject_YieldsEmptyDict()
        {
            var d = Parse("{}");
            Assert.That(d.Count, Is.EqualTo(0));
        }

        [Test]
        public void Parse_EmptyStringValue_Succeeds()
        {
            var d = Parse("{\"k\": \"\"}");
            Assert.That(d["k"], Is.EqualTo(""));
        }

        [Test]
        public void Parse_MalformedJson_DoesNotThrow()
        {
            // Should not throw — parser returns whatever it managed to read
            Assert.DoesNotThrow(() => Parse("{\"a\": \"b\", broken"));
        }

        [Test]
        public void Parse_BackslashEscape_InValue()
        {
            var d = Parse("{\"path\": \"C:\\\\Users\\\\\"}");
            Assert.That(d["path"], Is.EqualTo("C:\\Users\\"));
        }

        [Test]
        public void Parse_TabEscape_InValue()
        {
            var d = Parse("{\"tab\": \"col1\\tcol2\"}");
            Assert.That(d["tab"], Is.EqualTo("col1\tcol2"));
        }
    }
}
