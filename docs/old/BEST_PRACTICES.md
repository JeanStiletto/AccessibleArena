# MTGA Accessibility Mod - Best Practices

## Game Architecture

### Unity/MTGA Basics
- Engine: Unity with Mono runtime
- Game location: `C:\Program Files\Wizards of the Coast\MTGA`
- Key assemblies in `MTGA_Data\Managed`:
  - `Core.dll` - Game interfaces and systems (10,632 types)
  - `Assembly-CSharp.dll` - Main game code (HasbroGo namespace, login menus)
  - `Wizards.Arena.Models.dll` - Data models

### Card Identifiers
- `grpId` (UInt32): Unique card identifier across the system
- `InstanceId` (UInt32): In-game card instance on battlefield

### DuelScene Zone Architecture
Discovered via game code analysis and log inspection. Zone holders are GameObjects with specific naming patterns.

**Zone Holder Names (from game code):**
| Zone | GameObject Pattern | ZoneId | Type |
|------|-------------------|--------|------|
| Your Hand | `LocalHand_Desktop_16x9` | #31 | Hand |
| Opponent Hand | `OpponentHand_Desktop_16x9` | #35 | Hand |
| Battlefield | `BattlefieldCardHolder` | #28 | Battlefield |
| Your Graveyard | `LocalGraveyard` | #33 | Graveyard |
| Opponent Graveyard | `OpponentGraveyard` | #37 | Graveyard |
| Exile | `ExileCardHolder` | #29 | Exile |
| Stack | `StackCardHolder_Desktop_16x9` | #27 | Stack |
| Your Library | `LocalLibrary` | #32 | Library |
| Opponent Library | `OpponentLibrary` | #36 | Library |
| Command | `CommandCardHolder` | #26 | Command |

**Zone metadata embedded in GameObject names:**
```
LocalHand_Desktop_16x9 ZoneId: #31 | Type: Hand | OwnerId: #1
```
Parse with regex: `ZoneId:\s*#(\d+)`, `OwnerId:\s*#(\d+)`

**Card Detection in Zones:**
Cards are children of zone holders. Detection patterns:
- Name prefix: `CDC #` (Card Display Controller)
- Name contains: `CardAnchor`
- Component types: `CDCMetaCardView`, `CardView`, `DuelCardView`, `Meta_CDC`

**Key Game Classes (from decompiled Core.dll):**
- `GameManager` - Central manager with `CardHolderManager`, `ViewManager`, `CardDatabase`
- `CDCMetaCardView` - Card view with `Card`, `VisualCard` properties
- `IdNameProvider.GetName(UInt32 entityId, Boolean formatted)` - Get card names
- `ICardView.InstanceId` - Card instance identifier

### Key Interfaces (for Harmony patches)
- `Wotc.Mtga.Cards.Text.ICardTextEntry.GetText()` - Card text
- `Wotc.Mtga.DuelScene.IPlayerPresenceController.SetHoveredCardId(UInt32)` - Hover events
- `Core.Code.Input.INextActionHandler.OnNext()` - Navigation
- `Wotc.Mtga.DuelScene.ITurnInfoProvider.EventTranslationTurnNumber` - Turn tracking

### UXEvent System (Used by DuelAnnouncer)
The game uses a UX event queue (`Wotc.Mtga.DuelScene.UXEvents.UXEventQueue`) to process game events.

**Key Event Types:**
| Event Class | Purpose | Key Fields |
|-------------|---------|------------|
| `UpdateTurnUXEvent` | Turn changes | `_turnNumber` (uint), `_activePlayer` (Player object) |
| `UpdateZoneUXEvent` | Zone state updates | `_zone` (string like "Hand (PlayerPlayer: 1 (LocalPlayer), 6 cards)") |
| `ZoneTransferGroup` | Card movements | `_zoneTransfers`, `_reasonZonePairs` |
| `UXEventUpdatePhase` | Phase changes | `_phase` or `_step` (needs investigation) |
| `ToggleCombatUXEvent` | Combat start/end | `_isEnabling` |
| `AttackLobUXEvent` | Attack animation | - |

**Player identification from _activePlayer:**
```csharp
string playerStr = activePlayerObj.ToString();
// Returns: "Player: 1 (LocalPlayer)" or "Player: 2 (Opponent)"
bool isYourTurn = playerStr.Contains("LocalPlayer");
```

**Zone parsing from _zone:**
```csharp
// Format: "Hand (PlayerPlayer: 1 (LocalPlayer), 6 cards)"
var zoneMatch = Regex.Match(zoneStr, @"^(\w+)\s*\(");     // Zone name
var countMatch = Regex.Match(zoneStr, @"(\d+)\s*cards?\)"); // Card count
bool isLocal = zoneStr.Contains("LocalPlayer");
bool isOpponent = zoneStr.Contains("Opponent");
```

### Main Menu Architecture

The main menu uses a different architecture than DuelScene. Key classes in `Core.Meta.MainNavigation`:

**NavContentController** - Base class for all menu screens (MonoBehaviour)
- Properties: `NavContentType`, `IsOpen`, `IsReadyToShow`, `SkipScreen`
- Methods: `BeginOpen()`, `BeginClose()`, `FinishOpen()`, `FinishClose()`, `Activate(bool)`
- Implementations: `HomePageContentController`, `ProfileContentController`, `AchievementsContentController`, etc.

**SettingsMenu / SettingsMenuHost** - Settings panel management
- `IsOpen` property/method to check if settings is active
- `Open()` / `Close()` for panel control
- `IsMainPanelActive` - main settings vs submenu

**WrapperSceneManagement** - Scene/menu loader
- `LoadScene()`, `UnloadScene()`, `IsSceneLoaded()`
- Manages multiple scenes loaded simultaneously

**PopupManager** - Modal dialogs
- `RegisterPopup()`, `UnregisterPopup()`, `ToggleMenu()`
- Has `Priority` property for input handling

**Button Types in Menus:**
- `CustomButton` - MTGA's custom component (most menu buttons, NOT Unity Selectable)
- `Button` - Unity standard (some overlay elements)
- `EventTrigger` - Special interactive elements

**Input Priority (highest to lowest):**
1. Debug, 2. SystemMessage, 3. SettingsMenu, 4. DuelScene, 5. Wrapper, 6. NPE

See `docs/MENU_NAVIGATION.md` for detailed documentation.

### Panel State Detection (Harmony Patches)

We use Harmony patches to get event-driven notifications when panels open/close.
This provides reliable overlay detection for Settings, DeckSelect, and other menus.

**PanelStatePatch.cs** - Patches for panel open/close detection:

**Successfully Patched Methods:**

NavContentController (base class for menu screens):
- `FinishOpen()` - Fires when panel finishes opening animation
- `FinishClose()` - Fires when panel finishes closing animation
- `BeginOpen()` / `BeginClose()` - Fires at start of open/close (logged only)
- `IsOpen` setter - Backup detection

SettingsMenu:
- `Open()` - Fires when settings opens (has 7 boolean parameters)
- `Close()` - Fires when settings closes
- `IsOpen` setter - Backup detection

DeckSelectBlade:
- `Show(EventContext, DeckFormat, Action)` - Fires when deck selection opens
- `Hide()` - Fires when deck selection closes
- `IsShowing` setter - Backup detection

**Key Architecture - Harmony Flag Approach:**

The critical insight: Harmony events are 100% reliable (method was definitely called),
but our reflection-based panel detection during rescan was unreliable. Solution:

1. When Harmony fires (e.g., `SettingsMenu.Open()` postfix), immediately set overlay flags:
   ```csharp
   _settingsOverlayActive = true;  // Set in OnPanelStateChangedExternal()
   ```

2. During element discovery, `IsInForegroundPanel()` checks these flags:
   ```csharp
   bool overlayActive = _foregroundPanel != null || _settingsOverlayActive || _deckSelectOverlayActive;
   if (!overlayActive) return true;  // No overlay, show all elements
   return !IsInBackgroundPanel(obj); // Filter out NavBar, HomePage elements
   ```

3. This ensures background elements are filtered even before the content panel is detected.

**Why This Works:**
- Harmony tells us exactly when Open()/Close() methods are called
- We trust this information immediately instead of trying to re-detect via reflection
- Background filtering happens reliably from the first rescan

**Discovered Controller Types (via DiscoverPanelTypes()):**
- `NavContentController` - Base class, lifecycle methods patched
- `HomePageContentController` - Inherits from NavContentController
- `SettingsMenu` - Open/Close methods + IsOpen setter patched
- `DeckSelectBlade` - Show/Hide methods + IsShowing setter patched
- `ConstructedDeckSelectController` - IsOpen getter only (no setter to patch)
- `DeckManagerController` - IsOpen getter only

## Input System

### Game's Built-in Keybinds (DO NOT OVERRIDE)
- Arrow keys: Navigation
- Tab / Shift+Tab: Next / Previous item
- Enter / Space: Accept / Submit
- Escape: Cancel / Back
- F: Find
- Alt (hold): Alt view (card details)

### Safe Custom Shortcuts
Your Zones (Battle): C (Hand/Cards), B (Battlefield), G (Graveyard), X (Exile), S (Stack)
Opponent Zones: Shift+G (Graveyard), Shift+X (Exile)
Information: T (Turn/phase), L (Life totals), A (Your Mana), Shift+A (Opponent Mana)
Card Details: Arrow Up/Down when focused on a card
Global: F1 (Help), F2 (Context info), Ctrl+R (Repeat last)

### Keyboard Manager Architecture
- `MTGA.KeyboardManager.KeyboardManager`: Central keyboard handling
- Priority levels: Debug > SystemMessage > SettingsMenu > DuelScene > Wrapper > NPE

## UI Interaction Patterns

### Critical: Toggle Behavior
**Problem:** `EventSystem.SetSelectedGameObject(toggleElement)` triggers the toggle, changing its state.

**Solution:** Never call SetSelectedGameObject for Toggle components:
```csharp
var toggle = element.GetComponent<Toggle>();
if (toggle == null)
{
    EventSystem.current?.SetSelectedGameObject(element);
}
```

### CustomButton Pattern
- Game uses `CustomButton` component (not Unity's standard `Button`)
- CustomButton responds to pointer events but NOT `onClick.Invoke()`
- Use `UIActivator.Activate()` which handles this automatically

**Known Issue - Multiple Presses Required:**
Some CustomButtons (like HomeBanner buttons in Play menu) may require multiple Enter presses
to activate. This is because `UIActivator.Activate()` falls through to `SimulatePointerClick()`
for CustomButtons, which sends pointer enter/down/up/click events. This approach may not work
reliably for all CustomButton variants.

**Potential Future Fix:**
If activation becomes unreliable, consider adding direct CustomButton onClick invocation via
reflection (similar to the fallback in `SimulateScreenCenterClick()`):
```csharp
var customButton = GetCustomButton(element);
var onClickField = customButton.GetType().GetField("onClick", BindingFlags...);
var onClick = onClickField.GetValue(customButton);
onClick.GetType().GetMethod("Invoke").Invoke(onClick, null);
```
This is NOT currently implemented in `UIActivator.Activate()` because pointer simulation
works for most buttons. Add it if consistent issues arise with specific button types.

### StyledButton Pattern
- Used for prompt buttons (Continue, Cancel) in pre-battle and duel screens
- Inherits from `Selectable` (found via `FindObjectsOfType<Selectable>()`)
- Implements `IPointerClickHandler` - use `UIActivator.SimulatePointerClick()` directly
- Does NOT respond to `onClick.Invoke()` or reflection-based method calls

### Input Field Text
- Empty fields contain zero-width space (U+200B), not empty string
- Always check for TMP_InputField BEFORE checking TMP_Text children
- Password fields: announce character count, not content

### EventSystem Limitations
- `EventSystem.currentSelectedGameObject` is often null in MTGA
- Most screens use CustomButton/EventTrigger which don't register with EventSystem
- UIFocusTracker's OnFocusChanged event rarely fires due to this
- Navigation is handled by custom Navigator classes (EventTriggerNavigator, etc.)
- Card navigation preparation must happen in navigators, not via focus events

## Using Mod Utilities

### UIActivator (Always Use for Activation)
Handles all element types automatically:
```csharp
var result = UIActivator.Activate(element);
// result.Success, result.Message, result.Type
```

Handles: Button, Toggle, TMP_InputField, InputField, CustomButton (via pointer simulation)

**Card Playing from Hand:**
```csharp
UIActivator.PlayCardViaTwoClick(card, (success, message) =>
{
    if (success)
        announcer.Announce($"Played {cardName}", AnnouncementPriority.Normal);
    else
        announcer.Announce($"Could not play {cardName}", AnnouncementPriority.High);
});
```

Uses double-click + center click approach (see `docs/CARD_PLAY_IMPLEMENTATION.md`).

### UITextExtractor
Extracts text and detects element types:
```csharp
string text = UITextExtractor.GetText(element);
string type = UITextExtractor.GetElementType(element); // "button", "card", etc.
```

**Button Text Extraction (use for buttons):**
```csharp
// Searches ALL TMP_Text children (including inactive), skips icons
string label = UITextExtractor.GetButtonText(buttonObj, "Fallback");
```
Use `GetButtonText()` instead of `GetText()` for buttons because:
- Searches all text children, not just the first
- Includes inactive children (important for MTGA's button structure)
- Skips single-character content (often icons)
- Handles zero-width spaces automatically

**Text Cleaning (public utility):**
```csharp
string clean = UITextExtractor.CleanText(rawText);
```
Removes: zero-width spaces (`\u200B`), rich text tags, normalizes whitespace.

**Element Type Fallback:**
`GetElementType()` returns "item" when no specific type is detected. This is the default fallback - check for it if you need to handle unknown elements specially.

### CardDetector
Universal card detection (cached for performance):
```csharp
bool isCard = CardDetector.IsCard(element);
GameObject root = CardDetector.GetCardRoot(element);
CardInfo info = CardDetector.ExtractCardInfo(element);
List<CardInfoBlock> blocks = CardDetector.GetInfoBlocks(element);
CardDetector.ClearCache(); // Call on scene change
```

**Detection Priority** (fast to slow):
1. Object name patterns: CardAnchor, NPERewardPrefab_IndividualCard, MetaCardView, CDC #
2. Parent name patterns (one level up)
3. Component names: BoosterMetaCardView, RewardDisplayCard, Meta_CDC, CardView

### CardInfoNavigator
Handles Arrow Up/Down navigation through card info blocks.

**Automatic Activation:** Card navigation activates automatically when Tab focuses a card.
No Enter required - just press Arrow Down to hear card details.

**Lazy Loading:** For performance, card info is NOT extracted when focus changes.
Info blocks are only loaded on first Arrow press. This allows fast Tab navigation
through many cards without performance impact.

**Manual Activation (legacy):**
```csharp
MTGAAccessibilityMod.Instance.ActivateCardDetails(element);
```

**Preparing for a card (used by navigators):**
```csharp
MTGAAccessibilityMod.Instance.CardNavigator.PrepareForCard(element);
```

Info block order: Name, Mana Cost, Type, Power/Toughness, Rules, Flavor, Artist

### DuelAnnouncer (Work in Progress)
Announces game events via Harmony patch on `UXEventQueue.EnqueuePending()`.

**Working Announcements:**
- Turn changes: "Turn X. Your turn" / "Turn X. Opponent's turn"
- Card draws: "Drew X card(s)" / "Opponent drew X card(s)"
- Spell resolution: "Spell resolved" (when stack empties)

**Needs Cleanup/Fixes:**
- Phase announcements (field names need investigation)
- Combat announcements
- Opponent play detection
- Remove debug logging

**Privacy Protection:**
- NEVER reveals opponent's hidden info (hand contents, library)
- Only announces publicly visible information
- Opponent draws: "Opponent drew a card" (not card name)

**Integration:**
```csharp
// Created by DuelNavigator
_duelAnnouncer = new DuelAnnouncer(announcer);

// Activated when duel starts
_duelAnnouncer.Activate(localPlayerId);

// Receives events via Harmony patch automatically
// UXEventQueuePatch.EnqueuePendingPostfix() calls:
DuelAnnouncer.Instance?.OnGameEvent(uxEvent);
```

### TargetNavigator
Handles target selection when playing spells that require targets. Detects valid targets via HotHighlight child objects.

**User Flow:**
1. Play targeted spell from hand (Enter)
2. "Select a target. N valid targets" announced
3. Tab cycles through valid targets
4. Enter selects current target
5. Escape cancels targeting

**Key Discovery - HotHighlight Detection:**
The game uses `HotHighlight` child objects to visually indicate valid targets:
- Valid targets: `Highlights - Default(Clone)` → `HotHighlightBattlefield(Clone)` (ACTIVE)
- Non-targets: `Highlights - Default(Clone)` with no HotHighlight child
- The spell on stack has `_GlowColor` green glow (NOT the targeting indicator)

**Integration (via UIActivator):**
```csharp
// UIActivator.PlayCardViaTwoClick checks for targeting mode after playing
bool needsTargeting = IsTargetingModeActive(); // Checks for Submit/Cancel buttons
if (needsTargeting)
{
    targetNavigator.EnterTargetMode();
}
```

**Target Discovery:**
```csharp
// TargetNavigator.HasTargetingHighlight()
foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
{
    if (child.gameObject.activeInHierarchy && child.name.Contains("HotHighlight"))
        return true; // This card is a valid target
}
```

### HighlightNavigator
Handles Tab cycling through playable/highlighted cards during normal gameplay. Replaces the default Tab behavior that cycles through UI buttons.

**How It Works:**
The game uses `HotHighlight` child objects to visually indicate playable cards (same system used for targeting). HighlightNavigator detects these and allows Tab navigation through them.

**User Flow:**
1. During your turn, press Tab
2. "Card Name, in hand, 1 of 3 playable" announced
3. Tab/Shift+Tab cycles through all playable cards
4. Enter plays/activates the current card
5. Selection resets after activation

**Input Priority (in DuelNavigator):**
```
TargetNavigator    → Tab during targeting mode (spell targets)
HighlightNavigator → Tab during normal play (playable cards)
ZoneNavigator      → Zone shortcuts (C/B/G/X/S) and arrows
Base behavior      → Fallback (rarely reached)
```

**Key Methods:**
```csharp
highlightNavigator.Activate();              // Enable on duel start
highlightNavigator.Deactivate();            // Disable on scene change
highlightNavigator.HandleInput();           // Process Tab/Enter
```

**Detection Logic:**
```csharp
// Scans hand and battlefield for cards with active HotHighlight
private bool HasHotHighlight(GameObject card)
{
    foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
    {
        if (child.gameObject.activeInHierarchy && child.name.Contains("HotHighlight"))
            return true;
    }
    return false;
}
```

**Integration with DuelNavigator:**
```csharp
// In DuelNavigator.HandleCustomInput()
if (_targetNavigator.HandleInput())     // Targeting takes priority
    return true;
if (_highlightNavigator.HandleInput())  // Then playable card cycling
    return true;
if (_zoneNavigator.HandleInput())       // Then zone navigation
    return true;
```

### ZoneNavigator
Handles zone navigation in DuelScene. Separate service following same pattern as CardInfoNavigator.

**Zone Shortcuts:**
- C - Your hand (Cards)
- B - Battlefield
- G - Your graveyard
- X - Exile
- S - Stack
- Shift+G - Opponent graveyard
- Shift+X - Opponent exile

**Card Navigation within Zones:**
- Left Arrow - Previous card in current zone
- Right Arrow - Next card in current zone
- Enter - Play/activate current card (uses PlayCardViaTwoClick for hand cards)

**Usage (via DuelNavigator):**
```csharp
// DuelNavigator creates and owns ZoneNavigator
_zoneNavigator = new ZoneNavigator(announcer);

// In HandleCustomInput, delegate to ZoneNavigator
if (_zoneNavigator.HandleInput())
    return true;
```

**Key Methods:**
```csharp
zoneNavigator.Activate();           // Discover zones on duel start
zoneNavigator.NavigateToZone(zone); // Jump to zone with shortcut
zoneNavigator.NextCard();           // Navigate within zone
zoneNavigator.GetCurrentCard();     // Get current card for CardInfoNavigator
zoneNavigator.ActivateCurrentCard(); // Play card (hand) or activate (battlefield)
```

**EventSystem Conflict Resolution:**
Arrow keys also trigger Unity's EventSystem navigation, causing focus cycling between UI buttons.
ZoneNavigator clears EventSystem selection before handling arrow keys:
```csharp
private void ClearEventSystemSelection()
{
    var eventSystem = EventSystem.current;
    if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
    {
        eventSystem.SetSelectedGameObject(null);
    }
}
```

## Navigator Patterns

### When to Create a Navigator
- EventSystem doesn't work on the screen
- Screen has special activation requirements (like NPE chest)
- Screen needs custom Tab order

### Creating a New Navigator (BaseNavigator Pattern)
New navigators should extend `BaseNavigator` for consistency and reduced duplication.

1. Create a class extending `BaseNavigator`:
```csharp
public class MyScreenNavigator : BaseNavigator
{
    public override string NavigatorId => "MyScreen";
    public override string ScreenName => "My Screen";
    public override int Priority => 50; // Higher = checked first

    public MyScreenNavigator(IAnnouncementService announcer) : base(announcer) { }

    protected override bool DetectScreen()
    {
        // Return true if this screen is currently displayed
        var panel = GameObject.Find("MyPanel(Clone)");
        return panel != null && panel.activeInHierarchy;
    }

    protected override void DiscoverElements()
    {
        // Use helper methods to populate _elements list
        AddButton(FindChildByName(panel, "Button1"), "Button 1");
        AddToggle(toggle, "Checkbox label");
        AddInputField(inputField, "Email");
    }
}
```

2. Register in `MTGAAccessibilityMod.InitializeServices()`:
```csharp
_navigatorManager.RegisterAll(
    new MyScreenNavigator(_announcer),
    // ... other navigators
);
```

### BaseNavigator Data Structure
Elements are stored in a single list using the `NavigableElement` struct:
```csharp
protected struct NavigableElement
{
    public GameObject GameObject;  // The UI element
    public string Label;           // Announcement text
    public CarouselInfo Carousel;  // Arrow key navigation info (optional)
}
```
Access via `_elements[index].GameObject`, `_elements[index].Label`, etc.

### BaseNavigator Features
- **Common input handling**: Tab/Shift+Tab/Enter/Space built-in
- **Card navigation integration**: Automatic PrepareForCard() calls
- **Helper methods**: AddButton(), AddToggle(), AddInputField(), FindChildByName(), GetButtonText()
- **Override points**:
  - `HandleCustomInput()` - Add custom keys (return true if handled)
  - `OnElementActivated()` - Special activation logic (return true if handled)
  - `OnActivated()` / `OnDeactivating()` - Lifecycle hooks
  - `GetActivationAnnouncement()` - Custom screen announcement
  - `ValidateElements()` - Custom element validity check
  - `AcceptSpaceKey` - Whether Space triggers activation (default: true)
  - `SupportsCardNavigation` - Whether to integrate with CardInfoNavigator

### Special Activation Cases
Some elements (NPE chest/deck boxes) need controller reflection:
- Find controller via `GameObject.FindObjectOfType<NPEContentControllerRewards>()`
- Call methods like `Coroutine_UnlockAnimation()`, `OnClaimClicked_Unity()`
- See `EventTriggerNavigator.HandleSpecialNPEElement()` for example

## Card Handling in Navigators

### Automatic Card Navigation on Tab
When Tab changes the current element, prepare card navigation:
```csharp
private void PrepareCardNavigationForCurrentElement()
{
    var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
    if (cardNavigator == null) return;

    var element = _elements[_currentIndex].GameObject;
    if (element != null && CardDetector.IsCard(element))
    {
        // Prepare (lazy) - info extracted only when user presses Arrow
        cardNavigator.PrepareForCard(element);
    }
    else if (cardNavigator.IsActive)
    {
        // Not a card - deactivate card navigation
        cardNavigator.Deactivate();
    }
}
```

Call this method:
1. After Tab changes `_currentIndex`
2. In `FinalizeActivation()` for the initial element

### Manual Card Activation on Enter (legacy)
When activating an element with Enter:
```csharp
private void ActivateElement(int index)
{
    var element = _elements[index].GameObject;

    // Check if card - delegate to central CardInfoNavigator
    if (CardDetector.IsCard(element))
    {
        if (MTGAAccessibilityMod.Instance.ActivateCardDetails(element))
            return;
    }

    // Not a card - normal activation
    UIActivator.Activate(element);
}
```

## Debugging Tips

1. **Scan for panel by name:** `GameObject.Find("Panel - Name(Clone)")`
2. **Check EventSystem:** Log `currentSelectedGameObject` on Tab
3. **Identify element types:** Button vs CustomButton vs Image
4. **Test Toggle behavior:** Does selecting trigger state change?
5. **Find elements by path:** More reliable than name search
6. **Log all components:** On problematic elements to understand structure

## Common Gotchas

- MelonGame attribute is case sensitive: `"Wizards Of The Coast"`
- NPE reward chest/deck boxes need controller reflection, not pointer events
- Mana costs use sprite tags (`<sprite name="xW">`) - parse with regex for symbol names
- ManaCost text elements need special handling: CleanText() strips all tags leaving empty content,
  so skip the empty-content check for ManaCost to allow ParseManaCost() to process raw sprite tags
- CardDetector cache must be cleared on scene changes (stale references)
- CustomButton.OnClick may have 0 listeners - direct invocation does nothing
- EventSystem.currentSelectedGameObject is often null - game uses custom navigation
- Card navigation must be prepared by navigators (EventTriggerNavigator), not UIFocusTracker
- CardInfoNavigator uses lazy loading - PrepareForCard() is fast, LoadBlocks() extracts info
