# PlayBladeNavigationHelper.cs - Code Index

## File-level Comment
Centralized helper for PlayBlade navigation.
Handles all PlayBlade-specific Enter and Backspace logic.
GeneralMenuNavigator just calls this and acts on the result.

## Enums

### PlayBladeResult (line 9)
```csharp
public enum PlayBladeResult
```
Result of PlayBlade navigation handling:
- NotHandled (line 12) - Not a PlayBlade context, let normal handling proceed
- Handled (line 14) - Helper handled it, no further action needed
- RescanNeeded (line 16) - Helper handled it, trigger a rescan to update navigation
- CloseBlade (line 18) - Close the PlayBlade

## Classes

### PlayBladeNavigationHelper (line 26)
```csharp
public class PlayBladeNavigationHelper
```

#### Fields
- private readonly GroupedNavigator _groupedNavigator (line 28)

#### Properties
- public bool IsActive (line 34) - Whether currently in a PlayBlade context
- public static bool IsBotMatchMode { get; private set; } (line 41) - Whether user selected Bot-Match mode

#### Constructor
- public PlayBladeNavigationHelper(GroupedNavigator groupedNavigator) (line 43)

#### Methods - Bot Match Mode
- public static void SetBotMatchMode(bool value) (line 51)
  - Set Bot-Match mode (called when user activates a PlayBlade mode button)

#### Methods - Navigation Handling
- public PlayBladeResult HandleEnter(GameObject element, ElementGroup elementGroup) (line 67)
  - Handle Enter key press on an element (called BEFORE UIActivator.Activate)
- public PlayBladeResult HandleQueueTypeEntry(GroupedElement element) (line 100)
  - Handle Enter on a queue type subgroup entry (Ranked, Open Play, Brawl)
  - Note: Handles both virtual and real tabs with two-step activation
- public PlayBladeResult HandleBackspace() (line 140)
  - Handle Backspace key press (called BEFORE generic grouped navigation handling)
  - Navigation hierarchy: Tabs -> Content -> Folders -> Folder (decks)

#### Methods - Lifecycle
- public void OnPlayBladeOpened(string bladeViewName) (line 231)
  - Called when PlayBlade opens, sets context and requests tabs entry
- public void OnPlayBladeClosed() (line 241)
  - Called when PlayBlade closes, clears the PlayBlade context
- public void Reset() (line 251)
