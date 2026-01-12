# Target Selection Implementation Plan

## Implementation Status (2026-01-10)

**Working Implementation - HotHighlight-based target validation**

### What Was Implemented
- [x] TargetNavigator service (`src/Core/Services/TargetNavigator.cs`) - ~340 lines, clean implementation
- [x] TargetInfo model (`src/Core/Models/TargetInfo.cs`)
- [x] Integration with DuelNavigator and ZoneNavigator
- [x] Land detection - skips targeting mode (via card type check)
- [x] **Submit button detection** - detects "Submit X" or "Cancel" button to enter targeting mode
- [x] **HotHighlight detection** - valid targets have active `HotHighlightBattlefield(Clone)` child

### How Target Validation Works
1. After card play, detect targeting mode via Submit/Cancel button presence
2. Scan battlefield and stack for cards
3. Check each card for active `HotHighlight` child object
4. Cards with `HotHighlightBattlefield(Clone)` active = valid targets
5. Announce target count and allow Tab navigation

### Key Discovery
The game uses `HotHighlight` child objects to visually indicate valid targets:
- **Valid target cards:** `Highlights - Default(Clone)` → `HotHighlightBattlefield(Clone)` (ACTIVE)
- **Non-target cards:** `Highlights - Default(Clone)` with no HotHighlight child
- **Spell on stack:** Has `_GlowColor` green glow (NOT the targeting indicator)

### Testing Completed
- [x] Knight's Pledge (Aura targeting creature you control) - correctly shows only your creatures

### Testing Required
- [ ] Shock (instant targeting any target) - should show creatures and players
- [ ] Counterspell (targeting spell on stack) - should show stack spells only
- [ ] Murder (targeting creature) - should show all creatures

### User Flow
1. Play card from hand (Enter on card in hand)
2. "Select a target. N valid targets" announced
3. Tab cycles through valid targets only
4. Enter selects current target
5. Escape cancels

---

## Overview

Enable blind players to select targets for spells and abilities using Tab navigation. When a card requires target selection, Tab cycles through valid targets while zone shortcuts and arrow keys remain available for battlefield exploration.

## Current Problem

Playing cards from hand works via `UIActivator.PlayCardViaTwoClick()` (`src/Core/Services/UIActivator.cs:211-258`):
1. Double-click card (select + pick up)
2. Click screen center (confirm play)

This fails for cards requiring targets (e.g., "Shock", "Murder") because clicking screen center doesn't select a valid target.

## Solution: Tab-Based Target Navigation

### Behavior During Targeting Mode

**Tab**
- Cycle through valid targets, announce each

**Enter**
- Select current target (simulate click)

**Zone shortcuts (C,B,G,X,S)**
- Still work for exploration

**Arrow keys**
- Still work for card navigation

**Escape**
- Cancel targeting

### User Flow Example

1. Player navigates to "Shock" in hand, presses Enter
2. Card is picked up (double-click sequence)
3. Game enters targeting mode
4. Mod announces: "Select a target. 3 valid targets."
5. Tab cycles: "Grizzly Bears, 2/2" → "Llanowar Elves, 1/1" → "Opponent"
6. Player can press B to hear full battlefield, then Tab to resume target cycling
7. Enter on desired target confirms selection
8. Spell goes on stack

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     UXEventQueue                             │
│                  (Harmony Patched)                           │
└──────────────────────┬──────────────────────────────────────┘
                       │ PlayerSelectingTargetsEventTranslator
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    DuelAnnouncer                             │
│              src/Core/Services/DuelAnnouncer.cs              │
│                                                              │
│  - Detects targeting mode via event type                     │
│  - Notifies TargetNavigator.EnterTargetMode()               │
│  - Announces "Select a target"                               │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                   TargetNavigator (NEW)                      │
│            src/Core/Services/TargetNavigator.cs              │
│                                                              │
│  Properties:                                                 │
│  - IsTargeting: bool                                         │
│  - ValidTargets: List<TargetInfo>                           │
│  - CurrentTargetIndex: int                                   │
│                                                              │
│  Methods:                                                    │
│  - EnterTargetMode() - called by DuelAnnouncer              │
│  - ExitTargetMode() - called on confirm/cancel              │
│  - DiscoverValidTargets() - finds highlighted cards          │
│  - NextTarget() - Tab forward                                │
│  - PreviousTarget() - Shift+Tab backward                    │
│  - SelectCurrentTarget() - Enter confirms                    │
│  - HandleInput() - processes Tab during targeting           │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┴──────────────┐
        │                             │
        ▼                             ▼
┌───────────────────┐    ┌────────────────────────┐
│   ZoneNavigator   │    │      UIActivator       │
│                   │    │                        │
│ - Zone shortcuts  │    │ - SimulatePointerClick │
│ - Arrow keys work │    │ - Click on target      │
│ - Checks          │    │                        │
│   IsTargeting     │    │                        │
└───────────────────┘    └────────────────────────┘
```

---

## Game Infrastructure (from analysis files)

### Targeting Event Detection

**Source:** `analysis_core_menus.txt:24162`
```
CLASS: Wotc.Mtga.DuelScene.UXEvents.PlayerSelectingTargetsEventTranslator
  Base: Object
  Public: True
```

This event fires when the game enters target selection mode. Add to DuelAnnouncer's event map.

### Target State Management

**Source:** `analysis_core_menus.txt:21632`
```
CLASS: Wotc.Mtga.DuelScene.SelectTargetsState
  Base: Object
  Public: True
```

### Highlight System for Valid Targets

**Source:** `analysis_core_menus.txt:20494`
```
CLASS: Wotc.Mtga.DuelScene.Interactions.SelectTargets.SelectTargetsHighlightsGenerator
  Base: Object
  Public: True
  Methods:
    Highlights GetHighlights()
```

Valid targets are visually highlighted. The `GetHighlights()` method returns which entities are valid.

### Entity View Interface

**Source:** `analysis_core.txt:379`
```
CLASS: IEntityView
  Base: None
  Public: True
  Properties:
    UInt32 InstanceId
```

Each card/entity has a unique `InstanceId` for identification.

### Click Validation

**Source:** `analysis_input.txt:21659`
```
CLASS: Wotc.Mtga.DuelScene.Interactions.IClickableWorkflow
  Methods:
    Boolean CanClick(IEntityView entity, SimpleInteractionType clickType)
    Void OnClick(IEntityView entity, SimpleInteractionType clickType)
```

The game validates clicks via `CanClick()` before processing with `OnClick()`.

### Workflow Variants

**Source:** `analysis_input.txt:21137`
```
CLASS: Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.WorkflowVariant
  Methods:
    Void Open()
    Void Close()
    Highlights GetHighlights()
    Buttons GetButtons()
```

All action workflows extend this base class and provide `GetHighlights()`.

---

## Implementation Details

### 1. TargetNavigator Service

```csharp
// src/Core/Services/TargetNavigator.cs

public class TargetNavigator
{
    private readonly IAnnouncementService _announcer;
    private readonly ZoneNavigator _zoneNavigator;

    private bool _isTargeting;
    private List<TargetInfo> _validTargets = new List<TargetInfo>();
    private int _currentIndex = -1;

    public bool IsTargeting => _isTargeting;

    public void EnterTargetMode()
    {
        _isTargeting = true;
        DiscoverValidTargets();

        if (_validTargets.Count > 0)
        {
            _currentIndex = 0;
            _announcer.Announce($"Select a target. {_validTargets.Count} valid targets.");
        }
        else
        {
            _announcer.Announce("No valid targets.");
        }
    }

    public void ExitTargetMode()
    {
        _isTargeting = false;
        _validTargets.Clear();
        _currentIndex = -1;
    }

    public bool HandleInput()
    {
        if (!_isTargeting) return false;

        // Tab - next target
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift)
                PreviousTarget();
            else
                NextTarget();
            return true;
        }

        // Enter - select current target
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SelectCurrentTarget();
            return true;
        }

        // Escape - cancel targeting
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTargeting();
            return true;
        }

        // Zone shortcuts still work - don't consume
        return false;
    }

    private void DiscoverValidTargets()
    {
        _validTargets.Clear();

        // Strategy 1: Find cards with highlight/glow components
        // Strategy 2: Check CanClick on battlefield cards
        // See "Target Discovery Strategies" below
    }

    private void NextTarget()
    {
        if (_validTargets.Count == 0) return;

        _currentIndex = (_currentIndex + 1) % _validTargets.Count;
        AnnounceCurrentTarget();
    }

    private void SelectCurrentTarget()
    {
        if (_currentIndex < 0 || _currentIndex >= _validTargets.Count) return;

        var target = _validTargets[_currentIndex];
        UIActivator.SimulatePointerClick(target.GameObject);

        _announcer.Announce($"Targeted {target.Name}");
        ExitTargetMode();
    }
}
```

### 2. Target Discovery Strategies

#### Strategy A: Highlight Component Detection

Cards with active targeting highlights have visual effects. Detect via:
- Material property changes (glow shader)
- Active child objects with highlight prefabs
- Component state changes

```csharp
private bool HasTargetHighlight(GameObject card)
{
    // Check for highlight components
    var renderers = card.GetComponentsInChildren<Renderer>();
    foreach (var renderer in renderers)
    {
        // Check material for glow/highlight properties
        if (renderer.material.HasProperty("_GlowColor") ||
            renderer.material.HasProperty("_HighlightIntensity"))
        {
            // Check if highlight is active
            if (renderer.material.GetFloat("_HighlightIntensity") > 0)
                return true;
        }
    }

    // Check for highlight child objects
    var highlightChild = card.transform.Find("Highlight");
    if (highlightChild != null && highlightChild.gameObject.activeInHierarchy)
        return true;

    return false;
}
```

#### Strategy B: Reflection on Game State

Access `SelectTargetsState` or workflow to get valid target IDs:

```csharp
private void DiscoverValidTargetsViaReflection()
{
    // Find SelectTargetsHighlightsGenerator instance
    var generator = FindObjectOfType<SelectTargetsHighlightsGenerator>();
    if (generator == null) return;

    // Call GetHighlights() via reflection
    var highlightsMethod = generator.GetType().GetMethod("GetHighlights");
    var highlights = highlightsMethod.Invoke(generator, null);

    // Parse highlights to get valid target IDs
    // ...
}
```

#### Strategy C: Compare Cards to Expected Behavior

For common targeting patterns:
- "Target creature" → All creatures on battlefield
- "Target creature you control" → Your creatures only
- "Target player" → Player avatars
- "Target creature or player" → Creatures + avatars

### 3. DuelAnnouncer Modifications

Add to event map in `BuildEventTypeMap()`:

```csharp
// Targeting events
{ "PlayerSelectingTargetsEventTranslator", DuelEventType.TargetSelection },
{ "PlayerSubmittedTargetsEventTranslator", DuelEventType.TargetConfirmed },
```

Add new event type and handler:

```csharp
case DuelEventType.TargetSelection:
    MTGAAccessibilityMod.Instance?.TargetNavigator?.EnterTargetMode();
    return null; // TargetNavigator announces
```

### 4. ZoneNavigator Modifications

Check targeting mode before handling Tab:

```csharp
public bool HandleInput()
{
    if (!_isActive) return false;

    // Let TargetNavigator handle Tab during targeting
    var targetNav = MTGAAccessibilityMod.Instance?.TargetNavigator;
    if (targetNav?.IsTargeting == true)
    {
        // Only handle zone shortcuts and arrows, not Tab
        if (Input.GetKeyDown(KeyCode.Tab))
            return false; // Let TargetNavigator handle
    }

    // ... rest of existing code
}
```

---

## Data Structures

### TargetInfo

```csharp
public class TargetInfo
{
    public GameObject GameObject { get; set; }
    public string Name { get; set; }           // Card name or "Opponent"/"You"
    public uint InstanceId { get; set; }       // From IEntityView
    public TargetType Type { get; set; }       // Creature, Player, Permanent, etc.
    public string Details { get; set; }        // Power/toughness, life total, etc.
}

public enum TargetType
{
    Creature,
    Player,
    Permanent,
    Spell,
    Planeswalker
}
```

---

## MTGA Built-in Keyboard Shortcuts Reference

These shortcuts are handled by the game and must not be overridden:

- Space - Pass priority
- Enter - Pass until response
- Shift+Enter - Pass turn
- Z - Undo
- Ctrl - Full control (temporary)
- Ctrl+Shift - Full control (permanent)
- Q+Q - Tap all lands
- Tab - Chat navigation (in lobby)

**Sources:**
- [MTG Arena Keyboard Shortcuts - MTGA Assistant](https://mtgaassistant.net/Article/All-MTG-Arena-Keyboard-Shortcuts)
- [Draftsim MTGA Shortcuts](https://draftsim.com/mtg-arena-keyboard-shortcuts/)

---

## File References

### Existing Mod Files

- `src/Core/Services/UIActivator.cs` - Click simulation, PlayCardViaTwoClick
- `src/Core/Services/ZoneNavigator.cs` - Zone shortcuts, card navigation
- `src/Core/Services/DuelAnnouncer.cs` - UX event handling, game announcements
- `src/Core/Services/InputManager.cs` - Custom keyboard shortcuts
- `src/Core/Services/CardDetector.cs` - Card identification
- `src/Patches/UXEventQueuePatch.cs` - Harmony patch for game events

### Game Analysis Files

- `analysis_core.txt` - Core interfaces (IEntityView, INextActionHandler)
- `analysis_core_menus.txt` - UI classes, SelectTargets namespace
- `analysis_input.txt` - Input handling, workflow classes
- `docs/game_analysis_2026-01-08_13-58.md` - Interface summary

### Documentation

- `docs/BEST_PRACTICES.md` - Mod patterns, utility usage
- `docs/CARD_PLAY_IMPLEMENTATION.md` - Double-click approach
- `docs/MOD_STRUCTURE.md` - Project layout

---

## Testing Checklist

### Card Playing (Tested & Working)
- [x] Lands play correctly (skip targeting mode)
- [x] Creatures play correctly (skip targeting mode via stack detection)
- [x] Non-targeted spells play correctly

### Target Selection (Working - HotHighlight Detection)
- [x] Targeting mode detected when playing targeted spell
- [x] Tab cycles through valid targets
- [x] Shift+Tab cycles backward
- [x] Enter selects current target
- [x] Escape cancels targeting
- [x] Zone shortcuts (C,B,G,X,S) work during targeting
- [x] Arrow keys work during targeting
- [x] Target count announced on mode entry
- [x] Current target announced on Tab
- [x] "No valid targets" announced when applicable
- [x] Works for creature targets
- [ ] Works for player targets (needs testing)
- [ ] Works for "any target" spells (needs testing)
- [ ] Works for triggered abilities requiring targets (needs testing)

### Workaround (Manual Targeting)
- [x] Can navigate to battlefield with B key during target selection
- [x] Can find target with arrow keys
- [x] Can select target with Enter key

---

## Implementation Order

1. Add `PlayerSelectingTargetsEventTranslator` to DuelAnnouncer event map
2. Create `TargetNavigator` service with basic structure
3. Implement target discovery (start with battlefield creatures)
4. Add Tab handling in TargetNavigator
5. Modify ZoneNavigator to defer Tab during targeting
6. Add Enter handling for target selection
7. Test with simple targeted spell (e.g., Shock)
8. Expand to player targets
9. Expand to stack targets
10. Handle edge cases (no valid targets, fizzled spells)
