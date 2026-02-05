using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Information about a detected browser.
    /// </summary>
    public class BrowserInfo
    {
        public bool IsActive { get; set; }
        public string BrowserType { get; set; }
        public GameObject BrowserGameObject { get; set; }
        public bool IsZoneBased => IsScryLike || IsLondon;
        public bool IsScryLike { get; set; }
        public bool IsLondon { get; set; }
        public bool IsMulligan { get; set; }
        public bool IsWorkflow { get; set; }

        // For workflow browsers, stores all workflow action buttons found
        public List<GameObject> WorkflowButtons { get; set; }

        public static BrowserInfo None => new BrowserInfo { IsActive = false };
    }

    /// <summary>
    /// Static utility for detecting browser GameObjects and extracting browser properties.
    /// Follows the same pattern as CardDetector - stateless detection with caching.
    /// </summary>
    public static class BrowserDetector
    {
        #region Constants

        // Button names
        public const string ButtonKeep = "KeepButton";
        public const string ButtonMulligan = "MulliganButton";
        public const string ButtonSubmit = "SubmitButton";
        public const string PromptButtonPrimaryPrefix = "PromptButton_Primary";
        public const string PromptButtonSecondaryPrefix = "PromptButton_Secondary";

        // Card holder names
        public const string HolderDefault = "BrowserCardHolder_Default";
        public const string HolderViewDismiss = "BrowserCardHolder_ViewDismiss";

        // Browser scaffold prefix
        private const string ScaffoldPrefix = "BrowserScaffold_";

        // WorkflowBrowser detection
        private const string WorkflowBrowserName = "WorkflowBrowser";

        // Browser type names
        public const string BrowserTypeMulligan = "Mulligan";
        public const string BrowserTypeOpeningHand = "OpeningHand";
        public const string BrowserTypeLondon = "London";
        public const string BrowserTypeWorkflow = "Workflow";

        // Button name patterns for detection
        public static readonly string[] ButtonPatterns = { "Button", "Accept", "Confirm", "Cancel", "Done", "Keep", "Submit", "Yes", "No", "Mulligan" };
        public static readonly string[] ConfirmPatterns = { "Confirm", "Accept", "Done", "Submit", "OK", "Yes", "Keep", "Primary" };
        public static readonly string[] CancelPatterns = { "Cancel", "No", "Back", "Close", "Secondary" };

        // Friendly browser name mappings (keyword -> display name)
        private static readonly Dictionary<string, string> FriendlyBrowserNames = new Dictionary<string, string>
        {
            // Library manipulation
            { "Scryish", "Scry" },
            { "Scry", "Scry" },
            { "Surveil", "Surveil" },
            { "ReadAhead", "Read ahead" },
            { "LibrarySideboard", "Search library" },
            // Opening hand
            { "London", "Mulligan" },
            { "Mulligan", "Mulligan" },
            { "OpeningHand", "Opening hand" },
            // Card ordering
            { "OrderCards", "Order cards" },
            { "SplitCards", "Split cards into piles" },
            // Combat
            { "AssignDamage", "Assign damage" },
            { "Attachment", "View attachments" },
            // Selection
            { "SelectCards", "Select cards" },
            { "SelectGroup", "Select group" },
            { "SelectMana", "Choose mana type" },
            { "Keyword", "Choose keyword" },
            // Special
            { "Dungeon", "Choose dungeon room" },
            { "Mutate", "Mutate choice" },
            { "YesNo", "Choose yes or no" },
            { "Optional", "Optional action" },
            { "Informational", "Information" },
            // Workflow (ability activation, mana payment, etc.)
            { "Workflow", "Choose action" }
        };

        #endregion

        #region Cache

        private static BrowserInfo _cachedBrowserInfo;
        private static float _lastScanTime;
        private const float ScanInterval = 0.1f; // Only scan every 100ms

        // Track discovered browser types for one-time logging
        private static readonly HashSet<string> _loggedBrowserTypes = new HashSet<string>();

        #endregion

        #region Public API

        /// <summary>
        /// Finds an active browser in the scene.
        /// Results are cached to reduce expensive scene scans.
        /// </summary>
        public static BrowserInfo FindActiveBrowser()
        {
            float currentTime = Time.time;

            // Return cached result if still valid
            if (currentTime - _lastScanTime < ScanInterval && _cachedBrowserInfo != null)
            {
                // Validate cache - check if cached browser is still valid
                if (_cachedBrowserInfo.IsActive && _cachedBrowserInfo.BrowserGameObject != null &&
                    _cachedBrowserInfo.BrowserGameObject.activeInHierarchy)
                {
                    // For mulligan browsers, verify buttons are still present
                    if (_cachedBrowserInfo.IsMulligan && !IsMulliganBrowserVisible())
                    {
                        InvalidateCache();
                        return BrowserInfo.None;
                    }

                    // For all browsers, verify cards or prompt buttons still exist
                    if (!IsBrowserStillValid())
                    {
                        InvalidateCache();
                        return BrowserInfo.None;
                    }

                    return _cachedBrowserInfo;
                }
            }

            _lastScanTime = currentTime;

            // Perform scan
            var result = ScanForBrowser();
            _cachedBrowserInfo = result;
            return result;
        }

        /// <summary>
        /// Checks if the cached browser is still valid.
        /// For scaffold browsers: checks if scaffold is still active.
        /// For CardBrowserCardHolder: checks if DEFAULT holder has cards.
        /// </summary>
        private static bool IsBrowserStillValid()
        {
            if (_cachedBrowserInfo == null) return false;

            // For scaffold-based browsers (Scry, YesNo, etc.), the scaffold must still be present
            if (_cachedBrowserInfo.BrowserType != "CardBrowserCardHolder")
            {
                // Scaffold browsers validated by BrowserGameObject.activeInHierarchy check already
                return true;
            }

            // For CardBrowserCardHolder browsers, only check DEFAULT holder
            // ViewDismiss only makes sense with a scaffold (it's the "put on bottom" zone)
            // Cards in ViewDismiss without a scaffold are just animation remnants
            var defaultHolder = FindActiveGameObject(HolderDefault);
            if (defaultHolder != null && CountCardsInContainer(defaultHolder) > 0)
            {
                return true;
            }

            // No cards in default holder - browser is closed
            return false;
        }

        /// <summary>
        /// Invalidates the browser cache, forcing a fresh scan on next call.
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedBrowserInfo = null;
            _lastScanTime = 0f;
        }

        /// <summary>
        /// Checks if a browser type is mulligan-related (OpeningHand or Mulligan).
        /// </summary>
        public static bool IsMulliganBrowser(string browserType)
        {
            return browserType == BrowserTypeMulligan || browserType == BrowserTypeOpeningHand;
        }

        /// <summary>
        /// Checks if a browser type is London mulligan.
        /// </summary>
        public static bool IsLondonBrowser(string browserType)
        {
            return browserType == BrowserTypeLondon;
        }

        /// <summary>
        /// Checks if a browser type supports two-zone navigation (Scry, Surveil, etc.).
        /// These browsers have a "keep on top" and "put on bottom" zone.
        /// </summary>
        public static bool IsScryLikeBrowser(string browserType)
        {
            if (string.IsNullOrEmpty(browserType)) return false;
            return browserType.Contains("Scry") ||
                   browserType.Contains("Surveil") ||
                   browserType.Contains("ReadAhead");
        }

        /// <summary>
        /// Checks if a browser type uses zone-based navigation (Scry/Surveil OR London).
        /// </summary>
        public static bool IsZoneBasedBrowser(string browserType)
        {
            return IsScryLikeBrowser(browserType) || IsLondonBrowser(browserType);
        }

        /// <summary>
        /// Checks if a browser type is a workflow browser (ability activation, mana payment).
        /// </summary>
        public static bool IsWorkflowBrowser(string browserType)
        {
            return browserType == BrowserTypeWorkflow;
        }

        /// <summary>
        /// Gets a user-friendly name for the browser type.
        /// </summary>
        public static string GetFriendlyBrowserName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "Browser";

            // Check each keyword in the dictionary
            foreach (var kvp in FriendlyBrowserNames)
            {
                if (typeName.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            return "Browser";
        }

        /// <summary>
        /// Checks if a card name is valid (not empty, not unknown).
        /// </summary>
        public static bool IsValidCardName(string cardName)
        {
            return !string.IsNullOrEmpty(cardName) &&
                   !cardName.Contains("Unknown") &&
                   !cardName.Contains("unknown") &&
                   cardName != "Card";
        }

        #endregion

        #region Detection Implementation

        /// <summary>
        /// Performs the actual browser scan.
        /// </summary>
        private static BrowserInfo ScanForBrowser()
        {
            GameObject scaffoldCandidate = null;
            string scaffoldType = null;
            GameObject cardHolderCandidate = null;
            int cardHolderCardCount = 0;
            bool hasMulliganButtons = false;
            List<GameObject> workflowBrowsers = new List<GameObject>();

            // Single pass through all GameObjects
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                string goName = go.name;

                // Check for mulligan buttons
                if (goName == ButtonKeep || goName == ButtonMulligan)
                {
                    hasMulliganButtons = true;
                }

                // Priority 1: Browser scaffold pattern (BrowserScaffold_*)
                if (scaffoldCandidate == null && goName.StartsWith(ScaffoldPrefix, StringComparison.Ordinal))
                {
                    scaffoldCandidate = go;
                    scaffoldType = ExtractBrowserTypeFromScaffold(goName);
                }

                // Priority 2: CardBrowserCardHolder component (fallback) - only from DEFAULT holder
                // ViewDismiss holder only makes sense with a scaffold present
                if (cardHolderCandidate == null && goName == HolderDefault)
                {
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                        {
                            int cardCount = CountCardsInContainer(go);
                            if (cardCount > 0)
                            {
                                cardHolderCandidate = go;
                                cardHolderCardCount = cardCount;
                            }
                            break;
                        }
                    }
                }

                // Priority 3: WorkflowBrowser (ability activation, mana payment choices)
                if (goName == WorkflowBrowserName)
                {
                    workflowBrowsers.Add(go);
                }
            }

            // Return results in priority order

            // Priority 1: Scaffold (skip if mulligan scaffold without buttons)
            if (scaffoldCandidate != null)
            {
                bool isMulligan = IsMulliganBrowser(scaffoldType);
                if (!isMulligan || hasMulliganButtons)
                {
                    LogBrowserDiscovery(scaffoldCandidate.name, scaffoldType);
                    return new BrowserInfo
                    {
                        IsActive = true,
                        BrowserType = scaffoldType,
                        BrowserGameObject = scaffoldCandidate,
                        IsScryLike = IsScryLikeBrowser(scaffoldType),
                        IsLondon = IsLondonBrowser(scaffoldType),
                        IsMulligan = isMulligan
                    };
                }
            }

            // Priority 2: CardBrowserCardHolder (skip if looks like mulligan without buttons)
            if (cardHolderCandidate != null)
            {
                if (cardHolderCardCount < 5 || hasMulliganButtons)
                {
                    if (!_loggedBrowserTypes.Contains("CardBrowserCardHolder"))
                    {
                        _loggedBrowserTypes.Add("CardBrowserCardHolder");
                        MelonLogger.Msg($"[BrowserDetector] Found CardBrowserCardHolder: {cardHolderCandidate.name} with {cardHolderCardCount} cards");
                    }
                    return new BrowserInfo
                    {
                        IsActive = true,
                        BrowserType = "CardBrowserCardHolder",
                        BrowserGameObject = cardHolderCandidate,
                        IsScryLike = false,
                        IsLondon = false,
                        IsMulligan = false
                    };
                }
            }

            // Priority 3: WorkflowBrowser (ability activation, mana payment)
            if (workflowBrowsers.Count > 0)
            {
                // Find workflow buttons that have meaningful text (not empty, not just card names)
                // WorkflowBrowser is a container - we need to find clickable children inside it
                var actionButtons = new List<GameObject>();
                foreach (var wb in workflowBrowsers)
                {
                    string text = UITextExtractor.GetText(wb);
                    // Look for action-related text patterns (localized)
                    // German: "aktivieren" (activate), "bezahlen" (pay), "abbrechen" (cancel)
                    // English: "activate", "pay", "cancel"
                    if (!string.IsNullOrEmpty(text) &&
                        (text.ToLowerInvariant().Contains("aktiv") ||
                         text.ToLowerInvariant().Contains("activate") ||
                         text.ToLowerInvariant().Contains("pay") ||
                         text.ToLowerInvariant().Contains("bezahl") ||
                         text.ToLowerInvariant().Contains("cancel") ||
                         text.ToLowerInvariant().Contains("abbrech")))
                    {
                        // Try to find clickable child inside WorkflowBrowser
                        GameObject clickableButton = FindClickableChild(wb);
                        if (clickableButton != null)
                        {
                            actionButtons.Add(clickableButton);
                            MelonLogger.Msg($"[BrowserDetector] Found clickable child '{clickableButton.name}' in WorkflowBrowser with text '{text}'");
                        }
                        else
                        {
                            // Fallback: use the WorkflowBrowser itself
                            actionButtons.Add(wb);
                            MelonLogger.Msg($"[BrowserDetector] No clickable child found, using WorkflowBrowser itself for '{text}'");
                        }
                    }
                }

                if (actionButtons.Count > 0)
                {
                    if (!_loggedBrowserTypes.Contains(BrowserTypeWorkflow))
                    {
                        _loggedBrowserTypes.Add(BrowserTypeWorkflow);
                        MelonLogger.Msg($"[BrowserDetector] Found WorkflowBrowser with {actionButtons.Count} action buttons");
                        foreach (var ab in actionButtons)
                        {
                            string text = UITextExtractor.GetText(ab);
                            MelonLogger.Msg($"[BrowserDetector]   Action: '{text}'");
                        }
                    }
                    return new BrowserInfo
                    {
                        IsActive = true,
                        BrowserType = BrowserTypeWorkflow,
                        BrowserGameObject = actionButtons[0], // Primary action button
                        IsScryLike = false,
                        IsLondon = false,
                        IsMulligan = false,
                        IsWorkflow = true,
                        WorkflowButtons = actionButtons
                    };
                }
            }

            return BrowserInfo.None;
        }

        /// <summary>
        /// Extracts browser type from scaffold name.
        /// E.g., "BrowserScaffold_Scry_Desktop_16x9(Clone)" -> "Scry"
        /// </summary>
        private static string ExtractBrowserTypeFromScaffold(string scaffoldName)
        {
            if (scaffoldName.StartsWith(ScaffoldPrefix))
            {
                string remainder = scaffoldName.Substring(ScaffoldPrefix.Length);
                int underscoreIndex = remainder.IndexOf('_');
                if (underscoreIndex > 0)
                {
                    return remainder.Substring(0, underscoreIndex);
                }
                // If no second underscore, take until ( or end
                int parenIndex = remainder.IndexOf('(');
                if (parenIndex > 0)
                    return remainder.Substring(0, parenIndex);
                return remainder;
            }
            return "Unknown";
        }

        /// <summary>
        /// Checks if the mulligan/opening hand browser UI is actually visible.
        /// </summary>
        private static bool IsMulliganBrowserVisible()
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.name == ButtonKeep || go.name == ButtonMulligan)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Counts cards in a container without creating intermediate lists.
        /// </summary>
        private static int CountCardsInContainer(GameObject container)
        {
            int count = 0;
            foreach (Transform child in container.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.activeInHierarchy && CardDetector.IsCard(child.gameObject))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Logs browser scaffold discovery (once per scaffold name).
        /// </summary>
        private static void LogBrowserDiscovery(string scaffoldName, string scaffoldType)
        {
            if (!_loggedBrowserTypes.Contains(scaffoldName))
            {
                _loggedBrowserTypes.Add(scaffoldName);
                MelonLogger.Msg($"[BrowserDetector] Found browser scaffold: {scaffoldName}, type: {scaffoldType}");
            }
        }

        #endregion

        #region Helper Methods for BrowserNavigator

        /// <summary>
        /// Finds an active GameObject by exact name.
        /// </summary>
        public static GameObject FindActiveGameObject(string exactName)
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go != null && go.activeInHierarchy && go.name == exactName)
                {
                    return go;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds all active GameObjects matching a predicate.
        /// </summary>
        public static List<GameObject> FindActiveGameObjects(Func<GameObject, bool> predicate)
        {
            var results = new List<GameObject>();
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go != null && go.activeInHierarchy && predicate(go))
                {
                    results.Add(go);
                }
            }
            return results;
        }

        /// <summary>
        /// Checks if a GameObject has any clickable component.
        /// </summary>
        public static bool HasClickableComponent(GameObject go)
        {
            if (go.GetComponent<UnityEngine.UI.Button>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.Toggle>() != null) return true;
            if (go.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null) return true;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;
                if (typeName.Contains("Button") || typeName.Contains("Interactable") || typeName.Contains("Clickable"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a button name matches any of the given patterns (case-insensitive).
        /// </summary>
        public static bool MatchesButtonPattern(string buttonName, string[] patterns)
        {
            string nameLower = buttonName.ToLowerInvariant();
            foreach (var pattern in patterns)
            {
                if (nameLower.Contains(pattern.ToLowerInvariant()))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a card is already in the given list (by instance ID).
        /// </summary>
        public static bool IsDuplicateCard(GameObject card, List<GameObject> existingCards)
        {
            if (card == null) return false;
            int instanceId = card.GetInstanceID();
            return existingCards.Exists(c => c != null && c.GetInstanceID() == instanceId);
        }

        /// <summary>
        /// Finds the first clickable child inside a container (Button, EventTrigger, etc.).
        /// Used for WorkflowBrowser which is a container with clickable children.
        /// </summary>
        private static GameObject FindClickableChild(GameObject container)
        {
            if (container == null) return null;

            // First check if the container itself is clickable
            if (HasClickableComponent(container))
            {
                var button = container.GetComponent<UnityEngine.UI.Button>();
                if (button != null && button.interactable)
                {
                    return container;
                }
            }

            // Search children for clickable components
            foreach (Transform child in container.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (child.gameObject == container) continue; // Skip self

                // Check for Button component
                var button = child.GetComponent<UnityEngine.UI.Button>();
                if (button != null && button.interactable)
                {
                    return child.gameObject;
                }

                // Check for EventTrigger
                var eventTrigger = child.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (eventTrigger != null)
                {
                    return child.gameObject;
                }

                // Check for IPointerClickHandler (custom click handlers)
                var clickHandlers = child.GetComponents<UnityEngine.EventSystems.IPointerClickHandler>();
                if (clickHandlers != null && clickHandlers.Length > 0)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        #endregion
    }
}
