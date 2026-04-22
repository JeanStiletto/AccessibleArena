using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Linq;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Handles modal overlays that appear on top of other screens.
    /// Detects overlays by looking for Background_ClickBlocker and similar modal patterns.
    /// Examples: What's New carousel, announcements, reward popups.
    /// </summary>
    public class OverlayNavigator : BaseNavigator
    {
        private GameObject _overlayBlocker;
        private string _overlayType;
        private float _nextTypeRecheckTime;
        private const float TypeRecheckInterval = 0.5f;

        public override string NavigatorId => "Overlay";
        public override string ScreenName => GetOverlayScreenName();
        public override int Priority => 85; // High priority - overlays should intercept other screens

        public OverlayNavigator(IAnnouncementService announcer) : base(announcer) { }

        private string GetOverlayScreenName()
        {
            return _overlayType switch
            {
                "WhatsNew" => Strings.ScreenWhatsNew,
                "Announcement" => Strings.ScreenAnnouncement,
                "Reward" => Strings.ScreenRewardPopup,
                _ => Strings.ScreenOverlay
            };
        }

        protected override bool DetectScreen()
        {
            // Look for modal overlay indicators
            _overlayBlocker = GameObject.Find("Background_ClickBlocker");
            if (_overlayBlocker == null || !_overlayBlocker.activeInHierarchy)
            {
                _overlayBlocker = null;
                return false;
            }

            // Defer to RewardPopupNavigator while a reward claim is revealing.
            // That navigator has content-gated activation and may take several
            // frames to become ready — without this yield, we'd grab the
            // click-blocker first and block its preemption path.
            var rewardNav = NavigatorManager.Instance?.GetNavigator<RewardPopupNavigator>();
            if (rewardNav != null && rewardNav.IsClaimInProgress)
            {
                return false;
            }

            // Determine overlay type by checking for specific elements
            DetermineOverlayType();

            Log.Msg("{NavigatorId}", $"Detected overlay: {_overlayType}");
            return true;
        }

        private void DetermineOverlayType()
        {
            // Check for What's New carousel (has NavPip pagination dots).
            // Exclude pips that belong to the home page banner carousel
            // (Home_Desktop_16x9/SafeZone/Banners/NavDots/...) — those are always
            // active behind any modal and would cause a false WhatsNew match.
            var navPips = GameObject.FindObjectsOfType<Button>()
                .Where(b => b.gameObject.activeInHierarchy
                    && b.gameObject.name.Contains("NavPip")
                    && !IsUnderHomePageBanners(b.transform))
                .ToList();

            if (navPips.Count > 0)
            {
                _overlayType = "WhatsNew";
                return;
            }

            // Check for reward-related overlays
            var rewardIndicators = new[] { "Reward", "Prize", "Chest", "Pack" };
            foreach (var indicator in rewardIndicators)
            {
                var found = GameObject.FindObjectsOfType<GameObject>()
                    .Any(g => g.activeInHierarchy && g.name.Contains(indicator));
                if (found)
                {
                    _overlayType = "Reward";
                    return;
                }
            }

            _overlayType = "Announcement";
        }

        private static bool IsUnderHomePageBanners(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n.StartsWith("Home_Desktop_") || n == "Banners" || n == "NavDots")
                    return true;
            }
            return false;
        }

        protected override void DiscoverElements()
        {
            var addedObjects = new HashSet<GameObject>();

            switch (_overlayType)
            {
                case "WhatsNew":
                    DiscoverWhatsNewElements(addedObjects);
                    break;
                case "Reward":
                    DiscoverRewardElements(addedObjects);
                    break;
                default:
                    DiscoverGenericOverlayElements(addedObjects);
                    break;
            }
        }

        public override void Update()
        {
            // Re-evaluate overlay type periodically while active. Some overlay
            // signals (e.g. late-loading canvases) aren't present on the first
            // DetectScreen call, so we rescan every TypeRecheckInterval and
            // force an element rescan if the type changes.
            if (_isActive && Time.time >= _nextTypeRecheckTime)
            {
                _nextTypeRecheckTime = Time.time + TypeRecheckInterval;
                string previous = _overlayType;
                DetermineOverlayType();
                if (_overlayType != previous)
                {
                    Log.Msg("{NavigatorId}", $"Overlay type changed: {previous} -> {_overlayType}");
                    ForceRescan();
                }
            }

            base.Update();
        }

        protected override void OnDeactivating()
        {
            _nextTypeRecheckTime = 0f;
        }

        private void DiscoverWhatsNewElements(HashSet<GameObject> addedObjects)
        {
            // Find carousel content - look for text elements that might contain news
            var allTexts = GameObject.FindObjectsOfType<TMPro.TMP_Text>()
                .Where(t => t.gameObject.activeInHierarchy)
                .ToList();

            // Try to find main content text (title, description)
            string mainContent = ExtractMainContent(allTexts);
            if (!string.IsNullOrEmpty(mainContent))
            {
                // Create a virtual element for content announcement
                Log.Msg("{NavigatorId}", $"Found content: {mainContent}");
            }

            // Find navigation dots (for carousel position)
            var navPips = GameObject.FindObjectsOfType<Button>()
                .Where(b => b.gameObject.activeInHierarchy && b.gameObject.name.Contains("NavPip"))
                .OrderBy(b => b.transform.position.x)
                .ToList();

            int currentPage = 1;
            int totalPages = navPips.Count;
            for (int i = 0; i < navPips.Count; i++)
            {
                // Try to detect which page is currently selected
                var navPip = navPips[i];
                var images = navPip.GetComponentsInChildren<Image>();
                bool isSelected = images.Any(img => img.color.a > 0.8f);
                if (isSelected) currentPage = i + 1;
            }

            if (totalPages > 0)
            {
                Log.Msg("{NavigatorId}", $"Carousel page {currentPage} of {totalPages}");
            }

            // Find Continue/dismiss button - this is the main actionable element
            FindDismissButtons(addedObjects);

            // Add carousel navigation if multiple pages
            if (totalPages > 1)
            {
                foreach (var pip in navPips)
                {
                    int pageNum = navPips.IndexOf(pip) + 1;
                    AddElement(pip.gameObject, Strings.PageOf(pageNum, totalPages));
                    addedObjects.Add(pip.gameObject);
                }
            }
        }

        private void DiscoverRewardElements(HashSet<GameObject> addedObjects)
        {
            // Find reward cards first - they should be the main navigable content
            FindRewardCards(addedObjects);

            // Find dismiss/claim buttons
            FindDismissButtons(addedObjects);
        }

        /// <summary>
        /// Find reward cards displayed on the rewards screen.
        /// These cards aren't buttons but should be navigable to read card info.
        /// </summary>
        private void FindRewardCards(HashSet<GameObject> addedObjects)
        {
            Log.Msg("{NavigatorId}", $"Searching for reward cards...");

            // Find the rewards content controller
            var rewardsController = GameObject.Find("ContentController - Rewards_Desktop_16x9(Clone)");
            if (rewardsController == null)
            {
                Log.Msg("{NavigatorId}", $"No rewards controller found");
                return;
            }

            // Search for card elements within the rewards controller
            var cardPrefabs = new List<GameObject>();
            foreach (var transform in rewardsController.GetComponentsInChildren<Transform>(false))
            {
                if (transform == null || !transform.gameObject.activeInHierarchy)
                    continue;

                string name = transform.name;

                // Card patterns - CDC is the card data context, MetaCardView is the card display
                if (name.Contains("CDC") ||
                    name.Contains("MetaCardView") ||
                    name.Contains("CardReward") ||
                    name.Contains("CardAnchor") ||
                    name.Contains("RewardCard") ||
                    name.Contains("CardPrefab"))
                {
                    // Skip if it's a child of something we already found
                    bool isChildOfExisting = cardPrefabs.Any(existing =>
                        transform.IsChildOf(existing.transform));
                    if (isChildOfExisting) continue;

                    // Skip if parent is already in the list (prefer parent)
                    bool parentExists = cardPrefabs.RemoveAll(existing =>
                        existing.transform.IsChildOf(transform)) > 0;

                    if (!addedObjects.Contains(transform.gameObject))
                    {
                        Log.Msg("{NavigatorId}", $"Found potential card: {name}");
                        cardPrefabs.Add(transform.gameObject);
                    }
                }
            }

            if (cardPrefabs.Count == 0)
            {
                Log.Msg("{NavigatorId}", $"No reward cards found");
                return;
            }

            // Sort cards by X position (left to right)
            cardPrefabs = cardPrefabs.OrderBy(c => c.transform.position.x).ToList();
            Log.Msg("{NavigatorId}", $"Found {cardPrefabs.Count} reward card(s)");

            int cardNum = 1;
            foreach (var cardPrefab in cardPrefabs)
            {
                // Extract card info using CardDetector
                var cardInfo = CardDetector.ExtractCardInfo(cardPrefab);
                string cardName = cardInfo.IsValid ? cardInfo.Name : "Unknown card";

                // Build label with card number if multiple cards
                string label = cardPrefabs.Count > 1
                    ? $"Card {cardNum}: {cardName}"
                    : $"Unlocked card: {cardName}";

                // Add type line if available
                if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                Log.Msg("{NavigatorId}", $"Adding reward card: {label}");
                AddElement(cardPrefab, label);
                addedObjects.Add(cardPrefab);
                cardNum++;
            }
        }

        private void DiscoverGenericOverlayElements(HashSet<GameObject> addedObjects)
        {
            // Find all interactive elements in the overlay
            var buttons = GameObject.FindObjectsOfType<Button>()
                .Where(b => b.gameObject.activeInHierarchy && b.interactable)
                .ToList();

            foreach (var button in buttons)
            {
                if (addedObjects.Contains(button.gameObject)) continue;

                string label = GetButtonText(button.gameObject, button.name);

                // Skip generic/internal buttons
                if (string.IsNullOrEmpty(label) || label.ToLower().Contains("navpip"))
                    continue;

                AddElement(button.gameObject, $"{label}, button");
                addedObjects.Add(button.gameObject);
            }

            // If no buttons found, look for any clickable elements
            if (_elements.Count == 0)
            {
                FindDismissButtons(addedObjects);
            }
        }

        private void FindDismissButtons(HashSet<GameObject> addedObjects)
        {
            // Look for common dismiss button patterns
            var dismissPatterns = new[] {
                "Return to Arena", "Continue", "Close", "Dismiss", "OK", "Got it",
                "MainButton", "MainButtonOutline", "Button_TopBarDismiss"
            };

            var allButtons = GameObject.FindObjectsOfType<Button>()
                .Where(b => b.gameObject.activeInHierarchy && b.interactable)
                .ToList();

            // Also check for EventTriggers (some buttons use EventTrigger instead of Button)
            var eventTriggers = GameObject.FindObjectsOfType<UnityEngine.EventSystems.EventTrigger>()
                .Where(et => et.gameObject.activeInHierarchy)
                .ToList();

            // First pass: look for buttons with dismiss-like text
            foreach (var button in allButtons)
            {
                if (addedObjects.Contains(button.gameObject)) continue;

                string buttonText = GetButtonText(button.gameObject, null);
                string buttonName = button.gameObject.name;

                bool isDismissButton = dismissPatterns.Any(p =>
                    (!string.IsNullOrEmpty(buttonText) && buttonText.Contains(p)) ||
                    buttonName.Contains(p));

                if (isDismissButton)
                {
                    string label = !string.IsNullOrEmpty(buttonText) ? buttonText : CleanButtonName(buttonName);
                    AddElement(button.gameObject, $"{label}, button");
                    addedObjects.Add(button.gameObject);
                }
            }

            // Check EventTriggers too
            foreach (var trigger in eventTriggers)
            {
                if (addedObjects.Contains(trigger.gameObject)) continue;

                string objName = trigger.gameObject.name;
                bool isDismissButton = dismissPatterns.Any(p => objName.Contains(p));

                if (isDismissButton)
                {
                    string buttonText = GetButtonText(trigger.gameObject, null);
                    string label = !string.IsNullOrEmpty(buttonText) ? buttonText : CleanButtonName(objName);

                    // Skip the blocker itself
                    if (label.ToLower().Contains("blocker")) continue;

                    AddElement(trigger.gameObject, $"{label}, button");
                    addedObjects.Add(trigger.gameObject);
                }
            }
        }

        private string ExtractMainContent(List<TMPro.TMP_Text> texts)
        {
            // Look for title/header text
            foreach (var text in texts)
            {
                string objName = text.gameObject.name.ToLower();
                if (objName.Contains("title") || objName.Contains("header") || objName.Contains("headline"))
                {
                    string content = CleanText(text.text);
                    if (!string.IsNullOrEmpty(content) && content.Length > 2)
                        return content;
                }
            }

            // Fallback: find the largest/most prominent text
            var sortedBySize = texts
                .Where(t => !string.IsNullOrEmpty(t.text?.Trim()) && t.text.Length > 3)
                .OrderByDescending(t => t.fontSize)
                .ToList();

            if (sortedBySize.Count > 0)
            {
                return CleanText(sortedBySize[0].text);
            }

            return null;
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = UITextExtractor.StripRichText(text).Trim();
            if (text == "\u200B") return null;
            return text;
        }

        private string CleanButtonName(string name)
        {
            name = name.Replace("_", " ").Replace("Button", "").Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            if (name.StartsWith("Main ")) name = name.Substring(5);
            return string.IsNullOrEmpty(name) ? "Continue" : name;
        }

        protected override string GetActivationAnnouncement()
        {
            string countInfo = _elements.Count > 1 ? $" {_elements.Count} items." : "";

            // Try to include content summary for What's New
            if (_overlayType == "WhatsNew")
            {
                string core = $"{ScreenName} overlay.{countInfo}".TrimEnd();
                return Strings.WithHint(core, "NavigateHint");
            }

            string coreDefault = $"{ScreenName}.{countInfo}".TrimEnd();
            return Strings.WithHint(coreDefault, "NavigateHint");
        }

        public override void OnSceneChanged(string sceneName)
        {
            // Overlays might persist across some scene changes, but we should recheck
            if (_isActive)
            {
                // Verify overlay is still present
                var blocker = GameObject.Find("Background_ClickBlocker");
                if (blocker == null || !blocker.activeInHierarchy)
                {
                    Deactivate();
                }
            }
        }

        protected override bool ValidateElements()
        {
            // Check if overlay is still present
            if (_overlayBlocker == null || !_overlayBlocker.activeInHierarchy)
            {
                Log.Msg("{NavigatorId}", $"Overlay dismissed");
                return false;
            }

            return base.ValidateElements();
        }
    }
}
