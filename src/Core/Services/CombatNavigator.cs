using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Handles combat phase navigation.
    /// During Declare Attackers phase:
    /// - Space presses "All Attack" or "X Attack" button (not "No Attacks")
    /// - Announces attacker selection state when navigating battlefield cards
    /// During Declare Blockers phase:
    /// - Space presses "Done" or confirm button
    /// - Announces blocker selection state when navigating battlefield cards
    /// </summary>
    public class CombatNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly DuelAnnouncer _duelAnnouncer;

        // Debug flag for logging card children
        private bool _debugBlockerCards = true;

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
        /// Looks for blocker indicator (to be determined from debug logging).
        /// </summary>
        public bool IsCreatureBlocking(GameObject card)
        {
            if (card == null) return false;

            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;

                // Look for blocker indicators - patterns to be refined based on debug output
                // Likely similar to CombatIcon_AttackerFrame but for blockers
                if (child.name.Contains("CombatIcon_BlockerFrame") ||
                    child.name.Contains("BlockerIcon") ||
                    child.name.Contains("Blocking"))
                {
                    return true;
                }
            }

            return false;
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

            // Declare Blockers phase - show blocking state and debug log
            if (_duelAnnouncer.IsInDeclareBlockersPhase)
            {
                // Debug: Log card children to find blocker indicator
                if (_debugBlockerCards)
                {
                    LogCardChildrenForBlocker(card);
                }

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
            // Handle Declare Attackers phase
            if (_duelAnnouncer.IsInDeclareAttackersPhase)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    return TryClickAttackButton();
                }
            }

            // Handle Declare Blockers phase
            if (_duelAnnouncer.IsInDeclareBlockersPhase)
            {
                // Debug: Log available buttons when Space is pressed
                if (Input.GetKeyDown(KeyCode.Space))
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

                // Look for confirm/done type buttons (not "Opponent's Turn")
                if (buttonText.Contains("Done") || buttonText.Contains("Confirm") ||
                    buttonText.Contains("Block") || buttonText.Contains("OK"))
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
    }
}
