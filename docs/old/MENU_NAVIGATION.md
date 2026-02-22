# MTGA Menu Navigation System

## Overview

This document describes how MTGA handles menus and how the accessibility mod navigates them.

---

## Current Status (January 2026)

### Working Features
- **Settings Menu**: Opens correctly with 10 items (Gameplay, Graphics, Audio, Exit Game, Log Out, Account, Privacy Policy, Report a Bug, Your Privacy Choices, back button)
- **Settings Back Button**: Works correctly, returns to main menu
- **Settings Submenus**: Gameplay, Graphics, Audio submenus navigate correctly
- **Escape Key**: Properly closes Settings menu with single press
- **Main Menu**: Shows NavBar + HomePage elements (22-23 items) with unique items
- **Duplicate Prevention**: Instance ID-based duplicate detection working
- **Panel State Cleanup**: Navigator properly clears panel filters on deactivation

### Known Issues

1. **Multiple Announcements on Load**
   - Menu announces 3 times as it loads: ~7 items, 13 items, 22 items
   - Cause: NavBar loads first, then HomePage loads and triggers rescan
   - Not a bug per se, but could be improved with longer initial wait

2. **Unclear Labels**
   - "nav wild card, button" - should be "Wildcards"
   - "nav direct challenge, button" - should be "Direct Challenge"
   - "nav settings, button" - should be "Settings"
   - "nav learn, button" - should be "Learn"

3. **Screen Recognition Needs More Work**
   - Current approach relies on detecting UI element types (CustomButton, Toggle, Slider, etc.)
   - Each new element type requires explicit discovery code in `DiscoverElements()`
   - A more generalized approach using `Selectable` base class could simplify this
   - Panel/overlay detection works but edge cases may exist for uncommon menus

### Fixed Issues (January 11, 2026)

1. **Non-useful Items Now Filtered**
   - Notification badge numbers (e.g., "22" from Nav_Mail) - now filtered
   - Progress indicators ("objective graphics", "1") - now non-navigable
   - Social panel hitboxes (Backer_Hitbox elements) - now filtered
   - Social corner icon - now filtered

2. **Carousel Navigation Improved**
   - Promotional banners now detected as carousel elements
   - Previous/Next buttons hidden from Tab navigation (internal controls)
   - Left/Right arrow keys cycle carousel content when focused on a carousel item
   - Carousel elements announced with "carousel, use left and right arrows"
   - Generic detection works for any element with NavLeft/NavRight child controls

---

## Recent Code Changes (January 12, 2026)

### GeneralMenuNavigator.cs - Element Discovery Improvements

**Added Toggle and Slider discovery:**
- Settings submenus (Gameplay, Audio) use Toggle checkboxes and Slider volume controls
- Previously only CustomButton, Button, and EventTrigger were discovered
- Added explicit discovery loops for `Toggle` and `Slider` components
- Audio menu now correctly shows: back button, Play Audio While Minimized toggle, 5 volume sliders
- Gameplay menu now correctly shows all toggle options

**Generalized post-activation rescan:**
- `OnElementActivated()` now always schedules a post-activation check
- Works for all menus, not just Settings-specific code
- Detects panel changes via `CheckForPanelChanges()` which handles:
  - New panels opening
  - Panels closing
  - Panel transitions within overlays (e.g., Settings main -> Gameplay submenu)

### UIElementClassifier.cs - Filter Improvements

**Removed overly broad "background" filter:**
- Old filter: `if (ContainsIgnoreCase(name, "background")) return true`
- This accidentally filtered legitimate UI elements like "BackgroundAudio" toggle
- Now only filters specific patterns like "blocker" for modal overlays
- Principle: Filter by behavior/purpose, not generic name patterns

### GeneralMenuNavigator.cs - Code Cleanup

**Removed dead code:**
- `CleanButtonName()` method - was never called
- `GetElementText()` method - duplicated `UITextExtractor.GetText()` functionality

**Added centralized Settings detection:**
```csharp
// Cached Settings panel reference (refreshed each check)
private GameObject _settingsContentPanel;

protected bool IsInSettingsMenu
{
    get
    {
        _settingsContentPanel = GameObject.Find("Content - MainMenu");
        return _settingsContentPanel != null && _settingsContentPanel.activeInHierarchy;
    }
}

protected GameObject SettingsContentPanel => _settingsContentPanel;
```

This replaces scattered `GameObject.Find("Content - MainMenu")` calls in:
- `GetMenuScreenName()`
- `GetActivePanelsWithObjects()`
- `OnElementActivated()`

**Removed duplicate foreground panel validation:**
- Was in both `CheckForPanelChanges()` and `DiscoverElements()`
- Now only in `CheckForPanelChanges()` (the proper place for panel state management)

**Consolidated `_hasLoggedUIOnce` resets:**
- Was set to `false` in 6 places
- Now only in 2 places:
  - `OnSceneChanged()` - for fresh scenes
  - `PerformRescan()` - central place for all rescans
- Other places called `TriggerRescan()` which leads to `PerformRescan()`

---

## Code Changes (January 11, 2026)

### UITextExtractor.cs

**Added: HasActualText() method** (lines 17-50)

New method to check if a GameObject has real text content vs just object name fallback:
```csharp
public static bool HasActualText(GameObject gameObject)
{
    // Checks TMP_InputField, InputField, TMP_Text, and Text components
    // Returns false if no real text found (would fall back to object name)
}
```

This enables proper filtering of elements that only have internal names (like "Backer_Hitbox").

### UIElementClassifier.cs

**Fixed: Hitbox Filter** (line 344)

Changed from checking `string.IsNullOrEmpty(text)` to using `UITextExtractor.HasActualText()`:
```csharp
// OLD: if (nameLower.Contains("hitbox") && string.IsNullOrEmpty(text)) return true;
// NEW:
if (nameLower.Contains("hitbox") && !UITextExtractor.HasActualText(obj)) return true;
```

The old check didn't work because `text` always had a value (object name fallback).

**Added: Backer Element Filter** (line 347)
```csharp
if (nameLower.Contains("backer") && !UITextExtractor.HasActualText(obj)) return true;
```

Filters social panel internal hitboxes like `Backer_Hitbox`.

**Added: Social Corner Icon Filter** (line 350)
```csharp
if (nameLower.Contains("socialcorner") || nameLower.Contains("social corner")) return true;
```

**Added: Numeric Badge Filter** (lines 364-369)
```csharp
if (nameLower.Contains("mail") || nameLower.Contains("notification") || nameLower.Contains("badge"))
{
    if (IsNumericOnly(text))
        return true;
}
```

Filters notification badges that show only a number (like "22" unread mail).

**Added: IsNumericOnly() Helper** (lines 386-398)
```csharp
private static bool IsNumericOnly(string text)
{
    // Returns true if text contains only digits
}
```

**Changed: Progress Indicators Non-Navigable** (lines 142-143)
```csharp
result.IsNavigable = false;  // Was true
result.ShouldAnnounce = false;  // Was true
```

Progress indicators are informational only, not meant for direct keyboard navigation.

**Added: Carousel Detection** (lines 182-197, 420-491)

ClassificationResult now includes carousel properties:
```csharp
public bool HasArrowNavigation { get; set; }
public GameObject PreviousControl { get; set; }
public GameObject NextControl { get; set; }
```

New methods for carousel detection:
- `IsCarouselNavControl()` - detects NavLeft/NavRight gradient controls (to hide them)
- `IsCarouselElement()` - detects elements with nav controls as children

Carousel elements announced with role: "carousel, use left and right arrows"

### BaseNavigator.cs

> **Note (Jan 2026 Refactor):** BaseNavigator was refactored to use a single `List<NavigableElement>`
> instead of parallel lists. The code snippets below are historical. See `BEST_PRACTICES.md` for
> current structure.

**Added: CarouselInfo class and storage** (historical - now part of NavigableElement struct)
```csharp
// OLD: Three parallel lists
protected readonly List<CarouselInfo> _carouselInfo = new List<CarouselInfo>();

// NEW: Single list with NavigableElement struct containing GameObject, Label, and Carousel
protected readonly List<NavigableElement> _elements = new List<NavigableElement>();
```

**Added: Arrow key handling** (lines 241-252, 264-299)
```csharp
if (Input.GetKeyDown(KeyCode.LeftArrow))
    if (HandleCarouselArrow(isNext: false)) return;
if (Input.GetKeyDown(KeyCode.RightArrow))
    if (HandleCarouselArrow(isNext: true)) return;
```

`HandleCarouselArrow()` activates the nav control and announces the updated content.

**Updated: AddElement overload** (lines 358-379)

New overload accepts CarouselInfo parameter for elements with arrow navigation.

### GeneralMenuNavigator.cs

**Updated: DiscoverElements** (lines 708-720)

Now passes CarouselInfo when adding elements that support arrow navigation.

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
4. PreBattle (80) - VS screen before duel
5. Duel (70) - In-game combat
6. CodeOfConduct (50) - Terms/consent screens
7. GeneralMenu (15) - Fallback menu navigator
8. EventTrigger (10) - Fallback for non-Selectable screens

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

## Previous Code Changes

### BaseNavigator.cs

**Duplicate Prevention with Instance ID** (lines 341-361)
```csharp
protected void AddElement(GameObject element, string label)
{
    if (element == null) return;

    // Check by instance ID for reliable comparison
    int instanceId = element.GetInstanceID();
    foreach (var existing in _elements)
    {
        if (existing != null && existing.GetInstanceID() == instanceId)
        {
            MelonLogger.Msg($"[{NavigatorId}] Duplicate skipped (ID:{instanceId}): {label}");
            return;
        }
    }

    // OLD: Added to parallel lists
    _elements.Add(element);
    _labels.Add(label);

    // NEW: Single struct added to one list
    _elements.Add(new NavigableElement { GameObject = element, Label = label, Carousel = carouselInfo });
}
```
- Previously used `_elements.Contains(element)` which may not work reliably for Unity objects
- Now uses `GetInstanceID()` for definitive comparison
- Refactored to use single `NavigableElement` struct instead of parallel lists

### GeneralMenuNavigator.cs

**Fixed Missing _labels.Clear() in PerformRescan()** (historical - no longer needed after refactor)
```csharp
// OLD: Required clearing multiple parallel lists
_elements.Clear();
_labels.Clear();

// NEW: Single list to clear
_elements.Clear();
```

**Two-Pass Panel Detection** (lines 189-272)
- Pass 1: Find all open menu controllers via reflection
- Pass 2: Apply priority filtering (SettingsMenu > NavContentController)
- When SettingsMenu is open, skip other controllers to show only Settings items

**Animation State Checking** (lines 277-352)
- Checks `IsReadyToShow` property for NavContentController descendants
- Checks `IsMainPanelActive` property for SettingsMenu
- Prevents activation before panel animations complete

**Overlay Panel Filtering** (lines 358-380)
- Only applies foreground filtering for OVERLAY panels (Settings, Popups)
- For normal main menu, `_foregroundPanel = null` means no filtering
- Fixes issue where NavBar was excluded when filtering to HomePage

**Rescan Debouncing** (lines 393-402)
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

## Questions for Further Investigation

### Filtering Questions

1. **How to identify notification badges?**
   - "22, button" is a notification count, not a real button
   - Need pattern to distinguish badges from real buttons
   - Check parent hierarchy or specific component types?

2. **How to clean up "nav_" prefixes?**
   - Game uses "Nav_Settings" etc. as GameObject names
   - Text extraction returns "nav settings"
   - Should strip prefix or use different text source?

3. **What makes "objective graphics" and "1" navigable?**
   - These are progress indicators, not interactive
   - UIElementClassifier is not filtering them out
   - Need to check what component type they have

### Architecture Questions

4. **Is there a notification/badge manager?**
   - Could query it to know which buttons have badges
   - Would help announce "Settings (3 new)" etc.

5. **How does the game handle element focus order?**
   - Game might have internal tab order
   - Could use this instead of position-based sorting

6. **What triggers HomePage to load?**
   - Is it time-based or event-based?
   - Could we wait for a specific signal before first activation?

---

## Potential Improvements

### Short-term

1. **Filter notification badges**
   - Check if parent contains "Badge" or "Notification"
   - Check if text is purely numeric
   - Skip elements with very short numeric labels

2. **Clean up labels**
   - Strip "nav " prefix from labels
   - Map internal names to user-friendly names

3. **Reduce initial announcements**
   - Increase ACTIVATION_DELAY_SECONDS from 0.5 to 2.0
   - Wait for HomePage to load before first activation

### Medium-term

4. **Use IsReadyToShow more broadly**
   - Don't activate until all visible panels report IsReadyToShow=true
   - Would prevent intermediate states

5. **Add element type detection**
   - Distinguish buttons, badges, progress bars, decorations
   - Only navigate actual interactive elements

---

## Testing Checklist

### Main Menu
- [ ] Opens with stable element count (no multiple announcements)
- [ ] All items have meaningful labels
- [ ] No duplicate items
- [ ] No internal/decorative elements included

### Settings Menu
- [x] Opens with ~10 items
- [x] Shows correct items (Gameplay, Graphics, Audio, etc.)
- [x] Escape closes settings
- [x] Returns to main menu after close
- [x] Back button works (returns to main menu)
- [x] Submenus (Gameplay, Graphics, Audio) open correctly
- [x] CodeOfConductNavigator does NOT activate in Settings submenus

### Navigation
- [x] Tab cycles through all items
- [x] Shift+Tab goes backwards
- [x] Enter activates current item
- [ ] Position-based ordering makes sense

### Panel State Management
- [x] Navigator clears panel filter on deactivation
- [x] Navigator clears panel filter on scene change
- [x] Stale panel references don't block element discovery

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
dotnet build src/MTGAAccessibility.csproj

# Deploy mod (game must be closed)
Copy-Item -Path "$PWD\src\bin\Debug\net472\MTGAAccessibility.dll" -Destination 'C:\Program Files\Wizards of the Coast\MTGA\Mods\MTGAAccessibility.dll' -Force
```

---

## References

- Game assemblies: `C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed`
- Mod source: `src/Core/Services/*Navigator.cs`
- BaseNavigator: `src/Core/Services/BaseNavigator.cs`
- UIElementClassifier: `src/Core/Services/UIElementClassifier.cs`
