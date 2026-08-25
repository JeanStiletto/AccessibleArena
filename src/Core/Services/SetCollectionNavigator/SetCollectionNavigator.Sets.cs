using System;
using System.Collections.Generic;
using UnityEngine;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class SetCollectionNavigator
    {
        #region Metric Table

        /// <summary>
        /// The eleven meters the game shows, in reading order: the total first (it answers
        /// "how close am I?" without further navigation), then rarities, then colors.
        /// </summary>
        private static readonly (string EnumName, Func<string> Label)[] MetricOrder =
        {
            ("None",       () => Strings.SetCollectionMetricTotal),
            ("Common",     () => Strings.SetCollectionMetricCommon),
            ("Uncommon",   () => Strings.SetCollectionMetricUncommon),
            ("Rare",       () => Strings.SetCollectionMetricRare),
            ("MythicRare", () => Strings.SetCollectionMetricMythicRare),
            ("White",      () => Strings.SetCollectionMetricWhite),
            ("Blue",       () => Strings.SetCollectionMetricBlue),
            ("Black",      () => Strings.SetCollectionMetricBlack),
            ("Red",        () => Strings.SetCollectionMetricRed),
            ("Green",      () => Strings.SetCollectionMetricGreen),
            ("Colorless",  () => Strings.SetCollectionMetricColorless),
        };

        #endregion

        #region Level Entry

        private void EnterSetsLevel()
        {
            BuildSetList();

            if (_sets.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.SetCollectionNoSetsMatch);
                return;
            }

            if (_setIndex < 0 || _setIndex >= _sets.Count) _setIndex = 0;

            _navLevel = NavLevel.Sets;
            BuildInfoEntries();
            _infoIndex = 0;
            AnnounceCurrentSet();
        }

        private void ReturnToFiltersLevel()
        {
            _navLevel = NavLevel.Filters;
            AnnounceCurrentFilter();
        }

        /// <summary>Closes the screen through the game's own back button, returning to the profile.</summary>
        private void LeaveScreen()
        {
            try
            {
                H?.BackButtonClicked?.Invoke(_screenView, null);
                Log.Msg("{NavigatorId}", "Left set collection via BackButtonClicked");
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"BackButtonClicked failed: {ex.Message}");
            }

            _announcer.AnnounceInterrupt(Strings.Back);
        }

        #endregion

        #region Info Entries

        /// <summary>
        /// Builds the Up/Down cycle for the selected set: the eleven meters, then the release
        /// date and set-type banners the game shows alongside them.
        /// </summary>
        private void BuildInfoEntries()
        {
            _info.Clear();
            if (_setIndex < 0 || _setIndex >= _sets.Count) return;

            foreach (var (enumName, label) in MetricOrder)
            {
                object metric = SetCollectionDataProvider.GetMetric(enumName);
                if (metric == null) continue;

                _info.Add(new InfoEntry { Kind = InfoKind.Metric, Label = label(), Metric = metric });
            }

            var set = _sets[_setIndex];

            if (set.ReleaseDate != default(DateTime))
                _info.Add(new InfoEntry { Kind = InfoKind.ReleaseDate, Label = null });

            if (DescribeSetTypes(set) != null)
                _info.Add(new InfoEntry { Kind = InfoKind.SetTypes, Label = null });

            if (_infoIndex >= _info.Count) _infoIndex = 0;
        }

        private static string DescribeSetTypes(SetEntry set)
        {
            var types = new List<string>(4);
            if (set.IsStandard) types.Add(Strings.SetCollectionTypeStandard);
            if (set.IsHistoric) types.Add(Strings.SetCollectionTypeHistoric);
            if (set.IsAlchemy) types.Add(Strings.SetCollectionTypeAlchemy);
            if (set.IsUniversesBeyond) types.Add(Strings.SetCollectionTypeUniversesBeyond);
            return types.Count == 0 ? null : string.Join(", ", types.ToArray());
        }

        #endregion

        #region Sets Input

        private void HandleSetsInput()
        {
            if (_sets.Count == 0)
            {
                ReturnToFiltersLevel();
                return;
            }

            // Left/Right: move between sets
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => MoveSet(-1))) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => MoveSet(1))) return;

            // Up/Down: cycle the selected set's meters
            if (_holdRepeater.Check(KeyCode.UpArrow, () => MoveInfo(-1))) return;
            if (_holdRepeater.Check(KeyCode.DownArrow, () => MoveInfo(1))) return;

            // Home/End: first/last set
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                SelectSet(0);
                return;
            }
            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                SelectSet(_sets.Count - 1);
                return;
            }

            // Enter: per-set actions (deck builder / store)
            if (InputManager.GetEnterAndConsume())
            {
                EnterActionsLevel();
                return;
            }

            // Backspace: back up to the filters
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ReturnToFiltersLevel();
                return;
            }

            // A-Z: jump to the next set starting with that letter
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
            {
                if (KeyInput.GetKeyDown(key))
                {
                    JumpToSetByLetter((char)('a' + (key - KeyCode.A)));
                    return;
                }
            }
        }

        private bool MoveSet(int direction)
        {
            int newIndex = _setIndex + direction;
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return false;
            }
            if (newIndex >= _sets.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return false;
            }

            SelectSet(newIndex);
            return true;
        }

        private void SelectSet(int index)
        {
            if (index < 0 || index >= _sets.Count) return;

            _setIndex = index;
            BuildInfoEntries();
            _infoIndex = 0;
            AnnounceCurrentSet();
        }

        private bool MoveInfo(int direction)
        {
            if (_info.Count == 0) return false;

            int newIndex = _infoIndex + direction;
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return false;
            }
            if (newIndex >= _info.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return false;
            }

            _infoIndex = newIndex;
            AnnounceCurrentInfo();
            return true;
        }

        private void JumpToSetByLetter(char letter)
        {
            for (int offset = 1; offset <= _sets.Count; offset++)
            {
                int candidate = (_setIndex + offset) % _sets.Count;
                string name = _sets[candidate].Name;
                if (!string.IsNullOrEmpty(name) && char.ToLowerInvariant(name[0]) == letter)
                {
                    SelectSet(candidate);
                    return;
                }
            }

            _announcer.AnnounceVerbose(Strings.LetterSearchNoMatch(letter.ToString()), AnnouncementPriority.Normal);
        }

        #endregion

        #region Sets Announcements

        private void AnnounceCurrentSet()
        {
            if (_setIndex < 0 || _setIndex >= _sets.Count) return;
            var set = _sets[_setIndex];

            object total = SetCollectionDataProvider.TotalMetric;
            if (total == null || !TryGetMetricTotals(set, total, out int owned, out int available, out bool isPlayset))
            {
                _announcer.AnnounceInterrupt(set.Name);
                return;
            }

            int percent = Percent(owned, available);
            string announcement = isPlayset
                ? Strings.SetCollectionSetHeadlinePlayset(set.Name, owned, available, percent, _setIndex + 1, _sets.Count)
                : Strings.SetCollectionSetHeadline(set.Name, owned, available, percent, _setIndex + 1, _sets.Count);

            _announcer.AnnounceInterrupt(announcement);
        }

        private void AnnounceCurrentInfo()
        {
            if (_infoIndex < 0 || _infoIndex >= _info.Count) return;
            if (_setIndex < 0 || _setIndex >= _sets.Count) return;

            var entry = _info[_infoIndex];
            var set = _sets[_setIndex];

            switch (entry.Kind)
            {
                case InfoKind.ReleaseDate:
                    _announcer.AnnounceInterrupt(Strings.SetCollectionReleaseDate(
                        set.ReleaseDate.ToString("D")));
                    return;

                case InfoKind.SetTypes:
                    string types = DescribeSetTypes(set);
                    if (types != null)
                        _announcer.AnnounceInterrupt(Strings.SetCollectionSetTypes(types));
                    return;

                default:
                    if (!TryGetMetricTotals(set, entry.Metric, out int owned, out int available, out bool isPlayset))
                    {
                        _announcer.AnnounceInterrupt(entry.Label);
                        return;
                    }

                    int percent = Percent(owned, available);
                    _announcer.AnnounceInterrupt(isPlayset
                        ? Strings.SetCollectionMetricPlayset(entry.Label, owned, available, percent)
                        : Strings.SetCollectionMetric(entry.Label, owned, available, percent));
                    return;
            }
        }

        #endregion
    }
}
