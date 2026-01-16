# Player Representation Interaction

Documentation for player portrait/representation accessibility features during duels.

## Current Implementation

### PlayerPortraitNavigator (src/Core/Services/PlayerPortraitNavigator.cs)

**Implemented shortcuts:**
- **P** - Announces your timer info (time remaining, timeout count)
- **Shift+P** - Announces opponent timer info
- **L** - Attempts to announce life totals (currently not working reliably)

**Working features:**
- Timer text extraction from MatchTimer components
- Timeout count from TimeoutDisplay elements
- Low time warning detection

**Not working:**
- Life total query (MtgPlayer doesn't expose Life property directly)

---

## Discovered UI Structure

### Battlefield Layout Hierarchy
```
BattleFieldStaticElementsLayout_Desktop_16x9(Clone)
└── Base
    ├── LocalPlayer
    │   ├── HandContainer
    │   │   └── LifeFrameContainer
    │   │       ├── PlayerHpContainer [BattleFieldStaticLayoutWorldSpaceElementData]
    │   │       └── TurnFrameContainer [BattleFieldStaticLayoutWorldSpaceElementData]
    │   ├── LeftContainer
    │   │   └── AvatarContainer
    │   │       ├── WinPipsAnchorPoint (game wins in best-of-3)
    │   │       ├── UserNameContainer
    │   │       │   ├── UserName/UserNameAnchorPoint
    │   │       │   └── TimeoutDisplay/TimeoutDisplayAnchorPoint
    │   │       ├── MatchTimerContainer/MatchTimerAnchorPoint
    │   │       └── RankAnchorPoint
    │   └── PrompButtonsContainer
    │       ├── FullControlAnchorPoint
    │       └── PromptButtonsAnchorPoint
    └── Opponent
        ├── LifeFrameContainer
        │   ├── PlayerHpContainer [BattleFieldStaticLayoutWorldSpaceElementData]
        │   ├── ResourcePoolContainer [BattleFieldStaticLayoutWorldSpaceElementData]
        │   └── TurnFrameContainer [BattleFieldStaticLayoutWorldSpaceElementData]
        └── AvatarContainer
            ├── WinPipsAnchorPoint
            ├── UserNameContainer (UserName, TimeoutDisplay)
            ├── MatchTimerContainer
            └── RankAnchorPoint
```

### Match Timer Structure
```
LocalPlayerMatchTimer_Desktop_16x9(Clone) [MatchTimer, Animator]
├── Icon [Image]
│   ├── Pulse [Image]
│   └── HoverArea [Image, EventTrigger] <-- Clickable for emotes?
├── Text [TextMeshProUGUI] <-- Shows "00:00" format
└── WarningPrompt [Image]
    └── Text - DescriptionBottom [TextMeshProUGUI, Localize]
        └── WarningIcon [Image]
```

### Timer Bar Structure (Turn Timer)
```
Timer_Player / Timer_Opponent [TimerAnimationEvents]
├── Anchor
│   ├── Bar / Bar_Opponent (visual timer bar)
│   └── Flare
├── Icon_Bright / Icon_Dark
└── Pips [LayoutGroup] <-- Timeout pip indicators
```

---

## Discovered Components & Properties

### GameManager Properties (Available)
| Property | Type | Notes |
|----------|------|-------|
| CurrentGameState | MtgGameState | NULL at discovery, populated during gameplay |
| LatestGameState | MtgGameState | NULL at discovery, populated during gameplay |
| MatchManager | MatchManager | Match state management |
| TimerManager | TimerManager | Timer management |
| UIManager | UIManager | UI state |
| KeyboardManager | KeyboardManager | Game's keyboard input |
| ViewManager | EntityViewManager | Entity views |
| CardHolderManager | CardHolderManager | Card holder management |

### MtgGameState Properties (When Populated)
| Property | Type | Notes |
|----------|------|-------|
| LocalPlayer | MtgPlayer | Direct access to local player |
| Opponent | MtgPlayer | Direct access to opponent |
| LocalHand | MtgZone | Local player's hand |
| OpponentHand | MtgZone | Opponent's hand |
| Battlefield | MtgZone | Shared battlefield |
| Stack | MtgZone | The stack |
| LocalGraveyard | MtgZone | Local graveyard |
| OpponentGraveyard | MtgZone | Opponent graveyard |
| Exile | MtgZone | Exile zone |
| LocalPlayerBattlefieldCards | IReadOnlyList | Direct card list |
| OpponentBattlefieldCards | IReadOnlyList | Direct card list |
| IsMultiplayer | Boolean | Multiplayer flag |

### MtgPlayer Properties (Visible)
| Property | Type | Notes |
|----------|------|-------|
| IsLocalPlayer | Boolean | Identifies local vs opponent |
| ManaPoolString | String | Current mana pool (empty string seen) |
| DummyLocal | MtgPlayer | Reference to local player |
| DummyOpponent | MtgPlayer | Reference to opponent |

**Missing:** No visible `Life`, `LifeTotal`, or `Health` property found on MtgPlayer.

### TextMeshPro Elements Found
| Element | Parent | Content | Notes |
|---------|--------|---------|-------|
| Text | LocalPlayerMatchTimer | "00:00" | Match time remaining |
| Text | OpponentMatchTimer | "00:00" | Match time remaining |
| Text | LocalPlayerTimeoutDisplay | "x0" | Timeout count |
| Text | OpponentTimeoutDisplay | "x0" | Timeout count |

### World-Space Elements (3D Rendered)
| Container | Component | Purpose |
|-----------|-----------|---------|
| PlayerHpContainer | BattleFieldStaticLayoutWorldSpaceElementData | Life total display position |
| TurnFrameContainer | BattleFieldStaticLayoutWorldSpaceElementData | Turn indicator position |
| ResourcePoolContainer | BattleFieldStaticLayoutWorldSpaceElementData | Mana pool display (opponent) |

---

## Unused/Unexplored Properties

### Potentially Useful
1. **HoverArea EventTrigger** - Could trigger emote menu when clicked
2. **UserName/UserNameAnchorPoint** - Player display names
3. **WinPipsAnchorPoint** - Game wins in best-of-3 matches
4. **RankAnchorPoint** - Player rank display
5. **ResourcePoolContainer** - Opponent's mana pool visualization
6. **MatchManager** - Might have player/game state access
7. **TimerManager** - Detailed timer information

### Events (from DuelAnnouncer)
1. **LifeTotalUpdateUXEvent** - Contains life change info, might have absolute value
2. **CreatePlayerUXEvent** - Player creation, might have initial life
3. **UpdatePlayerHandSizeUXEvent** - Hand size updates

---

## Life Total Access - Investigation Needed

### Option 1: Query GameState On-Demand
- Query `CurrentGameState.LocalPlayer` when L is pressed (not at discovery)
- GameState is null at startup but populated during gameplay
- **Cost:** Low - minor code change
- **Risk:** May be null during certain phases

### Option 2: Find 3D Life Counter Component
- `PlayerHpContainer` spawns a 3D prefab for life display
- Need to explore children during gameplay
- **Cost:** Medium - runtime exploration
- **Risk:** Might use custom renderer

### Option 3: Extract from LifeTotalUpdateUXEvent
- Event likely contains absolute life value, not just delta
- Need to explore event fields more thoroughly
- **Cost:** Low - field exploration
- **Risk:** None if field exists

### Option 4: Search MtgPlayer Methods
- Might have `GetLifeTotal()` method instead of property
- **Cost:** Low - add method search
- **Risk:** Method might not exist

---

## TODO List

### High Priority
- [ ] Fix life total query (try Option 3 first - explore LifeTotalUpdateUXEvent fields)
- [ ] Test GameState query during gameplay (Option 1)
- [ ] Explore PlayerHpContainer children at runtime for 3D life display

### Medium Priority
- [ ] Add E key shortcut to trigger emote menu (click HoverArea)
- [ ] Extract and announce player usernames
- [ ] Show game wins (WinPips) in best-of-3 matches
- [ ] Show player rank information

### Low Priority
- [ ] Explore ResourcePoolContainer for opponent mana display
- [ ] Add mana pool announcement (from ManaPoolString or ResourcePool)
- [ ] Investigate MatchManager for additional game state info
- [ ] Investigate TimerManager for detailed timer data

### Code Restructuring
- [ ] Create unified PlayerInfo struct to hold all player data
- [ ] Add caching for player info to avoid repeated lookups
- [ ] Consider event-based life tracking as fallback
- [ ] Move exploration/debug logging to separate debug mode
- [ ] Clean up ExplorePlayerPortraits() after investigation complete

### Menu/Navigation Improvements
- [ ] Add player info to F1 help text
- [ ] Consider dedicated "player status" mode (cycle through all player info)
- [ ] Add Shift+L for detailed player breakdown (life, mana, timeouts, rank)

---

## Code References

- **PlayerPortraitNavigator:** `src/Core/Services/PlayerPortraitNavigator.cs`
- **DuelNavigator (exploration):** `src/Core/Services/DuelNavigator.cs:ExplorePlayerPortraits()`
- **DuelAnnouncer (life events):** `src/Core/Services/DuelAnnouncer.cs:BuildLifeChangeAnnouncement()`
- **Exploration logging:** Search for `[Portrait]`, `[Life]`, `[LifeUI]` in logs

---

## Testing Notes

- Timer info (P key) works correctly, shows "no time" in tutorial matches
- Life totals (L key) returns "not available" - MtgPlayer lacks Life property
- GameState is null at duel start, need to query during gameplay
- Life changes ARE announced via events (gained/lost X life)

---

## Player Targeting Investigation (2026-01-16)

### Findings

When targeting spells like Shock ("any target") are cast, the game uses HotHighlight to mark valid targets. Investigation of Color Challenge (tutorial mode) revealed:

**Player objects checked for HotHighlight:**
- `LocalPlayerMatchTimer_Desktop_16x9(Clone)` - HasHighlight: False, Icon child inactive
- `OpponentMatchTimer_Desktop_16x9(Clone)` - HasHighlight: False, Icon child inactive
- `LocalPlayer` container (under BattleFieldStaticElementsLayout/Base) - HasHighlight: False
- `Opponent` container (under BattleFieldStaticElementsLayout/Base) - HasHighlight: False
- PlayerHpContainer, LifeFrameContainer, AvatarContainer - all HasHighlight: False

**Conclusion:** Color Challenge does NOT support player targeting. Only creature targets receive HotHighlight. The MatchTimer UI is completely disabled (all children inactive) in tutorial mode.

### Code Location

Player target detection is implemented in `TargetNavigator.FindPlayerMatchTimer()` and `DiscoverPlayerTargets()`. The infrastructure is in place for when regular games are tested - if those modes support player targeting, HotHighlight should appear on one of the checked objects.

### For Future Reference

If player targeting works in regular games, the clickable element is likely:
- `MatchTimer > Icon > HoverArea` (preferred)
- `MatchTimer > Icon` (fallback)
- The container with HotHighlight itself
