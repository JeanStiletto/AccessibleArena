using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Tracks which navigator set the current zone.
    /// Higher priority owners can override lower priority settings.
    /// Priority order: HotHighlightNavigator > BattlefieldNavigator > ZoneNavigator
    /// </summary>
    public enum ZoneOwner
    {
        None,
        ZoneNavigator,
        BattlefieldNavigator,
        HighlightNavigator,      // Used by HotHighlightNavigator
        // DEPRECATED: TargetNavigator - was separate, now unified into HotHighlightNavigator
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
        private bool _dirty;

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
        /// <summary>
        /// Gets the cached stack card count. May be stale between DiscoverZones() calls.
        /// For timing-sensitive checks, use GetFreshStackCount() instead.
        /// </summary>
        public int StackCardCount => _zones.ContainsKey(ZoneType.Stack) ? _zones[ZoneType.Stack].Cards.Count : 0;

        /// <summary>
        /// Gets a fresh count of cards on the stack by directly scanning the scene.
        /// Use this for timing-sensitive checks where the cached StackCardCount may be stale.
        /// This is lightweight - only counts cards, doesn't discover full zone info.
        /// </summary>
        public int GetFreshStackCount()
        {
            int count = 0;
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                // Check if this is the stack holder
                if (!go.name.Contains("StackCardHolder"))
                    continue;

                // Count CDC (card) children
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy)
                        continue;

                    if (child.name.Contains("CDC #"))
                    {
                        count++;
                    }
                }
                break; // Only one stack holder
            }
            return count;
        }

        /// <summary>
        /// Gets the local player's ID from the discovered zones.
        /// Uses the OwnerId from LocalHand or LocalLibrary zone.
        /// Call this after Activate() to ensure zones are discovered.
        /// </summary>
        public uint GetLocalPlayerId()
        {
            // Try Hand zone first (most reliable)
            if (_zones.TryGetValue(ZoneType.Hand, out var handZone) && handZone.OwnerId > 0)
            {
                MelonLogger.Msg($"[ZoneNavigator] Local player ID from Hand zone: #{handZone.OwnerId}");
                return (uint)handZone.OwnerId;
            }

            // Fallback to Library zone
            if (_zones.TryGetValue(ZoneType.Library, out var libZone) && libZone.OwnerId > 0)
            {
                MelonLogger.Msg($"[ZoneNavigator] Local player ID from Library zone: #{libZone.OwnerId}");
                return (uint)libZone.OwnerId;
            }

            // Default to 1 if not found
            MelonLogger.Warning("[ZoneNavigator] Could not detect local player ID from zones, defaulting to #1");
            return 1;
        }

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

            // HotHighlightNavigator uses HighlightNavigator owner for zone ownership
            if (caller.Contains("HotHighlightNavigator")) return ZoneOwner.HighlightNavigator;
            if (caller.Contains("HighlightNavigator")) return ZoneOwner.HighlightNavigator;
            // DEPRECATED: if (caller.Contains("TargetNavigator")) return ZoneOwner.TargetNavigator;
            if (caller.Contains("BattlefieldNavigator")) return ZoneOwner.BattlefieldNavigator;
            if (caller.Contains("ZoneNavigator") || caller.Contains("NavigateToZone")) return ZoneOwner.ZoneNavigator;

            return ZoneOwner.None;
        }

        // DEPRECATED: TargetNavigator was used to enter targeting mode after playing cards
        // Now HotHighlightNavigator handles targeting via game's HotHighlight system
        // private TargetNavigator _targetNavigator;

        // Reference to DiscardNavigator for selection state announcements
        // private DiscardNavigator _discardNavigator;  // DEPRECATED

        // Reference to CombatNavigator for attacker state announcements
        private CombatNavigator _combatNavigator;

        // Reference to HotHighlightNavigator for clearing state on zone navigation
        private HotHighlightNavigator _hotHighlightNavigator;

        public ZoneNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Sets the CombatNavigator reference for attacker state announcements.
        /// </summary>
        public void SetCombatNavigator(CombatNavigator navigator)
        {
            _combatNavigator = navigator;
        }

        /// <summary>
        /// Sets the HotHighlightNavigator reference for clearing state on zone navigation.
        /// </summary>
        public void SetHotHighlightNavigator(HotHighlightNavigator navigator)
        {
            _hotHighlightNavigator = navigator;
        }

        /// <summary>
        /// Marks zone data as stale. Called by DuelAnnouncer when zone contents change.
        /// The next card navigation input will refresh the active zone before navigating.
        /// </summary>
        public void MarkDirty()
        {
            _dirty = true;
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
        /// Deactivates zone navigation and resets all state.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _zones.Clear();
            _cardIndexInZone = 0;
            _zoneOwner = ZoneOwner.None;
        }

        /// <summary>
        /// Handles zone navigation input.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Check shift state FIRST for all key handlers
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // C key: Hand navigation (no shift) or Opponent hand count (with shift)
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (shift)
                {
                    // Shift+C: Opponent's hand count
                    AnnounceOpponentHandCount();
                }
                else
                {
                    // C: Navigate to your hand
                    _hotHighlightNavigator?.ClearState();
                    NavigateToZone(ZoneType.Hand);
                }
                return true;
            }

            // B shortcut handled by BattlefieldNavigator (row-based navigation)

            if (Input.GetKeyDown(KeyCode.G))
            {
                _hotHighlightNavigator?.ClearState();
                if (shift)
                    NavigateToZone(ZoneType.OpponentGraveyard);
                else
                    NavigateToZone(ZoneType.Graveyard);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                _hotHighlightNavigator?.ClearState();
                if (shift)
                    NavigateToZone(ZoneType.OpponentExile);
                else
                    NavigateToZone(ZoneType.Exile);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                _hotHighlightNavigator?.ClearState();
                NavigateToZone(ZoneType.Stack);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                _hotHighlightNavigator?.ClearState();
                if (shift)
                    AnnounceOpponentCommander();
                else
                    NavigateToZone(ZoneType.Command);
                return true;
            }

            // D key for library counts (hidden zone info)
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (shift)
                {
                    // Shift+D: Opponent's library count
                    AnnounceOpponentLibraryCount();
                }
                else
                {
                    // D: Your library count
                    AnnounceLocalLibraryCount();
                }
                return true;
            }

            // Left/Right arrows for navigating cards within current zone
            // Skip if current zone is Battlefield - that's handled by BattlefieldNavigator
            if (_currentZone != ZoneType.Battlefield)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    RefreshIfDirty();
                    ClearEventSystemSelection();
                    if (HasCardsInCurrentZone())
                    {
                        PreviousCard();
                    }
                    else
                    {
                        // Announce empty zone so user knows why nothing happened
                        _announcer.AnnounceInterrupt(Strings.ZoneEmpty(GetZoneName(_currentZone)));
                    }
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    RefreshIfDirty();
                    ClearEventSystemSelection();
                    if (HasCardsInCurrentZone())
                    {
                        NextCard();
                    }
                    else
                    {
                        // Announce empty zone so user knows why nothing happened
                        _announcer.AnnounceInterrupt(Strings.ZoneEmpty(GetZoneName(_currentZone)));
                    }
                    return true;
                }
            }

            // Up/Down arrows - consume for non-Battlefield zones to prevent menu navigation
            // Battlefield has its own Up/Down handling in BattlefieldNavigator
            if (_currentZone != ZoneType.Battlefield)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    RefreshIfDirty();
                    // For zones with cards, CardInfoNavigator handles Up/Down for card details
                    // For empty zones, just announce empty and consume the input
                    if (!HasCardsInCurrentZone())
                    {
                        _announcer.AnnounceInterrupt(Strings.ZoneEmpty(GetZoneName(_currentZone)));
                    }
                    // If zone has cards but CardInfoNavigator isn't active, re-announce current card
                    else
                    {
                        AnnounceCurrentCard();
                    }
                    return true;
                }
            }

            // Home/End for jumping to first/last card in zone
            if (Input.GetKeyDown(KeyCode.Home))
            {
                RefreshIfDirty();
                ClearEventSystemSelection();
                if (HasCardsInCurrentZone())
                {
                    FirstCard();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.ZoneEmpty(GetZoneName(_currentZone)));
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                RefreshIfDirty();
                ClearEventSystemSelection();
                if (HasCardsInCurrentZone())
                {
                    LastCard();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.ZoneEmpty(GetZoneName(_currentZone)));
                }
                return true;
            }

            // Enter key - play/activate current card
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                RefreshIfDirty();
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

            // MTGA places commanders visually in the hand holder, not CommandCardHolder.
            // Populate the Command zone from hand cards whose model ZoneType is "Command".
            PopulateCommandZoneFromHand();
        }

        /// <summary>
        /// Finds commander cards in the hand holder (model ZoneType=="Command")
        /// and adds them to the Command zone. MTGA always places castable commanders
        /// in the hand holder visually, leaving CommandCardHolder empty.
        /// </summary>
        private void PopulateCommandZoneFromHand()
        {
            if (!_zones.TryGetValue(ZoneType.Command, out var commandZone)) return;
            if (commandZone.Cards.Count > 0) return; // Already has cards, don't override

            if (!_zones.TryGetValue(ZoneType.Hand, out var handZone)) return;

            foreach (var card in handZone.Cards)
            {
                string modelZone = CardModelProvider.GetCardZoneTypeName(card);
                if (modelZone == "Command")
                {
                    commandZone.Cards.Add(card);
                    string cardName = CardDetector.GetCardName(card);
                    MelonLogger.Msg($"[ZoneNavigator] Added commander {cardName} to Command zone from hand");
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
        /// If dirty, refreshes only the current zone's card list and clamps the index.
        /// Called before card navigation to ensure the list is up-to-date.
        /// </summary>
        private void RefreshIfDirty()
        {
            if (!_dirty) return;
            _dirty = false;

            if (!_zones.TryGetValue(_currentZone, out var zoneInfo)) return;
            if (zoneInfo.Holder == null) return;

            int oldCount = zoneInfo.Cards.Count;
            DiscoverCardsInZone(zoneInfo);
            int newCount = zoneInfo.Cards.Count;

            if (oldCount != newCount)
            {
                MelonLogger.Msg($"[ZoneNavigator] Refreshed {_currentZone}: {oldCount} -> {newCount} cards");
            }

            // Clamp index to valid range
            if (newCount == 0)
                _cardIndexInZone = 0;
            else if (_cardIndexInZone >= newCount)
                _cardIndexInZone = newCount - 1;
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
                AnnounceCurrentCard(includeZoneName: true);
            }
        }

        /// <summary>
        /// Navigates to a specific card in a zone.
        /// Finds the card in the zone's card list, syncs index, and announces.
        /// Used by HotHighlightNavigator to sync zone position on Tab.
        /// </summary>
        /// <param name="zone">The zone the card is in</param>
        /// <param name="card">The card GameObject to navigate to</param>
        /// <param name="announceZoneChange">If true, includes zone name in announcement</param>
        /// <returns>True if the card was found and navigated to</returns>
        public bool NavigateToSpecificCard(ZoneType zone, GameObject card, bool announceZoneChange)
        {
            if (card == null) return false;

            DiscoverZones();

            if (!_zones.ContainsKey(zone))
                return false;

            var zoneInfo = _zones[zone];
            int index = zoneInfo.Cards.IndexOf(card);
            if (index < 0)
                return false;

            bool zoneChanged = _currentZone != zone;
            SetCurrentZone(zone, "HotHighlightNavigator");
            _cardIndexInZone = index;

            // Use High priority to bypass duplicate check - user explicitly pressed Tab
            AnnounceCurrentCard(includeZoneName: announceZoneChange || zoneChanged, priority: AnnouncementPriority.High);
            return true;
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
                _announcer.AnnounceInterruptVerbose(Strings.EndOfZone);
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
                _announcer.AnnounceInterruptVerbose(Strings.BeginningOfZone);
            }
        }

        /// <summary>
        /// Jumps to the first card in the current zone.
        /// </summary>
        public void FirstCard()
        {
            if (!_zones.ContainsKey(_currentZone)) return;

            var zoneInfo = _zones[_currentZone];
            if (zoneInfo.Cards.Count == 0) return;

            if (_cardIndexInZone == 0)
            {
                _announcer.AnnounceInterruptVerbose(Strings.BeginningOfZone);
                return;
            }

            _cardIndexInZone = 0;
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Jumps to the last card in the current zone.
        /// </summary>
        public void LastCard()
        {
            if (!_zones.ContainsKey(_currentZone)) return;

            var zoneInfo = _zones[_currentZone];
            if (zoneInfo.Cards.Count == 0) return;

            int lastIndex = zoneInfo.Cards.Count - 1;
            if (_cardIndexInZone == lastIndex)
            {
                _announcer.AnnounceInterruptVerbose(Strings.EndOfZone);
                return;
            }

            _cardIndexInZone = lastIndex;
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Gets all cards in the specified zone. Returns null if zone not discovered.
        /// </summary>
        public List<GameObject> GetCardsInZone(ZoneType zone)
        {
            if (_zones.TryGetValue(zone, out var zoneInfo))
                return zoneInfo.Cards;
            return null;
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

            // For hand and command zone cards, use the two-click approach (like sighted players)
            // Command zone cards (commander/companion) are castable just like hand cards
            if (_currentZone == ZoneType.Hand || _currentZone == ZoneType.Command)
            {
                // Check if selection mode is active (discard, exile choices, etc.)
                if (_currentZone == ZoneType.Hand && _hotHighlightNavigator != null && _hotHighlightNavigator.TryToggleSelection(card))
                {
                    MelonLogger.Msg($"[ZoneNavigator] Selection toggled for {cardName}");
                    return;
                }

                MelonLogger.Msg($"[ZoneNavigator] Playing {cardName} from {_currentZone} via two-click");

                // Two-click is async, result comes via callback
                // DEPRECATED: TargetNavigator was passed to enter targeting after card play
                // Now HotHighlightNavigator discovers targets via game's HotHighlight system
                UIActivator.PlayCardViaTwoClick(card, (success, message) =>
                {
                    if (success)
                    {
                        // Don't announce "Played" here - targeting mode will be discovered via Tab
                        MelonLogger.Msg($"[ZoneNavigator] Card play succeeded");
                    }
                    else
                    {
                        _announcer.Announce(Strings.CouldNotPlay(cardName), AnnouncementPriority.High);
                        MelonLogger.Msg($"[ZoneNavigator] Card play failed: {message}");
                    }
                });
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

        private void AnnounceCurrentCard(bool includeZoneName = false, AnnouncementPriority priority = AnnouncementPriority.Normal)
        {
            if (!_zones.ContainsKey(_currentZone)) return;

            var zoneInfo = _zones[_currentZone];
            if (_cardIndexInZone >= zoneInfo.Cards.Count) return;

            var card = zoneInfo.Cards[_cardIndexInZone];
            string cardName = CardDetector.GetCardName(card);
            int position = _cardIndexInZone + 1;
            int total = zoneInfo.Cards.Count;

            // Add selection state if in discard/selection mode
            string selectionState = _hotHighlightNavigator?.GetSelectionStateText(card) ?? "";

            // Check if the card's actual game zone differs from the UI holder zone
            // (e.g., commander in Command zone shown in hand, flashback card in Graveyard shown in hand)
            string originZoneText = "";
            if (_currentZone == ZoneType.Hand)
            {
                originZoneText = GetOriginZoneText(card);
            }

            // In selection mode (discard), adjust position/total to only count selectable cards
            // so non-discardable cards (e.g., commander from command zone) don't inflate the count
            bool inSelectionMode = _hotHighlightNavigator?.IsInSelectionMode() ?? false;
            if (inSelectionMode && _currentZone == ZoneType.Hand)
            {
                bool currentIsSelectable = CardDetector.HasHotHighlight(card)
                    || (_hotHighlightNavigator?.IsCardCurrentlySelected(card) ?? false);

                if (currentIsSelectable)
                {
                    int selectablePosition = 0;
                    int selectableTotal = 0;
                    for (int i = 0; i < zoneInfo.Cards.Count; i++)
                    {
                        var c = zoneInfo.Cards[i];
                        if (CardDetector.HasHotHighlight(c) || (_hotHighlightNavigator?.IsCardCurrentlySelected(c) ?? false))
                        {
                            selectableTotal++;
                            if (i < _cardIndexInZone)
                                selectablePosition++;
                            else if (i == _cardIndexInZone)
                                selectablePosition = selectableTotal;
                        }
                    }
                    if (selectableTotal > 0)
                    {
                        position = selectablePosition;
                        total = selectableTotal;
                    }
                }
            }

            // Add combat state if in declare attackers/blockers phase (battlefield only)
            string combatState = "";
            string attachmentText = "";
            if (_currentZone == ZoneType.Battlefield)
            {
                combatState = _combatNavigator?.GetCombatStateText(card) ?? "";
                attachmentText = CardModelProvider.GetAttachmentText(card);
            }

            // Add targeting info for battlefield and stack cards
            string targetingText = "";
            if (_currentZone == ZoneType.Battlefield || _currentZone == ZoneType.Stack)
            {
                targetingText = CardModelProvider.GetTargetingText(card);
            }

            string prefix = includeZoneName ? $"{GetZoneName(_currentZone)}, " : "";
            _announcer.Announce($"{prefix}{cardName}{originZoneText}{selectionState}{combatState}{attachmentText}{targetingText}, {position} of {total}", priority);

            // Set EventSystem focus to the card - this ensures other navigators
            // (like PlayerPortrait) detect the focus change and exit their modes
            if (card != null)
            {
                SetFocusedGameObject(card, "ZoneNavigator");
            }

            // Prepare card info navigation with zone context
            var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNavigator != null && CardDetector.IsCard(card))
            {
                cardNavigator.PrepareForCard(card, _currentZone);
            }
        }

        /// <summary>
        /// Maps game ZoneType enum names to mod ZoneType for origin zone detection.
        /// Returns null if the zone matches the current UI zone (no annotation needed).
        /// </summary>
        private static readonly Dictionary<string, ZoneType> GameZoneToModZone = new Dictionary<string, ZoneType>
        {
            { "Hand", ZoneType.Hand },
            { "Battlefield", ZoneType.Battlefield },
            { "Graveyard", ZoneType.Graveyard },
            { "Exile", ZoneType.Exile },
            { "Stack", ZoneType.Stack },
            { "Library", ZoneType.Library },
            { "Command", ZoneType.Command }
        };

        /// <summary>
        /// Gets origin zone annotation for cards whose game zone differs from UI zone.
        /// E.g., commander in Command zone shown in hand returns ", Kommandozone".
        /// Returns empty string if no annotation needed.
        /// </summary>
        private string GetOriginZoneText(GameObject card)
        {
            string modelZoneName = CardModelProvider.GetCardZoneTypeName(card);
            if (string.IsNullOrEmpty(modelZoneName)) return "";

            if (GameZoneToModZone.TryGetValue(modelZoneName, out var modelZoneType))
            {
                if (modelZoneType != _currentZone)
                {
                    return $", {Strings.GetZoneName(modelZoneType)}";
                }
            }

            return "";
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

        #region Hidden Zone Count Announcements

        /// <summary>
        /// Gets the card count for a zone, refreshing zones if needed.
        /// </summary>
        private int GetZoneCardCount(ZoneType zone)
        {
            // Refresh zones to get current counts
            DiscoverZones();

            if (_zones.TryGetValue(zone, out var zoneInfo))
            {
                return zoneInfo.Cards.Count;
            }
            return -1;
        }

        /// <summary>
        /// Announces the local player's library card count.
        /// </summary>
        private void AnnounceLocalLibraryCount()
        {
            int count = GetZoneCardCount(ZoneType.Library);
            if (count >= 0)
            {
                _announcer.Announce(Strings.LibraryCount(count), AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(Strings.LibraryCountNotAvailable, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Announces the opponent's library card count.
        /// </summary>
        private void AnnounceOpponentLibraryCount()
        {
            int count = GetZoneCardCount(ZoneType.OpponentLibrary);
            if (count >= 0)
            {
                _announcer.Announce(Strings.OpponentLibraryCount(count), AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(Strings.OpponentLibraryCountNotAvailable, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Announces the opponent's hand card count.
        /// </summary>
        private void AnnounceOpponentHandCount()
        {
            int count = GetZoneCardCount(ZoneType.OpponentHand);
            if (count >= 0)
            {
                _announcer.Announce(Strings.OpponentHandCount(count), AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(Strings.OpponentHandCountNotAvailable, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Announces the opponent's commander card name (Brawl/Commander).
        /// </summary>
        private void AnnounceOpponentCommander()
        {
            var duelAnnouncer = DuelAnnouncer.Instance;
            if (duelAnnouncer == null)
            {
                _announcer.Announce(Strings.ZoneNotFound(Strings.GetZoneName(ZoneType.OpponentCommand)), AnnouncementPriority.High);
                return;
            }

            // Try cached card info from zone transfer first, fall back to database lookup
            var cardInfo = duelAnnouncer.GetOpponentCommanderInfo();
            if (cardInfo == null)
            {
                uint grpId = duelAnnouncer.GetOpponentCommanderGrpId();
                if (grpId == 0)
                {
                    _announcer.Announce(Strings.ZoneEmpty(Strings.GetZoneName(ZoneType.OpponentCommand)), AnnouncementPriority.High);
                    return;
                }
                // Fallback: just announce the name
                string name = CardModelProvider.GetNameFromGrpId(grpId);
                _announcer.Announce($"{Strings.GetZoneName(ZoneType.OpponentCommand)}, {name ?? "Unknown"}", AnnouncementPriority.High);
                return;
            }

            string commanderName = cardInfo.Value.Name ?? "Unknown";

            // Set zone so Left/Right don't navigate the previous zone
            SetCurrentZone(ZoneType.OpponentCommand, "NavigateToZone");

            _announcer.Announce($"{Strings.GetZoneName(ZoneType.OpponentCommand)}, {commanderName}", AnnouncementPriority.High);

            // Prepare CardInfoNavigator for Up/Down card detail navigation
            var blocks = CardDetector.BuildInfoBlocks(cardInfo.Value);
            var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
            cardNavigator?.PrepareForCardInfo(blocks, commanderName);
        }

        #endregion

        private string GetZoneName(ZoneType zone)
        {
            return Strings.GetZoneName(zone);
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
        OpponentExile,
        OpponentCommand,
        Browser
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
