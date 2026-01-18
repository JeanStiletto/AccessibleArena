using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Unified navigator for all browser UIs in the duel scene.
    /// Handles: scry, surveil, mulligan, damage assignment, card ordering,
    /// card selection, yes/no choices, dungeon navigation, etc.
    /// </summary>
    public class BrowserNavigator
    {
        private readonly IAnnouncementService _announcer;

        // Browser state
        private bool _isActive;
        private bool _hasAnnouncedEntry;
        private object _activeBrowser;
        private string _browserTypeName;
        private GameObject _browserGameObject;

        // Card navigation
        private List<GameObject> _browserCards = new List<GameObject>();
        private int _currentCardIndex = -1;

        // Button navigation (for non-card browsers like YesNo)
        private List<GameObject> _browserButtons = new List<GameObject>();
        private int _currentButtonIndex = -1;

        // Track discovered browser types for one-time logging
        private static HashSet<string> _loggedBrowserTypes = new HashSet<string>();

        // Cache for browser detection to reduce scene scans
        private object _cachedBrowser;
        private GameObject _cachedBrowserGo;
        private float _lastBrowserScanTime;
        private const float BrowserScanInterval = 0.1f; // Only scan every 100ms

        #region Constants

        // Button names
        private const string ButtonKeep = "KeepButton";
        private const string ButtonMulligan = "MulliganButton";
        private const string ButtonSubmit = "SubmitButton";
        private const string PromptButtonPrimaryPrefix = "PromptButton_Primary";
        private const string PromptButtonSecondaryPrefix = "PromptButton_Secondary";

        // Card holder names
        private const string HolderDefault = "BrowserCardHolder_Default";
        private const string HolderViewDismiss = "BrowserCardHolder_ViewDismiss";

        // Browser scaffold prefix
        private const string ScaffoldPrefix = "BrowserScaffold_";

        // Zone names
        private const string ZoneLocalHand = "LocalHand";

        // Browser type names (used for special-case handling)
        private static class BrowserTypes
        {
            public const string Mulligan = "Mulligan";
            public const string OpeningHand = "OpeningHand";
            public const string London = "London";
        }

        // Button name patterns for detection
        private static readonly string[] ButtonPatterns = { "Button", "Accept", "Confirm", "Cancel", "Done", "Keep", "Submit", "Yes", "No", "Mulligan" };
        private static readonly string[] ConfirmPatterns = { "Confirm", "Accept", "Done", "Submit", "OK", "Yes", "Keep", "Primary" };
        private static readonly string[] CancelPatterns = { "Cancel", "No", "Back", "Close", "Secondary" };

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
            { "Informational", "Information" }
        };

        #endregion

        public BrowserNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Returns true if a browser is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        #region Helper Methods

        /// <summary>
        /// Checks if a browser type is mulligan-related (OpeningHand or Mulligan).
        /// </summary>
        private bool IsMulliganBrowser(string browserType)
        {
            return browserType == BrowserTypes.Mulligan || browserType == BrowserTypes.OpeningHand;
        }

        /// <summary>
        /// Checks if a card name is valid (not empty, not unknown).
        /// </summary>
        private bool IsValidCardName(string cardName)
        {
            return !string.IsNullOrEmpty(cardName) &&
                   !cardName.Contains("Unknown") &&
                   !cardName.Contains("unknown") &&
                   cardName != "Card";
        }

        /// <summary>
        /// Checks if a card is already in the given list (by instance ID).
        /// </summary>
        private bool IsDuplicateCard(GameObject card, List<GameObject> existingCards)
        {
            if (card == null) return false;
            int instanceId = card.GetInstanceID();
            return existingCards.Exists(c => c != null && c.GetInstanceID() == instanceId);
        }

        /// <summary>
        /// Finds an active GameObject by exact name.
        /// </summary>
        private GameObject FindActiveGameObject(string exactName)
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
        private List<GameObject> FindActiveGameObjects(System.Func<GameObject, bool> predicate)
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
        private bool HasClickableComponent(GameObject go)
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
        private bool MatchesButtonPattern(string buttonName, string[] patterns)
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
        /// Tries to click a button matching the given patterns. Returns true if successful.
        /// </summary>
        private bool TryClickButtonByPatterns(string[] patterns, out string clickedLabel)
        {
            clickedLabel = null;

            // First check our discovered buttons
            foreach (var button in _browserButtons)
            {
                if (button == null) continue;
                if (MatchesButtonPattern(button.name, patterns))
                {
                    clickedLabel = UITextExtractor.GetButtonText(button, button.name);
                    var result = UIActivator.SimulatePointerClick(button);
                    if (result.Success)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to click a specific button by exact name. Returns true if successful.
        /// </summary>
        private bool TryClickButtonByName(string buttonName, out string clickedLabel)
        {
            clickedLabel = null;

            // Check discovered buttons first
            foreach (var button in _browserButtons)
            {
                if (button == null) continue;
                if (button.name == buttonName)
                {
                    clickedLabel = UITextExtractor.GetButtonText(button, button.name);
                    var result = UIActivator.SimulatePointerClick(button);
                    if (result.Success)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Clicked {buttonName}: '{clickedLabel}'");
                        return true;
                    }
                }
            }

            // Search scene as fallback
            var go = FindActiveGameObject(buttonName);
            if (go != null)
            {
                clickedLabel = UITextExtractor.GetButtonText(go, go.name);
                var result = UIActivator.SimulatePointerClick(go);
                if (result.Success)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Clicked {buttonName} (scene): '{clickedLabel}'");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to click a PromptButton (Primary or Secondary). Returns true if successful.
        /// </summary>
        private bool TryClickPromptButton(string prefix, out string clickedLabel)
        {
            clickedLabel = null;

            var buttons = FindActiveGameObjects(go => go.name.StartsWith(prefix));
            foreach (var go in buttons)
            {
                var selectable = go.GetComponent<Selectable>();
                if (selectable != null && !selectable.interactable) continue;

                clickedLabel = UITextExtractor.GetButtonText(go, go.name);

                // Skip if it's just a keyboard hint (like "Strg" / "Ctrl")
                if (prefix == PromptButtonSecondaryPrefix && clickedLabel.Length <= 4 && !clickedLabel.Contains(" "))
                {
                    continue;
                }

                var result = UIActivator.SimulatePointerClick(go);
                if (result.Success)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Clicked {prefix}: '{clickedLabel}'");
                    return true;
                }
            }
            return false;
        }

        #endregion

        /// <summary>
        /// The type name of the currently active browser.
        /// </summary>
        public string ActiveBrowserType => _browserTypeName;

        private float _lastSearchLogTime = 0f;

        /// <summary>
        /// Updates browser detection state. Call each frame from DuelNavigator.
        /// </summary>
        public void Update()
        {
            var (browser, browserGo) = FindActiveBrowser();

            if (browser != null)
            {
                // Get the new browser type
                string newBrowserType = null;
                if (browser is BrowserScaffoldInfo scaffoldInfo)
                {
                    newBrowserType = scaffoldInfo.BrowserType;
                }
                else
                {
                    newBrowserType = browser.GetType().Name;
                }

                if (!_isActive)
                {
                    EnterBrowserMode(browser, browserGo);
                }
                // Re-enter if browser type changed (e.g., OpeningHand -> Mulligan)
                else if (newBrowserType != _browserTypeName)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Browser type changed: {_browserTypeName} -> {newBrowserType}");
                    ExitBrowserMode();
                    EnterBrowserMode(browser, browserGo);
                }
            }
            else if (_isActive)
            {
                ExitBrowserMode();
            }

            // Periodic debug: log browser search when DuelAnnouncer says library browser is active
            if (DuelAnnouncer.Instance?.IsLibraryBrowserActive == true && !_isActive)
            {
                float currentTime = Time.time;
                if (currentTime - _lastSearchLogTime > 2f) // Log every 2 seconds
                {
                    _lastSearchLogTime = currentTime;
                    LogBrowserSearch();
                }
            }
        }

        /// <summary>
        /// Debug: logs what browser-like components exist in the scene.
        /// </summary>
        private void LogBrowserSearch()
        {
            MelonLogger.Msg("[BrowserNavigator] === SEARCHING FOR BROWSERS (DuelAnnouncer says browser active) ===");

            int browserCount = 0;

            // Single pass through scene
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                string goName = go.name;

                // Check name for browser-related terms (case-insensitive)
                bool isBrowserRelated =
                    goName.IndexOf("browser", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    goName.IndexOf("scry", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    goName.IndexOf("surveil", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    goName.IndexOf("mulligan", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (goName.IndexOf("cardholder", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                     goName.IndexOf("select", System.StringComparison.OrdinalIgnoreCase) >= 0);

                if (isBrowserRelated)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Found GO: {goName}");
                    browserCount++;

                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp != null)
                        {
                            MelonLogger.Msg($"[BrowserNavigator]   Component: {comp.GetType().FullName}");
                        }
                    }
                }

                // Check components for browser types
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    if (typeName.IndexOf("Browser", System.StringComparison.Ordinal) >= 0)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Found component {typeName} on {goName}");
                        browserCount++;
                    }
                }

                // Check for card containers
                bool isCardContainer =
                    goName.IndexOf("CardHolder", System.StringComparison.Ordinal) >= 0 ||
                    goName.IndexOf("Selection", System.StringComparison.Ordinal) >= 0 ||
                    goName.IndexOf("Prompt", System.StringComparison.Ordinal) >= 0;

                if (isCardContainer)
                {
                    int cardCount = CountCardsInContainer(go);
                    if (cardCount > 0)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Container {goName} has {cardCount} cards");
                    }
                }
            }

            MelonLogger.Msg($"[BrowserNavigator] === END SEARCH (found {browserCount} browser elements) ===");
        }

        /// <summary>
        /// Handles input during browser mode.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Announce browser state on first interaction
            if (!_hasAnnouncedEntry)
            {
                AnnounceBrowserState();
                _hasAnnouncedEntry = true;
            }

            // Tab / Shift+Tab - cycle through items
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                MelonLogger.Msg($"[BrowserNavigator] Tab in browser mode: {_browserCards.Count} cards, {_browserButtons.Count} buttons");
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (_browserCards.Count > 0)
                {
                    if (shift) NavigateToPreviousCard();
                    else NavigateToNextCard();
                }
                else if (_browserButtons.Count > 0)
                {
                    if (shift) NavigateToPreviousButton();
                    else NavigateToNextButton();
                }
                return true;
            }

            // Left/Right arrows - card/button navigation
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_browserCards.Count > 0) NavigateToPreviousCard();
                else if (_browserButtons.Count > 0) NavigateToPreviousButton();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_browserCards.Count > 0) NavigateToNextCard();
                else if (_browserButtons.Count > 0) NavigateToNextButton();
                return true;
            }

            // Up/Down arrows - card details (delegate to CardInfoNavigator)
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                {
                    // Let CardInfoNavigator handle detail navigation
                    var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
                    if (cardNav != null && cardNav.IsActive)
                    {
                        return false; // Let CardInfoNavigator handle it
                    }
                }
                return false;
            }

            // Enter - activate current card or button
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                {
                    ActivateCurrentCard();
                }
                else if (_browserButtons.Count > 0 && _currentButtonIndex >= 0)
                {
                    ActivateCurrentButton();
                }
                return true;
            }

            // Space - confirm/submit (find and click primary button)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ClickConfirmButton();
                return true;
            }

            // Backspace - cancel if possible
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ClickCancelButton();
                return true;
            }

            return false;
        }

        #region Browser Detection

        /// <summary>
        /// Finds an active browser component in the scene.
        /// Uses scaffold pattern detection (BrowserScaffold_*) and CardBrowserCardHolder.
        /// Implements caching to reduce expensive scene scans.
        /// </summary>
        private (object browser, GameObject go) FindActiveBrowser()
        {
            float currentTime = Time.time;

            // Return cached result if still valid
            if (currentTime - _lastBrowserScanTime < BrowserScanInterval)
            {
                // Validate cache - check if cached browser is still valid
                if (_cachedBrowser != null && _cachedBrowserGo != null && _cachedBrowserGo.activeInHierarchy)
                {
                    // For mulligan browsers, verify buttons are still present
                    if (_cachedBrowser is BrowserScaffoldInfo scaffoldInfo && IsMulliganBrowser(scaffoldInfo.BrowserType))
                    {
                        if (!IsMulliganBrowserVisible())
                        {
                            InvalidateBrowserCache();
                            return (null, null);
                        }
                    }
                    return (_cachedBrowser, _cachedBrowserGo);
                }
            }

            _lastBrowserScanTime = currentTime;

            // Perform single scene scan and categorize results
            var result = ScanForBrowser();

            // Cache the result
            _cachedBrowser = result.browser;
            _cachedBrowserGo = result.go;

            return result;
        }

        /// <summary>
        /// Invalidates the browser cache, forcing a fresh scan on next call.
        /// </summary>
        private void InvalidateBrowserCache()
        {
            _cachedBrowser = null;
            _cachedBrowserGo = null;
            _lastBrowserScanTime = 0f;
        }

        /// <summary>
        /// Performs the actual browser scan. Detects browsers via scaffold pattern or CardBrowserCardHolder.
        /// </summary>
        private (object browser, GameObject go) ScanForBrowser()
        {
            GameObject scaffoldCandidate = null;
            string scaffoldType = null;
            GameObject cardHolderCandidate = null;
            Component cardHolderComponent = null;
            int cardHolderCardCount = 0;
            bool hasMulliganButtons = false;

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
                if (scaffoldCandidate == null && goName.StartsWith(ScaffoldPrefix, System.StringComparison.Ordinal))
                {
                    scaffoldCandidate = go;
                    scaffoldType = ExtractBrowserTypeFromScaffold(goName);
                }

                // Priority 2: CardBrowserCardHolder component (fallback)
                if (cardHolderCandidate == null && goName.Contains("Browser"))
                {
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                        {
                            int cardCount = CountCardsInContainer(go);
                            if (cardCount > 0)
                            {
                                cardHolderCandidate = go;
                                cardHolderComponent = comp;
                                cardHolderCardCount = cardCount;
                            }
                            break;
                        }
                    }
                }
            }

            // Return results in priority order

            // Priority 1: Scaffold (skip if mulligan scaffold without buttons)
            if (scaffoldCandidate != null)
            {
                if (!IsMulliganBrowser(scaffoldType) || hasMulliganButtons)
                {
                    LogBrowserDiscovery(scaffoldCandidate.name, scaffoldType);
                    return (new BrowserScaffoldInfo { ScaffoldName = scaffoldCandidate.name, BrowserType = scaffoldType }, scaffoldCandidate);
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
                        MelonLogger.Msg($"[BrowserNavigator] Found CardBrowserCardHolder: {cardHolderCandidate.name} with {cardHolderCardCount} cards");
                    }
                    return (cardHolderComponent, cardHolderCandidate);
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Counts cards in a container without creating intermediate lists.
        /// </summary>
        private int CountCardsInContainer(GameObject container)
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
        private void LogBrowserDiscovery(string scaffoldName, string scaffoldType)
        {
            if (!_loggedBrowserTypes.Contains(scaffoldName))
            {
                _loggedBrowserTypes.Add(scaffoldName);
                MelonLogger.Msg($"[BrowserNavigator] Found browser scaffold: {scaffoldName}, type: {scaffoldType}");
            }
        }

        /// <summary>
        /// Helper class to hold scaffold browser info.
        /// </summary>
        private class BrowserScaffoldInfo
        {
            public string ScaffoldName { get; set; }
            public string BrowserType { get; set; }
        }

        /// <summary>
        /// Extracts browser type from scaffold name.
        /// E.g., "BrowserScaffold_Scry_Desktop_16x9(Clone)" -> "Scry"
        /// </summary>
        private string ExtractBrowserTypeFromScaffold(string scaffoldName)
        {
            // Pattern: BrowserScaffold_{Type}_{Resolution}
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
        /// The scaffold can remain active even after mulligan ends, so we check for actual UI elements.
        /// </summary>
        private bool IsMulliganBrowserVisible()
        {
            // Either Keep or Mulligan button being present means mulligan is still active
            return FindActiveGameObject(ButtonKeep) != null || FindActiveGameObject(ButtonMulligan) != null;
        }

        /// <summary>
        /// Discovery: Logs components on a browser scaffold to find controller APIs.
        /// This helps identify if there's a better way to detect browser state.
        /// </summary>
        private void LogScaffoldComponents(GameObject scaffold, string scaffoldType)
        {
            MelonLogger.Msg($"[BrowserNavigator] === SCAFFOLD DISCOVERY: {scaffoldType} ===");

            // Log all components on the scaffold itself
            foreach (var comp in scaffold.GetComponents<Component>())
            {
                if (comp == null) continue;
                var type = comp.GetType();
                MelonLogger.Msg($"[BrowserNavigator] Component: {type.Name}");

                // Check for useful properties
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var propName = prop.Name;
                    // Look for visibility/state properties
                    if (propName.Contains("IsOpen") || propName.Contains("IsVisible") ||
                        propName.Contains("IsActive") || propName.Contains("IsClosed") ||
                        propName.Contains("State") || propName.Contains("Browser"))
                    {
                        try
                        {
                            var value = prop.GetValue(comp);
                            MelonLogger.Msg($"[BrowserNavigator]   Property: {propName} = {value}");
                        }
                        catch
                        {
                            MelonLogger.Msg($"[BrowserNavigator]   Property: {propName} (could not read)");
                        }
                    }
                }
            }

            // Also check parent for controller components
            if (scaffold.transform.parent != null)
            {
                var parent = scaffold.transform.parent.gameObject;
                MelonLogger.Msg($"[BrowserNavigator] Parent: {parent.name}");
                foreach (var comp in parent.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (typeName.Contains("Browser") || typeName.Contains("Controller") || typeName.Contains("Manager"))
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Parent component: {typeName}");
                    }
                }
            }

            MelonLogger.Msg($"[BrowserNavigator] === END SCAFFOLD DISCOVERY ===");
        }

        /// <summary>
        /// Logs browser details for discovery (once per type).
        /// </summary>
        private void LogBrowserDetails(object browser, string typeName)
        {
            MelonLogger.Msg($"[BrowserNavigator] === NEW BROWSER: {typeName} ===");

            var type = browser.GetType();

            // Log key properties
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var value = prop.GetValue(browser);
                    string valueStr = value?.ToString() ?? "null";
                    if (valueStr.Length > 80) valueStr = valueStr.Substring(0, 80) + "...";
                    MelonLogger.Msg($"[BrowserNavigator]   {prop.Name}: {valueStr}");
                }
                catch { }
            }

            MelonLogger.Msg($"[BrowserNavigator] === END BROWSER ===");
        }

        #endregion

        #region Browser Mode Management

        /// <summary>
        /// Enters browser mode and discovers navigable elements.
        /// </summary>
        private void EnterBrowserMode(object browser, GameObject browserGo)
        {
            _isActive = true;
            _activeBrowser = browser;
            _browserGameObject = browserGo;
            _hasAnnouncedEntry = false;
            _currentCardIndex = -1;
            _currentButtonIndex = -1;

            // Get browser type name based on what we found
            if (browser is BrowserScaffoldInfo scaffoldInfo)
            {
                _browserTypeName = scaffoldInfo.BrowserType;
            }
            else
            {
                _browserTypeName = browser.GetType().Name;
            }

            MelonLogger.Msg($"[BrowserNavigator] Entering browser: {_browserTypeName}");

            // Discover cards and buttons
            DiscoverBrowserElements();
        }

        /// <summary>
        /// Exits browser mode.
        /// </summary>
        private void ExitBrowserMode()
        {
            MelonLogger.Msg($"[BrowserNavigator] Exiting browser: {_browserTypeName}");

            _isActive = false;
            _activeBrowser = null;
            _browserGameObject = null;
            _browserTypeName = null;
            _hasAnnouncedEntry = false;
            _browserCards.Clear();
            _browserButtons.Clear();
            _currentCardIndex = -1;
            _currentButtonIndex = -1;

            // Invalidate cache so next detection starts fresh
            InvalidateBrowserCache();

            // Notify DuelAnnouncer
            DuelAnnouncer.Instance?.OnLibraryBrowserClosed();
        }

        /// <summary>
        /// Discovers cards and buttons in the browser.
        /// </summary>
        private void DiscoverBrowserElements()
        {
            _browserCards.Clear();
            _browserButtons.Clear();

            if (_activeBrowser == null || _browserGameObject == null) return;

            if (_activeBrowser is BrowserScaffoldInfo scaffoldInfo)
            {
                DiscoverScaffoldBrowserElements(scaffoldInfo);
            }
            else
            {
                DiscoverComponentBrowserElements();
            }

            MelonLogger.Msg($"[BrowserNavigator] Found {_browserCards.Count} cards, {_browserButtons.Count} buttons");
        }

        /// <summary>
        /// Discovers elements for scaffold-based browsers (Scry, Surveil, Mulligan, etc.).
        /// </summary>
        private void DiscoverScaffoldBrowserElements(BrowserScaffoldInfo scaffoldInfo)
        {
            bool isOpeningHandOrMulligan = IsMulliganBrowser(scaffoldInfo.BrowserType);

            // Discover cards
            if (isOpeningHandOrMulligan)
            {
                DiscoverMulliganCards();
            }
            DiscoverCardsInHolders();

            // Discover buttons
            DiscoverBrowserRelatedButtons();

            if (isOpeningHandOrMulligan)
            {
                DiscoverMulliganButtons();
            }

            // Fallback: prompt buttons if no other buttons found
            if (_browserCards.Count > 0 && _browserButtons.Count == 0)
            {
                DiscoverPromptButtons();
            }
        }

        /// <summary>
        /// Discovers cards for mulligan/opening hand browsers.
        /// </summary>
        private void DiscoverMulliganCards()
        {
            MelonLogger.Msg($"[BrowserNavigator] Searching for opening hand cards");

            // Search for LocalHand zone
            var localHandZones = FindActiveGameObjects(go => go.name.StartsWith(ZoneLocalHand));
            foreach (var zone in localHandZones)
            {
                MelonLogger.Msg($"[BrowserNavigator] Found LocalHand zone: {zone.name}");
                SearchForCardsInLocalHand(zone);
            }

            // Also search within the browser scaffold
            SearchForCardsInContainer(_browserGameObject, "Scaffold");

            MelonLogger.Msg($"[BrowserNavigator] After opening hand search: {_browserCards.Count} cards found");
        }

        /// <summary>
        /// Discovers cards in BrowserCardHolder containers.
        /// </summary>
        private void DiscoverCardsInHolders()
        {
            var holders = FindActiveGameObjects(go => go.name == HolderDefault || go.name == HolderViewDismiss);

            foreach (var holder in holders)
            {
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (!CardDetector.IsCard(child.gameObject)) continue;

                    string cardName = CardDetector.GetCardName(child.gameObject);

                    if (!IsValidCardName(cardName))
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Skipping invalid card: {child.name} -> {cardName}");
                        continue;
                    }

                    if (!IsDuplicateCard(child.gameObject, _browserCards))
                    {
                        _browserCards.Add(child.gameObject);
                        MelonLogger.Msg($"[BrowserNavigator] Found card in {holder.name}: {child.name} -> {cardName}");
                    }
                }
            }
        }

        /// <summary>
        /// Discovers buttons in browser-related containers.
        /// </summary>
        private void DiscoverBrowserRelatedButtons()
        {
            var browserContainers = FindActiveGameObjects(go =>
                go.name.Contains("Browser") || go.name.Contains("Prompt"));

            foreach (var container in browserContainers)
            {
                FindBrowserButtons(container);
            }
        }

        /// <summary>
        /// Discovers mulligan-specific buttons (Keep/Mulligan).
        /// </summary>
        private void DiscoverMulliganButtons()
        {
            MelonLogger.Msg($"[BrowserNavigator] === MULLIGAN BUTTON DISCOVERY ===");

            var mulliganButtons = FindActiveGameObjects(go =>
                go.name == ButtonKeep || go.name == ButtonMulligan);

            foreach (var button in mulliganButtons)
            {
                if (!_browserButtons.Contains(button))
                {
                    _browserButtons.Add(button);
                    MelonLogger.Msg($"[BrowserNavigator] Added mulligan button: {button.name}");
                }
            }

            MelonLogger.Msg($"[BrowserNavigator] === END MULLIGAN BUTTON DISCOVERY ===");
        }

        /// <summary>
        /// Discovers PromptButton_Primary/Secondary as fallback.
        /// </summary>
        private void DiscoverPromptButtons()
        {
            MelonLogger.Msg($"[BrowserNavigator] No buttons found, searching for PromptButtons...");

            var promptButtons = FindActiveGameObjects(go =>
                go.name.StartsWith(PromptButtonPrimaryPrefix) || go.name.StartsWith(PromptButtonSecondaryPrefix));

            foreach (var button in promptButtons)
            {
                if (!_browserButtons.Contains(button))
                {
                    _browserButtons.Add(button);
                    string buttonText = UITextExtractor.GetButtonText(button, button.name);
                    MelonLogger.Msg($"[BrowserNavigator] Found prompt button: {button.name} -> '{buttonText}'");
                }
            }
        }

        /// <summary>
        /// Discovers elements for component-based browsers (CardBrowserCardHolder fallback).
        /// </summary>
        private void DiscoverComponentBrowserElements()
        {
            MelonLogger.Msg($"[BrowserNavigator] Component-based browser: {_browserTypeName}");

            // Try to get CardHolder from browser component
            var cardHolderProp = _activeBrowser.GetType().GetProperty("CardHolder");
            GameObject cardHolderGo = null;

            if (cardHolderProp != null)
            {
                var cardHolder = cardHolderProp.GetValue(_activeBrowser);
                if (cardHolder is GameObject go)
                    cardHolderGo = go;
                else if (cardHolder is Component comp)
                    cardHolderGo = comp.gameObject;
            }

            // Find cards in CardHolder or browser hierarchy
            var searchRoot = cardHolderGo ?? _browserGameObject;
            foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;

                if (CardDetector.IsCard(child.gameObject) && !_browserCards.Contains(child.gameObject))
                {
                    _browserCards.Add(child.gameObject);
                }
            }

            // Find buttons in browser hierarchy
            FindBrowserButtons(_browserGameObject);

            // If looks like mulligan (5+ cards, no buttons), search for mulligan buttons
            if (_browserCards.Count >= 5 && _browserButtons.Count == 0)
            {
                MelonLogger.Msg($"[BrowserNavigator] Component browser with {_browserCards.Count} cards and 0 buttons - checking for mulligan");
                SearchForMulliganButtons();
            }
        }

        /// <summary>
        /// Finds clickable buttons in the browser.
        /// </summary>
        private void FindBrowserButtons(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;

                // Check if name matches button patterns
                if (!MatchesButtonPattern(child.name, ButtonPatterns)) continue;

                // Verify it has a clickable component
                if (HasClickableComponent(child.gameObject) && !_browserButtons.Contains(child.gameObject))
                {
                    _browserButtons.Add(child.gameObject);
                    MelonLogger.Msg($"[BrowserNavigator] Found button: {child.name}");
                }
            }
        }

        /// <summary>
        /// Searches the entire scene for mulligan-specific buttons (KeepButton, MulliganButton).
        /// Called when CardBrowserCardHolder fallback detects what looks like an opening hand.
        /// </summary>
        private void SearchForMulliganButtons()
        {
            MelonLogger.Msg($"[BrowserNavigator] === SCENE SEARCH FOR MULLIGAN BUTTONS ===");

            var mulliganButtons = FindActiveGameObjects(go =>
                go.name == ButtonKeep || go.name == ButtonMulligan);

            foreach (var go in mulliganButtons)
            {
                if (!_browserButtons.Contains(go))
                {
                    _browserButtons.Add(go);
                    var buttonLabel = UITextExtractor.GetButtonText(go, go.name);
                    MelonLogger.Msg($"[BrowserNavigator] Found mulligan button: {go.name} -> '{buttonLabel}'");
                }
            }

            // Also search for PromptButton_Primary/Secondary as fallback
            var promptButtons = FindActiveGameObjects(go =>
                go.name.StartsWith(PromptButtonPrimaryPrefix) || go.name.StartsWith(PromptButtonSecondaryPrefix));

            foreach (var go in promptButtons)
            {
                if (!_browserButtons.Contains(go))
                {
                    _browserButtons.Add(go);
                    var buttonLabel = UITextExtractor.GetButtonText(go, go.name);
                    MelonLogger.Msg($"[BrowserNavigator] Found prompt button: {go.name} -> '{buttonLabel}'");
                }
            }

            MelonLogger.Msg($"[BrowserNavigator] === END SCENE SEARCH - found {_browserButtons.Count} buttons ===");
        }

        /// <summary>
        /// Searches for cards in a container and adds them to the browser cards list.
        /// </summary>
        private void SearchForCardsInContainer(GameObject container, string containerName)
        {
            int foundCount = 0;
            foreach (Transform child in container.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!CardDetector.IsCard(child.gameObject)) continue;

                string cardName = CardDetector.GetCardName(child.gameObject);

                // Filter and check for duplicates using helpers
                if (!IsValidCardName(cardName)) continue;
                if (IsDuplicateCard(child.gameObject, _browserCards)) continue;

                _browserCards.Add(child.gameObject);
                foundCount++;
                MelonLogger.Msg($"[BrowserNavigator] Found card in {containerName}: {child.name} -> {cardName}");
            }

            if (foundCount > 0)
            {
                MelonLogger.Msg($"[BrowserNavigator] Container {containerName} had {foundCount} cards");
            }
        }

        /// <summary>
        /// Searches for cards in the LocalHand zone for mulligan/opening hand browsers.
        /// Filters to only include valid local player cards with readable card data.
        /// </summary>
        private void SearchForCardsInLocalHand(GameObject localHandZone)
        {
            var foundCards = new List<GameObject>();

            foreach (Transform child in localHandZone.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!CardDetector.IsCard(child.gameObject)) continue;

                var card = child.gameObject;
                string cardName = CardDetector.GetCardName(card);

                MelonLogger.Msg($"[BrowserNavigator] LocalHand card candidate: {card.name} -> '{cardName}'");

                // Filter: must have valid card name
                if (!IsValidCardName(cardName))
                {
                    MelonLogger.Msg($"[BrowserNavigator]   -> Skipped (invalid name)");
                    continue;
                }

                // Additional filter: check if card is face-up (has readable data)
                var cardInfo = CardDetector.ExtractCardInfo(card);
                if (string.IsNullOrEmpty(cardInfo.Name))
                {
                    MelonLogger.Msg($"[BrowserNavigator]   -> Skipped (no card info)");
                    continue;
                }

                // Check for duplicates using unified helper
                if (!IsDuplicateCard(card, foundCards))
                {
                    foundCards.Add(card);
                    MelonLogger.Msg($"[BrowserNavigator]   -> Added: {cardName}");
                }
            }

            // Sort cards by horizontal position (left to right)
            foundCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            // Add to browser cards (avoiding duplicates)
            foreach (var card in foundCards)
            {
                if (!_browserCards.Contains(card))
                {
                    _browserCards.Add(card);
                }
            }

            MelonLogger.Msg($"[BrowserNavigator] LocalHand search: found {foundCards.Count} valid cards");
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announces the current browser state.
        /// </summary>
        private void AnnounceBrowserState()
        {
            string browserName = GetFriendlyBrowserName(_browserTypeName);
            int cardCount = _browserCards.Count;
            int buttonCount = _browserButtons.Count;

            string message;

            if (cardCount > 0)
            {
                message = Strings.BrowserCards(cardCount, browserName);
            }
            else if (buttonCount > 0)
            {
                message = Strings.BrowserOptions(browserName);
            }
            else
            {
                message = browserName;
            }

            _announcer.Announce(message, AnnouncementPriority.High);

            // Auto-navigate to first item
            if (cardCount > 0)
            {
                _currentCardIndex = 0;
                AnnounceCurrentCard();
            }
            else if (buttonCount > 0)
            {
                _currentButtonIndex = 0;
                AnnounceCurrentButton();
            }
        }

        /// <summary>
        /// Gets a user-friendly name for the browser type.
        /// </summary>
        private string GetFriendlyBrowserName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "Browser";

            // Check each keyword in the dictionary (order matters for Scryish before Scry)
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
        /// Announces the current card.
        /// </summary>
        private void AnnounceCurrentCard()
        {
            if (_currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count) return;

            var card = _browserCards[_currentCardIndex];
            var info = CardDetector.ExtractCardInfo(card);

            string cardName = info.Name ?? "Unknown card";

            // Check selection state based on which holder the card is in
            string selectionState = GetCardSelectionState(card);

            // Build announcement
            string announcement;
            if (_browserCards.Count == 1)
            {
                // Single card - just announce name and state
                announcement = string.IsNullOrEmpty(selectionState)
                    ? cardName
                    : $"{cardName}, {selectionState}";
            }
            else
            {
                // Multiple cards - include position
                string position = $"{_currentCardIndex + 1} of {_browserCards.Count}";
                announcement = string.IsNullOrEmpty(selectionState)
                    ? $"{cardName}, {position}"
                    : $"{cardName}, {selectionState}, {position}";
            }

            _announcer.Announce(announcement, AnnouncementPriority.High);

            // Prepare CardInfoNavigator for arrow key details
            MTGAAccessibilityMod.Instance?.CardNavigator?.PrepareForCard(card, ZoneType.Library);
        }

        /// <summary>
        /// Gets the selection state of a card in the browser.
        /// For scry: "keep on top" or "put on bottom"
        /// For mulligan/opening hand: no state (just viewing cards)
        /// </summary>
        private string GetCardSelectionState(GameObject card)
        {
            if (card == null) return null;

            // Mulligan/opening hand browsers don't have selection states
            // Cards are just displayed for viewing, not sorted into piles
            if (IsMulliganBrowser(_browserTypeName))
            {
                return null;
            }

            // If no buttons found, this is likely opening hand display, not scry
            // Real scry/surveil browsers always have confirm buttons
            if (_browserButtons.Count == 0)
            {
                return null;
            }

            // Check parent hierarchy to determine which holder the card is in
            Transform parent = card.transform.parent;
            while (parent != null)
            {
                if (parent.name == HolderDefault)
                {
                    return Strings.KeepOnTop;
                }
                if (parent.name == HolderViewDismiss)
                {
                    return Strings.PutOnBottom;
                }
                parent = parent.parent;
            }

            return null;
        }

        /// <summary>
        /// Announces the current button.
        /// </summary>
        private void AnnounceCurrentButton()
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _browserButtons.Count) return;

            var button = _browserButtons[_currentButtonIndex];

            // Safety check - button may have been destroyed
            if (button == null)
            {
                MelonLogger.Warning("[BrowserNavigator] Button at index was destroyed, refreshing buttons");
                RefreshBrowserButtons();
                return;
            }

            string label = UITextExtractor.GetButtonText(button, button.name);

            string position = _browserButtons.Count > 1 ? $", {_currentButtonIndex + 1} of {_browserButtons.Count}" : "";

            _announcer.Announce($"{label}{position}", AnnouncementPriority.High);
        }

        #endregion

        #region Navigation

        private void NavigateToNextCard()
        {
            if (_browserCards.Count == 0)
            {
                _announcer.Announce(Strings.NoCards, AnnouncementPriority.Normal);
                return;
            }

            _currentCardIndex = (_currentCardIndex + 1) % _browserCards.Count;
            AnnounceCurrentCard();
        }

        private void NavigateToPreviousCard()
        {
            if (_browserCards.Count == 0)
            {
                _announcer.Announce(Strings.NoCards, AnnouncementPriority.Normal);
                return;
            }

            _currentCardIndex--;
            if (_currentCardIndex < 0) _currentCardIndex = _browserCards.Count - 1;
            AnnounceCurrentCard();
        }

        private void NavigateToNextButton()
        {
            if (_browserButtons.Count == 0) return;

            _currentButtonIndex = (_currentButtonIndex + 1) % _browserButtons.Count;
            AnnounceCurrentButton();
        }

        private void NavigateToPreviousButton()
        {
            if (_browserButtons.Count == 0) return;

            _currentButtonIndex--;
            if (_currentButtonIndex < 0) _currentButtonIndex = _browserButtons.Count - 1;
            AnnounceCurrentButton();
        }

        #endregion

        #region Activation

        /// <summary>
        /// Activates (clicks) the current card.
        /// The game handles moving cards between holders (scry/surveil) or marking as selected.
        /// </summary>
        private void ActivateCurrentCard()
        {
            if (_currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count)
            {
                _announcer.Announce(Strings.NoCardSelected, AnnouncementPriority.Normal);
                return;
            }

            var card = _browserCards[_currentCardIndex];
            var cardName = CardDetector.GetCardName(card) ?? "card";
            string stateBefore = GetCardSelectionState(card);

            MelonLogger.Msg($"[BrowserNavigator] Activating card: {cardName}, state before: {stateBefore}");

            // Just click the card - let the game handle selection/toggle logic
            var result = UIActivator.SimulatePointerClick(card);
            if (result.Success)
            {
                MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName, stateBefore));
            }
            else
            {
                _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Logs public methods on a component for API discovery (debug).
        /// </summary>
        private void LogComponentMethods(object component)
        {
            var type = component.GetType();
            MelonLogger.Msg($"[BrowserNavigator] Methods on {type.Name}:");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                // Skip inherited methods from UnityEngine.Object
                if (method.DeclaringType == typeof(UnityEngine.Object) ||
                    method.DeclaringType == typeof(MonoBehaviour) ||
                    method.DeclaringType == typeof(Behaviour) ||
                    method.DeclaringType == typeof(Component) ||
                    method.DeclaringType == typeof(object))
                    continue;

                var paramInfo = string.Join(", ", System.Array.ConvertAll(method.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                MelonLogger.Msg($"[BrowserNavigator]   {method.ReturnType.Name} {method.Name}({paramInfo})");
            }
        }

        /// <summary>
        /// Waits for UI update then announces the new state.
        /// </summary>
        private IEnumerator AnnounceStateChangeAfterDelay(string cardName, string stateBefore)
        {
            yield return new WaitForSeconds(0.2f);

            // Re-find the card (it may have moved to different holder)
            GameObject card = null;
            var holders = FindActiveGameObjects(go => go.name == HolderDefault || go.name == HolderViewDismiss);
            foreach (var holder in holders)
            {
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (CardDetector.IsCard(child.gameObject))
                    {
                        string name = CardDetector.GetCardName(child.gameObject);
                        if (name == cardName)
                        {
                            card = child.gameObject;
                            break;
                        }
                    }
                }
                if (card != null) break;
            }

            if (card != null)
            {
                string stateAfter = GetCardSelectionState(card);
                MelonLogger.Msg($"[BrowserNavigator] State change: {stateBefore} -> {stateAfter}");

                // Only announce if state actually changed
                if (stateAfter != stateBefore && !string.IsNullOrEmpty(stateAfter))
                {
                    _announcer.Announce(stateAfter, AnnouncementPriority.Normal);
                }
                else if (string.IsNullOrEmpty(stateAfter))
                {
                    // Card not found in either holder - might be confirmed
                    _announcer.Announce(Strings.Selected, AnnouncementPriority.Normal);
                }

                // Update the card reference in our list
                if (_currentCardIndex >= 0 && _currentCardIndex < _browserCards.Count)
                {
                    _browserCards[_currentCardIndex] = card;
                }
            }
        }

        /// <summary>
        /// Activates (clicks) the current button.
        /// </summary>
        private void ActivateCurrentButton()
        {
            if (_currentButtonIndex < 0 || _currentButtonIndex >= _browserButtons.Count)
            {
                _announcer.Announce(Strings.NoButtonSelected, AnnouncementPriority.Normal);
                return;
            }

            var button = _browserButtons[_currentButtonIndex];
            var label = UITextExtractor.GetButtonText(button, button.name);

            MelonLogger.Msg($"[BrowserNavigator] Activating button: {label}");

            var result = UIActivator.SimulatePointerClick(button);
            if (result.Success)
            {
                _announcer.Announce(label, AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(Strings.CouldNotClick(label), AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Clicks the confirm/primary button.
        /// For mulligan: clicks KeepButton
        /// For other browsers: clicks PromptButton_Primary or browser-specific confirm buttons.
        /// </summary>
        private void ClickConfirmButton()
        {
            MelonLogger.Msg($"[BrowserNavigator] ClickConfirmButton called. Browser: {_browserTypeName}, Buttons: {_browserButtons.Count}");

            string clickedLabel;

            // London mulligan: click SubmitButton to confirm card selection
            if (_browserTypeName == BrowserTypes.London)
            {
                if (TryClickButtonByName(ButtonSubmit, out clickedLabel))
                {
                    _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                    return;
                }
                MelonLogger.Msg($"[BrowserNavigator] SubmitButton not found during London mulligan");
            }

            // For mulligan/opening hand, prioritize KeepButton
            if (IsMulliganBrowser(_browserTypeName))
            {
                if (TryClickButtonByName(ButtonKeep, out clickedLabel))
                {
                    _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                    return;
                }
                MelonLogger.Msg($"[BrowserNavigator] KeepButton not found during mulligan");
            }

            // Try PromptButton_Primary
            if (TryClickPromptButton(PromptButtonPrimaryPrefix, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                return;
            }

            // Fallback: browser-specific buttons by name pattern
            if (TryClickButtonByPatterns(ConfirmPatterns, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                return;
            }

            _announcer.Announce(Strings.NoConfirmButton, AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Clicks the cancel/secondary button.
        /// For mulligan: clicks MulliganButton
        /// For other browsers: clicks browser-specific cancel buttons or PromptButton_Secondary.
        /// </summary>
        private void ClickCancelButton()
        {
            MelonLogger.Msg($"[BrowserNavigator] ClickCancelButton called. Browser: {_browserTypeName}, Buttons: {_browserButtons.Count}");

            string clickedLabel;

            // First priority: MulliganButton (for mulligan browsers)
            if (TryClickButtonByName(ButtonMulligan, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                return;
            }

            // Second priority: other cancel buttons by pattern
            if (TryClickButtonByPatterns(CancelPatterns, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                return;
            }

            // Third priority: PromptButton_Secondary
            if (TryClickPromptButton(PromptButtonSecondaryPrefix, out clickedLabel))
            {
                _announcer.Announce(clickedLabel, AnnouncementPriority.Normal);
                return;
            }

            // Not finding cancel is OK - some browsers don't have it
            MelonLogger.Msg("[BrowserNavigator] No cancel button found");
        }

        /// <summary>
        /// Refreshes the button list, removing destroyed buttons.
        /// </summary>
        private void RefreshBrowserButtons()
        {
            // Remove null/destroyed buttons
            _browserButtons.RemoveAll(b => b == null);

            // Re-discover buttons if needed
            if (_browserButtons.Count == 0 && _browserGameObject != null)
            {
                FindBrowserButtons(_browserGameObject);

                // Also search in scene for mulligan-specific buttons
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (go.name.Contains("Browser") || go.name.Contains("Prompt") ||
                        go.name.Contains("Keep") || go.name.Contains("Mulligan"))
                    {
                        FindBrowserButtons(go);
                    }
                }
            }

            // Adjust index if needed
            if (_currentButtonIndex >= _browserButtons.Count)
            {
                _currentButtonIndex = _browserButtons.Count - 1;
            }

            // Announce if we have buttons
            if (_browserButtons.Count > 0 && _currentButtonIndex >= 0)
            {
                AnnounceCurrentButton();
            }
            else if (_browserButtons.Count > 0)
            {
                _currentButtonIndex = 0;
                AnnounceCurrentButton();
            }
            else
            {
                _announcer.Announce(Strings.NoButtonsAvailable, AnnouncementPriority.Normal);
            }
        }

        #endregion

        /// <summary>
        /// Gets the currently focused card (for external use).
        /// </summary>
        public GameObject GetCurrentCard()
        {
            if (_currentCardIndex < 0 || _currentCardIndex >= _browserCards.Count)
                return null;
            return _browserCards[_currentCardIndex];
        }
    }
}
