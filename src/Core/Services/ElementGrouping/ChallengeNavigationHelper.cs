using System;
using System.Reflection;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
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
        private readonly IAnnouncementService _announcer;

        // Cached reflection info for player status extraction
        private static Type _challengeDisplayType;
        private static Type _playerDisplayType;
        private static Type _playBladeControllerType;
        private static Type _bladeWidgetType;
        private static MethodInfo _hideDeckSelectorMethod;
        private static FieldInfo _deckSelectorField;

        private static FieldInfo _localPlayerField;
        private static FieldInfo _enemyPlayerField;
        private static FieldInfo _playerNameField;
        private static FieldInfo _noPlayerField;
        private static FieldInfo _playerInvitedField;
        private static PropertyInfo _playerIdProp;

        // Blade widget reflection for status text and settings lock
        private static FieldInfo _challengeStatusTextField;
        private static FieldInfo _isChallengeSettingsLockedField;
        private static PropertyInfo _isChallengeSettingsLockedProp;
        private static bool _reflectionInitialized;

        // Polling state for player status changes
        private enum EnemyState { NotInvited, Invited, Joined }
        private EnemyState _lastEnemyState = EnemyState.NotInvited;
        private string _lastEnemyName;
        private string _lastStatusText;
        private bool _wasCountdownActive;
        private float _pollTimer;
        private const float PollIntervalSeconds = 1.0f;
        private bool _pollingInitialized;

        /// <summary>
        /// Whether currently in a challenge context.
        /// Uses the context flag set by OnChallengeOpened/OnChallengeClosed.
        /// </summary>
        public bool IsActive => _groupedNavigator.IsChallengeContext;

        public ChallengeNavigationHelper(GroupedNavigator groupedNavigator, IAnnouncementService announcer)
        {
            _groupedNavigator = groupedNavigator;
            _announcer = announcer;
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
                    // Close the DeckSelectBlade properly so the challenge display reactivates
                    CloseDeckSelectBlade();
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
                    // Close the DeckSelectBlade properly so the challenge display reactivates
                    CloseDeckSelectBlade();
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
        /// Initializes polling state silently (no announcements).
        /// </summary>
        public void OnChallengeOpened()
        {
            _groupedNavigator.SetChallengeContext(true);
            _groupedNavigator.RequestChallengeMainEntry();

            // Initialize polling state silently
            InitializePollingState();

            MelonLogger.Msg("[ChallengeHelper] Challenge opened, set context and requesting ChallengeMain entry");
        }

        /// <summary>
        /// Called when challenge screen closes. Clears the challenge context and polling state.
        /// </summary>
        public void OnChallengeClosed()
        {
            _groupedNavigator.SetChallengeContext(false);
            _pollingInitialized = false;
            MelonLogger.Msg("[ChallengeHelper] Challenge closed, cleared context");
        }

        /// <summary>
        /// Called when a deck is selected in the challenge deck picker.
        /// Reactivates the challenge display (hidden by game's DeckSelectBlade.Hide())
        /// and auto-returns to ChallengeMain.
        /// </summary>
        public void HandleDeckSelected()
        {
            // The game calls DeckSelectBlade.Hide() directly (not HideDeckSelector()),
            // which closes the blade but leaves _unifiedChallengeDisplay deactivated.
            // We must call HideDeckSelector() to reactivate it so Leave/Invite buttons appear.
            CloseDeckSelectBlade();
            _groupedNavigator.RequestChallengeMainEntry();
            MelonLogger.Msg("[ChallengeHelper] Deck selected, closing blade and requesting ChallengeMain entry");
        }

        /// <summary>
        /// Check if an element is the "Select Deck" or "NoDeck" button in the challenge screen,
        /// or the already-selected deck display in ContextDisplay (clicking it re-opens the deck selector).
        /// </summary>
        private static bool IsDeckSelectionButton(GameObject element)
        {
            if (element == null) return false;
            string name = element.name;
            // "NoDeck" is shown when no deck is selected, and the deck display button when one is
            if (name.Contains("NoDeck") || name.Contains("DeckDisplay") ||
                name.Contains("SelectDeck") || name.Contains("Select Deck") ||
                name.Contains("DeckSelectButton"))
                return true;

            // DeckView_Base inside ContextDisplay = the already-selected deck (e.g., "mono blau").
            // Clicking it should open the deck selector to change decks (same as NoDeck).
            return IsContextDisplayDeck(element);
        }

        /// <summary>
        /// Check if an element is the deck display inside ContextDisplay (not a deck entry in the selector list).
        /// </summary>
        private static bool IsContextDisplayDeck(GameObject element)
        {
            Transform current = element.transform;
            bool hasDeckView = false;
            bool hasContextDisplay = false;
            int depth = 0;
            while (current != null && depth < 8)
            {
                if (current.name.Contains("DeckView_Base")) hasDeckView = true;
                if (current.name == "ContextDisplay") hasContextDisplay = true;
                current = current.parent;
                depth++;
            }
            return hasDeckView && hasContextDisplay;
        }

        /// <summary>
        /// Reset - no-op since we derive state from GroupedNavigator.
        /// </summary>
        public void Reset() { }

        #region Label Enhancement

        /// <summary>
        /// Enhance a button label for challenge screen elements.
        /// - Main button: prefix with player name + append status text
        /// - Enemy action buttons: map icon-only buttons to readable labels
        /// - Spinners: prefix with "Locked" when settings are locked
        /// Returns the original label if not applicable.
        /// </summary>
        public string EnhanceButtonLabel(GameObject element, string label)
        {
            if (element == null) return label;

            string name = element.name;

            // Enemy player action buttons (icon-only)
            if (name == "KickPlayer_SecondaryButton")
                return Models.Strings.ChallengeKickOpponent;
            if (name == "BlockPlayer_SecondaryButton")
                return Models.Strings.ChallengeBlockOpponent;
            if (name == "AddFriend_SecondaryButton")
                return Models.Strings.ChallengeAddFriend;

            // Main challenge button: prefix with player name + append status text
            if (name == "UnifiedChallenge_MainButton")
            {
                string playerName = GetLocalPlayerName();
                string statusText = GetChallengeStatusText();

                string result = label;
                if (!string.IsNullOrEmpty(playerName))
                    result = $"{playerName}: {result}";
                if (!string.IsNullOrEmpty(statusText))
                    result = $"{result}. {statusText}";

                return result;
            }

            // Spinners: prefix with "Locked" when settings are locked by host
            if (IsSettingsLocked() && IsSpinnerElement(element))
                return Models.Strings.ChallengeLocked(label);

            return label;
        }

        /// <summary>
        /// Check if an element is a spinner/stepper (inside ChallengeOptions).
        /// </summary>
        private static bool IsSpinnerElement(GameObject element)
        {
            // Check if element or a parent is inside ChallengeOptions
            Transform current = element.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                if (current.name.Contains("ChallengeOptions"))
                    return true;
                // Check for Spinner_OptionSelector component by type name
                foreach (var c in current.GetComponents<Component>())
                {
                    if (c != null && c.GetType().Name.Contains("Spinner_OptionSelector"))
                        return true;
                }
                current = current.parent;
                depth++;
            }
            return false;
        }

        #endregion

        #region Challenge Status Text

        /// <summary>
        /// Get the status text from _challengeStatusText on the UnifiedChallengeBladeWidget.
        /// Shows guidance like "Invite an opponent", "Select a deck", "Waiting for opponent".
        /// </summary>
        public string GetChallengeStatusText()
        {
            try
            {
                InitReflection();
                var widget = FindBladeWidget();
                if (widget == null) return null;

                if (_challengeStatusTextField == null) return null;

                var statusComponent = _challengeStatusTextField.GetValue(widget) as Component;
                if (statusComponent == null || !statusComponent.gameObject.activeInHierarchy)
                    return null;

                return UITextExtractor.GetText(statusComponent.gameObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ChallengeHelper] Error getting challenge status text: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Settings Lock Detection

        /// <summary>
        /// Check if challenge settings are locked (joining someone else's challenge).
        /// </summary>
        public bool IsSettingsLocked()
        {
            try
            {
                InitReflection();
                var widget = FindBladeWidget();
                if (widget == null) return false;

                // Try property first, then field
                if (_isChallengeSettingsLockedProp != null)
                    return (bool)_isChallengeSettingsLockedProp.GetValue(widget);
                if (_isChallengeSettingsLockedField != null)
                    return (bool)_isChallengeSettingsLockedField.GetValue(widget);

                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Tournament Parameters

        /// <summary>
        /// Get a summary of tournament parameters when in tournament mode.
        /// Reads Format/BestofX/Coin/Timer static text labels under TournamentParameters.
        /// Returns null if not in tournament mode or parameters not found.
        /// </summary>
        public string GetTournamentParametersSummary()
        {
            try
            {
                // Find TournamentParameters container
                var tournamentParams = GameObject.Find("TournamentParameters");
                if (tournamentParams == null || !tournamentParams.activeInHierarchy)
                    return null;

                var parts = new System.Collections.Generic.List<string>();

                // Read each parameter child's Text sub-element
                foreach (Transform child in tournamentParams.transform)
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    // Each parameter has a child named "Text" with TMP_Text + Localize
                    var textTransform = child.Find("Text");
                    if (textTransform == null) continue;

                    string text = UITextExtractor.GetText(textTransform.gameObject);
                    if (!string.IsNullOrEmpty(text))
                        parts.Add(text);
                }

                if (parts.Count == 0) return null;
                return string.Join(", ", parts);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ChallengeHelper] Error getting tournament params: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Polling / Update

        /// <summary>
        /// Poll for player status changes. Call from GeneralMenuNavigator.Update().
        /// Detects opponent join/leave, status text changes, and countdown state.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!IsActive || !_pollingInitialized) return;

            _pollTimer -= deltaTime;
            if (_pollTimer > 0) return;
            _pollTimer = PollIntervalSeconds;

            try
            {
                PollPlayerStatusChanges();
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ChallengeHelper] Polling error: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize polling state silently (read current state without announcing).
        /// </summary>
        private void InitializePollingState()
        {
            _pollTimer = PollIntervalSeconds;
            _wasCountdownActive = false;

            try
            {
                InitReflection();
                var display = FindChallengeDisplay();
                if (display != null)
                {
                    var enemyDisplay = _enemyPlayerField?.GetValue(display);
                    _lastEnemyState = GetEnemyState(enemyDisplay);
                    _lastEnemyName = GetEnemyName(enemyDisplay);
                }
                else
                {
                    _lastEnemyState = EnemyState.NotInvited;
                    _lastEnemyName = null;
                }

                _lastStatusText = GetChallengeStatusText();
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ChallengeHelper] Error initializing polling state: {ex.Message}");
                _lastEnemyState = EnemyState.NotInvited;
                _lastEnemyName = null;
                _lastStatusText = null;
            }

            _pollingInitialized = true;
        }

        private void PollPlayerStatusChanges()
        {
            InitReflection();
            var display = FindChallengeDisplay();
            if (display == null) return;

            // Check enemy state
            var enemyDisplay = _enemyPlayerField?.GetValue(display);
            var currentState = GetEnemyState(enemyDisplay);
            var currentName = GetEnemyName(enemyDisplay);

            // Detect transitions
            if (currentState != _lastEnemyState)
            {
                if (currentState == EnemyState.Joined && _lastEnemyState != EnemyState.Joined)
                {
                    string name = !string.IsNullOrEmpty(currentName) ? currentName : Models.Strings.ChallengeOpponent;
                    _announcer.Announce(Models.Strings.ChallengeOpponentJoined(name), Models.AnnouncementPriority.High);
                    MelonLogger.Msg($"[ChallengeHelper] Opponent joined: {name}");
                }
                else if (_lastEnemyState == EnemyState.Joined && currentState != EnemyState.Joined)
                {
                    _announcer.Announce(Models.Strings.ChallengeOpponentLeft, Models.AnnouncementPriority.High);
                    MelonLogger.Msg("[ChallengeHelper] Opponent left");
                }

                _lastEnemyState = currentState;
                _lastEnemyName = currentName;
            }

            // Check status text changes
            string currentStatusText = GetChallengeStatusText();
            if (currentStatusText != _lastStatusText && !string.IsNullOrEmpty(currentStatusText))
            {
                // Detect countdown start/cancel
                bool isCountdown = IsCountdownText(currentStatusText);
                bool wasCountdown = _wasCountdownActive;

                if (isCountdown && !wasCountdown)
                {
                    _announcer.Announce(Models.Strings.ChallengeMatchStarting, Models.AnnouncementPriority.High);
                    MelonLogger.Msg("[ChallengeHelper] Match countdown started");
                    _wasCountdownActive = true;
                }
                else if (!isCountdown && wasCountdown)
                {
                    _announcer.Announce(Models.Strings.ChallengeCountdownCancelled, Models.AnnouncementPriority.High);
                    MelonLogger.Msg("[ChallengeHelper] Countdown cancelled");
                    _wasCountdownActive = false;
                }
                else if (!isCountdown)
                {
                    // Normal status text change (e.g., "Select a deck" -> "Waiting for opponent")
                    _announcer.Announce(currentStatusText, Models.AnnouncementPriority.Normal);
                    MelonLogger.Msg($"[ChallengeHelper] Status text changed: {currentStatusText}");
                }

                _lastStatusText = currentStatusText;
            }
            else if (string.IsNullOrEmpty(currentStatusText) && !string.IsNullOrEmpty(_lastStatusText))
            {
                // Status text disappeared
                if (_wasCountdownActive)
                {
                    // Match probably started - no cancellation announcement needed
                    _wasCountdownActive = false;
                }
                _lastStatusText = currentStatusText;
            }
        }

        private EnemyState GetEnemyState(object enemyDisplay)
        {
            if (enemyDisplay == null) return EnemyState.NotInvited;

            var noPlayerObj = _noPlayerField?.GetValue(enemyDisplay) as GameObject;
            var invitedObj = _playerInvitedField?.GetValue(enemyDisplay) as GameObject;

            if (noPlayerObj != null && noPlayerObj.activeSelf)
                return EnemyState.NotInvited;
            if (invitedObj != null && invitedObj.activeSelf)
                return EnemyState.Invited;

            return EnemyState.Joined;
        }

        private string GetEnemyName(object enemyDisplay)
        {
            if (enemyDisplay == null) return null;

            var nameText = _playerNameField?.GetValue(enemyDisplay) as TMP_Text;
            if (nameText == null || string.IsNullOrEmpty(nameText.text)) return null;

            return StripRichTextTags(nameText.text);
        }

        /// <summary>
        /// Detect if status text indicates a countdown is active.
        /// Countdown text typically contains numbers (timer digits) or "Starting".
        /// </summary>
        private static bool IsCountdownText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            // Countdown text typically contains a number (seconds) - e.g., "Starting in 5..."
            // or the text changes to contain digits when the timer is active
            foreach (char c in text)
            {
                if (char.IsDigit(c)) return true;
            }
            return false;
        }

        #endregion

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
        /// Close the DeckSelectBlade via PlayBladeController.HideDeckSelector().
        /// This is the game's proper close flow: it closes the blade AND reactivates
        /// the UnifiedChallengeDisplay (which contains Leave and Invite buttons).
        /// Calling DeckSelectBlade.Hide() directly would leave the challenge display
        /// deactivated, making Leave and Invite buttons permanently invisible.
        /// </summary>
        public static void CloseDeckSelectBlade()
        {
            try
            {
                if (_playBladeControllerType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        _playBladeControllerType = asm.GetType("PlayBladeController");
                        if (_playBladeControllerType != null) break;
                    }
                    if (_playBladeControllerType == null) return;
                }

                // Find the active PlayBladeController
                var controllers = UnityEngine.Object.FindObjectsOfType(_playBladeControllerType);
                foreach (var controller in controllers)
                {
                    var mb = controller as MonoBehaviour;
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                    // Resolve DeckSelector field (needed to verify controller has one)
                    if (_deckSelectorField == null)
                        _deckSelectorField = _playBladeControllerType.GetField("DeckSelector",
                            BindingFlags.Public | BindingFlags.Instance);
                    if (_deckSelectorField == null) continue;

                    var deckSelector = _deckSelectorField.GetValue(controller);
                    if (deckSelector == null) continue;

                    // Call HideDeckSelector() unconditionally - it closes the blade AND reactivates
                    // the challenge display. Safe to call even when blade is already hidden:
                    // - DeckSelector.Hide() is a no-op when already hidden
                    // - _unifiedChallengeDisplay.SetActive(true) restores Leave/Invite buttons
                    // This is needed because the game sometimes calls DeckSelectBlade.Hide() directly
                    // (e.g., after deck selection), which hides the blade but leaves the display inactive.
                    if (_hideDeckSelectorMethod == null)
                    {
                        _hideDeckSelectorMethod = _playBladeControllerType.GetMethod("HideDeckSelector",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                    if (_hideDeckSelectorMethod != null)
                    {
                        _hideDeckSelectorMethod.Invoke(controller, null);
                        MelonLogger.Msg("[ChallengeHelper] Closed DeckSelectBlade via PlayBladeController.HideDeckSelector");
                    }
                    break;
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

            // Find types
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_challengeDisplayType == null)
                    _challengeDisplayType = asm.GetType("UnifiedChallengeDisplay");
                if (_playerDisplayType == null)
                    _playerDisplayType = asm.GetType("Wizards.Mtga.PrivateGame.ChallengePlayerDisplay");
                if (_bladeWidgetType == null)
                    _bladeWidgetType = asm.GetType("UnifiedChallengeBladeWidget");
                if (_challengeDisplayType != null && _playerDisplayType != null && _bladeWidgetType != null)
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

            if (_bladeWidgetType != null)
            {
                _challengeStatusTextField = _bladeWidgetType.GetField("_challengeStatusText", flags);

                // Try both property and field for settings lock
                _isChallengeSettingsLockedProp = _bladeWidgetType.GetProperty("IsChallengeSettingsLocked",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_isChallengeSettingsLockedProp == null)
                    _isChallengeSettingsLockedField = _bladeWidgetType.GetField("IsChallengeSettingsLocked",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (_isChallengeSettingsLockedField == null)
                    _isChallengeSettingsLockedField = _bladeWidgetType.GetField("_isChallengeSettingsLocked", flags);
            }

            MelonLogger.Msg($"[ChallengeHelper] Reflection init: display={_challengeDisplayType != null}, " +
                $"player={_playerDisplayType != null}, widget={_bladeWidgetType != null}, " +
                $"statusField={_challengeStatusTextField != null}, " +
                $"lockProp={_isChallengeSettingsLockedProp != null}, lockField={_isChallengeSettingsLockedField != null}");
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

        /// <summary>
        /// Find the active UnifiedChallengeBladeWidget in the scene.
        /// </summary>
        private static UnityEngine.Object FindBladeWidget()
        {
            if (_bladeWidgetType == null) return null;

            var objects = UnityEngine.Object.FindObjectsOfType(_bladeWidgetType);
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
