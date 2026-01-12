# Card Play Implementation

Documentation of approaches tried for implementing keyboard-based card playing from hand to battlefield.

## Goal
Enable blind players to play cards from hand using keyboard (Enter key) instead of mouse drag-and-drop.

## Solution (Working)

### Double-Click + Center Click Approach

After extensive testing, the working approach mimics what sighted players do with a mouse:

1. **Click card** (selects it internally)
2. **Wait 0.2s** (game processes selection)
3. **Click card again** (picks it up, triggers focus change)
4. **Wait 0.5s** (game enters "held" state, UI updates)
5. **Click screen center** (confirms play) - **via Unity events**

### Current Implementation (Unity Events - January 2026)

The Unity events approach is currently active and working. After a PC restart, this simpler approach works reliably.

```csharp
private static IEnumerator PlayCardCoroutine(GameObject card, Action<bool, string> callback)
{
    // Step 1: First click (select)
    SimulatePointerClick(card);
    yield return new WaitForSeconds(0.2f);

    // Step 2: Second click (pick up)
    SimulatePointerClick(card);
    yield return new WaitForSeconds(0.5f);

    // Step 3: Click screen center via Unity events (confirm)
    Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
    SimulateClickAtPosition(center);

    callback?.Invoke(true, "Card played");
}
```

### Known Potential Issues

The Unity events approach may fail in certain conditions:
- **Overlapping overlays** - Other UI elements intercepting the raycast at screen center
- **Mouse positioning** - External factors affecting Input.mousePosition
- **Multi-monitor setups** - Coordinate system differences

If card playing stops working, check these factors first. A PC restart resolved the issue previously.

---

### Windows API Fallback (Proven Working - Keep for Reference)

> **DO NOT DELETE THIS APPROACH** - It is a proven working fallback!
>
> This Windows API approach was implemented when Unity events stopped working. It directly controls the mouse cursor, bypassing Unity's event system entirely. The approach works reliably but has drawbacks:
> - Physically moves the user's mouse cursor
> - May cause issues if the game window is not in focus
> - May cause issues with multi-monitor setups
> - Could break if Windows security policies change
>
> The WinAPI code is preserved (commented out) in UIActivator.cs for reactivation if needed.

```csharp
// In UIActivator.cs - Windows API declarations
internal static class WinAPI
{
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
}

private static IEnumerator PlayCardCoroutine(GameObject card, Action<bool, string> callback)
{
    // Step 1: First click (select)
    SimulatePointerClick(card);
    yield return new WaitForSeconds(0.2f);

    // Step 2: Second click (pick up)
    SimulatePointerClick(card);
    yield return new WaitForSeconds(0.5f);

    // Step 3: Move REAL mouse to screen center and click (Windows API)
    int centerX = Screen.width / 2;
    int centerY = Screen.height / 2;
    WinAPI.SetCursorPos(centerX, centerY);
    yield return new WaitForSeconds(0.05f);

    WinAPI.mouse_event(WinAPI.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
    yield return new WaitForSeconds(0.05f);
    WinAPI.mouse_event(WinAPI.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

    callback?.Invoke(true, "Card played");
}
```

### Why Windows API May Be Needed (Reference)

When Unity events approach fails, debug logging may reveal:
- The game checks `Input.mousePosition` (actual hardware cursor position)
- Unity's `PointerEventData.position` may be ignored by the game's card drop system
- When the real mouse cursor is away from screen center, the card drop can fail
- The game's `CardInput` component uses drag handlers that track real mouse position

**Example log when Unity events fail:**
```
Actual mouse position: (2044.00, 148.00, 0.00)  <- Far from center
Screen center: (1440.00, 900.00)
Raycast found 0 objects at screen center
No raycast hit, sending global click  <- Game ignores this
```

**Example log when WinAPI works:**
```
Original cursor pos: (1440, 900)
Moving cursor to screen center: (1440, 900)
Cursor now at: (1440, 900)
Sending mouse click...  <- Real click at real position
Mouse click sent
=== CARD PLAY COMPLETE ===
```

---

### Investigation History (January 2026)

1. **Initial state**: Unity events approach working
2. **Problem appeared**: Card playing stopped working (unknown trigger)
3. **WinAPI solution**: Implemented Windows API mouse control - worked reliably
4. **PC restart test**: After restart, Unity events approach works again
5. **Conclusion**: External factors (overlays, mouse position, multi-monitor) likely caused the issue, not code changes

**Current status**: Unity events active, WinAPI preserved as fallback in UIActivator.cs

### Key Insights

1. **Two clicks on the card are required** - First click selects, second click "picks up"
2. **Focus change is the indicator** - When the card is truly "held", Unity's focus changes to the card
3. **Game uses real mouse position** - `Input.mousePosition` not `PointerEventData.position`
4. **Timing matters** - 0.2s between clicks, 0.5s before center click

---

## Failed Approaches (Historical Reference)

### Approach 1: Simple Click
- Single/double click on card
- Result: Card selects but doesn't play
- Problem: Game uses drag-and-drop workflow

### Approach 2: Click Card + Click Battlefield
- Click card, then click battlefield GameObject
- Result: Battlefield has no IPointerClickHandler
- Problem: Wrong click target

### Approach 3: Unity Drag Event Simulation
- Full BeginDrag → Drag → EndDrag → Drop sequence
- Result: Events fire, sounds play, card doesn't move
- Problem: Game's workflow system not triggered

### Approach 4: Screen Coordinate Drag
- Convert 3D positions to screen coordinates
- Result: Invalid coordinates (negative Y for hand cards)
- Problem: Hand cards are positioned off-screen in 3D space

### Approach 5: Direct Workflow Invocation
- Call IDraggableWorkflow methods via reflection
- Result: WorkflowController not findable
- Problem: Not a MonoBehaviour, pure C# object

### Approach 6: Hover + Click
- SimulatePointerEnter (hover) then click
- Result: Same as click-only, no improvement
- Problem: Hover alone doesn't prepare the card state

---

## Game Architecture (Reference)

### Card Structure
- `CDC #XX` - Card Display Controller objects in duel
- `CardInput` - Handles IBeginDragHandler, IDragHandler, IEndDragHandler
- `DuelScene_CDC` - Implements IEntityView

### Sighted Player Workflow
1. Mouse hovers over card (highlights it)
2. Left click (picks up card, card follows cursor)
3. Move mouse to battlefield/center
4. Left click (drops/plays card)

### Why Double-Click Works
- First click simulates the "highlight" that hover provides
- Second click simulates the "pick up" action
- Center click simulates the "drop" confirmation
- Game's internal state machine progresses correctly with this sequence

---

## Files

- `src/Core/Services/UIActivator.cs` - PlayCardViaTwoClick, SimulatePointerClick
- `src/Core/Services/ZoneNavigator.cs` - ActivateCurrentCard (delegates to UIActivator)

---

## Target Selection (Working)

### Current Status

Target selection via Tab navigation is **fully functional**. The TargetNavigator service uses HotHighlight detection to identify valid targets.

### What Works

| Card Type | Behavior |
|-----------|----------|
| **Lands** | Play correctly, skip targeting mode (detected via card type) |
| **Creatures** | Play correctly, skip targeting mode (detected via stack increase event) |
| **Targeted spells** | Tab targeting mode activates, cycles through valid targets only |

### User Flow for Targeted Spells

1. Play targeted spell from hand (Enter)
2. "Select a target. N valid targets" announced
3. Press **Tab** to cycle through valid targets
4. Press **Shift+Tab** to cycle backward
5. Press **Enter** to select current target
6. Press **Escape** to cancel targeting

### Technical Details

#### Targeting Mode Detection
After playing a card, UIActivator checks for Submit/Cancel buttons to detect targeting mode:

```csharp
// UIActivator.IsTargetingModeActive()
// Looks for "Submit X" button (Submit 0, Submit 1, etc.)
// Or Cancel button on PromptButton_Secondary
var submitPattern = new Regex(@"^Submit\s*\d+$", RegexOptions.IgnoreCase);
```

#### HotHighlight Detection (Key Discovery)
Valid targets have an active `HotHighlightBattlefield(Clone)` child object:

```csharp
// TargetNavigator.HasTargetingHighlight()
foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
{
    if (child == null || !child.gameObject.activeInHierarchy) continue;
    if (child.name.Contains("HotHighlight"))
        return true; // This card is a valid target
}
```

**Visual highlight structure:**
- **Valid target:** `Highlights - Default(Clone)` → `HotHighlightBattlefield(Clone)` (ACTIVE)
- **Non-target:** `Highlights - Default(Clone)` with no HotHighlight child
- **Spell on stack:** Has `_GlowColor` green glow (NOT the targeting indicator)

#### Land Detection
```csharp
// In UIActivator.PlayCardCoroutine
var cardInfo = CardDetector.ExtractCardInfo(card);
bool isLand = cardInfo.TypeLine?.ToLower().Contains("land") ?? false;
if (isLand)
{
    Log("Land played, skipping targeting mode");
}
```

### Files

| File | Purpose |
|------|---------|
| `src/Core/Services/TargetNavigator.cs` | Target cycling service (~340 lines) |
| `src/Core/Services/UIActivator.cs` | `IsTargetingModeActive()` detection, `PlayCardViaTwoClick()` integration |
| `src/Core/Models/TargetInfo.cs` | Target data model and CardTargetType enum |

See `docs/TARGET_SELECTION_IMPLEMENTATION_PLAN.md` for detailed implementation notes.

---

## Related Documentation

- `docs/BEST_PRACTICES.md` - UIActivator usage patterns
- `docs/MOD_STRUCTURE.md` - Implementation status
- `docs/TARGET_SELECTION_IMPLEMENTATION_PLAN.md` - Original targeting plan
