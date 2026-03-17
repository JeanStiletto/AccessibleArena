using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Services.PanelDetection;
using System.Collections.Generic;
using System.Linq;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Dedicated navigator for Settings menu. Works in all scenes including duels.
    /// Priority 90 ensures it takes over when settings panel is visible.
    /// </summary>
    public class SettingsMenuNavigator : BaseNavigator
    {
        #region Configuration

        // Settings submenu panel names (same as MenuScreenDetector)
        private static readonly string[] SettingsPanelNames = new[]
        {
            "Content - MainMenu",
            "Content - Gameplay",
            "Content - Graphics",
            "Content - Audio",
            "Content - Account"
        };

        private const float RescanDelaySeconds = 0.3f;

        #endregion

        #region State

        private GameObject _settingsContentPanel;
        private GameObject _settingsMenuObject; // Fallback for Login scene (no content panels)
        private string _lastPanelName;
        private float _rescanDelay;
        private bool _silentRescan; // Suppress screen re-announcement on silent rescans (e.g. after toggle)
        private bool _pendingDropdownAnnounce; // Announce current element label after post-dropdown-close rescan
        private bool _prevInDropdownOrSuppressed; // Tracks combined dropdown+suppressed state for exit detection
        private GameObject _pendingDropdownObject; // The dropdown element to announce after rescan (by reference)

        // Quick menu: shows only Concede/Options/Logout on initial settings open
        private bool _isInQuickMenu = true;
        private GameObject _optionsVirtualElement; // Carrier GO for virtual Options button

        // Web browser accessibility (for privacy policy, GDPR consent, etc.)
        private readonly WebBrowserAccessibility _webBrowser = new WebBrowserAccessibility();
        private bool _isWebBrowserActive;

        #endregion

        public override string NavigatorId => "SettingsMenu";
        public override string ScreenName => GetSettingsScreenName();

        // Priority 90 - higher than DuelNavigator (70) and GeneralMenuNavigator (15)
        public override int Priority => 90;

        public SettingsMenuNavigator(IAnnouncementService announcer) : base(announcer)
        {
        }

        #region Screen Detection

        /// <summary>
        /// Check if Settings menu is currently open.
        /// Uses Harmony-tracked panel state for precise detection.
        /// Works in all scenes including duels.
        /// </summary>
        protected override bool DetectScreen()
        {
            // Use Harmony-tracked state - no polling needed
            if (PanelStateManager.Instance?.IsSettingsMenuOpen != true)
            {
                _settingsContentPanel = null;
                _settingsMenuObject = null;
                return false;
            }

            // Settings is open (per Harmony), find the content panel for element discovery
            _settingsContentPanel = FindActiveSettingsPanel();

            // Harmony says settings is open - return true even if panel not found yet
            // (elements will appear shortly during submenu transitions)
            return true;
        }

        /// <summary>
        /// Get the screen name based on current settings panel or popup.
        /// </summary>
        private string GetSettingsScreenName()
        {
            // If popup is active, return popup name
            if (IsInPopupMode)
            {
                return Models.Strings.ScreenConfirmation;
            }

            if (_isInQuickMenu)
                return Models.Strings.ScreenQuickMenu;

            if (_settingsContentPanel == null)
                return Models.Strings.ScreenSettings;

            return _settingsContentPanel.name switch
            {
                "Content - MainMenu" => Models.Strings.ScreenSettings,
                "Content - Gameplay" => Models.Strings.ScreenSettingsGameplay,
                "Content - Graphics" => Models.Strings.ScreenSettingsGraphics,
                "Content - Audio" => Models.Strings.ScreenSettingsAudio,
                "Content - Account" => Models.Strings.ScreenSettingsAccount,
                _ => Models.Strings.ScreenSettings
            };
        }

        #endregion

        #region Lifecycle

        public override void Update()
        {
            // Web browser takes full control when active
            if (_isWebBrowserActive)
            {
                _webBrowser.Update();
                if (!_webBrowser.IsActive)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Web browser became inactive, returning to settings");
                    _isWebBrowserActive = false;
                    TriggerRescan();
                }
                else
                {
                    _webBrowser.HandleInput();
                }
                return;
            }

            // Handle rescan delay for submenu changes or initial element loading
            if (_rescanDelay > 0)
            {
                _rescanDelay -= Time.deltaTime;
                if (_rescanDelay <= 0)
                {
                    PerformRescan();
                }
            }

            // Check if settings panel changed (submenu navigation)
            // Skip in quick menu mode - we don't care about panel content changes
            if (_isActive && !_isInQuickMenu && _settingsContentPanel != null)
            {
                string currentPanelName = _settingsContentPanel.name;
                if (_lastPanelName != currentPanelName)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Settings panel changed: {_lastPanelName} -> {currentPanelName}");
                    _lastPanelName = currentPanelName;
                    TriggerRescan();
                }
            }

            // Detect dropdown+suppression exit transition to announce the newly selected value.
            // We can't rely on SyncIndexToFocusedElement (it requires justExitedDropdown which
            // never fires during suppression), so we track the combined state here instead.
            if (_isActive)
            {
                bool nowInDropdownOrSuppressed = DropdownStateManager.IsInDropdownMode || DropdownStateManager.IsSuppressed;
                if (_prevInDropdownOrSuppressed && !nowInDropdownOrSuppressed)
                {
                    // We just fully exited dropdown mode. If the focused element is a dropdown,
                    // schedule a silent rescan so we can read its refreshed label.
                    if (_currentIndex >= 0 && _currentIndex < _elements.Count &&
                        _elements[_currentIndex].Role == UIElementClassifier.ElementRole.Dropdown)
                    {
                        _pendingDropdownObject = _elements[_currentIndex].GameObject;
                        _pendingDropdownAnnounce = true;
                        _silentRescan = true;
                        if (_rescanDelay <= 0)
                            TriggerRescan();
                        // If rescan already pending, it will pick up the flags
                        MelonLogger.Msg($"[{NavigatorId}] Dropdown exit detected, scheduling value announcement");
                    }
                }
                _prevInDropdownOrSuppressed = nowInDropdownOrSuppressed;
            }

            // Base handles: activation, delayed announcements, validation, input, input field tracking
            base.Update();
        }

        /// <summary>
        /// Custom activation: allows activating with 0 elements.
        /// We trust Harmony: if IsSettingsMenuOpen is true, settings IS open.
        /// </summary>
        protected override void TryActivate()
        {
            if (!DetectScreen()) return;

            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();

            _isActive = true;
            _currentIndex = _elements.Count > 0 ? 0 : -1;

            MelonLogger.Msg($"[{NavigatorId}] Activated with {_elements.Count} elements");
            OnActivated();

            if (_elements.Count > 0)
            {
                _announcer.AnnounceInterrupt(GetActivationAnnouncement());
            }
            else
            {
                // No elements yet - schedule rescan, don't announce
                MelonLogger.Msg($"[{NavigatorId}] No elements yet, scheduling rescan");
                TriggerRescan();
            }
        }

        protected override bool ValidateElements()
        {
            // Use Harmony-tracked state - precise detection
            if (PanelStateManager.Instance?.IsSettingsMenuOpen != true)
            {
                _settingsContentPanel = null;
                _settingsMenuObject = null;
                return false;
            }

            // Update content panel reference
            _settingsContentPanel = FindActiveSettingsPanel();

            // Trust Harmony - stay active even if elements temporarily empty
            // (e.g., during submenu transitions)
            return true;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _isInQuickMenu = true;
            _lastPanelName = _settingsContentPanel?.name;
            EnablePopupDetection();
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged += OnPanelChanged;
        }

        protected override void OnDeactivating()
        {
            base.OnDeactivating();
            _isInQuickMenu = true;
            _optionsVirtualElement = null;
            DisablePopupDetection();
            if (PanelStateManager.Instance != null)
                PanelStateManager.Instance.OnPanelChanged -= OnPanelChanged;

            if (_isWebBrowserActive)
            {
                _webBrowser.Deactivate();
                _isWebBrowserActive = false;
            }

            _settingsContentPanel = null;
            _settingsMenuObject = null;
            _lastPanelName = null;
        }

        protected override void OnPopupClosed()
        {
            TriggerRescan();
        }

        /// <summary>
        /// Exclude web browser panels from base popup handling (they use WebBrowserAccessibility instead).
        /// </summary>
        protected override bool IsPopupExcluded(PanelInfo panel)
        {
            return IsWebBrowserPanel(panel);
        }

        /// <summary>
        /// Handle panel changes - detect web browser panels (privacy policy, GDPR consent, etc.)
        /// </summary>
        private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            if (newPanel != null && IsWebBrowserPanel(newPanel))
            {
                MelonLogger.Msg($"[{NavigatorId}] Web browser panel detected: {newPanel.Name}");
                _isWebBrowserActive = true;
                _webBrowser.Activate(newPanel.GameObject, _announcer);
            }
            else if (_isWebBrowserActive && newPanel == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Web browser closed, returning to settings");
                _webBrowser.Deactivate();
                _isWebBrowserActive = false;
                TriggerRescan();
            }
        }

        private static bool IsWebBrowserPanel(PanelInfo panel)
        {
            if (panel == null || panel.GameObject == null) return false;
            return panel.GameObject.GetComponentInChildren<ZenFulcrum.EmbeddedBrowser.Browser>(true) != null;
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            // If popup is active, don't discover settings elements
            if (IsInPopupMode)
                return;

            // Login scene fallback (no content panels) - skip quick menu
            if (_settingsContentPanel == null)
            {
                _isInQuickMenu = false;
                DiscoverAllElements();
                return;
            }

            // Always do full discovery first
            DiscoverAllElements();

            // In quick menu mode on MainMenu, filter to only show action buttons + Options
            if (_isInQuickMenu && _settingsContentPanel?.name == "Content - MainMenu")
            {
                ApplyQuickMenuFilter();
            }
        }

        /// <summary>
        /// Filter the discovered elements to only show quick menu items:
        /// Concede (if visible), Options (virtual), Logout.
        /// </summary>
        private void ApplyQuickMenuFilter()
        {
            NavigableElement? concedeElement = null;
            NavigableElement? logoutElement = null;

            for (int i = 0; i < _elements.Count; i++)
            {
                var elem = _elements[i];
                if (elem.GameObject == null) continue;
                string goName = elem.GameObject.name;

                if (goName.IndexOf("Concede", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    concedeElement = elem;
                else if (goName.IndexOf("LogOut", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    logoutElement = elem;
            }

            _elements.Clear();

            // Add in order: Concede (if visible), Options, Logout
            if (concedeElement.HasValue)
                _elements.Add(concedeElement.Value);

            // Virtual Options button - uses SettingsMenu GO as carrier
            _optionsVirtualElement = FindSettingsMenuObject();
            if (_optionsVirtualElement != null)
            {
                string optionsLabel = BuildLabel(Models.Strings.QuickMenuOptions,
                    Models.Strings.RoleButton, UIElementClassifier.ElementRole.Button);
                AddElement(_optionsVirtualElement, optionsLabel, default, null, null,
                    UIElementClassifier.ElementRole.Button);
            }

            if (logoutElement.HasValue)
                _elements.Add(logoutElement.Value);

            MelonLogger.Msg($"[{NavigatorId}] Quick menu: {_elements.Count} items");
        }

        /// <summary>
        /// Full element discovery for settings panels (all buttons, controls, text blocks).
        /// </summary>
        private void DiscoverAllElements()
        {
            // Use content panel if available, otherwise fall back to SettingsMenu object
            // This handles Login scene where Content - MainMenu etc. don't exist
            GameObject searchRoot = _settingsContentPanel;
            if (searchRoot == null)
            {
                _settingsMenuObject = FindSettingsMenuObject();
                searchRoot = _settingsMenuObject;
                if (searchRoot == null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] No settings content panel or SettingsMenu object found");
                    return;
                }
                MelonLogger.Msg($"[{NavigatorId}] Using SettingsMenu object as fallback for element discovery");
            }

            var addedObjects = new HashSet<GameObject>();
            var discoveredElements = new List<(GameObject obj, UIElementClassifier.ClassificationResult classification, float sortOrder)>();

            void TryAddElement(GameObject obj)
            {
                if (obj == null || !obj.activeInHierarchy) return;
                if (addedObjects.Contains(obj)) return;

                // Only include elements that are children of the settings panel/menu
                if (!IsChildOf(obj, searchRoot))
                    return;

                var classification = UIElementClassifier.Classify(obj);
                if (classification.IsNavigable && classification.ShouldAnnounce)
                {
                    var pos = obj.transform.position;
                    float sortOrder = -pos.y * 1000 + pos.x;
                    discoveredElements.Add((obj, classification, sortOrder));
                    addedObjects.Add(obj);

                    // If this element wraps a slider child, mark the child as handled
                    // to prevent duplicate entries when FindObjectsOfType<Slider> runs
                    if (classification.SliderComponent != null && classification.SliderComponent.gameObject != obj)
                    {
                        addedObjects.Add(classification.SliderComponent.gameObject);
                    }
                }
            }

            // Find Settings custom controls (dropdowns, steppers) FIRST
            // so that parent stepper controls with slider children get priority
            FindSettingsCustomControls(TryAddElement);

            // Find CustomButtons in settings
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
                {
                    TryAddElement(mb.gameObject);
                }
            }

            // Find standard Unity UI elements
            foreach (var btn in GameObject.FindObjectsOfType<Button>())
            {
                if (btn != null && btn.interactable)
                    TryAddElement(btn.gameObject);
            }

            foreach (var toggle in GameObject.FindObjectsOfType<Toggle>())
            {
                if (toggle != null && toggle.interactable)
                    TryAddElement(toggle.gameObject);
            }

            foreach (var slider in GameObject.FindObjectsOfType<Slider>())
            {
                if (slider != null && slider.interactable)
                    TryAddElement(slider.gameObject);
            }

            foreach (var dropdown in GameObject.FindObjectsOfType<TMP_Dropdown>())
            {
                if (dropdown != null && dropdown.interactable)
                    TryAddElement(dropdown.gameObject);
            }

            // Remove ancestor elements when a more specific descendant is also discovered.
            // E.g., "Button - CaptureLog" (parent container) vs "CULL_CaptureLog - Button" (actual button).
            // The descendant is always the more functional element.
            for (int i = discoveredElements.Count - 1; i >= 0; i--)
            {
                var candidate = discoveredElements[i].obj;
                bool hasDescendant = false;
                for (int j = 0; j < discoveredElements.Count; j++)
                {
                    if (i == j) continue;
                    if (IsChildOf(discoveredElements[j].obj, candidate))
                    {
                        hasDescendant = true;
                        break;
                    }
                }
                if (hasDescendant)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Removing ancestor element: {candidate.name} (has navigable descendant)");
                    discoveredElements.RemoveAt(i);
                }
            }

            // Remove image-only elements (no text content, useless for screen reader).
            // E.g., ESRBImage is a Button with just a rating badge graphic and no text.
            // Only remove when the classifier's label is a GO-name fallback (no real text found).
            // Keep elements where the classifier found meaningful text (e.g., from parent/sibling).
            for (int i = discoveredElements.Count - 1; i >= 0; i--)
            {
                var go = discoveredElements[i].obj;
                bool hasText = false;
                foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
                    {
                        hasText = true;
                        break;
                    }
                }
                if (!hasText)
                {
                    // Check if the classifier found a meaningful label (text from parent/sibling)
                    string label = discoveredElements[i].classification.Label;
                    if (!string.IsNullOrEmpty(label) &&
                        !go.name.Replace(" ", "").Equals(label.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase))
                        continue; // Label differs from GO name - classifier found real text, keep it

                    MelonLogger.Msg($"[{NavigatorId}] Removing image-only element: {go.name}");
                    discoveredElements.RemoveAt(i);
                }
            }

            // Add static text blocks (explanatory paragraphs) before interactive elements.
            // These appear in submenus like PrivacyPolicy, ReportIssue, etc.
            DiscoverStaticTextBlocks(searchRoot, addedObjects);

            // Sort by position and add elements
            foreach (var (obj, classification, _) in discoveredElements.OrderBy(x => x.sortOrder))
            {
                string announcement = BuildAnnouncement(classification);

                CarouselInfo carouselInfo = classification.HasArrowNavigation
                    ? new CarouselInfo
                    {
                        HasArrowNavigation = true,
                        PreviousControl = classification.PreviousControl,
                        NextControl = classification.NextControl,
                        SliderComponent = classification.SliderComponent,
                        UseHoverActivation = classification.UseHoverActivation
                    }
                    : default;

                AddElement(obj, announcement, carouselInfo, null, null, classification.Role);
            }

            string searchRootName = _settingsContentPanel?.name ?? _settingsMenuObject?.name ?? "unknown";
            MelonLogger.Msg($"[{NavigatorId}] Discovered {_elements.Count} elements in {searchRootName}");
        }

        /// <summary>
        /// Find Settings custom controls (dropdowns and steppers).
        /// </summary>
        private void FindSettingsCustomControls(System.Action<GameObject> tryAddElement)
        {
            foreach (var transform in GameObject.FindObjectsOfType<Transform>())
            {
                if (transform == null || !transform.gameObject.activeInHierarchy)
                    continue;

                string name = transform.name;

                // All Settings controls start with "Control - "
                if (!name.StartsWith("Control - ", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check for dropdown pattern: "Control - *_Dropdown"
                if (name.EndsWith("_Dropdown", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Skip if there's a TMP_Dropdown child - those are detected separately
                    var tmpDropdown = transform.GetComponentInChildren<TMP_Dropdown>();
                    if (tmpDropdown != null)
                        continue;

                    GameObject clickableElement = FindClickableInDropdownControl(transform.gameObject);
                    if (clickableElement != null)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Found Settings dropdown: {name}");
                        tryAddElement(clickableElement);
                    }
                    continue;
                }

                // Check for stepper pattern: "Control - Setting:*" or "Control - *_Selector"
                bool isSettingControl = name.StartsWith("Control - Setting:", System.StringComparison.OrdinalIgnoreCase);
                bool isSelectorControl = name.EndsWith("_Selector", System.StringComparison.OrdinalIgnoreCase);

                if (!isSettingControl && !isSelectorControl)
                    continue;

                // Check if this control has Increment/Decrement buttons
                bool hasIncrement = false;
                bool hasDecrement = false;

                foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
                {
                    if (child == transform || !child.gameObject.activeInHierarchy)
                        continue;

                    var button = child.GetComponent<Button>();
                    if (button != null)
                    {
                        string childName = child.name;
                        if (childName.IndexOf("increment", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            hasIncrement = true;
                        else if (childName.IndexOf("decrement", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            hasDecrement = true;
                    }

                    if (hasIncrement && hasDecrement)
                        break;
                }

                if (hasIncrement || hasDecrement)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Found Settings stepper: {name}");
                    tryAddElement(transform.gameObject);
                }
            }
        }

        /// <summary>
        /// Find the clickable element inside a Settings dropdown control.
        /// </summary>
        private GameObject FindClickableInDropdownControl(GameObject control)
        {
            if (UIElementClassifier.HasCustomButton(control))
                return control;

            var button = control.GetComponent<Button>();
            if (button != null)
                return control;

            foreach (Transform child in control.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject == control || !child.gameObject.activeInHierarchy)
                    continue;

                if (UIElementClassifier.HasCustomButton(child.gameObject))
                    return child.gameObject;

                var childButton = child.GetComponent<Button>();
                if (childButton != null && childButton.interactable)
                    return child.gameObject;
            }

            if (UITextExtractor.HasActualText(control))
                return control;

            return null;
        }

        /// <summary>
        /// Find substantial static text blocks in the settings panel (e.g., privacy explanations).
        /// Adds them as read-only TextBlock elements so they can be read with the screen reader.
        /// </summary>
        private void DiscoverStaticTextBlocks(GameObject searchRoot, HashSet<GameObject> interactiveObjects)
        {
            const int MinTextLength = 30; // Skip short labels/headings

            foreach (var tmp in searchRoot.GetComponentsInChildren<TMP_Text>(false))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                string text = tmp.text?.Trim();
                if (string.IsNullOrWhiteSpace(text) || text.Length < MinTextLength) continue;

                // Skip if this text element is part of an interactive element
                bool isPartOfInteractive = false;
                foreach (var interactive in interactiveObjects)
                {
                    if (IsChildOf(tmp.gameObject, interactive) || tmp.gameObject == interactive)
                    {
                        isPartOfInteractive = true;
                        break;
                    }
                }
                if (isPartOfInteractive) continue;

                MelonLogger.Msg($"[{NavigatorId}] Static text block ({text.Length} chars): {text.Substring(0, System.Math.Min(60, text.Length))}...");
                AddTextBlock(text);
            }
        }

        private string BuildAnnouncement(UIElementClassifier.ClassificationResult classification)
        {
            if (classification.Role == UIElementClassifier.ElementRole.Toggle)
            {
                bool isOn = !classification.RoleLabel.Contains(Models.Strings.RoleUnchecked);
                string state = isOn ? Models.Strings.SettingOn : Models.Strings.SettingOff;
                return $"{classification.Label}, {state}";
            }
            return BuildLabel(classification.Label, classification.RoleLabel, classification.Role);
        }

        private static bool IsChildOf(GameObject child, GameObject parent)
        {
            if (child == null || parent == null) return false;

            Transform current = child.transform;
            while (current != null)
            {
                if (current.gameObject == parent)
                    return true;
                current = current.parent;
            }
            return false;
        }

        #endregion

        #region Input Handling

        protected override bool HandleCustomInput()
        {
            // Backspace: Navigate back in settings or close settings
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (UIFocusTracker.IsAnyInputFieldFocused())
                    return false; // Let backspace delete characters

                return HandleSettingsBack();
            }

            return false;
        }

        /// <summary>
        /// Handle back navigation within Settings menu.
        /// Priority: popup -> quick menu close -> submenu back -> main menu back to quick menu
        /// </summary>
        private bool HandleSettingsBack()
        {
            // If popup is active, dismiss it first (handled by base popup mode via Backspace)
            if (IsInPopupMode)
            {
                DismissPopup();
                return true;
            }

            // Quick menu: Backspace closes settings entirely
            if (_isInQuickMenu)
            {
                return CloseSettingsMenu();
            }

            if (_settingsContentPanel == null)
                return false;

            string panelName = _settingsContentPanel.name;
            MelonLogger.Msg($"[{NavigatorId}] Settings back from: {panelName}");

            // Any panel that's not MainMenu is a submenu
            bool isInSubmenu = panelName != "Content - MainMenu";

            if (isInSubmenu)
            {
                var backButton = FindSettingsBackButton();
                if (backButton != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Activating Settings submenu back button");
                    _announcer.AnnounceVerbose(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                    UIActivator.Activate(backButton);
                    TriggerRescan();
                    return true;
                }
            }

            // On MainMenu in full settings: go back to quick menu
            MelonLogger.Msg($"[{NavigatorId}] Returning to quick menu from full settings");
            _isInQuickMenu = true;
            _optionsVirtualElement = null;
            TriggerRescan();
            return true;
        }

        /// <summary>
        /// Find the back button within the current Settings panel.
        /// </summary>
        private GameObject FindSettingsBackButton()
        {
            if (_settingsContentPanel == null)
                return null;

            var headerTransform = _settingsContentPanel.transform.Find("Header");
            if (headerTransform == null)
                return null;

            var backContainer = headerTransform.Find("Back");
            if (backContainer == null)
                return null;

            var backButton = backContainer.Find("BackButton");
            if (backButton != null && backButton.gameObject.activeInHierarchy)
                return backButton.gameObject;

            return null;
        }

        /// <summary>
        /// Find the active settings content panel.
        /// Checks known panel names first, then searches for any active "Content - *" panel
        /// under the settings hierarchy (handles panels like PrivacyPolicy, ReportIssue, etc.)
        /// </summary>
        private GameObject FindActiveSettingsPanel()
        {
            // Check known panel names first (fast path)
            foreach (var panelName in SettingsPanelNames)
            {
                var panel = GameObject.Find(panelName);
                if (panel != null && panel.activeInHierarchy)
                    return panel;
            }

            // Dynamic fallback: search for any active "Content - *" panel
            // This catches panels we don't know by name (PrivacyPolicy, ReportIssue, etc.)
            foreach (var transform in GameObject.FindObjectsOfType<Transform>())
            {
                if (transform == null || !transform.gameObject.activeInHierarchy)
                    continue;

                string name = transform.name;
                if (!name.StartsWith("Content - "))
                    continue;

                // Verify it's inside a Settings hierarchy
                Transform parent = transform.parent;
                while (parent != null)
                {
                    if (parent.name.Contains("Settings") || parent.name == "Middle")
                        return transform.gameObject;
                    parent = parent.parent;
                }
            }

            return null;
        }

        /// <summary>
        /// Find the SettingsMenu MonoBehaviour's GameObject.
        /// Used as fallback for element discovery on Login scene where content panels don't exist.
        /// </summary>
        private GameObject FindSettingsMenuObject()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "SettingsMenu" && mb.gameObject.activeInHierarchy)
                    return mb.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Close the Settings menu by calling SettingsMenu.Close() directly.
        /// </summary>
        private bool CloseSettingsMenu()
        {
            MelonLogger.Msg($"[{NavigatorId}] Closing Settings menu");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "SettingsMenu")
                    continue;

                var closeMethod = mb.GetType().GetMethod("Close",
                    AllInstanceFlags);

                if (closeMethod != null)
                {
                    try
                    {
                        _announcer.Announce(Models.Strings.ClosingSettings, Models.AnnouncementPriority.High);
                        closeMethod.Invoke(mb, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Warning($"[{NavigatorId}] Error closing Settings: {ex.Message}");
                    }
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] Could not find SettingsMenu.Close() method");
            return false;
        }

        #endregion

        #region Element Activation

        protected override bool OnElementActivated(int index, GameObject element)
        {
            // Quick menu: virtual "Options" button -> switch to full settings
            if (_isInQuickMenu && element == _optionsVirtualElement)
            {
                MelonLogger.Msg($"[{NavigatorId}] Quick menu: Options selected, entering full settings");
                _isInQuickMenu = false;
                _optionsVirtualElement = null;
                TriggerRescan();
                return true;
            }

            // Check if this is a submenu button
            if (IsSettingsSubmenuButton(element))
            {
                MelonLogger.Msg($"[{NavigatorId}] Settings submenu button activated: {element.name}");
                UIActivator.Activate(element);
                // Don't call TriggerRescan() here — the Update() panel-change detection will
                // trigger it once the new panel is actually active, avoiding a premature rescan
                // that catches the transition midpoint (0 elements found).
                return true;
            }

            // For toggles: activate and re-announce new On/Off state immediately.
            // Use stored Role (not GetComponentInChildren) to avoid false-positive matches
            // on Toggles nested inside Dropdown item templates.
            if (index >= 0 && index < _elements.Count &&
                _elements[index].Role == UIElementClassifier.ElementRole.Toggle)
            {
                var toggle = element?.GetComponent<Toggle>() ?? element?.GetComponentInChildren<Toggle>();
                UIActivator.Activate(element);
                string label = UITextExtractor.GetText(element);
                bool isOn = toggle != null && toggle.isOn;
                string state = isOn ? Models.Strings.SettingOn : Models.Strings.SettingOff;
                _announcer.Announce($"{label}, {state}", Models.AnnouncementPriority.High);
                _silentRescan = true;
                TriggerRescan();
                return true;
            }

            // Settings BottomMenu link buttons (Link_Privacy, Link_ReportABug, etc.)
            // SimulatePointerClick may not trigger CustomButton.OnClick because
            // OnPointerUp requires _mouseOver state that isn't reliably set.
            // Invoke Click() directly which bypasses the mouseOver check.
            if (IsSettingsLinkButton(element))
            {
                MelonLogger.Msg($"[{NavigatorId}] Settings link activated: {element.name}");
                if (TryInvokeCustomButtonClick(element))
                {
                    TriggerRescan();
                    return true;
                }
                // Fall through to default UIActivator if no CustomButton found
            }

            return false;
        }

        /// <summary>
        /// Check if element is a Settings submenu button (Audio, Gameplay, Graphics, etc.)
        /// </summary>
        private bool IsSettingsSubmenuButton(GameObject element)
        {
            if (element == null) return false;

            string name = element.name;

            // Settings submenu buttons follow the pattern "Button_*" in the CenterMenu
            if (name.StartsWith("Button_"))
            {
                Transform parent = element.transform.parent;
                while (parent != null)
                {
                    if (parent.name == "CenterMenu" || parent.name.Contains("Settings"))
                        return true;
                    parent = parent.parent;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if element is a Settings link button (Link_* in BottomMenu).
        /// These are CustomButtons for privacy, report bug, account, etc.
        /// </summary>
        private static bool IsSettingsLinkButton(GameObject element)
        {
            if (element == null) return false;
            if (!element.name.StartsWith("Link_")) return false;

            Transform parent = element.transform.parent;
            while (parent != null)
            {
                if (parent.name == "BottomMenu" || parent.name.Contains("Settings"))
                    return true;
                parent = parent.parent;
            }
            return false;
        }

        /// <summary>
        /// Invoke Click() directly on a CustomButton component.
        /// This bypasses the _mouseOver check that SimulatePointerClick may fail on.
        /// </summary>
        private static bool TryInvokeCustomButtonClick(GameObject element)
        {
            foreach (var mb in element.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "CustomButton")
                {
                    var clickMethod = mb.GetType().GetMethod("Click", AllInstanceFlags);
                    if (clickMethod != null)
                    {
                        try
                        {
                            clickMethod.Invoke(mb, null);
                            return true;
                        }
                        catch (System.Exception ex)
                        {
                            MelonLogger.Warning($"[SettingsMenu] Error invoking Click(): {ex.Message}");
                        }
                    }
                }
            }
            return false;
        }

        #endregion

        #region Rescan

        private void TriggerRescan()
        {
            _rescanDelay = RescanDelaySeconds;
        }

        private void PerformRescan()
        {
            MelonLogger.Msg($"[{NavigatorId}] Performing rescan");

            // Remember current selection
            GameObject previousSelection = null;
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
            {
                previousSelection = _elements[_currentIndex].GameObject;
            }

            _elements.Clear();
            _currentIndex = 0;

            DiscoverElements();

            // Try to restore selection
            if (previousSelection != null)
            {
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == previousSelection)
                    {
                        _currentIndex = i;
                        break;
                    }
                }
            }

            // After a dropdown selection: find the dropdown by stored object reference (not _currentIndex,
            // which may have moved) and announce its freshly updated label.
            if (_pendingDropdownAnnounce && _pendingDropdownObject != null)
            {
                _pendingDropdownAnnounce = false;
                _silentRescan = false;
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == _pendingDropdownObject)
                    {
                        string label = _elements[i].Label;
                        _pendingDropdownObject = null;
                        if (!string.IsNullOrEmpty(label))
                        {
                            MelonLogger.Msg($"[{NavigatorId}] Announcing dropdown new value: {label}");
                            _announcer.Announce(label, Models.AnnouncementPriority.High);
                        }
                        return;
                    }
                }
                // Dropdown not found after rescan — fall through to normal announce
                _pendingDropdownObject = null;
            }

            // Announce the change (skip if this was a silent rescan, e.g. after toggle flip)
            if (!IsInPopupMode && !_silentRescan)
            {
                string announcement = GetActivationAnnouncement();
                _announcer.Announce(announcement, Models.AnnouncementPriority.High);
            }
            _silentRescan = false;
        }

        #endregion

        #region Announcement

        protected override string GetActivationAnnouncement()
        {
            string menuName = GetSettingsScreenName();

            if (_elements.Count == 0)
            {
                return $"{menuName}. No navigable items found.";
            }
            return $"{menuName}. {Models.Strings.NavigateHint}. {_elements.Count} items.";
        }

        #endregion
    }
}
