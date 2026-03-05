# BaseNavigator.cs

Base class for screen navigators. Handles common Tab/Enter navigation,
element management, and announcements. Subclasses implement screen detection
and element discovery.

## Class: BaseNavigator : IScreenNavigator (line 21)

### Core Fields
- readonly IAnnouncementService _announcer (line 25)
- readonly List<NavigableElement> _elements (line 26)
- int _currentIndex (line 27)
- bool _isActive (line 28)
- int _currentActionIndex (line 35)
- float _stepperAnnounceDelay (line 41)
- float _spinnerRescanDelay (line 45)
- InputFieldEditHelper _inputFieldHelper (line 49)
- bool _lastNavigationWasTab (line 53)

### Structs
- struct AttachedAction (line 59)
- struct NavigableElement (line 70)
- struct CarouselInfo (line 85)

### Popup Mode Fields (line 109-117)
- bool _isInPopupMode (line 111)
- GameObject _popupGameObject (line 112)
- List<NavigableElement> _savedElements (line 113)
- int _savedIndex (line 114)
- InputFieldEditHelper _popupInputHelper (line 115)
- DropdownEditHelper _popupDropdownHelper (line 116)

### Abstract Members
- abstract string NavigatorId (line 123)
- abstract string ScreenName (line 126)
- protected abstract bool DetectScreen() (line 132)
- protected abstract void DiscoverElements() (line 140)

### Virtual Members
- virtual int Priority => 0 (line 147)
- protected virtual bool HandleCustomInput() (line 150)
- protected virtual void OnActivated() (line 153)
- protected virtual void OnDeactivating() (line 155)
- protected virtual bool OnElementActivated(int index, GameObject element) (line 158)
- protected virtual void OnDeckBuilderCardActivated() (line 162)
- protected virtual void OnPopupDetected(PanelInfo panel) (line 165)
- protected virtual void OnPopupClosed() (line 172)
- protected virtual bool IsPopupExcluded(PanelInfo panel) (line 178)
- protected virtual string GetActivationAnnouncement() (line 181)
- protected virtual string GetElementAnnouncement(int index) (line 189)
- static string RefreshElementLabel(GameObject obj, string label, ElementRole role) (line 203)
  Note: Refreshes cached labels with live state (toggle checked, input field content, dropdown value)
- protected virtual bool SupportsCardNavigation => true (line 321)
- protected virtual bool AcceptSpaceKey => true (line 324)

### IScreenNavigator Implementation
- bool IsActive => _isActive (line 330)
- int ElementCount => _elements.Count (line 331)
- int CurrentIndex => _currentIndex (line 332)
- IReadOnlyList<GameObject> GetNavigableGameObjects() (line 338)
- virtual void OnSceneChanged(string sceneName) (line 346)
- virtual void ForceRescan() (line 359)
- protected virtual void ForceRescanAfterSearch() (line 391)

### Constructor
- protected BaseNavigator(IAnnouncementService announcer) (line 429)

### Core Update Loop
- virtual void Update() (line 439)
- void TrackInputFieldState() (line 500)
- protected virtual void TryActivate() (line 513)
- protected virtual bool ValidateElements() (line 547)
- virtual void Deactivate() (line 572)

### Input Handling Methods
- bool ExitInputFieldEditMode(bool suppressNextAnnouncement) (line 603)
- void ScheduleSearchRescan() (line 635)
- protected virtual void HandleInputFieldNavigation() (line 652)
- protected virtual void HandleDropdownNavigation() (line 719)
- void SelectDropdownItem() (line 785)
- static void SelectCurrentDropdownItem(string callerId) (line 791)
- static bool SetDropdownValueSilent(GameObject dropdownObj, int itemIndex) (line 839)
- static string GetDropdownDisplayValue(GameObject dropdownObj) (line 894)
- static string GetDropdownFirstOptionFallback(GameObject dropdownObj) (line 945)
- void CloseActiveDropdown(bool silent) (line 988)
- static void CloseDropdown(string callerId, IAnnouncementService announcer, bool silent) (line 994)
- protected virtual void SyncIndexToFocusedElement() (line 1088)
- void SyncIndexToElement(GameObject element) (line 1120)
- protected virtual bool HandleEarlyInput() (line 1143)
- protected virtual void HandleInput() (line 1145)
- protected virtual void ActivateAlternateAction() (line 1372)
- protected virtual bool HandleCarouselArrow(bool isNext) (line 1392)
- bool HandleSliderArrow(Slider slider, bool isNext) (line 1458)
- bool HandleAttachedActionArrow(NavigableElement element, bool isNext) (line 1481)
- protected virtual void RescanAfterSpinnerChange() (line 1526)
- void AnnounceStepperValue() (line 1570)

### Label Building
- static string BuildLabel(string label, string roleLabel, ElementRole role) (line 1626)
- protected virtual string BuildElementLabel(ClassificationResult classification) (line 1640)

### Navigation Methods
- protected virtual void Move(int direction) (line 1646)
- protected virtual void UpdateEventSystemSelection() (line 1687)
  Note: Updates Unity EventSystem to match navigator's current element
- void CloseDropdownOnElement(GameObject element) (line 1780)
- void DeactivateInputFieldOnElement(GameObject element) (line 1841)
- protected virtual void MoveNext() (line 1863)
- protected virtual void MovePrevious() (line 1864)
- protected virtual void MoveFirst() (line 1867)
- protected virtual void MoveLast() (line 1885)
- protected virtual void AnnounceCurrentElement() (line 1903)
- protected virtual void ActivateCurrentElement() (line 1912)

### Card Navigation Integration
- protected void UpdateCardNavigation() (line 2032)

### Popup Mode Methods (line 2060-2500)
- bool IsInPopupMode (line 2063)
- GameObject PopupGameObject (line 2066)
- void EnablePopupDetection() (line 2072)
- void DisablePopupDetection() (line 2081)
- void OnPopupPanelChanged(PanelInfo oldPanel, PanelInfo newPanel) (line 2090)
- static bool IsPopupPanel(PanelInfo panel) (line 2114)
- protected void EnterPopupMode(GameObject popup) (line 2129)
- protected void ExitPopupMode() (line 2175)
- void ClearPopupModeState() (line 2186)
- bool ValidatePopup() (line 2227)
- protected void DismissPopup() (line 2238)
- void HandlePopupInput() (line 2285)
- void NavigatePopupItem(int direction) (line 2349)
- void ActivatePopupItem() (line 2370)
- void AnnouncePopupOpen() (line 2405)
- void AnnouncePopupCurrentItem() (line 2433)
- protected virtual void DiscoverPopupElements(GameObject popup) (line 2469)
  Note: Discovers text blocks, input fields, dropdowns, and buttons in popup

Note: BaseNavigator continues beyond line 2500 with additional popup discovery, element addition,
and helper methods. Full file is 3408 lines.
