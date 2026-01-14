using UnityEngine;
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

        // Known browser type names (in Wotc.Mtga.DuelScene.Browsers namespace)
        private static readonly string[] KnownBrowserTypes = new[]
        {
            // Library manipulation
            "ScryBrowser",
            "ScryishBrowser",
            "SurveilBrowser",
            "ReadAheadBrowser",
            "LibrarySideboardBrowser",
            // Card selection
            "SelectCardsBrowser",
            "SelectCardsBrowser_MultiZone",
            "SelectGroupBrowser",
            "SelectManaTypeBrowser",
            "SelectNKeywordWithContextBrowser",
            "KeywordSelectionBrowser",
            // Combat/damage
            "AssignDamageBrowser",
            "AttachmentAndExileStackBrowser",
            // Opening hand
            "MulliganBrowser",
            "LondonBrowser",
            "OpeningHandBrowser",
            // Card ordering
            "OrderCardsBrowser",
            "SplitCardsBrowser",
            // Special
            "DungeonRoomSelectBrowser",
            "MutateOptionalActionBrowser",
            "OptionalActionBrowser",
            "RepeatSelectionBrowser",
            "YesNoBrowser",
            "InformationalBrowser",
            "ButtonScrollListBrowser",
            "ButtonSelectionBrowser"
        };

        // Track discovered browser types for one-time logging
        private static HashSet<string> _loggedBrowserTypes = new HashSet<string>();

        public BrowserNavigator(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        /// <summary>
        /// Returns true if a browser is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// The type name of the currently active browser.
        /// </summary>
        public string ActiveBrowserType => _browserTypeName;

        // Track if we've logged a search for debugging
        private static bool _hasLoggedBrowserSearch = false;
        private float _lastSearchLogTime = 0f;

        /// <summary>
        /// Updates browser detection state. Call each frame from DuelNavigator.
        /// </summary>
        public void Update()
        {
            var (browser, browserGo) = FindActiveBrowser();

            if (browser != null)
            {
                if (!_isActive)
                {
                    EnterBrowserMode(browser, browserGo);
                }
                // Don't re-enter if already active - this prevents spam
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
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check name for browser-related terms
                string name = go.name.ToLower();
                if (name.Contains("browser") || name.Contains("scry") || name.Contains("surveil") ||
                    name.Contains("mulligan") || name.Contains("cardholder") && name.Contains("select"))
                {
                    MelonLogger.Msg($"[BrowserNavigator] Found GO: {go.name}");
                    browserCount++;

                    // Log components
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp != null)
                        {
                            MelonLogger.Msg($"[BrowserNavigator]   Component: {comp.GetType().FullName}");
                        }
                    }
                }

                // Also check components for browser types
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (typeName.Contains("Browser"))
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Found component {typeName} on {go.name}");
                        browserCount++;
                    }
                }
            }

            // Also look for any UI with card selection patterns
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Look for card-like children in top-level containers
                if (go.name.Contains("CardHolder") || go.name.Contains("Selection") || go.name.Contains("Prompt"))
                {
                    int cardCount = 0;
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.gameObject.activeInHierarchy && CardDetector.IsCard(child.gameObject))
                        {
                            cardCount++;
                        }
                    }
                    if (cardCount > 0)
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Container {go.name} has {cardCount} cards");
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

            // Escape - cancel if possible
            if (Input.GetKeyDown(KeyCode.Escape))
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
        /// </summary>
        private (object browser, GameObject go) FindActiveBrowser()
        {
            // First, look for BrowserScaffold_* GameObjects (the actual browser UI)
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check for browser scaffold pattern: BrowserScaffold_Scry_*, BrowserScaffold_Surveil_*, etc.
                if (go.name.StartsWith("BrowserScaffold_"))
                {
                    // Extract browser type from name (e.g., "Scry" from "BrowserScaffold_Scry_Desktop_16x9(Clone)")
                    string scaffoldType = ExtractBrowserTypeFromScaffold(go.name);

                    if (!_loggedBrowserTypes.Contains(go.name))
                    {
                        _loggedBrowserTypes.Add(go.name);
                        MelonLogger.Msg($"[BrowserNavigator] Found browser scaffold: {go.name}, type: {scaffoldType}");
                    }

                    // Return a wrapper object with the scaffold info
                    return (new BrowserScaffoldInfo { ScaffoldName = go.name, BrowserType = scaffoldType }, go);
                }
            }

            // Fallback: look for CardBrowserCardHolder with cards (when scaffold not found)
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check for CardBrowserCardHolder component
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;

                    if (typeName == "CardBrowserCardHolder")
                    {
                        // Check if it has cards
                        int cardCount = 0;
                        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.gameObject.activeInHierarchy && CardDetector.IsCard(child.gameObject))
                                cardCount++;
                        }

                        if (cardCount > 0 && go.name.Contains("Browser"))
                        {
                            if (!_loggedBrowserTypes.Contains("CardBrowserCardHolder"))
                            {
                                _loggedBrowserTypes.Add("CardBrowserCardHolder");
                                MelonLogger.Msg($"[BrowserNavigator] Found CardBrowserCardHolder: {go.name} with {cardCount} cards");
                            }
                            return (comp, go);
                        }
                    }
                }
            }

            // Also check for known browser MonoBehaviour types (original approach as final fallback)
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;

                    var typeName = comp.GetType().Name;

                    if (KnownBrowserTypes.Contains(typeName))
                    {
                        // Check visibility
                        if (IsBrowserVisible(comp))
                        {
                            // Log new browser type discovery
                            if (!_loggedBrowserTypes.Contains(typeName))
                            {
                                _loggedBrowserTypes.Add(typeName);
                                LogBrowserDetails(comp, typeName);
                            }

                            return (comp, go);
                        }
                    }
                }
            }

            return (null, null);
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
            if (scaffoldName.StartsWith("BrowserScaffold_"))
            {
                string remainder = scaffoldName.Substring("BrowserScaffold_".Length);
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
        /// Checks if a browser component is currently visible.
        /// </summary>
        private bool IsBrowserVisible(object browser)
        {
            var type = browser.GetType();

            // Check IsVisible property
            var isVisibleProp = type.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Instance);
            if (isVisibleProp != null)
            {
                var val = isVisibleProp.GetValue(browser);
                if (val is bool visible && !visible)
                    return false;
            }

            // Check IsClosed property
            var isClosedProp = type.GetProperty("IsClosed", BindingFlags.Public | BindingFlags.Instance);
            if (isClosedProp != null)
            {
                var val = isClosedProp.GetValue(browser);
                if (val is bool closed && closed)
                    return false;
            }

            return true;
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

            // Notify DuelAnnouncer
            DuelAnnouncer.Instance?.OnLibraryBrowserClosed(); // Reset any previous state
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

            // For scaffold-based browsers, search for BrowserCardHolder_Default
            if (_activeBrowser is BrowserScaffoldInfo)
            {
                // Find BrowserCardHolder_Default which contains the scry card(s)
                // Only use Default holder - ViewDismiss shows same card in different position
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;

                    // Look in both holders but track unique cards by InstanceId
                    if (go.name == "BrowserCardHolder_Default" || go.name == "BrowserCardHolder_ViewDismiss")
                    {
                        foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                        {
                            if (!child.gameObject.activeInHierarchy) continue;
                            if (CardDetector.IsCard(child.gameObject))
                            {
                                // Filter out unknown/invalid cards
                                string cardName = CardDetector.GetCardName(child.gameObject);
                                if (string.IsNullOrEmpty(cardName) ||
                                    cardName.Contains("Unknown") ||
                                    cardName.Contains("unknown"))
                                {
                                    MelonLogger.Msg($"[BrowserNavigator] Skipping invalid card: {child.name} -> {cardName}");
                                    continue;
                                }

                                // Check if we already have a card with same name (avoid duplicates)
                                bool isDuplicate = false;
                                foreach (var existingCard in _browserCards)
                                {
                                    string existingName = CardDetector.GetCardName(existingCard);
                                    if (existingName == cardName)
                                    {
                                        isDuplicate = true;
                                        break;
                                    }
                                }

                                if (!isDuplicate)
                                {
                                    _browserCards.Add(child.gameObject);
                                    MelonLogger.Msg($"[BrowserNavigator] Found card in {go.name}: {child.name} -> {cardName}");
                                }
                            }
                        }
                    }
                }

                // Find buttons - search entire scene for browser-related buttons
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (go.name.Contains("Browser") || go.name.Contains("Prompt"))
                    {
                        FindBrowserButtons(go);
                    }
                }
            }
            else
            {
                // Original approach for component-based browsers
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

                    if (CardDetector.IsCard(child.gameObject))
                    {
                        if (!_browserCards.Contains(child.gameObject))
                        {
                            _browserCards.Add(child.gameObject);
                        }
                    }
                }

                // Find buttons in browser hierarchy
                FindBrowserButtons(_browserGameObject);
            }

            MelonLogger.Msg($"[BrowserNavigator] Found {_browserCards.Count} cards, {_browserButtons.Count} buttons");
        }

        /// <summary>
        /// Finds clickable buttons in the browser.
        /// </summary>
        private void FindBrowserButtons(GameObject root)
        {
            // Look for common button patterns
            var buttonPatterns = new[] { "Button", "Accept", "Confirm", "Cancel", "Done", "Keep", "Submit", "Yes", "No" };

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy) continue;

                var name = child.name;

                // Check if it's a button-like element
                bool isButton = false;
                foreach (var pattern in buttonPatterns)
                {
                    if (name.Contains(pattern))
                    {
                        isButton = true;
                        break;
                    }
                }

                if (!isButton) continue;

                // Verify it has a clickable component
                var hasClickable = child.GetComponent<UnityEngine.UI.Button>() != null ||
                                   child.GetComponent<UnityEngine.UI.Toggle>() != null ||
                                   child.GetComponent<UnityEngine.EventSystems.IPointerClickHandler>() != null;

                // Also check for CustomButton via reflection
                foreach (var comp in child.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name.Contains("Button"))
                    {
                        hasClickable = true;
                        break;
                    }
                }

                if (hasClickable && !_browserButtons.Contains(child.gameObject))
                {
                    _browserButtons.Add(child.gameObject);
                    MelonLogger.Msg($"[BrowserNavigator] Found button: {name}");
                }
            }
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

            // Library manipulation
            if (typeName.Contains("Scryish")) return "Scry";
            if (typeName.Contains("Scry")) return "Scry";
            if (typeName.Contains("Surveil")) return "Surveil";
            if (typeName.Contains("ReadAhead")) return "Read ahead";
            if (typeName.Contains("LibrarySideboard")) return "Search library";

            // Opening hand
            if (typeName.Contains("London")) return "Mulligan";
            if (typeName.Contains("Mulligan")) return "Mulligan";
            if (typeName.Contains("OpeningHand")) return "Opening hand";

            // Card ordering
            if (typeName.Contains("OrderCards")) return "Order cards";
            if (typeName.Contains("SplitCards")) return "Split cards into piles";

            // Combat
            if (typeName.Contains("AssignDamage")) return "Assign damage";
            if (typeName.Contains("Attachment")) return "View attachments";

            // Selection
            if (typeName.Contains("SelectCards")) return "Select cards";
            if (typeName.Contains("SelectGroup")) return "Select group";
            if (typeName.Contains("SelectMana")) return "Choose mana type";
            if (typeName.Contains("Keyword")) return "Choose keyword";

            // Special
            if (typeName.Contains("Dungeon")) return "Choose dungeon room";
            if (typeName.Contains("Mutate")) return "Mutate choice";
            if (typeName.Contains("YesNo")) return "Choose yes or no";
            if (typeName.Contains("Optional")) return "Optional action";
            if (typeName.Contains("Informational")) return "Information";

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
        /// </summary>
        private string GetCardSelectionState(GameObject card)
        {
            if (card == null) return null;

            // Check parent hierarchy to determine which holder the card is in
            Transform parent = card.transform.parent;
            while (parent != null)
            {
                string parentName = parent.name;

                if (parentName == "BrowserCardHolder_Default")
                {
                    return Strings.KeepOnTop;
                }
                else if (parentName == "BrowserCardHolder_ViewDismiss")
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
        /// Activates (clicks) the current card - toggles between keep/dismiss.
        /// For scry: moves card between Default (keep) and ViewDismiss (bottom) holders.
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

            // Get state BEFORE action
            string stateBefore = GetCardSelectionState(card);

            MelonLogger.Msg($"[BrowserNavigator] Activating card: {cardName}, state before: {stateBefore}");

            // For scry-style browsers, toggle by clicking the opposite holder
            if (_browserTypeName == "Scry" || _browserTypeName == "Surveil")
            {
                ToggleCardPosition(card, cardName, stateBefore);
            }
            else
            {
                // Default: just click the card
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
        }

        /// <summary>
        /// Toggles card position for scry-style browsers.
        /// Explores the CardBrowserCardHolder API to find the right method.
        /// </summary>
        private void ToggleCardPosition(GameObject card, string cardName, string stateBefore)
        {
            MelonLogger.Msg($"[BrowserNavigator] === TOGGLE CARD POSITION ===");

            // Log all button texts
            MelonLogger.Msg($"[BrowserNavigator] Available buttons ({_browserButtons.Count}):");
            foreach (var btn in _browserButtons)
            {
                string btnText = UITextExtractor.GetButtonText(btn, btn.name);
                MelonLogger.Msg($"[BrowserNavigator]   {btn.name} -> '{btnText}'");
            }

            // Find both card holders and their components
            GameObject defaultHolder = null;
            GameObject dismissHolder = null;
            object defaultHolderComp = null;
            object dismissHolderComp = null;

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                if (go.name == "BrowserCardHolder_Default")
                {
                    defaultHolder = go;
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                        {
                            defaultHolderComp = comp;
                            MelonLogger.Msg($"[BrowserNavigator] Found Default CardBrowserCardHolder");
                            LogComponentMethods(comp);
                        }
                    }
                }
                else if (go.name == "BrowserCardHolder_ViewDismiss")
                {
                    dismissHolder = go;
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "CardBrowserCardHolder")
                        {
                            dismissHolderComp = comp;
                            MelonLogger.Msg($"[BrowserNavigator] Found ViewDismiss CardBrowserCardHolder");
                        }
                    }
                }
            }

            // Also check the card itself for interactive components
            MelonLogger.Msg($"[BrowserNavigator] Card components:");
            foreach (var comp in card.GetComponents<Component>())
            {
                if (comp != null)
                {
                    var typeName = comp.GetType().Name;
                    MelonLogger.Msg($"[BrowserNavigator]   {typeName}");

                    // Look for card-specific browser interaction components
                    if (typeName.Contains("Browser") || typeName.Contains("Card"))
                    {
                        LogComponentMethods(comp);
                    }
                }
            }

            // Try different approaches based on what we found

            // Approach 1: Try to invoke MoveToHolder/Toggle methods on card or holder
            bool success = TryInvokeToggleMethod(card, defaultHolderComp, dismissHolderComp, stateBefore);
            if (success)
            {
                MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName, stateBefore));
                return;
            }

            // Approach 2: Try clicking the opposite holder directly
            if (stateBefore == "keep on top" && dismissHolder != null)
            {
                MelonLogger.Msg($"[BrowserNavigator] Trying to click dismiss holder");
                var result = UIActivator.SimulatePointerClick(dismissHolder);
                if (result.Success)
                {
                    MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName, stateBefore));
                    return;
                }
            }
            else if (stateBefore == "put on bottom" && defaultHolder != null)
            {
                MelonLogger.Msg($"[BrowserNavigator] Trying to click default holder");
                var result = UIActivator.SimulatePointerClick(defaultHolder);
                if (result.Success)
                {
                    MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName, stateBefore));
                    return;
                }
            }

            // Approach 3: Try clicking the card directly
            MelonLogger.Msg($"[BrowserNavigator] Trying direct card click");
            var cardResult = UIActivator.SimulatePointerClick(card);
            if (cardResult.Success)
            {
                MelonCoroutines.Start(AnnounceStateChangeAfterDelay(cardName, stateBefore));
            }
            else
            {
                _announcer.Announce(Strings.CouldNotTogglePosition, AnnouncementPriority.High);
            }
        }

        /// <summary>
        /// Logs public methods on a component for API discovery.
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
        /// Tries to invoke toggle/move methods on card or holder components.
        /// </summary>
        private bool TryInvokeToggleMethod(GameObject card, object defaultHolder, object dismissHolder, string currentState)
        {
            // Try common method names for toggling
            var toggleMethodNames = new[] { "Toggle", "Select", "Click", "OnClick", "OnPointerClick", "MoveCard", "TransferCard", "Dismiss", "ViewDismiss" };

            // Try on card components first
            foreach (var comp in card.GetComponents<Component>())
            {
                if (comp == null) continue;
                var type = comp.GetType();

                foreach (var methodName in toggleMethodNames)
                {
                    var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        try
                        {
                            MelonLogger.Msg($"[BrowserNavigator] Invoking {type.Name}.{methodName}()");
                            method.Invoke(comp, null);
                            return true;
                        }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Msg($"[BrowserNavigator] Method {methodName} failed: {ex.Message}");
                        }
                    }
                }
            }

            // Try on target holder
            var targetHolder = (currentState == "keep on top") ? dismissHolder : defaultHolder;
            if (targetHolder != null)
            {
                var type = targetHolder.GetType();
                foreach (var methodName in toggleMethodNames)
                {
                    var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (method != null && method.GetParameters().Length == 0)
                    {
                        try
                        {
                            MelonLogger.Msg($"[BrowserNavigator] Invoking {type.Name}.{methodName}()");
                            method.Invoke(targetHolder, null);
                            return true;
                        }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Msg($"[BrowserNavigator] Method {methodName} failed: {ex.Message}");
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a button by name pattern.
        /// </summary>
        private GameObject FindButtonByPattern(string pattern)
        {
            foreach (var button in _browserButtons)
            {
                if (button != null && button.name.Contains(pattern))
                {
                    return button;
                }
            }
            return null;
        }

        /// <summary>
        /// Simulates dragging a card to a target holder.
        /// </summary>
        private bool SimulateDragToHolder(GameObject card, GameObject targetHolder)
        {
            try
            {
                // Get positions
                var cardPos = card.transform.position;
                var targetPos = targetHolder.transform.position;

                MelonLogger.Msg($"[BrowserNavigator] Drag from {cardPos} to {targetPos}");

                // Create pointer event data
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem == null)
                {
                    MelonLogger.Warning("[BrowserNavigator] No EventSystem found");
                    return false;
                }

                var pointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem);

                // Simulate begin drag on card
                pointerData.position = Camera.main?.WorldToScreenPoint(cardPos) ?? Vector2.zero;
                pointerData.pointerDrag = card;

                var beginDragHandler = card.GetComponent<UnityEngine.EventSystems.IBeginDragHandler>();
                var dragHandler = card.GetComponent<UnityEngine.EventSystems.IDragHandler>();
                var endDragHandler = card.GetComponent<UnityEngine.EventSystems.IEndDragHandler>();
                var dropHandler = targetHolder.GetComponent<UnityEngine.EventSystems.IDropHandler>();

                MelonLogger.Msg($"[BrowserNavigator] Handlers - BeginDrag:{beginDragHandler != null}, Drag:{dragHandler != null}, EndDrag:{endDragHandler != null}, Drop:{dropHandler != null}");

                // Execute drag sequence
                if (beginDragHandler != null)
                {
                    beginDragHandler.OnBeginDrag(pointerData);
                }

                // Move to target
                pointerData.position = Camera.main?.WorldToScreenPoint(targetPos) ?? Vector2.zero;

                if (dragHandler != null)
                {
                    dragHandler.OnDrag(pointerData);
                }

                // Drop on target
                if (dropHandler != null)
                {
                    dropHandler.OnDrop(pointerData);
                }

                // End drag
                if (endDragHandler != null)
                {
                    endDragHandler.OnEndDrag(pointerData);
                }

                return true;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[BrowserNavigator] Drag simulation error: {ex.Message}");
                return false;
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
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.name == "BrowserCardHolder_Default" || go.name == "BrowserCardHolder_ViewDismiss")
                {
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
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
        /// </summary>
        private void ClickConfirmButton()
        {
            var confirmPatterns = new[] { "Confirm", "Accept", "Done", "Submit", "OK", "Yes", "Keep", "Primary" };

            foreach (var button in _browserButtons)
            {
                var name = button.name.ToLower();
                foreach (var pattern in confirmPatterns)
                {
                    if (name.Contains(pattern.ToLower()))
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Clicking confirm: {button.name}");
                        var result = UIActivator.SimulatePointerClick(button);
                        if (result.Success)
                        {
                            _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                            return;
                        }
                    }
                }
            }

            // Try finding PromptButton_Primary
            var primaryButton = GameObject.Find("PromptButton_Primary");
            if (primaryButton != null && primaryButton.activeInHierarchy)
            {
                var result = UIActivator.SimulatePointerClick(primaryButton);
                if (result.Success)
                {
                    _announcer.Announce(Strings.Confirmed, AnnouncementPriority.Normal);
                    return;
                }
            }

            _announcer.Announce(Strings.NoConfirmButton, AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Clicks the cancel button.
        /// </summary>
        private void ClickCancelButton()
        {
            var cancelPatterns = new[] { "Cancel", "No", "Back", "Close", "Secondary" };

            foreach (var button in _browserButtons)
            {
                var name = button.name.ToLower();
                foreach (var pattern in cancelPatterns)
                {
                    if (name.Contains(pattern.ToLower()))
                    {
                        MelonLogger.Msg($"[BrowserNavigator] Clicking cancel: {button.name}");
                        var result = UIActivator.SimulatePointerClick(button);
                        if (result.Success)
                        {
                            _announcer.Announce(Strings.Cancelled, AnnouncementPriority.Normal);
                            return;
                        }
                    }
                }
            }

            // Not finding cancel is OK - some browsers don't have it
            MelonLogger.Msg("[BrowserNavigator] No cancel button found");
        }

        /// <summary>
        /// Refreshes browser elements after a short delay.
        /// </summary>
        private IEnumerator RefreshAfterDelay()
        {
            yield return new WaitForSeconds(0.3f);

            DiscoverBrowserElements();

            // Adjust index if needed
            if (_browserCards.Count > 0)
            {
                if (_currentCardIndex >= _browserCards.Count)
                    _currentCardIndex = _browserCards.Count - 1;
                if (_currentCardIndex >= 0)
                    AnnounceCurrentCard();
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
