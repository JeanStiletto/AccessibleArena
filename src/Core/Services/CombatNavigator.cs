using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Handles combat phase navigation.
    /// During Declare Attackers phase:
    /// - Space or F presses "All Attack" or "X Attack" button
    /// - Backspace presses "No Attacks" button
    /// - Announces attacker selection state when navigating battlefield cards
    /// During Declare Blockers phase:
    /// - Space or F presses confirm button (X Blocker / Next)
    /// - Backspace presses "No Blocks" or "Cancel Blocks" button
    /// - Tracks selected blockers and announces combined power/toughness
    /// </summary>
    public class CombatNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly DuelAnnouncer _duelAnnouncer;

        // Track selected blockers by instance ID for change detection
        private HashSet<int> _previousSelectedBlockerIds = new HashSet<int>();

        // Track assigned blockers (IsBlocking) by instance ID for change detection
        private HashSet<int> _previousAssignedBlockerIds = new HashSet<int>();

        // Track if we were in blockers phase last frame (to reset on phase change)
        private bool _wasInBlockersPhase = false;

        public CombatNavigator(IAnnouncementService announcer, DuelAnnouncer duelAnnouncer)
        {
            _announcer = announcer;
            _duelAnnouncer = duelAnnouncer;
        }

        // Debug flag for logging attacker card children (set to true for debugging)
        private bool _debugAttackerCards = false;

        /// <summary>
        /// Checks if a creature is currently selected/declared as an attacker.
        /// Uses model data first, falls back to UI child scan for transitional states.
        /// </summary>
        public bool IsCreatureAttacking(GameObject card)
        {
            if (card == null) return false;

            // Model-based check (authoritative)
            if (CardModelProvider.GetIsAttackingFromCard(card))
                return true;

            // Debug: Log relevant children to find the exact indicator
            if (_debugAttackerCards && _duelAnnouncer.IsInDeclareAttackersPhase)
            {
                LogAttackerRelevantChildren(card);
            }

            // UI fallback for transitional states (Lobbed animations, etc.)
            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;

                if (child.name.Contains("Declared") ||
                    child.name.Contains("Selected") ||
                    child.name.Contains("Lobbed"))
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
        /// Uses model data first, falls back to UI child scan.
        /// </summary>
        public bool IsCreatureBlocking(GameObject card)
        {
            if (card == null) return false;

            // Model-based check (authoritative)
            if (CardModelProvider.GetIsBlockingFromCard(card))
                return true;

            // UI fallback - "IsBlocking" child is ACTIVE when assigned as a blocker
            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "IsBlocking" && child.gameObject.activeInHierarchy)
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
        /// Finds all creatures currently assigned as blockers.
        /// Returns a list of card GameObjects that have the IsBlocking indicator present.
        /// </summary>
        private List<GameObject> FindAssignedBlockers()
        {
            var assignedBlockers = new List<GameObject>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy)
                    continue;

                if (!go.name.StartsWith("CDC "))
                    continue;

                if (IsCreatureBlocking(go))
                {
                    assignedBlockers.Add(go);
                }
            }

            return assignedBlockers;
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
        /// Tracks both selected blockers (potential) and assigned blockers (confirmed).
        /// </summary>
        public void UpdateBlockerSelection()
        {
            bool isInBlockersPhase = _duelAnnouncer.IsInDeclareBlockersPhase;

            // Reset tracking only when ENTERING blockers phase, not when exiting.
            // The game can momentarily fire phase events during blocker assignment
            // that make IsInDeclareBlockersPhase return False briefly. If we reset
            // on "exit", we lose track of assigned blockers and can't assign more.
            if (isInBlockersPhase && !_wasInBlockersPhase)
            {
                _previousSelectedBlockerIds.Clear();
                _previousAssignedBlockerIds.Clear();
                MelonLogger.Msg("[CombatNavigator] Entering blockers phase, tracking reset");
            }
            _wasInBlockersPhase = isInBlockersPhase;

            // Only track during blockers phase
            if (!isInBlockersPhase)
                return;

            // Get current assigned blockers (IsBlocking active)
            var currentAssigned = FindAssignedBlockers();
            var currentAssignedIds = new HashSet<int>();
            foreach (var blocker in currentAssigned)
            {
                currentAssignedIds.Add(blocker.GetInstanceID());
            }

            // Check if assigned blockers changed (blocker was assigned to an attacker)
            if (!currentAssignedIds.SetEquals(_previousAssignedBlockerIds))
            {
                // Find newly assigned blockers
                var newlyAssigned = new List<GameObject>();
                foreach (var blocker in currentAssigned)
                {
                    if (!_previousAssignedBlockerIds.Contains(blocker.GetInstanceID()))
                    {
                        newlyAssigned.Add(blocker);
                    }
                }

                // Announce newly assigned blockers with attacker names
                if (newlyAssigned.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var blocker in newlyAssigned)
                    {
                        var info = CardDetector.ExtractCardInfo(blocker);
                        string blockerName = info.Name ?? "creature";

                        // Try to resolve what this blocker is blocking
                        string blockingTarget = GetBlockingText(blocker);
                        if (!string.IsNullOrEmpty(blockingTarget))
                            parts.Add($"{blockerName} {blockingTarget}");
                        else
                            parts.Add($"{blockerName} assigned");
                    }
                    string announcement = string.Join(", ", parts);
                    MelonLogger.Msg($"[CombatNavigator] Blockers assigned: {newlyAssigned.Count} - {announcement}");
                    _announcer.Announce(announcement, AnnouncementPriority.High);

                    // Clear selected tracking since these blockers are now assigned
                    _previousSelectedBlockerIds.Clear();
                }

                // Update assigned tracking
                _previousAssignedBlockerIds = currentAssignedIds;
            }

            // Get current selected blockers (not yet assigned)
            var currentSelected = FindSelectedBlockers();
            var currentSelectedIds = new HashSet<int>();
            foreach (var blocker in currentSelected)
            {
                // Only track if not already assigned
                if (!currentAssignedIds.Contains(blocker.GetInstanceID()))
                {
                    currentSelectedIds.Add(blocker.GetInstanceID());
                }
            }

            // Check if selection changed
            if (!currentSelectedIds.SetEquals(_previousSelectedBlockerIds))
            {
                // Get the blockers that are selected but not assigned
                var selectedNotAssigned = new List<GameObject>();
                foreach (var blocker in currentSelected)
                {
                    if (!currentAssignedIds.Contains(blocker.GetInstanceID()))
                    {
                        selectedNotAssigned.Add(blocker);
                    }
                }

                // Selection changed - announce new combined stats
                if (selectedNotAssigned.Count > 0)
                {
                    var (totalPower, totalToughness) = CalculateCombinedStats(selectedNotAssigned);
                    string announcement = $"{totalPower}/{totalToughness} blocking";
                    MelonLogger.Msg($"[CombatNavigator] Blocker selection changed: {selectedNotAssigned.Count} blockers, {announcement}");
                    _announcer.Announce(announcement, AnnouncementPriority.High);
                }
                else if (_previousSelectedBlockerIds.Count > 0)
                {
                    MelonLogger.Msg("[CombatNavigator] Blocker selection cleared");
                }

                // Update tracking
                _previousSelectedBlockerIds = currentSelectedIds;
            }
        }

        /// <summary>
        /// Gets text to append to card announcement indicating active states.
        /// Uses model data for attacking/blocking with relationship names,
        /// and UI scan for frames, selection, and tapped state.
        /// </summary>
        public string GetCombatStateText(GameObject card)
        {
            if (card == null) return "";

            var states = new List<string>();
            bool hasAttackerFrame = false;
            bool hasBlockerFrame = false;
            bool isSelected = false;
            bool isTapped = false;

            // Model-based combat state
            bool isAttacking = CardModelProvider.GetIsAttackingFromCard(card);
            bool isBlocking = CardModelProvider.GetIsBlockingFromCard(card);

            // UI scan for frames, selection, tapped (still needed for "can block"/"can attack" etc.)
            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                // UI fallback for attacking/blocking if model didn't detect
                if (!isAttacking && child.name == "IsAttacking" && child.gameObject.activeInHierarchy)
                    isAttacking = true;
                if (!isBlocking && child.name == "IsBlocking" && child.gameObject.activeInHierarchy)
                    isBlocking = true;

                if (!child.gameObject.activeInHierarchy)
                    continue;

                if (child.name.Contains("CombatIcon_AttackerFrame"))
                    hasAttackerFrame = true;
                if (child.name.Contains("CombatIcon_BlockerFrame"))
                    hasBlockerFrame = true;
                if (child.name.Contains("SelectedHighlightBattlefield"))
                    isSelected = true;
                if (child.name.Contains("TappedIcon"))
                    isTapped = true;
            }

            // Attacking states (priority: is attacking > can attack)
            if (isAttacking)
            {
                // Try to resolve who is blocking this attacker
                string blockedByText = GetBlockedByText(card);
                if (!string.IsNullOrEmpty(blockedByText))
                    states.Add($"attacking, {blockedByText}");
                else
                    states.Add("attacking");
            }
            else if (hasAttackerFrame && _duelAnnouncer.IsInDeclareAttackersPhase)
                states.Add("can attack");

            // Blocking states (priority: is blocking > selected to block > can block)
            if (isBlocking)
            {
                // Try to resolve what this blocker is blocking
                string blockingText = GetBlockingText(card);
                if (!string.IsNullOrEmpty(blockingText))
                    states.Add(blockingText);
                else
                    states.Add("blocking");
            }
            else if (hasBlockerFrame && isSelected)
                states.Add("selected to block");
            else if (hasBlockerFrame && _duelAnnouncer.IsInDeclareBlockersPhase)
                states.Add("can block");

            // Show tapped state only if not attacking (attackers are always tapped)
            if (isTapped && !isAttacking)
                states.Add("tapped");

            if (states.Count == 0)
                return "";

            return ", " + string.Join(", ", states);
        }

        /// <summary>
        /// Resolves BlockingIds to card names for a blocker card.
        /// Returns e.g. "blocking Angel" or "blocking Angel and Dragon".
        /// </summary>
        private string GetBlockingText(GameObject card)
        {
            try
            {
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc == null) return null;
                var model = CardModelProvider.GetCardModel(cdc);
                if (model == null) return null;

                var blockingIds = CardModelProvider.GetBlockingIds(model);
                if (blockingIds.Count == 0) return null;

                var names = new List<string>();
                foreach (var id in blockingIds)
                {
                    string name = CardModelProvider.ResolveInstanceIdToName(id);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }

                if (names.Count == 0) return null;
                return "blocking " + string.Join(" and ", names);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Resolves BlockedByIds to card names for an attacker card.
        /// Returns e.g. "blocked by Cat" or "blocked by Cat and Bear".
        /// </summary>
        private string GetBlockedByText(GameObject card)
        {
            try
            {
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc == null) return null;
                var model = CardModelProvider.GetCardModel(cdc);
                if (model == null) return null;

                var blockedByIds = CardModelProvider.GetBlockedByIds(model);
                if (blockedByIds.Count == 0) return null;

                var names = new List<string>();
                foreach (var id in blockedByIds)
                {
                    string name = CardModelProvider.ResolveInstanceIdToName(id);
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }

                if (names.Count == 0) return null;
                return "blocked by " + string.Join(" and ", names);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Handles input during combat phases and main phase pass/next.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            // Track blocker selection changes and announce combined P/T
            UpdateBlockerSelection();

            // Handle Declare Attackers phase
            if (_duelAnnouncer.IsInDeclareAttackersPhase)
            {
                // Backspace - press the secondary button (No Attacks / cancel)
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    return TryClickSecondaryButton();
                }

                // Space or F - press the primary button (All Attack / X Attack)
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F))
                {
                    return TryClickPrimaryButton();
                }
            }

            // Handle Declare Blockers phase
            if (_duelAnnouncer.IsInDeclareBlockersPhase)
            {
                // Backspace - press the secondary button (No Blocks / Cancel Blocks)
                if (Input.GetKeyDown(KeyCode.Backspace))
                {
                    return TryClickSecondaryButton();
                }

                // Space or F - press the primary button (X Blocker / Next / Confirm)
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F))
                {
                    return TryClickPrimaryButton();
                }
            }

            // NOTE: Space during main phase is NOT handled here - the game handles it natively.
            // Previously we clicked the primary button on Space, but this caused double-pass
            // because both our click AND the game's native handler triggered.

            return false;
        }

        /// <summary>
        /// Finds and clicks the primary prompt button.
        /// Language-agnostic: identifies button by GameObject name, announces localized text.
        /// Returns true if button was found and clicked.
        /// </summary>
        private bool TryClickPrimaryButton()
        {
            var button = FindPromptButton(isPrimary: true);
            if (button == null)
            {
                MelonLogger.Msg("[CombatNavigator] No primary button found");
                return false;
            }

            string buttonText = UITextExtractor.GetButtonText(button);
            MelonLogger.Msg($"[CombatNavigator] Clicking primary button: {buttonText}");

            var result = UIActivator.SimulatePointerClick(button);
            if (result.Success)
            {
                _announcer.Announce(buttonText, AnnouncementPriority.Normal);
                return true;
            }
            else
            {
                MelonLogger.Msg("[CombatNavigator] Primary button click failed");
                return false;
            }
        }

        /// <summary>
        /// Finds and clicks the secondary prompt button.
        /// Language-agnostic: identifies button by GameObject name, announces localized text.
        /// Returns true if button was found and clicked.
        /// </summary>
        private bool TryClickSecondaryButton()
        {
            var button = FindPromptButton(isPrimary: false);
            if (button == null)
            {
                MelonLogger.Msg("[CombatNavigator] No secondary button found");
                return false;
            }

            string buttonText = UITextExtractor.GetButtonText(button);
            MelonLogger.Msg($"[CombatNavigator] Clicking secondary button: {buttonText}");

            var result = UIActivator.SimulatePointerClick(button);
            if (result.Success)
            {
                _announcer.Announce(buttonText, AnnouncementPriority.Normal);
                return true;
            }
            else
            {
                MelonLogger.Msg("[CombatNavigator] Secondary button click failed");
                return false;
            }
        }

        /// <summary>
        /// Finds a prompt button by type (primary or secondary).
        /// Language-agnostic: uses GameObject name pattern, not button text.
        /// </summary>
        private GameObject FindPromptButton(bool isPrimary)
        {
            string pattern = isPrimary ? "PromptButton_Primary" : "PromptButton_Secondary";

            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                // Skip emote panel buttons
                if (IsInEmotePanel(selectable.gameObject))
                    continue;

                if (selectable.gameObject.name.Contains(pattern))
                {
                    return selectable.gameObject;
                }
            }

            return null;
        }


        /// <summary>
        /// Checks if a GameObject is part of the emote/communication panel UI.
        /// Used to filter out emote buttons from combat button searches.
        /// </summary>
        private bool IsInEmotePanel(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                string name = current.name;
                if (name.Contains("EmoteOptionsPanel") ||
                    name.Contains("CommunicationOptionsPanel") ||
                    name.Contains("EmoteView") ||
                    name.Contains("NavArrow"))
                {
                    return true;
                }
                current = current.parent;
                depth++;
            }
            return false;
        }
    }
}
