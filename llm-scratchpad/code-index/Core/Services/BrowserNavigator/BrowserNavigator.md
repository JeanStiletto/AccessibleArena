# BrowserNavigator.cs
Path: src/Core/Services/BrowserNavigator/BrowserNavigator.cs
Lines: 2220

## Top-level comments
- File-level XML summary: "Navigator for browser UIs in the duel scene. Orchestrates browser detection and navigation: uses BrowserDetector for finding active browsers, delegates zone-based navigation (Scry/London) to BrowserZoneNavigator, handles generic browsers (YesNo, Dungeon, etc.) directly."
- Core partial. Owns the class's base plumbing (ctor, lifecycle, generic input dispatch, element discovery, announcements, generic navigation/activation, button clicking helpers). Feature partials (AssignDamage, Keyword, Workflow, OrderCards, SelectGroup, MultiZone) extend this class with feature-specific state + methods.

## public partial class BrowserNavigator (line 23)
Note: No base class — BrowserNavigator is a standalone class (not a BaseNavigator subclass). Managed by DuelNavigator.

### Fields
- private readonly IAnnouncementService _announcer (line 25)
- private readonly BrowserZoneNavigator _zoneNavigator (line 26)
- private readonly ZoneNavigator _duelZoneNavigator (line 27)
- private static bool _isActive (line 30) — static so EventSystemPatch can block Submit while browser is active
- private bool _hasAnnouncedEntry (line 31)
- private float _announceSettleTimer (line 32) — delays announcement for scaffold-less browsers to avoid transient states
- private BrowserInfo _browserInfo (line 33)
- private List<GameObject> _browserCards (line 36)
- private List<GameObject> _browserButtons (line 37)
- private int _currentCardIndex (line 38)
- private int _currentButtonIndex (line 39)
- private bool _viewDismissDismissed (line 42)
- private bool _pendingRescan (line 45) — forces re-entry when scaffold is reused for a new interaction
- private bool _isChoiceList (line 48)
- private bool _isHighlightFilteredBrowser (line 51)
- private const string ZoneLocalHand (line 54)
- private const int HighlightTypeHot (line 1489) — CDC HighlightType value 3 (selectable target)
- private const int HighlightTypeSelected (line 1490) — CDC HighlightType value 5 (already toggled/selected)
- private static MethodInfo _currentHighlightMethod (line 1491, static)
- private static bool _currentHighlightSearched (line 1492, static)
- private const string RepeatSelectionsHolder (line 1589)
- private static FieldInfo _browserHeaderSubheaderField (line 1715, static)
- private static bool _browserHeaderReflectionInit (line 1716, static)

### Properties
- public static bool IsActive => _isActive (line 65)
- public string ActiveBrowserType => _browserInfo?.BrowserType (line 66)
- public BrowserZoneNavigator ZoneNavigator => _zoneNavigator (line 67)

### Methods
- public BrowserNavigator(IAnnouncementService announcer, ZoneNavigator duelZoneNavigator) (line 56)
- public string GetTutorialHint() (line 70) — returns tutorial hint for current browser type via LocaleManager
- private string GetBrowserHintKey() (line 76) — maps current browser type to a locale key string; checks all browser-type flags in priority order
- public void ResetMulliganState() (line 124)
- public void Update() (line 132) — per-frame browser detection; handles type/scaffold/rescan changes; auto-dismisses ViewDismiss; calls EnterBrowserMode/ExitBrowserMode
- private void EnterBrowserMode(BrowserInfo browserInfo) (line 197) — sets _isActive, claims ZoneType.Browser, activates zone navigator for zone-based browsers, detects special browser types, calls DiscoverBrowserElements
- private void ExitBrowserMode() (line 263) — clears all state fields, deactivates zone navigator, calls BrowserDetector.InvalidateCache and DuelAnnouncer.OnLibraryBrowserClosed
- private void AutoDismissViewDismiss(BrowserInfo browserInfo) (line 332) — scans for Dismiss/Done/Close child button and activates it via UIActivator
- public bool HandleInput() (line 368) — main per-frame input handler; routes to AssignDamage/Keyword/ZoneBased/MultiZone sub-handlers; handles Tab, Left/Right, Up/Down, Enter, Space, Backspace
- private void DiscoverBrowserElements() (line 826) — populates _browserCards and _browserButtons; handles workflow/SelectGroup/KeywordSelection/Mulligan/OrderCards/MultiZone special cases; filters invisible and ViewBattlefield buttons
- private void DiscoverMulliganCards() (line 1002)
- private void DiscoverCardsInHolders() (line 1025) — scans BrowserCardHolder_Default and BrowserCardHolder_ViewDismiss for card GOs
- private void DiscoverBrowserButtons() (line 1051) — scans GOs named "Browser*" or "Prompt*" for clickable buttons
- private void DiscoverMulliganButtons() (line 1065) — searches for KeepButton and MulliganButton GOs
- private void DiscoverPromptButtons() (line 1083) — fallback: finds PromptButton_Primary/Secondary GOs
- private void FindButtonsInContainer(GameObject root) (line 1105) — scans all children matching BrowserDetector.ButtonPatterns for clickable components
- private void DiscoverLargeScrollListChoices(GameObject root) (line 1125) — discovers text-named choice buttons in LargeScrollList/SelectNCounters scaffolds; deduplicates by text; sets _isChoiceList=true
- private void SearchForCardsInContainer(GameObject container, string containerName) (line 1185)
- private void SearchForCardsInLocalHand(GameObject localHandZone) (line 1210) — filters by zone=="Hand", sorts by x position
- private void AnnounceBrowserState() (line 1265) — announces entry message (SelectGroup/Keyword/AssignDamage/Mulligan/London/RepeatSelection variants); auto-navigates to first item
- private void AnnounceCurrentCard() (line 1382) — announces card name + selection state + position; handles AssignDamage/SelectGroup/RepeatSelection variants; calls CardNavigator.PrepareForCard
- private int GetCardHighlightValue(GameObject card) (line 1497) — reads CDC.CurrentHighlight() via cached reflection; returns int or -1
- private string GetCardCDCSelectionState(GameObject card) (line 1528) — returns Strings.Selected if HighlightType==Selected(5), else null
- private bool IsCardSelectable(GameObject card) (line 1537) — in highlight-filtered browsers requires Hot(3) or Selected(5) highlight
- private int FindNextSelectableCard(int fromIndex) (line 1548)
- private int FindPreviousSelectableCard(int fromIndex) (line 1562)
- private int FindFirstSelectableCard() (line 1575)
- private int FindLastSelectableCard() (line 1583)
- private string GetRepeatSelectionState(GameObject card) (line 1595) — walks parent hierarchy for RepeatSelectionsHolder name
- private void GetRepeatSelectionPosition(GameObject card, out int index, out int total) (line 1613) — counts option cards vs selected copies for position display
- private bool IsInDefaultHolder(GameObject card) (line 1653) — walks parent hierarchy for BrowserDetector.HolderDefault name
- private bool IsInRepeatSelectionsHolder(GameObject card) (line 1667) — walks parent hierarchy for RepeatSelectionsHolder name
- private string BuildGroupedHandSummary() (line 1684) — builds "3x CardA, CardB, 2x CardC" deduplicated summary from _browserCards; preserves first-seen order
- private string ExtractBrowserHeaderText() (line 1723) — finds BrowserHeader component, reads protected 'subheader' TMP field via cached reflection; returns stripped text or null
- private void AnnounceCurrentButton() (line 1772) — overrides GroupAButton/GroupBButton labels for SelectGroup; appends position
- private void NavigateToNextCard() (line 1809) — wraps with modulo
- private void NavigateToPreviousCard() (line 1821) — wraps to last on underflow
- private void TabToNextCard() (line 1838) — wraps; skips non-selectable in highlight-filtered browsers
- private void TabToPreviousCard() (line 1865) — wraps; skips non-selectable in highlight-filtered browsers
- private void NavigateToNextButton() (line 1888) — wraps with modulo
- private void NavigateToPreviousButton() (line 1896) — wraps to last on underflow
- private void NavigateToNextItem() (line 1910) — unified cards+buttons navigation (OptionalAction); maintains mutual exclusion of card/button index
- private void NavigateToPreviousItem() (line 1947) — unified cards+buttons navigation (OptionalAction); maintains mutual exclusion of card/button index
- private void ActivateCurrentCard() (line 1989) — zone-based browsers use BrowserZoneNavigator.ActivateCardFromGenericNavigation; others use SimulatePointerClick; starts AnnounceRepeatSelectionAfterDelay or AnnounceStateChangeAfterDelay
- private IEnumerator AnnounceRepeatSelectionAfterDelay(bool wasSelected) (line 2039) — WaitForSeconds(0.2f); reads updated BrowserHeader subheader text
- private IEnumerator AnnounceStateChangeAfterDelay(string cardName, bool wasSelected) (line 2057) — WaitForSeconds(0.2f); re-finds card by name in holders; updates _browserCards reference
- private void ActivateCurrentButton() (line 2103) — choice-list uses ActivateViaCustomButtonClick; others use SimulatePointerClick
- private void RefreshBrowserButtons() (line 2138) — removes destroyed buttons, re-scans scaffold + mulligan + prompt buttons; re-announces
- private void ClearEventSystemSelection() (line 2192) — sets EventSystem.current.currentSelectedGameObject to null so scaffold buttons don't steal focus
- public GameObject GetCurrentCard() (line 2204) — returns zone navigator's CurrentCard if zone-based, else _browserCards[_currentCardIndex]
