using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
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

        // Reference to TargetNavigator for targeting mode row navigation
        private TargetNavigator _targetNavigator;

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
        /// Sets the TargetNavigator reference for targeting mode row navigation.
        /// </summary>
        public void SetTargetNavigator(TargetNavigator navigator)
        {
            _targetNavigator = navigator;
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
        /// Deactivates battlefield navigation.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            foreach (var row in _rows.Values)
            {
                row.Clear();
            }
        }

        /// <summary>
        /// Handles battlefield navigation input.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Row shortcuts: A for lands
            if (Input.GetKeyDown(KeyCode.A))
            {
                _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
                if (shift)
                    NavigateToRow(BattlefieldRow.EnemyLands);
                else
                    NavigateToRow(BattlefieldRow.PlayerLands);
                return true;
            }

            // Row shortcuts: R for non-creatures (artifacts, enchantments, planeswalkers)
            if (Input.GetKeyDown(KeyCode.R))
            {
                _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
                if (shift)
                    NavigateToRow(BattlefieldRow.EnemyNonCreatures);
                else
                    NavigateToRow(BattlefieldRow.PlayerNonCreatures);
                return true;
            }

            // Row shortcuts: B for creatures
            if (Input.GetKeyDown(KeyCode.B))
            {
                _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
                if (shift)
                    NavigateToRow(BattlefieldRow.EnemyCreatures);
                else
                    NavigateToRow(BattlefieldRow.PlayerCreatures);
                return true;
            }

            // Check if in targeting mode - special handling for battlefield inspection
            bool isTargeting = _targetNavigator?.IsTargeting == true;
            bool inBattlefield = _zoneNavigator.CurrentZone == ZoneType.Battlefield;

            // In targeting mode: Shift+Up/Down for row navigation, but Left/Right/Enter are NOT captured
            if (isTargeting)
            {
                if (shift && Input.GetKeyDown(KeyCode.UpArrow))
                {
                    // If not in battlefield yet, default to PlayerCreatures
                    if (!inBattlefield)
                    {
                        _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
                        _currentRow = BattlefieldRow.PlayerCreatures;
                    }
                    PreviousRow();
                    return true;
                }

                if (shift && Input.GetKeyDown(KeyCode.DownArrow))
                {
                    // If not in battlefield yet, default to PlayerCreatures
                    if (!inBattlefield)
                    {
                        _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
                        _currentRow = BattlefieldRow.PlayerCreatures;
                    }
                    NextRow();
                    return true;
                }

                // Don't capture Left/Right/Enter during targeting - let other zones handle them
                return false;
            }

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

            // Enter to activate card (only when in battlefield)
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
            MelonLogger.Msg($"[BattlefieldNavigator] Activating card: {cardName}");

            var result = UIActivator.SimulatePointerClick(card);

            if (!result.Success)
            {
                _announcer.Announce(Strings.CannotActivate(cardName), AnnouncementPriority.High);
                MelonLogger.Msg($"[BattlefieldNavigator] Card activation failed: {result.Message}");
            }
        }

        /// <summary>
        /// Announces the current card with position and combat state.
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

            _announcer.Announce($"{cardName}{combatState}, {position} of {total}", AnnouncementPriority.Normal);

            // Prepare card info navigation (for Arrow Up/Down detail viewing)
            var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
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
                eventSystem.SetSelectedGameObject(null);
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
