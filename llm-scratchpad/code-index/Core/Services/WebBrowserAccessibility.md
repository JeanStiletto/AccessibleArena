# WebBrowserAccessibility.cs

## Overview
Provides full keyboard navigation and screen reader support for embedded Chromium browser popups (ZFBrowser). Extracts page elements via JavaScript, presents them as a navigable list, and allows clicking buttons, typing into fields, etc.

Used by StoreNavigator when a payment popup opens.

## Class: WebBrowserAccessibility (line 21)

### Constants
- private const float RescanDelayClick (line 25)
- private const float RescanDelaySecond (line 26)
- private const float RescanDelayCheckbox (line 27)
- private const float LoadTimeout (line 28)

### State
- private Browser _browser (line 34)
- private GameObject _browserPanel (line 35)
- private IAnnouncementService _announcer (line 36)
- private bool _isActive (line 37)
- private List<WebElement> _elements (line 39)
- private int _currentIndex (line 40)
- private bool _isEditingField (line 41)
- private bool _isLoading (line 42)
- private int _lastWebElementCount (line 43)
- private string _editFieldValue (line 46)
- private int _editCursorPos (line 47)
- private bool _pendingRescan (line 50)
- private float _rescanTimer (line 51)
- private float _secondRescanTimer (line 52)
- private float _extractionStartTime (line 53)
- private GameObject _backToArenaButton (line 56)
- private int _emptyRescanCount (line 59)
- private bool _captchaDetected (line 60)
- private const int MaxEmptyRescansBeforeCheck (line 61)

### WebElement
- private struct WebElement (line 67)
  - string Tag
  - string Text
  - string Role
  - string InputType
  - string Placeholder
  - string Value
  - int Index
  - bool IsInteractive
  - bool IsChecked
  - bool IsBackToArena

### JavaScript
- private const string ExtractionScript (line 88)
  - Scans main document AND same-origin iframes recursively
- private const string FindElementFunc (line 266)
- private static string ClickScript(int index) (line 283)
- private static string FocusScript(int index) (line 289)
- private static string ReadValueScript(int index) (line 295)
- private static string AppendTextScript(int index, string text) (line 306)
  - Three approaches: execCommand, keyboard events, direct value
- private static string BackspaceScript(int index) (line 366)
- private static string SubmitScript(int index) (line 410)
- private const string DetectCrossOriginIframesScript (line 430)

### Public API
- public bool IsActive (line 450)
- public void Activate(GameObject panel, IAnnouncementService announcer) (line 456)
- public void Deactivate() (line 506)
- public void Update() (line 535)
- public void HandleInput() (line 580)

### Navigation Input
- private void HandleNavigationInput() (line 610)

### Edit Mode Input
- private void HandleEditModeInput() (line 689)
- private void ExitEditMode() (line 794)
- private void RefreshAndReadFieldValue(bool readFull, int cursorDelta = 0) (line 799)

### Element Extraction
- private void ExtractElements() (line 862)
- private void OnElementsExtracted(JSONNode result) (line 885)
- private void OnExtractionError(Exception ex) (line 995)

### Page Load Handling
- private void OnPageLoad(JSONNode loadData) (line 1006)
- private void ScheduleRescan(float delay) (line 1024)

### Element Navigation
- private void MoveElement(int direction) (line 1034)
- private void AnnounceCurrentElement() (line 1055)
- private string FormatElementAnnouncement(WebElement elem, int index, int total) (line 1094)
- private string FormatRole(WebElement elem) (line 1133)

### Element Activation
- private void ActivateCurrentElement() (line 1159)
- private void EnterEditMode(WebElement elem) (line 1208)
- private void ClickElement(WebElement elem) (line 1222)

### CAPTCHA Detection
- private void CheckForCaptcha() (line 1249)

### Back to Arena
- private void FindBackToArenaButton(GameObject panel) (line 1339)
- private void ClickBackToArena() (line 1381)
