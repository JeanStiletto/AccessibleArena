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
    /// Unified navigator for all HotHighlight-based navigation.
    /// Replaces both TargetNavigator and HighlightNavigator.
    ///
    /// Key insight: The game correctly manages HotHighlight to show only what's
    /// relevant in the current context. We don't need separate "modes" - we just
    /// scan for highlights and let the zone determine announcement/activation.
    ///
    /// - Hand cards with HotHighlight = playable cards (two-click to play)
    /// - Battlefield/Stack cards with HotHighlight = valid targets (single-click)
    /// - Player portraits with HotHighlight = player targets (single-click)
    /// </summary>
    public class HotHighlightNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ZoneNavigator _zoneNavigator;

        private List<HighlightedItem> _items = new List<HighlightedItem>();
        private int _currentIndex = -1;
        private bool _isActive;

        public bool IsActive => _isActive;
        public int ItemCount => _items.Count;
        public HighlightedItem CurrentItem =>
            (_currentIndex >= 0 && _currentIndex < _items.Count)
                ? _items[_currentIndex]
                : null;

        /// <summary>
        /// Returns true if any battlefield/stack targets are highlighted.
        /// Used by other systems that need to know if targeting is active.
        /// </summary>
        public bool HasTargetsHighlighted => _items.Any(i =>
            i.Zone == "Battlefield" || i.Zone == "Stack" || i.IsPlayer);

        /// <summary>
        /// Returns true if hand cards are highlighted (playable).
        /// </summary>
        public bool HasPlayableHighlighted => _items.Any(i => i.Zone == "Hand");

        public HotHighlightNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = zoneNavigator;
        }

        public void Activate()
        {
            _isActive = true;
            MelonLogger.Msg("[HotHighlightNavigator] Activated");
        }

        public void Deactivate()
        {
            _isActive = false;
            _items.Clear();
            _currentIndex = -1;
            MelonLogger.Msg("[HotHighlightNavigator] Deactivated");
        }

        /// <summary>
        /// Handles Tab/Enter/Backspace input for highlight navigation.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Tab - cycle through highlighted items
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                // Refresh highlights on each Tab press
                DiscoverAllHighlights();

                if (_items.Count == 0)
                {
                    // Check if there's a primary button to show game state (Pass, Resolve, Next, etc.)
                    string primaryButtonText = GetPrimaryButtonText();
                    if (!string.IsNullOrEmpty(primaryButtonText))
                    {
                        _announcer.Announce(primaryButtonText, AnnouncementPriority.High);
                    }
                    else
                    {
                        _announcer.Announce(Strings.NoPlayableCards, AnnouncementPriority.High);
                    }
                    return true;
                }

                // Cycle through items
                if (shift)
                {
                    _currentIndex--;
                    if (_currentIndex < 0)
                        _currentIndex = _items.Count - 1;
                }
                else
                {
                    _currentIndex = (_currentIndex + 1) % _items.Count;
                }

                AnnounceCurrentItem();
                return true;
            }

            // Enter - activate current item
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_currentIndex >= 0 && _currentIndex < _items.Count)
                {
                    ActivateCurrentItem();
                    return true;
                }
                return false; // Let other handlers deal with Enter
            }

            // Backspace - cancel/dismiss (game handles what this means in context)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (HasTargetsHighlighted)
                {
                    _announcer.Announce(Strings.TargetingCancelled, AnnouncementPriority.Normal);
                    MelonLogger.Msg("[HotHighlightNavigator] Cancel requested");
                    // Clear our state - game will update highlights
                    _items.Clear();
                    _currentIndex = -1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Discovers ALL items with HotHighlight across all zones.
        /// No zone filtering - we trust the game to highlight only what's relevant.
        /// </summary>
        private void DiscoverAllHighlights()
        {
            _items.Clear();
            var addedIds = new HashSet<int>();

            MelonLogger.Msg("[HotHighlightNavigator] Discovering highlights...");

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check for HotHighlight
                string highlightType = CardDetector.GetHotHighlightType(go);
                if (highlightType == null) continue;

                // Only process actual cards (skip parent containers that also have the highlight)
                if (!CardDetector.IsCard(go)) continue;

                // Avoid duplicates
                int id = go.GetInstanceID();
                if (addedIds.Contains(id)) continue;

                var item = CreateHighlightedItem(go, highlightType);
                if (item != null)
                {
                    _items.Add(item);
                    addedIds.Add(id);
                }
            }

            // Also check for player targets
            DiscoverPlayerTargets(addedIds);

            // Sort: Hand cards first, then your permanents, then opponent's, then players
            _items = _items
                .OrderBy(i => i.Zone == "Hand" ? 0 : 1)
                .ThenBy(i => i.IsPlayer ? 1 : 0)
                .ThenBy(i => i.IsOpponent ? 1 : 0)
                .ThenBy(i => i.GameObject?.transform.position.x ?? 0)
                .ToList();

            MelonLogger.Msg($"[HotHighlightNavigator] Found {_items.Count} highlighted items");

            // Reset index if out of range
            if (_currentIndex >= _items.Count)
                _currentIndex = _items.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// Creates a HighlightedItem from a card GameObject.
        /// </summary>
        private HighlightedItem CreateHighlightedItem(GameObject go, string highlightType)
        {
            string zone = DetectZone(go);
            string cardName = CardDetector.GetCardName(go);

            if (cardName == "Unknown card") return null;

            var item = new HighlightedItem
            {
                GameObject = go,
                Name = cardName,
                Zone = zone,
                HighlightType = highlightType,
                IsOpponent = CardDetector.IsOpponentCard(go),
                IsPlayer = false
            };

            // Get additional info for battlefield cards
            if (zone == "Battlefield" || zone == "Stack")
            {
                var cardInfo = CardDetector.ExtractCardInfo(go);
                item.PowerToughness = cardInfo.PowerToughness;
                item.CardType = DetermineCardType(cardInfo.TypeLine);
            }

            return item;
        }

        /// <summary>
        /// Discovers player portraits as targets if they have HotHighlight.
        /// </summary>
        private void DiscoverPlayerTargets(HashSet<int> addedIds)
        {
            // Check local player
            var (localHighlight, localClickable) = FindPlayerWithHighlight(isOpponent: false);
            if (localHighlight != null && localClickable != null)
            {
                int id = localClickable.GetInstanceID();
                if (!addedIds.Contains(id))
                {
                    _items.Add(new HighlightedItem
                    {
                        GameObject = localClickable,
                        Name = Strings.You,
                        Zone = "Player",
                        HighlightType = "PlayerHighlight",
                        IsOpponent = false,
                        IsPlayer = true,
                        CardType = "Player"
                    });
                    addedIds.Add(id);
                    MelonLogger.Msg("[HotHighlightNavigator] Added local player as target");
                }
            }

            // Check opponent
            var (opponentHighlight, opponentClickable) = FindPlayerWithHighlight(isOpponent: true);
            if (opponentHighlight != null && opponentClickable != null)
            {
                int id = opponentClickable.GetInstanceID();
                if (!addedIds.Contains(id))
                {
                    _items.Add(new HighlightedItem
                    {
                        GameObject = opponentClickable,
                        Name = Strings.Opponent,
                        Zone = "Player",
                        HighlightType = "PlayerHighlight",
                        IsOpponent = true,
                        IsPlayer = true,
                        CardType = "Player"
                    });
                    addedIds.Add(id);
                    MelonLogger.Msg("[HotHighlightNavigator] Added opponent as target");
                }
            }
        }

        /// <summary>
        /// Finds player portrait with HotHighlight.
        /// Returns (objectWithHighlight, clickableObject).
        /// </summary>
        private (GameObject, GameObject) FindPlayerWithHighlight(bool isOpponent)
        {
            string prefix = isOpponent ? "Opponent" : "LocalPlayer";
            GameObject foundWithHighlight = null;
            GameObject clickable = null;

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                string goName = go.name;

                // Check MatchTimer objects
                if (goName.Contains(prefix) && goName.Contains("MatchTimer"))
                {
                    if (CardDetector.HasHotHighlight(go))
                    {
                        foundWithHighlight = go;

                        // Find clickable child (HoverArea or Icon)
                        var iconTransform = go.transform.Find("Icon");
                        if (iconTransform != null)
                        {
                            var hoverArea = iconTransform.Find("HoverArea");
                            clickable = hoverArea != null ? hoverArea.gameObject : iconTransform.gameObject;
                        }
                        else
                        {
                            clickable = go;
                        }
                        break;
                    }
                }
            }

            return (foundWithHighlight, clickable);
        }

        /// <summary>
        /// Announces the current highlighted item based on its zone.
        /// </summary>
        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            int position = _currentIndex + 1;
            int total = _items.Count;

            string announcement = BuildAnnouncement(item, position, total);

            // Use High priority to bypass duplicate check - user explicitly pressed Tab
            _announcer.Announce(announcement, AnnouncementPriority.High);

            // Set EventSystem focus
            if (item.GameObject != null)
            {
                ZoneNavigator.SetFocusedGameObject(item.GameObject, "HotHighlightNavigator");
            }

            // Update zone context and prepare CardInfo for arrow navigation
            if (!item.IsPlayer)
            {
                var zoneType = StringToZoneType(item.Zone);
                _zoneNavigator.SetCurrentZone(zoneType, "HotHighlightNavigator");

                var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
                if (cardNavigator != null)
                {
                    cardNavigator.PrepareForCard(item.GameObject, zoneType);
                }
            }
        }

        /// <summary>
        /// Builds announcement string based on item zone.
        /// </summary>
        private string BuildAnnouncement(HighlightedItem item, int position, int total)
        {
            // Player target
            if (item.IsPlayer)
            {
                string name = item.IsOpponent ? Strings.Opponent : Strings.You;
                return $"{name}, player, {position} of {total}";
            }

            // Hand card - simple format
            if (item.Zone == "Hand")
            {
                return $"{item.Name}, in hand, {position} of {total}";
            }

            // Stack card
            if (item.Zone == "Stack")
            {
                return $"{item.Name}, on stack, {position} of {total}";
            }

            // Battlefield target - rich format with P/T and owner
            var parts = new List<string> { item.Name };

            if (!string.IsNullOrEmpty(item.PowerToughness))
                parts.Add(item.PowerToughness);

            string ownerType = item.IsOpponent
                ? $"opponent's {item.CardType ?? "permanent"}"
                : (item.CardType ?? "permanent");
            parts.Add(ownerType);

            parts.Add($"{position} of {total}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Activates the current item based on its zone.
        /// </summary>
        private void ActivateCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            MelonLogger.Msg($"[HotHighlightNavigator] Activating: {item.Name} in {item.Zone}");

            if (item.Zone == "Hand")
            {
                // Hand card - use two-click to play
                UIActivator.PlayCardViaTwoClick(item.GameObject, (success, message) =>
                {
                    if (success)
                    {
                        MelonLogger.Msg($"[HotHighlightNavigator] Card play initiated");
                    }
                    else
                    {
                        _announcer.Announce(Strings.CouldNotPlay(item.Name), AnnouncementPriority.High);
                        MelonLogger.Msg($"[HotHighlightNavigator] Card play failed: {message}");
                    }
                }, null);
            }
            else
            {
                // Battlefield/Stack/Player target - single click to select
                var result = UIActivator.SimulatePointerClick(item.GameObject);

                if (result.Success)
                {
                    string action = item.IsPlayer ? "Targeted" : "Selected";
                    _announcer.Announce($"{action} {item.Name}", AnnouncementPriority.Normal);
                    MelonLogger.Msg($"[HotHighlightNavigator] {action} {item.Name}");
                }
                else
                {
                    _announcer.Announce(Strings.CouldNotTarget(item.Name), AnnouncementPriority.High);
                    MelonLogger.Warning($"[HotHighlightNavigator] Click failed: {result.Message}");
                }
            }

            // Clear state after activation - highlights will update
            _items.Clear();
            _currentIndex = -1;
        }

        /// <summary>
        /// Detects zone from parent hierarchy.
        /// </summary>
        private string DetectZone(GameObject obj)
        {
            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.name;

                if (name.Contains("LocalHand") || name.Contains("Hand"))
                    return "Hand";
                if (name.Contains("StackCardHolder") || name.Contains("Stack"))
                    return "Stack";
                if (name.Contains("BattlefieldCardHolder") || name.Contains("Battlefield"))
                    return "Battlefield";
                if (name.Contains("Graveyard"))
                    return "Graveyard";
                if (name.Contains("Exile"))
                    return "Exile";

                current = current.parent;
            }
            return "Unknown";
        }

        /// <summary>
        /// Determines card type from type line.
        /// </summary>
        private string DetermineCardType(string typeLine)
        {
            if (string.IsNullOrEmpty(typeLine)) return "Permanent";

            string lower = typeLine.ToLower();

            if (lower.Contains("creature")) return "Creature";
            if (lower.Contains("planeswalker")) return "Planeswalker";
            if (lower.Contains("artifact")) return "Artifact";
            if (lower.Contains("enchantment")) return "Enchantment";
            if (lower.Contains("land")) return "Land";
            if (lower.Contains("instant")) return "Instant";
            if (lower.Contains("sorcery")) return "Sorcery";

            return "Permanent";
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
                "Graveyard" => ZoneType.Graveyard,
                "Exile" => ZoneType.Exile,
                _ => ZoneType.Battlefield
            };
        }

        /// <summary>
        /// Gets the text of the primary prompt button if one exists.
        /// This indicates game state like "Pass", "Resolve", "Next", "End Turn", etc.
        /// Provides useful context when there are no playable cards.
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
    /// Represents a highlighted item (card or player).
    /// </summary>
    public class HighlightedItem
    {
        public GameObject GameObject { get; set; }
        public string Name { get; set; }
        public string Zone { get; set; }
        public string HighlightType { get; set; }
        public bool IsOpponent { get; set; }
        public bool IsPlayer { get; set; }
        public string CardType { get; set; }
        public string PowerToughness { get; set; }
    }
}
