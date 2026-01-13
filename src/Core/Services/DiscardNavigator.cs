using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections;
using System.Text.RegularExpressions;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Handles card selection during discard phases.
    /// Detects discard mode via "Submit X" button and allows:
    /// - Enter to toggle card selection
    /// - Space to submit or announce error
    /// Zone navigation (C, arrows) is handled by ZoneNavigator.
    /// </summary>
    public class DiscardNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ZoneNavigator _zoneNavigator;
        private static readonly Regex SubmitPattern = new Regex(@"^Submit\s*(\d+)$", RegexOptions.IgnoreCase);
        private static readonly Regex DiscardCountPattern = new Regex(@"Discard\s+(\d+)\s+cards?", RegexOptions.IgnoreCase);
        private static readonly Regex DiscardACardPattern = new Regex(@"Discard\s+a\s+card", RegexOptions.IgnoreCase);
        private int? _requiredCount = null; // Cached when entering discard mode

        public DiscardNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = zoneNavigator;
        }

        /// <summary>
        /// Checks if discard/selection mode is active by looking for "Submit X" button.
        /// Also checks that we're NOT in targeting mode (cards with HotHighlight).
        /// </summary>
        public bool IsDiscardModeActive()
        {
            if (GetSubmitButtonInfo() == null)
                return false;

            // If there are valid targets on the battlefield (HotHighlight),
            // we're in targeting mode, not discard mode
            if (CardDetector.HasValidTargetsOnBattlefield())
            {
                MelonLogger.Msg("[DiscardNavigator] Submit button found but valid targets exist - yielding to targeting mode");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the required discard count from the prompt text.
        /// Looks for patterns like "Discard a card" (=1) or "Discard 2 cards" (=2).
        /// Returns null if not found.
        /// </summary>
        public int? GetRequiredDiscardCount()
        {
            foreach (var tmpText in GameObject.FindObjectsOfType<TMP_Text>())
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                string text = tmpText.text?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                // Check for "Discard a card" pattern (= 1 card)
                if (DiscardACardPattern.IsMatch(text))
                {
                    return 1;
                }

                // Check for "Discard X cards" pattern
                var match = DiscardCountPattern.Match(text);
                if (match.Success)
                {
                    return int.Parse(match.Groups[1].Value);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the Submit button info: count of selected cards and button GameObject.
        /// Returns null if no Submit button found.
        /// </summary>
        public (int count, GameObject button)? GetSubmitButtonInfo()
        {
            // Check TMP_Text components (used by StyledButton)
            foreach (var tmpText in GameObject.FindObjectsOfType<TMP_Text>())
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                string text = tmpText.text?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                var match = SubmitPattern.Match(text);
                if (match.Success)
                {
                    // Find the PromptButton_Primary parent (the actual clickable button)
                    var parent = tmpText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name;
                        // Look specifically for PromptButton_Primary, not just any "button"
                        if (parentName.Contains("PromptButton_Primary"))
                        {
                            int count = int.Parse(match.Groups[1].Value);
                            return (count, parent.gameObject);
                        }
                        parent = parent.parent;
                    }
                }
            }

            // Also check legacy Text components
            foreach (var uiText in GameObject.FindObjectsOfType<Text>())
            {
                if (uiText == null || !uiText.gameObject.activeInHierarchy)
                    continue;

                string text = uiText.text?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                var match = SubmitPattern.Match(text);
                if (match.Success)
                {
                    var parent = uiText.transform.parent;
                    while (parent != null)
                    {
                        string parentName = parent.name;
                        if (parentName.Contains("PromptButton_Primary"))
                        {
                            int count = int.Parse(match.Groups[1].Value);
                            return (count, parent.gameObject);
                        }
                        parent = parent.parent;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a card is selected for discard.
        /// Looks for selection visual indicators on the card.
        /// </summary>
        public bool IsCardSelectedForDiscard(GameObject card)
        {
            if (card == null) return false;

            // Look for selection indicator children
            // Common patterns: "Selected", "SelectionHighlight", "DiscardSelection"
            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                string childName = child.name.ToLower();
                if ((childName.Contains("select") || childName.Contains("chosen") || childName.Contains("pick"))
                    && child.gameObject.activeInHierarchy)
                {
                    MelonLogger.Msg($"[DiscardNavigator] Found selection indicator: {child.name} on {card.name}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets text to append to card announcement indicating selection state.
        /// Returns empty string if not in discard mode.
        /// </summary>
        public string GetSelectionStateText(GameObject card)
        {
            if (!IsDiscardModeActive()) return "";

            if (IsCardSelectedForDiscard(card))
                return ", selected for discard";
            else
                return ", not selected";
        }

        /// <summary>
        /// Handles input during discard mode.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!IsDiscardModeActive())
            {
                _requiredCount = null; // Reset when discard mode ends
                return false;
            }

            // First time entering discard mode - cache required count and announce
            if (_requiredCount == null)
            {
                _requiredCount = GetRequiredDiscardCount() ?? 0;
                string cardWord = _requiredCount == 1 ? "card" : "cards";
                _announcer.Announce($"Discard {_requiredCount} {cardWord}", AnnouncementPriority.High);
            }

            // Enter - toggle card selection
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ToggleCurrentCard();
                return true;
            }

            // Space - submit selection
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TrySubmit();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Toggles selection on the current card by clicking it.
        /// </summary>
        private void ToggleCurrentCard()
        {
            var card = _zoneNavigator.GetCurrentCard();
            if (card == null)
            {
                _announcer.Announce("No card selected", AnnouncementPriority.High);
                return;
            }

            string cardName = CardDetector.GetCardName(card);
            MelonLogger.Msg($"[DiscardNavigator] Toggling selection on: {cardName}");

            // Click the card to toggle selection
            var result = UIActivator.SimulatePointerClick(card);
            if (result.Success)
            {
                // Wait for game to update, then announce the count
                MelonCoroutines.Start(AnnounceSelectionCountDelayed());
            }
            else
            {
                _announcer.Announce($"Could not select {cardName}", AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Waits for the game to update the Submit button, then announces the count.
        /// </summary>
        private IEnumerator AnnounceSelectionCountDelayed()
        {
            // Wait 0.2 seconds for game to update UI
            yield return new WaitForSeconds(0.2f);

            var info = GetSubmitButtonInfo();
            if (info != null)
            {
                int count = info.Value.count;
                if (count == 0)
                {
                    _announcer.Announce("0 cards selected", AnnouncementPriority.Normal);
                }
                else if (count == 1)
                {
                    _announcer.Announce("1 card selected", AnnouncementPriority.Normal);
                }
                else
                {
                    _announcer.Announce($"{count} cards selected", AnnouncementPriority.Normal);
                }
            }
        }

        /// <summary>
        /// Attempts to submit the discard selection.
        /// Checks if selected count matches required count.
        /// </summary>
        private void TrySubmit()
        {
            var info = GetSubmitButtonInfo();
            if (info == null)
            {
                _announcer.Announce("No submit button found", AnnouncementPriority.High);
                return;
            }

            int selected = info.Value.count;
            int required = _requiredCount ?? 0;

            if (selected == 0)
            {
                _announcer.Announce($"Need {required}, have 0 selected", AnnouncementPriority.High);
                return;
            }

            // Check if selected matches required
            if (selected != required)
            {
                _announcer.Announce($"Need {required}, have {selected} selected", AnnouncementPriority.High);
                return;
            }

            MelonLogger.Msg($"[DiscardNavigator] Submitting {selected} cards for discard");

            var result = UIActivator.SimulatePointerClick(info.Value.button);
            if (result.Success)
            {
                _announcer.Announce($"Submitting {selected} cards for discard", AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce("Could not submit discard", AnnouncementPriority.High);
            }
        }
    }
}
