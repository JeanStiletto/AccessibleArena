# MasteryNavigator.cs (core partial)
Path: src/Core/Services/MasteryNavigator/MasteryNavigator.cs
Lines: 322

## Top-level comments
- Standalone navigator for the MTGA Mastery/Rewards screen (RewardTrack scene). Split 10/12 (2026-04-20) into 4 partials under `MasteryNavigator/`: core (this file), Levels, PrizeWall, ConfirmationModal.
- Core partial holds: mode routing, lifecycle (OnActivated/OnDeactivating/OnSceneChanged), Update loop with modal polling, Input routing, Screen detection orchestration.

## public partial class MasteryNavigator : BaseNavigator (line 18)

### Fields
- private const int MasteryPriority = 60 (line 22)
- private enum MasteryMode { Levels, PrizeWall } (line 28)
- private MasteryMode _mode (line 29)

### Navigator Identity (overrides)
- public override string NavigatorId => "Mastery" (line 35)
- public override string ScreenName (line 36) — switches on _mode
- public override int Priority => MasteryPriority (line 37)
- protected override bool SupportsCardNavigation => false (line 38)
- protected override bool AcceptSpaceKey => false (line 39)

### Constructor
- public MasteryNavigator(IAnnouncementService announcer) (line 45)

### Screen Detection
- protected override bool DetectScreen() (line 53) — Routes to Levels (ProgressionTracksContentController) or PrizeWall (ContentController_PrizeWall) via Find+IsOpen helpers in respective partials
- protected override bool IsPopupExcluded(PanelInfo panel) (line 82) — excludes ObjectivePopup, FullscreenZFBrowser, Rewards
- protected override void OnPopupClosed() (line 93) — re-announces current position

### Element Discovery
- protected override void DiscoverElements() (line 106) — routes to DiscoverPrizeWallItems or BuildLevelData/BuildActionButtons/InsertStatusItem

### Activation & Deactivation
- protected override void OnActivated() (line 132) — mode-aware init of _prizeWallIndex or _currentLevelIndex
- protected override void OnDeactivating() (line 149) — clears levels, prize wall items, modal state
- public override void OnSceneChanged(string sceneName) (line 162) — resets controllers + reflection flags

### Announcements (mode-aware)
- public override string GetTutorialHint() (line 178) — returns LocaleManager "MasteryHint"
- protected override string GetActivationAnnouncement() (line 180) — PrizeWall vs Levels activation string
- protected override string GetElementAnnouncement(int index) (line 206) — returns "" (mod uses own announcements)

### Update Loop
- public override void Update() (line 216) — Note: polls `_confirmationModalGameObject.activeInHierarchy` for modal open/close transitions, dispatches DiscoverConfirmationModalElements/AnnounceConfirmationModal → AnnouncePrizeWallItem
- protected override bool ValidateElements() (line 297) — mode-aware

### Input Routing
- private void HandleMasteryInput() (line 309) — routes to HandlePrizeWallInput or HandleLevelInput
