using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string BrowserTypeViewDismiss = "ViewDismiss";

        // Button name patterns for detection
        public static readonly string[] ButtonPatterns = { "Button", "Accept", "Confirm", "Cancel", "Done", "Keep", "Submit", "Yes", "No", "Mulligan" };
        public static readonly string[] ConfirmPatterns = { "Confirm", "Accept", "Done", "Submit", "OK", "Yes", "Keep", "Primary" };
        public static readonly string[] CancelPatterns = { "Cancel", "No", "Back", "Close", "Dismiss", "Secondary" };

        // Friendly browser name mappings now handled by Strings.GetFriendlyBrowserName()

        #endregion

        #region Cache

        private static BrowserInfo _cachedBrowserInfo;
        private static float _lastScanTime;
        private const float ScanInterval = 0.1f; // Only scan every 100ms

        // Track discovered browser types for one-time logging
        private static readonly HashSet<string> _loggedBrowserTypes = new HashSet<string>();

        // Debug mode - set of browser types to dump detailed debug info for
        private static readonly HashSet<string> _debugEnabledBrowsers = new HashSet<string>();

        #endregion

        #region Debug Control

        /// <summary>
        /// Enable comprehensive debug logging for a specific browser type.
        /// Use this when investigating browser activation issues.
        /// </summary>
        /// <param name="browserType">Browser type constant (e.g., BrowserTypeWorkflow, BrowserTypeScry)</param>
        public static void EnableDebugForBrowser(string browserType)
        {
            _debugEnabledBrowsers.Add(browserType);
            MelonLogger.Msg($"[BrowserDetector] Debug ENABLED for browser type: {browserType}");
        }

        /// <summary>
        /// Disable debug logging for a specific browser type.
        /// </summary>
        public static void DisableDebugForBrowser(string browserType)
        {
            _debugEnabledBrowsers.Remove(browserType);
            MelonLogger.Msg($"[BrowserDetector] Debug DISABLED for browser type: {browserType}");
        }

        /// <summary>
        /// Check if debug is enabled for a browser type.
        /// </summary>
        public static bool IsDebugEnabled(string browserType)
        {
            return _debugEnabledBrowsers.Contains(browserType);
        }

        /// <summary>
        /// Clear all debug flags.
        /// </summary>
        public static void DisableAllDebug()
        {
            _debugEnabledBrowsers.Clear();
            MelonLogger.Msg($"[BrowserDetector] All browser debug DISABLED");
        }

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
            return Models.Strings.GetFriendlyBrowserName(typeName);
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

            // Priority 1: Scaffold (skip mulligan without buttons)
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
            // Language-agnostic: detect by presence of ConfirmWidgetButton sibling,
            // not by matching action text keywords.
            if (workflowBrowsers.Count > 0)
            {
                var actionButtons = new List<GameObject>();
                foreach (var wb in workflowBrowsers)
                {
                    // Check if this WorkflowBrowser has a ConfirmWidgetButton sibling.
                    // Real workflow prompts (activate ability, sacrifice, pay mana) have one.
                    // Noise WorkflowBrowser objects (opponent name display) do not.
                    GameObject confirmButton = FindConfirmWidgetButton(wb);
                    if (confirmButton != null)
                    {
                        string text = UITextExtractor.GetText(wb);
                        actionButtons.Add(confirmButton);
                        MelonLogger.Msg($"[BrowserDetector] Found WorkflowBrowser with ConfirmWidgetButton: '{text}'");

                        // Debug dump if enabled
                        if (IsDebugEnabled(BrowserTypeWorkflow) && !_loggedBrowserTypes.Contains("WorkflowBrowserDump"))
                        {
                            _loggedBrowserTypes.Add("WorkflowBrowserDump");
                            DumpWorkflowBrowserDebug(wb, text);
                        }
                    }
                }

                if (actionButtons.Count > 0)
                {
                    if (!_loggedBrowserTypes.Contains(BrowserTypeWorkflow))
                    {
                        _loggedBrowserTypes.Add(BrowserTypeWorkflow);
                        MelonLogger.Msg($"[BrowserDetector] Found WorkflowBrowser with {actionButtons.Count} action buttons");
                    }
                    return new BrowserInfo
                    {
                        IsActive = true,
                        BrowserType = BrowserTypeWorkflow,
                        BrowserGameObject = actionButtons[0],
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
        /// Comprehensive debug dump of WorkflowBrowser structure.
        /// </summary>
        private static void DumpWorkflowBrowserDebug(GameObject wb, string actionText)
        {
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            MelonLogger.Msg($"[BrowserDetector] ========== WorkflowBrowser FULL DEBUG ==========");
            MelonLogger.Msg($"[BrowserDetector] GameObject: {wb.name}");
            MelonLogger.Msg($"[BrowserDetector] Action text: '{actionText}'");
            MelonLogger.Msg($"[BrowserDetector] Full path: {MenuDebugHelper.GetGameObjectPath(wb)}");
            MelonLogger.Msg($"[BrowserDetector] Active: {wb.activeInHierarchy}");

            // Components on WorkflowBrowser itself
            MelonLogger.Msg($"[BrowserDetector] --- Components on WorkflowBrowser ---");
            foreach (var comp in wb.GetComponents<Component>())
            {
                if (comp == null) continue;
                var compType = comp.GetType();
                MelonLogger.Msg($"[BrowserDetector]   Component: {compType.FullName}");

                // Check for click-related interfaces
                if (comp is UnityEngine.EventSystems.IPointerClickHandler)
                    MelonLogger.Msg($"[BrowserDetector]     ^ Has IPointerClickHandler!");
                if (comp is UnityEngine.EventSystems.IPointerDownHandler)
                    MelonLogger.Msg($"[BrowserDetector]     ^ Has IPointerDownHandler!");
                if (comp is UnityEngine.EventSystems.ISubmitHandler)
                    MelonLogger.Msg($"[BrowserDetector]     ^ Has ISubmitHandler!");

                // For MonoBehaviours, log interesting methods
                if (comp is MonoBehaviour mb)
                {
                    var methods = compType.GetMethods(flags);
                    foreach (var method in methods)
                    {
                        string methodName = method.Name.ToLower();
                        if (methodName.Contains("click") || methodName.Contains("submit") ||
                            methodName.Contains("activate") || methodName.Contains("confirm") ||
                            methodName.Contains("select") || methodName.Contains("execute") ||
                            methodName.Contains("invoke") || methodName.Contains("action"))
                        {
                            MelonLogger.Msg($"[BrowserDetector]     Method: {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
                        }
                    }

                    // Check for UnityEvent fields (onClick, onSubmit, etc.)
                    var fields = compType.GetFields(flags);
                    foreach (var field in fields)
                    {
                        if (field.FieldType.Name.Contains("UnityEvent") || field.FieldType.Name.Contains("Action"))
                        {
                            MelonLogger.Msg($"[BrowserDetector]     Event field: {field.Name} ({field.FieldType.Name})");
                        }
                    }
                }

                // Check for Graphic raycast target
                if (comp is UnityEngine.UI.Graphic graphic)
                {
                    MelonLogger.Msg($"[BrowserDetector]     Raycast target: {graphic.raycastTarget}");
                }
            }

            // Check for EventTrigger
            var eventTrigger = wb.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger != null)
            {
                MelonLogger.Msg($"[BrowserDetector]   EventTrigger entries: {eventTrigger.triggers.Count}");
                foreach (var trigger in eventTrigger.triggers)
                {
                    MelonLogger.Msg($"[BrowserDetector]     - {trigger.eventID}: {trigger.callback.GetPersistentEventCount()} listeners");
                }
            }

            // All children with details
            MelonLogger.Msg($"[BrowserDetector] --- Children ---");
            foreach (Transform child in wb.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.gameObject == wb) continue;

                string activeStr = child.gameObject.activeInHierarchy ? "" : " [INACTIVE]";
                var childComps = child.GetComponents<Component>();
                var compNames = new List<string>();
                bool hasClickHandler = false;
                bool hasButton = false;
                bool hasEventTrigger = false;

                foreach (var c in childComps)
                {
                    if (c == null) continue;
                    compNames.Add(c.GetType().Name);

                    if (c is UnityEngine.EventSystems.IPointerClickHandler) hasClickHandler = true;
                    if (c is UnityEngine.UI.Button) hasButton = true;
                    if (c is UnityEngine.EventSystems.EventTrigger) hasEventTrigger = true;
                }

                string flags_str = "";
                if (hasClickHandler) flags_str += " [CLICKABLE]";
                if (hasButton) flags_str += " [BUTTON]";
                if (hasEventTrigger) flags_str += " [EVENTTRIGGER]";

                string childText = UITextExtractor.GetText(child.gameObject);
                if (!string.IsNullOrEmpty(childText) && childText.Length > 50)
                    childText = childText.Substring(0, 50) + "...";

                MelonLogger.Msg($"[BrowserDetector]   {child.name}{activeStr}{flags_str}: [{string.Join(", ", compNames)}] text='{childText}'");
            }

            // Check parent for workflow controller
            MelonLogger.Msg($"[BrowserDetector] --- Parent hierarchy (looking for controllers) ---");
            Transform parent = wb.transform.parent;
            int level = 0;
            while (parent != null && level < 5)
            {
                var parentComps = parent.GetComponents<MonoBehaviour>();
                foreach (var mb in parentComps)
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName.Contains("Workflow") || typeName.Contains("Controller") ||
                        typeName.Contains("Browser") || typeName.Contains("Action"))
                    {
                        MelonLogger.Msg($"[BrowserDetector]   Parent[{level}] {parent.name}: {typeName}");

                        // Log submit/confirm methods
                        var methods = mb.GetType().GetMethods(flags);
                        foreach (var method in methods)
                        {
                            string methodName = method.Name.ToLower();
                            if (methodName.Contains("submit") || methodName.Contains("confirm") ||
                                methodName.Contains("execute") || methodName.Contains("complete"))
                            {
                                MelonLogger.Msg($"[BrowserDetector]     -> Method: {method.Name}");
                            }
                        }
                    }
                }
                parent = parent.parent;
                level++;
            }

            // Look for any workflow-related objects in scene
            MelonLogger.Msg($"[BrowserDetector] --- Scene workflow objects ---");
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.name.Contains("AutoTap") || go.name.Contains("ManaPayment") ||
                    (go.name.Contains("Workflow") && go.name != "WorkflowBrowser"))
                {
                    var mbs = go.GetComponents<MonoBehaviour>();
                    foreach (var mb in mbs)
                    {
                        if (mb == null) continue;
                        MelonLogger.Msg($"[BrowserDetector]   {go.name}: {mb.GetType().FullName}");
                    }
                }
            }

            MelonLogger.Msg($"[BrowserDetector] ========== END WorkflowBrowser DEBUG ==========");
        }

        /// <summary>
        /// Finds the first clickable child inside a container (Button, EventTrigger, etc.).
        /// Used for WorkflowBrowser which is a container with clickable children.
        /// Also searches sibling hierarchies for ConfirmWidgetButton since the actual
        /// clickable button may be a sibling of WorkflowBrowser, not a child.
        /// </summary>
        /// <summary>
        /// Checks if a WorkflowBrowser has a ConfirmWidgetButton sibling.
        /// This is the structural marker that distinguishes real workflow prompts
        /// (ability activation, sacrifice, mana payment) from noise WorkflowBrowser
        /// objects that always exist in the scene. Language-agnostic.
        /// </summary>
        private static GameObject FindConfirmWidgetButton(GameObject workflowBrowser)
        {
            if (workflowBrowser == null || workflowBrowser.transform.parent == null)
                return null;

            var parent = workflowBrowser.transform.parent;
            foreach (Transform sibling in parent)
            {
                if (sibling == null || sibling.gameObject == workflowBrowser) continue;
                if (!sibling.gameObject.activeInHierarchy) continue;

                // Search sibling and its descendants for ConfirmWidgetButton
                foreach (Transform descendant in sibling.GetComponentsInChildren<Transform>(true))
                {
                    if (descendant == null || !descendant.gameObject.activeInHierarchy) continue;

                    string name = descendant.name;
                    if (name.Contains("ConfirmWidgetButton") || name.Contains("ConfirmButton"))
                    {
                        var btn = descendant.GetComponent<UnityEngine.UI.Button>();
                        if (btn != null && btn.interactable)
                            return descendant.gameObject;
                    }
                }

                // Also check ConfirmWidget container siblings
                if (sibling.name.Contains("ConfirmWidget"))
                {
                    foreach (Transform descendant in sibling.GetComponentsInChildren<Transform>(true))
                    {
                        if (descendant == null || !descendant.gameObject.activeInHierarchy) continue;
                        var btn = descendant.GetComponent<UnityEngine.UI.Button>();
                        if (btn != null && btn.interactable)
                            return descendant.gameObject;
                    }
                }
            }

            return null;
        }

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

            // Not found in children - search sibling hierarchies
            // The ConfirmWidgetButton is often a sibling of WorkflowBrowser under the same parent
            if (container.transform.parent != null)
            {
                var parent = container.transform.parent;
                foreach (Transform sibling in parent)
                {
                    if (sibling == null || sibling.gameObject == container) continue;
                    if (!sibling.gameObject.activeInHierarchy) continue;

                    // Look for ConfirmWidgetButton or similar in sibling hierarchy
                    foreach (Transform descendant in sibling.GetComponentsInChildren<Transform>(true))
                    {
                        if (descendant == null || !descendant.gameObject.activeInHierarchy) continue;

                        // Priority: look for ConfirmWidgetButton component by name
                        string name = descendant.name;
                        if (name.Contains("ConfirmWidgetButton") || name.Contains("ConfirmButton"))
                        {
                            var btn = descendant.GetComponent<UnityEngine.UI.Button>();
                            if (btn != null && btn.interactable)
                            {
                                MelonLogger.Msg($"[BrowserDetector] Found ConfirmWidgetButton in sibling: {name}");
                                return descendant.gameObject;
                            }
                        }

                        // Also check for any Button in ConfirmWidget sibling
                        if (sibling.name.Contains("ConfirmWidget"))
                        {
                            var btn = descendant.GetComponent<UnityEngine.UI.Button>();
                            if (btn != null && btn.interactable)
                            {
                                MelonLogger.Msg($"[BrowserDetector] Found Button in ConfirmWidget sibling: {descendant.name}");
                                return descendant.gameObject;
                            }
                        }
                    }
                }
            }

            return null;
        }

        #endregion
    }
}
