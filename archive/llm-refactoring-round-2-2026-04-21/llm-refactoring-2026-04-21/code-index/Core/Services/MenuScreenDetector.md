# MenuScreenDetector.cs
Path: src/Core/Services/MenuScreenDetector.cs
Lines: 469

## Top-level comments
- Detects active content controllers and screens in the MTGA menu system. Provides screen name mapping and visibility checks for various UI elements (settings, social, mailbox, carousel, color challenge).

## public class MenuScreenDetector (line 15)

### Fields
- private static readonly string[] ContentControllerTypes (line 20)
- private static readonly string[] SettingsPanelNames (line 41)
- private static readonly string[] CarouselPatterns (line 50)
- private static readonly string[] ColorChallengePatterns (line 56)
- private string _activeContentController (line 65)
- private GameObject _activeControllerGameObject (line 66)
- private GameObject _navBarGameObject (line 67)
- private GameObject _settingsContentPanel (line 68)
- private float _cachedControllerTime = -1f (line 71)
- private const float ControllerCacheExpiry = 0.5f (line 72)

### Properties
- public string ActiveContentController => _activeContentController (line 81)
- public GameObject ActiveControllerGameObject => _activeControllerGameObject (line 86)
- public GameObject NavBarGameObject => _navBarGameObject (line 91)
- public GameObject SettingsContentPanel => _settingsContentPanel (line 96)

### Methods
- public void Reset() (line 105)
- public string DetectActiveContentController() (line 121) — Cached for 0.5s; invalidates when cached GameObject is destroyed or inactive
- private string DetectActiveContentControllerUncached() (line 139)
- public bool CheckSettingsMenuOpen() (line 216)
- public bool IsSocialPanelOpen() (line 235)
- public bool IsChatWindowOpen() (line 274)
- public bool IsMailboxOpen() (line 298)
- public bool HasVisibleCarousel(bool hasCarouselElement = false) (line 317)
- public bool HasColorChallengeVisible(Func<IEnumerable<GameObject>> getActiveCustomButtons = null, Func<GameObject, string> getGameObjectPath = null) (line 337)
- public string GetContentControllerDisplayName(string controllerTypeName) (line 363)
- private bool IsRewardsOverlayWithContent() (line 394)
- public static bool IsContentControllerType(string typeName) (line 431)
- public bool IsNPERewardsScreenActive() (line 441) — Returns true only when ActiveContainer is visible (actual card unlock, not deck preview)
