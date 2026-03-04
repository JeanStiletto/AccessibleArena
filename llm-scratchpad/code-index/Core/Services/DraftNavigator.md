# DraftNavigator.cs

Navigator for the draft card picking screen. Detects DraftContentController and makes draft pack cards navigable. Cards are DraftPackCardView inside a DraftPackHolder. Enter selects/toggles a card, Space confirms the pick.

## class DraftNavigator : BaseNavigator (line 17)

### Private Fields
- _draftControllerObject (GameObject) (line 19)
- _totalCards (int) (line 20)
- _rescanPending (bool) (line 23)
- _rescanFrameCounter (int) (line 24)
- _initialRescanDone (bool) (line 25)
- _closeTriggered (bool) (line 501)
- _closeRescanCounter (int) (line 502)
- _emptyCardCounter (int) (line 503)

### Constants
- RescanDelayFrames (int) = 90 (line 26) - ~1.5 seconds at 60fps
- EmptyCardDeactivateFrames (int) = 300 (line 504) - ~5 seconds at 60fps

### Public Properties
- NavigatorId → string (line 28)
- ScreenName → string (line 29)
- Priority → int (line 30) - 78 (below BoosterOpen 80, above General 15)

### Constructor
- DraftNavigator(IAnnouncementService) (line 32)

### Private Methods
- GetScreenName() → string (line 36)

### Protected Methods - Screen Detection (line 45)
- DetectScreen() → bool (line 45) - Note: checks DraftContentController.IsOpen and verifies pack cards
- DiscoverElements() (line 101)
- FindDraftPackCards(HashSet<GameObject>) (line 113)
- ExtractCardName(GameObject) → string (line 167)
- GetCardSelectedStatus(GameObject) → string (line 194)
- FindActionButtons(HashSet<GameObject>) (line 216)
- GetActivationAnnouncement() → string (line 245)

### Protected Methods - Input Handling (line 251)
- HandleInput() (line 252)
- ClickConfirmButton() (line 345)
- ClickBackButton() (line 390)

### Public Methods - Lifecycle (line 413)
- Update() (line 414) - Note: handles delayed rescans
- OnSceneChanged(string) (line 549)

### Protected Methods - Lifecycle (line 506)
- OnActivated() (line 506)
- OnDeactivating() (line 518)
- OnPopupClosed() (line 523)
- ValidateElements() → bool (line 538)

### Private Methods - Delayed Rescan (line 530)
- TriggerCloseRescan() (line 530)
