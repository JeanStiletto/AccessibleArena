using System;
using System.Reflection;
using MelonLoader;
using TMPro;
using UnityEngine;

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

        // Cached reflection info for player status extraction
        private static Type _challengeDisplayType;
        private static Type _playerDisplayType;
        private static Type _deckSelectBladeType;
        private static MethodInfo _deckSelectHideMethod;
        private static FieldInfo _localPlayerField;
        private static FieldInfo _enemyPlayerField;
        private static FieldInfo _playerNameField;
        private static FieldInfo _noPlayerField;
        private static FieldInfo _playerInvitedField;
        private static PropertyInfo _playerIdProp;
        private static bool _reflectionInitialized;

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
        public PlayBladeResult HandleEnter(GameObject element, ElementGroup elementGroup)
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
        private static bool IsDeckSelectionButton(GameObject element)
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

        /// <summary>
        /// Enhance a button label by prefixing with the local player name for challenge buttons.
        /// Returns the original label if not applicable.
        /// </summary>
        public string EnhanceButtonLabel(GameObject element, string label)
        {
            if (element == null) return label;
            // Only enhance the main challenge button (shows status like "Ungültiges Deck", "Warten", "Ready")
            if (element.name != "UnifiedChallenge_MainButton")
                return label;

            string playerName = GetLocalPlayerName();
            if (string.IsNullOrEmpty(playerName))
                return label;

            return $"{playerName}: {label}";
        }

        /// <summary>
        /// Get the local player's display name (stripped of rich text tags).
        /// </summary>
        public string GetLocalPlayerName()
        {
            try
            {
                InitReflection();
                var display = FindChallengeDisplay();
                if (display == null) return null;

                var localDisplay = _localPlayerField?.GetValue(display);
                if (localDisplay == null) return null;

                var nameText = _playerNameField?.GetValue(localDisplay) as TMP_Text;
                if (nameText == null || string.IsNullOrEmpty(nameText.text)) return null;

                return StripRichTextTags(nameText.text);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ChallengeHelper] Error getting local player name: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Close the DeckSelectBlade if it's currently showing.
        /// Called after spinner changes in challenge context to prevent the blade from
        /// auto-opening and causing inconsistent element counts.
        /// </summary>
        public static void CloseDeckSelectBlade()
        {
            try
            {
                if (_deckSelectBladeType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        _deckSelectBladeType = asm.GetType("DeckSelectBlade");
                        if (_deckSelectBladeType != null) break;
                    }
                    if (_deckSelectBladeType == null) return;
                }

                var blades = UnityEngine.Object.FindObjectsOfType(_deckSelectBladeType);
                foreach (var blade in blades)
                {
                    var mb = blade as MonoBehaviour;
                    if (mb != null && mb.gameObject.activeInHierarchy)
                    {
                        if (_deckSelectHideMethod == null)
                        {
                            _deckSelectHideMethod = _deckSelectBladeType.GetMethod("Hide",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        if (_deckSelectHideMethod != null)
                        {
                            _deckSelectHideMethod.Invoke(blade, null);
                            MelonLogger.Msg("[ChallengeHelper] Closed DeckSelectBlade after spinner change");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ChallengeHelper] Error closing DeckSelectBlade: {ex.Message}");
            }
        }

        #region Player Status

        /// <summary>
        /// Get a summary of player status for the challenge screen announcement.
        /// Returns something like "You: PlayerName. Opponent: Not invited" or
        /// "You: PlayerName. Opponent: OpponentName".
        /// Returns null if player info cannot be read.
        /// </summary>
        public string GetPlayerStatusSummary()
        {
            try
            {
                InitReflection();
                if (_challengeDisplayType == null)
                    return null;

                // Find UnifiedChallengeDisplay in scene
                var displayComponent = FindChallengeDisplay();
                if (displayComponent == null)
                    return null;

                // Get local and enemy player displays
                var localDisplay = _localPlayerField?.GetValue(displayComponent);
                var enemyDisplay = _enemyPlayerField?.GetValue(displayComponent);

                string localInfo = GetPlayerInfo(localDisplay, isLocal: true);
                string enemyInfo = GetPlayerInfo(enemyDisplay, isLocal: false);

                if (localInfo == null && enemyInfo == null)
                    return null;

                string result = "";
                if (localInfo != null)
                    result += localInfo;
                if (enemyInfo != null)
                {
                    if (result.Length > 0) result += ". ";
                    result += enemyInfo;
                }

                return result;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ChallengeHelper] Error getting player status: {ex.Message}");
                return null;
            }
        }

        private static void InitReflection()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            // Find UnifiedChallengeDisplay type
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_challengeDisplayType == null)
                    _challengeDisplayType = asm.GetType("UnifiedChallengeDisplay");
                if (_playerDisplayType == null)
                    _playerDisplayType = asm.GetType("Wizards.Mtga.PrivateGame.ChallengePlayerDisplay");
                if (_challengeDisplayType != null && _playerDisplayType != null)
                    break;
            }

            if (_challengeDisplayType != null)
            {
                _localPlayerField = _challengeDisplayType.GetField("_localPlayerDisplay", flags);
                _enemyPlayerField = _challengeDisplayType.GetField("_enemyPlayerDisplay", flags);
            }

            if (_playerDisplayType != null)
            {
                _playerNameField = _playerDisplayType.GetField("_playerName", flags);
                _noPlayerField = _playerDisplayType.GetField("_noPlayer", flags);
                _playerInvitedField = _playerDisplayType.GetField("_playerInvited", flags);
                _playerIdProp = _playerDisplayType.GetProperty("PlayerId", BindingFlags.Public | BindingFlags.Instance);
            }

            MelonLogger.Msg($"[ChallengeHelper] Reflection init: display={_challengeDisplayType != null}, player={_playerDisplayType != null}");
        }

        private static UnityEngine.Object FindChallengeDisplay()
        {
            if (_challengeDisplayType == null) return null;

            // FindObjectsOfType with the resolved type
            var objects = UnityEngine.Object.FindObjectsOfType(_challengeDisplayType);
            foreach (var obj in objects)
            {
                var mb = obj as MonoBehaviour;
                if (mb != null && mb.gameObject.activeInHierarchy)
                    return obj;
            }
            return null;
        }

        private static string GetPlayerInfo(object playerDisplay, bool isLocal)
        {
            if (playerDisplay == null) return null;

            string prefix = isLocal
                ? Models.Strings.ChallengeYou
                : Models.Strings.ChallengeOpponent;

            // For enemy card: check if no player or invited
            if (!isLocal)
            {
                var noPlayerObj = _noPlayerField?.GetValue(playerDisplay) as GameObject;
                var invitedObj = _playerInvitedField?.GetValue(playerDisplay) as GameObject;

                if (noPlayerObj != null && noPlayerObj.activeSelf)
                    return $"{prefix}: {Models.Strings.ChallengeNotInvited}";

                if (invitedObj != null && invitedObj.activeSelf)
                {
                    // Invited but not yet joined
                    return $"{prefix}: {Models.Strings.ChallengeInvited}";
                }
            }

            // Read player name
            string playerName = null;
            var nameText = _playerNameField?.GetValue(playerDisplay) as TMP_Text;
            if (nameText != null)
                playerName = nameText.text;

            // Read player status via UITextExtractor on the _playerStatus Localize component
            string status = null;
            var playerDisplayMb = playerDisplay as MonoBehaviour;
            if (playerDisplayMb != null)
            {
                // Find _playerStatus field (Localize component) and read its text
                var statusField = _playerDisplayType?.GetField("_playerStatus", BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusField != null)
                {
                    var statusComponent = statusField.GetValue(playerDisplay) as Component;
                    if (statusComponent != null && statusComponent.gameObject.activeInHierarchy)
                    {
                        status = UITextExtractor.GetText(statusComponent.gameObject);
                    }
                }
            }

            if (string.IsNullOrEmpty(playerName))
                return null;

            // Strip rich text tags from player name (FormatDisplayName adds color tags)
            playerName = StripRichTextTags(playerName);

            if (!string.IsNullOrEmpty(status))
                return $"{prefix}: {playerName}, {status}";
            return $"{prefix}: {playerName}";
        }

        private static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Remove <color=...>...</color> and similar rich text tags
            return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "").Trim();
        }

        #endregion
    }
}
