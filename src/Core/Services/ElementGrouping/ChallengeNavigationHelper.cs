using MelonLoader;

namespace AccessibleArena.Core.Services.ElementGrouping
{
    /// <summary>
    /// Centralized helper for Challenge screen navigation (Direct Challenge / Friend Challenge).
    /// Handles two-level navigation: ChallengeMain (spinners + buttons) and Deck Selection (folders).
    /// GeneralMenuNavigator calls this and acts on the PlayBladeResult.
    /// </summary>
    public class ChallengeNavigationHelper
    {
        private readonly GroupedNavigator _groupedNavigator;

        /// <summary>
        /// Whether currently in a challenge context.
        /// Uses the context flag set by OnChallengeOpened/OnChallengeClosed.
        /// </summary>
        public bool IsActive => _groupedNavigator.IsChallengeContext;

        public ChallengeNavigationHelper(GroupedNavigator groupedNavigator)
        {
            _groupedNavigator = groupedNavigator;
        }

        /// <summary>
        /// Handle Enter key press on an element.
        /// Called BEFORE UIActivator.Activate so we can set up pending entries.
        /// </summary>
        /// <param name="element">The element being activated.</param>
        /// <param name="elementGroup">The element's group type.</param>
        /// <returns>Result indicating what action to take.</returns>
        public PlayBladeResult HandleEnter(UnityEngine.GameObject element, ElementGroup elementGroup)
        {
            // ChallengeMain: "Select Deck" button -> navigate to folder list
            if (elementGroup == ElementGroup.ChallengeMain)
            {
                // Check if element is a deck selection button
                if (element != null && IsDeckSelectionButton(element))
                {
                    _groupedNavigator.RequestFoldersEntry();
                    MelonLogger.Msg("[ChallengeHelper] Select Deck activated -> requesting folders entry");
                    return PlayBladeResult.RescanNeeded;
                }
            }

            return PlayBladeResult.NotHandled;
        }

        /// <summary>
        /// Handle Backspace key press.
        /// Navigation: ChallengeMain -> close challenge, Folders -> ChallengeMain
        /// </summary>
        public PlayBladeResult HandleBackspace()
        {
            var currentGroup = _groupedNavigator.CurrentGroup;
            if (!currentGroup.HasValue)
                return PlayBladeResult.NotHandled;

            var groupType = currentGroup.Value.Group;

            // Check if we're in a challenge-relevant group
            bool isChallengeGroup = groupType == ElementGroup.ChallengeMain;
            bool isFolderGroup = groupType == ElementGroup.PlayBladeFolders || currentGroup.Value.IsFolderGroup;

            if (!isChallengeGroup && !isFolderGroup)
                return PlayBladeResult.NotHandled;

            if (_groupedNavigator.Level == NavigationLevel.InsideGroup)
            {
                if (currentGroup.Value.IsFolderGroup)
                {
                    // Inside a folder (viewing decks) -> let HandleGroupedBackspace handle toggle OFF
                    // It will call RequestFoldersEntry for PlayBlade context, but we want ChallengeMain
                    // So we DON'T handle this - let it fall through and we fix up in the folder exit path
                    return PlayBladeResult.NotHandled;
                }

                _groupedNavigator.ExitGroup();

                if (groupType == ElementGroup.PlayBladeFolders)
                {
                    // Was inside folders list -> go back to ChallengeMain
                    _groupedNavigator.RequestChallengeMainEntry();
                    MelonLogger.Msg("[ChallengeHelper] Backspace: exited folders, going to ChallengeMain");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.ChallengeMain)
                {
                    // Was inside ChallengeMain -> close the challenge blade
                    MelonLogger.Msg("[ChallengeHelper] Backspace: exited ChallengeMain, closing blade");
                    return PlayBladeResult.CloseBlade;
                }
            }
            else
            {
                // At group level
                if (isFolderGroup)
                {
                    // At folder group level -> go to ChallengeMain
                    _groupedNavigator.RequestChallengeMainEntry();
                    MelonLogger.Msg("[ChallengeHelper] Backspace: at folder level, going to ChallengeMain");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.ChallengeMain)
                {
                    // At ChallengeMain level -> close the challenge blade
                    MelonLogger.Msg("[ChallengeHelper] Backspace: at ChallengeMain level, closing blade");
                    return PlayBladeResult.CloseBlade;
                }
            }

            return PlayBladeResult.NotHandled;
        }

        /// <summary>
        /// Called when challenge screen opens. Sets context and requests ChallengeMain entry.
        /// </summary>
        public void OnChallengeOpened()
        {
            _groupedNavigator.SetChallengeContext(true);
            _groupedNavigator.RequestChallengeMainEntry();
            MelonLogger.Msg("[ChallengeHelper] Challenge opened, set context and requesting ChallengeMain entry");
        }

        /// <summary>
        /// Called when challenge screen closes. Clears the challenge context.
        /// </summary>
        public void OnChallengeClosed()
        {
            _groupedNavigator.SetChallengeContext(false);
            MelonLogger.Msg("[ChallengeHelper] Challenge closed, cleared context");
        }

        /// <summary>
        /// Called when a deck is selected in the challenge deck picker.
        /// Auto-returns to ChallengeMain.
        /// </summary>
        public void HandleDeckSelected()
        {
            _groupedNavigator.RequestChallengeMainEntry();
            MelonLogger.Msg("[ChallengeHelper] Deck selected, requesting ChallengeMain entry");
        }

        /// <summary>
        /// Check if an element is the "Select Deck" or "NoDeck" button in the challenge screen.
        /// </summary>
        private static bool IsDeckSelectionButton(UnityEngine.GameObject element)
        {
            if (element == null) return false;
            string name = element.name;
            // "NoDeck" is shown when no deck is selected, and the deck display button when one is
            return name.Contains("NoDeck") || name.Contains("DeckDisplay") ||
                   name.Contains("SelectDeck") || name.Contains("Select Deck") ||
                   name.Contains("DeckSelectButton");
        }

        /// <summary>
        /// Reset - no-op since we derive state from GroupedNavigator.
        /// </summary>
        public void Reset() { }
    }
}
