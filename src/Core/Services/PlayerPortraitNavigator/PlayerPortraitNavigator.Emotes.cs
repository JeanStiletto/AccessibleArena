using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System;
using System.Reflection;
using TMPro;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Emote wheel handling: opening via PortraitButton, discovery of EmoteView children,
    /// navigation within the wheel, emote selection, and avatar reflection for PortraitButton access.
    /// </summary>
    public partial class PlayerPortraitNavigator
    {
        // Emote navigation
        private System.Collections.Generic.List<GameObject> _emoteButtons = new System.Collections.Generic.List<GameObject>();
        private int _currentEmoteIndex = 0;

        // Avatar reflection cache (for emote wheel via PortraitButton)
        private static PropertyInfo _isLocalPlayerProp;
        private static FieldInfo _portraitButtonField;
        private static bool _avatarReflectionInitialized;

        /// <summary>
        /// Handles input while in emote navigation state.
        /// </summary>
        private bool HandleEmoteNavigation()
        {
            // Check if focus has moved away from player zone
            var currentFocus = EventSystem.current?.currentSelectedGameObject;
            if (currentFocus != null && currentFocus && !IsPlayerZoneElement(currentFocus))
            {
                Log.Nav("PlayerPortrait", $"Focus moved to '{currentFocus.name}', exiting emote navigation");
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
            Log.Nav("PlayerPortrait", $"Discovering emote buttons...");

            // Look for EmoteOptionsPanel which contains the emote wheel
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Find the EmoteOptionsPanel
                if (go.name.Contains("EmoteOptionsPanel"))
                {
                    Log.Nav("PlayerPortrait", $"Found EmoteOptionsPanel: {go.name}");

                    // Look for Container child
                    var container = go.transform.Find("Container");
                    if (container != null)
                    {
                        Log.Nav("PlayerPortrait", $"Found Container, searching for buttons...");
                        SearchForEmoteButtons(container, 0);
                    }

                    // Also search Wheel if present
                    var wheel = go.transform.Find("Wheel");
                    if (wheel != null)
                    {
                        Log.Nav("PlayerPortrait", $"Found Wheel, searching for buttons...");
                        SearchForEmoteButtons(wheel, 0);
                    }
                }

                // Also check CommunicationOptionsPanel
                if (go.name.Contains("CommunicationOptionsPanel"))
                {
                    Log.Nav("PlayerPortrait", $"Found CommunicationOptionsPanel: {go.name}");
                    SearchForEmoteButtons(go.transform, 0);
                }
            }

            Log.Nav("PlayerPortrait", $"Found {_emoteButtons.Count} emote buttons");
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
                Log.Nav("PlayerPortrait", $"{indent}Child: {childName}");

                // Skip navigation arrows and utility buttons - not actual emotes
                if (childName.Contains("NavArrow") || childName == "Mute Container")
                {
                    Log.Nav("PlayerPortrait", $"{indent}  -> Skipping (navigation/utility)");
                    continue;
                }

                // EmoteView objects are the clickable emotes (no standard UI.Button)
                if (childName.Contains("EmoteView"))
                {
                    var text = ExtractEmoteNameFromTransform(child);
                    if (!string.IsNullOrEmpty(text))
                    {
                        Log.Nav("PlayerPortrait", $"{indent}  -> Adding emote: '{text}'");
                        _emoteButtons.Add(child.gameObject);
                    }
                    continue; // Don't recurse into EmoteView children
                }

                // Recurse into children to find EmoteViews
                if (child.childCount > 0)
                {
                    SearchForEmoteButtons(child, depth + 1);
                }
            }
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
                _announcer.Announce(Strings.CouldNotSend(emoteName), AnnouncementPriority.Normal);
            }

            // Return to player navigation
            _navigationState = NavigationState.PlayerNavigation;
            _emoteButtons.Clear();
        }

        /// <summary>
        /// Initializes reflection cache for DuelScene_AvatarView fields.
        /// </summary>
        private static void InitializeAvatarReflection(System.Type avatarType)
        {
            try
            {
                _isLocalPlayerProp = avatarType.GetProperty("IsLocalPlayer", PublicInstance);
                if (_isLocalPlayerProp == null)
                {
                    MelonLogger.Warning("[PlayerPortrait] Could not find IsLocalPlayer property on DuelScene_AvatarView");
                    return;
                }

                _portraitButtonField = avatarType.GetField("PortraitButton", PrivateInstance);
                if (_portraitButtonField == null)
                {
                    MelonLogger.Warning("[PlayerPortrait] Could not find PortraitButton field on DuelScene_AvatarView");
                    return;
                }

                _avatarReflectionInitialized = true;
                MelonLogger.Msg($"[PlayerPortrait] Avatar reflection initialized: PortraitButton={_portraitButtonField.FieldType.Name}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[PlayerPortrait] Failed to initialize avatar reflection: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the DuelScene_AvatarView MonoBehaviour for the local or opponent player.
        /// </summary>
        private MonoBehaviour FindAvatarView(bool isLocal)
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if (typeName != "DuelScene_AvatarView") continue;

                if (!_avatarReflectionInitialized)
                    InitializeAvatarReflection(mb.GetType());
                if (!_avatarReflectionInitialized) return null;

                bool mbIsLocal = (bool)_isLocalPlayerProp.GetValue(mb);
                if (mbIsLocal == isLocal) return mb;
            }
            return null;
        }

        /// <summary>
        /// Clicks the local player's PortraitButton to open/close the emote wheel.
        /// </summary>
        public void TriggerEmoteMenu(bool opponent = false)
        {
            var avatarView = FindAvatarView(isLocal: !opponent);
            if (avatarView == null)
            {
                Log.Nav("PlayerPortrait", $"AvatarView not found for {(opponent ? "opponent" : "local")}");
                _announcer.Announce(Strings.PortraitNotFound, AnnouncementPriority.Normal);
                return;
            }

            if (!_avatarReflectionInitialized)
            {
                _announcer.Announce(Strings.PortraitNotAvailable, AnnouncementPriority.Normal);
                return;
            }

            var portraitButton = _portraitButtonField.GetValue(avatarView) as MonoBehaviour;
            if (portraitButton == null)
            {
                Log.Nav("PlayerPortrait", $"PortraitButton is null on {(opponent ? "opponent" : "local")} AvatarView");
                _announcer.Announce(Strings.PortraitButtonNotFound, AnnouncementPriority.Normal);
                return;
            }

            Log.Nav("PlayerPortrait", $"Clicking PortraitButton for {(opponent ? "opponent" : "local")} avatar");
            UIActivator.SimulatePointerClick(portraitButton.gameObject);
        }
    }
}
