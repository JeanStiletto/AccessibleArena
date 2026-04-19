# OverlayNavigator.cs
Path: src/Core/Services/OverlayNavigator.cs
Lines: 494

## Top-level comments
- Handles modal overlays that appear on top of other screens. Detects overlays by looking for Background_ClickBlocker and similar modal patterns. Examples: What's New carousel, announcements, reward popups, embedded web browser.

## public class OverlayNavigator : BaseNavigator (line 17)

### Fields
- private GameObject _overlayBlocker (line 19)
- private string _overlayType (line 20)
- private readonly WebBrowserAccessibility _webBrowser = new WebBrowserAccessibility() (line 21)
- private GameObject _browserCanvas (line 22)

### Properties
- public override string NavigatorId => "Overlay" (line 24)
- public override string ScreenName => GetOverlayScreenName() (line 25)
- public override int Priority => 85 (line 26)

### Methods
- public OverlayNavigator(IAnnouncementService announcer) (line 28)
- private string GetOverlayScreenName() (line 30)
- protected override bool DetectScreen() (line 42)
- private void DetermineOverlayType() (line 59) — Classifies as WebBrowser, WhatsNew, Reward, or Announcement
- protected override void DiscoverElements() (line 105)
- private void DiscoverWebBrowserElements() (line 126)
- protected override bool HandleEarlyInput() (line 133)
- public override void Update() (line 143)
- protected override void OnDeactivating() (line 152)
- private void DiscoverWhatsNewElements(HashSet<GameObject> addedObjects) (line 161)
- private void DiscoverRewardElements(HashSet<GameObject> addedObjects) (line 213)
- private void FindRewardCards(HashSet<GameObject> addedObjects) (line 226)
- private void DiscoverGenericOverlayElements(HashSet<GameObject> addedObjects) (line 307)
- private void FindDismissButtons(HashSet<GameObject> addedObjects) (line 335)
- private string ExtractMainContent(List<TMPro.TMP_Text> texts) (line 394)
- private string CleanText(string text) (line 422)
- private string CleanButtonName(string name) (line 430)
- protected override string GetActivationAnnouncement() (line 438)
- public override void OnSceneChanged(string sceneName) (line 457)
- protected override bool ValidateElements() (line 471)
