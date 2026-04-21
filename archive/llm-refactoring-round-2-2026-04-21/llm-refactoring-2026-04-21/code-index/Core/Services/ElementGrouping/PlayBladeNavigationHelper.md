# PlayBladeNavigationHelper.cs
Path: src/Core/Services/ElementGrouping/PlayBladeNavigationHelper.cs
Lines: 256

## Top-level comments
- Centralized helper for PlayBlade navigation: handles Enter and Backspace within Tabs/Content/Folders/FolderContents hierarchy. Also manages BotMatch mode flag and two-step virtual queue-type tab activation.

## public enum PlayBladeResult (line 9)
- NotHandled (line 11)
- Handled (line 13)
- RescanNeeded (line 15)
- CloseBlade (line 17)

## public class PlayBladeNavigationHelper (line 27)
### Fields
- private readonly GroupedNavigator _groupedNavigator (line 28)
### Properties
- public bool IsActive => _groupedNavigator.IsPlayBladeContext (line 34)
- public static bool IsBotMatchMode { get; private set; } (line 41)
### Methods
- public PlayBladeNavigationHelper(GroupedNavigator groupedNavigator) (line 43)
- public static void SetBotMatchMode(bool value) (line 50)
- public PlayBladeResult HandleEnter(GameObject element, ElementGroup elementGroup) (line 67)
- public PlayBladeResult HandleQueueTypeEntry(GroupedElement element) (line 100) — Note: for virtual entries clicks FindMatch tab then stores pending queue type for automatic two-step activation
- public PlayBladeResult HandleBackspace() (line 139)
- public void OnPlayBladeOpened(string bladeViewName) (line 234)
- public void OnPlayBladeClosed() (line 243) — Note: also clears IsBotMatchMode
- public void Reset() (line 253)
