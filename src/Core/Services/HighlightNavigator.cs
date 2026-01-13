using UnityEngine;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Handles Tab navigation through highlighted/playable cards during normal gameplay.
    /// The game uses HotHighlight to show which cards can be played or activated.
    /// Tab cycles through these cards, replacing the default button-cycling behavior.
    ///
    /// This does NOT handle targeting mode - TargetNavigator handles that separately.
    /// </summary>
    public class HighlightNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ZoneNavigator _zoneNavigator;

        private List<HighlightedCard> _highlightedCards = new List<HighlightedCard>();
        private int _currentIndex = -1;
        private bool _isActive;

        public bool IsActive => _isActive;
        public int HighlightCount => _highlightedCards.Count;
        public HighlightedCard CurrentCard =>
            (_currentIndex >= 0 && _currentIndex < _highlightedCards.Count)
                ? _highlightedCards[_currentIndex]
                : null;

        public HighlightNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = zoneNavigator;
        }

        /// <summary>
        /// Activates highlight navigation.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
            MelonLogger.Msg("[HighlightNavigator] Activated");
        }

        /// <summary>
        /// Deactivates highlight navigation.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _highlightedCards.Clear();
            _currentIndex = -1;
            MelonLogger.Msg("[HighlightNavigator] Deactivated");
        }

        /// <summary>
        /// Handles Tab input to cycle through highlighted cards and Enter to play.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                // Refresh highlighted cards on each Tab press
                DiscoverHighlightedCards();

                if (_highlightedCards.Count == 0)
                {
                    _announcer.Announce("No playable cards", AnnouncementPriority.Normal);
                    return true;
                }

                if (shift)
                    PreviousCard();
                else
                    NextCard();

                return true;
            }

            // Handle Enter to play the currently highlighted card
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_currentIndex >= 0 && _currentIndex < _highlightedCards.Count)
                {
                    ActivateCurrentCard();
                    return true;
                }
                // No highlighted card selected, let other handlers deal with Enter
            }

            return false;
        }

        /// <summary>
        /// Activates (plays) the currently highlighted card.
        /// </summary>
        private void ActivateCurrentCard()
        {
            if (_currentIndex < 0 || _currentIndex >= _highlightedCards.Count)
            {
                _announcer.Announce("No card selected", AnnouncementPriority.High);
                return;
            }

            var card = _highlightedCards[_currentIndex];
            MelonLogger.Msg($"[HighlightNavigator] Activating: {card.Name} ({card.GameObject.name})");

            // Use UIActivator to play the card
            if (card.Zone == "Hand")
            {
                // For hand cards, use the two-click approach
                UIActivator.PlayCardViaTwoClick(card.GameObject, (success, message) =>
                {
                    if (success)
                    {
                        MelonLogger.Msg($"[HighlightNavigator] Card play succeeded");
                    }
                    else
                    {
                        _announcer.Announce($"Could not play {card.Name}", AnnouncementPriority.High);
                        MelonLogger.Msg($"[HighlightNavigator] Card play failed: {message}");
                    }
                }, null); // Don't pass TargetNavigator - targeting is handled separately
            }
            else
            {
                // For battlefield cards (activated abilities), use click
                var result = UIActivator.SimulatePointerClick(card.GameObject);

                if (result.Success)
                {
                    _announcer.Announce($"Activated {card.Name}", AnnouncementPriority.Normal);
                }
                else
                {
                    _announcer.Announce($"Cannot activate {card.Name}", AnnouncementPriority.High);
                    MelonLogger.Msg($"[HighlightNavigator] Activation failed: {result.Message}");
                }
            }

            // Reset selection after activation
            _currentIndex = -1;
            _highlightedCards.Clear();
        }

        /// <summary>
        /// Moves to the next highlighted card.
        /// </summary>
        public void NextCard()
        {
            if (_highlightedCards.Count == 0)
            {
                _announcer.Announce("No playable cards", AnnouncementPriority.Normal);
                return;
            }

            _currentIndex = (_currentIndex + 1) % _highlightedCards.Count;
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Moves to the previous highlighted card.
        /// </summary>
        public void PreviousCard()
        {
            if (_highlightedCards.Count == 0)
            {
                _announcer.Announce("No playable cards", AnnouncementPriority.Normal);
                return;
            }

            _currentIndex--;
            if (_currentIndex < 0)
                _currentIndex = _highlightedCards.Count - 1;

            AnnounceCurrentCard();
        }

        /// <summary>
        /// Announces the current highlighted card.
        /// </summary>
        private void AnnounceCurrentCard()
        {
            if (_currentIndex < 0 || _currentIndex >= _highlightedCards.Count) return;

            var card = _highlightedCards[_currentIndex];
            int position = _currentIndex + 1;
            int total = _highlightedCards.Count;

            string zoneInfo = GetZoneDisplayName(card.Zone);
            string announcement = $"{card.Name}, {zoneInfo}, {position} of {total} playable";

            _announcer.Announce(announcement, AnnouncementPriority.Normal);

            // Prepare card for detailed navigation with arrow keys
            var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
            if (cardNavigator != null)
            {
                var zoneType = StringToZoneType(card.Zone);
                cardNavigator.PrepareForCard(card.GameObject, zoneType);
            }
        }

        /// <summary>
        /// Converts zone string to ZoneType enum.
        /// </summary>
        private ZoneType StringToZoneType(string zone)
        {
            return zone switch
            {
                "Hand" => ZoneType.Hand,
                "Battlefield" => ZoneType.Battlefield,
                "Stack" => ZoneType.Stack,
                _ => ZoneType.Hand
            };
        }

        /// <summary>
        /// Discovers all cards with active HotHighlight indicators.
        /// These are cards that can be played or activated.
        /// </summary>
        private void DiscoverHighlightedCards()
        {
            _highlightedCards.Clear();
            _zoneNavigator.DiscoverZones();

            // Zones to scan for playable cards
            string[] scanZones = new[]
            {
                "LocalHand",           // Cards in hand
                "BattlefieldCardHolder" // Activated abilities on battlefield
            };

            var addedIds = new HashSet<int>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check if in a relevant zone
                string zoneName = GetParentZone(go);
                if (string.IsNullOrEmpty(zoneName)) continue;

                bool inScanZone = false;
                foreach (var zone in scanZones)
                {
                    if (zoneName.Contains(zone))
                    {
                        inScanZone = true;
                        break;
                    }
                }
                if (!inScanZone) continue;

                // Check if it's a card
                if (!CardDetector.IsCard(go)) continue;

                // Check for HotHighlight (playable indicator)
                if (!HasHotHighlight(go)) continue;

                // Avoid duplicates
                int instanceId = go.GetInstanceID();
                if (addedIds.Contains(instanceId)) continue;

                var cardInfo = CreateHighlightedCard(go, zoneName);
                if (cardInfo != null)
                {
                    _highlightedCards.Add(cardInfo);
                    addedIds.Add(instanceId);
                }
            }

            // Sort: hand cards first (by position left to right), then battlefield
            _highlightedCards = _highlightedCards
                .OrderBy(c => c.Zone == "Hand" ? 0 : 1)
                .ThenBy(c => c.GameObject.transform.position.x)
                .ToList();

            MelonLogger.Msg($"[HighlightNavigator] Found {_highlightedCards.Count} playable cards");

            // If current index is out of range, reset
            if (_currentIndex >= _highlightedCards.Count)
            {
                _currentIndex = _highlightedCards.Count > 0 ? 0 : -1;
            }
        }

        /// <summary>
        /// Checks if a card has an active HotHighlight child, indicating it can be played/activated.
        /// </summary>
        private bool HasHotHighlight(GameObject card)
        {
            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.gameObject == card) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                // HotHighlight variants: HotHighlightBattlefield, HotHighlightHand, etc.
                if (child.name.Contains("HotHighlight"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the parent zone name for a card.
        /// </summary>
        private string GetParentZone(GameObject card)
        {
            Transform current = card.transform;
            while (current != null)
            {
                string name = current.name;
                if (name.Contains("LocalHand") || name.Contains("Hand"))
                    return "LocalHand";
                if (name.Contains("BattlefieldCardHolder") || name.Contains("Battlefield"))
                    return "BattlefieldCardHolder";
                if (name.Contains("StackCardHolder"))
                    return "StackCardHolder";
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Creates a HighlightedCard info object from a card GameObject.
        /// </summary>
        private HighlightedCard CreateHighlightedCard(GameObject cardObj, string zoneName)
        {
            string cardName = CardDetector.GetCardName(cardObj);
            if (cardName == "Unknown card") return null;

            string zone = "Unknown";
            if (zoneName.Contains("Hand"))
                zone = "Hand";
            else if (zoneName.Contains("Battlefield"))
                zone = "Battlefield";
            else if (zoneName.Contains("Stack"))
                zone = "Stack";

            return new HighlightedCard
            {
                GameObject = cardObj,
                Name = cardName,
                Zone = zone,
                InstanceId = (uint)cardObj.GetInstanceID()
            };
        }

        /// <summary>
        /// Gets a user-friendly zone display name.
        /// </summary>
        private string GetZoneDisplayName(string zone)
        {
            switch (zone)
            {
                case "Hand": return "in hand";
                case "Battlefield": return "on battlefield";
                case "Stack": return "on stack";
                default: return zone;
            }
        }
    }

    /// <summary>
    /// Information about a highlighted/playable card.
    /// </summary>
    public class HighlightedCard
    {
        public GameObject GameObject { get; set; }
        public string Name { get; set; }
        public string Zone { get; set; }
        public uint InstanceId { get; set; }
    }
}
