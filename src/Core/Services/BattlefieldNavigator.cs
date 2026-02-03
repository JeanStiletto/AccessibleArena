using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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

        // Reference to CombatNavigator for attacker/blocker state announcements
        private CombatNavigator _combatNavigator;

        // DEPRECATED: TargetNavigator was used to check IsTargeting for row navigation behavior
        // Now HotHighlightNavigator handles targeting - battlefield navigation is always available
        // private TargetNavigator _targetNavigator;

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
                        _announcer.Announce($"Mana: {mana}", AnnouncementPriority.High);
                    }
                    NavigateToRow(BattlefieldRow.PlayerLands);
                }
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
                return true;
            }

            // DEPRECATED: Old targeting mode special handling removed
            // HotHighlightNavigator handles targeting via Tab - battlefield navigation always works
            // bool isTargeting = _targetNavigator?.IsTargeting == true;
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
                ClearEventSystemSelection();
                PreviousCard();
                return true;
            }

            if (!shift && inBattlefield && Input.GetKeyDown(KeyCode.RightArrow))
            {
                ClearEventSystemSelection();
                NextCard();
                return true;
            }

            // Plain Up/Down (without shift) - re-announce current card or empty row
            // This prevents fall-through to base menu navigation when in battlefield
            if (!shift && inBattlefield && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
            {
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
                ClearEventSystemSelection();
                FirstCard();
                return true;
            }

            if (!shift && inBattlefield && Input.GetKeyDown(KeyCode.End))
            {
                ClearEventSystemSelection();
                LastCard();
                return true;
            }

            // Enter to activate card (only when in battlefield)
            // Note: HotHighlightNavigator handles Enter for targets - this is for non-highlighted cards
            if (inBattlefield && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                ActivateCurrentCard();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Discovers all battlefield cards and categorizes them into rows.
        /// </summary>
        public void DiscoverAndCategorizeCards()
        {
            // Clear existing rows
            foreach (var row in _rows.Values)
            {
                row.Clear();
            }

            // Find battlefield zone holder
            GameObject battlefieldHolder = null;
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                if (go.name.Contains("BattlefieldCardHolder"))
                {
                    battlefieldHolder = go;
                    break;
                }
            }

            if (battlefieldHolder == null)
            {
                MelonLogger.Msg("[BattlefieldNavigator] Battlefield holder not found");
                return;
            }

            // Find all cards in battlefield
            var cards = new List<GameObject>();
            foreach (Transform child in battlefieldHolder.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                var go = child.gameObject;
                if (CardDetector.IsCard(go))
                {
                    // Avoid duplicates (parent-child relationships)
                    if (!cards.Any(c => c.transform.IsChildOf(go.transform) || go.transform.IsChildOf(c.transform)))
                    {
                        cards.Add(go);
                    }
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
            MelonLogger.Msg("[BattlefieldNavigator] Categorized cards:");
            foreach (var kvp in _rows)
            {
                if (kvp.Value.Count > 0)
                {
                    MelonLogger.Msg($"  {kvp.Key}: {kvp.Value.Count} cards");
                }
            }
        }

        /// <summary>
        /// Determines which row a card belongs to based on type and ownership.
        /// Uses CardDetector.GetCardCategory for efficient single-lookup detection.
        /// </summary>
        private BattlefieldRow CategorizeCard(GameObject card)
        {
            string cardName = CardDetector.GetCardName(card);
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

            MelonLogger.Msg($"[BattlefieldNavigator] Card: {cardName}, IsCreature: {isCreature}, IsLand: {isLand}, IsOpponent: {isOpponent} -> {row}");
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

            _announcer.Announce(Strings.RowWithCount(GetRowName(row), cards.Count), AnnouncementPriority.High);
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Moves to the next row (towards player side / Shift+Down).
        /// Skips empty rows.
        /// </summary>
        private void NextRow()
        {
            DiscoverAndCategorizeCards();

            int currentIdx = Array.IndexOf(RowOrder, _currentRow);

            // Find next non-empty row
            for (int i = currentIdx + 1; i < RowOrder.Length; i++)
            {
                if (_rows[RowOrder[i]].Count > 0)
                {
                    _currentRow = RowOrder[i];
                    _currentIndex = 0;
                    _announcer.Announce(Strings.RowWithCount(GetRowName(_currentRow), _rows[_currentRow].Count), AnnouncementPriority.High);
                    AnnounceCurrentCard();
                    return;
                }
            }

            _announcer.AnnounceInterrupt(Strings.EndOfBattlefield);
        }

        /// <summary>
        /// Moves to the previous row (towards enemy side / Shift+Up).
        /// Skips empty rows.
        /// </summary>
        private void PreviousRow()
        {
            DiscoverAndCategorizeCards();

            int currentIdx = Array.IndexOf(RowOrder, _currentRow);

            // Find previous non-empty row
            for (int i = currentIdx - 1; i >= 0; i--)
            {
                if (_rows[RowOrder[i]].Count > 0)
                {
                    _currentRow = RowOrder[i];
                    _currentIndex = 0;
                    _announcer.Announce(Strings.RowWithCount(GetRowName(_currentRow), _rows[_currentRow].Count), AnnouncementPriority.High);
                    AnnounceCurrentCard();
                    return;
                }
            }

            _announcer.AnnounceInterrupt(Strings.BeginningOfBattlefield);
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
                _announcer.AnnounceInterrupt(Strings.EndOfRow);
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
                _announcer.AnnounceInterrupt(Strings.BeginningOfRow);
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
                _announcer.AnnounceInterrupt(Strings.BeginningOfRow);
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
                _announcer.AnnounceInterrupt(Strings.EndOfRow);
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
            MelonLogger.Msg($"[BattlefieldNavigator] Clicking card: {cardName}");

            UIActivator.SimulatePointerClick(card);

            // DIAGNOSTIC: Log button state after clicking (for activated ability debugging)
            MelonCoroutines.Start(LogButtonStateAfterClick(cardName));
        }

        /// <summary>
        /// DIAGNOSTIC: Logs button state after a short delay to capture ability activation mode.
        /// </summary>
        private System.Collections.IEnumerator LogButtonStateAfterClick(string cardName)
        {
            // Wait for game UI to update
            yield return new UnityEngine.WaitForSeconds(0.3f);
            MelonLogger.Msg($"[BattlefieldNavigator] === BUTTON STATE AFTER CLICKING {cardName} ===");
            DuelAnnouncer.LogAllPromptButtons();
        }

        /// <summary>
        /// Announces the current card with position, combat state, and attachments.
        /// </summary>
        private void AnnounceCurrentCard()
        {
            var cards = _rows[_currentRow];
            if (_currentIndex >= cards.Count) return;

            var card = cards[_currentIndex];
            string cardName = CardDetector.GetCardName(card);
            int position = _currentIndex + 1;
            int total = cards.Count;

            // Add combat state if available
            string combatState = _combatNavigator?.GetCombatStateText(card) ?? "";

            // Add attachment info (enchantments, equipment attached to this card)
            string attachmentText = CardModelProvider.GetAttachmentText(card);

            _announcer.Announce($"{cardName}{combatState}{attachmentText}, {position} of {total}", AnnouncementPriority.Normal);

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

        /// <summary>
        /// Clears the EventSystem selection to prevent UI conflicts.
        /// </summary>
        private void ClearEventSystemSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                ZoneNavigator.SetFocusedGameObject(null, "BattlefieldNavigator.Clear");
            }
        }

        /// <summary>
        /// Gets the display name for a row.
        /// </summary>
        private string GetRowName(BattlefieldRow row)
        {
            return row switch
            {
                BattlefieldRow.EnemyLands => "Enemy lands",
                BattlefieldRow.EnemyNonCreatures => "Enemy non-creatures",
                BattlefieldRow.EnemyCreatures => "Enemy creatures",
                BattlefieldRow.PlayerCreatures => "Your creatures",
                BattlefieldRow.PlayerNonCreatures => "Your non-creatures",
                BattlefieldRow.PlayerLands => "Your lands",
                _ => row.ToString()
            };
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
