# Auto-Skip Mode Investigation

## Problem Description

In Color Challenge fights (not tutorials), the game auto-skips phases too aggressively:
- After playing a card (creature, land, spell), phases skip instantly
- Combat phase is skipped even with untapped creatures that could attack
- Declare Attackers phase lasts only ~50ms before skipping to End of Combat
- This makes the game nearly unwinnable for accessibility users

**Tutorial vs Color Challenge difference:**
- Tutorial has special NPE (New Player Experience) pauses that wait for player input
- Color Challenges use normal game auto-pass behavior

## Key Findings

### 1. FullControl Component Exists

The game has a `FullControl` component that can prevent auto-passing:

```
Found FullControl on FullControl_Desktop_16x9(Clone)
  enabled: True, activeInHierarchy: True
  isActiveAndEnabled: True
```

**Game classes discovered (from analysis_input.txt):**
- `FullControl` - Base MonoBehaviour with `ToggleAutoPass()` method
- `Wotc.Mtga.DuelScene.FullControlToggle` - Extends FullControl, has `ShowToggle()` and `HideToggle()`
- `Wotc.Mtga.DuelScene.FullControlEvents` - Events for full control changes

### 2. "Ctrl" is a BUTTON, Not Just a Keyboard Shortcut

**Critical discovery:** The game UI shows a "Ctrl" button:
```
Selectable: StyledButton - PromptButton_Secondary_Desktop_16x9(Clone) - Text: Ctrl
Selectable: StyledButton - PromptButton_Primary_Desktop_16x9(Clone) - Text: Next
```

The "Ctrl" button appears as the secondary prompt button during your turn. Clicking it should enable full control mode. The keyboard Ctrl shortcut may not work in Color Challenges.

### 3. Documented Game Keyboard Shortcuts

From `docs/TARGET_SELECTION_IMPLEMENTATION_PLAN.md`:
```
- Space - Pass priority
- Enter - Pass until response
- Shift+Enter - Pass turn
- Ctrl - Full control (temporary)
- Ctrl+Shift - Full control (permanent)
```

**Status:** Keyboard Ctrl was tested but did NOT prevent auto-skip in Color Challenges.

### 4. The 0.6 Second Wait Issue

In `UIActivator.PlayCardCoroutine()`, after playing a spell:
```csharp
// Step 3: Click screen center
SimulateClickAtPosition(center);

// Step 4: Wait for game to process
yield return new WaitForSeconds(0.6f);  // <-- During this wait, game auto-passes!
```

**Timeline example:**
- Card play completes
- 0.6s wait begins
- ~100ms: Spell resolves
- ~200ms: Combat phase
- ~250ms: Declare attackers
- ~300ms: End of combat
- ~350ms: Second main
- ~400ms: Opponent's turn
- 0.6s wait ends (too late!)

### 5. Global Click Fix Applied

We removed the "global click" that was sent to EventSystem when screen center didn't hit UI:
```csharp
// OLD (caused phase skipping):
ExecuteEvents.Execute(eventSystem.gameObject, pointer, ExecuteEvents.pointerDownHandler);
ExecuteEvents.Execute(eventSystem.gameObject, pointer, ExecuteEvents.pointerUpHandler);

// NEW:
Log($"No raycast hit at {screenPosition}, skipping global click");
return new ActivationResult(true, "No UI target at position", ActivationType.Unknown);
```

**Result:** Did not fully fix the issue - game still auto-passes through phases.

### 6. Stack Resolution Check Applied

Added check in `HighlightNavigator.ActivateCurrentCard()` to prevent playing cards when stack needs resolution:
```csharp
if (card.Zone == "Hand" && IsStackResolutionPending())
{
    _announcer.Announce(Strings.ResolveStackFirst, AnnouncementPriority.High);
    return;
}
```

This checks for "Resolve" button + cards on stack before allowing new card plays.

## Log Evidence

### Auto-skip pattern (every time after playing a card):
```
[17:59:07.626] Card play succeeded
[17:59:08.072] Combat phase
[17:59:08.122] Declare attackers     (50ms later)
[17:59:08.172] End of combat         (50ms later - NO TIME TO ATTACK!)
[17:59:08.216] Second main phase
[17:59:08.318] Turn 8. Opponent's turn
```

### Ctrl key detection working but not helping:
```
[17:59:04.321] Ctrl pressed - full control should be toggled
[17:59:04.332] Found FullControl on FullControl_Desktop_16x9(Clone)
[17:59:07.626] Card play succeeded
[17:59:08.072] Combat phase          <-- Still auto-skips!
```

## Potential Solutions (Not Yet Implemented)

### Option 1: Click the "Ctrl" Button Automatically
After playing a card, find and click the "Ctrl" button (PromptButton_Secondary) to enable full control before phases auto-skip.

### Option 2: Add Keyboard Shortcut to Click Ctrl Button
Add a custom shortcut (e.g., Ctrl+F) that clicks the "Ctrl" button for the user.

### Option 3: Reduce Wait Times
For non-targeted spells (creatures, artifacts), skip or shorten the 0.6s wait since they don't need targeting UI.

### Option 4: Call ToggleAutoPass() Directly
Find the FullControl component and call `ToggleAutoPass()` via reflection after playing a card.

### Option 5: Simulate Ctrl Key via WinAPI
Add keyboard simulation (like we have for mouse) to press/hold Ctrl key.

## Files Modified

- `src/Core/Services/UIActivator.cs` - Removed global click, added targeting mode check
- `src/Core/Services/HighlightNavigator.cs` - Added stack resolution check, HasResolveButton()
- `src/Core/Services/DuelNavigator.cs` - Added Ctrl key logging and LogFullControlState()
- `src/Core/Models/Strings.cs` - Added ResolveStackFirst message

## Files to Reference

- `analysis_input.txt` - Game class analysis (search for FullControl, KeyboardManager)
- `docs/TARGET_SELECTION_IMPLEMENTATION_PLAN.md` - MTGA keyboard shortcuts reference
- `docs/CARD_PLAY_IMPLEMENTATION.md` - Card play flow and wait times

## Attempted Solution: Direct ToggleAutoPass() Call (2026-01-15)

### Implementation

We implemented a direct call to `ToggleAutoPass()` via reflection when Ctrl is pressed:

```csharp
/// <summary>
/// Toggles full control mode by calling ToggleAutoPass() on the FullControl component.
/// This bypasses the game's native Ctrl shortcut which doesn't work reliably.
/// TODO: Expand later - add permanent toggle (Ctrl+Shift), visual indicator, mode tracking
/// </summary>
private void ToggleFullControl()
{
    foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
    {
        if (mb == null || !mb.gameObject.activeInHierarchy) continue;

        var typeName = mb.GetType().Name;
        if (typeName == "FullControl" || typeName == "FullControlToggle")
        {
            // Find and invoke ToggleAutoPass method
            var method = mb.GetType().GetMethod("ToggleAutoPass",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                try
                {
                    method.Invoke(mb, null);

                    // Try to read the new state to announce it
                    var visibleProp = mb.GetType().GetProperty("Visible");
                    if (visibleProp != null)
                    {
                        bool isOn = (bool)visibleProp.GetValue(mb);
                        string announcement = isOn ? "Full control on" : "Full control off";
                        _announcer.Announce(announcement, Models.AnnouncementPriority.High);
                        MelonLogger.Msg($"[{NavigatorId}] {announcement}");
                    }
                    else
                    {
                        _announcer.Announce("Full control toggled", Models.AnnouncementPriority.High);
                        MelonLogger.Msg($"[{NavigatorId}] Full control toggled (state unknown)");
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"[{NavigatorId}] Failed to toggle full control: {ex.Message}");
                }
                return;
            }
        }
    }
    MelonLogger.Msg($"[{NavigatorId}] FullControl component not found in scene");
}
```

### Results

**The method was called successfully but had NO EFFECT on auto-skip behavior:**

```
06:10:17.487 - Full control toggled (state unknown) ← Ctrl pressed
06:10:21.225 - Land played via double-click
06:10:21.673 - Combat phase        ← 450ms later, still auto-skips!
06:10:21.714 - Declare attackers   ← 40ms
06:10:21.754 - End of combat       ← 40ms
06:10:21.809 - Second main phase   ← 55ms
06:10:21.904 - Turn 6. Opponent's turn ← 95ms
```

The game still auto-skipped through ALL phases in ~230ms total after card play.

### Key Observations

1. **"state unknown"** - Could not read the `Visible` property, suggesting it doesn't exist or works differently
2. **Same pattern on multiple turns** - Auto-skip happened identically on Turn 5 and Turn 7 after toggling
3. **Method call didn't throw errors** - The reflection call succeeded but didn't change behavior

### Possible Explanations

1. **ToggleAutoPass() might not be the right method** - Could need `EnableFullControl()`, `SetFullControl(bool)`, or similar
2. **Color Challenge mode bypasses FullControl entirely** - NPE/tutorial mode might have hardcoded auto-skip that ignores normal FullControl system
3. **FullControl only affects priority, not phase skipping** - May only prevent auto-passing when you COULD respond, not phase transitions
4. **Wrong component instance** - Might be finding a UI display component rather than the logic controller
5. **Method call silently fails** - Reflection succeeded but internal state didn't change

### Settings Panel Investigation

Also investigated the Gameplay settings panel to look for auto-skip related settings:

**Found 8 toggles (all accounted for):**
- Show Phases, Enable Gameplay Warnings, Auto Order Triggered Abilities
- Evergreen Keyword Reminders, Fixed Rules Text Size, Disable Emotes
- Hide Alternate Art Styles, Auto Apply Card Styles

**NOT found:**
- No CustomToggle components in Gameplay panel
- No Scrollbars (no hidden settings below)
- No "Auto Assign Combat Damage" setting (mentioned in online guides but not present)
- "Auto Tab" and "Auto Place" visible via OCR are likely static labels/images, not toggles

### Keyboard Shortcut Research

From web research, MTGA keyboard shortcuts include:
- **Ctrl** - Temporary full control (hold priority)
- **Ctrl + Shift** - Permanent full control toggle
- **Q + Click** - Mark lands to tap together
- **Only LEFT Shift key works** for hotkey combinations

## Next Steps (Requires Sighted Assistance)

1. **Verify Ctrl indicator** - Check if a "Ctrl" visual indicator appears on screen when pressing Ctrl
2. **Test in regular Play mode** - Try Ctrl in non-Color Challenge matches to see if it works there
3. **Observe FullControl UI** - See what the FullControl component actually displays when toggled
4. **Check for alternative methods** - Look for other methods on FullControl like `Enable()`, `SetActive()`, etc.
5. **Consider Color Challenge bypass** - May need completely different approach for NPE mode
