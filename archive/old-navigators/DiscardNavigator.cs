using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections;
using System.Text.RegularExpressions;

namespace AccessibleArena.Core.Services
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

        // Language-agnostic: matches any text with a number at the end (e.g., "Submit 2", "Abgeben 2")
        private static readonly Regex ButtonNumberPattern = new Regex(@"(\d+)\s*$", RegexOptions.IgnoreCase);

        // Multi-language patterns for required discard count
        // English: "Discard X cards", "Discard a card"
        // German: "X Karten abwerfen", "Eine Karte abwerfen", "Wirf X Karten ab"
        private static readonly Regex[] DiscardCountPatterns = new[]
        {
            new Regex(@"Discard\s+(\d+)\s+cards?", RegexOptions.IgnoreCase),           // English: "Discard 2 cards"
            new Regex(@"(\d+)\s+Karten?\s+abwerfen", RegexOptions.IgnoreCase),         // German: "2 Karten abwerfen"
            new Regex(@"Wirf\s+(\d+)\s+Karten?\s+ab", RegexOptions.IgnoreCase),        // German: "Wirf 2 Karten ab"
        };
        private static readonly Regex[] DiscardOnePatterns = new[]
        {
            new Regex(@"Discard\s+a\s+card", RegexOptions.IgnoreCase),                 // English: "Discard a card"
            new Regex(@"Eine\s+Karte\s+abwerfen", RegexOptions.IgnoreCase),            // German: "Eine Karte abwerfen"
            new Regex(@"Wirf\s+eine\s+Karte\s+ab", RegexOptions.IgnoreCase),           // German: "Wirf eine Karte ab"
        };

        private int? _requiredCount = null; // Cached when entering discard mode
        private bool _hasLoggedTargetYield = false; // Throttle log spam

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
            {
                _hasLoggedTargetYield = false; // Reset throttle when Submit button gone
                return false;
            }

            // If there are valid targets on the battlefield (HotHighlight),
            // we're in targeting mode, not discard mode
            if (CardDetector.HasValidTargetsOnBattlefield())
            {
                // Only log once to avoid spam
                if (!_hasLoggedTargetYield)
                {
                    MelonLogger.Msg("[DiscardNavigator] Submit button found but valid targets exist - yielding to targeting mode");
                    _hasLoggedTargetYield = true;
                }
                return false;
            }

            _hasLoggedTargetYield = false; // Reset when no targets
            return true;
        }

        /// <summary>
        /// Gets the required discard count from the prompt text.
        /// Language-agnostic: supports multiple language patterns.
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

                // Check for "discard one card" patterns (any language)
                foreach (var pattern in DiscardOnePatterns)
                {
                    if (pattern.IsMatch(text))
                    {
                        return 1;
                    }
                }

                // Check for "discard X cards" patterns (any language)
                foreach (var pattern in DiscardCountPatterns)
                {
                    var match = pattern.Match(text);
                    if (match.Success)
                    {
                        return int.Parse(match.Groups[1].Value);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the Submit button info: count of selected cards and button GameObject.
        /// Language-agnostic: finds PromptButton_Primary and extracts trailing number from text.
        /// Returns null if no Submit button with a number found.
        /// </summary>
        public (int count, GameObject button)? GetSubmitButtonInfo()
        {
            // Find PromptButton_Primary directly (language-agnostic by button name)
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                if (!selectable.gameObject.name.Contains("PromptButton_Primary"))
                    continue;

                // Get button text and look for a trailing number
                string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
                if (string.IsNullOrEmpty(buttonText))
                    continue;

                // Match any trailing number (e.g., "Submit 2", "Abgeben 2", "2")
                var match = ButtonNumberPattern.Match(buttonText);
                if (match.Success)
                {
                    int count = int.Parse(match.Groups[1].Value);
                    return (count, selectable.gameObject);
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

            return "";
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
                _announcer.Announce(Strings.DiscardCount(_requiredCount.Value), AnnouncementPriority.High);
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
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.High);
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
                _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
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
                _announcer.Announce(Strings.CardsSelected(count), AnnouncementPriority.Normal);
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
                _announcer.Announce(Strings.NoSubmitButtonFound, AnnouncementPriority.High);
                return;
            }

            int selected = info.Value.count;
            int required = _requiredCount ?? 0;

            if (selected == 0)
            {
                _announcer.Announce(Strings.NeedHaveSelected(required, 0), AnnouncementPriority.High);
                return;
            }

            // Check if selected matches required
            if (selected != required)
            {
                _announcer.Announce(Strings.NeedHaveSelected(required, selected), AnnouncementPriority.High);
                return;
            }

            MelonLogger.Msg($"[DiscardNavigator] Submitting {selected} cards for discard");

            var result = UIActivator.SimulatePointerClick(info.Value.button);
            if (result.Success)
            {
                _announcer.Announce(Strings.SubmittingDiscard(selected), AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(Strings.CouldNotSubmitDiscard, AnnouncementPriority.High);
            }
        }
    }
}
