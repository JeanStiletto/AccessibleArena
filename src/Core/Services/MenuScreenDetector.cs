using UnityEngine;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Detects active content controllers and screens in the MTGA menu system.
    /// Provides screen name mapping and visibility checks for various UI elements.
    /// </summary>
    public class MenuScreenDetector
    {
        #region Configuration

        // Content controller types for screen detection
        private static readonly string[] ContentControllerTypes = new[]
        {
            "HomePageContentController",
            "DeckManagerController",
            "ProfileContentController",
            "ContentController_StoreCarousel",
            "MasteryContentController",
            "AchievementsContentController",
            "LearnToPlayControllerV2",
            "PackOpeningController",
            "CampaignGraphContentController",
            "WrapperDeckBuilder",
            "ConstructedDeckSelectController",
            "EventPageContentController"
        };

        // Settings submenu panel names
        private static readonly string[] SettingsPanelNames = new[]
        {
            "Content - MainMenu",
            "Content - Gameplay",
            "Content - Graphics",
            "Content - Audio"
        };

        // Carousel indicator patterns
        private static readonly string[] CarouselPatterns = new[]
        {
            "Carousel", "NavGradient_Previous", "NavGradient_Next", "WelcomeBundle", "EventBlade_Item"
        };

        // Color Challenge indicator patterns
        private static readonly string[] ColorChallengePatterns = new[]
        {
            "ColorMastery", "CampaignGraph", "Color Challenge"
        };

        #endregion

        #region State

        private string _activeContentController;
        private GameObject _activeControllerGameObject;
        private GameObject _navBarGameObject;
        private GameObject _settingsContentPanel;

        #endregion

        #region Public Properties

        /// <summary>
        /// The currently active content controller type name, or null if none.
        /// </summary>
        public string ActiveContentController => _activeContentController;

        /// <summary>
        /// The GameObject of the active content controller.
        /// </summary>
        public GameObject ActiveControllerGameObject => _activeControllerGameObject;

        /// <summary>
        /// Cached NavBar GameObject reference.
        /// </summary>
        public GameObject NavBarGameObject => _navBarGameObject;

        /// <summary>
        /// Cached Settings content panel reference.
        /// </summary>
        public GameObject SettingsContentPanel => _settingsContentPanel;

        #endregion

        #region Public Methods

        /// <summary>
        /// Clear all cached state. Call on scene change or deactivation.
        /// </summary>
        public void Reset()
        {
            _activeContentController = null;
            _activeControllerGameObject = null;
            _navBarGameObject = null;
            _settingsContentPanel = null;
        }

        /// <summary>
        /// Detect which content controller is currently active.
        /// Updates ActiveContentController and ActiveControllerGameObject.
        /// </summary>
        /// <returns>The type name of the active controller, or null if none detected.</returns>
        public string DetectActiveContentController()
        {
            // Cache NavBar if not already cached
            if (_navBarGameObject == null)
            {
                _navBarGameObject = GameObject.Find("NavBar_Desktop_16x9(Clone)");
                if (_navBarGameObject == null)
                    _navBarGameObject = GameObject.Find("NavBar");
            }

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var type = mb.GetType();
                string typeName = type.Name;

                if (!ContentControllerTypes.Contains(typeName)) continue;

                // Check IsOpen property
                var isOpenProp = type.GetProperty("IsOpen",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (isOpenProp != null && isOpenProp.PropertyType == typeof(bool))
                {
                    try
                    {
                        bool isOpen = (bool)isOpenProp.GetValue(mb);
                        if (isOpen)
                        {
                            // Also check IsReadyToShow if available
                            var isReadyProp = type.GetProperty("IsReadyToShow",
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance);

                            if (isReadyProp != null && isReadyProp.PropertyType == typeof(bool))
                            {
                                bool isReady = (bool)isReadyProp.GetValue(mb);
                                if (!isReady) continue; // Skip if not ready yet
                            }

                            // Store the GameObject for element filtering
                            _activeControllerGameObject = mb.gameObject;
                            _activeContentController = typeName;
                            return typeName;
                        }
                    }
                    catch { /* Ignore reflection errors */ }
                }
            }

            // Fallback: Check for rewards/claim overlay by object name pattern
            var rewardsObj = GameObject.Find("ContentController - Rewards_Desktop_16x9(Clone)");
            if (rewardsObj != null && rewardsObj.activeInHierarchy)
            {
                _activeControllerGameObject = rewardsObj;
                _activeContentController = "RewardsOverlay";
                return "RewardsOverlay";
            }

            _activeControllerGameObject = null;
            _activeContentController = null;
            return null;
        }

        /// <summary>
        /// Check if Settings menu is currently open and update the cached panel reference.
        /// </summary>
        /// <returns>True if Settings is open, false otherwise.</returns>
        public bool CheckSettingsMenuOpen()
        {
            foreach (var panelName in SettingsPanelNames)
            {
                var panel = GameObject.Find(panelName);
                if (panel != null && panel.activeInHierarchy)
                {
                    _settingsContentPanel = panel;
                    return true;
                }
            }
            _settingsContentPanel = null;
            return false;
        }

        /// <summary>
        /// Check if the Social/Friends panel is currently open.
        /// </summary>
        public bool IsSocialPanelOpen()
        {
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null) return false;

            // Check for visible social content (the friends list panel)
            // Widget name varies: FriendsWidget_Desktop_16x9(Clone) or FriendsWidget_V2(Clone)
            var mobileSafeArea = socialPanel.transform.Find("MobileSafeArea");
            if (mobileSafeArea != null)
            {
                foreach (Transform child in mobileSafeArea)
                {
                    if (child.name.StartsWith("FriendsWidget") && child.gameObject.activeInHierarchy)
                        return true;
                }
            }

            // Alternative: check for the top bar dismiss button which appears when panel is open
            var topBarDismiss = socialPanel.GetComponentsInChildren<UnityEngine.UI.Button>(false)
                .FirstOrDefault(b => b.name.Contains("TopBarDismiss"));
            if (topBarDismiss != null && topBarDismiss.gameObject.activeInHierarchy)
                return true;

            return false;
        }

        /// <summary>
        /// Check if the promotional carousel is visible on the home screen.
        /// </summary>
        /// <param name="hasCarouselElement">Optional flag indicating if any navigator element has carousel navigation.</param>
        public bool HasVisibleCarousel(bool hasCarouselElement = false)
        {
            if (hasCarouselElement)
                return true;

            foreach (var pattern in CarouselPatterns)
            {
                var obj = GameObject.Find(pattern);
                if (obj != null && obj.activeInHierarchy)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if Color Challenge content is visible.
        /// </summary>
        /// <param name="getActiveCustomButtons">Function to get active custom buttons for pattern matching.</param>
        /// <param name="getGameObjectPath">Function to get GameObject path for pattern matching.</param>
        public bool HasColorChallengeVisible(
            System.Func<IEnumerable<GameObject>> getActiveCustomButtons = null,
            System.Func<GameObject, string> getGameObjectPath = null)
        {
            // Check for CampaignGraph content controller being open
            if (_activeContentController == "CampaignGraphContentController")
                return true;

            // Also check for Color Challenge buttons directly if functions provided
            if (getActiveCustomButtons != null && getGameObjectPath != null)
            {
                return getActiveCustomButtons().Any(obj =>
                {
                    string objName = obj.name;
                    string path = getGameObjectPath(obj);
                    return ColorChallengePatterns.Any(pattern =>
                        objName.Contains(pattern) || path.Contains(pattern));
                });
            }

            return false;
        }

        /// <summary>
        /// Map content controller type name to user-friendly screen name.
        /// </summary>
        public string GetContentControllerDisplayName(string controllerTypeName)
        {
            return controllerTypeName switch
            {
                "HomePageContentController" => "Home",
                "DeckManagerController" => "Decks",
                "ProfileContentController" => "Profile",
                "ContentController_StoreCarousel" => "Store",
                "MasteryContentController" => "Mastery",
                "AchievementsContentController" => "Achievements",
                "LearnToPlayControllerV2" => "Learn to Play",
                "PackOpeningController" => "Pack Opening",
                "CampaignGraphContentController" => "Color Challenge",
                "WrapperDeckBuilder" => "Deck Builder",
                "ConstructedDeckSelectController" => "Deck Selection",
                "EventPageContentController" => "Event",
                "RewardsOverlay" => "Rewards",
                _ => controllerTypeName?.Replace("ContentController", "").Replace("Controller", "").Trim()
            };
        }

        /// <summary>
        /// Check if the given controller types list contains the specified type.
        /// </summary>
        public static bool IsContentControllerType(string typeName)
        {
            return ContentControllerTypes.Contains(typeName);
        }

        #endregion
    }
}
