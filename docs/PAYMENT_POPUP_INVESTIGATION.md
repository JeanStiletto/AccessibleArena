# Payment Method Popup Investigation

## Status: Fixed (PackProgressMeter excluded from panel detection)

## Problem
When pressing Enter on "Change payment method" in the Store, a browser-based popup opens (confirmed by OCR) but StoreNavigator does not detect or handle it.

## What We Know

### Call Chain
1. StoreNavigator calls `OnButton_PaymentSetup()` via reflection on `ContentController_StoreCarousel`
2. This calls `WrapperController.Instance.Store.OpenPaymentSetup()`
3. `Store` is a `StoreManager` (abstract class in Core.dll)
4. `StoreManager.OpenPaymentSetup()` is a virtual method with **empty body**
5. The concrete subclass is loaded at runtime from a platform-specific assembly (not in Managed folder)
6. Searched ALL managed DLLs - no override found

### What Opens
- `FullscreenZFBrowserCanvas(Clone)` - an embedded Chromium browser overlay (ZFBrowser.dll / ZenFulcrum)
- Contains: `CanvasGroup/Panel/MainButtonOutline` with text "Zurueck zur Arena" (Back to Arena)
- Uses CanvasGroup alpha for visibility (0 = hidden, 1 = visible)
- Always present in scene hierarchy (active with alpha=0 when hidden)

### Detection Progress

**AlphaPanelDetector (WORKING)**
Added "fullscreenzfbrowsercanvas" to AlphaPanelDetector.OwnedPatterns. This correctly:
- Registers `FullscreenZFBrowserCanvas(Clone)` as a tracked popup on scene load
- Detects alpha transition from 0 to 1 when payment popup opens
- Reports to PanelStateManager as `PanelType.Popup, Priority=1000, Filters=True`

Log confirms detection works:
```
[PanelStateManager] AUDIT: Panel 'FullscreenZFBrowserCanvas(Clone)' owned by Alpha
[PanelStateManager] Panel opened: PanelInfo(FullscreenZFBrowserCanvas(Clone), Popup, Priority=1000, Filters=True, Valid=True)
[AlphaDetector] Reported popup opened: FullscreenZFBrowserCanvas(Clone)
```

**PanelStateManager (BLOCKED)**
The panel is added to the stack, but `OnPanelChanged` never fires because:
- `PackProgressMeter:PackProgressMeter_Desktop_16x9(Clone)` is already in the stack as active panel
- PackProgressMeter also has Priority=1000, Filters=True
- PackProgressMeter was opened earlier and never closed (stale entry)
- When browser canvas opens with same priority, `UpdateActivePanel()` finds PackProgressMeter first
- `oldActive == newActive` (both point to PackProgressMeter) -> no change -> no event

**StoreNavigator (NEVER REACHED)**
`OnPanelChanged` callback never fires, so StoreNavigator never enters popup mode.

## Root Cause
`PackProgressMeter` (detected by ReflectionPanelDetector) opens during Store navigation and its closure is never detected. It remains in the panel stack as the "active panel", blocking any new panel with equal or lower priority from triggering `OnPanelChanged`.

## Fix Applied
PackProgressMeter excluded from `ReflectionPanelDetector.ExcludedTypeNames`. It's a bonus pack progress bar (not a real popup) that inherits from PopupBase. Its stale entry in the panel stack was blocking the browser canvas from becoming the active panel. With the exclusion, AlphaPanelDetector's browser canvas detection flows through to StoreNavigator's popup handler.

Additionally, pack progress info is now shown as a utility element in StoreNavigator's tab list.

## Infrastructure Already Built
StoreNavigator already has full popup handling code (following SettingsMenuNavigator pattern):
- `OnPanelChanged` callback subscribed in `OnActivated()`
- `IsPopupPanel()` matches `PanelType.Popup`
- `DiscoverPopupElements()` finds buttons in popup
- `HandlePopupInput()` for Up/Down navigation
- `DismissPopup()` with cancel button search (matches "zurueck" pattern)
- All of this works - it just never gets triggered due to the stale panel stack issue.

## Related Files
- `src/Core/Services/PanelDetection/AlphaPanelDetector.cs` - Added "fullscreenzfbrowsercanvas" pattern
- `src/Core/Services/PanelDetection/PanelStateManager.cs` - Panel stack management
- `src/Core/Services/PanelDetection/ReflectionPanelDetector.cs` - Tracks PackProgressMeter
- `src/Core/Services/StoreNavigator.cs` - Popup handling code (lines 257-293, 1291-1509)
- ZFBrowser.dll (ZenFulcrum.EmbeddedBrowser) - The embedded browser library
