using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for player portrait/timer interactions during duels.
    /// Provides V key zone for player info, property cycling, and emotes.
    /// Core partial: state machine + zone entry/exit + input routing + focus management.
    /// </summary>
    public partial class PlayerPortraitNavigator
    {
        private readonly IAnnouncementService _announcer;
        private bool _isActive;

        // State machine for V key navigation
        private enum NavigationState { Inactive, PlayerNavigation, EmoteNavigation }
        private NavigationState _navigationState = NavigationState.Inactive;
        private int _currentPlayerIndex = 0; // 0 = You, 1 = Opponent
        private int _currentPropertyIndex = 0;

        // Focus management - store previous focus to restore on exit
        private GameObject _previousFocus;
        private GameObject _playerZoneFocusElement;

        public PlayerPortraitNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        public void Activate()
        {
            _isActive = true;
            DiscoverTimerElements();
            SubscribeLowTimeWarnings();
            Log.Nav("PlayerPortrait", $"Activated");
        }

        public void Deactivate()
        {
            _isActive = false;
            _navigationState = NavigationState.Inactive;
            _emoteButtons.Clear();
            UnsubscribeLowTimeWarnings();
            _localTimerObj = null;
            _opponentTimerObj = null;
            _localMatchTimer = null;
            _opponentMatchTimer = null;
            _localLowTimeWarning = null;
            _opponentLowTimeWarning = null;
            _loggedTimerPlayer = false;
            _loggedTimerOpponent = false;
            Log.Nav("PlayerPortrait", $"Deactivated");
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
                Log.Nav("PlayerPortrait", $"Focus changed to '{newFocus.name}', auto-exiting player info zone");
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
                    name.Contains("AvatarView") ||
                    name.Contains("PortraitButton") ||
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

            // Store current focus to restore on exit.
            // Unity's fake-null: a destroyed GameObject is != null in C# but throws from .name.
            _previousFocus = EventSystem.current?.currentSelectedGameObject;
            if (_previousFocus != null && !_previousFocus) _previousFocus = null;
            Log.Nav("PlayerPortrait", $"Storing previous focus: {_previousFocus?.name ?? "null"}");

            // Find and focus on the player zone element (local timer's HoverArea)
            _playerZoneFocusElement = FindPlayerZoneFocusElement();
            if (_playerZoneFocusElement != null)
            {
                EventSystem.current?.SetSelectedGameObject(_playerZoneFocusElement);
                Log.Nav("PlayerPortrait", $"Set focus to: {_playerZoneFocusElement.name}");
            }

            _navigationState = NavigationState.PlayerNavigation;
            _currentPlayerIndex = 0; // Start on local player
            _currentPropertyIndex = 0; // Start on Life

            var lifeValue = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
            string announcement = $"{Strings.PlayerInfo}. {lifeValue}";
            _announcer.Announce(announcement, AnnouncementPriority.High);
            Log.Nav("PlayerPortrait", $"Entered player info zone");
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
                Log.Nav("PlayerPortrait", $"Restored focus to: {_previousFocus.name}");
            }
            _previousFocus = null;
            _playerZoneFocusElement = null;

            Log.Nav("PlayerPortrait", $"Exited player info zone");
        }

        /// <summary>
        /// Finds the PortraitButton element to focus on when entering player zone.
        /// </summary>
        private GameObject FindPlayerZoneFocusElement()
        {
            var avatarView = FindAvatarView(isLocal: true);
            if (avatarView != null && _avatarReflectionInitialized)
            {
                var portraitButton = _portraitButtonField.GetValue(avatarView) as MonoBehaviour;
                if (portraitButton != null)
                    return portraitButton.gameObject;
            }

            // Fallback: use local timer's HoverArea
            if (_localTimerObj != null)
            {
                var iconTransform = _localTimerObj.transform.Find("Icon");
                if (iconTransform != null)
                {
                    var hoverArea = iconTransform.Find("HoverArea");
                    if (hoverArea != null)
                        return hoverArea.gameObject;
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
                Log.Nav("PlayerPortrait", $"Focus moved to '{currentFocus.name}', exiting player zone");
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
                    _announcer.AnnounceVerbose(Strings.EndOfZone, AnnouncementPriority.Normal);
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
                    _announcer.AnnounceVerbose(Strings.EndOfZone, AnnouncementPriority.Normal);
                }
                return true;
            }

            // Up/Down cycles through properties (skipping rows where neither player has content)
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                int next = FindNextVisibleProperty(_currentPropertyIndex, forward: true);
                if (next >= 0)
                {
                    _currentPropertyIndex = next;
                    var value = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
                    _announcer.Announce(value, AnnouncementPriority.High);
                }
                else
                {
                    _announcer.AnnounceVerbose(Strings.EndOfProperties, AnnouncementPriority.Normal);
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                int next = FindNextVisibleProperty(_currentPropertyIndex, forward: false);
                if (next >= 0)
                {
                    _currentPropertyIndex = next;
                    var value = GetPropertyValue((PlayerProperty)_currentPropertyIndex);
                    _announcer.Announce(value, AnnouncementPriority.High);
                }
                else
                {
                    _announcer.AnnounceVerbose(Strings.EndOfProperties, AnnouncementPriority.Normal);
                }
                return true;
            }

            // Enter: open emote menu (local player only)
            // Use InputManager to consume the key so game doesn't also process it
            if (InputManager.GetEnterAndConsume())
            {
                Log.Nav("PlayerPortrait", $"Enter pressed and consumed in PlayerNavigation, playerIndex={_currentPlayerIndex}");

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
    }
}
