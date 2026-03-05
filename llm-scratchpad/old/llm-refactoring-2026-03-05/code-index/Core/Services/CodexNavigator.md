# CodexNavigator.cs

Navigator for the Codex of the Multiverse (Learn to Play) screen.
Three modes: TOC (table of contents), Content (article paragraphs), Credits.

TOC uses drill-down navigation:
- Enter on a category → shows only its children
- Backspace → returns to parent level
- Backspace at top level → navigate Home

## Enum: CodexMode (line 31)
- TableOfContents (line 31)
- Content (line 31)
- Credits (line 31)

## Struct: TocItem (line 73)
- GameObject ButtonGameObject (line 75)
  Note: CustomButton GO to click
- MonoBehaviour SectionComponent (line 76)
  Note: TableOfContentsSection (null for standalone)
- string Label (line 77)
- bool IsCategory (line 78)
  Note: has childAnchor = drillable category
- bool IsStandalone (line 79)
  Note: Replay Tutorial, Credits

## Struct: TocLevel (line 83)
- string Label (line 85)
  Note: parent category label
- int SelectedIndex (line 86)
  Note: cursor position at that level
- List<TocItem> Items (line 87)
  Note: items at that level

## Class: CodexNavigator (line 21)

### Constants (line 25)
- const int CodexPriority (line 25)

### Fields (line 32-122)
- CodexMode _mode (line 32)
- readonly List<TocItem> _tocItems (line 49)
  Note: Current visible TOC items (changes on drill-down)
- int _tocIndex (line 50)
- readonly List<TocLevel> _navStack (line 53)
  Note: Drill-down stack: each entry stores a parent level's items + position
- readonly List<string> _contentParagraphs (line 56)
- int _contentIndex (line 57)
- readonly List<string> _creditsParagraphs (line 60)
- int _creditsIndex (line 61)
- bool _pendingDrillDown (line 64)
  Note: Delayed drill-down after clicking a category (game needs time to expand children)
- float _drillDownTimer (line 65)
- MonoBehaviour _drillDownSection (line 66)
- string _drillDownLabel (line 67)
- MonoBehaviour _controller (line 94)
- GameObject _controllerGameObject (line 95)
- Type _controllerType (line 98)
- PropertyInfo _isOpenProp (line 99)
- FieldInfo _learnToPlayRootField (line 100)
- FieldInfo _tableOfContentsField (line 101)
- FieldInfo _tableOfContentsTopicsField (line 102)
- FieldInfo _contentViewField (line 103)
- FieldInfo _replayTutorialButtonField (line 104)
- FieldInfo _creditsButtonField (line 105)
- FieldInfo _creditsDisplayField (line 106)
- Type _tocSectionType (line 109)
- FieldInfo _tocButtonField (line 110)
- FieldInfo _tocIntentField (line 111)
- FieldInfo _tocChildAnchorField (line 112)
- FieldInfo _tocSectionField (line 113)
- Type _learnMoreSectionType (line 116)
- FieldInfo _sectionTitleField (line 117)
- FieldInfo _sectionIdField (line 118)
- FieldInfo _childSectionsField (line 119)
  Note: Cached from LearnMoreSection on first use
- bool _reflectionInitialized (line 121)

### Constructor (line 127)
- CodexNavigator(IAnnouncementService announcer) (line 127)

### Navigator Identity (line 37-43)
- override string NavigatorId (line 38)
- override string ScreenName (line 39)
- override int Priority (line 40)
- override bool SupportsCardNavigation (line 41)
- override bool AcceptSpaceKey (line 42)

### Screen Detection (line 132-181)
- override bool DetectScreen() (line 133)
- MonoBehaviour FindController() (line 146)
  Note: Use cached reference if still valid
- bool IsControllerOpen(MonoBehaviour controller) (line 165)

### Reflection Caching (line 185-239)
- void EnsureReflectionCached(Type controllerType) (line 186)
  Note: Scans assemblies for TableOfContentsSection and LearnMoreSection types

### Element Discovery (line 243-498)
- override void DiscoverElements() (line 245)
- void DiscoverTopLevel() (line 260)
  Note: Discover top-level TOC items (depth 0 categories + standalone buttons); clears navigation stack
- void ScanContainerForItems(Transform container) (line 285)
  Note: Scan container's direct children for TableOfContentsSection components; only scans one level to avoid nested subcategories
- MonoBehaviour FindTocSectionComponent(GameObject go) (line 317)
- void CacheTocSectionType(Type type) (line 338)
  Note: Fallback caching when namespace lookup fails
- GameObject GetCustomButtonGameObject(MonoBehaviour tocSection) (line 361)
- GameObject GetChildAnchor(MonoBehaviour tocSection) (line 378)
- bool HasChildSections(MonoBehaviour tocSection) (line 396)
  Note: Check if TOC section's LearnMoreSection has child sections (sub-category)
- string ExtractSectionLabel(MonoBehaviour tocSection, GameObject buttonGo) (line 418)
- static bool HasActiveChildren(Transform t) (line 434)
- void AddStandaloneButton(FieldInfo field, string fallbackLabel) (line 444)
- GameObject FindCustomButton(GameObject parent) (line 470)
- GameObject GetFieldGameObject(FieldInfo field) (line 481)
- static string CleanLabel(string text) (line 491)

### Content Extraction (line 501-602)
- void ExtractContentParagraphs() (line 503)
  Note: Extract TMP_Text paragraphs, skip embedded card displays
- static bool IsInsideCardDisplay(Transform textTransform, Transform stopAt) (line 541)
  Note: Check if TMP_Text element is inside embedded card display; walks up parent hierarchy
- void ExtractCreditsParagraphs() (line 570)

### State Detection (line 605-635)
- bool IsCreditsActive() (line 607)
- bool IsContentActive() (line 622)
  Note: Checks for LearnToPlayContents component

### Activation & Deactivation (line 638-664)
- override void OnActivated() (line 640)
- override void OnDeactivating() (line 648)
- override void OnSceneChanged(string sceneName) (line 657)

### Announcements (line 667-715)
- override string GetActivationAnnouncement() (line 669)
- override string GetElementAnnouncement(int index) (line 675)
- void AnnounceTocItem() (line 679)
- void AnnounceContentBlock() (line 700)
- void AnnounceCreditsBlock() (line 708)

### Update Loop (line 718-779)
- override void Update() (line 720)
  Note: Detects mode transitions and handles controller validity
- override bool ValidateElements() (line 775)

### Mode Switching (line 782-830)
- void SwitchToContentMode() (line 784)
- void SwitchToCreditsMode() (line 799)
- void ReturnToToc() (line 818)
  Note: Return to TOC mode from content/credits; preserves drill-down level and position

### Drill-Down Navigation (line 833-917)
- void DrillDown(MonoBehaviour parentSection, string parentLabel) (line 839)
  Note: Drill into category: push current items to stack, scan children as new list
- void PopNavStack() (line 899)
  Note: Pop back to parent level from navigation stack

### Input Handling (line 920-1168)
- void HandleTocInput() (line 922)
  Note: Up/W/Shift+Tab (prev), Down/S/Tab (next), Home/End (jump), Enter (activate), Backspace (back/home)
- void ActivateTocItem() (line 1000)
  Note: Standalone buttons activated immediately; categories schedule drill-down after 0.4s delay
- void HandleContentInput() (line 1028)
  Note: Up/Down (navigate paragraphs), Home/End (jump), Backspace (close)
- void HandleCreditsInput() (line 1103)

### Close Content / Credits (line 1171-1246)
- void CloseContent() (line 1173)
  Note: Find LearnToPlayContents component and click backButton
- void CloseCredits() (line 1214)

### Main Input Dispatch (line 1249-1279)
- void HandleCodexInput() (line 1251)
  Note: Handles pending drill-down timer and dispatches to mode-specific input handlers
