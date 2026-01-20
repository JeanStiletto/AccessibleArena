using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Linq;
using TMPro;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for player portrait/timer interactions during duels.
    /// Provides V key zone for player info, property cycling, and emotes.
    /// </summary>
    public class PlayerPortraitNavigator
    {
        private readonly IAnnouncementService _announcer;
        // DEPRECATED: TargetNavigator was used to check IsTargeting to prevent exiting player zone during targeting
        // private readonly TargetNavigator _targetNavigator;
        private bool _isActive;

        // State machine for V key navigation
        private enum NavigationState { Inactive, PlayerNavigation, EmoteNavigation }
        private NavigationState _navigationState = NavigationState.Inactive;
        private int _currentPlayerIndex = 0; // 0 = You, 1 = Opponent
        private int _currentPropertyIndex = 0;

        // Property list for cycling (Username merged into Life announcement)
        private enum PlayerProperty { Life, Timer, Timeouts, Wins, Rank }
        private const int PropertyCount = 5;

        // Emote navigation
        private System.Collections.Generic.List<GameObject> _emoteButtons = new System.Collections.Generic.List<GameObject>();
        private int _currentEmoteIndex = 0;

        // Cached references to timer elements
        private GameObject _localTimerObj;
        private GameObject _opponentTimerObj;
        private MonoBehaviour _localMatchTimer;
        private MonoBehaviour _opponentMatchTimer;

        // Focus management - store previous focus to restore on exit
        private GameObject _previousFocus;
        private GameObject _playerZoneFocusElement;

        public PlayerPortraitNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
            // DEPRECATED: _targetNavigator = targetNavigator;
        }

        public void Activate()
        {
            _isActive = true;
            DiscoverTimerElements();
            MelonLogger.Msg("[PlayerPortrait] Activated");
        }

        public void Deactivate()
        {
            _isActive = false;
            _navigationState = NavigationState.Inactive;
            _emoteButtons.Clear();
            _localTimerObj = null;
            _opponentTimerObj = null;
            _localMatchTimer = null;
            _opponentMatchTimer = null;
            MelonLogger.Msg("[PlayerPortrait] Deactivated");
        }

        /// <summary>
        /// Returns true if the player info zone is currently active.
        /// </summary>
        public bool IsInPlayerInfoZone => _navigationState != NavigationState.Inactive;

        /// <summary>
        /// Called when UI focus changes. If focus moves to something outside the player zone,
        /// automatically exit the player info zone to prevent consuming keys meant for other UI.
        /// This makes player zone behave like card zones - leaving when focus moves elsewhere.
        /// </summary>
        public void OnFocusChanged(GameObject newFocus)
        {
            if (_navigationState == NavigationState.Inactive) return;
            if (newFocus == null) return;

            // Check if focus is still on player zone related elements
            if (!IsPlayerZoneElement(newFocus))
            {
                MelonLogger.Msg($"[PlayerPortrait] Focus changed to '{newFocus.name}', auto-exiting player info zone");
                ExitPlayerInfoZone();
            }
        }

        /// <summary>
        /// Checks if a GameObject is part of the player zone UI (portraits, emotes, timers).
        /// </summary>
        private bool IsPlayerZoneElement(GameObject obj)
        {
            if (obj == null) return false;

            // Check the object and its parents for player zone indicators
            Transform current = obj.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                string name = current.name;
                if (name.Contains("MatchTimer") ||
                    name.Contains("PlayerPortrait") ||
                    name.Contains("EmoteOptionsPanel") ||
                    name.Contains("CommunicationOptionsPanel") ||
                    name.Contains("EmoteView") ||
                    (name == "HoverArea" && current.parent != null &&
                     (current.parent.name == "Icon" || current.parent.name.Contains("Timer"))))
                {
                    return true;
                }
                current = current.parent;
                depth++;
            }
            return false;
        }

        /// <summary>
        /// Handles input for player info zone navigation.
        /// V = Enter player info zone
        /// L = Life totals (quick access)
        /// When in zone: Left/Right = switch player, Up/Down = cycle properties
        /// Enter = emotes (local player only), Backspace = exit zone
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;


            // V key activates player info zone
            if (Input.GetKeyDown(KeyCode.V))
            {
                EnterPlayerInfoZone();
                return true;
            }

            // L key for quick life total access (works anytime)
            if (Input.GetKeyDown(KeyCode.L))
            {
                AnnounceLifeTotals();
                return true;
            }

            // Handle emote navigation state (modal - blocks other keys)
            if (_navigationState == NavigationState.EmoteNavigation)
            {
                return HandleEmoteNavigation();
            }

            // Handle player navigation state
            if (_navigationState == NavigationState.PlayerNavigation)
            {
                return HandlePlayerNavigation();
            }

            return false;
        }

        /// <summary>
        /// Enters the player info zone, starting on local player with life total.
        /// </summary>
        private void EnterPlayerInfoZone()
        {
            DiscoverTimerElements();

            // Store current focus to restore on exit
            _previousFocus = EventSystem.current?.currentSelectedGameObject;
            MelonLogger.Msg($"[PlayerPortrait] Storing previous focus: {_previousFocus?.name ?? "null"}");

            // Find and focus on the player zone element (local timer's HoverArea)
            _playerZoneFocusElement = FindPlayerZoneFocusElement();
            if (_playerZoneFocusElement != null)
            {
                EventSystem.current?.SetSelectedGameObject(_playerZoneFocusElement);
                MelonLogger.Msg($"[PlayerPortrait] Set focus to: {_playerZoneFocusElement.name}");
            }

            _navigationState = NavigationState.PlayerNavigation;
            _currentPlayerIndex = 0; // Start on local player
            _currentPropertyIndex = 0; // Start on Life

            var lifeValue = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
            string announcement = $"{Strings.PlayerInfo}. {lifeValue}";
            _announcer.Announce(announcement, AnnouncementPriority.High);
            MelonLogger.Msg("[PlayerPortrait] Entered player info zone");
        }

        /// <summary>
        /// Exits the player info zone and restores previous focus.
        /// </summary>
        public void ExitPlayerInfoZone()
        {
            if (_navigationState == NavigationState.Inactive) return;

            _navigationState = NavigationState.Inactive;
            _emoteButtons.Clear();

            // Restore previous focus
            if (_previousFocus != null && _previousFocus) // Check both null and Unity destroyed
            {
                EventSystem.current?.SetSelectedGameObject(_previousFocus);
                MelonLogger.Msg($"[PlayerPortrait] Restored focus to: {_previousFocus.name}");
            }
            _previousFocus = null;
            _playerZoneFocusElement = null;

            MelonLogger.Msg("[PlayerPortrait] Exited player info zone");
        }

        /// <summary>
        /// Finds the HoverArea element to focus on when entering player zone.
        /// </summary>
        private GameObject FindPlayerZoneFocusElement()
        {
            // Use local timer's HoverArea as the focus element
            if (_localTimerObj != null)
            {
                var iconTransform = _localTimerObj.transform.Find("Icon");
                if (iconTransform != null)
                {
                    var hoverArea = iconTransform.Find("HoverArea");
                    if (hoverArea != null)
                    {
                        return hoverArea.gameObject;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Handles input while in player navigation state.
        /// </summary>
        private bool HandlePlayerNavigation()
        {
            // Check if focus has moved away from player zone (e.g., Tab cycled to a card)
            // This catches focus changes that happened during the same frame before OnFocusChanged fires
            var currentFocus = EventSystem.current?.currentSelectedGameObject;
            if (currentFocus != null && currentFocus && !IsPlayerZoneElement(currentFocus))
            {
                MelonLogger.Msg($"[PlayerPortrait] Focus moved to '{currentFocus.name}', exiting player zone");
                ExitPlayerInfoZone();
                return false; // Let other handlers process the key
            }

            // Backspace exits zone
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ExitPlayerInfoZone();
                return true;
            }

            // Left/Right switches between players (stays on same property)
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_currentPlayerIndex == 0)
                {
                    _currentPlayerIndex = 1;
                    var propertyValue = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
                    _announcer.Announce(propertyValue, AnnouncementPriority.High);
                }
                else
                {
                    _announcer.Announce(Strings.EndOfZone, AnnouncementPriority.Normal);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_currentPlayerIndex == 1)
                {
                    _currentPlayerIndex = 0;
                    var propertyValue = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
                    _announcer.Announce(propertyValue, AnnouncementPriority.High);
                }
                else
                {
                    _announcer.Announce(Strings.EndOfZone, AnnouncementPriority.Normal);
                }
                return true;
            }

            // Up/Down cycles through properties
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_currentPropertyIndex < PropertyCount - 1)
                {
                    _currentPropertyIndex++;
                    var value = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
                    _announcer.Announce(value, AnnouncementPriority.High);
                }
                else
                {
                    _announcer.Announce(Strings.EndOfProperties, AnnouncementPriority.Normal);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_currentPropertyIndex > 0)
                {
                    _currentPropertyIndex--;
                    var value = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
                    _announcer.Announce(value, AnnouncementPriority.High);
                }
                else
                {
                    _announcer.Announce(Strings.EndOfProperties, AnnouncementPriority.Normal);
                }
                return true;
            }

            // Enter: open emote menu (local player only)
            // Use InputManager to consume the key so game doesn't also process it
            if (InputManager.GetEnterAndConsume())
            {
                MelonLogger.Msg($"[PlayerPortrait] Enter pressed and consumed in PlayerNavigation, playerIndex={_currentPlayerIndex}");

                // DEPRECATED: Old targeting check - now handled by HotHighlightNavigator which includes player targets
                // if (_targetNavigator != null && _targetNavigator.IsTargeting)
                // {
                //     var playerAvatar = FindCurrentPlayerAvatar();
                //     if (playerAvatar != null && HasPlayerTargetingHighlight(playerAvatar))
                //     {
                //         var result = UIActivator.SimulatePointerClick(playerAvatar);
                //         string playerName = _currentPlayerIndex == 0 ? Strings.You : Strings.Opponent;
                //         if (result.Success)
                //             _announcer.Announce(Strings.Targeted(playerName), AnnouncementPriority.Normal);
                //         else
                //             _announcer.Announce(Strings.CouldNotTarget(playerName), AnnouncementPriority.Normal);
                //         return true;
                //     }
                // }

                // Open emote wheel (local player only)
                if (_currentPlayerIndex == 0)
                {
                    OpenEmoteWheel();
                }
                // Do nothing for opponent
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles input while in emote navigation state.
        /// </summary>
        private bool HandleEmoteNavigation()
        {
            // Check if focus has moved away from player zone
            var currentFocus = EventSystem.current?.currentSelectedGameObject;
            if (currentFocus != null && currentFocus && !IsPlayerZoneElement(currentFocus))
            {
                MelonLogger.Msg($"[PlayerPortrait] Focus moved to '{currentFocus.name}', exiting emote navigation");
                ExitPlayerInfoZone();
                return false;
            }

            // Backspace cancels emote menu
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseEmoteWheel();
                _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                return true;
            }

            // Up/Down navigates emotes
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_emoteButtons.Count == 0) return true;
                _currentEmoteIndex = (_currentEmoteIndex + 1) % _emoteButtons.Count;
                AnnounceCurrentEmote();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_emoteButtons.Count == 0) return true;
                _currentEmoteIndex--;
                if (_currentEmoteIndex < 0) _currentEmoteIndex = _emoteButtons.Count - 1;
                AnnounceCurrentEmote();
                return true;
            }

            // Enter selects emote - consume to block game
            if (InputManager.GetEnterAndConsume())
            {
                SelectCurrentEmote();
                return true;
            }

            // Block all other keys while emote menu is open
            return true;
        }

        /// <summary>
        /// Gets the display value for a property for the current player.
        /// </summary>
        private string GetPropertyValue(PlayerProperty property)
        {
            bool isOpponent = _currentPlayerIndex == 1;

            switch (property)
            {
                case PlayerProperty.Life:
                    var (localLife, opponentLife) = GetLifeTotals();
                    int life = isOpponent ? opponentLife : localLife;
                    string lifeText = life >= 0 ? Strings.Life(life) : Strings.LifeNotAvailable;
                    // Include username in life announcement
                    string username = GetPlayerUsername(isOpponent);
                    if (!string.IsNullOrEmpty(username))
                    {
                        return $"{username}, {lifeText}";
                    }
                    return lifeText;

                case PlayerProperty.Timer:
                    var timerObj = isOpponent ? _opponentTimerObj : _localTimerObj;
                    if (timerObj == null) return Strings.TimerNotAvailable;
                    var timerText = GetTimerText(timerObj);
                    if (string.IsNullOrEmpty(timerText)) return Strings.TimerNotAvailable;
                    return Strings.Timer(FormatTimerText(timerText));

                case PlayerProperty.Timeouts:
                    var timeoutCount = GetTimeoutCount(isOpponent ? "Opponent" : "LocalPlayer");
                    return timeoutCount >= 0 ? Strings.Timeouts(timeoutCount) : Strings.Timeouts(0);

                case PlayerProperty.Wins:
                    var wins = GetWinCount(isOpponent);
                    return wins >= 0 ? Strings.GamesWon(wins) : Strings.WinsNotAvailable;

                case PlayerProperty.Rank:
                    var rank = GetPlayerRank(isOpponent);
                    return !string.IsNullOrEmpty(rank) ? Strings.Rank(rank) : Strings.RankNotAvailable;

                default:
                    return "Unknown property";
            }
        }

        /// <summary>
        /// Gets win count for Bo3 matches. Returns 0 for Bo1 games.
        /// </summary>
        private int GetWinCount(bool isOpponent)
        {
            // In Bo1 games, there are no win pips - default to 0
            // For Bo3, we'd need to find the actual match win indicator
            // For now, return 0 as a sensible default (no games won yet in current match)
            return 0;
        }

        /// <summary>
        /// Gets player rank from RankAnchorPoint.
        /// </summary>
        private string GetPlayerRank(bool isOpponent)
        {
            string containerName = isOpponent ? "Opponent" : "LocalPlayer";
            MelonLogger.Msg($"[PlayerPortrait] Looking for rank for {containerName}");

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.Contains(containerName)) continue;

                // Look for RankAnchorPoint or similar
                var rankAnchor = go.transform.Find("RankAnchorPoint");
                if (rankAnchor == null)
                {
                    // Try alternative names
                    foreach (Transform child in go.transform)
                    {
                        if (child.name.Contains("Rank"))
                        {
                            rankAnchor = child;
                            MelonLogger.Msg($"[PlayerPortrait] Found alternative rank: {child.name}");
                            break;
                        }
                    }
                }
                if (rankAnchor == null) continue;

                MelonLogger.Msg($"[PlayerPortrait] RankAnchor found: {rankAnchor.name}");
                foreach (Transform child in rankAnchor)
                {
                    MelonLogger.Msg($"[PlayerPortrait]   Rank child: {child.name}");
                }

                // Look for TextMeshPro with rank text
                var tmpComponents = rankAnchor.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (var tmp in tmpComponents)
                {
                    MelonLogger.Msg($"[PlayerPortrait]   TMP in rank: '{tmp.text}' on {tmp.gameObject.name}");
                    if (!string.IsNullOrEmpty(tmp.text))
                    {
                        return tmp.text.Trim();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets player username from PlayerNameView.
        /// </summary>
        private string GetPlayerUsername(bool isOpponent)
        {
            string containerName = isOpponent ? "Opponent" : "LocalPlayer";
            MelonLogger.Msg($"[PlayerPortrait] Looking for username for {containerName}");

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Look for PlayerNameView objects (e.g., LocalPlayerNameView_Desktop_16x9(Clone))
                if (go.name.Contains(containerName) && go.name.Contains("NameView"))
                {
                    MelonLogger.Msg($"[PlayerPortrait] Found NameView: {go.name}");

                    // Log all children and their text
                    foreach (Transform child in go.transform)
                    {
                        MelonLogger.Msg($"[PlayerPortrait]   NameView child: {child.name}");
                    }

                    // Search for TextMeshPro components
                    var tmpComponents = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var tmp in tmpComponents)
                    {
                        MelonLogger.Msg($"[PlayerPortrait]   TMP found: '{tmp.text}' on {tmp.gameObject.name}");
                        if (!string.IsNullOrEmpty(tmp.text) && !tmp.text.Contains("Rank"))
                        {
                            return tmp.text.Trim();
                        }
                    }
                }

                // Also check for NameText objects
                if (go.name.Contains(containerName) && go.name.Contains("NameText"))
                {
                    MelonLogger.Msg($"[PlayerPortrait] Found NameText: {go.name}");
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                    {
                        MelonLogger.Msg($"[PlayerPortrait]   NameText value: '{tmp.text}'");
                        return tmp.text.Trim();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the avatar/portrait object for the currently selected player.
        /// </summary>
        private GameObject FindCurrentPlayerAvatar()
        {
            bool isOpponent = _currentPlayerIndex == 1;
            string containerName = isOpponent ? "Opponent" : "LocalPlayer";
            MelonLogger.Msg($"[PlayerPortrait] Finding avatar for {containerName}");

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                if (go.name.Contains(containerName) &&
                    (go.name.Contains("Portrait") || go.name.Contains("Avatar") || go.name.Contains("Player")))
                {
                    MelonLogger.Msg($"[PlayerPortrait] Potential avatar container: {go.name}");

                    var iconTransform = go.transform.Find("Icon");
                    if (iconTransform != null)
                    {
                        MelonLogger.Msg($"[PlayerPortrait]   Found Icon child");
                        var hoverArea = iconTransform.Find("HoverArea");
                        if (hoverArea != null)
                        {
                            MelonLogger.Msg($"[PlayerPortrait]   Found HoverArea under Icon");
                            return hoverArea.gameObject;
                        }
                    }

                    var directHover = go.transform.Find("HoverArea");
                    if (directHover != null)
                    {
                        MelonLogger.Msg($"[PlayerPortrait]   Found direct HoverArea");
                        return directHover.gameObject;
                    }

                    // Log children to help discover structure
                    MelonLogger.Msg($"[PlayerPortrait]   Children of {go.name}:");
                    foreach (Transform child in go.transform)
                    {
                        MelonLogger.Msg($"[PlayerPortrait]     - {child.name}");
                    }
                }
            }
            MelonLogger.Msg($"[PlayerPortrait] No avatar found for {containerName}");
            return null;
        }

        /// <summary>
        /// Checks if a player avatar has an active targeting highlight.
        /// </summary>
        private bool HasPlayerTargetingHighlight(GameObject avatar)
        {
            if (avatar == null) return false;

            // Check self and parent hierarchy for HotHighlight
            Transform current = avatar.transform;
            while (current != null)
            {
                foreach (Transform child in current.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null) continue;
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (child.name.Contains("HotHighlight"))
                    {
                        return true;
                    }
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Opens the emote wheel and discovers available emotes.
        /// </summary>
        private void OpenEmoteWheel()
        {
            TriggerEmoteMenu(opponent: false);

            // Give the UI a moment to open, then discover emotes
            // For now, we'll try to discover immediately - may need coroutine later
            DiscoverEmoteButtons();

            if (_emoteButtons.Count > 0)
            {
                _navigationState = NavigationState.EmoteNavigation;
                _currentEmoteIndex = 0;
                _announcer.Announce(Strings.Emotes, AnnouncementPriority.High);
                AnnounceCurrentEmote();
            }
            else
            {
                _announcer.Announce(Strings.EmotesNotAvailable, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Closes the emote wheel and returns to player navigation.
        /// </summary>
        private void CloseEmoteWheel()
        {
            _navigationState = NavigationState.PlayerNavigation;
            _emoteButtons.Clear();

            // Try to close the emote wheel by clicking elsewhere or finding close button
            // The wheel typically closes when clicking outside it
        }

        /// <summary>
        /// Discovers emote buttons from the open emote wheel.
        /// </summary>
        private void DiscoverEmoteButtons()
        {
            _emoteButtons.Clear();
            MelonLogger.Msg("[PlayerPortrait] Discovering emote buttons...");

            // Look for EmoteOptionsPanel which contains the emote wheel
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Find the EmoteOptionsPanel
                if (go.name.Contains("EmoteOptionsPanel"))
                {
                    MelonLogger.Msg($"[PlayerPortrait] Found EmoteOptionsPanel: {go.name}");

                    // Look for Container child
                    var container = go.transform.Find("Container");
                    if (container != null)
                    {
                        MelonLogger.Msg($"[PlayerPortrait] Found Container, searching for buttons...");
                        SearchForEmoteButtons(container, 0);
                    }

                    // Also search Wheel if present
                    var wheel = go.transform.Find("Wheel");
                    if (wheel != null)
                    {
                        MelonLogger.Msg($"[PlayerPortrait] Found Wheel, searching for buttons...");
                        SearchForEmoteButtons(wheel, 0);
                    }
                }

                // Also check CommunicationOptionsPanel
                if (go.name.Contains("CommunicationOptionsPanel"))
                {
                    MelonLogger.Msg($"[PlayerPortrait] Found CommunicationOptionsPanel: {go.name}");
                    SearchForEmoteButtons(go.transform, 0);
                }
            }

            MelonLogger.Msg($"[PlayerPortrait] Found {_emoteButtons.Count} emote buttons");
            _emoteButtons.Sort((a, b) => string.Compare(a.name, b.name));
        }

        /// <summary>
        /// Recursively searches for emote buttons in a transform hierarchy.
        /// </summary>
        private void SearchForEmoteButtons(Transform parent, int depth)
        {
            if (depth > 5) return; // Limit recursion depth

            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                string childName = child.name;
                string indent = new string(' ', depth * 2);
                MelonLogger.Msg($"[PlayerPortrait] {indent}Child: {childName}");

                // Skip navigation arrows and utility buttons - not actual emotes
                if (childName.Contains("NavArrow") || childName == "Mute Container")
                {
                    MelonLogger.Msg($"[PlayerPortrait] {indent}  -> Skipping (navigation/utility)");
                    continue;
                }

                // Check for Button component
                var button = child.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    // Only add if it has emote text or is in an EmoteView
                    var text = ExtractEmoteNameFromTransform(child);
                    bool hasEmoteText = !string.IsNullOrEmpty(text) && !text.Contains("NavArrow");
                    bool isInEmoteView = IsChildOfEmoteView(child);

                    if (hasEmoteText || isInEmoteView)
                    {
                        MelonLogger.Msg($"[PlayerPortrait] {indent}  -> Adding emote button: '{text}'");
                        _emoteButtons.Add(child.gameObject);
                    }
                    else
                    {
                        MelonLogger.Msg($"[PlayerPortrait] {indent}  -> Skipping button (no emote text)");
                    }
                }

                // Recurse into children
                if (child.childCount > 0)
                {
                    SearchForEmoteButtons(child, depth + 1);
                }
            }
        }

        /// <summary>
        /// Checks if a transform is a child of an EmoteView element.
        /// </summary>
        private bool IsChildOfEmoteView(Transform t)
        {
            Transform current = t.parent;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (current.name.Contains("EmoteView"))
                    return true;
                current = current.parent;
                depth++;
            }
            return false;
        }

        /// <summary>
        /// Extracts emote text from a transform without adding to list.
        /// </summary>
        private string ExtractEmoteNameFromTransform(Transform t)
        {
            var tmpComponents = t.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var tmp in tmpComponents)
            {
                if (!string.IsNullOrEmpty(tmp.text))
                {
                    return tmp.text.Trim();
                }
            }
            return null;
        }

        /// <summary>
        /// Announces the currently selected emote.
        /// </summary>
        private void AnnounceCurrentEmote()
        {
            if (_currentEmoteIndex < 0 || _currentEmoteIndex >= _emoteButtons.Count) return;

            var emoteObj = _emoteButtons[_currentEmoteIndex];
            string emoteName = ExtractEmoteName(emoteObj);
            _announcer.Announce(emoteName, AnnouncementPriority.High);
        }

        /// <summary>
        /// Extracts the emote name from an emote button object.
        /// </summary>
        private string ExtractEmoteName(GameObject emoteObj)
        {
            // Try to get text from the button
            var tmpComponents = emoteObj.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var tmp in tmpComponents)
            {
                if (!string.IsNullOrEmpty(tmp.text))
                {
                    return tmp.text.Trim();
                }
            }

            // Fall back to parsing object name (e.g., "EmoteButton_Hello" -> "Hello")
            string name = emoteObj.name;
            if (name.Contains("_"))
            {
                var parts = name.Split('_');
                return parts[parts.Length - 1];
            }

            return name;
        }

        /// <summary>
        /// Selects and sends the current emote.
        /// </summary>
        private void SelectCurrentEmote()
        {
            if (_currentEmoteIndex < 0 || _currentEmoteIndex >= _emoteButtons.Count)
            {
                _announcer.Announce(Strings.EmotesNotAvailable, AnnouncementPriority.Normal);
                return;
            }

            var emoteObj = _emoteButtons[_currentEmoteIndex];
            string emoteName = ExtractEmoteName(emoteObj);

            var result = UIActivator.SimulatePointerClick(emoteObj);
            if (result.Success)
            {
                _announcer.Announce(Strings.EmoteSent(emoteName), AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce($"Could not send {emoteName}", AnnouncementPriority.Normal);
            }

            // Return to player navigation
            _navigationState = NavigationState.PlayerNavigation;
            _emoteButtons.Clear();
        }

        private void DiscoverTimerElements()
        {
            // Find MatchTimer components
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                string typeName = mb.GetType().Name;
                if (typeName == "MatchTimer")
                {
                    string objName = mb.gameObject.name;
                    if (objName.Contains("LocalPlayer"))
                    {
                        _localTimerObj = mb.gameObject;
                        _localMatchTimer = mb;
                        MelonLogger.Msg($"[PlayerPortrait] Found local timer: {objName}");
                    }
                    else if (objName.Contains("Opponent"))
                    {
                        _opponentTimerObj = mb.gameObject;
                        _opponentMatchTimer = mb;
                        MelonLogger.Msg($"[PlayerPortrait] Found opponent timer: {objName}");
                    }
                }
            }

            // Also find the Timer_Player and Timer_Opponent for timeout pips
            var timerPlayer = GameObject.Find("Timer_Player");
            var timerOpponent = GameObject.Find("Timer_Opponent");

            if (timerPlayer != null)
                MelonLogger.Msg($"[PlayerPortrait] Found Timer_Player for timeouts");
            if (timerOpponent != null)
                MelonLogger.Msg($"[PlayerPortrait] Found Timer_Opponent for timeouts");
        }

        private void AnnounceLifeTotals()
        {
            var (localLife, opponentLife) = GetLifeTotals();

            string announcement;
            if (localLife >= 0 && opponentLife >= 0)
            {
                announcement = $"You {localLife} life. Opponent {opponentLife} life";
            }
            else if (localLife >= 0)
            {
                announcement = $"You {localLife} life. Opponent life unknown";
            }
            else if (opponentLife >= 0)
            {
                announcement = $"Your life unknown. Opponent {opponentLife} life";
            }
            else
            {
                announcement = "Life totals not available";
            }

            _announcer.Announce(announcement, AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Gets life totals from GameManager's game state.
        /// Returns (localLife, opponentLife), -1 if not found.
        /// </summary>
        private (int localLife, int opponentLife) GetLifeTotals()
        {
            int localLife = -1;
            int opponentLife = -1;

            try
            {
                // Find GameManager
                MonoBehaviour gameManager = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "GameManager")
                    {
                        gameManager = mb;
                        break;
                    }
                }

                if (gameManager == null)
                {
                    MelonLogger.Msg("[PlayerPortrait] GameManager not found");
                    return (-1, -1);
                }

                var gmType = gameManager.GetType();

                // Try CurrentGameState first, then LatestGameState
                object gameState = null;
                var currentStateProp = gmType.GetProperty("CurrentGameState");
                if (currentStateProp != null)
                {
                    gameState = currentStateProp.GetValue(gameManager);
                }

                if (gameState == null)
                {
                    var latestStateProp = gmType.GetProperty("LatestGameState");
                    if (latestStateProp != null)
                    {
                        gameState = latestStateProp.GetValue(gameManager);
                    }
                }

                if (gameState == null)
                {
                    MelonLogger.Msg("[PlayerPortrait] GameState not available");
                    return (-1, -1);
                }

                // Get LocalPlayer and Opponent directly from game state
                var gsType = gameState.GetType();

                // Get local player life
                var localPlayerProp = gsType.GetProperty("LocalPlayer");
                if (localPlayerProp != null)
                {
                    var localPlayer = localPlayerProp.GetValue(gameState);
                    if (localPlayer != null)
                    {
                        localLife = GetPlayerLife(localPlayer);
                        MelonLogger.Msg($"[PlayerPortrait] Local player life: {localLife}");
                    }
                }

                // Get opponent life
                var opponentProp = gsType.GetProperty("Opponent");
                if (opponentProp != null)
                {
                    var opponent = opponentProp.GetValue(gameState);
                    if (opponent != null)
                    {
                        opponentLife = GetPlayerLife(opponent);
                        MelonLogger.Msg($"[PlayerPortrait] Opponent life: {opponentLife}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[PlayerPortrait] Error getting life totals: {ex.Message}");
            }

            return (localLife, opponentLife);
        }

        /// <summary>
        /// Extracts life total from an MtgPlayer object.
        /// </summary>
        private int GetPlayerLife(object player)
        {
            if (player == null) return -1;

            var playerType = player.GetType();
            var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            // Try various property names for life
            string[] lifeNames = { "LifeTotal", "Life", "CurrentLife", "StartingLife", "_life", "_lifeTotal", "life", "lifeTotal" };

            // Check properties first
            foreach (var propName in lifeNames)
            {
                var lifeProp = playerType.GetProperty(propName, bindingFlags);
                if (lifeProp != null)
                {
                    try
                    {
                        var lifeVal = lifeProp.GetValue(player);
                        if (lifeVal != null)
                        {
                            if (lifeVal is int intLife) return intLife;
                            if (int.TryParse(lifeVal.ToString(), out int parsed)) return parsed;
                        }
                    }
                    catch { }
                }
            }

            // Check fields
            foreach (var fieldName in lifeNames)
            {
                var lifeField = playerType.GetField(fieldName, bindingFlags);
                if (lifeField != null)
                {
                    try
                    {
                        var lifeVal = lifeField.GetValue(player);
                        if (lifeVal != null)
                        {
                            if (lifeVal is int intLife) return intLife;
                            if (int.TryParse(lifeVal.ToString(), out int parsed)) return parsed;
                        }
                    }
                    catch { }
                }
            }

            // Log all properties and fields for debugging
            MelonLogger.Msg($"[PlayerPortrait] MtgPlayer properties:");
            foreach (var prop in playerType.GetProperties(bindingFlags))
            {
                try
                {
                    var val = prop.GetValue(player);
                    MelonLogger.Msg($"[PlayerPortrait]   Prop {prop.Name}: {val}");
                }
                catch { }
            }

            MelonLogger.Msg($"[PlayerPortrait] MtgPlayer fields:");
            foreach (var field in playerType.GetFields(bindingFlags))
            {
                try
                {
                    var val = field.GetValue(player);
                    var valStr = val?.ToString() ?? "null";
                    if (valStr.Length > 50) valStr = valStr.Substring(0, 50) + "...";
                    MelonLogger.Msg($"[PlayerPortrait]   Field {field.Name}: {valStr}");
                }
                catch { }
            }

            return -1;
        }

        private string GetPlayerInfo(GameObject timerObj, MonoBehaviour matchTimer, string playerLabel)
        {
            if (timerObj == null)
            {
                return $"{playerLabel} timer not found";
            }

            var parts = new System.Collections.Generic.List<string>();
            parts.Add(playerLabel);

            // Get timer text (shows remaining match time like "00:00")
            var timerText = GetTimerText(timerObj);
            if (!string.IsNullOrEmpty(timerText))
            {
                // Format time more naturally
                parts.Add($"timer {FormatTimerText(timerText)}");
            }

            // Get timeout count from TimeoutDisplay
            var timeoutCount = GetTimeoutCount(playerLabel == "Your" ? "LocalPlayer" : "Opponent");
            if (timeoutCount >= 0)
            {
                parts.Add($"{timeoutCount} timeouts");
            }

            // Try to get additional info from MatchTimer component
            if (matchTimer != null)
            {
                var additionalInfo = GetMatchTimerInfo(matchTimer);
                if (!string.IsNullOrEmpty(additionalInfo))
                {
                    parts.Add(additionalInfo);
                }
            }

            return string.Join(". ", parts);
        }

        private string GetTimerText(GameObject timerObj)
        {
            // Find TextMeshProUGUI child named "Text"
            var textChild = timerObj.transform.Find("Text");
            if (textChild != null)
            {
                var tmp = textChild.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    return tmp.text;
                }
            }

            // Fallback: search all TMP children
            var tmpComponents = timerObj.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var tmp in tmpComponents)
            {
                if (tmp.gameObject.name == "Text")
                {
                    return tmp.text;
                }
            }

            return null;
        }

        private string FormatTimerText(string timerText)
        {
            // Timer is in format "MM:SS" - make it more readable
            if (string.IsNullOrEmpty(timerText)) return timerText;

            var parts = timerText.Split(':');
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
                {
                    if (minutes == 0 && seconds == 0)
                    {
                        return "no time";
                    }
                    else if (minutes == 0)
                    {
                        return $"{seconds} seconds";
                    }
                    else if (seconds == 0)
                    {
                        return $"{minutes} minutes";
                    }
                    else
                    {
                        return $"{minutes} minutes {seconds} seconds";
                    }
                }
            }

            return timerText;
        }

        private int GetTimeoutCount(string playerType)
        {
            // Find the TimeoutDisplay for this player
            var displayName = playerType == "LocalPlayer"
                ? "LocalPlayerTimeoutDisplay_Desktop_16x9(Clone)"
                : "OpponentTimeoutDisplay_Desktop_16x9(Clone)";

            var displayObj = GameObject.Find(displayName);
            if (displayObj == null) return -1;

            // Find the Text child with timeout count (shows "x0", "x1", etc.)
            var tmpComponents = displayObj.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var tmp in tmpComponents)
            {
                var text = tmp.text?.Trim() ?? "";
                if (text.StartsWith("x") && int.TryParse(text.Substring(1), out int count))
                {
                    return count;
                }
            }

            return -1;
        }

        private string GetMatchTimerInfo(MonoBehaviour matchTimer)
        {
            // Try to get additional properties from MatchTimer component
            var type = matchTimer.GetType();

            // Look for useful properties
            var timeRemaining = GetProperty<float>(type, matchTimer, "TimeRemaining");
            var isLowTime = GetProperty<bool>(type, matchTimer, "IsLowTime");
            var isWarning = GetProperty<bool>(type, matchTimer, "IsWarning");

            var info = new System.Collections.Generic.List<string>();

            if (isLowTime || isWarning)
            {
                info.Add("low time warning");
            }

            return string.Join(", ", info);
        }

        private T GetProperty<T>(System.Type type, object obj, string propName)
        {
            try
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    return (T)prop.GetValue(obj);
                }
            }
            catch { }

            return default;
        }

        /// <summary>
        /// Clicks on the player's HoverArea to potentially trigger emote menu.
        /// </summary>
        public void TriggerEmoteMenu(bool opponent = false)
        {
            var timerObj = opponent ? _opponentTimerObj : _localTimerObj;
            if (timerObj == null)
            {
                DiscoverTimerElements();
                timerObj = opponent ? _opponentTimerObj : _localTimerObj;
            }

            if (timerObj == null)
            {
                _announcer.Announce("Timer not found", AnnouncementPriority.Normal);
                return;
            }

            // Find the HoverArea child (Icon/HoverArea path)
            var iconTransform = timerObj.transform.Find("Icon");
            if (iconTransform != null)
            {
                var hoverArea = iconTransform.Find("HoverArea");
                if (hoverArea != null)
                {
                    MelonLogger.Msg($"[PlayerPortrait] Clicking HoverArea for {(opponent ? "opponent" : "local")} portrait");
                    UIActivator.SimulatePointerClick(hoverArea.gameObject);
                    _announcer.Announce(opponent ? "Opponent portrait clicked" : "Your portrait clicked", AnnouncementPriority.Normal);
                    return;
                }
            }

            _announcer.Announce("Portrait hover area not found", AnnouncementPriority.Normal);
        }
    }
}
