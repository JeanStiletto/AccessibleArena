using System;
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

        // London mulligan tracking
        private int _mulliganCount = 0;
        private HashSet<int> _londonSelectedCards = new HashSet<int>();

        // London zone navigation (hand = keep, library = bottom)
        private enum LondonZone { Hand, Library }
        private LondonZone _londonCurrentZone = LondonZone.Hand;
        private List<GameObject> _londonHandCards = new List<GameObject>();
        private List<GameObject> _londonLibraryCards = new List<GameObject>();
        private int _londonCardIndex = -1;

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

        /// <summary>
        /// Resets mulligan tracking state. Call when entering a new duel.
        /// </summary>
        public void ResetMulliganState()
        {
            _mulliganCount = 0;
            _londonSelectedCards.Clear();
            MelonLogger.Msg("[BrowserNavigator] Mulligan state reset for new game");
        }

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

            // C key - Enter hand (keep) zone in London browser, or activate card in other browsers
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (_browserTypeName == BrowserTypes.London)
                {
                    EnterLondonZone(LondonZone.Hand);
                    return true;
                }
                // For non-London browsers, C activates current card (like Enter)
                if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                {
                    ActivateCurrentCard();
                    return true;
                }
            }

            // D key - Enter library (bottom) zone in London browser
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (_browserTypeName == BrowserTypes.London)
                {
                    EnterLondonZone(LondonZone.Library);
                    return true;
                }
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
                // In London zone mode, use London navigation
                if (_browserTypeName == BrowserTypes.London && _londonCardIndex >= 0)
                {
                    NavigateLondonPrevious();
                    return true;
                }
                if (_browserCards.Count > 0) NavigateToPreviousCard();
                else if (_browserButtons.Count > 0) NavigateToPreviousButton();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                // In London zone mode, use London navigation
                if (_browserTypeName == BrowserTypes.London && _londonCardIndex >= 0)
                {
                    NavigateLondonNext();
                    return true;
                }
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
                // In London zone mode, use London activation
                if (_browserTypeName == BrowserTypes.London && _londonCardIndex >= 0)
                {
                    ActivateCurrentLondonCard();
                    return true;
                }
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

            // Reset London selection state when entering London browser
            if (_browserTypeName == BrowserTypes.London)
            {
                _londonSelectedCards.Clear();
                _londonCurrentZone = LondonZone.Hand;
                _londonHandCards.Clear();
                _londonLibraryCards.Clear();
                _londonCardIndex = -1;
                MelonLogger.Msg($"[BrowserNavigator] London mulligan: need to select {_mulliganCount} cards for bottom");

                // Investigation: Log browser holder components to find card selection APIs
                InvestigateBrowserHolders();
            }

            // Discover cards and buttons
            DiscoverBrowserElements();
        }

        /// <summary>
        /// Exits browser mode.
        /// </summary>
        private void ExitBrowserMode()
        {
            MelonLogger.Msg($"[BrowserNavigator] Exiting browser: {_browserTypeName}");

            // Reset mulligan count after London phase completes
            if (_browserTypeName == BrowserTypes.London)
            {
                MelonLogger.Msg($"[BrowserNavigator] London phase complete, resetting mulligan count");
                _mulliganCount = 0;
                _londonSelectedCards.Clear();
            }

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
        /// Enters a specific zone in London mulligan (hand = keep, library = bottom).
        /// Refreshes card lists from LondonBrowser and announces state.
        /// </summary>
        private void EnterLondonZone(LondonZone zone)
        {
            _londonCurrentZone = zone;
            _londonCardIndex = -1;

            // Refresh card lists from LondonBrowser
            RefreshLondonCardLists();

            var currentList = zone == LondonZone.Hand ? _londonHandCards : _londonLibraryCards;
            string zoneName = zone == LondonZone.Hand ? "Keep pile" : "Bottom pile";

            if (currentList.Count == 0)
            {
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.High);
            }
            else
            {
                // Navigate to first card
                _londonCardIndex = 0;
                var firstCard = currentList[0];
                var cardName = CardDetector.GetCardName(firstCard);
                _announcer.Announce($"{zoneName}: {currentList.Count} cards. {cardName}, 1 of {currentList.Count}", AnnouncementPriority.High);

                // Update CardInfoNavigator with this card
                var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
                cardNav?.PrepareForCard(firstCard, ZoneType.Library);
            }

            MelonLogger.Msg($"[BrowserNavigator] Entered London zone: {zoneName}, {currentList.Count} cards");
        }

        /// <summary>
        /// Refreshes the London hand and library card lists from the LondonBrowser.
        /// </summary>
        private void RefreshLondonCardLists()
        {
            _londonHandCards.Clear();
            _londonLibraryCards.Clear();

            try
            {
                var defaultHolder = FindActiveGameObject(HolderDefault);
                if (defaultHolder == null) return;

                Component cardBrowserHolder = null;
                foreach (var comp in defaultHolder.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                    {
                        cardBrowserHolder = comp;
                        break;
                    }
                }
                if (cardBrowserHolder == null) return;

                var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
                    BindingFlags.Public | BindingFlags.Instance);
                var londonBrowser = providerProp?.GetValue(cardBrowserHolder);
                if (londonBrowser == null) return;

                // Get hand cards (keep pile)
                var getHandCardsMethod = londonBrowser.GetType().GetMethod("GetHandCards",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getHandCardsMethod != null)
                {
                    var handCards = getHandCardsMethod.Invoke(londonBrowser, null) as System.Collections.IList;
                    if (handCards != null)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] GetHandCards returned {handCards.Count} items");
                        foreach (var cardCDC in handCards)
                        {
                            if (cardCDC is Component comp && comp.gameObject != null)
                            {
                                var go = comp.gameObject;
                                var cardName = CardDetector.GetCardName(go);
                                MelonLogger.Msg($"[BrowserNavigator] Hand card: {go.name} -> {cardName}");

                                // Filter out placeholder cards (CDC #0 or invalid)
                                if (!string.IsNullOrEmpty(cardName) && cardName != "Unknown card" && !go.name.Contains("CDC #0"))
                                {
                                    _londonHandCards.Add(go);
                                }
                            }
                        }
                    }
                }

                // Get library cards (bottom pile)
                var getLibraryCardsMethod = londonBrowser.GetType().GetMethod("GetLibraryCards",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getLibraryCardsMethod != null)
                {
                    var libraryCards = getLibraryCardsMethod.Invoke(londonBrowser, null) as System.Collections.IList;
                    if (libraryCards != null)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] GetLibraryCards returned {libraryCards.Count} items");
                        foreach (var cardCDC in libraryCards)
                        {
                            if (cardCDC is Component comp && comp.gameObject != null)
                            {
                                var go = comp.gameObject;
                                var cardName = CardDetector.GetCardName(go);
                                MelonLogger.Msg($"[BrowserNavigator] Library card: {go.name} -> {cardName}");

                                // Filter out placeholder cards (CDC #0 or invalid)
                                if (!string.IsNullOrEmpty(cardName) && cardName != "Unknown card" && !go.name.Contains("CDC #0"))
                                {
                                    _londonLibraryCards.Add(go);
                                }
                            }
                        }
                    }
                }

                MelonLogger.Msg($"[BrowserNavigator] Refreshed London lists - Hand: {_londonHandCards.Count}, Library: {_londonLibraryCards.Count}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BrowserNavigator] Error refreshing London card lists: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to the next card in the current London zone.
        /// </summary>
        private void NavigateLondonNext()
        {
            var currentList = _londonCurrentZone == LondonZone.Hand ? _londonHandCards : _londonLibraryCards;
            if (currentList.Count == 0) return;

            _londonCardIndex++;
            if (_londonCardIndex >= currentList.Count)
                _londonCardIndex = 0;

            AnnounceCurrentLondonCard();
        }

        /// <summary>
        /// Navigates to the previous card in the current London zone.
        /// </summary>
        private void NavigateLondonPrevious()
        {
            var currentList = _londonCurrentZone == LondonZone.Hand ? _londonHandCards : _londonLibraryCards;
            if (currentList.Count == 0) return;

            _londonCardIndex--;
            if (_londonCardIndex < 0)
                _londonCardIndex = currentList.Count - 1;

            AnnounceCurrentLondonCard();
        }

        /// <summary>
        /// Announces the current card in London zone navigation.
        /// </summary>
        private void AnnounceCurrentLondonCard()
        {
            var currentList = _londonCurrentZone == LondonZone.Hand ? _londonHandCards : _londonLibraryCards;
            if (_londonCardIndex < 0 || _londonCardIndex >= currentList.Count) return;

            var card = currentList[_londonCardIndex];
            var cardName = CardDetector.GetCardName(card);
            string zoneName = _londonCurrentZone == LondonZone.Hand ? "keep" : "bottom";

            _announcer.Announce($"{cardName}, {zoneName}, {_londonCardIndex + 1} of {currentList.Count}", AnnouncementPriority.High);

            // Update CardInfoNavigator
            var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
            cardNav?.PrepareForCard(card, ZoneType.Library);
        }

        /// <summary>
        /// Activates (toggles) the current card in London zone navigation.
        /// Moves the card to the other zone.
        /// </summary>
        private void ActivateCurrentLondonCard()
        {
            var currentList = _londonCurrentZone == LondonZone.Hand ? _londonHandCards : _londonLibraryCards;
            if (_londonCardIndex < 0 || _londonCardIndex >= currentList.Count)
            {
                _announcer.Announce("No card selected", AnnouncementPriority.Normal);
                return;
            }

            var card = currentList[_londonCardIndex];
            var cardName = CardDetector.GetCardName(card);

            // Use the existing activation method
            bool success = TryActivateCardViaLondonBrowser(card, cardName);
            if (success)
            {
                // Refresh lists and announce new state after a short delay
                MelonCoroutines.Start(RefreshLondonZoneAfterDelay());
            }
            else
            {
                _announcer.Announce($"Could not move {cardName}", AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Refreshes London zone after card activation with a short delay.
        /// </summary>
        private System.Collections.IEnumerator RefreshLondonZoneAfterDelay()
        {
            yield return new WaitForSeconds(0.2f);

            RefreshLondonCardLists();

            var currentList = _londonCurrentZone == LondonZone.Hand ? _londonHandCards : _londonLibraryCards;
            string zoneName = _londonCurrentZone == LondonZone.Hand ? "keep" : "bottom";

            // Adjust index if needed
            if (_londonCardIndex >= currentList.Count)
                _londonCardIndex = currentList.Count - 1;

            if (currentList.Count == 0)
            {
                _announcer.Announce($"{zoneName} pile: empty. {_londonLibraryCards.Count} of {_mulliganCount} selected for bottom", AnnouncementPriority.Normal);
            }
            else if (_londonCardIndex >= 0)
            {
                var card = currentList[_londonCardIndex];
                var cardName = CardDetector.GetCardName(card);
                _announcer.Announce($"{cardName}, {zoneName}, {_londonCardIndex + 1} of {currentList.Count}. {_londonLibraryCards.Count} of {_mulliganCount} selected for bottom", AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// INVESTIGATION: Logs detailed information about BrowserCardHolder components
        /// to discover APIs for card selection in London mulligan.
        /// </summary>
        private void InvestigateBrowserHolders()
        {
            MelonLogger.Msg("[BrowserNavigator] === INVESTIGATING BROWSER HOLDERS FOR CARD SELECTION API ===");

            // Find all browser-related holders
            var holders = FindActiveGameObjects(go =>
                go.name.Contains("BrowserCardHolder") ||
                go.name.Contains("CardHolder") ||
                go.name.Contains("Browser"));

            foreach (var holder in holders)
            {
                MelonLogger.Msg($"[BrowserNavigator] --- Holder: {holder.name} ---");

                foreach (var comp in holder.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var type = comp.GetType();
                    string typeName = type.FullName ?? type.Name;

                    // Skip common Unity components
                    if (typeName.StartsWith("UnityEngine.") && !typeName.Contains("Event"))
                        continue;

                    MelonLogger.Msg($"[BrowserNavigator]   Component: {typeName}");

                    // Log fields (especially Action/UnityEvent delegates)
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        string fieldTypeName = field.FieldType.Name;
                        bool isInteresting = fieldTypeName.Contains("Action") ||
                                            fieldTypeName.Contains("Event") ||
                                            fieldTypeName.Contains("Click") ||
                                            fieldTypeName.Contains("Select") ||
                                            fieldTypeName.Contains("Card") ||
                                            field.Name.ToLower().Contains("click") ||
                                            field.Name.ToLower().Contains("select") ||
                                            field.Name.ToLower().Contains("card");

                        if (isInteresting)
                        {
                            try
                            {
                                var value = field.GetValue(comp);
                                string valueStr = value != null ? $"has value ({value.GetType().Name})" : "null";
                                MelonLogger.Msg($"[BrowserNavigator]     Field: {field.Name} : {fieldTypeName} = {valueStr}");
                            }
                            catch
                            {
                                MelonLogger.Msg($"[BrowserNavigator]     Field: {field.Name} : {fieldTypeName} (could not read)");
                            }
                        }
                    }

                    // Log properties (especially Action/UnityEvent delegates)
                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        string propTypeName = prop.PropertyType.Name;
                        bool isInteresting = propTypeName.Contains("Action") ||
                                            propTypeName.Contains("Event") ||
                                            propTypeName.Contains("Click") ||
                                            propTypeName.Contains("Select") ||
                                            propTypeName.Contains("Card") ||
                                            prop.Name.ToLower().Contains("click") ||
                                            prop.Name.ToLower().Contains("select") ||
                                            prop.Name.ToLower().Contains("card");

                        if (isInteresting)
                        {
                            try
                            {
                                var value = prop.GetValue(comp);
                                string valueStr = value != null ? $"has value ({value.GetType().Name})" : "null";
                                MelonLogger.Msg($"[BrowserNavigator]     Property: {prop.Name} : {propTypeName} = {valueStr}");
                            }
                            catch
                            {
                                MelonLogger.Msg($"[BrowserNavigator]     Property: {prop.Name} : {propTypeName} (could not read)");
                            }
                        }
                    }

                    // Log methods that look relevant to card interaction
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        string methodName = method.Name.ToLower();
                        bool isInteresting = methodName.Contains("click") ||
                                            methodName.Contains("select") ||
                                            methodName.Contains("card") ||
                                            methodName.Contains("toggle") ||
                                            methodName.Contains("add") ||
                                            methodName.Contains("remove") ||
                                            methodName.Contains("drag") ||
                                            methodName.Contains("drop");

                        if (isInteresting)
                        {
                            var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            MelonLogger.Msg($"[BrowserNavigator]     Method: {method.Name}({paramStr}) -> {method.ReturnType.Name}");
                        }
                    }
                }
            }

            // Also investigate the first card found in the browser
            InvestigateBrowserCard();

            // Investigate the LondonBrowser instance (CardGroupProvider)
            InvestigateLondonBrowser();

            // Look for InteractionSystem or GameInteractionSystem
            InvestigateInteractionSystem();

            MelonLogger.Msg("[BrowserNavigator] === END BROWSER HOLDER INVESTIGATION ===");
        }

        /// <summary>
        /// INVESTIGATION: Logs details about card components in browser context.
        /// </summary>
        private void InvestigateBrowserCard()
        {
            MelonLogger.Msg("[BrowserNavigator] --- Investigating Browser Card Components ---");

            // Find a card in the browser
            GameObject card = null;
            var holders = FindActiveGameObjects(go => go.name == HolderDefault || go.name == HolderViewDismiss);
            foreach (var holder in holders)
            {
                foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.activeInHierarchy && CardDetector.IsCard(child.gameObject))
                    {
                        card = child.gameObject;
                        break;
                    }
                }
                if (card != null) break;
            }

            if (card == null)
            {
                MelonLogger.Msg("[BrowserNavigator] No card found in browser holders");
                return;
            }

            MelonLogger.Msg($"[BrowserNavigator] Found card: {card.name}");

            foreach (var comp in card.GetComponents<Component>())
            {
                if (comp == null) continue;
                var type = comp.GetType();
                string typeName = type.Name;

                // Focus on game-specific components, not Unity builtins
                if (typeName.StartsWith("UnityEngine"))
                    continue;

                MelonLogger.Msg($"[BrowserNavigator]   Card Component: {type.FullName}");

                // Log all public and declared methods
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MelonLogger.Msg($"[BrowserNavigator]     Method: {method.Name}({paramStr}) -> {method.ReturnType.Name}");
                }

                // Log fields that might be callbacks
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    string fieldTypeName = field.FieldType.Name;
                    if (fieldTypeName.Contains("Action") || fieldTypeName.Contains("Event") ||
                        fieldTypeName.Contains("Func") || fieldTypeName.Contains("Callback"))
                    {
                        try
                        {
                            var value = field.GetValue(comp);
                            string valueStr = value != null ? "has callback" : "null";
                            MelonLogger.Msg($"[BrowserNavigator]     Callback Field: {field.Name} : {fieldTypeName} = {valueStr}");
                        }
                        catch
                        {
                            MelonLogger.Msg($"[BrowserNavigator]     Callback Field: {field.Name} : {fieldTypeName}");
                        }
                    }
                }

                // Check for IPointerClickHandler implementation
                var interfaces = type.GetInterfaces();
                foreach (var iface in interfaces)
                {
                    if (iface.Name.Contains("Pointer") || iface.Name.Contains("Click") ||
                        iface.Name.Contains("Drag") || iface.Name.Contains("Handler"))
                    {
                        MelonLogger.Msg($"[BrowserNavigator]     Implements: {iface.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// INVESTIGATION: Gets LondonBrowser from CardGroupProvider and logs all its APIs.
        /// </summary>
        private void InvestigateLondonBrowser()
        {
            MelonLogger.Msg("[BrowserNavigator] --- Investigating LondonBrowser (CardGroupProvider) ---");

            // Find BrowserCardHolder_Default
            var defaultHolder = FindActiveGameObject(HolderDefault);
            if (defaultHolder == null)
            {
                MelonLogger.Msg("[BrowserNavigator] BrowserCardHolder_Default not found");
                return;
            }

            // Get CardBrowserCardHolder component
            Component cardBrowserHolder = null;
            foreach (var comp in defaultHolder.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                {
                    cardBrowserHolder = comp;
                    break;
                }
            }

            if (cardBrowserHolder == null)
            {
                MelonLogger.Msg("[BrowserNavigator] CardBrowserCardHolder component not found");
                return;
            }

            // Get CardGroupProvider property (should be LondonBrowser)
            var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
                BindingFlags.Public | BindingFlags.Instance);
            if (providerProp == null)
            {
                MelonLogger.Msg("[BrowserNavigator] CardGroupProvider property not found");
                return;
            }

            var londonBrowser = providerProp.GetValue(cardBrowserHolder);
            if (londonBrowser == null)
            {
                MelonLogger.Msg("[BrowserNavigator] CardGroupProvider is null");
                return;
            }

            var browserType = londonBrowser.GetType();
            MelonLogger.Msg($"[BrowserNavigator] LondonBrowser type: {browserType.FullName}");

            // Log ALL methods (including inherited)
            MelonLogger.Msg("[BrowserNavigator] === LondonBrowser Methods ===");
            foreach (var method in browserType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                // Skip common object methods
                if (method.DeclaringType == typeof(object)) continue;

                var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                MelonLogger.Msg($"[BrowserNavigator]   {method.DeclaringType?.Name}.{method.Name}({paramStr}) -> {method.ReturnType.Name}");
            }

            // Log ALL fields
            MelonLogger.Msg("[BrowserNavigator] === LondonBrowser Fields ===");
            foreach (var field in browserType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    var value = field.GetValue(londonBrowser);
                    string valueStr = value != null ? $"has value ({value.GetType().Name})" : "null";
                    MelonLogger.Msg($"[BrowserNavigator]   {field.Name} : {field.FieldType.Name} = {valueStr}");
                }
                catch
                {
                    MelonLogger.Msg($"[BrowserNavigator]   {field.Name} : {field.FieldType.Name} (could not read)");
                }
            }

            // Log ALL properties
            MelonLogger.Msg("[BrowserNavigator] === LondonBrowser Properties ===");
            foreach (var prop in browserType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    var value = prop.GetValue(londonBrowser);
                    string valueStr = value != null ? $"has value ({value.GetType().Name})" : "null";
                    MelonLogger.Msg($"[BrowserNavigator]   {prop.Name} : {prop.PropertyType.Name} = {valueStr}");
                }
                catch
                {
                    MelonLogger.Msg($"[BrowserNavigator]   {prop.Name} : {prop.PropertyType.Name} (could not read)");
                }
            }

            // Log interfaces
            MelonLogger.Msg("[BrowserNavigator] === LondonBrowser Interfaces ===");
            foreach (var iface in browserType.GetInterfaces())
            {
                MelonLogger.Msg($"[BrowserNavigator]   Implements: {iface.FullName}");

                // Log interface methods
                foreach (var method in iface.GetMethods())
                {
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MelonLogger.Msg($"[BrowserNavigator]     Interface method: {method.Name}({paramStr})");
                }
            }

            // Check base class hierarchy
            MelonLogger.Msg("[BrowserNavigator] === LondonBrowser Base Classes ===");
            var baseType = browserType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                MelonLogger.Msg($"[BrowserNavigator]   Base: {baseType.FullName}");

                // Log base class methods that might be useful
                foreach (var method in baseType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    string methodName = method.Name.ToLower();
                    if (methodName.Contains("select") || methodName.Contains("card") ||
                        methodName.Contains("toggle") || methodName.Contains("click") ||
                        methodName.Contains("add") || methodName.Contains("remove") ||
                        methodName.Contains("move") || methodName.Contains("group"))
                    {
                        var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        MelonLogger.Msg($"[BrowserNavigator]     Base.{method.Name}({paramStr})");
                    }
                }

                baseType = baseType.BaseType;
            }

            // Try to find and log events/actions
            MelonLogger.Msg("[BrowserNavigator] === LondonBrowser Events/Actions ===");
            foreach (var eventInfo in browserType.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                MelonLogger.Msg($"[BrowserNavigator]   Event: {eventInfo.Name} : {eventInfo.EventHandlerType?.Name}");
            }

            MelonLogger.Msg("[BrowserNavigator] === End LondonBrowser Investigation ===");
        }

        /// <summary>
        /// INVESTIGATION: Look for InteractionSystem or GameInteractionSystem.
        /// </summary>
        private void InvestigateInteractionSystem()
        {
            MelonLogger.Msg("[BrowserNavigator] --- Investigating InteractionSystem ---");

            // Search for InteractionSystem type
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name.Contains("InteractionSystem") ||
                            type.Name.Contains("GameInteraction") ||
                            type.Name.Contains("CardInteraction"))
                        {
                            MelonLogger.Msg($"[BrowserNavigator] Found type: {type.FullName}");

                            // Check for static instance property
                            var instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                            if (instanceProp != null)
                            {
                                try
                                {
                                    var instance = instanceProp.GetValue(null);
                                    if (instance != null)
                                    {
                                        MelonLogger.Msg($"[BrowserNavigator]   Has singleton instance");
                                        LogInteractionSystemMembers(instance);
                                    }
                                }
                                catch { }
                            }

                            // Check for OnCardClicked or similar events
                            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                            {
                                if (field.Name.ToLower().Contains("card") || field.Name.ToLower().Contains("click"))
                                {
                                    MelonLogger.Msg($"[BrowserNavigator]   Static/Instance Field: {field.Name} : {field.FieldType.Name}");
                                }
                            }
                        }
                    }
                }
                catch { /* Skip assemblies that can't be scanned */ }
            }

            // Also search MonoBehaviours in scene
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName.Contains("InteractionSystem") || typeName.Contains("CardInteraction"))
                {
                    MelonLogger.Msg($"[BrowserNavigator] Found MonoBehaviour: {typeName} on {mb.gameObject.name}");
                    LogInteractionSystemMembers(mb);
                }
            }
        }

        /// <summary>
        /// Logs relevant members of an InteractionSystem instance.
        /// </summary>
        private void LogInteractionSystemMembers(object instance)
        {
            var type = instance.GetType();

            // Log fields related to card/click
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                string fieldName = field.Name.ToLower();
                string fieldTypeName = field.FieldType.Name;
                if (fieldName.Contains("card") || fieldName.Contains("click") ||
                    fieldTypeName.Contains("Action") || fieldTypeName.Contains("Event"))
                {
                    try
                    {
                        var value = field.GetValue(instance);
                        string valueStr = value != null ? "has value" : "null";
                        MelonLogger.Msg($"[BrowserNavigator]     Field: {field.Name} : {fieldTypeName} = {valueStr}");
                    }
                    catch
                    {
                        MelonLogger.Msg($"[BrowserNavigator]     Field: {field.Name} : {fieldTypeName}");
                    }
                }
            }

            // Log methods related to card interaction
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                string methodName = method.Name.ToLower();
                if (methodName.Contains("card") || methodName.Contains("click") ||
                    methodName.Contains("select") || methodName.Contains("toggle"))
                {
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MelonLogger.Msg($"[BrowserNavigator]     Method: {method.Name}({paramStr})");
                }
            }
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

            // Special announcement for London mulligan - explain the task
            if (_browserTypeName == BrowserTypes.London && _mulliganCount > 0)
            {
                string cardWord = _mulliganCount == 1 ? "card" : "cards";
                message = $"Select {_mulliganCount} {cardWord} to put on bottom. {cardCount} cards. Enter to toggle, Space when done";
            }
            else if (cardCount > 0)
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
        /// For London: "keep" or "bottom" (tracked in our selection set)
        /// For mulligan/opening hand: no state (just viewing cards)
        /// </summary>
        private string GetCardSelectionState(GameObject card)
        {
            if (card == null) return null;

            // London mulligan uses our tracked selection state
            if (_browserTypeName == BrowserTypes.London)
            {
                int cardId = card.GetInstanceID();
                return _londonSelectedCards.Contains(cardId) ? "bottom" : "keep";
            }

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
        /// For London mulligan, we track selection ourselves and toggle on click.
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

            // For London browser, use LondonBrowser.OnCardViewSelected() instead of UI clicks
            if (_browserTypeName == BrowserTypes.London)
            {
                bool success = TryActivateCardViaLondonBrowser(card, cardName);
                if (success)
                {
                    // The game handles selection state, announce the new state after a short delay
                    MelonCoroutines.Start(AnnounceLondonStateAfterDelay(card, cardName));
                }
                else
                {
                    _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
                }
                return;
            }

            // Non-London browsers: use standard UI click
            var result = UIActivator.SimulatePointerClick(card);
            if (!result.Success)
            {
                _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
                return;
            }

            // Wait for game state to update
            MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName, stateBefore));
        }

        /// <summary>
        /// Activates a card via the LondonBrowser's OnCardViewSelected method.
        /// This is the proper way to select cards in London mulligan.
        /// </summary>
        private bool TryActivateCardViaLondonBrowser(GameObject card, string cardName)
        {
            MelonLogger.Msg($"[BrowserNavigator] Attempting LondonBrowser.OnCardViewSelected for: {cardName}");

            try
            {
                // Step 1: Find BrowserCardHolder_Default
                var defaultHolder = FindActiveGameObject(HolderDefault);
                if (defaultHolder == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] BrowserCardHolder_Default not found");
                    return false;
                }

                // Step 2: Get CardBrowserCardHolder component
                Component cardBrowserHolder = null;
                foreach (var comp in defaultHolder.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                    {
                        cardBrowserHolder = comp;
                        break;
                    }
                }

                if (cardBrowserHolder == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] CardBrowserCardHolder component not found");
                    return false;
                }

                // Step 3: Get CardGroupProvider property (LondonBrowser instance)
                var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
                    BindingFlags.Public | BindingFlags.Instance);
                if (providerProp == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] CardGroupProvider property not found");
                    return false;
                }

                var londonBrowser = providerProp.GetValue(cardBrowserHolder);
                if (londonBrowser == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] CardGroupProvider (LondonBrowser) is null");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Got LondonBrowser: {londonBrowser.GetType().Name}");

                // Step 4: Get DuelScene_CDC component from card
                var cardCDC = CardDetector.GetDuelSceneCDC(card);
                if (cardCDC == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] DuelScene_CDC component not found on card");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Got DuelScene_CDC: {cardCDC.GetType().Name}");

                // Step 5: Check if the card can change zones
                var canChangeZonesMethod = londonBrowser.GetType().GetMethod("CanChangeZones",
                    BindingFlags.Public | BindingFlags.Instance);
                if (canChangeZonesMethod != null)
                {
                    bool canChange = (bool)canChangeZonesMethod.Invoke(londonBrowser, new object[] { cardCDC });
                    MelonLogger.Msg($"[BrowserNavigator] CanChangeZones: {canChange}");
                }

                // Step 6: Check if card is in hand (keep) or library (bottom)
                var isInHandMethod = londonBrowser.GetType().GetMethod("IsInHand",
                    BindingFlags.Public | BindingFlags.Instance);
                var isInLibraryMethod = londonBrowser.GetType().GetMethod("IsInLibrary",
                    BindingFlags.Public | BindingFlags.Instance);

                bool isInHand = isInHandMethod != null && (bool)isInHandMethod.Invoke(londonBrowser, new object[] { cardCDC });
                bool isInLibrary = isInLibraryMethod != null && (bool)isInLibraryMethod.Invoke(londonBrowser, new object[] { cardCDC });
                MelonLogger.Msg($"[BrowserNavigator] Card position - IsInHand: {isInHand}, IsInLibrary: {isInLibrary}");

                // Step 7: Get target screen position (opposite of current zone)
                // HandScreenSpace = keep pile, LibraryScreenSpace = bottom pile
                string targetPropName = isInHand ? "LibraryScreenSpace" : "HandScreenSpace";
                var targetPosProp = londonBrowser.GetType().GetProperty(targetPropName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (targetPosProp != null)
                {
                    var targetPos = (Vector2)targetPosProp.GetValue(londonBrowser);
                    MelonLogger.Msg($"[BrowserNavigator] Target position ({targetPropName}): {targetPos}");

                    // Move the card's screen position to the target zone
                    // The card GameObject's position needs to be in the target zone for OnDragRelease to detect it
                    var cardTransform = card.transform;
                    Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(targetPos.x, targetPos.y, 10f));
                    cardTransform.position = worldPos;
                    MelonLogger.Msg($"[BrowserNavigator] Moved card to world position: {worldPos}");
                }

                // Step 8: Call HandleDrag to start the drag
                var handleDragMethod = londonBrowser.GetType().GetMethod("HandleDrag",
                    BindingFlags.Public | BindingFlags.Instance);
                if (handleDragMethod == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] HandleDrag method not found on LondonBrowser");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Invoking HandleDrag...");
                handleDragMethod.Invoke(londonBrowser, new object[] { cardCDC });
                MelonLogger.Msg($"[BrowserNavigator] HandleDrag invoked!");

                // Step 9: Call OnDragRelease to complete the drag (card is now at target position)
                var onDragReleaseMethod = londonBrowser.GetType().GetMethod("OnDragRelease",
                    BindingFlags.Public | BindingFlags.Instance);
                if (onDragReleaseMethod == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] OnDragRelease method not found on LondonBrowser");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Invoking OnDragRelease...");
                onDragReleaseMethod.Invoke(londonBrowser, new object[] { cardCDC });
                MelonLogger.Msg($"[BrowserNavigator] OnDragRelease invoked successfully!");

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] Error in TryActivateCardViaLondonBrowser: {ex.Message}");
                MelonLogger.Error($"[BrowserNavigator] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Announces the London mulligan state after a short delay to allow game state to update.
        /// </summary>
        private System.Collections.IEnumerator AnnounceLondonStateAfterDelay(GameObject card, string cardName)
        {
            // Wait for game state to update
            yield return new WaitForSeconds(0.15f);

            // Get LondonBrowser to check actual state
            int bottomCount = 0;
            int keepCount = 0;
            bool cardIsInLibrary = false;

            try
            {
                var defaultHolder = FindActiveGameObject(HolderDefault);
                if (defaultHolder != null)
                {
                    Component cardBrowserHolder = null;
                    foreach (var comp in defaultHolder.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                        {
                            cardBrowserHolder = comp;
                            break;
                        }
                    }

                    if (cardBrowserHolder != null)
                    {
                        var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
                            BindingFlags.Public | BindingFlags.Instance);
                        var londonBrowser = providerProp?.GetValue(cardBrowserHolder);

                        if (londonBrowser != null)
                        {
                            // Get card counts from LondonBrowser's internal lists
                            var getLibraryCardsMethod = londonBrowser.GetType().GetMethod("GetLibraryCards",
                                BindingFlags.Public | BindingFlags.Instance);
                            var getHandCardsMethod = londonBrowser.GetType().GetMethod("GetHandCards",
                                BindingFlags.Public | BindingFlags.Instance);

                            if (getLibraryCardsMethod != null)
                            {
                                var libraryCards = getLibraryCardsMethod.Invoke(londonBrowser, null) as System.Collections.IList;
                                bottomCount = libraryCards?.Count ?? 0;
                            }

                            if (getHandCardsMethod != null)
                            {
                                var handCards = getHandCardsMethod.Invoke(londonBrowser, null) as System.Collections.IList;
                                keepCount = handCards?.Count ?? 0;
                            }

                            // Check if this specific card is now in library (bottom)
                            var cardCDC = CardDetector.GetDuelSceneCDC(card);
                            if (cardCDC != null)
                            {
                                var isInLibraryMethod = londonBrowser.GetType().GetMethod("IsInLibrary",
                                    BindingFlags.Public | BindingFlags.Instance);
                                if (isInLibraryMethod != null)
                                {
                                    cardIsInLibrary = (bool)isInLibraryMethod.Invoke(londonBrowser, new object[] { cardCDC });
                                }
                            }

                            MelonLogger.Msg($"[BrowserNavigator] London state - Keep: {keepCount}, Bottom: {bottomCount}, Card in library: {cardIsInLibrary}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BrowserNavigator] Error getting London state: {ex.Message}");
            }

            // Announce based on the card's new position
            if (cardIsInLibrary)
            {
                _announcer.Announce($"bottom. {bottomCount} of {_mulliganCount} selected", AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce($"keep. {bottomCount} of {_mulliganCount} selected", AnnouncementPriority.Normal);
            }

            // Refresh browser cards list
            DiscoverBrowserElements();
        }

        /// <summary>
        /// Activates a card via the game's workflow system instead of UI clicks.
        /// Uses WorkflowController.CurrentWorkflow.OnClick(entityView, Primary).
        /// NOTE: This doesn't work for London mulligan - use TryActivateCardViaLondonBrowser instead.
        /// </summary>
        private bool TryActivateCardViaWorkflow(GameObject card, string cardName)
        {
            MelonLogger.Msg($"[BrowserNavigator] Attempting workflow-based activation for: {cardName}");

            // Step 1: Find WorkflowController in scene
            var workflowController = FindWorkflowController();
            if (workflowController == null)
            {
                MelonLogger.Warning("[BrowserNavigator] WorkflowController not found");
                return false;
            }

            // Step 2: Get CurrentWorkflow property
            var currentWorkflow = GetCurrentWorkflow(workflowController);
            if (currentWorkflow == null)
            {
                MelonLogger.Warning("[BrowserNavigator] CurrentWorkflow is null");
                return false;
            }

            MelonLogger.Msg($"[BrowserNavigator] CurrentWorkflow type: {currentWorkflow.GetType().FullName}");

            // Step 3: Get IEntityView from card (DuelScene_CDC component)
            var entityView = GetEntityViewFromCard(card);
            if (entityView == null)
            {
                MelonLogger.Warning("[BrowserNavigator] Could not get IEntityView from card");
                return false;
            }

            MelonLogger.Msg($"[BrowserNavigator] Got IEntityView, attempting OnClick");

            // Step 4: Get SimpleInteractionType.Primary enum value
            var interactionType = GetSimpleInteractionTypePrimary();
            if (interactionType == null)
            {
                MelonLogger.Warning("[BrowserNavigator] Could not get SimpleInteractionType.Primary");
                return false;
            }

            // Step 5: Call OnClick on the workflow
            return InvokeWorkflowOnClick(currentWorkflow, entityView, interactionType);
        }

        /// <summary>
        /// Finds the WorkflowController in the scene.
        /// WorkflowController is not a MonoBehaviour, so we need to find it via a manager class.
        /// </summary>
        private object FindWorkflowController()
        {
            try
            {
                // WorkflowController is typically accessed via a DuelScene manager
                // Look for objects that might have a WorkflowController property

                // Try to find via InteractionManager or similar
                var interactionManagerType = FindTypeByName("Wotc.Mtga.DuelScene.Interactions.InteractionManager");
                if (interactionManagerType != null && typeof(MonoBehaviour).IsAssignableFrom(interactionManagerType))
                {
                    var managers = UnityEngine.Object.FindObjectsOfType(interactionManagerType);
                    if (managers.Length > 0)
                    {
                        var prop = interactionManagerType.GetProperty("WorkflowController", BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                        {
                            var controller = prop.GetValue(managers[0]);
                            if (controller != null)
                            {
                                MelonLogger.Msg($"[BrowserNavigator] Found WorkflowController via InteractionManager");
                                return controller;
                            }
                        }
                    }
                }

                // Try DuelSceneController
                var duelSceneControllerType = FindTypeByName("Wotc.Mtga.DuelScene.DuelSceneController");
                if (duelSceneControllerType != null && typeof(MonoBehaviour).IsAssignableFrom(duelSceneControllerType))
                {
                    var controllers = UnityEngine.Object.FindObjectsOfType(duelSceneControllerType);
                    if (controllers.Length > 0)
                    {
                        // Try to find WorkflowController property
                        var prop = duelSceneControllerType.GetProperty("WorkflowController", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (prop != null)
                        {
                            var controller = prop.GetValue(controllers[0]);
                            if (controller != null)
                            {
                                MelonLogger.Msg($"[BrowserNavigator] Found WorkflowController via DuelSceneController");
                                return controller;
                            }
                        }

                        // Try all public properties to find WorkflowController
                        foreach (var p in duelSceneControllerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (p.PropertyType.Name == "WorkflowController")
                            {
                                var controller = p.GetValue(controllers[0]);
                                if (controller != null)
                                {
                                    MelonLogger.Msg($"[BrowserNavigator] Found WorkflowController via {p.Name}");
                                    return controller;
                                }
                            }
                        }
                    }
                }

                // Fallback: Search all MonoBehaviours for one with WorkflowController property
                var allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                MelonLogger.Msg($"[BrowserNavigator] Searching {allBehaviours.Length} MonoBehaviours for WorkflowController");

                int duelSceneCount = 0;
                foreach (var behaviour in allBehaviours)
                {
                    var behaviourType = behaviour.GetType();

                    // Check all types that might have WorkflowController
                    var prop = behaviourType.GetProperty("WorkflowController", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Found WorkflowController property on: {behaviourType.FullName}");
                        try
                        {
                            var controller = prop.GetValue(behaviour);
                            if (controller != null)
                            {
                                MelonLogger.Msg($"[BrowserNavigator] Got WorkflowController instance from {behaviourType.FullName}");
                                return controller;
                            }
                            else
                            {
                                MelonLogger.Msg($"[BrowserNavigator] WorkflowController property was null on {behaviourType.FullName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[BrowserNavigator] Error getting WorkflowController from {behaviourType.FullName}: {ex.Message}");
                        }
                    }

                    if (behaviourType.FullName?.Contains("DuelScene") == true)
                        duelSceneCount++;
                }

                MelonLogger.Msg($"[BrowserNavigator] Checked {duelSceneCount} DuelScene types, none had WorkflowController");

                MelonLogger.Warning("[BrowserNavigator] WorkflowController not found on any manager");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] Error finding WorkflowController: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the CurrentWorkflow property from WorkflowController.
        /// </summary>
        private object GetCurrentWorkflow(object workflowController)
        {
            var type = workflowController.GetType();
            var prop = type.GetProperty("CurrentWorkflow", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                MelonLogger.Warning("[BrowserNavigator] CurrentWorkflow property not found");
                return null;
            }

            return prop.GetValue(workflowController);
        }

        /// <summary>
        /// Gets the IEntityView interface from a card's DuelScene_CDC component.
        /// </summary>
        private object GetEntityViewFromCard(GameObject card)
        {
            // Find DuelScene_CDC component
            var cdcComponent = card.GetComponents<Component>()
                .FirstOrDefault(c => c.GetType().Name == "DuelScene_CDC");

            if (cdcComponent == null)
            {
                MelonLogger.Warning("[BrowserNavigator] DuelScene_CDC component not found on card");
                return null;
            }

            // Check if it implements IEntityView (has InstanceId property)
            var entityViewInterface = FindTypeByName("IEntityView");
            if (entityViewInterface != null && entityViewInterface.IsAssignableFrom(cdcComponent.GetType()))
            {
                MelonLogger.Msg($"[BrowserNavigator] DuelScene_CDC implements IEntityView");
                return cdcComponent;
            }

            // If not directly implementing, check for interfaces
            var interfaces = cdcComponent.GetType().GetInterfaces();
            foreach (var iface in interfaces)
            {
                if (iface.Name == "IEntityView" || iface.FullName?.Contains("IEntityView") == true)
                {
                    MelonLogger.Msg($"[BrowserNavigator] Found IEntityView interface: {iface.FullName}");
                    return cdcComponent;
                }
            }

            // Log available interfaces for debugging
            MelonLogger.Msg($"[BrowserNavigator] DuelScene_CDC interfaces:");
            foreach (var iface in interfaces)
            {
                MelonLogger.Msg($"[BrowserNavigator]   - {iface.FullName}");
            }

            // Even if we don't confirm IEntityView, try using the component directly
            // The workflow's OnClick might accept it anyway
            MelonLogger.Msg("[BrowserNavigator] Using DuelScene_CDC as entity (IEntityView not confirmed)");
            return cdcComponent;
        }

        /// <summary>
        /// Gets the SimpleInteractionType.Primary enum value.
        /// </summary>
        private object GetSimpleInteractionTypePrimary()
        {
            var enumType = FindTypeByName("SimpleInteractionType");
            if (enumType == null)
            {
                MelonLogger.Warning("[BrowserNavigator] SimpleInteractionType enum not found");
                return null;
            }

            try
            {
                return Enum.Parse(enumType, "Primary");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BrowserNavigator] Failed to get Primary enum value: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Invokes OnClick on the workflow with the entity and interaction type.
        /// </summary>
        private bool InvokeWorkflowOnClick(object workflow, object entityView, object interactionType)
        {
            var workflowType = workflow.GetType();

            // Try to find OnClick method
            var onClickMethod = workflowType.GetMethod("OnClick", BindingFlags.Public | BindingFlags.Instance);
            if (onClickMethod == null)
            {
                // Try to find it in interfaces
                foreach (var iface in workflowType.GetInterfaces())
                {
                    onClickMethod = iface.GetMethod("OnClick", BindingFlags.Public | BindingFlags.Instance);
                    if (onClickMethod != null) break;
                }
            }

            if (onClickMethod == null)
            {
                MelonLogger.Warning($"[BrowserNavigator] OnClick method not found on workflow type: {workflowType.FullName}");

                // Log ALL public methods for debugging
                MelonLogger.Msg("[BrowserNavigator] All public methods on workflow:");
                foreach (var method in workflowType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MelonLogger.Msg($"[BrowserNavigator]   {method.Name}({paramStr})");
                }

                // Also log base type methods
                if (workflowType.BaseType != null && workflowType.BaseType != typeof(object))
                {
                    MelonLogger.Msg($"[BrowserNavigator] Base type: {workflowType.BaseType.FullName}");
                    foreach (var method in workflowType.BaseType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        MelonLogger.Msg($"[BrowserNavigator]   Base.{method.Name}({paramStr})");
                    }
                }

                // Log interfaces
                MelonLogger.Msg("[BrowserNavigator] Interfaces:");
                foreach (var iface in workflowType.GetInterfaces())
                {
                    MelonLogger.Msg($"[BrowserNavigator]   {iface.FullName}");
                }

                return false;
            }

            try
            {
                MelonLogger.Msg($"[BrowserNavigator] Invoking OnClick on {workflowType.Name}");
                onClickMethod.Invoke(workflow, new object[] { entityView, interactionType });
                MelonLogger.Msg("[BrowserNavigator] OnClick invoked successfully");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] OnClick invocation failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    MelonLogger.Error($"[BrowserNavigator] Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Finds a type by name across all loaded assemblies.
        /// </summary>
        private System.Type FindTypeByName(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name == typeName || t.FullName == typeName);
                    if (type != null) return type;
                }
                catch
                {
                    // Ignore assemblies that can't be scanned
                }
            }
            return null;
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
                // Track mulligan count for London phase
                _mulliganCount++;
                MelonLogger.Msg($"[BrowserNavigator] Mulligan taken, count now: {_mulliganCount}");
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
