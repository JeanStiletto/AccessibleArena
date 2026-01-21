# BrowserNavigator Refactoring Plan

## Goal
Split BrowserNavigator.cs (~2465 lines) into 3 files following the established pattern:
- CardDetector (static, stateless) → **BrowserDetector**
- DuelNavigator (orchestrator) → **BrowserNavigator** (slimmed down)
- ZoneNavigator (zone state) → **BrowserZoneNavigator**

---

## File 1: BrowserDetector.cs (~200 lines)

**Purpose:** Static utility for detecting browser GameObjects and extracting browser properties. No navigation state.

**Pattern:** Same as CardDetector - static class, cached results, pure detection.

### Public API
```csharp
public static class BrowserDetector
{
    // Main detection
    public static BrowserInfo FindActiveBrowser();
    public static void InvalidateCache();

    // Browser type helpers
    public static bool IsMulliganBrowser(string browserType);
    public static bool IsScryLikeBrowser(string browserType);
    public static bool IsZoneBasedBrowser(string browserType); // NEW: Scry OR London
    public static string GetFriendlyBrowserName(string browserType);
}

public class BrowserInfo
{
    public bool IsActive { get; }
    public string BrowserType { get; }       // "Scry", "London", "Mulligan", etc.
    public GameObject BrowserGameObject { get; }
    public bool IsZoneBased { get; }         // True for Scry/London (two-zone navigation)
    public bool IsMulligan { get; }          // True for OpeningHand/Mulligan
}
```

### What moves here from BrowserNavigator
- `ScanForBrowser()` → `FindActiveBrowser()`
- `ExtractBrowserTypeFromScaffold()`
- `IsMulliganBrowserVisible()`
- `IsMulliganBrowser()`, `IsScryLikeBrowser()` → static helpers
- `GetFriendlyBrowserName()`
- `FriendlyBrowserNames` dictionary
- `BrowserScaffoldInfo` → merged into `BrowserInfo`
- Cache fields: `_cachedBrowser`, `_cachedBrowserGo`, `_lastBrowserScanTime`
- Constants: `ScaffoldPrefix`, `HolderDefault`, `HolderViewDismiss`, button names

### What stays out
- All navigation state
- All input handling
- All announcements

---

## File 2: BrowserNavigator.cs (~500-600 lines)

**Purpose:** Orchestrator for browser navigation. Owns lifecycle, input routing, generic browser handling.

**Pattern:** Same as DuelNavigator - owns sub-navigators, routes input, manages lifecycle.

### Public API
```csharp
public class BrowserNavigator
{
    // Lifecycle
    public bool IsActive { get; }
    public string ActiveBrowserType { get; }
    public void Update();                    // Call each frame from DuelNavigator
    public bool HandleInput();               // Returns true if input consumed
    public void ResetMulliganState();        // Call when entering new duel

    // For external access
    public GameObject GetCurrentCard();
}
```

### Internal Structure
```csharp
// Dependencies
private readonly IAnnouncementService _announcer;
private readonly BrowserZoneNavigator _zoneNavigator;  // NEW

// State
private bool _isActive;
private bool _hasAnnouncedEntry;
private BrowserInfo _browserInfo;  // From BrowserDetector

// Generic browser state (non-zone browsers)
private List<GameObject> _browserCards;
private List<GameObject> _browserButtons;
private int _currentCardIndex;
private int _currentButtonIndex;
```

### Responsibilities
1. **Update()** - Calls `BrowserDetector.FindActiveBrowser()`, manages enter/exit
2. **HandleInput()** - Routes to `_zoneNavigator` for zone browsers, handles generic browsers directly
3. **Enter/ExitBrowserMode()** - Lifecycle management
4. **Generic navigation** - Card/button navigation for simple browsers (YesNo, Dungeon, etc.)
5. **Button clicking** - `ClickConfirmButton()`, `ClickCancelButton()` (shared by all browsers)
6. **Announcements** - `AnnounceBrowserState()`, generic card/button announcements
7. **Element discovery** - For generic browsers only; zone browsers discovered by BrowserZoneNavigator

### What moves here from old BrowserNavigator
- `Update()` - simplified to use BrowserDetector
- `HandleInput()` - simplified to route to zone navigator
- `EnterBrowserMode()`, `ExitBrowserMode()`
- Generic navigation: `NavigateToNextCard/Button()`, `AnnounceCurrentCard/Button()`
- Button methods: `ClickConfirmButton()`, `ClickCancelButton()`, helpers
- `DiscoverBrowserElements()` - but only for generic browsers
- `RefreshBrowserButtons()`

### What moves OUT
- All London-specific code → BrowserZoneNavigator
- All Scry-specific code → BrowserZoneNavigator
- Zone-based navigation (C/D keys) → BrowserZoneNavigator
- Detection code → BrowserDetector

---

## File 3: BrowserZoneNavigator.cs (~400-500 lines)

**Purpose:** Handles two-zone navigation for Scry/Surveil and London mulligan browsers.

**Pattern:** Same as ZoneNavigator - manages zone state, card lists per zone, navigation within zones.

### Public API
```csharp
public class BrowserZoneNavigator
{
    // State
    public bool IsActive { get; }
    public BrowserZoneType CurrentZone { get; }  // Top/Bottom or Hand/Library
    public int CurrentCardIndex { get; }
    public GameObject CurrentCard { get; }

    // Lifecycle
    public void Activate(BrowserInfo browserInfo);
    public void Deactivate();

    // Input handling (called by BrowserNavigator)
    public bool HandleInput();  // Returns true if consumed

    // For BrowserNavigator to coordinate announcements
    public int GetZoneCardCount(BrowserZoneType zone);
    public string GetCurrentZoneName();
}

public enum BrowserZoneType
{
    None,
    Top,      // Scry: keep on top
    Bottom,   // Scry: put on bottom
    Hand,     // London: keep pile
    Library   // London: bottom pile
}
```

### Internal Structure
```csharp
private readonly IAnnouncementService _announcer;

// State
private bool _isActive;
private string _browserType;  // "Scry", "Surveil", "London"
private BrowserZoneType _currentZone;
private int _cardIndex;

// Zone card lists
private List<GameObject> _topCards;     // Scry top / London hand
private List<GameObject> _bottomCards;  // Scry bottom / London library

// London-specific
private int _mulliganCount;  // Cards to put on bottom
```

### Responsibilities
1. **Zone navigation** - C key (top/hand), D key (bottom/library)
2. **Card navigation within zone** - Left/Right arrows
3. **Card activation** - Enter to toggle card between zones
4. **Zone-specific refresh** - `RefreshScryCardLists()`, `RefreshLondonCardLists()`
5. **Zone-specific activation** - `TryActivateCardViaScryBrowser()`, `TryActivateCardViaLondonBrowser()`
6. **Announcements** - Zone entry, card navigation, activation results

### What moves here from old BrowserNavigator
- `ScryZone` enum → merged into `BrowserZoneType`
- `LondonZone` enum → merged into `BrowserZoneType`
- `EnterScryZone()`, `EnterLondonZone()` → unified `EnterZone()`
- `NavigateScryNext/Previous()`, `NavigateLondonNext/Previous()` → unified
- `AnnounceCurrentScryCard()`, `AnnounceCurrentLondonCard()` → unified
- `ActivateCurrentScryCard()`, `ActivateCurrentLondonCard()` → unified with internal branching
- `RefreshScryCardLists()`, `RefreshLondonCardLists()`
- `TryActivateCardViaScryBrowser()`, `TryActivateCardViaLondonBrowser()`
- London tracking: `_mulliganCount`, `_londonSelectedCards`
- Coroutines: `RefreshScryZoneAfterDelay()`, `RefreshLondonZoneAfterDelay()`
- Helper: `GetLondonBrowser()`

---

## Implementation Order

### Step 1: Create BrowserDetector.cs
1. Create new file with static class
2. Move detection methods
3. Move constants
4. Update BrowserNavigator to use `BrowserDetector.FindActiveBrowser()`
5. Test: Browser detection still works

### Step 2: Create BrowserZoneNavigator.cs
1. Create new file with zone navigation class
2. Move zone-related enums, state, methods
3. Unify Scry/London where possible (shared patterns)
4. Keep browser-specific activation methods (APIs differ)
5. Update BrowserNavigator to delegate to zone navigator
6. Test: Scry and London still work

### Step 3: Simplify BrowserNavigator.cs
1. Remove moved code
2. Clean up HandleInput() to route to zone navigator
3. Simplify element discovery (generic browsers only)
4. Test: All browser types still work

### Step 4: Final cleanup
1. Remove dead code
2. Update documentation
3. Ensure consistent logging patterns

---

## Benefits of This Split

1. **Clear responsibilities**
   - Detector: "Is there a browser? What kind?"
   - Navigator: "Handle lifecycle and generic browsers"
   - ZoneNavigator: "Handle two-zone Scry/London navigation"

2. **Easier maintenance**
   - London bugs: look in BrowserZoneNavigator
   - Detection issues: look in BrowserDetector
   - Button clicking: look in BrowserNavigator

3. **Matches existing patterns**
   - CardDetector / DuelNavigator / ZoneNavigator
   - Familiar structure for future development

4. **Testability**
   - BrowserDetector can be unit tested (just detection)
   - BrowserZoneNavigator can be tested with mock browser state

---

## Potential Issues to Watch

1. **State coordination** - BrowserNavigator needs to know when zone navigator is active
2. **Announcement ownership** - Who announces what? Clear boundaries needed
3. **Mulligan count tracking** - Currently in BrowserNavigator, should move to ZoneNavigator
4. **Coroutine handling** - Zone navigator needs MelonCoroutines access
5. **CardInfoNavigator integration** - Both navigators may need to update it

---

## Questions Before Implementation

None - the pattern is clear from the existing codebase structure.
