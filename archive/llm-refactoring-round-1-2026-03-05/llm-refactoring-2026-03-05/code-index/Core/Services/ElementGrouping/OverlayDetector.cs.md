# OverlayDetector.cs - Code Index

## File-level Comment
Simplified overlay detection that replaces the complex ForegroundLayer system.
Detects which overlay (if any) is currently active and should suppress other groups.

## Classes

### OverlayDetector (line 11)
```csharp
public class OverlayDetector
```

#### Fields
- private readonly MenuScreenDetector _screenDetector (line 13)
- private GameObject ForegroundPanel (line 19) - Property: gets current foreground panel from PanelStateManager

#### Constructor
- public OverlayDetector(MenuScreenDetector screenDetector) (line 21)

#### Methods - Overlay Detection
- public ElementGroup? GetActiveOverlay() (line 30)
  - Get the currently active overlay group, returns null if no overlay is active
- private static bool IsPopupPanel(GameObject obj) (line 86)
  - Check if a panel is a popup/dialog overlay

#### Methods - Element Filtering
- public bool IsInsideActiveOverlay(GameObject obj) (line 97)
  - Check if the given GameObject belongs to the currently active overlay
- private bool IsInsidePopup(GameObject obj) (line 121)
  - Check if an element is inside a popup dialog
- private bool IsInsideSettingsMenu(GameObject obj) (line 129)
  - Check if an element is inside the settings menu
- private bool IsInsideSocialPanel(GameObject obj) (line 139)
  - Check if an element is inside the social/friends panel
- private bool IsMailContentVisible() (line 149)
  - Check if mail content is visible (a specific mail is opened)
- private bool IsInsideMailboxList(GameObject obj) (line 185)
  - Check if an element is inside the mailbox mail list (left pane)
- private bool IsInsideMailboxContent(GameObject obj) (line 209)
  - Check if an element is inside the mailbox content view (right pane)
- private bool IsInsidePlayBlade(GameObject obj) (line 234)
  - Check if an element is inside the play blade
- private static bool IsInsideChallengeScreen(GameObject obj) (line 269)
  - Check if an element is inside the challenge screen
- private static bool IsInsideNPEOverlay(GameObject obj) (line 302)
  - Check if an element is inside the NPE overlay

#### Methods - Rewards Popup
- private bool _lastRewardsPopupState (line 318) - Cache to avoid logging spam
- public bool IsRewardsPopupOpen() (line 325)
  - Check if the rewards popup is currently open
- private bool CheckRewardsPopupOpenInternal() (line 339)
  - Internal implementation of rewards popup detection
- private bool IsInsideRewardsPopup(GameObject obj) (line 405)
  - Check if an element is inside the rewards popup
