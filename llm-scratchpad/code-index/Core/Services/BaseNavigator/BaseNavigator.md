# BaseNavigator.cs
Path: src/Core/Services/BaseNavigator/BaseNavigator.cs
Lines: 1600

## Top-level comments
- Base class for all screen navigators. Handles Tab/Enter navigation, element management, announcements, and event system integration. Subclasses implement screen detection and element discovery.
- Core partial hosting abstract definitions, lifecycle, input dispatch, element discovery helpers, card navigation integration, and main update loop.

## public abstract partial class BaseNavigator : IScreenNavigator (line 25)
### Fields
- protected readonly IAnnouncementService _announcer (line 29)
- protected readonly List<NavigableElement> _elements (line 30)
- protected int _currentIndex (line 31)
- protected bool _isActive (line 32)
- protected int _currentActionIndex (line 39)
- private bool _lastNavigationWasTab (line 46)
- protected readonly LetterSearchHandler _letterSearch (line 49)
- protected readonly KeyHoldRepeater _holdRepeater (line 52)
- protected bool _suppressNavigationAnnouncement (line 607)

### Properties
- protected bool IsValidIndex (line 42)
- public bool IsActive (line 354)
- public int ElementCount (line 355)
- public int CurrentIndex (line 356)

### Methods
- public abstract string NavigatorId (line 114)
- public abstract string ScreenName (line 117)
- protected abstract bool DetectScreen() (line 124)
- protected abstract void DiscoverElements() (line 131)
- public virtual int Priority (line 138)
- protected virtual bool HandleCustomInput() (line 141)
- protected virtual void OnActivated() (line 144)
- protected virtual void OnDeactivating() (line 147)
- protected virtual bool OnElementActivated(int index, GameObject element) (line 150)
- protected virtual void OnDeckBuilderCardCountCapture() (line 153)
- protected virtual void OnDeckBuilderCardActivated() (line 156)
- protected virtual void OnPopupDetected(PanelInfo panel) (line 159)
- protected virtual void OnPopupClosed() (line 166)
- internal static bool IsDecorativePanel(string name) (line 172)
- protected virtual bool IsPopupExcluded(PanelInfo panel) (line 183)
- public virtual string GetTutorialHint() (line 190)
- protected virtual string GetActivationAnnouncement() (line 193)
- protected virtual string GetElementAnnouncement(int index) (line 201)
- public static string RefreshElementLabel(GameObject obj, string label, UIElementClassifier.ElementRole role) (line 215) — refresh live state
- protected virtual bool SupportsCardNavigation (line 342)
- protected virtual bool AcceptSpaceKey (line 345)
- protected virtual bool SupportsLetterNavigation (line 348)
- public IReadOnlyList<GameObject> GetNavigableGameObjects() (line 362)
- public virtual void OnSceneChanged(string sceneName) (line 370)
- public virtual void ForceRescan() (line 383)
- protected virtual void ForceRescanAfterSearch() (line 415)
- protected BaseNavigator(IAnnouncementService announcer) (line 453)
- public virtual void Update() (line 463)
- protected virtual void TryActivate() (line 519)
- protected virtual bool ValidateElements() (line 553)
- public virtual void Deactivate() (line 578)
- protected virtual void SyncIndexToFocusedElement() (line 613)
- private void SyncIndexToElement(GameObject element) (line 645)
- protected virtual bool HandleEarlyInput() (line 668)
- protected virtual void HandleInput() (line 670)
- protected virtual void ActivateAlternateAction() (line 932)
- public static string BuildLabel(string label, string roleLabel, UIElementClassifier.ElementRole role) (line 954)
- protected virtual string BuildElementLabel(UIElementClassifier.ClassificationResult classification) (line 968)
- protected virtual void Move(int direction) (line 974)
- protected virtual bool HandleLetterNavigation(KeyCode key) (line 1018)
- protected virtual void UpdateEventSystemSelection() (line 1053)
- private void CloseDropdownOnElement(GameObject element) (line 1156)
- protected virtual bool HandleAttachedAction(AttachedAction action) (line 1178)
- protected virtual void MoveNext() (line 1180)
- protected virtual void MovePrevious() (line 1181)
- protected virtual void MoveFirst() (line 1184)
- protected virtual void MoveLast() (line 1203)
- protected virtual void AnnounceCurrentElement() (line 1222)
- protected virtual void ActivateCurrentElement() (line 1231)
- protected static bool IsInsideRegistrationPanel(GameObject element) (line 1356)
- protected void UpdateCardNavigation() (line 1379)
- protected virtual bool IsCurrentCardHidden(GameObject cardElement) (line 1419)
- protected void AddElement(GameObject element, string label) (line 1426)
- protected void AddElement(GameObject element, string label, CarouselInfo carouselInfo) (line 1432)
- protected void AddElement(GameObject element, string label, CarouselInfo carouselInfo, GameObject alternateAction) (line 1438)
- protected void AddElement(GameObject element, string label, CarouselInfo carouselInfo, GameObject alternateAction, List<AttachedAction> attachedActions, UIElementClassifier.ElementRole role) (line 1444)
- protected void AddTextBlock(string text) (line 1472)
- protected void AddButton(GameObject buttonObj, string fallbackLabel) (line 1484)
- protected void AddToggle(Toggle toggle, string label) (line 1493)
- protected void AddInputField(GameObject inputObj, string fieldName) (line 1501)
- protected GameObject FindChildByName(Transform parent, string name) (line 1508)
- protected GameObject FindChildByPath(Transform parent, string path) (line 1528)
- protected bool NavigateToHome() (line 1547)
- protected string GetButtonText(GameObject buttonObj, string fallback) (line 1579)
- protected string TruncateLabel(string text, int maxLength) (line 1585)

## protected struct AttachedAction (line 58)
- string Id
- string Label
- GameObject TargetButton

## protected struct NavigableElement (line 72)
- GameObject GameObject
- string Label
- UIElementClassifier.ElementRole Role
- CarouselInfo Carousel
- GameObject AlternateActionObject
- List<AttachedAction> AttachedActions

## protected struct CarouselInfo (line 87)
- bool HasArrowNavigation
- GameObject PreviousControl
- GameObject NextControl
- Slider SliderComponent
- bool UseHoverActivation
- Action OnIncrement
- Action OnDecrement
- Func<string> ReadLabel
