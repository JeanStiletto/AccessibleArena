# HarmonyPanelDetector.cs - Code Index

## File-level Comment
Detector that uses Harmony patches to detect panel state changes.
Event-driven detection for panels that use Show/Hide methods or property setters.
Handles: PlayBlade, Settings, Blades, SocialUI, NavContentController

## Classes

### HarmonyPanelDetector (line 15)
```csharp
public class HarmonyPanelDetector
```

#### Properties
- public string DetectorId (line 17) - Returns "HarmonyDetector"

#### Fields
- private PanelStateManager _stateManager (line 19)
- private bool _initialized (line 20)
- private readonly Dictionary<object, GameObject> _controllerToGameObject (line 23)
  - Track controller instances to their GameObjects for proper panel tracking

#### Methods - Initialization
- public void Initialize(PanelStateManager stateManager) (line 25)
  - Initialize detector and subscribe to Harmony patch events
- public void Update() (line 42)
  - Event-driven detector - no polling needed, just cleanup stale references
- public void Reset() (line 49)
  - Reset controller tracking

#### Panel Ownership (Stage 5.3)
- public static readonly string[] OwnedPatterns (line 67)
  - OWNED PATTERNS - HarmonyDetector is the authoritative detector for these panels
  - Patterns: playblade, settings, socialui, friendswidget, eventblade, findmatchblade, deckselectblade, bladecontentview

#### Methods - Panel Handling
- public bool HandlesPanel(string panelName) (line 81)
  - Check if this detector handles a given panel
- private void OnHarmonyPanelStateChanged(object controller, bool isOpen, string typeName) (line 95)
  - Event handler for Harmony panel state changes
- private GameObject GetGameObjectForController(object controller) (line 186)
  - Get GameObject for a controller (with caching)
- private PanelType DeterminePanelType(string typeName) (line 224)
  - Determine panel type from type name
- private int ParsePlayBladeState(string stateName) (line 240)
  - Parse PlayBladeVisualStates: Hidden=0, Events=1, DirectChallenge=2, FriendChallenge=3
- private int ParseBladeContentViewState(string contentViewName) (line 264)
  - Maps BladeContentView names to PlayBlade states
- private void CleanupStaleReferences() (line 286)
  - Remove entries where GameObject has been destroyed
