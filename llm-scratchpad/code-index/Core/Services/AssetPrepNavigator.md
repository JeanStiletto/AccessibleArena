# AssetPrepNavigator.cs
Path: src/Core/Services/AssetPrepNavigator.cs
Lines: 390

## public class AssetPrepNavigator : BaseNavigator (line 22)

Navigator for the AssetPrep (download) screen shown on fresh install. Designed to fail gracefully; low priority so login screens take over immediately.

### Fields
- private GameObject _assetPrepScreen (line 25)
- private Component _assetPrepScreenComponent (line 26)
- private TMP_Text _infoText (line 29)
- private TMP_Text _buildVersionText (line 30)
- private Button _downloadButton (line 31)
- private Button _retryButton (line 32)
- private Button _withoutDownloadButton (line 33)
- private string _lastAnnouncedText = "" (line 36)
- private float _lastProgressAnnounceTime (line 37)
- private const float ProgressAnnounceInterval = 5f (line 38) — announce progress every 5 seconds

### Properties
- public override string NavigatorId => "AssetPrep" (line 40)
- public override string ScreenName => Strings.ScreenDownload (line 41)
- public override int Priority => 5 (line 44) — low so login screens take over ASAP
- protected override bool SupportsCardNavigation => false (line 47)

### Methods
- public AssetPrepNavigator(IAnnouncementService announcer) : base(announcer) (line 49)
- protected override bool DetectScreen() (line 51)
- private void TryGetUIElements() (line 94)
- private bool TryFindTextElementsFallback() (line 145)
- protected override void DiscoverElements() (line 197)
- protected override bool ValidateElements() (line 223)
- private void RefreshButtons() (line 243)
- protected override void OnActivated() (line 265)
- protected override string GetActivationAnnouncement() (line 271)
- private string GetCurrentStatusText() (line 309)
- private string CleanStatusText(string text) (line 323)
- public override void Update() (line 332)
- private void CheckProgressUpdate() (line 350)
- protected override bool HandleCustomInput() (line 375)
- public override void OnSceneChanged(string sceneName) (line 380)
