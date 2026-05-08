using UnityEngine;
using UnityEngine.EventSystems;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Local-player emote wheel: opens the visual panel for sighted observers, reads the
    /// equipped-emote list straight from <c>EmoteOptionsController._equippedEmoteOptions</c>
    /// (no GameObject scraping), and sends the chosen emote through
    /// <c>UIMessageHandler.TrySendEmote</c>. Also hosts the opponent mute toggle invoked from
    /// the player-zone Enter handler.
    /// </summary>
    public partial class PlayerPortraitNavigator
    {
        // Equipped emotes for the local player, in wheel order. Refreshed each time we open.
        private List<EmoteService.EquippedEmote> _equippedEmotes = new List<EmoteService.EquippedEmote>();
        private int _currentEmoteIndex = 0;

        // Avatar reflection cache (for opening the visual wheel via PortraitButton).
        private sealed class AvatarHandles
        {
            public PropertyInfo IsLocalPlayer;   // DuelScene_AvatarView.IsLocalPlayer
            public FieldInfo PortraitButton;     // DuelScene_AvatarView.PortraitButton (private)
        }

        private static readonly ReflectionCache<AvatarHandles> _avatarCache = new ReflectionCache<AvatarHandles>(
            builder: t => new AvatarHandles
            {
                IsLocalPlayer = t.GetProperty("IsLocalPlayer", PublicInstance),
                PortraitButton = t.GetField("PortraitButton", PrivateInstance),
            },
            validator: h => h.IsLocalPlayer != null && h.PortraitButton != null,
            logTag: "PlayerPortrait",
            logSubject: "Avatar");

        /// <summary>
        /// Toggles per-duel mute on the opponent's emote stream. Mirrors the in-game
        /// mute icon inside the opponent's CommunicationOptionsView; resets next match.
        /// </summary>
        private void ToggleOpponentMute()
        {
            var newState = EmoteService.ToggleOpponentMute();
            if (newState == null)
            {
                _announcer.Announce(Strings.OpponentMuteUnavailable, AnnouncementPriority.Normal);
                return;
            }

            _announcer.Announce(
                newState.Value ? Strings.OpponentMuted : Strings.OpponentUnmuted,
                AnnouncementPriority.High);
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
                Log.Nav("PlayerPortrait", $"Focus moved to '{currentFocus.name}', exiting emote navigation");
                ExitPlayerInfoZone();
                return false;
            }

            // Backspace cancels emote menu and dismisses the visual wheel
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseEmoteWheel();
                _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                return true;
            }

            // Up/Down navigates emotes
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_equippedEmotes.Count == 0) return true;
                _currentEmoteIndex = (_currentEmoteIndex + 1) % _equippedEmotes.Count;
                AnnounceCurrentEmote();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_equippedEmotes.Count == 0) return true;
                _currentEmoteIndex--;
                if (_currentEmoteIndex < 0) _currentEmoteIndex = _equippedEmotes.Count - 1;
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
        /// Opens the emote wheel: triggers the visual panel for sighted observers and
        /// reads the equipped-emote list from the game's controller.
        /// </summary>
        private void OpenEmoteWheel()
        {
            // Click the PortraitButton to open the visual wheel. This is purely cosmetic for
            // sighted helpers — our list comes from the controller's own state, not the panel.
            TriggerEmoteMenu(opponent: false);

            var emotes = EmoteService.GetEquippedEmotes();
            if (emotes == null || emotes.Count == 0)
            {
                _announcer.Announce(Strings.EmotesNotAvailable, AnnouncementPriority.Normal);
                return;
            }

            _equippedEmotes = emotes;
            _currentEmoteIndex = 0;
            _navigationState = NavigationState.EmoteNavigation;
            _announcer.Announce(Strings.Emotes, AnnouncementPriority.High);
            AnnounceCurrentEmote();
        }

        /// <summary>
        /// Closes the emote wheel: dismisses the visual panel and returns to player navigation.
        /// </summary>
        private void CloseEmoteWheel()
        {
            EmoteService.CloseLocalEmoteWheel();
            _navigationState = NavigationState.PlayerNavigation;
            _equippedEmotes.Clear();
        }

        /// <summary>
        /// Announces the currently selected emote.
        /// </summary>
        private void AnnounceCurrentEmote()
        {
            if (_currentEmoteIndex < 0 || _currentEmoteIndex >= _equippedEmotes.Count) return;
            _announcer.Announce(_equippedEmotes[_currentEmoteIndex].PreviewText, AnnouncementPriority.High);
        }

        /// <summary>
        /// Sends the currently selected emote and closes the wheel.
        /// </summary>
        private void SelectCurrentEmote()
        {
            if (_currentEmoteIndex < 0 || _currentEmoteIndex >= _equippedEmotes.Count)
            {
                _announcer.Announce(Strings.EmotesNotAvailable, AnnouncementPriority.Normal);
                return;
            }

            var picked = _equippedEmotes[_currentEmoteIndex];
            bool sent = EmoteService.SendEmote(picked.Id);
            _announcer.Announce(
                sent ? Strings.EmoteSent(picked.PreviewText) : Strings.CouldNotSend(picked.PreviewText),
                AnnouncementPriority.Normal);

            // Dismiss the visual wheel and return to player navigation regardless of send outcome.
            CloseEmoteWheel();
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

                if (!_avatarCache.EnsureInitialized(mb.GetType())) return null;

                bool mbIsLocal = (bool)_avatarCache.Handles.IsLocalPlayer.GetValue(mb);
                if (mbIsLocal == isLocal) return mb;
            }
            return null;
        }

        /// <summary>
        /// Clicks the local player's PortraitButton to open the visual emote wheel.
        /// Cosmetic only — our list and send path don't depend on the panel being open.
        /// </summary>
        public void TriggerEmoteMenu(bool opponent = false)
        {
            var avatarView = FindAvatarView(isLocal: !opponent);
            if (avatarView == null)
            {
                Log.Nav("PlayerPortrait", $"AvatarView not found for {(opponent ? "opponent" : "local")}");
                return;
            }

            if (!_avatarCache.IsInitialized) return;

            var portraitButton = _avatarCache.Handles.PortraitButton.GetValue(avatarView) as MonoBehaviour;
            if (portraitButton == null)
            {
                Log.Nav("PlayerPortrait", $"PortraitButton is null on {(opponent ? "opponent" : "local")} AvatarView");
                return;
            }

            Log.Nav("PlayerPortrait", $"Clicking PortraitButton for {(opponent ? "opponent" : "local")} avatar");
            UIActivator.SimulatePointerClick(portraitButton.gameObject);
        }
    }
}
