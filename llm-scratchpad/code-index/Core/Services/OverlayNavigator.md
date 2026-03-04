# OverlayNavigator.cs Code Index

## Summary
Handles modal overlays that appear on top of other screens. Detects overlays by looking for Background_ClickBlocker and similar modal patterns. Examples: What's New carousel, announcements, reward popups.

## Classes

### class OverlayNavigator : BaseNavigator (line 16)
```
private GameObject _overlayBlocker (line 18)
private string _overlayType (line 19)

public override string NavigatorId => "Overlay" (line 21)
public override string ScreenName => GetOverlayScreenName() (line 22)
public override int Priority => 85 (line 23)

public OverlayNavigator(IAnnouncementService announcer) (line 25)
private string GetOverlayScreenName() (line 27)
protected override bool DetectScreen() (line 38)
private void DetermineOverlayType() (line 55)
protected override void DiscoverElements() (line 84)
private void DiscoverWhatsNewElements(HashSet<GameObject> addedObjects) (line 102)
private void DiscoverRewardElements(HashSet<GameObject> addedObjects) (line 154)
private void FindRewardCards(HashSet<GameObject> addedObjects) (line 167)
  // NOTE: Finds reward cards displayed on rewards screen
private void DiscoverGenericOverlayElements(HashSet<GameObject> addedObjects) (line 248)
private void FindDismissButtons(HashSet<GameObject> addedObjects) (line 276)
private string ExtractMainContent(List<TMPro.TMP_Text> texts) (line 335)
private string ExtractContainerText(GameObject container) (line 363)
private string CleanText(string text) (line 376)
private string CleanButtonName(string name) (line 384)
protected override string GetActivationAnnouncement() (line 392)
public override void OnSceneChanged(string sceneName) (line 405)
protected override bool ValidateElements() (line 419)
```
