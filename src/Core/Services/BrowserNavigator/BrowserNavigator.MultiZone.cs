using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    public partial class BrowserNavigator
    {
        // Multi-zone browser state (SelectCardsMultiZone)
        private bool _isMultiZone;
        private List<GameObject> _zoneButtons = new List<GameObject>();
        private int _currentZoneButtonIndex = -1;
        private bool _onZoneSelector; // true when focus is on the zone selector element

        // Maps game zone names to mod ZoneType, with local/opponent variants
        private static readonly Dictionary<string, (ZoneType local, ZoneType opponent)> MultiZoneMap =
            new Dictionary<string, (ZoneType, ZoneType)>
        {
            { "Graveyard", (ZoneType.Graveyard, ZoneType.OpponentGraveyard) },
            { "Exile", (ZoneType.Exile, ZoneType.OpponentExile) },
            { "Library", (ZoneType.Library, ZoneType.OpponentLibrary) },
            { "Hand", (ZoneType.Hand, ZoneType.OpponentHand) },
            { "Command", (ZoneType.Command, ZoneType.OpponentCommand) }
        };

        /// <summary>
        /// Enters zone selector mode and deactivates CardInfoNavigator
        /// so it doesn't intercept arrow keys meant for zone cycling.
        /// </summary>
        private void EnterZoneSelector()
        {
            _onZoneSelector = true;
            AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();
        }

        /// <summary>
        /// Gets the localized zone name for a card in a SelectCardsMultiZone browser.
        /// Returns ", Your graveyard" or ", Opponent's graveyard" etc.
        /// </summary>
        private string GetMultiZoneCardZoneName(GameObject card)
        {
            string modelZone = CardStateProvider.GetCardZoneTypeName(card);
            if (string.IsNullOrEmpty(modelZone)) return "";

            if (MultiZoneMap.TryGetValue(modelZone, out var zonePair))
            {
                bool isOpponent = CardStateProvider.IsOpponentCard(card);
                var zoneType = isOpponent ? zonePair.opponent : zonePair.local;
                return $", {Strings.GetZoneName(zoneType)}";
            }

            return "";
        }

        /// <summary>
        /// Announces the multi-zone selector element with current zone name and card count.
        /// </summary>
        private void AnnounceMultiZoneSelector()
        {
            string zoneName = GetCurrentZoneButtonLabel();
            string zonePos = Strings.PositionOf(_currentZoneButtonIndex + 1, _zoneButtons.Count, force: true);
            string hint = zonePos != "" ? $", {zonePos}" : "";
            string cardInfo = _browserCards.Count > 0 ? $", {_browserCards.Count} cards" : "";
            string announcement = $"{Strings.ZoneChange}: {zoneName}{hint}{cardInfo}";
            _announcer.Announce(announcement, AnnouncementPriority.High);
        }

        /// <summary>
        /// Cycles to the next/previous zone in a multi-zone browser.
        /// Clicks the zone button and rediscovers cards after a delay.
        /// </summary>
        private void CycleMultiZone(bool next)
        {
            if (_zoneButtons.Count == 0) return;

            int newIndex = _currentZoneButtonIndex + (next ? 1 : -1);
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }
            if (newIndex >= _zoneButtons.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _currentZoneButtonIndex = newIndex;
            ActivateMultiZoneButton();
        }

        /// <summary>
        /// Clicks the current zone button and schedules card rediscovery.
        /// </summary>
        private void ActivateMultiZoneButton()
        {
            var button = _zoneButtons[_currentZoneButtonIndex];
            MelonLogger.Msg($"[BrowserNavigator] Activating zone button: {button.name}");
            UIActivator.SimulatePointerClick(button);

            // Rediscover cards after game updates the holder
            MelonCoroutines.Start(RediscoverMultiZoneCards());
        }

        /// <summary>
        /// Waits for the game to update the card holder after a zone change,
        /// then rediscovers cards and announces the new zone.
        /// </summary>
        private IEnumerator RediscoverMultiZoneCards()
        {
            yield return new WaitForSeconds(0.3f);

            // Rediscover cards in holders
            _browserCards.Clear();
            _currentCardIndex = -1;
            DiscoverCardsInHolders();

            MelonLogger.Msg($"[BrowserNavigator] Multi-zone rediscovery: {_browserCards.Count} cards");
            AnnounceMultiZoneSelector();
        }

        /// <summary>
        /// Gets the display label for the currently selected zone button.
        /// </summary>
        private string GetCurrentZoneButtonLabel()
        {
            if (_currentZoneButtonIndex < 0 || _currentZoneButtonIndex >= _zoneButtons.Count)
                return "?";

            var button = _zoneButtons[_currentZoneButtonIndex];
            string label = UITextExtractor.GetButtonText(button, button.name);

            // If the button only has a generic name like "ZoneButton0", try to extract zone info
            if (label.StartsWith("ZoneButton"))
                label = $"Zone {_currentZoneButtonIndex + 1}";

            return label;
        }

        /// <summary>
        /// Finds which zone button is currently active/selected by checking visual state.
        /// Falls back to 0 if no active button can be determined.
        /// </summary>
        private int FindActiveZoneButtonIndex()
        {
            // Try to detect which zone button is visually active (selected state)
            for (int i = 0; i < _zoneButtons.Count; i++)
            {
                var button = _zoneButtons[i];
                // Check if button has a Toggle component that's on
                var toggle = button.GetComponent<Toggle>();
                if (toggle != null && toggle.isOn)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Zone button {i} ({button.name}) is active (Toggle.isOn)");
                    return i;
                }
            }

            // Fallback: first button
            return 0;
        }
    }
}
