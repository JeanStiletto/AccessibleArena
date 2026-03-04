# MenuScreenDetector.cs

## Summary
Detects active content controllers and screens in the MTGA menu system. Provides screen name mapping and visibility checks for various UI elements.

## Classes

### MenuScreenDetector (line 13)
```
public class MenuScreenDetector
  // Configuration
  private static readonly string[] ContentControllerTypes (line 18)
  private static readonly string[] SettingsPanelNames (line 37)
  private static readonly string[] CarouselPatterns (line 46)
  private static readonly string[] ColorChallengePatterns (line 52)

  // State
  private string _activeContentController (line 62)
  private GameObject _activeControllerGameObject (line 63)
  private GameObject _navBarGameObject (line 64)
  private GameObject _settingsContentPanel (line 65)

  // Public Properties
  public string ActiveContentController => _activeContentController (line 74)
  public GameObject ActiveControllerGameObject => _activeControllerGameObject (line 79)
  public GameObject NavBarGameObject => _navBarGameObject (line 84)
  public GameObject SettingsContentPanel => _settingsContentPanel (line 89)

  // Public Methods
  public void Reset() (line 98)
  public string DetectActiveContentController() (line 111)
  public bool CheckSettingsMenuOpen() (line 201)
  public bool IsSocialPanelOpen() (line 220)
  public bool IsMailboxOpen() (line 249)
  public bool HasVisibleCarousel(bool hasCarouselElement = false) (line 268)
  public bool HasColorChallengeVisible(Func<IEnumerable<GameObject>> getActiveCustomButtons = null, Func<GameObject, string> getGameObjectPath = null) (line 287)
  public string GetContentControllerDisplayName(string controllerTypeName) (line 314)
  private bool IsRewardsOverlayWithContent() (line 345)
  public static bool IsContentControllerType(string typeName) (line 382)
  public bool IsNPERewardsScreenActive() (line 392)
```
