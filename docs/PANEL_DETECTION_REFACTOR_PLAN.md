# Panel Detection System Refactor Plan

## Implementation Status

**Phase 1: Foundation** - COMPLETED
- Created `PanelType.cs`, `PanelInfo.cs`, `PanelRegistry.cs`, `PanelStateManager.cs`
- PanelStateManager is single source of truth for panel state
- All panel changes report to PanelStateManager

**Phase 2: Unified Rescan Triggers** - COMPLETED
- Added `OnAnyPanelOpened` event for all panel opens (including non-filtering panels like HomePage)
- Added `OnPanelChanged` event for filtering panel changes
- GeneralMenuNavigator subscribes to both events
- Removed scattered TriggerRescan calls from panel detection code
- Added `ForceRescan()` to IScreenNavigator interface
- NavigatorManager coordinates scene-change handling centrally

**Phase 3: Refactor Detectors as Plugins** - COMPLETED
- Created `IPanelDetector.cs` interface with Initialize/Update/Reset/HandlesPanel
- Created `HarmonyPanelDetector.cs` - wraps PanelStatePatch, handles PlayBlade/Settings/Blades/SocialUI
- Created `ReflectionPanelDetector.cs` - polls IsOpen properties, handles Login panels/PopupBase
- Created `AlphaPanelDetector.cs` - uses CanvasGroup alpha, handles Popup/Dialog/Modal
- Created `PanelDetectorManager.cs` - coordinates all detectors, calls Update on each
- Each detector reports to PanelStateManager, never triggers rescans directly
- AccessibleArenaMod initializes PanelDetectorManager after PanelStateManager

**Phase 4: Eliminate Duplicate Detection** - COMPLETED
- Updated all detectors to use `PanelRegistry.GetDetectionMethod()` for ownership checks
- Removed scattered panel patterns from detectors (now centralized in PanelRegistry)
- Removed `ContentPanel:` prefix naming from GeneralMenuNavigator
- Removed deduplication hacks ("skip if already tracked by Harmony")
- Simplified HandleContentPanelChange and HandleBladeChange (removed dual-write reporting)
- Each panel is now detected by exactly one detector per PanelRegistry assignment

**Phase 5: Simplify GeneralMenuNavigator** - Not started
- Remove CheckForPanelChanges() - let detectors handle all detection
- Remove OnPanelStateChangedExternal() - HarmonyPanelDetector handles this
- Navigator responds only to PanelStateManager events
- Remove direct _foregroundPanel management (use PanelStateManager.GetFilterPanel())

---

## Executive Summary

The current menu/panel detection system uses 3 separate mechanisms built incrementally. They don't coordinate well, causing:
- Duplicate detections triggering multiple rescans
- Inconsistent naming conventions
- Special cases scattered throughout the code
- Complex state management spread across multiple classes

This plan proposes a unified architecture with clear responsibilities and single sources of truth.

---

## Current Architecture Analysis

### The 3 Systems

| System | File | Trigger | Handles | Issues |
|--------|------|---------|---------|--------|
| **Harmony Patches** | PanelStatePatch.cs | Event-driven (immediate) | PlayBlade, Settings, Blades, SocialUI | Fires OnPanelStateChanged event |
| **Reflection Polling** | MenuPanelTracker.cs + CheckForPanelChanges() | Every frame | NavContentController, SettingsMenu, PopupBase, Login | Polls IsOpen properties |
| **Alpha Detection** | UnifiedPanelDetector.cs | Every 10 frames | SystemMessageView, Dialog, Modal, Popups | Watches CanvasGroup alpha |

### Why 3 Systems Exist

Each system handles panel types the others cannot:

1. **Harmony** - Required for PlayBlade (uses slide animation, alpha stays 1.0)
2. **Reflection** - Required for controllers with IsOpen but no Harmony-patchable setters
3. **Alpha** - Required for popups without controllers (pure CanvasGroup fade)

### Current Problems

1. **19 different TriggerRescan() call sites** scattered through the code
2. **_foregroundPanel assigned from ~15 different locations**
3. **Duplicate tracking**: Same panel detected by Harmony (ContentPanel:X) AND reflection (X:...)
4. **Special case exclusions everywhere**: SocialUI ignored here, HomePage ignored there
5. **Naming inconsistencies**: ContentPanel:X, X:..., just X, cleaned names
6. **No single source of truth** for "what panel is currently active"

### Data Flow (Current - Messy)

```
PanelStatePatch.OnPanelStateChanged
    └── NavigatorManager.OnPanelStateChanged
        └── GeneralMenuNavigator.OnPanelStateChangedExternal
            ├── Sets _foregroundPanel (sometimes)
            ├── Sets _playBladeActive (sometimes)
            └── TriggerRescan() (sometimes)

GeneralMenuNavigator.Update() [every frame]
    ├── UnifiedPanelDetector.CheckForChanges()
    │   └── May set _foregroundPanel for popups
    │   └── May TriggerRescan()
    └── CheckForPanelChanges()
        └── MenuPanelTracker.GetActivePanelsWithObjects()
        └── May set _foregroundPanel
        └── May TriggerRescan()
```

---

## Proposed Architecture

### Core Principle: Single Source of Truth

One central service (`PanelStateManager`) owns ALL panel state. Other components subscribe to changes.

### New Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PanelStateManager                            │
│   (Single source of truth for panel state)                          │
├─────────────────────────────────────────────────────────────────────┤
│ State:                                                               │
│   - ActivePanel: PanelInfo                                          │
│   - PanelStack: Stack<PanelInfo>  (for nested panels)               │
│   - PlayBladeState: enum                                            │
│                                                                      │
│ Events:                                                              │
│   - OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel)          │
│   - OnPlayBladeChanged(PlayBladeState state)                        │
│                                                                      │
│ Methods:                                                             │
│   - SetActivePanel(PanelInfo)                                       │
│   - ClearActivePanel()                                              │
│   - GetFilterPanel() → GameObject                                   │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
          ┌───────────────────┼───────────────────┐
          │                   │                   │
┌─────────┴─────────┐ ┌──────┴──────┐ ┌─────────┴─────────┐
│ HarmonyDetector   │ │ Reflection  │ │ AlphaDetector     │
│ (Event-driven)    │ │ Detector    │ │ (Polling)         │
├───────────────────┤ ├─────────────┤ ├───────────────────┤
│ Handles:          │ │ Handles:    │ │ Handles:          │
│ - PlayBlade       │ │ - Login     │ │ - SystemMessage   │
│ - Settings        │ │   panels    │ │ - Dialogs         │
│ - Blades          │ │ - Popup     │ │ - Modals          │
│ - SocialUI        │ │   Base      │ │                   │
│                   │ │             │ │                   │
│ Calls:            │ │ Calls:      │ │ Calls:            │
│ PanelStateManager │ │ PanelState  │ │ PanelStateManager │
│ .SetActivePanel() │ │ Manager     │ │ .SetActivePanel() │
└───────────────────┘ └─────────────┘ └───────────────────┘
```

### PanelInfo Data Structure

```csharp
public class PanelInfo
{
    public string Name { get; }           // Canonical name (e.g., "Settings", "PlayBlade")
    public PanelType Type { get; }        // Enum: Settings, Blade, Popup, Login, Social
    public GameObject GameObject { get; } // The actual panel object
    public int Priority { get; }          // Higher = more foreground
    public bool FiltersNavigation { get; } // Should filter background elements?

    public string CleanDisplayName { get; } // User-friendly for announcements
}

public enum PanelType
{
    None,
    Login,           // Login scene panels
    Settings,        // Settings menu
    Blade,           // PlayBlade and sub-blades
    Social,          // Friends panel
    Popup,           // Alpha-based popups (SystemMessageView, dialogs)
    ContentPanel     // NavContentController descendants (home, profile, etc.)
}
```

### Panel Registry

Instead of scattered special cases, centralize panel knowledge:

```csharp
public static class PanelRegistry
{
    // Which system handles which panel
    public static PanelDetectionMethod GetDetectionMethod(string panelName)
    {
        // PlayBlade MUST use Harmony (slide animation)
        if (panelName.Contains("PlayBlade")) return PanelDetectionMethod.Harmony;
        if (panelName.Contains("Settings")) return PanelDetectionMethod.Harmony;
        if (panelName.Contains("Social")) return PanelDetectionMethod.Harmony;
        if (panelName.Contains("Blade")) return PanelDetectionMethod.Harmony;

        // Popups use Alpha
        if (panelName.Contains("SystemMessage")) return PanelDetectionMethod.Alpha;
        if (panelName.Contains("Dialog")) return PanelDetectionMethod.Alpha;
        if (panelName.Contains("Modal")) return PanelDetectionMethod.Alpha;

        // Everything else uses Reflection
        return PanelDetectionMethod.Reflection;
    }

    // Should this panel filter navigation elements?
    public static bool FiltersNavigation(string panelName)
    {
        // HomePage is always present, never filters
        if (panelName.Contains("HomePage")) return false;

        // These filter background elements
        if (panelName.Contains("Settings")) return true;
        if (panelName.Contains("Social")) return true;
        if (panelName.Contains("Blade")) return true;
        if (panelName.Contains("Popup")) return true;
        if (panelName.Contains("Modal")) return true;

        return false;
    }

    // Priority for stacking (higher = more foreground)
    public static int GetPriority(PanelType type)
    {
        return type switch
        {
            PanelType.Popup => 1000,      // Popups always on top
            PanelType.Settings => 500,    // Settings overlays everything
            PanelType.Social => 400,      // Social panel
            PanelType.Blade => 300,       // Play blade
            PanelType.Login => 200,       // Login panels
            PanelType.ContentPanel => 100,
            _ => 0
        };
    }
}
```

---

## Implementation Phases

### Phase 1: Create PanelStateManager (Foundation)

**Goal**: Single source of truth for panel state, without changing detection logic yet.

**Tasks**:
1. Create `PanelStateManager.cs` with:
   - `ActivePanel` property
   - `OnPanelChanged` event
   - `SetActivePanel()` / `ClearActivePanel()` methods
   - `GetFilterPanel()` method (returns GameObject for element filtering)

2. Create `PanelInfo.cs` and `PanelType.cs` enums

3. Create `PanelRegistry.cs` with centralized panel knowledge

4. Wire up GeneralMenuNavigator to use PanelStateManager:
   - Replace all `_foregroundPanel = X` with `PanelStateManager.SetActivePanel()`
   - Replace all `_foregroundPanel` reads with `PanelStateManager.GetFilterPanel()`

**Validation**: System works exactly as before, but state is centralized.

### Phase 2: Unify Rescan Triggers

**Goal**: Single rescan trigger point responding to panel changes.

**Tasks**:
1. Subscribe GeneralMenuNavigator to `PanelStateManager.OnPanelChanged`

2. Remove all scattered `TriggerRescan()` calls from:
   - OnPanelStateChangedExternal
   - CheckForPanelChanges
   - HandleBladeChange
   - Popup detection

3. Single rescan trigger in OnPanelChanged handler

4. Keep debounce logic in one place

**Validation**: Rescans happen only when panel state actually changes, not multiple times per change.

### Phase 3: Refactor Detectors as Plugins

**Goal**: Clean separation between detection and state management.

**Tasks**:
1. Create `IPanelDetector` interface:
   ```csharp
   interface IPanelDetector
   {
       void Initialize(PanelStateManager stateManager);
       void Update(); // Called every frame
       bool HandlesPanel(string panelName);
   }
   ```

2. Refactor existing detectors:
   - `HarmonyPanelDetector` - wraps PanelStatePatch, calls SetActivePanel
   - `ReflectionPanelDetector` - extracts from MenuPanelTracker + CheckForPanelChanges
   - `AlphaPanelDetector` - extracts from UnifiedPanelDetector

3. Each detector ONLY reports to PanelStateManager, never triggers rescans directly

4. PanelStateManager coordinates: if multiple detectors report same panel, only one state change fires

**Validation**: Clean separation, each detector has single responsibility.

### Phase 4: Eliminate Duplicate Detection

**Goal**: Each panel detected by exactly one system.

**Tasks**:
1. Use PanelRegistry to assign panels to detectors

2. When detector sees a panel it doesn't own, ignore it

3. Remove deduplication hacks from CheckForPanelChanges:
   - No more "skip if already tracked by Harmony"
   - No more ContentPanel: prefixes vs X:... naming

4. Consistent naming in PanelInfo

**Validation**: No duplicate OnPanelChanged events for same panel.

### Phase 5: Simplify GeneralMenuNavigator

**Goal**: Navigator only handles navigation, not detection.

**Tasks**:
1. Remove from GeneralMenuNavigator:
   - `CheckForPanelChanges()` method (moved to ReflectionPanelDetector)
   - `OnPanelStateChangedExternal()` (moved to HarmonyPanelDetector)
   - Direct _foregroundPanel management
   - All special case panel handling

2. GeneralMenuNavigator becomes:
   - Subscribes to PanelStateManager.OnPanelChanged
   - Uses PanelStateManager.GetFilterPanel() for element filtering
   - Handles only navigation logic

**Validation**: Clear separation - detection in detectors, state in manager, navigation in navigator.

---

## File Changes Summary

### New Files
- `src/Core/Services/PanelDetection/PanelStateManager.cs`
- `src/Core/Services/PanelDetection/PanelInfo.cs`
- `src/Core/Services/PanelDetection/PanelType.cs`
- `src/Core/Services/PanelDetection/PanelRegistry.cs`
- `src/Core/Services/PanelDetection/IPanelDetector.cs`
- `src/Core/Services/PanelDetection/HarmonyPanelDetector.cs`
- `src/Core/Services/PanelDetection/ReflectionPanelDetector.cs`
- `src/Core/Services/PanelDetection/AlphaPanelDetector.cs`

### Modified Files
- `src/Core/Services/GeneralMenuNavigator.cs` - Major simplification
- `src/Core/Services/NavigatorManager.cs` - Route to PanelStateManager instead
- `src/Patches/PanelStatePatch.cs` - Fire to HarmonyPanelDetector

### Deprecated/Removed Files
- `src/Core/Services/UnifiedPanelDetector.cs` → Replaced by AlphaPanelDetector
- `src/Core/Services/MenuPanelTracker.cs` → Absorbed into ReflectionPanelDetector

---

## Risk Mitigation

### High Risk: PlayBlade Detection
PlayBlade MUST use Harmony (slide animation, no alpha change). Ensure HarmonyPanelDetector handles this correctly.

**Mitigation**: Keep PanelStatePatch.cs mostly unchanged, just route events to new detector.

### Medium Risk: Timing Issues
Current system has debounce and cooldown logic. New system must preserve this.

**Mitigation**: Keep debounce in PanelStateManager, test with rapid panel changes.

### Medium Risk: Edge Cases
Current code has many special cases (SocialUI ignored, HomePage never filters). These must be preserved.

**Mitigation**: PanelRegistry centralizes all special case knowledge, nothing scattered.

### Low Risk: Regression
System is complex, changes could break working features.

**Mitigation**: Phase-by-phase implementation with validation at each step.

---

## Testing Checklist

After each phase, verify:

- [ ] Home screen navigation works
- [ ] Settings menu opens/closes, filters background
- [ ] Play blade slides in, shows deck selection
- [ ] Friends panel (F4) opens/closes
- [ ] Popups (confirmation dialogs) detected and navigable
- [ ] Login screen navigation works
- [ ] No double announcements
- [ ] Rescan happens once per panel change (check logs)
- [ ] Element filtering correct (background hidden when overlay active)

---

## Success Criteria

1. **Single source of truth**: PanelStateManager owns all panel state
2. **Single rescan trigger**: OnPanelChanged → rescan, nothing else triggers rescans
3. **Clear ownership**: Each panel type handled by exactly one detector
4. **Consistent naming**: PanelInfo.Name is canonical, no prefixes
5. **No special cases scattered**: All panel knowledge in PanelRegistry
6. **Maintainable**: Adding new panel type = add to registry, implement in appropriate detector
7. **Testable**: Each component has single responsibility

---

## Questions to Resolve

1. **Panel stacking**: Should we support true panel stack (popup over settings over blade)?
   - Current: Only tracks single _foregroundPanel
   - Proposed: Stack with priorities, highest filters navigation

2. **Scene transitions**: How does panel state reset on scene change?
   - Current: Various Reset() calls scattered
   - Proposed: PanelStateManager.ResetForScene() called by NavigatorManager

3. **Settings subpanels**: Settings has multiple content panels (Account, Graphics, etc.)
   - Current: Special SettingsContentPanel tracking
   - Proposed: PanelInfo can have parent reference for submenu tracking

---

## Estimated Effort

| Phase | Complexity | Estimated Time |
|-------|------------|----------------|
| Phase 1: Foundation | Medium | Core implementation |
| Phase 2: Unify Rescans | Low | Mostly deletion |
| Phase 3: Refactor Detectors | High | Most code movement |
| Phase 4: Eliminate Duplicates | Medium | Logic changes |
| Phase 5: Simplify Navigator | Medium | Cleanup |

Total: Significant refactor, but each phase is independently testable.
