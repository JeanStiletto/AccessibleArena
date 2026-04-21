# HarmonyPanelDetector.cs
Path: src/Core/Services/PanelDetection/HarmonyPanelDetector.cs
Lines: 317

## Top-level comments
- Event-driven detector subscribed to PanelStatePatch.OnPanelStateChanged. Handles PlayBlade, Settings, Blades, SocialUI, NavContentController. Also maps blade states and content view types to PlayBladeState int values.

## public class HarmonyPanelDetector (line 16)
### Fields
- private PanelStateManager _stateManager (line 23)
- private bool _initialized (line 24)
- private readonly Dictionary<object, GameObject> _controllerToGameObject (line 27)
- public static readonly string[] OwnedPatterns (line 68) — Note: authoritative list; other detectors must exclude these patterns
### Properties
- public string DetectorId => "HarmonyDetector" (line 18)
### Methods
- public void Initialize(PanelStateManager stateManager) (line 26)
- public void Update() (line 44)
- public void Reset() (line 50)
- public bool HandlesPanel(string panelName) (line 82)
- private void OnHarmonyPanelStateChanged(object controller, bool isOpen, string typeName) (line 96) — Note: maps PlayBlade:/Blade:/EventBlade type names to PlayBladeState calls; swallows exceptions with Warning log
- private GameObject GetGameObjectForController(object controller) (line 201)
- private PanelType DeterminePanelType(string typeName) (line 239)
- private int ParsePlayBladeState(string stateName) (line 255)
- private int ParseBladeContentViewState(string contentViewName) (line 279)
- private void CleanupStaleReferences() (line 301)
