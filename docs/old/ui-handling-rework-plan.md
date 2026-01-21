# UI Code Quality Improvement Plan

## Overview

Refactor UIElementClassifier (1,421 lines) and UIActivator (918 lines) to improve code quality while preserving all functionality.

**Goals:**
- Remove dead code (keep essential debug logging)
- Eliminate unnecessary repetitions
- Fix responsibility/logic issues
- Generalize where possible
- Improve efficiency, maintainability, readability

---

## Phase 1: UIElementClassifier Refactoring

### 1.1 Extract Filter Pattern Registry
**Problem:** 900+ lines of name-pattern string matching with repeated ContainsIgnoreCase calls

**Solution:** Create a centralized filter registry
```csharp
private static readonly HashSet<string> FilteredNamePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "hitbox", "backer", "gradient", "nav", "pip", "blocker", ...
};
```

**Files:** `UIElementClassifier.cs` lines 613-712

### 1.2 Split Classify() Method
**Problem:** 250-line method with deeply nested if-else chains

**Solution:** Extract role-specific classification into dedicated methods:
- `ClassifyAsCard()`
- `ClassifyAsButton()`
- `ClassifyAsToggle()`
- `ClassifyAsSlider()`
- `ClassifyAsDropdown()`
- `ClassifyAsSettingsControl()`

**Files:** `UIElementClassifier.cs` lines 98-348

### 1.3 Consolidate Label Cleaning
**Problem:** `CamelCasePattern.Replace(label, "$1 $2")` applied in 5+ locations

**Solution:** Create single helper:
```csharp
private static string CleanLabel(string text) { ... }
```

### 1.4 Extract Named Constants
**Problem:** Magic values scattered (e.g., `< 10` pixels, `MaxLabelLength = 80`)

**Solution:** Group at top of file:
```csharp
private const int MinDecorativeSize = 10;
private const int MaxLabelLength = 80;
private const float MinVisibleAlpha = 0.1f;
```

### 1.5 Consolidate GetEffectiveElementName
**Problem:** `GetEffectiveToggleName()` and `GetEffectiveButtonName()` are nearly identical

**Solution:** Single generic method with element type parameter

### 1.6 Remove/Inline Dead Code
- `ResolutionPattern` regex (used once) - inline
- `IsInsideFriendsWidget()` - inline into caller

---

## Phase 2: UIActivator Refactoring

### 2.1 Cache FindObjectsOfType Results
**Problem:** `IsTargetingModeActive()` performs two expensive global scans every call

**Solution:** Add caching with time-based invalidation:
```csharp
private static float _lastTargetingScanTime;
private static bool _cachedTargetingResult;
private const float TargetingCacheTimeout = 0.1f;
```

**Files:** `UIActivator.cs` lines 717-782

### 2.2 Extract CustomButton Detection
**Problem:** Same CustomButton detection logic in 4+ locations

**Solution:** Single helper method:
```csharp
private static bool IsCustomButton(MonoBehaviour mb)
    => mb.GetType().Name == "CustomButton";
```

### 2.3 Consolidate Text Component Scanning
**Problem:** Mirrored code for TMP_Text vs legacy Text (lines 724-776)

**Solution:** Generic helper:
```csharp
private static bool ScanTextComponentsForPattern<T>(Regex pattern, Func<T, string> getText) where T : Component
```

### 2.4 Remove Unused Parameter
**Problem:** `TargetNavigator targetNavigator` in `PlayCardViaTwoClick` is never used

**Solution:** Remove parameter from signature

### 2.5 Extract Timing Constants
**Problem:** Magic wait times (0.1s, 0.5s, 0.6s) hardcoded

**Solution:**
```csharp
private const float CardSelectDelay = 0.1f;
private const float CardPickupDelay = 0.5f;
private const float CardDropDelay = 0.6f;
```

### 2.6 WinAPI Code - Keep But Document
**Problem:** 47+ lines of commented WinAPI code with historical notes

**Solution:** Keep the WinAPI code (marked as working fallback), add note to KNOWN_ISSUES.md to test later if it can be removed

### 2.7 Cache Reflection Results
**Problem:** `TryInvokeCustomButtonOnClick()` re-reflects every call

**Solution:** Cache FieldInfo/MethodInfo for CustomButton._onClick

---

## Phase 3: Shared Improvements

### 3.1 Create UIConstants Class
Centralize shared constants used by both classes:
```csharp
public static class UIConstants
{
    public const string CustomButtonTypeName = "CustomButton";
    public const float MinVisibleAlpha = 0.1f;
    // etc.
}
```

### 3.2 Extract Common Type Checking
Both files check for CustomButton, TooltipTrigger, etc. Extract to shared helper.

---

## Implementation Order

1. **UIElementClassifier Phase 1.4** - Extract constants (low risk, immediate readability)
2. **UIElementClassifier Phase 1.3** - Consolidate label cleaning
3. **UIActivator Phase 2.5** - Extract timing constants
4. **UIActivator Phase 2.2** - Extract CustomButton detection
5. **UIActivator Phase 2.6** - Add KNOWN_ISSUES note about WinAPI code
6. **UIActivator Phase 2.4** - Remove unused parameter
7. **UIActivator Phase 2.1** - Cache targeting scan (performance)
8. **UIActivator Phase 2.3** - Consolidate text scanning
9. **UIElementClassifier Phase 1.5** - Consolidate GetEffectiveName methods
10. **UIElementClassifier Phase 1.6** - Inline dead code
11. **UIElementClassifier Phase 1.1** - Filter pattern registry (biggest change)
12. **UIElementClassifier Phase 1.2** - Split Classify method

---

## Verification

After each change:
1. Build: `dotnet build "C:\Users\fabia\arena\src\MTGAAccessibility.csproj"`
2. Deploy and test in game:
   - Menu navigation (GeneralMenuNavigator uses UIElementClassifier)
   - Settings screen (stepper controls, dropdowns)
   - Card play (UIActivator.PlayCardViaTwoClick)
   - Targeting mode (UIActivator.IsTargetingModeActive)

---

## Files to Modify

- `C:\Users\fabia\arena\src\Core\Services\UIElementClassifier.cs`
- `C:\Users\fabia\arena\src\Core\Services\UIActivator.cs`
- `C:\Users\fabia\arena\docs\KNOWN_ISSUES.md` (add note about WinAPI fallback code)
- Possibly create: `C:\Users\fabia\arena\src\Core\Services\UIConstants.cs` (if shared constants needed)

---

## Risk Assessment

- **Low Risk:** Constant extraction, dead code removal, comment cleanup
- **Medium Risk:** Method extraction, caching (could change timing)
- **Higher Risk:** Filter pattern registry (changes core classification logic)

All changes preserve existing functionality - this is pure refactoring.
