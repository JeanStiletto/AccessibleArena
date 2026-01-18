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

        // Scry/Surveil zone navigation (top = keep, bottom = put on bottom)
        private enum ScryZone { None, Top, Bottom }
        private ScryZone _scryCurrentZone = ScryZone.None;
        private List<GameObject> _scryTopCards = new List<GameObject>();
        private List<GameObject> _scryBottomCards = new List<GameObject>();
        private int _scryCardIndex = -1;

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
        /// Checks if a browser type supports two-zone navigation (Scry, Surveil, etc.).
        /// These browsers have a "keep on top" and "put on bottom" zone.
        /// </summary>
        private bool IsScryLikeBrowser(string browserType)
        {
            if (string.IsNullOrEmpty(browserType)) return false;
            return browserType.Contains("Scry") ||
                   browserType.Contains("Surveil") ||
                   browserType.Contains("ReadAhead");
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

        /// <summary>
        /// Gets the CardBrowserCardHolder component from a holder GameObject.
        /// </summary>
        private Component GetCardBrowserHolderComponent(GameObject holder)
        {
            if (holder == null) return null;

            foreach (var comp in holder.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                {
                    return comp;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the LondonBrowser (CardGroupProvider) from the default browser holder.
        /// Returns null if not found or not in London browser mode.
        /// </summary>
        private object GetLondonBrowser()
        {
            var defaultHolder = FindActiveGameObject(HolderDefault);
            if (defaultHolder == null) return null;

            var cardBrowserHolder = GetCardBrowserHolderComponent(defaultHolder);
            if (cardBrowserHolder == null) return null;

            var providerProp = cardBrowserHolder.GetType().GetProperty("CardGroupProvider",
                BindingFlags.Public | BindingFlags.Instance);
            return providerProp?.GetValue(cardBrowserHolder);
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

            // C key - Enter keep/top zone (London hand, Scry top)
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (_browserTypeName == BrowserTypes.London)
                {
                    EnterLondonZone(LondonZone.Hand);
                    return true;
                }
                if (IsScryLikeBrowser(_browserTypeName))
                {
                    EnterScryZone(ScryZone.Top);
                    return true;
                }
                // For other browsers, C activates current card (like Enter)
                if (_browserCards.Count > 0 && _currentCardIndex >= 0)
                {
                    ActivateCurrentCard();
                    return true;
                }
            }

            // D key - Enter bottom zone (London library, Scry bottom)
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (_browserTypeName == BrowserTypes.London)
                {
                    EnterLondonZone(LondonZone.Library);
                    return true;
                }
                if (IsScryLikeBrowser(_browserTypeName))
                {
                    EnterScryZone(ScryZone.Bottom);
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
                // In Scry zone mode, use Scry navigation
                if (_scryCurrentZone != ScryZone.None && _scryCardIndex >= 0)
                {
                    NavigateScryPrevious();
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
                // In Scry zone mode, use Scry navigation
                if (_scryCurrentZone != ScryZone.None && _scryCardIndex >= 0)
                {
                    NavigateScryNext();
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
                // In Scry zone mode, use Scry activation (toggles card between zones)
                if (_scryCurrentZone != ScryZone.None && _scryCardIndex >= 0)
                {
                    ActivateCurrentScryCard();
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

            // Reset Scry zone state
            _scryCurrentZone = ScryZone.None;
            _scryTopCards.Clear();
            _scryBottomCards.Clear();
            _scryCardIndex = -1;

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
                var londonBrowser = GetLondonBrowser();
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

        #region Scry Zone Navigation

        /// <summary>
        /// Enters a specific zone in Scry/Surveil browser (top = keep, bottom = put on bottom).
        /// </summary>
        private void EnterScryZone(ScryZone zone)
        {
            _scryCurrentZone = zone;
            _scryCardIndex = -1;

            // Refresh card lists from holders
            RefreshScryCardLists();

            var currentList = zone == ScryZone.Top ? _scryTopCards : _scryBottomCards;
            string zoneName = zone == ScryZone.Top ? Strings.KeepOnTop : Strings.PutOnBottom;

            if (currentList.Count == 0)
            {
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.High);
            }
            else
            {
                // Navigate to first card
                _scryCardIndex = 0;
                var firstCard = currentList[0];
                var cardName = CardDetector.GetCardName(firstCard);
                _announcer.Announce($"{zoneName}: {currentList.Count} cards. {cardName}, 1 of {currentList.Count}", AnnouncementPriority.High);

                // Update CardInfoNavigator with this card
                var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
                cardNav?.PrepareForCard(firstCard, ZoneType.Library);
            }

            MelonLogger.Msg($"[BrowserNavigator] Entered Scry zone: {zoneName}, {currentList.Count} cards");
        }

        /// <summary>
        /// Refreshes the Scry top and bottom card lists from the browser holders.
        /// </summary>
        private void RefreshScryCardLists()
        {
            _scryTopCards.Clear();
            _scryBottomCards.Clear();

            // Find cards in BrowserCardHolder_Default (top/keep)
            var defaultHolder = FindActiveGameObject(HolderDefault);
            if (defaultHolder != null)
            {
                foreach (Transform child in defaultHolder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (!CardDetector.IsCard(child.gameObject)) continue;

                    string cardName = CardDetector.GetCardName(child.gameObject);
                    if (IsValidCardName(cardName) && !IsDuplicateCard(child.gameObject, _scryTopCards))
                    {
                        _scryTopCards.Add(child.gameObject);
                    }
                }
            }

            // Find cards in BrowserCardHolder_ViewDismiss (bottom)
            var dismissHolder = FindActiveGameObject(HolderViewDismiss);
            if (dismissHolder != null)
            {
                foreach (Transform child in dismissHolder.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (!CardDetector.IsCard(child.gameObject)) continue;

                    string cardName = CardDetector.GetCardName(child.gameObject);
                    if (IsValidCardName(cardName) && !IsDuplicateCard(child.gameObject, _scryBottomCards))
                    {
                        _scryBottomCards.Add(child.gameObject);
                    }
                }
            }

            // Sort by horizontal position (left to right)
            _scryTopCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            _scryBottomCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            MelonLogger.Msg($"[BrowserNavigator] Refreshed Scry lists - Top: {_scryTopCards.Count}, Bottom: {_scryBottomCards.Count}");
        }

        /// <summary>
        /// Navigates to the next card in the current Scry zone.
        /// </summary>
        private void NavigateScryNext()
        {
            var currentList = _scryCurrentZone == ScryZone.Top ? _scryTopCards : _scryBottomCards;
            if (currentList.Count == 0)
            {
                string zoneName = _scryCurrentZone == ScryZone.Top ? Strings.KeepOnTop : Strings.PutOnBottom;
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.Normal);
                return;
            }

            _scryCardIndex++;
            if (_scryCardIndex >= currentList.Count)
                _scryCardIndex = 0;

            AnnounceCurrentScryCard();
        }

        /// <summary>
        /// Navigates to the previous card in the current Scry zone.
        /// </summary>
        private void NavigateScryPrevious()
        {
            var currentList = _scryCurrentZone == ScryZone.Top ? _scryTopCards : _scryBottomCards;
            if (currentList.Count == 0)
            {
                string zoneName = _scryCurrentZone == ScryZone.Top ? Strings.KeepOnTop : Strings.PutOnBottom;
                _announcer.Announce($"{zoneName}: empty", AnnouncementPriority.Normal);
                return;
            }

            _scryCardIndex--;
            if (_scryCardIndex < 0)
                _scryCardIndex = currentList.Count - 1;

            AnnounceCurrentScryCard();
        }

        /// <summary>
        /// Announces the current card in Scry zone navigation.
        /// </summary>
        private void AnnounceCurrentScryCard()
        {
            var currentList = _scryCurrentZone == ScryZone.Top ? _scryTopCards : _scryBottomCards;
            if (_scryCardIndex < 0 || _scryCardIndex >= currentList.Count) return;

            var card = currentList[_scryCardIndex];
            var cardName = CardDetector.GetCardName(card);
            string zoneName = _scryCurrentZone == ScryZone.Top ? Strings.KeepOnTop : Strings.PutOnBottom;

            _announcer.Announce($"{cardName}, {zoneName}, {_scryCardIndex + 1} of {currentList.Count}", AnnouncementPriority.High);

            // Update CardInfoNavigator
            var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
            cardNav?.PrepareForCard(card, ZoneType.Library);
        }

        /// <summary>
        /// Activates (toggles) the current card in Scry zone navigation.
        /// Moves the card to the other zone via UI click.
        /// </summary>
        private void ActivateCurrentScryCard()
        {
            var currentList = _scryCurrentZone == ScryZone.Top ? _scryTopCards : _scryBottomCards;
            if (_scryCardIndex < 0 || _scryCardIndex >= currentList.Count)
            {
                _announcer.Announce("No card selected", AnnouncementPriority.Normal);
                return;
            }

            var card = currentList[_scryCardIndex];
            var cardName = CardDetector.GetCardName(card);
            string fromZone = _scryCurrentZone == ScryZone.Top ? Strings.KeepOnTop : Strings.PutOnBottom;

            MelonLogger.Msg($"[BrowserNavigator] Activating Scry card: {cardName} from {fromZone}");

            // Use drag-based activation (same approach as London mulligan)
            bool success = TryActivateCardViaScryBrowser(card, cardName);
            if (success)
            {
                // Wait for state update and announce
                MelonCoroutines.Start(RefreshScryZoneAfterDelay(cardName));
            }
            else
            {
                _announcer.Announce($"Could not move {cardName}", AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Activates a card via the Scry/Surveil browser by moving it between holders.
        /// Uses RemoveCard on source holder and AddCard on target holder.
        /// </summary>
        private bool TryActivateCardViaScryBrowser(GameObject card, string cardName)
        {
            MelonLogger.Msg($"[BrowserNavigator] Attempting Scry card move for: {cardName}");

            try
            {
                // Step 1: Find both holders
                var defaultHolder = FindActiveGameObject(HolderDefault);
                var dismissHolder = FindActiveGameObject(HolderViewDismiss);

                if (defaultHolder == null || dismissHolder == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] Could not find both browser holders");
                    return false;
                }

                // Step 2: Get CardBrowserCardHolder components from both holders
                var defaultHolderComp = GetCardBrowserHolderComponent(defaultHolder);
                var dismissHolderComp = GetCardBrowserHolderComponent(dismissHolder);

                if (defaultHolderComp == null || dismissHolderComp == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] CardBrowserCardHolder components not found on holders");
                    return false;
                }

                Component sourceHolderComp = null;
                Component targetHolderComp = null;
                bool isInDefaultHolder = false;

                // Step 3: Determine which holder the card is in
                foreach (Transform child in defaultHolder.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject == card)
                    {
                        sourceHolderComp = defaultHolderComp;
                        targetHolderComp = dismissHolderComp;
                        isInDefaultHolder = true;
                        break;
                    }
                }

                if (sourceHolderComp == null)
                {
                    foreach (Transform child in dismissHolder.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.gameObject == card)
                        {
                            sourceHolderComp = dismissHolderComp;
                            targetHolderComp = defaultHolderComp;
                            isInDefaultHolder = false;
                            break;
                        }
                    }
                }

                if (sourceHolderComp == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] Could not find card in either holder");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Card is in {(isInDefaultHolder ? "Default (top)" : "ViewDismiss (bottom)")} holder");

                // Step 4: Get DuelScene_CDC component from card
                var cardCDC = CardDetector.GetDuelSceneCDC(card);
                if (cardCDC == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] DuelScene_CDC component not found on card");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Got card CDC: {cardCDC.GetType().Name}");

                // Step 5: Remove card from source holder
                var sourceType = sourceHolderComp.GetType();
                var removeCardMethod = sourceType.GetMethod("RemoveCard", BindingFlags.Public | BindingFlags.Instance);
                if (removeCardMethod == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] RemoveCard method not found on source holder");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Removing card from source holder...");
                removeCardMethod.Invoke(sourceHolderComp, new object[] { cardCDC });
                MelonLogger.Msg($"[BrowserNavigator] Card removed from source holder");

                // Step 6: Add card to target holder
                var targetType = targetHolderComp.GetType();

                // Try AddCard first (from base class CardHolderBase)
                var addCardMethod = targetType.GetMethod("AddCard", BindingFlags.Public | BindingFlags.Instance);
                if (addCardMethod == null)
                {
                    // Try base class
                    var baseType = targetType.BaseType;
                    while (baseType != null && addCardMethod == null)
                    {
                        addCardMethod = baseType.GetMethod("AddCard", BindingFlags.Public | BindingFlags.Instance);
                        baseType = baseType.BaseType;
                    }
                }

                if (addCardMethod == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] AddCard method not found on target holder");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Adding card to target holder...");
                addCardMethod.Invoke(targetHolderComp, new object[] { cardCDC });
                MelonLogger.Msg($"[BrowserNavigator] Card added to target holder successfully!");

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] Error in TryActivateCardViaScryBrowser: {ex.Message}");
                MelonLogger.Error($"[BrowserNavigator] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Refreshes Scry zone after card activation with a short delay.
        /// </summary>
        private System.Collections.IEnumerator RefreshScryZoneAfterDelay(string cardName)
        {
            yield return new WaitForSeconds(0.2f);

            RefreshScryCardLists();

            var currentList = _scryCurrentZone == ScryZone.Top ? _scryTopCards : _scryBottomCards;
            string zoneName = _scryCurrentZone == ScryZone.Top ? Strings.KeepOnTop : Strings.PutOnBottom;

            // Adjust index if needed
            if (_scryCardIndex >= currentList.Count)
                _scryCardIndex = currentList.Count - 1;

            if (currentList.Count == 0)
            {
                // Card moved to the other zone, announce the result
                string newZone = _scryCurrentZone == ScryZone.Top ? Strings.PutOnBottom : Strings.KeepOnTop;
                _announcer.Announce($"{cardName} moved to {newZone}. {zoneName}: empty", AnnouncementPriority.Normal);
            }
            else if (_scryCardIndex >= 0)
            {
                var currentCard = currentList[_scryCardIndex];
                var currentCardName = CardDetector.GetCardName(currentCard);

                // Announce what happened and current position
                string newZone = _scryCurrentZone == ScryZone.Top ? Strings.PutOnBottom : Strings.KeepOnTop;
                _announcer.Announce($"{cardName} moved to {newZone}. Now: {currentCardName}, {_scryCardIndex + 1} of {currentList.Count}", AnnouncementPriority.Normal);

                // Update CardInfoNavigator
                var cardNav = MTGAAccessibilityMod.Instance?.CardNavigator;
                cardNav?.PrepareForCard(currentCard, ZoneType.Library);
            }
        }

        #endregion

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
                var londonBrowser = GetLondonBrowser();
                if (londonBrowser == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] LondonBrowser not found");
                    return false;
                }

                MelonLogger.Msg($"[BrowserNavigator] Got LondonBrowser: {londonBrowser.GetType().Name}");

                // Get DuelScene_CDC component from card
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
                var londonBrowser = GetLondonBrowser();
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
