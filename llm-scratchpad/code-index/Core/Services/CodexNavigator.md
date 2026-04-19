# CodexNavigator.cs
Path: src/Core/Services/CodexNavigator.cs
Lines: 1279

## Top-level comments
- Navigator for the How to Play / Codex screen. Two-level model (table of contents + content), wraps the game's CodexController via reflection, and supports nested TOC, Credits mode, and dynamic discovery of learn-to-play sections.

## private enum CodexMode (line 33, nested in CodexNavigator)
- TableOfContents (line 35)
- Content (line 36)
- Credits (line 37)

## private struct TocItem (line 75, nested in CodexNavigator)
### Fields
- public string Label (line 77)
- public GameObject Target (line 78)
- public bool HasChildren (line 79)
- public int Depth (line 80)

## private struct TocLevel (line 84, nested in CodexNavigator)
### Fields
- public List<TocItem> Items (line 86)
- public int SelectedIndex (line 87)
- public string ContextLabel (line 88)

## public class CodexNavigator : BaseNavigator (line 28)
### Fields
- private static Type _controllerType (line 40)
- private static PropertyInfo _isOpenProp (line 41)
- private static FieldInfo _learnToPlayRootField (line 42)
- private static FieldInfo _creditsRootField (line 43)
- private static FieldInfo _tocContainerField (line 44)
- private static FieldInfo _contentContainerField (line 45)
- private static MethodInfo _openSectionMethod (line 46)
- private static MethodInfo _closeSectionMethod (line 47)
- private static MethodInfo _scrollMethod (line 48)
- private static bool _reflectionCached (line 49)
- private Component _controller (line 51)
- private CodexMode _mode (line 53)
- private readonly Stack<TocLevel> _tocStack = new Stack<TocLevel>() (line 54)
- private List<GameObject> _contentBlocks = new List<GameObject>() (line 55)
- private int _contentIndex (line 56)
- private GameObject _activeContentRoot (line 57)
- private int _rescanFrameCountdown (line 59)
- private const int RescanDelayFrames = 20 (line 60)
### Properties
- public override string NavigatorId (line 63)
- public override string ScreenName (line 64)
- public override int Priority (line 65)
### Methods
- public CodexNavigator(...) (line 67) — constructor, wires announcer/focus/activator
- protected override string GetScreenName() (line 94)
- public override bool DetectScreen() (line 103)
- private Component FindController() (line 135)
- private void EnsureReflectionCached() (line 170)
- public override void DiscoverElements() (line 220)
- private void DiscoverTopLevel() (line 260)
- private List<TocItem> ScanContainerForItems(GameObject container, int depth) (line 310)
- private Component FindTocSectionComponent(GameObject go) (line 410)
- private string ExtractTocSectionLabel(Component section) (line 450)
- private bool TocSectionHasChildren(Component section) (line 510)
- private GameObject ResolveTocSectionTarget(Component section) (line 560)
- private void DiscoverContentBlocks(GameObject root) (line 620)
- private void AnnounceCurrentTocItem() (line 700)
- private void AnnounceCurrentContent() (line 740)
- protected override string GetTutorialHint() (line 790)
- protected override string GetActivationAnnouncement(GameObject element) (line 810)
- public override bool HandleInput() (line 840) — Note: switches on _mode; handles Up/Down, Enter, Backspace, Home/End across TOC/Content/Credits
- private void EnterTocItem() (line 930)
- private void ExitTocLevel() (line 970)
- private void OpenCreditsMode() (line 1010)
- private void CloseCreditsMode() (line 1040)
- private void MoveContentNext() (line 1070)
- private void MoveContentPrevious() (line 1090)
- private void ScrollContent(int direction) (line 1110) — Note: invokes reflected _scrollMethod on controller
- public override void Update() (line 1150)
- private void QuietRescan() (line 1180)
- public override void OnActivated() (line 1210)
- public override void OnDeactivating() (line 1230)
- protected override bool ValidateElements() (line 1250)
- public override void OnSceneChanged() (line 1265)
