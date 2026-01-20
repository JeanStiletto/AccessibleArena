# MTGA Menu Navigation System

## Overview

This document describes how MTGA handles menus and how the accessibility mod navigates them.

---

## Current Status

### Working Features
- Settings Menu with submenus (Gameplay, Graphics, Audio)
- Escape key closes Settings
- Main Menu navigation (NavBar + HomePage elements)
- Instance ID-based duplicate prevention
- Panel state cleanup on deactivation
- Carousel navigation with Left/Right arrows
- Element filtering (badges, hitboxes, progress indicators)

### Known Issues

1. **Multiple Announcements on Load**
   - Menu announces multiple times as panels load sequentially
   - NavBar loads first, then HomePage triggers rescan

2. **Unclear Labels**
   - Some NavBar items use internal names ("nav wild card" instead of "Wildcards")

3. **Element Discovery Limitations**
   - Each new element type needs explicit discovery code
   - Could be improved with `Selectable` base class approach

---

## UIElementClassifier Features

### UITextExtractor.cs

**HasActualText() method:**
Checks if a GameObject has real text content vs just object name fallback. Used to filter elements that only have internal names (like "Backer_Hitbox").

### UIElementClassifier.cs

**Element Filtering:**
- Hitbox elements without real text are filtered
- Backer elements (social panel hitboxes) are filtered
- Social corner icons are filtered
- Numeric notification badges are filtered (like "22" unread mail)
- Progress indicators are non-navigable (informational only)

**Carousel Detection:**
ClassificationResult includes carousel properties:
- `HasArrowNavigation` - whether element supports left/right arrows
- `PreviousControl` / `NextControl` - nav control references

Detection methods:
- `IsCarouselNavControl()` - detects NavLeft/NavRight gradient controls (hidden from Tab)
- `IsCarouselElement()` - detects elements with nav controls as children

Carousel elements announced with role: "carousel, use left and right arrows"

### BaseNavigator.cs

**Data Structure:**
BaseNavigator uses a single `List<NavigableElement>` where each element contains GameObject, Label, and optional CarouselInfo. See `BEST_PRACTICES.md` for details.

**Arrow key handling for carousels:**
```csharp
if (Input.GetKeyDown(KeyCode.LeftArrow))
    if (HandleCarouselArrow(isNext: false)) return;
if (Input.GetKeyDown(KeyCode.RightArrow))
    if (HandleCarouselArrow(isNext: true)) return;
```

`HandleCarouselArrow()` activates the nav control and announces the updated content.

### GeneralMenuNavigator.cs

**DiscoverElements:**
Passes CarouselInfo when adding elements that support arrow navigation.

### CodeOfConductNavigator.cs

**Fixed: Incorrect Activation on Settings Submenus**

Problem: CodeOfConductNavigator was activating whenever it detected 2+ toggles, including Settings submenus like Gameplay (which has 8 toggle checkboxes).

Fix: Added `IsSettingsMenuOpen()` check in `DetectScreen()`:
```csharp
protected override bool DetectScreen()
{
    // Skip if Settings menu is open - GeneralMenuNavigator handles Settings submenus
    if (IsSettingsMenuOpen()) return false;

    // Detect by presence of multiple toggles
    var toggles = FindValidToggles();
    return toggles.Count >= 2;
}
```

### GeneralMenuNavigator.cs

**Fixed: Panel State Not Cleared on Deactivation**

Problem: After clicking BackButton in Settings, `_foregroundPanel` remained set to the now-closed "Content - MainMenu" panel. When the navigator tried to reactivate, it filtered all elements to this closed panel, finding 0 elements.

Symptoms in log:
```
[GeneralMenu] Filtering to panel: Content - MainMenu
[GeneralMenu] Discovered 0 navigable elements
[GeneralMenu] DetectScreen passed but no elements found
(repeats indefinitely)
```

Fix 1: Added `OnDeactivating()` override to clear panel state:
```csharp
protected override void OnDeactivating()
{
    base.OnDeactivating();
    _foregroundPanel = null;
    _activePanels.Clear();
}
```

Fix 2: Clear `_foregroundPanel` on scene change in `OnSceneChanged()`.

Fix 3: Validation in `CheckForPanelChanges()` detects when foreground panel becomes inactive.

---

## Navigator Lifecycle Lessons Learned

### Panel State Must Be Cleaned Up

**Problem Pattern:** Class-level state (like `_foregroundPanel`) persists across activation/deactivation cycles. If not cleared, stale references cause filtering to closed panels.

**Solution:** Always clean up panel/overlay state in:
1. `OnDeactivating()` - when navigator is deactivating
2. `OnSceneChanged()` - when scene changes
3. `CheckForPanelChanges()` - detects when panels become inactive

### Special Screen Navigators Must Exclude Other Contexts

**Problem Pattern:** Navigators that detect by generic patterns (e.g., "2+ toggles") can incorrectly activate in unrelated contexts.

**Solution:** Add exclusion checks for known contexts:
```csharp
protected override bool DetectScreen()
{
    // Exclude known contexts handled by other navigators
    if (IsSettingsMenuOpen()) return false;
    if (IsDuelActive()) return false;

    // Then do generic detection
    return HasExpectedElements();
}
```

### Navigator Priority and Activation Order

Navigators are checked in priority order (highest first):
1. WelcomeGate (100) - Login choice screen
2. LoginPanel (90) - Email/password entry
3. Overlay (85) - Modal popups
4. Duel (70) - In-game combat
5. CodeOfConduct (50) - Terms/consent screens
6. GeneralMenu (15) - Fallback menu navigator
7. EventTrigger (10) - Fallback for non-Selectable screens

**Key insight:** Higher priority navigators should have very specific detection. Lower priority navigators act as fallbacks.

### Deactivation vs Rescan Decision

When a button is clicked, the navigator must decide:
- **Deactivate fully** - Context is changing completely (leave menu)
- **Stay active + rescan** - Context is changing within same system (submenu)

Current logic in `OnElementActivated()`:
```csharp
// Uses centralized IsInSettingsMenu property (caches panel reference)
if (IsInSettingsMenu)
{
    // Set foreground panel to filter to Settings elements only
    _foregroundPanel = SettingsContentPanel;
    // Stay active, trigger rescan for submenu navigation
    TriggerRescan();
}
else
{
    // Check for other state changes, or schedule post-activation check
}
```

---

## Key Implementation Details

### BaseNavigator.cs

**Duplicate Prevention:**
Uses `GetInstanceID()` for reliable duplicate detection when adding elements. This is more reliable than `Contains()` for Unity objects.

### GeneralMenuNavigator.cs

**Two-Pass Panel Detection:**
- Pass 1: Find all open menu controllers via reflection
- Pass 2: Apply priority filtering (SettingsMenu > NavContentController)
- When SettingsMenu is open, skip other controllers to show only Settings items

**Animation State Checking:**
- Checks `IsReadyToShow` property for NavContentController descendants
- Checks `IsMainPanelActive` property for SettingsMenu
- Prevents activation before panel animations complete

**Overlay Panel Filtering:**
- Only applies foreground filtering for OVERLAY panels (Settings, Popups)
- For normal main menu, `_foregroundPanel = null` means no filtering
- Fixes issue where NavBar was excluded when filtering to HomePage

**Rescan Debouncing:**
```csharp
private float _lastRescanTime = 0f;
private const float RESCAN_DEBOUNCE_SECONDS = 1.0f;

private void PerformRescan()
{
    float currentTime = Time.time;
    if (currentTime - _lastRescanTime < RESCAN_DEBOUNCE_SECONDS)
    {
        MelonLogger.Msg($"[{NavigatorId}] Skipping rescan - debounce active");
        return;
    }
    _lastRescanTime = currentTime;
    // ... rest of rescan
}
```

### UIElementClassifier.cs

**Game Property Checks for Interactability**
```csharp
public static bool IsCustomButtonInteractable(GameObject obj)
{
    var customButton = GetCustomButton(obj);
    if (customButton == null) return true;

    // Check Interactable property
    var interactableProp = type.GetProperty("Interactable", ...);
    if (interactableProp != null)
    {
        bool interactable = (bool)interactableProp.GetValue(customButton);
        if (!interactable) return false;
    }

    // Check IsHidden() method (CustomButtonWithTooltip)
    var isHiddenMethod = type.GetMethod("IsHidden", ...);
    if (isHiddenMethod != null)
    {
        bool isHidden = (bool)isHiddenMethod.Invoke(customButton, null);
        if (isHidden) return false;
    }
    return true;
}

public static bool IsVisibleViaCanvasGroup(GameObject obj)
{
    var canvasGroup = obj.GetComponent<CanvasGroup>();
    if (canvasGroup != null)
    {
        if (canvasGroup.alpha < 0.1f) return false;
        if (!canvasGroup.interactable) return false;
    }
    // Also checks parent CanvasGroups
    return true;
}
```

---

## Game Architecture Learnings

### Menu Hierarchy (Confirmed)

```
Scene: NavBar (always visible, loads first)
└── NavBarController (NOT a NavContentController!)
    ├── Nav_Home, Nav_Profile, Nav_Decks, Nav_Packs, Nav_Store, Nav_Mastery
    ├── Achievements (only after HomePage loads)
    ├── Nav_WildCard, Nav_Coins, Nav_Gems
    ├── Nav_DeckbuilderLayout (only before HomePage loads)
    ├── Nav_DirectChallenge, Nav_Settings, Nav_Learn
    └── MainButtonOutline (Return to Arena - EventTrigger)

Scene: HomePage (content area, loads ~6 seconds after NavBar)
└── HomePageContentController (inherits NavContentController)
    ├── Play button, Bot Match
    ├── Color Challenge, Welcome Bundle (event blades)
    ├── Previous/Next (carousel navigation)
    ├── Objectives panel (quest progress)
    └── Social Corner Icon

PlayBlade (slides in when Play/Bot Match clicked)
└── PlayBladeController
    ├── PlayBladeVisualState (Hidden/Events/DirectChallenge/FriendChallenge)
    ├── EventBladeContentView - Game modes (Ranked, Play, Brawl)
    ├── FindMatchBladeContentView - Deck selection + Find Match
    │   ├── Deck folder toggles (My Decks, Starter Decks)
    │   └── Deck entries (DeckView_Base with UI button + TextBox)
    └── LastPlayedBladeContentView - Quick replay

Deck Entry Structure (MetaDeckView):
└── DeckView_Base(Clone)
    └── UI (CustomButton) ← Select deck (Enter)
        └── TextBox ← Edit name (Shift+Enter)

Overlay: SettingsMenu (modal)
├── Controller: SettingsMenu_Desktop_16x9(Clone)
└── Content panel: "Content - MainMenu" (where buttons actually are)
    ├── back button, Gameplay, Graphics, Audio
    ├── Exit Game, Log Out, Account
    └── Privacy Policy, Report a Bug, Your Privacy Choices
```

### Key Properties Discovered

**NavContentController**
- `IsOpen` (bool property) - Whether panel is currently open
- `IsReadyToShow` (bool property) - Whether animations are complete and content is ready

**SettingsMenu**
- `IsOpen` (bool property) - Whether settings panel is open
- `IsMainPanelActive` (bool property) - Whether on main settings vs submenu

**CustomButton**
- `Interactable` (bool property) - Whether button can be clicked

**CustomButtonWithTooltip**
- `IsHidden()` (method) - Whether button is visually hidden

**CanvasGroup**
- `alpha` - Visibility (< 0.1 = hidden)
- `interactable` - Whether children receive input

### Load Sequence

1. Bootstrap scene loads
2. AssetPrep scene loads (~4 seconds)
3. MainNavigation scene loads
4. NavBar scene loads - **First menu activation (13 items)**
5. HomePage scene loads (~6 seconds later) - **Rescan triggered (22 items)**
6. HomePageContentController.IsOpen becomes true
7. HomePageContentController.IsReadyToShow becomes true

### Panel Priority

When multiple panels have IsOpen=true:
1. SettingsMenu (highest) - Modal overlay
2. PopupBase - Dialogs
3. NavContentController descendants (lowest) - Content screens

---

## Log Messages Reference

### Successful Flow
```
[GeneralMenu] Detected menu: Store with 13 CustomButtons
[GeneralMenu] Added (ID:-2940): Home, button
[GeneralMenu] Activated with 13 elements
[GeneralMenu] Panel opened: HomePageContentController:...
[GeneralMenu] Rescanning elements after panel change
[GeneralMenu] Discovered 22 navigable elements
```

### Settings Flow
```
[GeneralMenu] Panel opened: SettingsMenu:SettingsMenu_Desktop_16x9(Clone)
[GeneralMenu] Filtering to panel: Content - MainMenu
[GeneralMenu] Added (ID:-37496): back button, button
[GeneralMenu] Discovered 10 navigable elements
[GeneralMenu] Foreground panel became inactive: Content - MainMenu
[GeneralMenu] Rescanning elements after panel change
```

### Duplicate Prevention
```
[GeneralMenu] Added (ID:-12345): Home, button
[GeneralMenu] Duplicate skipped (ID:-12345): Home, button
```

### Debounce Active
```
[GeneralMenu] Skipping rescan - debounce active
```

---

## Debug Commands

```powershell
# Read last 300 lines of log
Get-Content 'C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log' -Tail 300

# Watch log in real-time
Get-Content 'C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log' -Tail 50 -Wait

# Build mod
dotnet build "C:\Users\fabia\arena\src\AccessibleArena.csproj"

# Deploy mod (game must be closed)
Copy-Item -Path 'C:\Users\fabia\arena\src\bin\Debug\net472\AccessibleArena.dll' -Destination 'C:\Program Files\Wizards of the Coast\MTGA\Mods\AccessibleArena.dll' -Force
```

---

## PlayBlade Navigation

### Overview

The PlayBlade is the sliding panel that appears when clicking Play or Bot Match on the HomePage.
It contains game mode selection and deck selection.

### Key Learnings

**Play and Bot Match are separate buttons with different behavior:**
- **Play button** → Opens PlayBlade with Events/game mode selection → Then deck selection
- **Bot Match button** → Calls `Coroutine_PlayBotGame` → Opens deck selection directly

However, both eventually show `FindMatchBladeContentView` for deck selection.

**Deck selection in PlayBlade does NOT use DeckSelectBlade.Show():**
The deck list is embedded directly in the blade views. This is why the existing
`DeckSelectBlade` patches don't fire when using the Play button.

### Harmony Patches Required

To properly detect PlayBlade state changes, these patches were added:
- `PlayBladeController.PlayBladeVisualState` setter
- `PlayBladeController.IsDeckSelected` setter
- `HomePageContentController.IsEventBladeActive` setter
- `HomePageContentController.IsDirectChallengeBladeActive` setter
- `BladeContentView.Show/Hide` (base class)
- `EventBladeContentView.Show/Hide`

### Element Filtering

When PlayBlade is active (`_playBladeActive = true`):
- Elements inside Blade hierarchies are shown
- Elements inside HomePage but outside Blades are hidden
- This prevents duplicate buttons (e.g., two "Bot Match" buttons)

### Deck Entry Handling

Each deck entry has two interactive elements:
1. **UI** (CustomButton) - Main selection, parent: `DeckView_Base(Clone)`
2. **TextBox** - Edit name area, child of UI

The mod pairs these elements:
- Only UI is added to navigation list
- TextBox stored as `AlternateActionObject`
- **Enter** activates UI (select deck)
- **Shift+Enter** activates TextBox (edit deck name)

### Deck Name Extraction

Deck names are stored in `TMP_InputField.text` but the visual placeholder shows
"Enter deck name...". The `TryGetDeckName()` method:
1. Walks up hierarchy to find `DeckView_Base` or `Blade_ListItem`
2. Gets `TMP_InputField` children
3. Reads `.text` property (not placeholder)
4. Returns actual deck name with ", deck" suffix

---

## References

- Game assemblies: `C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed`
- Mod source: `src/Core/Services/*Navigator.cs`
- BaseNavigator: `src/Core/Services/BaseNavigator.cs`
- UIElementClassifier: `src/Core/Services/UIElementClassifier.cs`
