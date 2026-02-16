using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Modal help menu navigator. When active, blocks all other input
    /// and allows navigation through keybind help items with Up/Down arrows.
    /// Closes with Backspace or F1.
    /// </summary>
    public class HelpNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly List<string> _helpItems;
        private int _currentIndex;
        private bool _isActive;

        public bool IsActive => _isActive;

        public HelpNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
            _helpItems = BuildHelpItems();
        }

        /// <summary>
        /// Rebuild help items when the language changes.
        /// Call this after LocaleManager reloads strings.
        /// </summary>
        public void RebuildItems()
        {
            _helpItems.Clear();
            _helpItems.AddRange(BuildHelpItems());
        }

        /// <summary>
        /// Build the list of help items from localized strings.
        /// Each item is a single help entry (category header or keybind).
        /// </summary>
        private List<string> BuildHelpItems()
        {
            return new List<string>
            {
                // Global shortcuts
                Strings.HelpCategoryGlobal,
                Strings.HelpF1Help,
                Strings.HelpF2Settings,
                Strings.HelpF3Context,
                Strings.HelpCtrlRRepeat,
                Strings.HelpBackspace,

                // Menu navigation
                Strings.HelpCategoryMenuNavigation,
                Strings.HelpArrowUpDown,
                Strings.HelpTabNavigation,
                Strings.HelpArrowLeftRight,
                Strings.HelpHomeEnd,
                Strings.HelpPageUpDown,
                Strings.HelpNumberKeysFilters,
                Strings.HelpEnterSpace,

                // Input fields
                Strings.HelpCategoryInputFields,
                Strings.HelpEnterEditField,
                Strings.HelpEscapeExitField,
                Strings.HelpTabNextField,
                Strings.HelpShiftTabPrevField,
                Strings.HelpArrowsInField,

                // Zones in duel (yours and opponent)
                Strings.HelpCategoryDuelZones,
                Strings.HelpCHand,
                Strings.HelpBBattlefield,
                Strings.HelpALands,
                Strings.HelpRNonCreatures,
                Strings.HelpGGraveyard,
                Strings.HelpXExile,
                Strings.HelpSStack,
                Strings.HelpDLibrary,

                // Duel information
                Strings.HelpCategoryDuelInfo,
                Strings.HelpLLifeTotals,
                Strings.HelpTTurnPhase,
                Strings.HelpVPlayerInfo,

                // Card navigation in zone
                Strings.HelpCategoryCardNavigation,
                Strings.HelpLeftRightCards,
                Strings.HelpHomeEndCards,
                Strings.HelpEnterPlay,
                Strings.HelpTabTargets,

                // Card details
                Strings.HelpCategoryCardDetails,
                Strings.HelpUpDownDetails,

                // Combat
                Strings.HelpCategoryCombat,
                Strings.HelpSpaceCombat,
                Strings.HelpBackspaceCombat,

                // Browser
                Strings.HelpCategoryBrowser,
                Strings.HelpTabBrowser,
                Strings.HelpCDZones,
                Strings.HelpEnterToggle,
                Strings.HelpSpaceConfirm,

                // Debug keys
                Strings.HelpCategoryDebug,
                Strings.HelpF4Refresh,
                Strings.HelpF11CardDump,
                Strings.HelpF12UIDump
            };
        }

        /// <summary>
        /// Toggle the help menu on/off. Called when F1 is pressed.
        /// </summary>
        public void Toggle()
        {
            if (_isActive)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// Open the help menu.
        /// </summary>
        public void Open()
        {
            if (_isActive) return;

            _isActive = true;
            _currentIndex = 0;

            MelonLogger.Msg("[HelpNavigator] Opened");

            // Announce title and instructions
            string core = $"{Strings.HelpMenuTitle}. {_helpItems.Count} items";
            _announcer.AnnounceInterrupt(Strings.WithHint(core, "HelpMenuInstructions"));
        }

        /// <summary>
        /// Close the help menu.
        /// </summary>
        public void Close()
        {
            if (!_isActive) return;

            _isActive = false;
            _currentIndex = 0;

            MelonLogger.Msg("[HelpNavigator] Closed");
            _announcer.AnnounceInterrupt(Strings.HelpMenuClosed);
        }

        /// <summary>
        /// Handle input while help menu is active.
        /// Returns true if input was handled (blocks other input).
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // F1, Backspace, or Escape closes the menu
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return true;
            }

            // Up arrow or W: previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MovePrevious();
                return true;
            }

            // Down arrow or S: next item
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveNext();
                return true;
            }

            // Home: first item
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return true;
            }

            // End: last item
            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return true;
            }

            // Block all other input while help menu is open
            return true;
        }

        private void MoveNext()
        {
            if (_currentIndex >= _helpItems.Count - 1)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex++;
            AnnounceCurrentItem();
        }

        private void MovePrevious()
        {
            if (_currentIndex <= 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex--;
            AnnounceCurrentItem();
        }

        private void MoveFirst()
        {
            if (_currentIndex == 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = 0;
            AnnounceCurrentItem();
        }

        private void MoveLast()
        {
            int lastIndex = _helpItems.Count - 1;
            if (_currentIndex == lastIndex)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = lastIndex;
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _helpItems.Count) return;

            string item = _helpItems[_currentIndex];
            string announcement = Strings.HelpItemPosition(_currentIndex + 1, _helpItems.Count, item);
            _announcer.AnnounceInterrupt(announcement);
        }
    }
}
