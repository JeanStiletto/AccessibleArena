# BoosterOpenNavigator.cs

Navigator for the booster pack card list that appears after opening a pack.
Detects BoosterOpenToScrollListController and makes cards navigable.

## Class: BoosterOpenNavigator : BaseNavigator (line 15)

### Fields
- GameObject _scrollListController (line 17)
- GameObject _revealAllButton (line 18)
- int _totalCards (line 19)
- int _rescanFrameCounter (line 22)
- const int RescanDelayFrames = 90 (line 23)
- bool _rescanDone (line 24)
- bool _closeTriggered (line 27)
- int _closeRescanCounter (line 28)

### Properties
- string NavigatorId => "BoosterOpen" (line 30)
- string ScreenName => GetScreenName() (line 31)
- int Priority => 80 (line 32)

### Constructor
- BoosterOpenNavigator(IAnnouncementService announcer) (line 34)

### Helper Methods
- string GetScreenName() (line 36)

### Detection Methods
- protected override bool DetectScreen() (line 43)

### Discovery Methods
- protected override void DiscoverElements() (line 101)
- void FindRevealAllButton(HashSet<GameObject> addedObjects) (line 116)
- void FindCardEntries(HashSet<GameObject> addedObjects) (line 136)
- void FindCardsInCardScroller(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects) (line 210)
  Note: Path: CardScroller/Viewport/Centerer/Content/Prefab - BoosterMetaCardView_v2
- void FindCardsInEntryRoots(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects) (line 312)
- string ExtractCardName(GameObject cardObj) (line 348)
- GameObject FindCardInEntry(GameObject entry) (line 442)
- void FindCardsDirectly(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects) (line 472)
- void FindDismissButton(HashSet<GameObject> addedObjects) (line 500)
- IEnumerable<GameObject> FindCustomButtonsInScene() (line 547)
- bool IsInBoosterChamber(GameObject obj) (line 563)
- string CleanButtonName(string name) (line 576)
- int GetCardGrpId(GameObject cardObj) (line 588)
  Note: Gets GrpId from card object for deduplication
- string GetParentPath(Transform t, int levels) (line 624)

### Navigation Methods
- protected override string GetActivationAnnouncement() (line 637)
- protected override void HandleInput() (line 643)

### Close Methods
- void ClosePackProperly() (line 751)
  Note: Priority: Dismiss_MainButton > controller DismissCards > ModalFade
- System.Collections.IEnumerator ClickDismissAfterDelay() (line 806)
- void StopPackMusic() (line 832)
- bool TryInvokeSkipToEnd() (line 852)
  Note: Uses method iteration instead of GetMethod for IL2CPP compatibility
- bool TryClosePackContents() (line 942)
- Component FindBoosterController() (line 1011)

### Lifecycle Methods
- public override void Update() (line 1046)
  Note: Single rescan after ~1.5 seconds to catch all revealed cards
- protected override void OnActivated() (line 1094)
- void TriggerCloseRescan() (line 1106)
- protected override bool ValidateElements() (line 1114)
- public override void OnSceneChanged(string sceneName) (line 1126)
