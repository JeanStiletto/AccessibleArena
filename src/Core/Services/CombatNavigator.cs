using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections.Generic;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Handles combat phase navigation.
    /// During Declare Attackers phase:
    /// - Space or F presses "All Attack" or "X Attack" button
    /// - Shift+F presses "No Attacks" button
    /// - Announces attacker selection state when navigating battlefield cards
    /// During Declare Blockers phase:
    /// - Space or F presses confirm button (X Blocker / Next)
    /// - Shift+F presses "No Blocks" or "Cancel Blocks" button
    /// - Tracks selected blockers and announces combined power/toughness
    /// </summary>
    public class CombatNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly DuelAnnouncer _duelAnnouncer;

        // Debug flag for logging card children
        private bool _debugBlockerCards = true;

        // Track selected blockers by instance ID for change detection
        private HashSet<int> _previousSelectedBlockerIds = new HashSet<int>();

        // Track if we were in blockers phase last frame (to reset on phase change)
        private bool _wasInBlockersPhase = false;

        public CombatNavigator(IAnnouncementService announcer, DuelAnnouncer duelAnnouncer)
        {
            _announcer = announcer;
            _duelAnnouncer = duelAnnouncer;
        }

        // Debug flag for logging attacker card children
        private bool _debugAttackerCards = true;

        /// <summary>
        /// Checks if a creature is currently selected/declared as an attacker.
        /// </summary>
        public bool IsCreatureAttacking(GameObject card)
        {
            if (card == null) return false;

            // Debug: Log relevant children to find the exact indicator
            if (_debugAttackerCards && _duelAnnouncer.IsInDeclareAttackersPhase)
            {
                LogAttackerRelevantChildren(card);
            }

            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;

                // Check multiple possible indicators:
                // - "IsAttacking" might activate when declared
                // - There might be a "Selected" or "Declared" indicator
                // - The card might be tapped/rotated
                if (child.name == "IsAttacking" ||
                    child.name.Contains("Declared") ||
                    child.name.Contains("Selected") ||
                    child.name.Contains("Lobbed"))  // "Lobbed" from AttackLobUXEvent might be relevant
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Debug: Logs children related to attack state to find the right indicator.
        /// Only logs children within CombatIcon_AttackerFrame and other relevant areas.
        /// </summary>
        private void LogAttackerRelevantChildren(GameObject card)
        {
            MelonLogger.Msg($"[CombatNavigator] === ATTACKER DEBUG: {card.name} ===");

            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                string childName = child.name;

                // Only log potentially relevant children to reduce noise
                if (childName.Contains("Combat") ||
                    childName.Contains("Attack") ||
                    childName.Contains("Select") ||
                    childName.Contains("Declare") ||
                    childName.Contains("Lob") ||
                    childName.Contains("Tap") ||
                    childName.Contains("Is"))
                {
                    string status = child.gameObject.activeInHierarchy ? "ACTIVE" : "inactive";
                    MelonLogger.Msg($"[CombatNavigator]   [{status}] {childName}");
                }
            }
        }

        /// <summary>
        /// Checks if a creature is currently assigned as a blocker.
        /// Looks for the active "IsBlocking" child within the card hierarchy.
        /// </summary>
        public bool IsCreatureBlocking(GameObject card)
        {
            if (card == null) return false;

            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;

                // "IsBlocking" child activates when the creature is assigned as a blocker
                if (child.name == "IsBlocking")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a creature is currently selected (highlighted) as a potential blocker.
        /// This is different from IsCreatureBlocking - selected means the player clicked on it
        /// but hasn't yet assigned it to block a specific attacker.
        /// </summary>
        public bool IsCreatureSelectedAsBlocker(GameObject card)
        {
            if (card == null) return false;

            // A creature is selected as a blocker if it has both:
            // 1. CombatIcon_BlockerFrame (can block)
            // 2. SelectedHighlightBattlefield (currently selected)
            bool hasBlockerFrame = false;
            bool hasSelectedHighlight = false;

            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;

                if (child.name.Contains("CombatIcon_BlockerFrame"))
                    hasBlockerFrame = true;

                if (child.name.Contains("SelectedHighlightBattlefield"))
                    hasSelectedHighlight = true;
            }

            return hasBlockerFrame && hasSelectedHighlight;
        }

        /// <summary>
        /// Finds all creatures currently selected as blockers.
        /// Returns a list of card GameObjects that have both blocker frame and selection highlight.
        /// </summary>
        private List<GameObject> FindSelectedBlockers()
        {
            var selectedBlockers = new List<GameObject>();

            // Find all CDC objects (cards) on the battlefield
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                // Only check CDC (card) objects
                if (!go.name.StartsWith("CDC "))
                    continue;

                if (IsCreatureSelectedAsBlocker(go))
                {
                    selectedBlockers.Add(go);
                }
            }

            return selectedBlockers;
        }

        /// <summary>
        /// Parses power and toughness from a card.
        /// Returns (power, toughness) or (0, 0) if not a creature or can't parse.
        /// </summary>
        private (int power, int toughness) GetPowerToughness(GameObject card)
        {
            if (card == null) return (0, 0);

            var info = CardDetector.ExtractCardInfo(card);
            if (string.IsNullOrEmpty(info.PowerToughness))
                return (0, 0);

            // Parse "X/Y" format
            var parts = info.PowerToughness.Split('/');
            if (parts.Length != 2)
                return (0, 0);

            if (int.TryParse(parts[0].Trim(), out int power) &&
                int.TryParse(parts[1].Trim(), out int toughness))
            {
                return (power, toughness);
            }

            return (0, 0);
        }

        /// <summary>
        /// Calculates combined power and toughness for a list of blockers.
        /// </summary>
        private (int totalPower, int totalToughness) CalculateCombinedStats(List<GameObject> blockers)
        {
            int totalPower = 0;
            int totalToughness = 0;

            foreach (var blocker in blockers)
            {
                var (power, toughness) = GetPowerToughness(blocker);
                totalPower += power;
                totalToughness += toughness;
            }

            return (totalPower, totalToughness);
        }

        /// <summary>
        /// Updates blocker selection tracking and announces changes.
        /// Should be called each frame during declare blockers phase.
        /// </summary>
        public void UpdateBlockerSelection()
        {
            bool isInBlockersPhase = _duelAnnouncer.IsInDeclareBlockersPhase;

            // Reset tracking when entering/exiting blockers phase
            if (isInBlockersPhase != _wasInBlockersPhase)
            {
                _previousSelectedBlockerIds.Clear();
                _wasInBlockersPhase = isInBlockersPhase;
            }

            // Only track during blockers phase
            if (!isInBlockersPhase)
                return;

            // Get current selection
            var currentBlockers = FindSelectedBlockers();
            var currentIds = new HashSet<int>();
            foreach (var blocker in currentBlockers)
            {
                currentIds.Add(blocker.GetInstanceID());
            }

            // Check if selection changed
            if (!currentIds.SetEquals(_previousSelectedBlockerIds))
            {
                // Selection changed - announce new combined stats
                if (currentBlockers.Count > 0)
                {
                    var (totalPower, totalToughness) = CalculateCombinedStats(currentBlockers);
                    string announcement = $"{totalPower}/{totalToughness} blocking";
                    MelonLogger.Msg($"[CombatNavigator] Blocker selection changed: {currentBlockers.Count} blockers, {announcement}");
                    _announcer.Announce(announcement, AnnouncementPriority.High);
                }
                else
                {
                    MelonLogger.Msg("[CombatNavigator] Blocker selection cleared");
                    // Optionally announce "no blockers selected"
                }

                // Update tracking
                _previousSelectedBlockerIds = currentIds;
            }
        }

        /// <summary>
        /// Gets text to append to card announcement indicating combat state.
        /// Works for both declare attackers and declare blockers phases.
        /// </summary>
        public string GetCombatStateText(GameObject card)
        {
            // Declare Attackers phase - show attacking state
            if (_duelAnnouncer.IsInDeclareAttackersPhase)
            {
                if (IsCreatureAttacking(card))
                    return ", attacking";
                else
                    return ", not attacking";
            }

            // Declare Blockers phase - show blocking state for your creatures, attacking state for enemy attackers
            if (_duelAnnouncer.IsInDeclareBlockersPhase)
            {
                // Debug: Log card children to find blocker indicator
                if (_debugBlockerCards)
                {
                    LogCardChildrenForBlocker(card);
                }

                // Check if this is an attacking creature (enemy attacker)
                if (IsCreatureAttacking(card))
                    return ", attacking";

                // Otherwise check blocking state for your potential blockers
                if (IsCreatureBlocking(card))
                    return ", blocking";
                else
                    return ", not blocking";
            }

            return "";
        }

        /// <summary>
        /// Debug: Logs card children during declare blockers to find the blocker indicator.
        /// </summary>
        private void LogCardChildrenForBlocker(GameObject card)
        {
            if (card == null) return;

            MelonLogger.Msg($"[CombatNavigator] === BLOCKER DEBUG: Children of {card.name} ===");
            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                string status = child.gameObject.activeInHierarchy ? "ACTIVE" : "inactive";
                MelonLogger.Msg($"[CombatNavigator]   [{status}] {child.name}");
            }
        }

        /// <summary>
        /// Handles input during combat phases.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            // Track blocker selection changes and announce combined P/T
            UpdateBlockerSelection();

            // Handle Declare Attackers phase
            if (_duelAnnouncer.IsInDeclareAttackersPhase)
            {
                // Shift+F - press the no attacks button (check first before F alone)
                if (Input.GetKeyDown(KeyCode.F) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    return TryClickNoAttackButton();
                }

                // Space or F (without Shift) - press the attack button (All Attack / X Attack)
                if (Input.GetKeyDown(KeyCode.Space) ||
                    (Input.GetKeyDown(KeyCode.F) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
                {
                    return TryClickAttackButton();
                }
            }

            // Handle Declare Blockers phase
            if (_duelAnnouncer.IsInDeclareBlockersPhase)
            {
                // Shift+F - press the no blocks / cancel blocks button (check first before F alone)
                if (Input.GetKeyDown(KeyCode.F) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    LogBlockerPhaseButtons();
                    return TryClickNoBlockButton();
                }

                // Space or F (without Shift) - press the confirm blocks button (X Blocker / Next)
                if (Input.GetKeyDown(KeyCode.Space) ||
                    (Input.GetKeyDown(KeyCode.F) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
                {
                    LogBlockerPhaseButtons();
                    return TryClickBlockerConfirmButton();
                }
            }

            return false;
        }

        /// <summary>
        /// Debug: Logs all buttons visible during declare blockers phase.
        /// </summary>
        private void LogBlockerPhaseButtons()
        {
            MelonLogger.Msg("[CombatNavigator] === BLOCKER PHASE BUTTONS ===");
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                string name = selectable.gameObject.name;
                if (name.Contains("PromptButton") || name.Contains("Button"))
                {
                    string text = UITextExtractor.GetButtonText(selectable.gameObject);
                    MelonLogger.Msg($"[CombatNavigator]   Button: {name} - Text: '{text}'");
                }
            }
            MelonLogger.Msg("[CombatNavigator] === END BLOCKER PHASE BUTTONS ===");
        }

        /// <summary>
        /// Finds and clicks the confirm/done button during declare blockers.
        /// Matches "X Blocker" or "Next" but NOT "No Blocks".
        /// Returns true if button was found and clicked.
        /// </summary>
        private bool TryClickBlockerConfirmButton()
        {
            // Look for PromptButton_Primary with confirm-like text
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                string name = selectable.gameObject.name;
                if (!name.Contains("PromptButton_Primary"))
                    continue;

                string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
                if (string.IsNullOrEmpty(buttonText))
                    continue;

                // Skip "No Blocks" - that's handled by TryClickNoBlockButton
                if (buttonText.Contains("No "))
                    continue;

                // Look for confirm type buttons: "X Blocker", "Next", "Done", "Confirm", "OK"
                // But NOT "Opponent's Turn"
                if (buttonText.Contains("Blocker") || buttonText.Contains("Next") ||
                    buttonText.Contains("Done") || buttonText.Contains("Confirm") || buttonText.Contains("OK"))
                {
                    MelonLogger.Msg($"[CombatNavigator] Clicking blocker confirm button: {buttonText}");
                    var result = UIActivator.SimulatePointerClick(selectable.gameObject);
                    if (result.Success)
                    {
                        _announcer.Announce(buttonText, AnnouncementPriority.Normal);
                        return true;
                    }
                }
            }

            MelonLogger.Msg("[CombatNavigator] No blocker confirm button found");
            return false;
        }

        /// <summary>
        /// Finds and clicks the "No Blocks" or "Cancel Blocks" button.
        /// Returns true if button was found and clicked.
        /// </summary>
        private bool TryClickNoBlockButton()
        {
            // First check secondary button for "No Blocks" or "Cancel Blocks"
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                string name = selectable.gameObject.name;
                if (!name.Contains("PromptButton_Secondary"))
                    continue;

                string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
                if (string.IsNullOrEmpty(buttonText))
                    continue;

                // Match "No Blocks" or "Cancel Blocks"
                if (buttonText.Contains("No ") || buttonText.Contains("Cancel"))
                {
                    MelonLogger.Msg($"[CombatNavigator] Clicking no block button: {buttonText}");
                    var result = UIActivator.SimulatePointerClick(selectable.gameObject);
                    if (result.Success)
                    {
                        _announcer.Announce(buttonText, AnnouncementPriority.Normal);
                        return true;
                    }
                }
            }

            // Also check primary button for "No Blocks" (initial state)
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                string name = selectable.gameObject.name;
                if (!name.Contains("PromptButton_Primary"))
                    continue;

                string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
                if (string.IsNullOrEmpty(buttonText))
                    continue;

                // Match "No Blocks" on primary button
                if (buttonText.Contains("No Blocks"))
                {
                    MelonLogger.Msg($"[CombatNavigator] Clicking no block button (primary): {buttonText}");
                    var result = UIActivator.SimulatePointerClick(selectable.gameObject);
                    if (result.Success)
                    {
                        _announcer.Announce(buttonText, AnnouncementPriority.Normal);
                        return true;
                    }
                }
            }

            MelonLogger.Msg("[CombatNavigator] No 'No Blocks' or 'Cancel Blocks' button found");
            return false;
        }

        /// <summary>
        /// Finds and clicks the "All Attack" or "X Attack" button.
        /// Returns true if button was found and clicked.
        /// </summary>
        private bool TryClickAttackButton()
        {
            var buttonInfo = FindAttackButton();
            if (buttonInfo == null)
            {
                MelonLogger.Msg("[CombatNavigator] No attack button found");
                return false;
            }

            var (button, text) = buttonInfo.Value;
            MelonLogger.Msg($"[CombatNavigator] Clicking attack button: {text}");

            var result = UIActivator.SimulatePointerClick(button);
            if (result.Success)
            {
                _announcer.Announce(text, AnnouncementPriority.Normal);
                return true;
            }
            else
            {
                _announcer.Announce("Could not activate attack button", AnnouncementPriority.High);
                return true; // Still consume the input
            }
        }

        /// <summary>
        /// Finds and clicks the "No Attacks" button.
        /// Returns true if button was found and clicked.
        /// </summary>
        private bool TryClickNoAttackButton()
        {
            var buttonInfo = FindNoAttackButton();
            if (buttonInfo == null)
            {
                MelonLogger.Msg("[CombatNavigator] No 'No Attacks' button found");
                return false;
            }

            var (button, text) = buttonInfo.Value;
            MelonLogger.Msg($"[CombatNavigator] Clicking no attack button: {text}");

            var result = UIActivator.SimulatePointerClick(button);
            if (result.Success)
            {
                _announcer.Announce(text, AnnouncementPriority.Normal);
                return true;
            }
            else
            {
                _announcer.Announce("Could not activate no attack button", AnnouncementPriority.High);
                return true; // Still consume the input
            }
        }

        /// <summary>
        /// Finds the attack button (PromptButton_Primary with "Attack" in text).
        /// Returns null if not found or if it's opponent's turn.
        /// </summary>
        private (GameObject button, string text)? FindAttackButton()
        {
            // Look for PromptButton_Primary with text containing "Attack"
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                string name = selectable.gameObject.name;
                if (!name.Contains("PromptButton_Primary"))
                    continue;

                // Get the button text
                string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
                if (string.IsNullOrEmpty(buttonText))
                    continue;

                // Check if it contains "Attack" (e.g., "All Attack", "1 Attack", "2 Attack")
                // This also filters out "Opponent's Turn" which appears during opponent's declare attackers
                if (buttonText.Contains("Attack"))
                {
                    MelonLogger.Msg($"[CombatNavigator] Found attack button: {name} with text '{buttonText}'");
                    return (selectable.gameObject, buttonText);
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the "No Attacks" button (PromptButton_Secondary with "No" in text).
        /// Returns null if not found.
        /// </summary>
        private (GameObject button, string text)? FindNoAttackButton()
        {
            // Look for PromptButton_Secondary with text containing "No"
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                string name = selectable.gameObject.name;
                if (!name.Contains("PromptButton_Secondary"))
                    continue;

                // Get the button text
                string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
                if (string.IsNullOrEmpty(buttonText))
                    continue;

                // Check if it contains "No" (e.g., "No Attacks", "No Attack")
                if (buttonText.Contains("No"))
                {
                    MelonLogger.Msg($"[CombatNavigator] Found no attack button: {name} with text '{buttonText}'");
                    return (selectable.gameObject, buttonText);
                }
            }

            return null;
        }
    }
}
