# PanelStateManager.cs
Path: src/Core/Services/PanelDetection/PanelStateManager.cs
Lines: 499

## Top-level comments
- Singleton: single source of truth for all panel state. Three detectors (Harmony, Reflection, Alpha) report changes here. Maintains a priority-sorted stack of PanelInfo; fires OnPanelChanged and OnAnyPanelOpened events. Tracks PlayBlade state separately. Guards against transient scene-load popups via IsSceneLoading flag.

## public class PanelStateManager (line 15)
### Fields
- private static PanelStateManager _instance (line 18)
- private HarmonyPanelDetector _harmonyDetector (line 47)
- private ReflectionPanelDetector _reflectionDetector (line 48)
- private AlphaPanelDetector _alphaDetector (line 49)
- private readonly List<PanelInfo> _panelStack (line 64)
- private float _lastChangeTime (line 109)
- private const float DebounceSeconds = 0.1f (line 110)
- private readonly HashSet<string> _announcedPanels (line 115)
### Properties
- public static PanelStateManager Instance => _instance (line 19)
- public event Action<PanelInfo, PanelInfo> OnPanelChanged (line 27)
- public event Action<PanelInfo> OnAnyPanelOpened (line 33)
- public event Action<int> OnPlayBladeStateChanged (line 38)
- public PanelInfo ActivePanel { get; private set; } (line 58)
- public int PlayBladeState { get; private set; } (line 70)
- public bool IsPlayBladeActive { get; } (line 78) — Note: primary check is PlayBladeState != 0; fallback scans for Btn_BladeIsOpen, excluding CampaignGraphPage parents
- public bool IsSceneLoading { get; private set; } (line 123)
- public bool IsSettingsMenuOpen => _panelStack.Exists(p => p.Name == "SettingsMenu" && p.IsValid) (line 415)
### Methods
- public void ClearSceneLoadingGate() (line 129)
- public PanelStateManager() (line 141)
- public void Initialize() (line 149)
- public void Update() (line 167)
- public bool ReportPanelOpened(PanelInfo panel) (line 188) — Note: ignores decorative panels, duplicates, and debounced rapid-fire events; first non-Popup panel clears IsSceneLoading
- public bool ReportPanelClosed(GameObject gameObject) (line 257)
- public bool ReportPanelClosedByName(string panelName) (line 285)
- public void SetPlayBladeState(int state) (line 303)
- private void AddToStack(PanelInfo panel) (line 326)
- private void UpdateActivePanel() (line 333) — Note: auto-resets stale PlayBladeState if blade panel GameObjects are destroyed without Hide firing
- public GameObject GetFilterPanel() (line 382)
- public bool IsPanelTypeActive(PanelType type) (line 397)
- public bool IsPanelActive(string panelName) (line 406)
- public IReadOnlyList<PanelInfo> GetPanelStack() (line 420)
- public void Reset() (line 432) — Note: sets IsSceneLoading = true
- public void SoftReset() (line 458)
- public void ValidatePanels() (line 471) — Note: resets AlphaDetector tracking for removed alpha-owned panels
