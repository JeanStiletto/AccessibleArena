using System.Collections.Generic;

namespace AccessibleArena.Core.Utils
{
    /// <summary>
    /// Pure index math for Ctrl+PageUp/PageDown section jumps in sorted card lists.
    /// A section is a maximal run of adjacent equal keys (the sort bucket the game
    /// itself ordered the list by); jumps always target the first index of a run.
    /// </summary>
    public static class SectionJump
    {
        /// <summary>
        /// Start index of the section containing <paramref name="index"/>.
        /// Returns -1 for an empty list; the index is clamped into range first.
        /// </summary>
        public static int SectionStart(IReadOnlyList<long> keys, int index)
        {
            if (keys == null || keys.Count == 0) return -1;
            index = Clamp(index, keys.Count);
            while (index > 0 && keys[index - 1] == keys[index])
                index--;
            return index;
        }

        /// <summary>
        /// First index of the section after the one containing <paramref name="index"/>.
        /// Returns -1 when the current section is the last one (or the list is empty).
        /// </summary>
        public static int Next(IReadOnlyList<long> keys, int index)
        {
            if (keys == null || keys.Count == 0) return -1;
            index = Clamp(index, keys.Count);
            long currentKey = keys[index];
            for (int i = index + 1; i < keys.Count; i++)
            {
                if (keys[i] != currentKey)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Backward target: the start of the current section when <paramref name="index"/>
        /// is past it, otherwise the start of the previous section.
        /// Returns -1 when already at the very first section start (or the list is empty).
        /// </summary>
        public static int Previous(IReadOnlyList<long> keys, int index)
        {
            if (keys == null || keys.Count == 0) return -1;
            index = Clamp(index, keys.Count);
            int start = SectionStart(keys, index);
            if (index > start)
                return start;
            if (start == 0)
                return -1;
            return SectionStart(keys, start - 1);
        }

        private static int Clamp(int index, int count)
        {
            if (index < 0) return 0;
            if (index >= count) return count - 1;
            return index;
        }
    }
}
