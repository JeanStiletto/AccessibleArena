# OverlayDetector.cs
Path: src/Core/Services/ElementGrouping/OverlayDetector.cs
Lines: 451

## Top-level comments
- Simplified overlay detection replacing the complex ForegroundLayer system. Queries PanelStateManager for the foreground panel and MenuScreenDetector for secondary checks. Provides IsInsideActiveOverlay for element filtering.

## public class OverlayDetector (line 12)
### Fields
- private readonly MenuScreenDetector _screenDetector (line 13)
- private bool _lastRewardsPopupState = false (line 340)
### Properties
- private GameObject ForegroundPanel => PanelStateManager.Instance?.GetFilterPanel() (line 19)
### Methods
- public OverlayDetector(MenuScreenDetector screenDetector) (line 21)
- public ElementGroup? GetActiveOverlay() (line 30)
- private static bool IsPopupPanel(GameObject obj) (line 86)
- public bool IsInsideActiveOverlay(GameObject obj) (line 98) — Note: returns true (visible) when no overlay is active
- private bool IsInsidePopup(GameObject obj) (line 122)
- private bool IsInsideSettingsMenu(GameObject obj) (line 130)
- private bool IsInsideSocialPanel(GameObject obj) (line 139)
- private static bool IsChildOf(GameObject child, GameObject parent) (line 148)
- private bool IsMailContentVisible() (line 171)
- private bool IsInsideMailboxList(GameObject obj) (line 207)
- private bool IsInsideMailboxContent(GameObject obj) (line 230)
- private bool IsInsidePlayBlade(GameObject obj) (line 254)
- private static bool IsInsideChallengeScreen(GameObject obj) (line 291)
- private static bool IsInsideNPEOverlay(GameObject obj) (line 325)
- public bool IsRewardsPopupOpen() (line 347) — Note: caches last state and only logs on change to reduce spam
- private bool CheckRewardsPopupOpenInternal() (line 361)
- private bool IsInsideRewardsPopup(GameObject obj) (line 427)
