# Player Info Zone Implementation Plan

## Overview

Create a new player info zone accessible via **V key** that follows the established zone navigation pattern. Players can navigate between local player and opponent, read properties with arrow down, and access emotes via Enter.

---

## User Experience Design

### Activation (V key)
- **V** activates Player Info Zone
- Announces: "Player info. You" (starts on local player)
- Zone stays active until Escape or another zone shortcut

### Horizontal Navigation (Left/Right arrows)
```
[You] ←→ [Opponent]
```
- **Arrow Right**: Move from You to Opponent
  - Announces: "Opponent"
- **Arrow Left**: Move from Opponent to You
  - Announces: "You"
- At boundaries: "End of zone" (wrap optional)

### Vertical Navigation (Up/Down arrows) - Property Cycling
When focused on a player, arrow down cycles through their properties:

```
Position 1: Life total (announced first, most important)
Position 2: Timer remaining
Position 3: Timeouts used
Position 4: Wins (best-of-3 matches)
Position 5: Rank
Position 6: Username
```

- **Arrow Down**: Next property
  - Announces: "20 life" → "5 minutes 30 seconds" → "0 timeouts" → etc.
- **Arrow Up**: Previous property
  - Wraps from first to last and vice versa
- At boundaries: Optional wrap or "End of properties"

### Activation (Enter key) - Emote Menu
When focused on a player (at any property):
- **Enter** opens the emote wheel submenu
- Announces: "Emotes" or "Emote menu"
- Enters emote navigation mode

### Emote Navigation (submenu - modal)
```
[Hello]
[Nice]
[Good Game]
[Thinking]
[Oops]
[Your Go]
```

- **Arrow Down/Up**: Navigate through emotes
  - Announces emote name: "Hello", "Nice", etc.
- **Enter**: Select and send emote
  - Announces: "Hello sent" or similar
  - Returns to player navigation
- **Backspace**: Cancel without emoting
  - Closes emote wheel
  - Returns to player navigation
- Other keys blocked while emote wheel is open

---

## Architecture Decision

### Option A: Extend PlayerPortraitNavigator (Recommended)
- Already integrated in DuelNavigator input chain
- Has timer/timeout discovery logic
- Add V key handling and state machine

### Option B: Create New PlayerInfoNavigator
- Cleaner separation but requires DuelNavigator integration
- More code duplication

**Recommendation**: Option A - Extend existing PlayerPortraitNavigator

---

## State Machine

```
                                    ┌─────────────────┐
                                    │    INACTIVE     │
                                    └────────┬────────┘
                                             │ V key
                                             ▼
                     ┌───────────────────────────────────────────┐
                     │              PLAYER_NAVIGATION            │
                     │  ┌─────────┐           ┌─────────────┐   │
                     │  │   You   │◄─ L/R ──►│  Opponent   │   │
                     │  └────┬────┘           └──────┬──────┘   │
                     │       │ Up/Down              │ Up/Down   │
                     │       ▼                      ▼           │
                     │  [Property cycling]   [Property cycling] │
                     └──────────────┬───────────────────────────┘
                                    │ Enter
                                    ▼
                     ┌──────────────────────────────────────────┐
                     │       EMOTE_NAVIGATION (modal)           │
                     │  [Hello] [Nice] [GG] [Thinking] [Oops]   │
                     │  ▲ Up/Down ▼                             │
                     │  Enter = send emote → back to PLAYER     │
                     │  Backspace = cancel → back to PLAYER     │
                     │  (other keys blocked)                    │
                     └──────────────────────────────────────────┘
```

---

## Implementation Steps

### Phase 1: Basic V Key Zone Navigation

1. **Add state tracking to PlayerPortraitNavigator**
   - `_navigationState` enum: Inactive, PlayerNavigation, EmoteNavigation
   - `_currentPlayerIndex`: 0 = You, 1 = Opponent
   - `_currentPropertyIndex`: 0-5 for property cycling

2. **Handle V key activation**
   - Set state to PlayerNavigation
   - Set currentPlayerIndex to 0 (You)
   - Set currentPropertyIndex to 0 (Life)
   - Announce "Player info. You. 20 life" (zone + player + first property)

3. **Handle Left/Right arrows**
   - Toggle between player 0 and 1
   - Reset propertyIndex to 0 on player change
   - Announce player name + life total

4. **Handle Escape**
   - Set state to Inactive
   - (Let other navigators handle if needed)

### Phase 2: Property Cycling

5. **Define property list**
   ```csharp
   enum PlayerProperty { Life, Timer, Timeouts, Wins, Rank, Username }
   ```

6. **Handle Up/Down arrows**
   - Increment/decrement propertyIndex
   - Wrap at boundaries or announce "End of properties"
   - Announce current property value

7. **Implement property getters**
   - Life: From GameState (needs fix - currently not working)
   - Timer: From MatchTimer text (working)
   - Timeouts: From TimeoutDisplay (working)
   - Wins: From WinPipsAnchorPoint (new - needs discovery)
   - Rank: From RankAnchorPoint (new - needs discovery)
   - Username: From UserNameAnchorPoint (new - needs discovery)

### Phase 3: Emote Navigation

8. **Handle Enter key in PlayerNavigation state**
   - Trigger emote wheel click (existing TriggerEmoteMenu logic)
   - Wait for emote menu to appear
   - Set state to EmoteNavigation
   - Discover emote buttons

9. **Discover emote elements**
   - Find emote wheel container (needs investigation)
   - Extract button list with emote names
   - Cache for navigation

10. **Handle Up/Down in EmoteNavigation**
    - Navigate through discovered emotes
    - Announce emote name

11. **Handle Enter in EmoteNavigation**
    - Click selected emote button
    - Announce "Emote sent"
    - Return to PlayerNavigation state

12. **Handle Backspace in EmoteNavigation**
    - Close emote menu (click away or find close button)
    - Announce "Cancelled"
    - Return to PlayerNavigation state

13. **Block other keys in EmoteNavigation**
    - Return true (consumed) for all other keys while modal

### Phase 4: Player Targeting Integration

14. **Add player target discovery to TargetNavigator**
    - New method `DiscoverPlayerTargets()`
    - Find LocalPlayer/Opponent avatar containers
    - Check for HotHighlight to determine if targetable
    - Add as TargetInfo with Type = CardTargetType.Player

15. **Coordinate Enter key with targeting state**
    - Check if TargetNavigator.IsTargeting
    - If targeting and player has highlight: click to target
    - If not targeting and local player: open emote wheel
    - If not targeting and opponent: do nothing

16. **Add strings to Strings.cs**
    - PlayerInfo, You, Opponent, EndOfProperties
    - Life(), LifeNotAvailable, Timer(), Timeouts(), etc.
    - Emotes, EmoteSent(), EmotesNotAvailable

### Phase 5: Polish & Edge Cases

17. **Add to help text (F1)**
    - V: Player info zone
    - Document property cycling, targeting, and emote access

18. **Handle edge cases**
    - Emote menu not opening
    - Properties not available (use Strings.XxxNotAvailable)
    - Game state changes during navigation
    - Player not valid target but Enter pressed

---

## Player Targeting Support

### Current Gap
`TargetNavigator.DiscoverValidTargets()` only scans card zones (BattlefieldCardHolder, StackCardHolder). It doesn't discover player avatars as valid targets, even though `CardTargetType.Player` exists.

### Required Changes

**1. TargetNavigator - Add player target discovery**

In `DiscoverValidTargets()`, after scanning card zones, also scan for player portrait elements with HotHighlight:

```csharp
// After card scanning, check for player targets
DiscoverPlayerTargets();
```

```csharp
private void DiscoverPlayerTargets()
{
    // Find LocalPlayer and Opponent avatar containers
    // Check if they have active HotHighlight children
    // If so, add as TargetInfo with Type = CardTargetType.Player

    var localAvatar = FindPlayerAvatar(isOpponent: false);
    if (localAvatar != null && HasTargetingHighlight(localAvatar))
    {
        _validTargets.Add(new TargetInfo
        {
            GameObject = localAvatar,
            Name = Strings.You,
            InstanceId = (uint)localAvatar.GetInstanceID(),
            Type = CardTargetType.Player,
            IsOpponent = false,
            Details = "" // Could include life total
        });
    }

    var opponentAvatar = FindPlayerAvatar(isOpponent: true);
    if (opponentAvatar != null && HasTargetingHighlight(opponentAvatar))
    {
        _validTargets.Add(new TargetInfo
        {
            GameObject = opponentAvatar,
            Name = Strings.Opponent,
            InstanceId = (uint)opponentAvatar.GetInstanceID(),
            Type = CardTargetType.Player,
            IsOpponent = true,
            Details = ""
        });
    }
}
```

**2. PlayerPortraitNavigator - Coordinate with targeting**

When Enter is pressed on a player in player info zone:
- Check if TargetNavigator.IsTargeting
- Check if current player has HotHighlight (is valid target)
- If both true: Click to target (announce "Targeted You" / "Targeted Opponent")
- If not targeting: Open emote wheel (for local player only)

```csharp
private void HandleEnterOnPlayer()
{
    var targetNav = GetTargetNavigator();

    if (targetNav != null && targetNav.IsTargeting)
    {
        // Check if current player is a valid target
        if (IsCurrentPlayerValidTarget())
        {
            ClickCurrentPlayer();
            return;
        }
    }

    // Not targeting - open emote wheel (local player only)
    if (_currentPlayerIndex == 0) // You
    {
        OpenEmoteWheel();
    }
}
```

**3. Announce player targets properly**

When Tab cycling in targeting mode reaches a player:
- Announce: "You, player, 1 of 5" or "Opponent, player, 2 of 5"

---

## Strings System Integration

All announcements must use `Strings.cs` for future localization.

### New Strings to Add

```csharp
// ===========================================
// PLAYER INFO ZONE
// ===========================================
public const string PlayerInfo = "Player info";
public const string You = "You";
public const string Opponent = "Opponent";
public const string EndOfProperties = "End of properties";

// Property announcements
public static string Life(int amount) => $"{amount} life";
public const string LifeNotAvailable = "Life not available";
public static string Timer(string formatted) => formatted; // Already formatted
public const string TimerNotAvailable = "Timer not available";
public static string Timeouts(int count) => count == 1 ? "1 timeout" : $"{count} timeouts";
public static string GamesWon(int count) => count == 1 ? "1 game won" : $"{count} games won";
public const string WinsNotAvailable = "Wins not available";
public static string Rank(string rank) => rank;
public const string RankNotAvailable = "Rank not available";

// Emote menu
public const string Emotes = "Emotes";
public static string EmoteSent(string emoteName) => $"{emoteName} sent";
public const string EmotesNotAvailable = "Emotes not available";

// Player targeting
public const string PlayerType = "player"; // For target announcements
```

### Update Existing Targeting Strings

Already have:
- `Strings.Targeted(name)` - "Targeted {name}"
- `Strings.CouldNotTarget(name)` - "Could not target {name}"

These work for players too: "Targeted You", "Targeted Opponent"

---

## Data Discovery Needed

### Already Working
- Timer text (MatchTimer component)
- Timeout count (TimeoutDisplay)

### Needs Investigation
- **Life total**: MtgPlayer doesn't expose Life property directly. Options:
  - Try LifeTotalUpdateUXEvent fields
  - Find 3D PlayerHpContainer child with TextMesh
  - Track via DuelAnnouncer events

- **Win pips**: WinPipsAnchorPoint structure unknown
  - Likely contains pip images or count

- **Rank**: RankAnchorPoint structure unknown
  - May have rank icon + text

- **Username**: UserNameAnchorPoint
  - Should have TextMeshPro with display name

- **Emote wheel**: Need to discover structure when opened
  - Button names and how to click them

---

## Integration Points

### DuelNavigator.HandleCustomInput()
PlayerPortraitNavigator already integrated at line 389:
```csharp
if (_portraitNavigator.HandleInput())
    return true;
```
No changes needed to DuelNavigator.

### Input Priority
PlayerPortraitNavigator runs after:
- BrowserNavigator (modal dialogs)
- TargetNavigator (spell targeting)
- DiscardNavigator
- CombatNavigator
- HighlightNavigator
- BattlefieldNavigator

And before:
- ZoneNavigator
- BaseNavigator

This is appropriate - V key zone should not override targeting/combat.

---

## Announcements

### Zone Activation
- "Player info. You. 20 life"

### Player Switching
- "You" / "Opponent"
- Followed by current property (life by default)

### Property Announcements
- Life: "20 life" / "life not available"
- Timer: "5 minutes 30 seconds" / "timer not available"
- Timeouts: "0 timeouts" / "1 timeout"
- Wins: "1 game won" / "0 games won" / "wins not available"
- Rank: "Gold 2" / "rank not available"
- Username: "PlayerName#12345"

### Emote Menu
- "Emotes" (on Enter)
- "Hello" / "Nice" / "Good Game" / etc. (on navigation)
- "Hello sent" (on Enter to send)
- "Cancelled" (on Backspace)

### Boundaries
- "End of properties" (at top/bottom of property list)
- "End of zone" (at left/right player boundaries)

---

## Files to Modify

1. **PlayerPortraitNavigator.cs** - Main V key zone implementation
   - State machine (Inactive/PlayerNavigation/EmoteNavigation)
   - V key activation, Left/Right player switching, Up/Down property cycling
   - Enter handling (targeting vs emote wheel)
   - Backspace for emote cancel

2. **TargetNavigator.cs** - Add player target discovery
   - New `DiscoverPlayerTargets()` method
   - New `FindPlayerAvatar()` helper
   - Call from `DiscoverValidTargets()`

3. **Strings.cs** - Add announcement strings
   - Player info zone section
   - Property announcements
   - Emote menu strings

4. **DuelNavigator.cs** - No changes needed (already integrated)

5. **CLAUDE.md** - Update safe shortcuts documentation (add V key)

6. **Help text** - Add V key to F1 help

---

## Testing Checklist

### Basic Navigation
- [ ] V key activates zone, announces "Player info. You. X life"
- [ ] Right arrow moves to opponent, announces "Opponent. X life"
- [ ] Left arrow moves back to you
- [ ] Down arrow cycles through properties
- [ ] Up arrow cycles backwards
- [ ] Boundaries announce "End of zone" / "End of properties"

### Emote Menu
- [ ] Enter on You opens emote menu (when not targeting)
- [ ] Enter on Opponent does nothing (when not targeting)
- [ ] Arrow keys navigate emotes when wheel is open
- [ ] Enter sends emote and returns to player navigation
- [ ] Backspace cancels emote wheel without sending
- [ ] Other keys blocked while emote wheel is open

### Player Targeting
- [ ] Tab cycling includes players when they have HotHighlight
- [ ] Player targets announced as "You, player, X of Y"
- [ ] V key works mid-targeting to navigate to player
- [ ] Enter on highlighted player targets them
- [ ] Announces "Targeted You" / "Targeted Opponent"
- [ ] Enter on non-targetable player does nothing (or opens emotes for You)

### Zone Flexibility
- [ ] Other zone keys (C/G/X etc.) work from player navigation
- [ ] Can return to player info after visiting other zones
- [ ] Works during all game phases

### Data & Strings
- [ ] All announcements use Strings.cs constants/methods
- [ ] Handles missing data gracefully (uses XxxNotAvailable strings)

---

## Design Decisions

1. **No wrapping** - Navigation stops at ends with "End of zone" / "End of properties"
2. **Emotes only for local player** - Enter on opponent does nothing (no evidence of viewable content)
3. **Flexible zone switching** - Other zone keys (C/G/X/S etc.) work normally, allowing free navigation between zones mid-action (e.g., check life while targeting)
4. **Emote wheel is modal** - While emote wheel is open, only Up/Down/Enter/Backspace work
5. **Backspace closes emote wheel** - Cancels without sending, returns to player navigation
