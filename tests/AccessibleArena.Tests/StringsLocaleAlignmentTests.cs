using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    /// <summary>
    /// Audits alignment between the locale JSON files (lang/*.json) and the
    /// locale keys actually referenced from the C# code.
    ///
    /// Fails on:
    ///   - A key used in code but missing from lang/en.json.
    ///   - A key present in lang/en.json but not referenced anywhere in src/.
    ///   - A non-English locale with a different key set than en.json.
    ///
    /// Keys are extracted by text scan of src/ for L.Get("…"), L.Format("…", ...)
    /// and LocaleManager.Instance.Get/Format calls. Keys dynamically built from
    /// variables (e.g. constants or enum-to-key mappings) are allow-listed below
    /// so they don't produce false positives.
    /// </summary>
    [TestFixture]
    public class StringsLocaleAlignmentTests
    {
        // Matches L.Get("…"), L.Format("…", ...), LocaleManager.Instance.Get/Format,
        // LocaleManager.Instance?.Get/Format (null-conditional), and the helper
        // WithHint(…, "HintKey"), WithDetail(…, "DetailKey").
        private static readonly Regex LocCallRegex = new Regex(
            @"\b(?:L|LocaleManager\.Instance)\??\.(?:Get|Format|GetOrNull|GetHasKey)\(\s*""([^""\\]*(?:\\.[^""\\]*)*)""",
            RegexOptions.Compiled);

        private static readonly Regex WithHintRegex = new Regex(
            @"\b(?:Strings\.)?(?:WithHint|WithDetail)\(\s*[^,]+,\s*""([^""\\]*(?:\\.[^""\\]*)*)""",
            RegexOptions.Compiled);

        /// <summary>
        /// Prefixes of keys that are looked up dynamically (string concatenation,
        /// dictionary-backed mappings, enum-to-key switches). The regex scan can't
        /// see these, so they would otherwise show up as orphans.
        /// </summary>
        private static readonly string[] KnownIndirectPrefixes = new[]
        {
            "Lang",              // L.Get("Lang" + localeCode) in language picker
            "PhaseStop_",        // PriorityController.GetPhaseName
            "NPE_Hint_",         // NPETutorialTextProvider dictionary
            "NPE_DialogHint_",   // NPETutorialTextProvider dictionary
            "NPE_Tooltip_",      // NPE tooltip dictionary
            "BrowserHint_",      // switch by browser type
            "Objective",         // EventAccessor objective-type to key
            "Designation_",      // DuelAnnouncer designation icons
            "RegistrationError_",// EventSystemPatch builds key from AccountError.ErrorTypes (Email, Password, DisplayName, Token, Age)
        };

        /// <summary>
        /// Individual keys that are referenced but not as literal Get/Format arguments.
        /// </summary>
        private static readonly HashSet<string> KnownIndirectKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "NumberWords",             // LocaleManager.NumberWordsToInt — bare Get() from inside LocaleManager itself
            "NPERewardCardHint",       // NPERewardNavigator — ternary with NPERewardDeckHint
            "NPERewardDeckHint",       // NPERewardNavigator — ternary with NPERewardCardHint
            "Dungeon_Format",          // DuelAnnouncer/PortraitNavigator dynamic lookup
            "DungeonsCompleted_Format",
            "DungeonsCompleted_One",
            "LifeCounter_Format",
            "NoActiveEffects",
            "PlayerAbilityCount_Format",
            "PlayerAbilityCount_One",
            "PlayerEffects",
            "ItemsCount_Format",       // Strings.ItemCount plural helper
            "ItemsCount_One",
            "BrowserCards_Format",     // superseded by _Base variants but kept for back-compat
            "BrowserCards_One",
            "MulliganEntry_Format",
            "ActivationWithItems_Format",
            "BrowserOptions_Format",
            "Card_EnchantedBy_Many_Format",
        };

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "lang")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "src")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                $"Could not locate repo root from {AppContext.BaseDirectory}");
        }

        private static Dictionary<string, string> LoadLocale(string path)
        {
            var dict = new Dictionary<string, string>();
            LocaleManager.ParseFlatJson(File.ReadAllText(path), dict);
            return dict;
        }

        private static HashSet<string> ExtractUsedKeysFromSrc(string srcRoot)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (Match m in LocCallRegex.Matches(text))
                    keys.Add(m.Groups[1].Value);
                foreach (Match m in WithHintRegex.Matches(text))
                    keys.Add(m.Groups[1].Value);
            }
            return keys;
        }

        private static bool IsIndirectKey(string key) =>
            KnownIndirectKeys.Contains(key) ||
            KnownIndirectPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));

        [Test]
        public void EnglishLocale_ContainsEveryKeyReferencedByCode()
        {
            var root = FindRepoRoot();
            var en = LoadLocale(Path.Combine(root, "lang", "en.json"));
            var usedKeys = ExtractUsedKeysFromSrc(Path.Combine(root, "src"));

            var missing = usedKeys
                .Where(k => !en.ContainsKey(k))
                .OrderBy(k => k)
                .ToList();

            Assert.That(missing, Is.Empty,
                "Keys referenced in code but absent from lang/en.json:\n  " +
                string.Join("\n  ", missing));
        }

        [Test]
        public void EnglishLocale_HasNoOrphanKeys()
        {
            var root = FindRepoRoot();
            var en = LoadLocale(Path.Combine(root, "lang", "en.json"));
            var usedKeys = ExtractUsedKeysFromSrc(Path.Combine(root, "src"));

            var orphans = en.Keys
                .Where(k => !usedKeys.Contains(k) && !IsIndirectKey(k))
                .OrderBy(k => k)
                .ToList();

            Assert.That(orphans, Is.Empty,
                "Keys in lang/en.json with no code reference (dead keys — remove " +
                "or add to KnownIndirectKeys with a justification):\n  " +
                string.Join("\n  ", orphans));
        }

        [Test]
        public void AllLocales_ShareTheSameKeySetAsEnglish()
        {
            var root = FindRepoRoot();
            var langDir = Path.Combine(root, "lang");
            var en = LoadLocale(Path.Combine(langDir, "en.json"));
            var enKeys = new HashSet<string>(en.Keys, StringComparer.Ordinal);

            var divergences = new List<string>();
            foreach (var file in Directory.EnumerateFiles(langDir, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name == "en") continue;

                var other = LoadLocale(file);
                var missing = enKeys.Where(k => !other.ContainsKey(k)).OrderBy(k => k).ToList();
                var extra = other.Keys.Where(k => !enKeys.Contains(k)).OrderBy(k => k).ToList();

                if (missing.Count > 0)
                    divergences.Add($"{name}.json missing keys: {string.Join(", ", missing)}");
                if (extra.Count > 0)
                    divergences.Add($"{name}.json has keys absent from en.json: {string.Join(", ", extra)}");
            }

            Assert.That(divergences, Is.Empty,
                "Locale key divergences:\n  " + string.Join("\n  ", divergences));
        }
    }
}
