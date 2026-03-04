# MenuPanelTracker.cs

## Summary
Tracks active menu panels and provides content controller detection. Note: Popup detection has been moved to UnifiedPanelDetector which uses alpha-based visibility tracking instead of cooldowns/timers.

## Classes

### MenuPanelTracker (line 15)
```
public class MenuPanelTracker
  // Configuration
  private static readonly string[] MenuControllerTypes (line 20)
  private static readonly string[] LoginPanelPatterns (line 28)

  // State
  private readonly HashSet<string> _activePanels (line 49)
  private GameObject _foregroundPanel (line 50)
  private readonly string _logPrefix (line 51)

  // Public Properties
  public GameObject ForegroundPanel { get; set; } (line 60)
  public HashSet<string> ActivePanels => _activePanels (line 69)

  // Constructor
  public MenuPanelTracker(IAnnouncementService announcer, string logPrefix = "PanelTracker") (line 80)

  // Public Methods
  public void Reset() (line 92)
  public List<(string name, GameObject obj)> GetActivePanelsWithObjects(MenuScreenDetector screenDetector) (line 104)
  private void DetectLoginPanels(List<(string name, GameObject obj)> activePanels) (line 173)
  public bool CheckIsOpen(MonoBehaviour mb, System.Type type) (line 202)
  public void AddActivePanel(string panelId) (line 295)
  public void RemoveActivePanel(string panelId) (line 303)
  public void RemovePanelsWhere(System.Predicate<string> predicate) (line 311)
  public bool ContainsPanel(string panelId) (line 319)

  // Static Utility Methods
  public static bool IsOverlayPanel(string panelName) (line 331)
  public static string CleanPopupName(string popupName) (line 342)
  public static bool IsChildOf(GameObject child, GameObject parent) (line 367)
```
