# BoosterOpenNavigator.cs
Path: src/Core/Services/BoosterOpenNavigator.cs
Lines: 1494

## public class BoosterOpenNavigator : BaseNavigator (line 21)

Navigator for the booster pack card list shown after opening a pack. Uses the controller's _cardsToOpen field as authoritative detection source and GrpId-based lookup for card names.

### Fields
- private Component _controller (line 23)
- private GameObject _revealAllButton (line 24)
- private int _totalCards (line 25)
- private int _expectedCardCount (line 26)
- private static FieldInfo _cardsToOpenField (line 29)
- private static PropertyInfo _hiddenProp (line 30)
- private static FieldInfo _onScreenHoldersField (line 31)
- private static PropertyInfo _cardViewsProp (line 32)
- private static FieldInfo _autoRevealField (line 33) — CardDataAndRevealStatus.AutoReveal
- private static FieldInfo _cardDataField (line 36) — CardDataAndRevealStatus.CardData
- private static PropertyInfo _grpIdProp (line 37) — CardData.GrpId
- private static PropertyInfo _rarityProp (line 38) — CardData.Rarity
- private static PropertyInfo _revealedProp (line 39) — CardDataAndRevealStatus.Revealed
- private Dictionary<GameObject, int> _cardDataIndices (line 40)
- private List<int> _elementDataIndex (line 41) — parallel to _elements: _cardsToOpen index, -1 for non-card
- private static MethodInfo _setRTPCMethod (line 45) — AudioManager.SetRTPCValue
- private static MethodInfo _playAudioStringMethod (line 46) — AudioManager.PlayAudio(string, GameObject)
- private static FieldInfo _chamberSetCodeField (line 47)
- private bool _packMusicRestored (line 48)
- private int _rescanFrameCounter (line 51)
- private const int RescanIntervalFrames = 30 (line 52) — ~0.5s at 60fps
- private int _rescanAttempt (line 53)
- private const int MaxRescanAttempts = 20 (line 54) — ~10s total
- private bool _rescanDone (line 55)
- private bool _closeTriggered (line 58)
- private int _closeRescanCounter (line 59)

### Properties
- public override string NavigatorId => "BoosterOpen" (line 61)
- public override string ScreenName => GetScreenName() (line 62)
- public override int Priority => 80 (line 63) — higher than GeneralMenuNavigator (15), below OverlayNavigator (85)

### Methods
- public BoosterOpenNavigator(IAnnouncementService announcer) : base(announcer) (line 65)
- private string GetScreenName() (line 67)
- protected override bool DetectScreen() (line 74)
- private Component FindScrollListController(GameObject boosterChamber) (line 115)
- private IList GetCardsToOpen(Component controller) (line 129)
- protected override void DiscoverElements() (line 143)
- private void FindRevealAllButton(HashSet<GameObject> addedObjects) (line 163)
- private void FindCardEntries(HashSet<GameObject> addedObjects) (line 183)
- private Dictionary<int, Component> GetOnScreenHolders() (line 264) — reads _onScreenboosterCardHoldersWithIndex
- private GameObject GetFirstCardView(Component holder) (line 289)
- private string ExtractCardName(GameObject cardObj) (line 307) — reads Title TMP; falls back to vault progress labels
- private bool IsCardHidden(GameObject cardObj) (line 407) — reads BoosterCardHolder.Hidden on parent
- protected override bool IsCurrentCardHidden(GameObject cardElement) (line 432) — prefers data-driven Revealed flag
- private GameObject FindBoosterCardHolder(GameObject cardObj) (line 456)
- private void FindDismissButton(HashSet<GameObject> addedObjects) (line 471)
- private IEnumerable<GameObject> FindCustomButtonsInScene() (line 525)
- private bool IsInBoosterChamber(GameObject obj) (line 541)
- private string CleanButtonName(string name) (line 554)
- private int GetCardGrpId(GameObject cardObj) (line 566) — reads GrpId from Meta_CDC
- private void UpdateCardInfoForOffScreenCard() (line 602) — provides card info blocks for TextBlock elements
- protected override void Move(int direction) (line 637)
- protected override void MoveFirst() (line 643)
- protected override void MoveLast() (line 649)
- public override string GetTutorialHint() (line 655)
- protected override string GetActivationAnnouncement() (line 657)
- protected override void HandleInput() (line 664)
- private void ClosePackProperly() (line 825) — calls BoosterChamberController.DismissCards via reflection
- private void StopPackMusic() (line 868) — sends PointerExit to Hitbox_BoosterMesh
- private void RestorePackMusic() (line 889) — calls AudioManager.SetRTPCValue for booster_packrollover and boosterpack_<set>
- private void ForceRevealAllCards() (line 941) — sets Revealed=true on all _cardsToOpen entries, calls UpdateRevealed
- private void CallUpdateRevealed() (line 974)
- private bool TryClosePackContents() (line 1004) — calls DismissCards/Close/OnCloseClicked/Hide/Dismiss
- private bool TryCloseChamberController() (line 1071) — calls DismissCards on BoosterChamberController
- private Component FindBoosterController() (line 1118) — returns cached or re-finds controller
- private void ClearAutoReveal() (line 1143) — sets AutoReveal=false on all _cardsToOpen entries
- private uint GetGrpIdFromEntry(object entry) (line 1174)
- private bool IsEntryRevealed(object entry) (line 1197)
- private CardInfo? GetCardInfoFromData(int dataIndex) (line 1213) — uses CardModelProvider.GetCardInfoFromGrpId
- private void RevealCardByData(int dataIndex) (line 1230) — sets Revealed=true, plays flip sound, announces name
- private void PlayFlipSoundForEntry(object entry) (line 1268) — picks Wwise event by rarity (mythic/rare/common)
- public override void ForceRescan() (line 1308) — preserves cursor via _elementDataIndex across rescans
- public override void Update() (line 1377) — periodic rescan until cards found + post-close rescan
- protected override void OnActivated() (line 1440)
- private void TriggerCloseRescan() (line 1456)
- protected override bool ValidateElements() (line 1464)
- public override void OnSceneChanged(string sceneName) (line 1484)
