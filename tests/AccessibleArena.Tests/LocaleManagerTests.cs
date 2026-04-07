using System.Collections.Generic;
using NUnit.Framework;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class LocaleManagerTests
    {
        private static LocaleManager Make(
            Dictionary<string, string> active,
            Dictionary<string, string> fallback = null,
            string language = "en")
            => LocaleManager.CreateForTesting(active, fallback, language);

        // --- Get() ---

        [Test]
        public void Get_KeyInActive_ReturnsValue()
        {
            var lm = Make(new Dictionary<string, string> { { "Foo", "Bar" } });
            Assert.AreEqual("Bar", lm.Get("Foo"));
        }

        [Test]
        public void Get_KeyOnlyInFallback_ReturnsFallbackValue()
        {
            var lm = Make(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "Foo", "FallbackBar" } });
            Assert.AreEqual("FallbackBar", lm.Get("Foo"));
        }

        [Test]
        public void Get_KeyAbsent_ReturnsKeyName()
        {
            var lm = Make(new Dictionary<string, string>());
            Assert.AreEqual("Missing", lm.Get("Missing"));
        }

        [Test]
        public void Get_ActiveOverridesFallback()
        {
            var lm = Make(
                new Dictionary<string, string> { { "Foo", "Active" } },
                new Dictionary<string, string> { { "Foo", "Fallback" } });
            Assert.AreEqual("Active", lm.Get("Foo"));
        }

        // --- Format() ---

        [Test]
        public void Format_SubstitutesArgument()
        {
            var lm = Make(new Dictionary<string, string> { { "Greeting", "Hello {0}!" } });
            Assert.AreEqual("Hello World!", lm.Format("Greeting", "World"));
        }

        [Test]
        public void Format_BadTemplate_ReturnsTemplateAsIs()
        {
            var lm = Make(new Dictionary<string, string> { { "T", "No placeholder" } });
            // string.Format with no {0} and args provided doesn't throw — just returns template
            Assert.AreEqual("No placeholder", lm.Format("T", "ignored"));
        }

        // --- HasKey() ---

        [Test]
        public void HasKey_InActive_ReturnsTrue()
        {
            var lm = Make(new Dictionary<string, string> { { "K", "V" } });
            Assert.IsTrue(lm.HasKey("K"));
        }

        [Test]
        public void HasKey_InFallback_ReturnsTrue()
        {
            var lm = Make(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "K", "V" } });
            Assert.IsTrue(lm.HasKey("K"));
        }

        [Test]
        public void HasKey_Absent_ReturnsFalse()
        {
            var lm = Make(new Dictionary<string, string>());
            Assert.IsFalse(lm.HasKey("Nope"));
        }

        // --- Plural / OneOther (en) ---

        [Test]
        public void Plural_OneOther_CountOne_UsesOneSuffix()
        {
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} card" },
                    { "Card_Format", "{0} cards" }
                },
                language: "en");
            Assert.AreEqual("1 card", lm.Plural(1, "Card"));
        }

        [Test]
        public void Plural_OneOther_CountZero_UsesFormatSuffix()
        {
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} card" },
                    { "Card_Format", "{0} cards" }
                },
                language: "en");
            Assert.AreEqual("0 cards", lm.Plural(0, "Card"));
        }

        [Test]
        public void Plural_OneOther_CountTwo_UsesFormatSuffix()
        {
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} card" },
                    { "Card_Format", "{0} cards" }
                },
                language: "en");
            Assert.AreEqual("2 cards", lm.Plural(2, "Card"));
        }

        // --- Plural / Slavic (ru) ---

        [Test]
        public void Plural_Slavic_Count1_UsesOne()
        {
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} карта" },
                    { "Card_Few", "{0} карты" },
                    { "Card_Format", "{0} карт" }
                },
                language: "ru");
            Assert.AreEqual("1 карта", lm.Plural(1, "Card"));
        }

        [Test]
        public void Plural_Slavic_Count2_UsesFew()
        {
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} карта" },
                    { "Card_Few", "{0} карты" },
                    { "Card_Format", "{0} карт" }
                },
                language: "ru");
            Assert.AreEqual("2 карты", lm.Plural(2, "Card"));
        }

        [Test]
        public void Plural_Slavic_Count5_UsesFormat()
        {
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} карта" },
                    { "Card_Few", "{0} карты" },
                    { "Card_Format", "{0} карт" }
                },
                language: "ru");
            Assert.AreEqual("5 карт", lm.Plural(5, "Card"));
        }

        [Test]
        public void Plural_Slavic_Count11_UsesFormat_NotFew()
        {
            // 11 % 10 == 1 but 11 % 100 == 11, which is the Slavic exception
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} карта" },
                    { "Card_Few", "{0} карты" },
                    { "Card_Format", "{0} карт" }
                },
                language: "ru");
            Assert.AreEqual("11 карт", lm.Plural(11, "Card"));
        }

        [Test]
        public void Plural_Slavic_Count21_UsesOne()
        {
            // 21 % 10 == 1, 21 % 100 == 21 (not 11-14), so One
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_One", "{0} карта" },
                    { "Card_Few", "{0} карты" },
                    { "Card_Format", "{0} карт" }
                },
                language: "ru");
            Assert.AreEqual("21 карта", lm.Plural(21, "Card"));
        }

        // --- Plural / NoPluralForm (ja) ---

        [Test]
        public void Plural_NoPluralForm_Count1_UsesFormat()
        {
            var lm = Make(
                new Dictionary<string, string>
                {
                    { "Card_Format", "{0}枚" }
                },
                language: "ja");
            Assert.AreEqual("1枚", lm.Plural(1, "Card"));
        }

        // --- TryParseNumberWord() ---

        [Test]
        public void TryParseNumberWord_MatchesWholeWord()
        {
            var lm = Make(new Dictionary<string, string>
            {
                { "NumberWords", "one=1,two=2,three=3" }
            });
            Assert.AreEqual(2, lm.TryParseNumberWord("take two cards"));
        }

        [Test]
        public void TryParseNumberWord_NoMatch_ReturnsMinus1()
        {
            var lm = Make(new Dictionary<string, string>
            {
                { "NumberWords", "one=1,two=2" }
            });
            Assert.AreEqual(-1, lm.TryParseNumberWord("take five cards"));
        }

        [Test]
        public void TryParseNumberWord_PartialWordDoesNotMatch()
        {
            // "one" inside "stone" should NOT match
            var lm = Make(new Dictionary<string, string>
            {
                { "NumberWords", "one=1" }
            });
            Assert.AreEqual(-1, lm.TryParseNumberWord("stone cold"));
        }

        [Test]
        public void TryParseNumberWord_CaseInsensitive()
        {
            var lm = Make(new Dictionary<string, string>
            {
                { "NumberWords", "one=1" }
            });
            Assert.AreEqual(1, lm.TryParseNumberWord("ONE card"));
        }

        [Test]
        public void TryParseNumberWord_EmptyText_ReturnsMinus1()
        {
            var lm = Make(new Dictionary<string, string>
            {
                { "NumberWords", "one=1" }
            });
            Assert.AreEqual(-1, lm.TryParseNumberWord(""));
            Assert.AreEqual(-1, lm.TryParseNumberWord(null));
        }
    }
}
