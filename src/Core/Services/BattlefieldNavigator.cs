using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Handles battlefield navigation organized into 6 logical rows by card type and ownership.
    /// Row shortcuts: A (Your Lands), R (Your Non-creatures), B (Your Creatures)
    ///                Shift+A (Enemy Lands), Shift+R (Enemy Non-creatures), Shift+B (Enemy Creatures)
    /// Navigation: Left/Right arrows within row, Shift+Up/Down to switch rows.
    /// </summary>
    public class BattlefieldNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ZoneNavigator _zoneNavigator;

        private Dictionary<BattlefieldRow, List<GameObject>> _rows = new Dictionary<BattlefieldRow, List<GameObject>>();
        private BattlefieldRow _currentRow = BattlefieldRow.PlayerCreatures;
        private int _currentIndex = 0;
        private bool _isActive;
        private bool _dirty;

        // Reference to CombatNavigator for attacker/blocker state announcements
        private CombatNavigator _combatNavigator;

        // Per-frame state watcher: after Enter click, watch for state change on that card
        private GameObject _watchedCard;
        private string _watchedStateBefore;
        private float _watchStartTime;
        private const float WatchTimeoutSeconds = 3f;
        private const float WatchCheckIntervalSeconds = 0.1f;
        private float _lastWatchCheckTime;

        // Row order from top (enemy side) to bottom (player side) for Shift+Up/Down navigation
        private static readonly BattlefieldRow[] RowOrder = {
            BattlefieldRow.EnemyLands,
            BattlefieldRow.EnemyNonCreatures,
            BattlefieldRow.EnemyCreatures,
            BattlefieldRow.PlayerCreatures,
            BattlefieldRow.PlayerNonCreatures,
            BattlefieldRow.PlayerLands
        };

        public bool IsActive => _isActive;
        public BattlefieldRow CurrentRow => _currentRow;

        public BattlefieldNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = zoneNavigator;

            // Initialize empty rows
            foreach (BattlefieldRow row in Enum.GetValues(typeof(BattlefieldRow)))
            {
                _rows[row] = new List<GameObject>();
            }
        }

        /// <summary>
        /// Sets the CombatNavigator reference for combat state announcements.
        /// </summary>
        public void SetCombatNavigator(CombatNavigator navigator)
        {
            _combatNavigator = navigator;
        }

        /// <summary>
        /// Marks battlefield data as stale. Called by DuelAnnouncer when zone contents change.
        /// The next card navigation input will refresh before navigating.
        /// </summary>
        public void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// If dirty, refreshes battlefield cards and clamps the index.
        /// </summary>
        private void RefreshIfDirty()
        {
            if (!_dirty) return;
            _dirty = false;

            int oldCount = _rows[_currentRow].Count;
            DiscoverAndCategorizeCards();
            int newCount = _rows[_currentRow].Count;

            if (oldCount != newCount)
            {
                Log.Msg("BattlefieldNavigator", $"Refreshed {_currentRow}: {oldCount} -> {newCount} cards");
            }

            // Clamp index to valid range
            if (newCount == 0)
                _currentIndex = 0;
            else if (_currentIndex >= newCount)
                _currentIndex = newCount - 1;
        }

        /// <summary>
        /// Activates battlefield navigation.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
            DiscoverAndCategorizeCards();
        }

        /// <summary>
        /// Deactivates battlefield navigation and resets all state.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            foreach (var row in _rows.Values)
            {
                row.Clear();
            }
            _currentRow = BattlefieldRow.PlayerCreatures;
            _currentIndex = 0;
        }

        /// <summary>
        /// Handles battlefield navigation input.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Per-frame: check if a watched card's state changed after Enter click
            CheckWatchedCardState();

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Row shortcuts: A for lands (also announces floating mana for player lands)
            if (Input.GetKeyDown(KeyCode.A))
            {
                _zoneNavigator.SetCurrentZone(ZoneType.Battlefield, "BattlefieldNavigator");
                if (shift)
                {
                    NavigateToRow(BattlefieldRow.EnemyLands);
                }
                else
                {
                    // Announce floating mana when going to player lands
                    string mana = DuelAnnouncer.CurrentManaPool;
                    if (!string.IsNullOrEmpty(mana))
                    {
                        _announcer.Announce(Strings.ManaAmount(mana), AnnouncementPriority.High);
                    }
                    NavigateToRow(BattlefieldRow.PlayerLands);
                }
                _zoneNavigator.AnnounceBrowserReturnHintIfNeeded();
                return true;
            }

            // Row shortcuts: R for non-creatures (artifacts, enchantments, planeswalkers)
            if (Input.GetKeyDown(KeyCode.R))
            {
                _zoneNavigator.SetCurrentZone(ZoneType.Battlefield, "BattlefieldNavigator");
                if (shift)
                    NavigateToRow(BattlefieldRow.EnemyNonCreatures);
                else
                    NavigateToRow(BattlefieldRow.PlayerNonCreatures);
                _zoneNavigator.AnnounceBrowserReturnHintIfNeeded();
                return true;
            }

            // Row shortcuts: B for creatures
            if (Input.GetKeyDown(KeyCode.B))
            {
                _zoneNavigator.SetCurrentZone(ZoneType.Battlefield, "BattlefieldNavigator");
                if (shift)
                    NavigateToRow(BattlefieldRow.EnemyCreatures);
                else
                    NavigateToRow(BattlefieldRow.PlayerCreatures);
                _zoneNavigator.AnnounceBrowserReturnHintIfNeeded();
                return true;
            }

            bool inBattlefield = _zoneNavigator.CurrentZone == ZoneType.Battlefield;

            // Row switching with Shift+Up/Down (only when already in battlefield)
            if (inBattlefield && shift && Input.GetKeyDown(KeyCode.UpArrow))
            {
                PreviousRow();
                return true;
            }

            if (inBattlefield && shift && Input.GetKeyDown(KeyCode.DownArrow))
            {
                NextRow();
                return true;
            }

            if (!shift && inBattlefield && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                RefreshIfDirty();
                ClearEventSystemSelection();
                PreviousCard();
                return true;
            }

            if (!shift && inBattlefield && Input.GetKeyDown(KeyCode.RightArrow))
            {
                RefreshIfDirty();
                ClearEventSystemSelection();
                NextCard();
                return true;
            }

            // Plain Up/Down (without shift) - re-announce current card or empty row
            // This prevents fall-through to base menu navigation when in battlefield
            if (!shift && inBattlefield && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
                RefreshIfDirty();
                var cards = _rows[_currentRow];
                if (cards.Count == 0)
                {
                    _announcer.AnnounceInterrupt(Strings.RowEmpty(GetRowName(_currentRow)));
                }
                else
                {
                    AnnounceCurrentCard();
                }
                return true;
            }

            // Home/End for jumping to first/last card in row
            if (!shift && inBattlefield && Input.GetKeyDown(KeyCode.Home))
            {
                RefreshIfDirty();
                ClearEventSystemSelection();
                FirstCard();
                return true;
            }

            if (!shift && inBattlefield && Input.GetKeyDown(KeyCode.End))
            {
                RefreshIfDirty();
                ClearEventSystemSelection();
                LastCard();
                return true;
            }

            // Ctrl+Enter on a collapsed multi-card stack -> "click the count badge"
            // (select whole stack; only meaningful during Declare Attackers/Blockers).
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (inBattlefield && ctrl
                && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                RefreshIfDirty();
                ActivateStackOnCurrentCard();
                return true;
            }

            // Enter to activate card (only when in battlefield)
            // Note: HotHighlightNavigator handles Enter for targets - this is for non-highlighted cards
            if (inBattlefield && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                RefreshIfDirty();
                ActivateCurrentCard();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ctrl+Enter handler: invokes the game's stack-count badge click on the focused
        /// card's stack. Only meaningful during Declare Attackers/Blockers — for other
        /// workflows the game itself returns CanClickStack=false, so we announce
        /// "not in combat" instead of firing the native invalid sfx.
        /// For single-card entries (no stack), falls back to a normal Enter click.
        /// </summary>
        private void ActivateStackOnCurrentCard()
        {
            var card = GetCurrentCard();
            if (card == null)
            {
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.High);
                return;
            }

            uint id = CardStateProvider.GetCardInstanceId(card);
            bool isStack = id != 0
                && AccessibleArenaMod.Instance?.Settings?.BattlefieldStacking == true
                && BattlefieldStackProvider.TryGetStackSize(id, out int size) && size > 1;

            if (!isStack)
            {
                // No stack collapse in effect — Ctrl+Enter behaves like plain Enter.
                ActivateCurrentCard();
                return;
            }

            var result = StackInteractionBridge.TrySelectStack(id);
            if (result == StackInteractionBridge.Result.Unavailable)
            {
                _announcer.Announce(Strings.StackSelectUnavailable, AnnouncementPriority.High);
                return;
            }
            if (result != StackInteractionBridge.Result.Success)
            {
                // Reflection/setup failure — fall back to the normal per-card click.
                Log.Warn("BattlefieldNavigator",
                    $"Stack click bridge failed ({result}); falling back to single click");
                ActivateCurrentCard();
                return;
            }

            BattlefieldStackProvider.TryGetStackSize(id, out int n);
            string name = CardDetector.GetCardName(card);
            _announcer.Announce(Strings.StackSelectSent(n, name), AnnouncementPriority.High);
        }

        /// <summary>
        /// Discovers all battlefield cards and categorizes them into rows.
        /// Uses DuelHolderCache for cached holder lookup instead of full scene scan.
        /// </summary>
        public void DiscoverAndCategorizeCards()
        {
            // Clear existing rows
            foreach (var row in _rows.Values)
            {
                row.Clear();
            }

            // Find battlefield zone holder via shared cache
            var battlefieldHolder = DuelHolderCache.GetHolder("BattlefieldCardHolder");

            if (battlefieldHolder == null)
            {
                Log.Msg("BattlefieldNavigator", "Battlefield holder not found");
                return;
            }

            // Find all cards in battlefield
            // Use HashSet for O(1) ancestor lookup instead of O(n) IsChildOf checks per card.
            // GetComponentsInChildren is depth-first, so parent cards are always found before children.
            var cards = new List<GameObject>();
            var foundCardIds = new HashSet<int>();
            foreach (Transform child in battlefieldHolder.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                var go = child.gameObject;
                if (CardDetector.IsCard(go))
                {
                    // Walk up parent chain to check if this is a child of an already-found card
                    bool isChildOfExistingCard = false;
                    Transform ancestor = go.transform.parent;
                    while (ancestor != null && ancestor != battlefieldHolder.transform)
                    {
                        if (foundCardIds.Contains(ancestor.gameObject.GetInstanceID()))
                        {
                            isChildOfExistingCard = true;
                            break;
                        }
                        ancestor = ancestor.parent;
                    }

                    if (!isChildOfExistingCard)
                    {
                        foundCardIds.Add(go.GetInstanceID());
                        cards.Add(go);
                    }
                }
            }

            // When a browser is active, combat participants may be reparented to browser holders.
            // Include cards from browser holders whose game model ZoneType is still "Battlefield".
            if (BrowserNavigator.IsActive)
            {
                var browserHolder = BrowserDetector.FindActiveGameObject(BrowserDetector.HolderDefault);
                if (browserHolder != null)
                {
                    foreach (Transform child in browserHolder.GetComponentsInChildren<Transform>(true))
                    {
                        if (child == null || !child.gameObject.activeInHierarchy)
                            continue;

                        var go = child.gameObject;
                        if (!CardDetector.IsCard(go)) continue;
                        if (foundCardIds.Contains(go.GetInstanceID())) continue;

                        // Only include cards whose model says they're on the battlefield
                        string zone = CardStateProvider.GetCardZoneTypeName(go);
                        if (zone == "Battlefield")
                        {
                            foundCardIds.Add(go.GetInstanceID());
                            cards.Add(go);
                        }
                    }
                }
            }

            // When BattlefieldStacking is on, collapse stacks of identical tokens/cards:
            // keep the StackParent, drop the stacked-behind copies from the flat list.
            bool stacking = AccessibleArenaMod.Instance?.Settings?.BattlefieldStacking == true;
            if (stacking)
            {
                BattlefieldStackProvider.BuildStackIndex();
                var childIds = BattlefieldStackProvider.StackChildIds;
                if (childIds.Count > 0)
                {
                    cards.RemoveAll(go =>
                    {
                        uint id = CardStateProvider.GetCardInstanceId(go);
                        return id != 0 && childIds.Contains(id);
                    });
                }
            }

            // Categorize each card into appropriate row
            foreach (var card in cards)
            {
                var row = CategorizeCard(card);
                _rows[row].Add(card);
            }

            // Sort each row by x position (left to right)
            foreach (var row in _rows.Values)
            {
                row.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            }

            // Log summary
            Log.Msg("BattlefieldNavigator", "Categorized cards:");
            foreach (var kvp in _rows)
            {
                if (kvp.Value.Count > 0)
                {
                    MelonLogger.Msg($"  {kvp.Key}: {kvp.Value.Count} cards");
                }
            }

            if (stacking)
            {
                BattlefieldStackProvider.LogStackStructure();
            }
        }

        /// <summary>
        /// Determines which row a card belongs to based on type and ownership.
        /// Uses CardDetector.GetCardCategory for efficient single-lookup detection.
        /// </summary>
        private BattlefieldRow CategorizeCard(GameObject card)
        {
            var (isCreature, isLand, isOpponent) = CardDetector.GetCardCategory(card);

            // Determine row based on type and ownership
            BattlefieldRow row;
            if (isOpponent)
            {
                if (isLand) row = BattlefieldRow.EnemyLands;
                else if (isCreature) row = BattlefieldRow.EnemyCreatures;
                else row = BattlefieldRow.EnemyNonCreatures;
            }
            else
            {
                if (isLand) row = BattlefieldRow.PlayerLands;
                else if (isCreature) row = BattlefieldRow.PlayerCreatures;
                else row = BattlefieldRow.PlayerNonCreatures;
            }

            return row;
        }

        /// <summary>
        /// Navigates to a specific row and announces it.
        /// </summary>
        public void NavigateToRow(BattlefieldRow row)
        {
            DiscoverAndCategorizeCards();

            var cards = _rows[row];
            if (cards.Count == 0)
            {
                _announcer.Announce(Strings.RowEmptyShort(GetRowName(row)), AnnouncementPriority.High);
                // Still set the row so Shift+Up/Down work from here
                _currentRow = row;
                _currentIndex = 0;
                return;
            }

            _currentRow = row;
            _currentIndex = 0;

            // High priority: user explicitly pressed a row shortcut — always re-announce
            AnnounceCurrentCard(includeRowName: true, priority: AnnouncementPriority.High);
        }

        /// <summary>
        /// Navigates to a specific card on the battlefield.
        /// Finds the card's row and index, syncs state, and announces.
        /// Used by HotHighlightNavigator to sync battlefield position on Tab.
        /// </summary>
        /// <param name="card">The card GameObject to navigate to</param>
        /// <param name="announceRowChange">If true, includes row name in announcement (zone change)</param>
        /// <returns>True if the card was found and navigated to</returns>
        public bool NavigateToSpecificCard(GameObject card, bool announceRowChange)
        {
            if (card == null) return false;

            DiscoverAndCategorizeCards();

            // Find which row contains this card
            foreach (var kvp in _rows)
            {
                int index = kvp.Value.IndexOf(card);
                if (index >= 0)
                {
                    bool rowChanged = _currentRow != kvp.Key;
                    _currentRow = kvp.Key;
                    _currentIndex = index;

                    _zoneNavigator.SetCurrentZone(ZoneType.Battlefield, "BattlefieldNavigator");
                    // Use High priority to bypass duplicate check - user explicitly pressed Tab
                    AnnounceCurrentCard(includeRowName: announceRowChange || rowChanged, priority: AnnouncementPriority.High);
                    return true;
                }
            }

            Log.Warn("BattlefieldNavigator", $"NavigateToSpecificCard: card not found in any row");
            return false;
        }

        private void NextRow() => MoveRow(1);
        private void PreviousRow() => MoveRow(-1);

        /// <summary>
        /// Moves to the next/previous non-empty row.
        /// direction=1 towards player side (Shift+Down), -1 towards enemy side (Shift+Up).
        /// </summary>
        private void MoveRow(int direction)
        {
            DiscoverAndCategorizeCards();

            int currentIdx = Array.IndexOf(RowOrder, _currentRow);

            for (int i = currentIdx + direction; i >= 0 && i < RowOrder.Length; i += direction)
            {
                if (_rows[RowOrder[i]].Count > 0)
                {
                    _currentRow = RowOrder[i];
                    _currentIndex = 0;
                    AnnounceCurrentCard(includeRowName: true);
                    return;
                }
            }

            _announcer.AnnounceInterruptVerbose(direction > 0 ? Strings.EndOfBattlefield : Strings.BeginningOfBattlefield);
        }

        /// <summary>
        /// Moves to the next card in the current row.
        /// </summary>
        private void NextCard()
        {
            var cards = _rows[_currentRow];
            if (cards.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.RowEmpty(GetRowName(_currentRow)));
                return;
            }

            if (_currentIndex < cards.Count - 1)
            {
                _currentIndex++;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterruptVerbose(Strings.EndOfRow);
            }
        }

        /// <summary>
        /// Moves to the previous card in the current row.
        /// </summary>
        private void PreviousCard()
        {
            var cards = _rows[_currentRow];
            if (cards.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.RowEmpty(GetRowName(_currentRow)));
                return;
            }

            if (_currentIndex > 0)
            {
                _currentIndex--;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterruptVerbose(Strings.BeginningOfRow);
            }
        }

        /// <summary>
        /// Jumps to the first card in the current row.
        /// </summary>
        private void FirstCard()
        {
            var cards = _rows[_currentRow];
            if (cards.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.RowEmpty(GetRowName(_currentRow)));
                return;
            }

            if (_currentIndex == 0)
            {
                _announcer.AnnounceInterruptVerbose(Strings.BeginningOfRow);
                return;
            }

            _currentIndex = 0;
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Jumps to the last card in the current row.
        /// </summary>
        private void LastCard()
        {
            var cards = _rows[_currentRow];
            if (cards.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.RowEmpty(GetRowName(_currentRow)));
                return;
            }

            int lastIndex = cards.Count - 1;
            if (_currentIndex == lastIndex)
            {
                _announcer.AnnounceInterruptVerbose(Strings.EndOfRow);
                return;
            }

            _currentIndex = lastIndex;
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Gets the current card in the current row.
        /// </summary>
        public GameObject GetCurrentCard()
        {
            var cards = _rows[_currentRow];
            if (_currentIndex >= cards.Count) return null;
            return cards[_currentIndex];
        }

        /// <summary>
        /// Activates (clicks) the current card.
        /// Allows activation if:
        /// Sends a click to the game - the game decides what happens.
        /// </summary>
        private void ActivateCurrentCard()
        {
            var card = GetCurrentCard();

            if (card == null)
            {
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.High);
                return;
            }

            string cardName = CardDetector.GetCardName(card);
            string stateBefore = GetCardStateSnapshot(card);
            Log.Msg("BattlefieldNavigator", $"Clicking card: {cardName} (state: {stateBefore})");

            // Use the card's actual screen position to avoid hitting wrong overlapping token
            if (Camera.main != null)
            {
                Vector2 cardScreenPos = Camera.main.WorldToScreenPoint(card.transform.position);
                UIActivator.SimulatePointerClick(card, cardScreenPos);
            }
            else
            {
                UIActivator.SimulatePointerClick(card);
            }

            // Start watching this card for state change (checked per-frame in HandleInput)
            _watchedCard = card;
            _watchedStateBefore = stateBefore;
            _watchStartTime = Time.time;
        }

        /// <summary>
        /// Per-frame check: after Enter click on a card, watches for state change.
        /// Announces only the new state text (no card name - user already knows what card).
        /// Stops watching after timeout or state change detected.
        /// </summary>
        private void CheckWatchedCardState()
        {
            if (_watchedCard == null) return;

            // Timeout
            if (Time.time - _watchStartTime > WatchTimeoutSeconds)
            {
                Log.Msg("BattlefieldNavigator", "State watch timed out");
                _watchedCard = null;
                return;
            }

            // Throttle: only check every ~100ms instead of every frame
            if (Time.time - _lastWatchCheckTime < WatchCheckIntervalSeconds)
                return;
            _lastWatchCheckTime = Time.time;

            string stateAfter = GetCardStateSnapshot(_watchedCard);
            if (stateAfter != _watchedStateBefore)
            {
                Log.Msg("BattlefieldNavigator", $"State changed: '{_watchedStateBefore}' -> '{stateAfter}'");
                if (!string.IsNullOrEmpty(stateAfter))
                {
                    // Announce just the state (trim leading ", ")
                    string announcement = stateAfter.StartsWith(", ") ? stateAfter.Substring(2) : stateAfter;
                    string before = _watchedStateBefore.StartsWith(", ") ? _watchedStateBefore.Substring(2) : _watchedStateBefore;

                    // If the new state extends the old state, only announce the new part
                    // e.g. "attacking" -> "attacking, blocked by Angel" announces just "blocked by Angel"
                    if (!string.IsNullOrEmpty(before) && announcement.StartsWith(before + ", "))
                        announcement = announcement.Substring(before.Length + 2);

                    _announcer.Announce(announcement, AnnouncementPriority.High);
                }
                var clicked = _watchedCard;
                _watchedCard = null;
                TryAdvanceToSameNameSibling(clicked);
            }
        }

        /// <summary>
        /// After an Enter click that produced a state change on a stacked card, move
        /// focus to the next same-name sibling in the current row so repeated Enter
        /// targets the next copy instead of toggling the just-clicked card's state
        /// back off. No-op when stacking is disabled or no sibling exists.
        /// </summary>
        private void TryAdvanceToSameNameSibling(GameObject clickedCard)
        {
            if (clickedCard == null) return;
            if (AccessibleArenaMod.Instance?.Settings?.BattlefieldStacking != true) return;

            string clickedName = CardDetector.GetCardName(clickedCard);
            if (string.IsNullOrEmpty(clickedName)) return;
            uint clickedId = CardStateProvider.GetCardInstanceId(clickedCard);

            DiscoverAndCategorizeCards();
            var rowCards = _rows[_currentRow];
            for (int i = 0; i < rowCards.Count; i++)
            {
                var go = rowCards[i];
                if (go == null) continue;
                uint id = CardStateProvider.GetCardInstanceId(go);
                if (id == clickedId) continue;
                if (CardDetector.GetCardName(go) != clickedName) continue;
                _currentIndex = i;
                AnnounceCurrentCard(priority: AnnouncementPriority.High);
                return;
            }
        }

        /// <summary>
        /// Builds a combined state snapshot of a card: combat state + selection state.
        /// Used for before/after comparison to detect and announce state changes.
        /// We include selection state alongside combat state so targeting flows are
        /// caught even when combat state is unchanged (e.g. tapped card targeted by
        /// an untap ability — tap state stays "getappt", only selection flips).
        /// Drops the redundant selection fragment when combat state already names it
        /// (e.g. "zum Blocken ausgewählt") to avoid doubled announcements.
        /// </summary>
        private string GetCardStateSnapshot(GameObject card)
        {
            string combat = _combatNavigator?.GetCombatStateText(card) ?? "";
            string sel = GetSelectionState(card);
            if (!string.IsNullOrEmpty(combat) && !string.IsNullOrEmpty(sel)
                && !string.IsNullOrEmpty(Strings.Selected)
                && combat.Contains(Strings.Selected))
            {
                sel = "";
            }
            return combat + sel;
        }

        /// <summary>
        /// Checks if a card has selection indicators (sacrifice, exile, choose targets, etc.).
        /// Looks for active children with "select", "chosen", or "pick" in the name.
        /// </summary>
        private string GetSelectionState(GameObject card)
        {
            if (card == null) return "";

            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;

                string childName = child.name.ToLower();
                if (childName.Contains("select") || childName.Contains("chosen") || childName.Contains("pick"))
                    return $", {Strings.Selected}";
            }

            return "";
        }

        /// <summary>
        /// Announces the current card with position, combat state, and attachments.
        /// </summary>
        private void AnnounceCurrentCard(bool includeRowName = false, AnnouncementPriority priority = AnnouncementPriority.Normal, bool isRowSwitch = false)
        {
            var cards = _rows[_currentRow];
            if (_currentIndex >= cards.Count) return;

            var card = cards[_currentIndex];
            string cardName = CardDetector.GetCardName(card);

            // When stacking is on, collapsed stacks get an "N " prefix (e.g. "5 Tentakel").
            // Prefix chosen over suffix to avoid collision with the position counter (",X von Y").
            if (AccessibleArenaMod.Instance?.Settings?.BattlefieldStacking == true)
            {
                uint id = CardStateProvider.GetCardInstanceId(card);
                if (id != 0 && BattlefieldStackProvider.TryGetStackSize(id, out int stackSize) && stackSize > 1)
                    cardName = $"{stackSize} {cardName}";
            }

            int position = _currentIndex + 1;
            int total = cards.Count;

            // Add combat state if available
            string combatState = _combatNavigator?.GetCombatStateText(card) ?? "";

            // Add selection state so the user can tell selected vs unselected copies apart
            // when a stack has been split by targeting (e.g. 1 of 2 Kraken selected for untap).
            // Skip if combat state already names the selection (e.g. "zum Blocken ausgewählt").
            string selectionState = GetSelectionState(card);
            if (!string.IsNullOrEmpty(combatState) && !string.IsNullOrEmpty(selectionState)
                && !string.IsNullOrEmpty(Strings.Selected)
                && combatState.Contains(Strings.Selected))
            {
                selectionState = "";
            }

            // Add attachment info (enchantments, equipment attached to this card)
            string attachmentText = CardStateProvider.GetAttachmentText(card);

            // Add targeting info (what this card targets / what targets it)
            string targetingText = CardStateProvider.GetTargetingText(card);

            // For non-creature rows, include the primary card type
            string typeLabel = "";
            if (_currentRow == BattlefieldRow.PlayerNonCreatures || _currentRow == BattlefieldRow.EnemyNonCreatures)
            {
                string t = CardStateProvider.GetNonCreatureTypeLabel(card);
                if (t != null) typeLabel = $", {t}";
            }

            string prefix = "";
            if (includeRowName)
            {
                string rowName = GetRowName(_currentRow);
                bool verbose = AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements != false;
                prefix = (!isRowSwitch || verbose) ? $"{rowName}, " : "";
            }
            string pos = Strings.PositionOf(position, total, force: true);
            _announcer.Announce($"{prefix}{cardName}{typeLabel}{combatState}{selectionState}{attachmentText}{targetingText}" + (pos != "" ? $", {pos}" : ""), priority);

            // Set EventSystem focus to the card - this ensures other navigators
            // (like PlayerPortrait) detect the focus change and exit their modes
            if (card != null)
            {
                ZoneNavigator.SetFocusedGameObject(card, "BattlefieldNavigator");
            }

            // Prepare card info navigation (for Arrow Up/Down detail viewing)
            var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNavigator != null && CardDetector.IsCard(card))
            {
                cardNavigator.PrepareForCard(card, ZoneType.Battlefield);
            }
        }

        private void ClearEventSystemSelection() => ZoneNavigator.ClearFocus("BattlefieldNavigator.Clear");

        /// <summary>
        /// Builds a land summary for the given land row: total count + untapped lands grouped by name.
        /// Example: "7 lands, 2 Islands, 1 Mountain, 1 Azorius Gate untapped"
        /// </summary>
        public string GetLandSummary(BattlefieldRow landRow)
        {
            DiscoverAndCategorizeCards();

            var cards = _rows[landRow];

            // Group untapped lands by name, preserving order of first appearance.
            // Each row entry represents a stack of N identical lands (all same tap state
            // — IsSame requires IsTapped to match), so we count the stack size, not the
            // entry, when stacking is enabled.
            var untappedGroups = new List<KeyValuePair<string, int>>();
            var untappedCounts = new Dictionary<string, int>();
            int tappedCount = 0;
            int total = 0;

            foreach (var card in cards)
            {
                int n = GetStackSizeForCard(card);
                total += n;

                bool isTapped = CardStateProvider.GetIsTappedFromCard(card);
                if (isTapped)
                {
                    tappedCount += n;
                    continue;
                }

                string name = CardDetector.GetCardName(card);
                if (untappedCounts.ContainsKey(name))
                {
                    untappedCounts[name] += n;
                }
                else
                {
                    untappedCounts[name] = n;
                    untappedGroups.Add(new KeyValuePair<string, int>(name, 0)); // placeholder
                }
            }

            if (total == 0)
                return Strings.LandSummaryEmpty(GetRowName(landRow));

            // Build the untapped part: "2 Islands, 1 Mountain"
            var parts = new List<string>();
            foreach (var kvp in untappedGroups)
            {
                int count = untappedCounts[kvp.Key];
                parts.Add($"{count} {kvp.Key}");
            }

            string totalPart = Strings.LandSummaryTotal(total);

            if (parts.Count == 0)
                return Strings.LandSummaryAllTapped(totalPart);

            string untappedPart = string.Join(", ", parts);

            if (tappedCount == 0)
                return Strings.LandSummaryAllUntapped(totalPart, untappedPart);

            return Strings.LandSummaryMixed(totalPart, untappedPart);
        }

        /// <summary>
        /// Returns how many physical cards a row entry represents. With BattlefieldStacking
        /// off or for single-card entries, returns 1. With stacking on and a collapsed stack,
        /// returns the stack's AllCards count.
        /// </summary>
        private int GetStackSizeForCard(GameObject card)
        {
            if (card == null) return 0;
            if (AccessibleArenaMod.Instance?.Settings?.BattlefieldStacking != true) return 1;
            uint id = CardStateProvider.GetCardInstanceId(card);
            if (id != 0 && BattlefieldStackProvider.TryGetStackSize(id, out int n) && n > 1)
                return n;
            return 1;
        }

        /// <summary>
        /// Gets the display name for a row.
        /// </summary>
        private string GetRowName(BattlefieldRow row)
        {
            return Strings.GetRowName(row);
        }
    }

    /// <summary>
    /// Battlefield row categories organized by card type and ownership.
    /// Order from top (enemy) to bottom (player) of the screen.
    /// </summary>
    public enum BattlefieldRow
    {
        EnemyLands,           // Shift+A
        EnemyNonCreatures,    // Shift+R - artifacts, enchantments, planeswalkers
        EnemyCreatures,       // Shift+B
        PlayerCreatures,      // B
        PlayerNonCreatures,   // R - artifacts, enchantments, planeswalkers
        PlayerLands           // A
    }
}
