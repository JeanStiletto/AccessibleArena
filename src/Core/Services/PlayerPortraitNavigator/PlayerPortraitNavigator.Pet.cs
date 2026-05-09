using UnityEngine;
using UnityEngine.EventSystems;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System.Collections.Generic;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Local-player pet interaction menu (Shift+Enter from the player-info zone).
    /// Reads the available interactions from <see cref="PetService"/>, announces the
    /// equipped pet's localized name on open, then lets the user cycle parts with Up/Down
    /// and trigger a part with Enter — the same handlers a mouse click would invoke.
    /// </summary>
    public partial class PlayerPortraitNavigator
    {
        private List<PetService.PetInteraction> _petInteractions = new List<PetService.PetInteraction>();
        private int _currentPetIndex;

        private bool HandlePetNavigation()
        {
            // If focus drifts off the player zone (e.g. Tab cycled to a card), exit cleanly.
            var currentFocus = EventSystem.current?.currentSelectedGameObject;
            if (currentFocus != null && currentFocus && !IsPlayerZoneElement(currentFocus))
            {
                Log.Nav("PlayerPortrait", $"Focus moved to '{currentFocus.name}', exiting pet menu");
                ExitPlayerInfoZone();
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ClosePetMenu();
                _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_petInteractions.Count == 0) return true;
                _currentPetIndex = (_currentPetIndex + 1) % _petInteractions.Count;
                AnnounceCurrentPetAction();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_petInteractions.Count == 0) return true;
                _currentPetIndex--;
                if (_currentPetIndex < 0) _currentPetIndex = _petInteractions.Count - 1;
                AnnounceCurrentPetAction();
                return true;
            }

            if (InputManager.GetEnterAndConsume())
            {
                TriggerCurrentPetAction();
                return true;
            }

            // Modal: swallow everything else while the menu is up.
            return true;
        }

        private void OpenPetMenu()
        {
            var actions = PetService.GetAvailableInteractions();
            if (actions == null || actions.Count == 0)
            {
                // Distinguish "no pet" from "pet exists but every action's UnityEvent is empty"
                // — both end up here after we started filtering inert actions out of the menu.
                _announcer.Announce(
                    PetService.HasLocalPet() ? Strings.PetHasNoInteractions : Strings.PetNotEquipped,
                    AnnouncementPriority.Normal);
                return;
            }

            _petInteractions = actions;
            _currentPetIndex = 0;
            _navigationState = NavigationState.PetNavigation;

            // Lead with the pet's localized name so the user knows which pet they're petting.
            // Fall back to a generic header when the cosmetics provider hasn't surfaced a name.
            var petName = PetService.GetLocalPetName();
            _announcer.Announce(
                !string.IsNullOrEmpty(petName) ? Strings.PetMenuOpened(petName) : Strings.PetMenu,
                AnnouncementPriority.High);
            AnnounceCurrentPetAction();
        }

        private void ClosePetMenu()
        {
            _navigationState = NavigationState.PlayerNavigation;
            _petInteractions.Clear();
        }

        private void AnnounceCurrentPetAction()
        {
            if (_currentPetIndex < 0 || _currentPetIndex >= _petInteractions.Count) return;
            _announcer.Announce(_petInteractions[_currentPetIndex].Label, AnnouncementPriority.High);
        }

        private void TriggerCurrentPetAction()
        {
            if (_currentPetIndex < 0 || _currentPetIndex >= _petInteractions.Count)
            {
                _announcer.Announce(Strings.PetNotAvailable, AnnouncementPriority.Normal);
                return;
            }

            var picked = _petInteractions[_currentPetIndex];
            bool ok = PetService.TriggerInteraction(picked.Kind);
            _announcer.Announce(
                ok ? Strings.PetActionTriggered(picked.Label) : Strings.PetNotAvailable,
                AnnouncementPriority.Normal);

            // Stay in the menu so the user can chain interactions; Backspace exits.
        }
    }
}
