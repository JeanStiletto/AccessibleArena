using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Splits one text component's content into screen-reader-sized blocks for the Codex.
    ///
    /// The game puts an entire article section, or the whole credits roll, into a single
    /// TMP_Text, so per-component extraction yields blocks far too large to step through with
    /// Arrow Up/Down. Lines are the unit: a blank line always ends a block, a long line (a real
    /// paragraph) always stands alone, and runs of short lines (headings, list items, the names
    /// in the credits) are grouped until the block would exceed <see cref="MaxBlockLength"/>.
    /// Grouped lines are joined with a period so speech pauses between them, unless the previous
    /// line already ends in punctuation.
    ///
    /// Pure logic: no Unity/game dependencies, unit-tested in CodexTextSplitterTests.
    /// </summary>
    public static class CodexTextSplitter
    {
        /// <summary>A line at or above this length is a paragraph and gets its own block.</summary>
        public const int LongLineLength = 80;

        /// <summary>Short lines are grouped into one block up to roughly this many characters.</summary>
        public const int MaxBlockLength = 300;

        private static readonly Regex LineBreakTag = new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Splits <paramref name="text"/> (rich text already stripped except for line-break tags)
        /// into announcement blocks. Empty input yields an empty list.
        /// </summary>
        public static List<string> Split(string text)
        {
            var blocks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return blocks;

            text = LineBreakTag.Replace(text, "\n").Replace("\r\n", "\n").Replace('\r', '\n');

            var current = new StringBuilder();
            foreach (var rawLine in text.Split('\n'))
            {
                string line = Whitespace.Replace(rawLine, " ").Trim();
                if (line.Length == 0)
                {
                    Flush(current, blocks);
                    continue;
                }

                if (line.Length >= LongLineLength)
                {
                    Flush(current, blocks);
                    blocks.Add(line);
                    continue;
                }

                if (current.Length > 0 && current.Length + line.Length + 2 > MaxBlockLength)
                    Flush(current, blocks);

                if (current.Length > 0)
                    current.Append(EndsWithPunctuation(current) ? " " : ". ");
                current.Append(line);
            }
            Flush(current, blocks);
            return blocks;
        }

        private static void Flush(StringBuilder current, List<string> blocks)
        {
            if (current.Length == 0) return;
            blocks.Add(current.ToString());
            current.Clear();
        }

        private static bool EndsWithPunctuation(StringBuilder sb)
        {
            char last = sb[sb.Length - 1];
            return last == '.' || last == '!' || last == '?' || last == ':' || last == ';' || last == ',';
        }
    }
}
