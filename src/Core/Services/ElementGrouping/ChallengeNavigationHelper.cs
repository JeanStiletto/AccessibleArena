using System;
using System.Reflection;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using MelonLoader;
using TMPro;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

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

        private sealed class ChallengeHandles
        {
            public Type ChallengeDisplayType;
            public Type PlayerDisplayType;
            public Type BladeWidgetType;

            public FieldInfo LocalPlayer;
            public FieldInfo EnemyPlayer;
            public FieldInfo PlayerName;
            public FieldInfo NoPlayer;
            public FieldInfo PlayerInvited;

            public FieldInfo ChallengeStatusText;
            public FieldInfo IsChallengeSettingsLockedField;
            public PropertyInfo IsChallengeSettingsLockedProp;
        }

        private sealed class PlayBladeHandles
        {
            public Type ControllerType;
            public FieldInfo DeckSelector;
            public MethodInfo HideDeckSelector;
        }

        private static readonly ReflectionCache<ChallengeHandles> _challengeCache = new ReflectionCache<ChallengeHandles>(
            builder: _ =>
            {
                var h = new ChallengeHandles
                {
                    ChallengeDisplayType = FindType("UnifiedChallengeDisplay"),
                    PlayerDisplayType = FindType("Wizards.Mtga.PrivateGame.ChallengePlayerDisplay"),
                    BladeWidgetType = FindType("UnifiedChallengeBladeWidget"),
                };

                if (h.ChallengeDisplayType != null)
                {
                    h.LocalPlayer = h.ChallengeDisplayType.GetField("_localPlayerDisplay", PrivateInstance);
                    h.EnemyPlayer = h.ChallengeDisplayType.GetField("_enemyPlayerDisplay", PrivateInstance);
                }

                if (h.PlayerDisplayType != null)
                {
                    h.PlayerName = h.PlayerDisplayType.GetField("_playerName", PrivateInstance);
                    h.NoPlayer = h.PlayerDisplayType.GetField("_noPlayer", PrivateInstance);
                    h.PlayerInvited = h.PlayerDisplayType.GetField("_playerInvited", PrivateInstance);
                }

                if (h.BladeWidgetType != null)
                {
                    h.ChallengeStatusText = h.BladeWidgetType.GetField("_challengeStatusText", PrivateInstance);
                    h.IsChallengeSettingsLockedProp = h.BladeWidgetType.GetProperty("IsChallengeSettingsLocked", AllInstanceFlags);
                    if (h.IsChallengeSettingsLockedProp == null)
                        h.IsChallengeSettingsLockedField = h.BladeWidgetType.GetField("IsChallengeSettingsLocked", AllInstanceFlags);
                    if (h.IsChallengeSettingsLockedField == null)
                        h.IsChallengeSettingsLockedField = h.BladeWidgetType.GetField("_isChallengeSettingsLocked", PrivateInstance);
                }

                return h;
            },
            validator: h => h.ChallengeDisplayType != null && h.PlayerDisplayType != null,
            logTag: "ChallengeHelper",
            logSubject: "Challenge");

        private static readonly ReflectionCache<PlayBladeHandles> _playBladeCache = new ReflectionCache<PlayBladeHandles>(
            builder: _ =>
            {
                var h = new PlayBladeHandles
                {
                    ControllerType = FindType("PlayBladeController"),
                };
                if (h.ControllerType != null)
                {
                    h.DeckSelector = h.ControllerType.GetField("DeckSelector", PublicInstance);
                    h.HideDeckSelector = h.ControllerType.GetMethod("HideDeckSelector", PublicInstance);
                }
                return h;
            },
            validator: h => h.ControllerType != null,
            logTag: "ChallengeHelper",
            logSubject: "PlayBladeController");

        // Polling state for player status changes
        private enum EnemyState { NotInvited, Invited, Joined }
        private EnemyState _lastEnemyState = EnemyState.NotInvited;
        private string _lastEnemyName;
        private string _lastLocalStatus;
        private string _lastStatusText;
        private bool _wasCountdownActive;
        private float _pollTimer;
        private const float PollIntervalSeconds = 1.0f;
        private bool _pollingInitialized;

        // Element indices for label updates during polling
        private int _opponentElementIndex = -1;
        private int _mainButtonElementIndex = -1;
        private int _statusElementIndex = -1;

        /// <summary>
        /// Set by polling when local player status changes (game swaps buttons).
        /// GeneralMenuNavigator checks this and triggers a rescan.
        /// </summary>
        public bool RescanRequested { get; private set; }

        public void ClearRescanRequest() => RescanRequested = false;

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
                    Log.Msg("ChallengeHelper", "Select Deck activated -> requesting folders entry");
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
                    Log.Msg("ChallengeHelper", "Backspace: exited folders, going to ChallengeMain");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.ChallengeMain)
                {
                    // Was inside ChallengeMain -> close the challenge blade
                    Log.Msg("ChallengeHelper", "Backspace: exited ChallengeMain, closing blade");
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
                    Log.Msg("ChallengeHelper", "Backspace: at folder level, going to ChallengeMain");
                    return PlayBladeResult.RescanNeeded;
                }
                else if (groupType == ElementGroup.ChallengeMain)
                {
                    // At ChallengeMain level -> close the challenge blade
                    Log.Msg("ChallengeHelper", "Backspace: at ChallengeMain level, closing blade");
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

            Log.Msg("ChallengeHelper", "Challenge opened, set context and requesting ChallengeMain entry");
        }

        /// <summary>
        /// Called when challenge screen closes. Clears the challenge context and polling state.
        /// </summary>
        public void OnChallengeClosed()
        {
            _groupedNavigator.SetChallengeContext(false);
            _pollingInitialized = false;
            _opponentElementIndex = -1;
            _mainButtonElementIndex = -1;
            _statusElementIndex = -1;
            Log.Msg("ChallengeHelper", "Challenge closed, cleared context");
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
            Log.Msg("ChallengeHelper", "Deck selected, closing blade and requesting ChallengeMain entry");
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

            // Main challenge button: show player name + actual player status (not button text)
            // Game swaps MainButton <-> SecondaryButton when toggling Ready
            if (name == "UnifiedChallenge_MainButton" || name == "UnifiedChallenge_SecondaryButton")
            {
                string playerName = GetLocalPlayerName();
                string status = GetLocalPlayerStatus();

                // Use actual player status if available, fall back to button label
                string result = !string.IsNullOrEmpty(status) ? status : label;
                if (!string.IsNullOrEmpty(playerName))
                    result = $"{playerName}: {result}";

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

                if (_challengeCache.Handles.ChallengeStatusText == null) return null;

                var statusComponent = _challengeCache.Handles.ChallengeStatusText.GetValue(widget) as Component;
                if (statusComponent == null || !statusComponent.gameObject.activeInHierarchy)
                    return null;

                return UITextExtractor.GetText(statusComponent.gameObject);
            }
            catch (Exception ex)
            {
                Log.Msg("ChallengeHelper", $"Error getting challenge status text: {ex.Message}");
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
                if (_challengeCache.Handles.IsChallengeSettingsLockedProp != null)
                    return (bool)_challengeCache.Handles.IsChallengeSettingsLockedProp.GetValue(widget);
                if (_challengeCache.Handles.IsChallengeSettingsLockedField != null)
                    return (bool)_challengeCache.Handles.IsChallengeSettingsLockedField.GetValue(widget);

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
                Log.Msg("ChallengeHelper", $"Error getting tournament params: {ex.Message}");
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
                Log.Msg("ChallengeHelper", $"Polling error: {ex.Message}");
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
                    var enemyDisplay = _challengeCache.Handles.EnemyPlayer?.GetValue(display);
                    _lastEnemyState = GetEnemyState(enemyDisplay);
                    _lastEnemyName = GetEnemyName(enemyDisplay);
                }
                else
                {
                    _lastEnemyState = EnemyState.NotInvited;
                    _lastEnemyName = null;
                }

                _lastLocalStatus = GetLocalPlayerStatus();
                _lastStatusText = GetChallengeStatusText();
            }
            catch (Exception ex)
            {
                Log.Msg("ChallengeHelper", $"Error initializing polling state: {ex.Message}");
                _lastEnemyState = EnemyState.NotInvited;
                _lastEnemyName = null;
                _lastLocalStatus = null;
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
            var enemyDisplay = _challengeCache.Handles.EnemyPlayer?.GetValue(display);
            var currentState = GetEnemyState(enemyDisplay);
            var currentName = GetEnemyName(enemyDisplay);

            // Detect transitions
            if (currentState != _lastEnemyState)
            {
                if (currentState == EnemyState.Joined && _lastEnemyState != EnemyState.Joined)
                {
                    string name = !string.IsNullOrEmpty(currentName) ? currentName : Models.Strings.ChallengeOpponent;
                    _announcer.Announce(Models.Strings.ChallengeOpponentJoined(name), Models.AnnouncementPriority.High);
                    Log.Msg("ChallengeHelper", $"Opponent joined: {name}");
                }
                else if (_lastEnemyState == EnemyState.Joined && currentState != EnemyState.Joined)
                {
                    _announcer.Announce(Models.Strings.ChallengeOpponentLeft, Models.AnnouncementPriority.High);
                    Log.Msg("ChallengeHelper", "Opponent left");
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
                    Log.Msg("ChallengeHelper", "Match countdown started");
                    _wasCountdownActive = true;
                }
                else if (!isCountdown && wasCountdown)
                {
                    _announcer.Announce(Models.Strings.ChallengeCountdownCancelled, Models.AnnouncementPriority.High);
                    Log.Msg("ChallengeHelper", "Countdown cancelled");
                    _wasCountdownActive = false;
                }
                else if (!isCountdown)
                {
                    // Normal status text change (e.g., "Select a deck" -> "Waiting for opponent")
                    _announcer.Announce(currentStatusText, Models.AnnouncementPriority.Normal);
                    Log.Msg("ChallengeHelper", $"Status text changed: {currentStatusText}");
                }

                _lastStatusText = currentStatusText;

                // Refresh the status virtual element label so navigating back shows current text
                if (_statusElementIndex >= 0)
                {
                    _groupedNavigator.UpdateElementLabel(
                        ElementGroup.ChallengeMain, _statusElementIndex, currentStatusText);
                }
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

            // Detect local player status change and announce it
            string currentLocalStatus = GetLocalPlayerStatus();
            if (currentLocalStatus != _lastLocalStatus && !string.IsNullOrEmpty(currentLocalStatus))
            {
                string playerName = GetLocalPlayerName();
                string label = !string.IsNullOrEmpty(playerName)
                    ? $"{playerName}: {currentLocalStatus}"
                    : currentLocalStatus;
                _announcer.Announce(label, Models.AnnouncementPriority.Normal);
                _lastLocalStatus = currentLocalStatus;
                // Game swaps MainButton <-> SecondaryButton on status change
                RescanRequested = true;
            }

            // Refresh opponent virtual element label
            if (_opponentElementIndex >= 0)
            {
                string newOpponentLabel = BuildOpponentLabel(enemyDisplay);
                _groupedNavigator.UpdateElementLabel(
                    ElementGroup.ChallengeMain, _opponentElementIndex, newOpponentLabel);
            }

            // Refresh main button label (local player name + current button text)
            if (_mainButtonElementIndex >= 0)
            {
                var mainButtonGO = _groupedNavigator.GetElementFromGroup(
                    ElementGroup.ChallengeMain, _mainButtonElementIndex);
                if (mainButtonGO != null)
                {
                    string refreshed = GetRefreshedMainButtonLabel(mainButtonGO);
                    if (!string.IsNullOrEmpty(refreshed))
                    {
                        _groupedNavigator.UpdateElementLabel(
                            ElementGroup.ChallengeMain, _mainButtonElementIndex, refreshed);
                    }
                }
            }

        }

        private EnemyState GetEnemyState(object enemyDisplay)
        {
            if (enemyDisplay == null) return EnemyState.NotInvited;

            var noPlayerObj = _challengeCache.Handles.NoPlayer?.GetValue(enemyDisplay) as GameObject;
            var invitedObj = _challengeCache.Handles.PlayerInvited?.GetValue(enemyDisplay) as GameObject;

            if (noPlayerObj != null && noPlayerObj.activeSelf)
                return EnemyState.NotInvited;
            if (invitedObj != null && invitedObj.activeSelf)
                return EnemyState.Invited;

            return EnemyState.Joined;
        }

        private string GetEnemyName(object enemyDisplay)
        {
            if (enemyDisplay == null) return null;

            var nameText = _challengeCache.Handles.PlayerName?.GetValue(enemyDisplay) as TMP_Text;
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

                var localDisplay = _challengeCache.Handles.LocalPlayer?.GetValue(display);
                if (localDisplay == null) return null;

                var nameText = _challengeCache.Handles.PlayerName?.GetValue(localDisplay) as TMP_Text;
                if (nameText == null || string.IsNullOrEmpty(nameText.text)) return null;

                return StripRichTextTags(nameText.text);
            }
            catch (Exception ex)
            {
                Log.Msg("ChallengeHelper", $"Error getting local player name: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the local player's actual status text from _playerStatus (e.g., "Bereit", "Nicht bereit").
        /// This is different from the main button text which is always an action label.
        /// </summary>
        public string GetLocalPlayerStatus()
        {
            try
            {
                InitReflection();
                var display = FindChallengeDisplay();
                if (display == null) return null;

                var localDisplay = _challengeCache.Handles.LocalPlayer?.GetValue(display);
                return GetPlayerStatus(localDisplay);
            }
            catch (Exception ex)
            {
                Log.Msg("ChallengeHelper", $"Error getting local player status: {ex.Message}");
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
                if (!_playBladeCache.EnsureInitialized(typeof(ChallengeNavigationHelper))) return;
                var h = _playBladeCache.Handles;
                if (h.ControllerType == null || h.DeckSelector == null || h.HideDeckSelector == null) return;

                var controllers = UnityEngine.Object.FindObjectsOfType(h.ControllerType);
                foreach (var controller in controllers)
                {
                    var mb = controller as MonoBehaviour;
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                    var deckSelector = h.DeckSelector.GetValue(controller);
                    if (deckSelector == null) continue;

                    h.HideDeckSelector.Invoke(controller, null);
                    Log.Msg("ChallengeHelper", "Closed DeckSelectBlade via PlayBladeController.HideDeckSelector");
                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Msg("ChallengeHelper", $"Error closing DeckSelectBlade: {ex.Message}");
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
                if (_challengeCache.Handles.ChallengeDisplayType == null)
                    return null;

                // Find UnifiedChallengeDisplay in scene
                var displayComponent = FindChallengeDisplay();
                if (displayComponent == null)
                    return null;

                // Get local and enemy player displays
                var localDisplay = _challengeCache.Handles.LocalPlayer?.GetValue(displayComponent);
                var enemyDisplay = _challengeCache.Handles.EnemyPlayer?.GetValue(displayComponent);

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
                Log.Msg("ChallengeHelper", $"Error getting player status: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Set the element indices for opponent virtual element, main button, and status text
        /// within the ChallengeMain group. Called by GeneralMenuNavigator after injection.
        /// </summary>
        public void SetElementIndices(int opponentIndex, int mainButtonIndex, int statusIndex = -1)
        {
            _opponentElementIndex = opponentIndex;
            _mainButtonElementIndex = mainButtonIndex;
            _statusElementIndex = statusIndex;
        }

        /// <summary>
        /// Find the currently active challenge button (MainButton or SecondaryButton).
        /// The game swaps visibility between them on state changes, so we must find
        /// whichever is active in the scene rather than using cached indices.
        /// </summary>
        public static GameObject FindActiveChallengeButton()
        {
            // GameObject.Find only returns active GameObjects
            var mainBtn = GameObject.Find("UnifiedChallenge_MainButton");
            if (mainBtn != null && mainBtn.activeInHierarchy) return mainBtn;

            var secBtn = GameObject.Find("UnifiedChallenge_SecondaryButton");
            if (secBtn != null && secBtn.activeInHierarchy) return secBtn;

            return null;
        }

        /// <summary>
        /// Get the opponent status label for display as a virtual element.
        /// Format matches the main button style: "Name: Status" or "Gegner: Nicht eingeladen".
        /// </summary>
        public string GetOpponentStatusLabel()
        {
            try
            {
                InitReflection();
                var display = FindChallengeDisplay();
                if (display == null)
                    return $"{Models.Strings.ChallengeOpponent}: {Models.Strings.ChallengeNotInvited}";

                var enemyDisplay = _challengeCache.Handles.EnemyPlayer?.GetValue(display);
                return BuildOpponentLabel(enemyDisplay);
            }
            catch (Exception ex)
            {
                Log.Msg("ChallengeHelper", $"Error getting opponent status label: {ex.Message}");
                return $"{Models.Strings.ChallengeOpponent}: {Models.Strings.ChallengeNotInvited}";
            }
        }

        private string BuildOpponentLabel(object enemyDisplay)
        {
            if (enemyDisplay == null)
                return $"{Models.Strings.ChallengeOpponent}: {Models.Strings.ChallengeNotInvited}";

            var noPlayerObj = _challengeCache.Handles.NoPlayer?.GetValue(enemyDisplay) as GameObject;
            var invitedObj = _challengeCache.Handles.PlayerInvited?.GetValue(enemyDisplay) as GameObject;

            if (noPlayerObj != null && noPlayerObj.activeSelf)
                return $"{Models.Strings.ChallengeOpponent}: {Models.Strings.ChallengeNotInvited}";

            if (invitedObj != null && invitedObj.activeSelf)
                return $"{Models.Strings.ChallengeOpponent}: {Models.Strings.ChallengeInvited}";

            // Opponent has joined - show name + status
            string name = GetEnemyName(enemyDisplay);
            string status = GetPlayerStatus(enemyDisplay);

            if (string.IsNullOrEmpty(name))
                return $"{Models.Strings.ChallengeOpponent}: {Models.Strings.ChallengeInvited}";

            if (!string.IsNullOrEmpty(status))
                return $"{name}: {status}";
            return name;
        }

        /// <summary>
        /// Read the _playerStatus Localize text from a ChallengePlayerDisplay.
        /// </summary>
        private static string GetPlayerStatus(object playerDisplay)
        {
            if (playerDisplay == null || _challengeCache.Handles.PlayerDisplayType == null) return null;

            var statusField = _challengeCache.Handles.PlayerDisplayType.GetField("_playerStatus", PrivateInstance);
            if (statusField == null) return null;

            var statusComponent = statusField.GetValue(playerDisplay) as Component;
            if (statusComponent == null || !statusComponent.gameObject.activeInHierarchy)
                return null;

            return UITextExtractor.GetText(statusComponent.gameObject);
        }

        /// <summary>
        /// Rebuild the main button label with current player name and actual player status.
        /// Falls back to the button's native text if status is unavailable.
        /// </summary>
        public string GetRefreshedMainButtonLabel(GameObject mainButton)
        {
            if (mainButton == null) return null;

            string status = GetLocalPlayerStatus();
            if (string.IsNullOrEmpty(status))
            {
                // Fall back to button text
                status = UITextExtractor.GetText(mainButton);
            }
            if (string.IsNullOrEmpty(status)) return null;

            string playerName = GetLocalPlayerName();
            if (!string.IsNullOrEmpty(playerName))
                return $"{playerName}: {status}";
            return status;
        }

        private static bool InitReflection()
        {
            return _challengeCache.EnsureInitialized(typeof(ChallengeNavigationHelper));
        }

        private static UnityEngine.Object FindChallengeDisplay()
        {
            if (_challengeCache.Handles.ChallengeDisplayType == null) return null;

            // FindObjectsOfType with the resolved type
            var objects = UnityEngine.Object.FindObjectsOfType(_challengeCache.Handles.ChallengeDisplayType);
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
            if (_challengeCache.Handles.BladeWidgetType == null) return null;

            var objects = UnityEngine.Object.FindObjectsOfType(_challengeCache.Handles.BladeWidgetType);
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
                var noPlayerObj = _challengeCache.Handles.NoPlayer?.GetValue(playerDisplay) as GameObject;
                var invitedObj = _challengeCache.Handles.PlayerInvited?.GetValue(playerDisplay) as GameObject;

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
            var nameText = _challengeCache.Handles.PlayerName?.GetValue(playerDisplay) as TMP_Text;
            if (nameText != null)
                playerName = nameText.text;

            // Read player status via UITextExtractor on the _playerStatus Localize component
            string status = null;
            var playerDisplayMb = playerDisplay as MonoBehaviour;
            if (playerDisplayMb != null)
            {
                // Find _playerStatus field (Localize component) and read its text
                var statusField = _challengeCache.Handles.PlayerDisplayType?.GetField("_playerStatus", PrivateInstance);
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
            return UITextExtractor.StripRichText(text).Trim();
        }

        #endregion
    }
}
