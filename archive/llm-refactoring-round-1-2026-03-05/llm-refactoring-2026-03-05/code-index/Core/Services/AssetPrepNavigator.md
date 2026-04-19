# AssetPrepNavigator.cs

Navigator for the AssetPrep (download) screen shown on fresh install.
SAFETY: Designed to fail gracefully rather than block users.

## Class: AssetPrepNavigator : BaseNavigator (line 21)

### Constants
- const string SCENE_NAME = "AssetPrep" (line 24)
- const float ProgressAnnounceInterval = 5f (line 38)

### Properties
- string NavigatorId => "AssetPrep" (line 40)
- string ScreenName => Strings.ScreenDownload (line 41)
- int Priority => 5 (line 44)
- protected override bool SupportsCardNavigation => false (line 47)

### Fields
- GameObject _assetPrepScreen (line 25)
- Component _assetPrepScreenComponent (line 26)
- TMP_Text _infoText (line 29)
- TMP_Text _buildVersionText (line 30)
- Button _downloadButton (line 31)
- Button _retryButton (line 32)
- Button _withoutDownloadButton (line 33)
- string _lastAnnouncedText (line 36)
- float _lastProgressAnnounceTime (line 37)

### Constructor
- AssetPrepNavigator(IAnnouncementService announcer) (line 49)

### Detection Methods
- protected override bool DetectScreen() (line 52)
- void TryGetUIElements() (line 94)
- bool TryFindTextElementsFallback() (line 145)

### Discovery Methods
- protected override void DiscoverElements() (line 197)
- void RefreshButtons() (line 243)

### Lifecycle Methods
- protected override void OnActivated() (line 265)
- protected override string GetActivationAnnouncement() (line 271)
- protected override bool ValidateElements() (line 223)
- public override void Update() (line 332)
- protected override bool HandleCustomInput() (line 375)
- public override void OnSceneChanged(string sceneName) (line 380)

### Helper Methods
- string GetCurrentStatusText() (line 309)
- string CleanStatusText(string text) (line 323)
- void CheckProgressUpdate() (line 350)
