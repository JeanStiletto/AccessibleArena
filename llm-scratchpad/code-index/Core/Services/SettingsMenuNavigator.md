# SettingsMenuNavigator.cs
Path: src/Core/Services/SettingsMenuNavigator.cs
Lines: 1117

## Top-level comments
- Dedicated navigator for the Settings menu. Works in all scenes including duels. Priority 90 ensures it takes over when the settings panel is visible.

## public class SettingsMenuNavigator : BaseNavigator (line 17)
### Fields
- private static readonly string[] SettingsPanelNames (line 22) — Note: five "Content - *" panel names for MainMenu/Gameplay/Graphics/Audio/Account submenus.
- private const float RescanDelaySeconds = 0.3f (line 31)
- private GameObject _settingsContentPanel (line 37)
- private GameObject _settingsMenuObject (line 38) — Note: fallback for Login scene (no content panels).
- private string _lastPanelName (line 39)
- private float _rescanDelay (line 40)
- private bool _silentRescan (line 41) — Note: suppresses screen re-announcement on silent rescans (e.g. after toggle).
- private bool _pendingDropdownAnnounce (line 42)
- private bool _prevInDropdownOrSuppressed (line 43)
- private GameObject _pendingDropdownObject (line 44)
- private bool _isInQuickMenu (line 47)
- private GameObject _optionsVirtualElement (line 48) — Note: carrier GameObject for the virtual Options button in the quick menu.
- private readonly WebBrowserAccessibility _webBrowser (line 51)
- private bool _isWebBrowserActive (line 52)

### Properties
- public override string NavigatorId (line 56)
- public override string ScreenName (line 57)
- public override int Priority (line 60)

### Methods
- public SettingsMenuNavigator(IAnnouncementService announcer) (line 62)
- protected override bool DetectScreen() (line 73)
- private string GetSettingsScreenName() (line 94)
- public override void Update() (line 123)
- protected override void TryActivate() (line 198)
- protected override bool ValidateElements() (line 224)
- protected override void OnActivated() (line 242)
- protected override void OnDeactivating() (line 252)
- protected override void OnPopupClosed() (line 272)
- protected override bool IsPopupExcluded(PanelInfo panel) (line 280)
- private void OnPanelChanged(PanelInfo oldPanel, PanelInfo newPanel) (line 288)
- private static bool IsWebBrowserPanel(PanelInfo panel) (line 307)
- protected override void DiscoverElements() (line 317)
- private void ApplyQuickMenuFilter() (line 345)
- private void DiscoverAllElements() (line 387)
- private void FindSettingsCustomControls(System.Action<GameObject> tryAddElement) (line 555)
- private GameObject FindClickableInDropdownControl(GameObject control) (line 626)
- private void DiscoverStaticTextBlocks(GameObject searchRoot, HashSet<GameObject> interactiveObjects) (line 658)
- private string BuildAnnouncement(UIElementClassifier.ClassificationResult classification) (line 686)
- private static bool IsChildOf(GameObject child, GameObject parent) (line 697)
- protected override bool HandleCustomInput() (line 715)
- private bool HandleSettingsBack() (line 733)
- private GameObject FindSettingsBackButton() (line 781)
- private GameObject FindActiveSettingsPanel() (line 806)
- private GameObject FindSettingsMenuObject() (line 844)
- private bool CloseSettingsMenu() (line 857)
- protected override bool OnElementActivated(int index, GameObject element) (line 892)
- private bool IsSettingsSubmenuButton(GameObject element) (line 953)
- private static bool IsSettingsLinkButton(GameObject element) (line 978)
- private static bool TryInvokeCustomButtonClick(GameObject element) (line 997)
- private void TriggerRescan() (line 1025)
- private void PerformRescan() (line 1030)
- protected override string GetActivationAnnouncement() (line 1104)
