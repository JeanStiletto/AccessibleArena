using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Tracks which navigator set the current zone.
    /// Higher priority owners can override lower priority settings.
    /// Priority order: TargetNavigator > HighlightNavigator > BattlefieldNavigator > ZoneNavigator
    /// </summary>
    public enum ZoneOwner
    {
        None,
        ZoneNavigator,
        BattlefieldNavigator,
        HighlightNavigator,
        TargetNavigator
    }

    /// <summary>
    /// Handles navigation through game zones and cards within zones.
    /// Zone shortcuts: C (Hand), B (Battlefield), G (Graveyard), X (Exile), S (Stack)
    /// Opponent zones: Shift+G (Opponent Graveyard), Shift+X (Opponent Exile)
    /// Card navigation: Left/Right arrows to move between cards in current zone.
    ///
    /// Zone holder names discovered from game code analysis:
    /// - LocalHand_Desktop_16x9, OpponentHand_Desktop_16x9
    /// - BattlefieldCardHolder
    /// - LocalGraveyard, OpponentGraveyard
    /// - ExileCardHolder, StackCardHolder_Desktop_16x9
    /// - LocalLibrary, OpponentLibrary, CommandCardHolder
    /// </summary>
    public class ZoneNavigator
    {
        private readonly IAnnouncementService _announcer;

        private Dictionary<ZoneType, ZoneInfo> _zones = new Dictionary<ZoneType, ZoneInfo>();
        private ZoneType _currentZone = ZoneType.Hand;
        private ZoneOwner _zoneOwner = ZoneOwner.None;
        private int _cardIndexInZone = 0;
        private bool _isActive;

        // Known zone holder names from game code (discovered via log analysis)
        private static readonly Dictionary<string, ZoneType> ZoneHolderPatterns = new Dictionary<string, ZoneType>
        {
            { "LocalHand", ZoneType.Hand },
            { "BattlefieldCardHolder", ZoneType.Battlefield },
            { "LocalGraveyard", ZoneType.Graveyard },
            { "ExileCardHolder", ZoneType.Exile },
            { "StackCardHolder", ZoneType.Stack },
            { "LocalLibrary", ZoneType.Library },
            { "CommandCardHolder", ZoneType.Command },
            { "OpponentHand", ZoneType.OpponentHand },
            { "OpponentGraveyard", ZoneType.OpponentGraveyard },
            { "OpponentLibrary", ZoneType.OpponentLibrary },
            { "OpponentExile", ZoneType.OpponentExile }
        };

        public bool IsActive => _isActive;
        public ZoneType CurrentZone => _currentZone;
        public ZoneOwner CurrentZoneOwner => _zoneOwner;
        public int CardCount => _zones.ContainsKey(_currentZone) ? _zones[_currentZone].Cards.Count : 0;
        public int HandCardCount => _zones.ContainsKey(ZoneType.Hand) ? _zones[ZoneType.Hand].Cards.Count : 0;
        public int StackCardCount => _zones.ContainsKey(ZoneType.Stack) ? _zones[ZoneType.Stack].Cards.Count : 0;

        /// <summary>
        /// Sets the EventSystem focus to a GameObject with logging.
        /// All navigators should use this instead of direct EventSystem.SetSelectedGameObject calls.
        /// </summary>
        /// <param name="gameObject">The GameObject to focus, or null to clear focus</param>
        /// <param name="caller">Name of the calling class for debugging</param>
        public static void SetFocusedGameObject(GameObject gameObject, string caller)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var current = eventSystem.currentSelectedGameObject;
            if (current != gameObject)
            {
                string fromName = current != null ? current.name : "null";
                string toName = gameObject != null ? gameObject.name : "null";
                MelonLogger.Msg($"[Navigation] Focus change: {fromName} -> {toName} (by {caller})");
            }
            eventSystem.SetSelectedGameObject(gameObject);
        }

        /// <summary>
        /// Sets the current zone without full navigation (used by BattlefieldNavigator, TargetNavigator, etc).
        /// Tracks which navigator set the zone for debugging race conditions.
        /// </summary>
        /// <param name="zone">The zone to set</param>
        /// <param name="caller">Optional caller name for debugging zone change tracking</param>
        public void SetCurrentZone(ZoneType zone, string caller = null)
        {
            // Determine zone owner from caller string
            ZoneOwner newOwner = ParseZoneOwner(caller);

            // Log if zone or owner changed
            if (_currentZone != zone || _zoneOwner != newOwner)
            {
                string ownerChange = _zoneOwner != newOwner ? $", owner: {_zoneOwner} -> {newOwner}" : "";
                MelonLogger.Msg($"[ZoneNavigator] Zone change: {_currentZone} -> {zone}{ownerChange}{(caller != null ? $" (by {caller})" : "")}");
            }

            _currentZone = zone;
            _zoneOwner = newOwner;
        }

        /// <summary>
        /// Parses the caller string to determine which navigator is setting the zone.
        /// </summary>
        private ZoneOwner ParseZoneOwner(string caller)
        {
            if (string.IsNullOrEmpty(caller)) return ZoneOwner.None;

            if (caller.Contains("TargetNavigator")) return ZoneOwner.TargetNavigator;
            if (caller.Contains("HighlightNavigator")) return ZoneOwner.HighlightNavigator;
            if (caller.Contains("BattlefieldNavigator")) return ZoneOwner.BattlefieldNavigator;
            if (caller.Contains("ZoneNavigator") || caller.Contains("NavigateToZone")) return ZoneOwner.ZoneNavigator;

            return ZoneOwner.None;
        }

        // Reference to TargetNavigator for entering targeting mode after playing cards
        private TargetNavigator _targetNavigator;

        // Reference to DiscardNavigator for selection state announcements
        private DiscardNavigator _discardNavigator;

        // Reference to CombatNavigator for attacker state announcements
        private CombatNavigator _combatNavigator;

        public ZoneNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Sets the TargetNavigator reference for targeting mode after card plays.
        /// </summary>
        public void SetTargetNavigator(TargetNavigator navigator)
        {
            _targetNavigator = navigator;
        }

        /// <summary>
        /// Sets the DiscardNavigator reference for selection state announcements.
        /// </summary>
        public void SetDiscardNavigator(DiscardNavigator navigator)
        {
            _discardNavigator = navigator;
        }

        /// <summary>
        /// Sets the CombatNavigator reference for attacker state announcements.
        /// </summary>
        public void SetCombatNavigator(CombatNavigator navigator)
        {
            _combatNavigator = navigator;
        }

        /// <summary>
        /// Activates zone navigation and discovers all zones.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
            DiscoverZones();
        }

        /// <summary>
        /// Deactivates zone navigation.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _zones.Clear();
        }

        /// <summary>
        /// Handles zone navigation input.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Zone shortcuts
            if (Input.GetKeyDown(KeyCode.C))
            {
                NavigateToZone(ZoneType.Hand);
                return true;
            }

            // B shortcut handled by BattlefieldNavigator (row-based navigation)

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.G))
            {
                if (shift)
                    NavigateToZone(ZoneType.OpponentGraveyard);
                else
                    NavigateToZone(ZoneType.Graveyard);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                if (shift)
                    NavigateToZone(ZoneType.OpponentExile);
                else
                    NavigateToZone(ZoneType.Exile);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                NavigateToZone(ZoneType.Stack);
                return true;
            }

            // Left/Right arrows for navigating cards within current zone
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ClearEventSystemSelection();
                if (HasCardsInCurrentZone())
                {
                    PreviousCard();
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ClearEventSystemSelection();
                if (HasCardsInCurrentZone())
                {
                    NextCard();
                }
                return true;
            }

            // Enter key - play/activate current card
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (HasCardsInCurrentZone())
                {
                    ActivateCurrentCard();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Discovers all zone holders and their cards.
        /// </summary>
        public void DiscoverZones()
        {
            _zones.Clear();
            MelonLogger.Msg("[ZoneNavigator] Discovering zones...");

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                string name = go.name;

                foreach (var pattern in ZoneHolderPatterns)
                {
                    if (name.Contains(pattern.Key))
                    {
                        var zoneType = pattern.Value;
                        if (!_zones.ContainsKey(zoneType))
                        {
                            var zoneInfo = new ZoneInfo
                            {
                                Type = zoneType,
                                Holder = go,
                                ZoneId = ParseZoneId(name),
                                OwnerId = ParseOwnerId(name)
                            };

                            DiscoverCardsInZone(zoneInfo);
                            _zones[zoneType] = zoneInfo;
                            MelonLogger.Msg($"[ZoneNavigator] Zone: {zoneType} - {name} - {zoneInfo.Cards.Count} cards");
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Discovers cards within a zone holder.
        /// </summary>
        private void DiscoverCardsInZone(ZoneInfo zone)
        {
            zone.Cards.Clear();

            if (zone.Holder == null) return;

            foreach (Transform child in zone.Holder.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                var go = child.gameObject;

                if (CardDetector.IsCard(go))
                {
                    if (!zone.Cards.Any(c => c.transform.IsChildOf(go.transform) || go.transform.IsChildOf(c.transform)))
                    {
                        zone.Cards.Add(go);
                    }
                }
            }

            // Sort cards by position (left to right)
            zone.Cards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        }

        /// <summary>
        /// Navigates to a specific zone and announces it.
        /// </summary>
        public void NavigateToZone(ZoneType zone)
        {
            DiscoverZones();

            if (!_zones.ContainsKey(zone))
            {
                _announcer.Announce(Strings.ZoneNotFound(GetZoneName(zone)), AnnouncementPriority.High);
                return;
            }

            SetCurrentZone(zone, "NavigateToZone");
            _cardIndexInZone = 0;

            var zoneInfo = _zones[zone];
            int cardCount = zoneInfo.Cards.Count;

            if (cardCount == 0)
            {
                _announcer.Announce(Strings.ZoneEmpty(GetZoneName(zone)), AnnouncementPriority.High);
            }
            else
            {
                _announcer.Announce(Strings.ZoneWithCount(GetZoneName(zone), cardCount), AnnouncementPriority.High);
                AnnounceCurrentCard();
            }
        }

        /// <summary>
        /// Moves to the next card in the current zone.
        /// Stops at the right border (last card) without wrapping.
        /// </summary>
        public void NextCard()
        {
            if (!_zones.ContainsKey(_currentZone)) return;

            var zoneInfo = _zones[_currentZone];
            if (zoneInfo.Cards.Count == 0) return;

            if (_cardIndexInZone < zoneInfo.Cards.Count - 1)
            {
                _cardIndexInZone++;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.EndOfZone);
            }
        }

        /// <summary>
        /// Moves to the previous card in the current zone.
        /// Stops at the left border (first card) without wrapping.
        /// </summary>
        public void PreviousCard()
        {
            if (!_zones.ContainsKey(_currentZone)) return;

            var zoneInfo = _zones[_currentZone];
            if (zoneInfo.Cards.Count == 0) return;

            if (_cardIndexInZone > 0)
            {
                _cardIndexInZone--;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.BeginningOfZone);
            }
        }

        /// <summary>
        /// Gets the current card in the current zone.
        /// </summary>
        public GameObject GetCurrentCard()
        {
            if (!_zones.ContainsKey(_currentZone)) return null;

            var zoneInfo = _zones[_currentZone];
            if (_cardIndexInZone >= zoneInfo.Cards.Count) return null;

            return zoneInfo.Cards[_cardIndexInZone];
        }

        /// <summary>
        /// Activates (plays/casts) the current card.
        /// For hand cards: Two-click approach (click card, then click screen center).
        /// For other zones: simulates click.
        /// </summary>
        public void ActivateCurrentCard()
        {
            var card = GetCurrentCard();

            if (card == null)
            {
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.High);
                return;
            }

            string cardName = CardDetector.GetCardName(card);
            MelonLogger.Msg($"[ZoneNavigator] Activating card: {cardName} ({card.name}) in zone {_currentZone}");

            // For hand cards, use the two-click approach (like sighted players)
            if (_currentZone == ZoneType.Hand)
            {
                MelonLogger.Msg($"[ZoneNavigator] Playing {cardName} from hand via two-click");

                // Two-click is async, result comes via callback
                // Pass TargetNavigator so targeting mode is entered after the card is played
                UIActivator.PlayCardViaTwoClick(card, (success, message) =>
                {
                    if (success)
                    {
                        // Don't announce "Played" here - targeting mode will announce if needed
                        MelonLogger.Msg($"[ZoneNavigator] Card play succeeded");
                    }
                    else
                    {
                        _announcer.Announce(Strings.CouldNotPlay(cardName), AnnouncementPriority.High);
                        MelonLogger.Msg($"[ZoneNavigator] Card play failed: {message}");
                    }
                }, _targetNavigator);
            }
            else
            {
                // For other zones (battlefield, etc.), use click
                var result = UIActivator.SimulatePointerClick(card);

                if (!result.Success)
                {
                    _announcer.Announce(Strings.CannotActivate(cardName), AnnouncementPriority.High);
                    MelonLogger.Msg($"[ZoneNavigator] Card activation failed: {result.Message}");
                }
            }
        }

        /// <summary>
        /// Logs a summary of discovered zones.
        /// </summary>
        public void LogZoneSummary()
        {
            MelonLogger.Msg("[ZoneNavigator] --- Zone Summary ---");
            foreach (var zone in _zones.Values.OrderBy(z => z.Type.ToString()))
            {
                MelonLogger.Msg($"[ZoneNavigator]   {zone.Type}: {zone.Cards.Count} cards (ZoneId: #{zone.ZoneId})");
            }
        }

        private void AnnounceCurrentCard()
        {
            if (!_zones.ContainsKey(_currentZone)) return;

            var zoneInfo = _zones[_currentZone];
            if (_cardIndexInZone >= zoneInfo.Cards.Count) return;

            var card = zoneInfo.Cards[_cardIndexInZone];
            string cardName = CardDetector.GetCardName(card);
            int position = _cardIndexInZone + 1;
            int total = zoneInfo.Cards.Count;

            // Add selection state if in discard mode
            string selectionState = _discardNavigator?.GetSelectionStateText(card) ?? "";

            // Add combat state if in declare attackers/blockers phase (battlefield only)
            string combatState = "";
            if (_currentZone == ZoneType.Battlefield)
            {
                combatState = _combatNavigator?.GetCombatStateText(card) ?? "";
            }

            _announcer.Announce($"{cardName}{selectionState}{combatState}, {position} of {total}", AnnouncementPriority.Normal);

            // Set EventSystem focus to the card - this ensures other navigators
            // (like PlayerPortrait) detect the focus change and exit their modes
            if (card != null)
            {
                SetFocusedGameObject(card, "ZoneNavigator");
            }

            // Prepare card info navigation with zone context
            var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
            if (cardNavigator != null && CardDetector.IsCard(card))
            {
                cardNavigator.PrepareForCard(card, _currentZone);
            }
        }

        private bool HasCardsInCurrentZone()
        {
            if (!_zones.ContainsKey(_currentZone)) return false;
            return _zones[_currentZone].Cards.Count > 0;
        }

        private void ClearEventSystemSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                SetFocusedGameObject(null, "ZoneNavigator.Clear");
            }
        }

        private string GetZoneName(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Hand: return "Your hand";
                case ZoneType.Battlefield: return "Battlefield";
                case ZoneType.Graveyard: return "Your graveyard";
                case ZoneType.Exile: return "Exile";
                case ZoneType.Stack: return "Stack";
                case ZoneType.Library: return "Your library";
                case ZoneType.Command: return "Command zone";
                case ZoneType.OpponentHand: return "Opponent's hand";
                case ZoneType.OpponentGraveyard: return "Opponent's graveyard";
                case ZoneType.OpponentLibrary: return "Opponent's library";
                case ZoneType.OpponentExile: return "Opponent's exile";
                default: return zone.ToString();
            }
        }

        private int ParseZoneId(string name)
        {
            var match = Regex.Match(name, @"ZoneId:\s*#(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private int ParseOwnerId(string name)
        {
            var match = Regex.Match(name, @"OwnerId:\s*#(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }

    /// <summary>
    /// Types of zones in the duel scene.
    /// </summary>
    public enum ZoneType
    {
        Hand,
        Battlefield,
        Graveyard,
        Exile,
        Stack,
        Library,
        Command,
        OpponentHand,
        OpponentGraveyard,
        OpponentLibrary,
        OpponentExile
    }

    /// <summary>
    /// Information about a zone including its cards.
    /// </summary>
    public class ZoneInfo
    {
        public ZoneType Type { get; set; }
        public GameObject Holder { get; set; }
        public int ZoneId { get; set; }
        public int OwnerId { get; set; }
        public List<GameObject> Cards { get; set; } = new List<GameObject>();
    }
}
