using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Services.PanelDetection;
using System.Collections.Generic;
using System.Linq;

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
            "Content - Audio"
        };

        private const float RescanDelaySeconds = 0.3f;

        #endregion

        #region State

        private GameObject _settingsContentPanel;
        private GameObject _settingsMenuObject; // Fallback for Login scene (no content panels)
        private string _lastPanelName;
        private float _rescanDelay;

        // Popup overlay handling
        private readonly PopupHandler _popupHandler;
        private bool _isPopupActive;

        #endregion

        public override string NavigatorId => "SettingsMenu";
        public override string ScreenName => GetSettingsScreenName();

        // Priority 90 - higher than DuelNavigator (70) and GeneralMenuNavigator (15)
        public override int Priority => 90;

        public SettingsMenuNavigator(IAnnouncementService announcer) : base(announcer)
        {
            _popupHandler = new PopupHandler("SettingsMenu", announcer);
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
            foreach (var panelName in SettingsPanelNames)
            {
                var panel = GameObject.Find(panelName);
                if (panel != null && panel.activeInHierarchy)
                {
                    _settingsContentPanel = panel;
                    return true;
                }
            }

            // Harmony says settings is open but content panel not found yet
            // Return true anyway - we trust Harmony, elements will appear shortly
            return true;
        }

        /// <summary>
        /// Get the screen name based on current settings panel or popup.
        /// </summary>
        private string GetSettingsScreenName()
        {
            // If popup is active, return popup name
            if (_isPopupActive && _popupHandler.IsActive)
            {
                return Models.Strings.ScreenConfirmation;
            }

            if (_settingsContentPanel == null)
                return Models.Strings.ScreenSettings;

            return _settingsContentPanel.name switch
            {
                "Content - MainMenu" => Models.Strings.ScreenSettings,
                "Content - Gameplay" => Models.Strings.ScreenSettingsGameplay,
                "Content - Graphics" => Models.Strings.ScreenSettingsGraphics,
                "Content - Audio" => Models.Strings.ScreenSettingsAudio,
                _ => Models.Strings.ScreenSettings
            };
        }

        #endregion

        #region Lifecycle

        public override void Update()
        {
            // Handle rescan delay for submenu changes or initial element loading
            if (_rescanDelay > 0)
            {
                _rescanDelay -= Time.deltaTime;
                if (_rescanDelay <= 0)
                {
                    PerformRescan();
                }
            }

            // Custom activation logic - allows activating with 0 elements
            // We trust Harmony: if IsSettingsMenuOpen is true, settings IS open
            if (!_isActive)
            {
                if (DetectScreen())
                {
                    // Activate even with 0 elements - we trust Harmony
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
                return;
            }

            // Check if settings panel changed (submenu navigation)
            if (_settingsContentPanel != null)
            {
                string currentPanelName = _settingsContentPanel.name;
                if (_lastPanelName != currentPanelName)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Settings panel changed: {_lastPanelName} -> {currentPanelName}");
                    _lastPanelName = currentPanelName;
                    TriggerRescan();
                }
            }

            // Validate we should stay active
            if (!ValidateElements())
            {
                Deactivate();
                return;
            }

            // Handle input
            HandleInput();
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
            foreach (var panelName in SettingsPanelNames)
            {
                var panel = GameObject.Find(panelName);
                if (panel != null && panel.activeInHierarchy)
                {
                    _settingsContentPanel = panel;
                    break;
                }
            }

            // Trust Harmony - stay active even if elements temporarily empty
            // (e.g., during submenu transitions)
            return true;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _lastPanelName = _settingsContentPanel?.name;

            // Subscribe to panel changes to detect popups appearing/closing
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged += OnPanelChanged;
            }
        }

        protected override void OnDeactivating()
        {
            base.OnDeactivating();

            // Unsubscribe from panel changes
            if (PanelStateManager.Instance != null)
            {
                PanelStateManager.Instance.OnPanelChanged -= OnPanelChanged;
            }

            _settingsContentPanel = null;
            _settingsMenuObject = null;
            _lastPanelName = null;
            _isPopupActive = false;
            _popupHandler.Clear();
        }

        /// <summary>
        /// Handle panel changes - detect popups appearing on top of settings.
        /// </summary>
        private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)
        {
            if (!_isActive) return;

            // Check if a popup appeared on top of settings
            if (newPanel != null && PopupHandler.IsPopupPanel(newPanel))
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup detected on top of settings: {newPanel.Name}");
                _isPopupActive = true;
                _popupHandler.OnPopupDetected(newPanel.GameObject);
            }
            // Check if popup closed and we're back to settings
            else if (_isPopupActive && (newPanel == null || newPanel.Name == "SettingsMenu"))
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup closed, returning to settings");
                _isPopupActive = false;
                _popupHandler.Clear();
                TriggerRescan();
            }
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            // If popup is active, don't discover settings elements
            // (popup is handled by PopupHandler, not BaseNavigator elements)
            if (_isPopupActive)
                return;

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
                }
            }

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

            // Find Settings custom controls (dropdowns, steppers)
            FindSettingsCustomControls(TryAddElement);

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

                AddElement(obj, announcement, carouselInfo);
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

        private string BuildAnnouncement(UIElementClassifier.ClassificationResult classification)
        {
            if (string.IsNullOrEmpty(classification.RoleLabel))
                return classification.Label;

            return $"{classification.Label}, {classification.RoleLabel}";
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

        protected override bool HandleEarlyInput()
        {
            if (_isPopupActive)
            {
                _popupHandler.HandleInput();
                return true; // Consume all input while popup is active
            }
            return false;
        }

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
        /// Priority: popup -> submenu -> close settings
        /// </summary>
        private bool HandleSettingsBack()
        {
            // If popup is active, dismiss it first
            if (_isPopupActive && _popupHandler.IsActive)
            {
                MelonLogger.Msg($"[{NavigatorId}] Dismissing popup via backspace");
                _popupHandler.DismissPopup();
                _isPopupActive = false;
                TriggerRescan();
                return true;
            }

            if (_settingsContentPanel == null)
                return false;

            string panelName = _settingsContentPanel.name;
            MelonLogger.Msg($"[{NavigatorId}] Settings back from: {panelName}");

            // Check if we're in a submenu
            bool isInSubmenu = panelName != "Content - MainMenu" &&
                              (panelName == "Content - Audio" ||
                               panelName == "Content - Graphics" ||
                               panelName == "Content - Gameplay");

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

            // Close settings menu entirely
            return CloseSettingsMenu();
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
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

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
            // Check if this is a submenu button - trigger rescan after activation
            if (IsSettingsSubmenuButton(element))
            {
                MelonLogger.Msg($"[{NavigatorId}] Settings submenu button activated: {element.name}");
                UIActivator.Activate(element);
                TriggerRescan();
                return true;
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

            // Announce the change (only if not in popup mode - popup has its own announcements)
            if (!_isPopupActive)
            {
                string announcement = GetActivationAnnouncement();
                _announcer.Announce(announcement, Models.AnnouncementPriority.High);
            }
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
            return $"{menuName}. {_elements.Count} items. {Models.Strings.NavigateWithArrows}, Enter to select.";
        }

        #endregion
    }
}
