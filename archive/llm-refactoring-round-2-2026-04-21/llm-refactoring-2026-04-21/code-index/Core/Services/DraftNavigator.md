# DraftNavigator.cs
Path: src/Core/Services/DraftNavigator.cs
Lines: 1068

## Top-level comments
- Navigator for the draft card picking screen. Detects DraftContentController, enumerates DraftPackCardView cards left-to-right, reads selection state via DraftDeckManager reflection, counts drafted copies, and schedules delayed rescans after toggle/confirm/popup.

## public class DraftNavigator : BaseNavigator (line 21)
### Fields
- private GameObject _draftControllerObject (line 23)
- private int _totalCards (line 24)
- private bool _rescanPending (line 27)
- private int _rescanFrameCounter (line 28)
- private bool _initialRescanDone (line 29)
- private const int InitialRescanDelayFrames = 90 (line 30)
- private const int ToggleRescanDelayFrames = 20 (line 31)
- private const int ConfirmRescanDelayFrames = 90 (line 32)
- private const int PopupRescanDelayFrames = 15 (line 33)
- private int _currentRescanDelay = InitialRescanDelayFrames (line 34)
- private bool _isToggleRescan (line 37)
- private bool _isConfirmRescan (line 38)
- private static FieldInfo _draftDeckManagerField (line 41)
- private static MethodInfo _getDeckMethod (line 42)
- private static MethodInfo _getReservedCardsMethod (line 43)
- private static MethodInfo _isCardAlreadyReservedMethod (line 44)
- private static PropertyInfo _deckMainProp (line 45)
- private static PropertyInfo _deckSideboardProp (line 46)
- private static MethodInfo _cardCollectionQuantityMethod (line 47)
- private static PropertyInfo _useButtonOverlayProp (line 48)
- private static PropertyInfo _currentCardProp (line 49)
- private static PropertyInfo _metaCardViewCardProp (line 50)
- private static PropertyInfo _cardDataGrpIdProp (line 51)
- private static bool _reflectionInitialized (line 52)
- private bool _closeTriggered (line 987)
- private int _closeRescanCounter (line 988)
- private int _emptyCardCounter (line 989)
- private const int EmptyCardDeactivateFrames = 300 (line 990)
### Properties
- public override string NavigatorId (line 54)
- public override string ScreenName (line 55)
- public override int Priority (line 56)
### Methods
- public DraftNavigator(IAnnouncementService announcer) (line 58)
- private string GetScreenName() (line 62)
- protected override bool DetectScreen() (line 71) — Note: requires DraftContentController.IsOpen AND presence of DraftPackHolder/DraftPackCardView to distinguish from deck-building sub-screen
- private static void EnsureReflectionInitialized() (line 127)
- private object GetDraftDeckManager() (line 205)
- private int GetDraftedCopies(object draftDeckManager, MonoBehaviour cardViewMb, uint grpId) (line 233) — Note: sums main + sideboard + reserved-but-uncommitted copies
- private uint GetCardGrpId(MonoBehaviour cardViewMb) (line 317)
- protected override void DiscoverElements() (line 364)
- private void FindDraftPackCards(HashSet<GameObject> addedObjects) (line 376) — Note: sorts cards left-to-right by transform x position
- private string ExtractCardName(GameObject cardObj) (line 443)
- private string GetCardSelectedStatus(object draftDeckManager, MonoBehaviour cardViewMb) (line 471)
- private string GetCardSelectedStatusFallback(GameObject cardObj) (line 513) — Note: visual child-name heuristic used only when reflection fails
- private void FindActionButtons(HashSet<GameObject> addedObjects) (line 537)
- public override string GetTutorialHint() (line 566)
- protected override string GetActivationAnnouncement() (line 568)
- protected override void HandleInput() (line 575) — Note: handles I (ext info), F11 (debug dump), Tab/arrows/Home/End, Enter (toggle or confirm), Space (confirm), Backspace (back)
- private void ClickConfirmButton() (line 689)
- private void ClickBackButton() (line 740)
- public override void Update() (line 764) — Note: runs initial rescan (~1.5s), post-action rescans, empty-card timeout, and post-back close detection
- private void QuietRescan(bool announceSelectionOnly = false) (line 889) — Note: rediscovers without full activation announcement; preserves cursor by matching current GameObject
- private void AnnounceSelectionStatus() (line 937)
- private int PeekCardCount() (line 975)
- protected override void OnActivated() (line 992)
- protected override void OnDeactivating() (line 1007)
- protected override void OnPopupClosed() (line 1012)
- private void TriggerCloseRescan() (line 1022)
- protected override bool ValidateElements() (line 1030)
- public override void OnSceneChanged(string sceneName) (line 1041)
- private static void ClearReflectionCache() (line 1051)
