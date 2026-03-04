# SettingsMenuNavigator.cs

## Overview
Dedicated navigator for Settings menu. Works in all scenes including duels.
Priority 90 ensures it takes over when settings panel is visible.

## Class: SettingsMenuNavigator : BaseNavigator (line 16)

### Configuration
- private static readonly string[] SettingsPanelNames (line 21)
- private const float RescanDelaySeconds (line 29)

### State
- private GameObject _settingsContentPanel (line 35)
- private GameObject _settingsMenuObject (line 36)
- private string _lastPanelName (line 37)
- private float _rescanDelay (line 38)

### Properties
- public override string NavigatorId (line 42)
- public override string ScreenName (line 43)
- public override int Priority (line 46)

### Constructor
- public SettingsMenuNavigator(IAnnouncementService announcer) (line 48)

### Screen Detection
- protected override bool DetectScreen() (line 59)
  - Uses Harmony-tracked panel state for precise detection
- private string GetSettingsScreenName() (line 88)

### Lifecycle
- public override void Update() (line 113)
- protected override void TryActivate() (line 145)
  - Custom activation: allows activating with 0 elements
- protected override bool ValidateElements() (line 171)
- protected override void OnActivated() (line 197)
- protected override void OnDeactivating() (line 204)
- protected override void OnPopupClosed() (line 214)

### Element Discovery
- protected override void DiscoverElements() (line 223)
- private void FindSettingsCustomControls(System.Action<GameObject> tryAddElement) (line 339)
- private GameObject FindClickableInDropdownControl(GameObject control) (line 410)
- private string BuildAnnouncement(UIElementClassifier.ClassificationResult classification) (line 438)
- private static bool IsChildOf(GameObject child, GameObject parent) (line 443)

### Input Handling
- protected override bool HandleCustomInput() (line 461)
- private bool HandleSettingsBack() (line 479)
  - Priority: popup -> submenu -> close settings
- private GameObject FindSettingsBackButton() (line 520)
- private GameObject FindSettingsMenuObject() (line 544)
- private bool CloseSettingsMenu() (line 557)

### Element Activation
- protected override bool OnElementActivated(int index, GameObject element) (line 594)
- private bool IsSettingsSubmenuButton(GameObject element) (line 611)

### Rescan
- private void TriggerRescan() (line 636)
- private void PerformRescan() (line 641)

### Announcement
- protected override string GetActivationAnnouncement() (line 682)
