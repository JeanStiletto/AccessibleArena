using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
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
                    // Check if there's a primary button to pass priority
                    string primaryButtonText = GetPrimaryButtonText();
                    if (!string.IsNullOrEmpty(primaryButtonText))
                    {
                        // Use High priority to bypass duplicate check - user explicitly pressed Tab
                        _announcer.Announce(primaryButtonText, AnnouncementPriority.High);
                    }
                    else
                    {
                        _announcer.Announce(Strings.NoPlayableCards, AnnouncementPriority.High);
                    }
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
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.High);
                return;
            }

            var card = _highlightedCards[_currentIndex];

            // Check if there's stack resolution pending - don't play new cards if so
            // This prevents accidentally playing cards when the game is waiting for
            // target selection or stack resolution
            if (card.Zone == "Hand" && IsStackResolutionPending())
            {
                _announcer.Announce(Strings.ResolveStackFirst, AnnouncementPriority.High);
                MelonLogger.Msg($"[HighlightNavigator] Blocked card play - stack resolution pending");
                return;
            }

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
                        _announcer.Announce(Strings.CouldNotPlay(card.Name), AnnouncementPriority.High);
                        MelonLogger.Msg($"[HighlightNavigator] Card play failed: {message}");
                    }
                }, null); // Don't pass TargetNavigator - targeting is handled separately
            }
            else
            {
                // For battlefield cards (activated abilities), use click
                var result = UIActivator.SimulatePointerClick(card.GameObject);

                if (!result.Success)
                {
                    _announcer.Announce(Strings.CannotActivate(card.Name), AnnouncementPriority.High);
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
                _announcer.Announce(Strings.NoPlayableCards, AnnouncementPriority.Normal);
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
                _announcer.Announce(Strings.NoPlayableCards, AnnouncementPriority.Normal);
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
            string announcement = $"{card.Name}, {zoneInfo}, {position} of {total}";

            // Use High priority to bypass duplicate check - user explicitly pressed Tab
            _announcer.Announce(announcement, AnnouncementPriority.High);

            // Set EventSystem focus to the card - this ensures other navigators
            // (like PlayerPortrait) detect the focus change and exit their modes
            var eventSystem = EventSystem.current;
            if (eventSystem != null && card.GameObject != null)
            {
                eventSystem.SetSelectedGameObject(card.GameObject);
            }

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

        /// <summary>
        /// Checks if there's pending stack resolution that should take priority over playing new cards.
        /// This prevents the mod from playing cards when the game is waiting for:
        /// - Target selection for a spell on the stack
        /// - Resolution confirmation (Resolve button showing)
        /// </summary>
        private bool IsStackResolutionPending()
        {
            // Check if stack has cards waiting
            if (_zoneNavigator.StackCardCount > 0)
            {
                // Stack has cards - check if Resolve button is showing
                if (HasResolveButton())
                {
                    MelonLogger.Msg("[HighlightNavigator] Stack resolution pending - Resolve button found");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a "Resolve" button is currently visible.
        /// This indicates the game is waiting for the player to resolve stack effects.
        /// </summary>
        private bool HasResolveButton()
        {
            // Check TMP_Text components (used by StyledButton)
            foreach (var tmpText in GameObject.FindObjectsOfType<TMP_Text>())
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                string text = tmpText.text?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                // Check for "Resolve" button text
                if (text.Equals("Resolve", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Verify it's on a button
                    var parent = tmpText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name.ToLower();
                        if (parentName.Contains("button") || parentName.Contains("prompt"))
                        {
                            return true;
                        }
                        parent = parent.parent;
                    }
                }
            }

            // Also check legacy Text components
            foreach (var uiText in GameObject.FindObjectsOfType<Text>())
            {
                if (uiText == null || !uiText.gameObject.activeInHierarchy)
                    continue;

                string text = uiText.text?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (text.Equals("Resolve", System.StringComparison.OrdinalIgnoreCase))
                {
                    var parent = uiText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name.ToLower();
                        if (parentName.Contains("button") || parentName.Contains("prompt"))
                        {
                            return true;
                        }
                        parent = parent.parent;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the text of the primary prompt button if one exists.
        /// This indicates the player can pass priority by pressing Space.
        /// </summary>
        private string GetPrimaryButtonText()
        {
            // Look for the primary prompt button
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check for primary prompt button by name pattern
                if (!go.name.Contains("PromptButton_Primary")) continue;

                // Get the TMP_Text component to extract button text
                var tmpText = go.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    string text = tmpText.text?.Trim();
                    // Filter out non-meaningful text like "Ctrl" (full control indicator)
                    if (!string.IsNullOrEmpty(text) && text != "Ctrl")
                    {
                        return text;
                    }
                }

                // Also check legacy Text component
                var uiText = go.GetComponentInChildren<Text>();
                if (uiText != null)
                {
                    string text = uiText.text?.Trim();
                    if (!string.IsNullOrEmpty(text) && text != "Ctrl")
                    {
                        return text;
                    }
                }
            }

            return null;
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
