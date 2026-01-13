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
    /// Navigation: Left/Right arrows within row, Alt+Up/Down to switch rows.
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

        // Row order from top (enemy side) to bottom (player side) for Alt+Up/Down navigation
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
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

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

            // In targeting mode: Alt+Up/Down for row navigation, but Left/Right/Enter are NOT captured
            if (isTargeting)
            {
                if (alt && Input.GetKeyDown(KeyCode.UpArrow))
                {
                    // If not in battlefield yet, default to PlayerCreatures
                    if (!inBattlefield)
                    {
                        _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
                        _currentRow = BattlefieldRow.PlayerCreatures;
                        DiscoverAndCategorizeCards();
                    }
                    PreviousRow();
                    return true;
                }

                if (alt && Input.GetKeyDown(KeyCode.DownArrow))
                {
                    // If not in battlefield yet, default to PlayerCreatures
                    if (!inBattlefield)
                    {
                        _zoneNavigator.SetCurrentZone(ZoneType.Battlefield);
                        _currentRow = BattlefieldRow.PlayerCreatures;
                        DiscoverAndCategorizeCards();
                    }
                    NextRow();
                    return true;
                }

                // Don't capture Left/Right/Enter during targeting - let other zones handle them
                return false;
            }

            // Row switching with Alt+Up/Down (only when already in battlefield)
            if (inBattlefield && alt && Input.GetKeyDown(KeyCode.UpArrow))
            {
                PreviousRow();
                return true;
            }

            if (inBattlefield && alt && Input.GetKeyDown(KeyCode.DownArrow))
            {
                NextRow();
                return true;
            }

            if (!alt && inBattlefield && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ClearEventSystemSelection();
                PreviousCard();
                return true;
            }

            if (!alt && inBattlefield && Input.GetKeyDown(KeyCode.RightArrow))
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
        /// Uses the game's CardData Model for accurate type detection.
        /// </summary>
        private BattlefieldRow CategorizeCard(GameObject card)
        {
            string cardName = CardDetector.GetCardName(card);
            bool isOpponent = false;
            bool isCreature = false;
            bool isLand = false;

            // Try to get card data from DuelScene_CDC component using shared utility
            var cdcComponent = CardDetector.GetDuelSceneCDC(card);
            var model = CardDetector.GetCardModel(cdcComponent);
            if (model != null)
            {
                try
                {
                    var modelType = model.GetType();

                    // Get ownership from ControllerNum (returns "Opponent" or "Player")
                    var controllerProp = modelType.GetProperty("ControllerNum");
                    if (controllerProp != null)
                    {
                        var controller = controllerProp.GetValue(model);
                        isOpponent = controller?.ToString() == "Opponent";
                    }

                    // Check IsBasicLand and IsLandButNotBasic for land detection
                    var isBasicLandProp = modelType.GetProperty("IsBasicLand");
                    var isLandNotBasicProp = modelType.GetProperty("IsLandButNotBasic");
                    if (isBasicLandProp != null)
                    {
                        isLand = (bool)isBasicLandProp.GetValue(model);
                    }
                    if (!isLand && isLandNotBasicProp != null)
                    {
                        isLand = (bool)isLandNotBasicProp.GetValue(model);
                    }

                    // Get CardTypes list to check for Creature
                    var cardTypesProp = modelType.GetProperty("CardTypes");
                    if (cardTypesProp != null)
                    {
                        var cardTypes = cardTypesProp.GetValue(model);
                        if (cardTypes != null)
                        {
                            // Enumerate the list to check for creature/land type
                            var enumerable = cardTypes as System.Collections.IEnumerable;
                            if (enumerable != null)
                            {
                                foreach (var cardType in enumerable)
                                {
                                    string typeStr = cardType.ToString();
                                    if (typeStr == "Creature" || typeStr.Contains("Creature"))
                                    {
                                        isCreature = true;
                                    }
                                    if (typeStr == "Land" || typeStr.Contains("Land"))
                                    {
                                        isLand = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[BattlefieldNavigator] Error reading card data: {ex.Message}");
                }
            }
            else
            {
                // Fallback to screen position for ownership
                isOpponent = IsOpponentCard(card);
            }

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
        /// Determines if a card belongs to the opponent based on parent hierarchy or screen position.
        /// </summary>
        private bool IsOpponentCard(GameObject cardObj)
        {
            Transform current = cardObj.transform;
            while (current != null)
            {
                string name = current.name.ToLower();
                if (name.Contains("opponent"))
                    return true;
                if (name.Contains("local") || name.Contains("player1"))
                    return false;

                current = current.parent;
            }

            // Fallback: check screen position (top 60% = opponent)
            Vector3 screenPos = Camera.main?.WorldToScreenPoint(cardObj.transform.position) ?? Vector3.zero;
            if (screenPos != Vector3.zero)
            {
                return screenPos.y > Screen.height * 0.6f;
            }

            return false;
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
                _announcer.Announce($"{GetRowName(row)}, empty", AnnouncementPriority.High);
                // Still set the row so Alt+Up/Down work from here
                _currentRow = row;
                _currentIndex = 0;
                return;
            }

            _currentRow = row;
            _currentIndex = 0;

            _announcer.Announce($"{GetRowName(row)}, {cards.Count} card{(cards.Count != 1 ? "s" : "")}", AnnouncementPriority.High);
            AnnounceCurrentCard();
        }

        /// <summary>
        /// Moves to the next row (towards player side / Alt+Down).
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
                    _announcer.Announce($"{GetRowName(_currentRow)}, {_rows[_currentRow].Count} card{(_rows[_currentRow].Count != 1 ? "s" : "")}", AnnouncementPriority.High);
                    AnnounceCurrentCard();
                    return;
                }
            }

            _announcer.AnnounceInterrupt("End of battlefield");
        }

        /// <summary>
        /// Moves to the previous row (towards enemy side / Alt+Up).
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
                    _announcer.Announce($"{GetRowName(_currentRow)}, {_rows[_currentRow].Count} card{(_rows[_currentRow].Count != 1 ? "s" : "")}", AnnouncementPriority.High);
                    AnnounceCurrentCard();
                    return;
                }
            }

            _announcer.AnnounceInterrupt("Beginning of battlefield");
        }

        /// <summary>
        /// Moves to the next card in the current row.
        /// </summary>
        private void NextCard()
        {
            var cards = _rows[_currentRow];
            if (cards.Count == 0)
            {
                _announcer.AnnounceInterrupt($"{GetRowName(_currentRow)} is empty");
                return;
            }

            if (_currentIndex < cards.Count - 1)
            {
                _currentIndex++;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterrupt("End of row");
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
                _announcer.AnnounceInterrupt($"{GetRowName(_currentRow)} is empty");
                return;
            }

            if (_currentIndex > 0)
            {
                _currentIndex--;
                AnnounceCurrentCard();
            }
            else
            {
                _announcer.AnnounceInterrupt("Beginning of row");
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
                _announcer.Announce("No card selected", AnnouncementPriority.High);
                return;
            }

            string cardName = CardDetector.GetCardName(card);
            MelonLogger.Msg($"[BattlefieldNavigator] Activating card: {cardName}");

            var result = UIActivator.SimulatePointerClick(card);

            if (result.Success)
            {
                _announcer.Announce($"Activated {cardName}", AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce($"Cannot activate {cardName}", AnnouncementPriority.High);
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
