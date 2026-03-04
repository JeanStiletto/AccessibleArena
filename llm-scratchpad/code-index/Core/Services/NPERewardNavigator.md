# NPERewardNavigator.cs Code Index

## Summary
Navigator for the NPE (New Player Experience) reward screen. Shows unlocked cards after completing tutorial objectives. Uses Left/Right for navigation (like pack opening), Up/Down for card details.

## Classes

### class NPERewardNavigator : BaseNavigator (line 15)
```
private GameObject _rewardsContainer (line 17)
private int _totalCards (line 18)
private bool _isDeckReward (line 19)
private string _lastDetectState (line 20)

public override string NavigatorId => "NPEReward" (line 22)
public override string ScreenName => GetScreenName() (line 23)
public override int Priority => 75 (line 24)

public NPERewardNavigator(IAnnouncementService announcer) (line 26)
private void Log(string message) (line 28)
private string GetScreenName() (line 30)
private string GetPath(Transform t) (line 43)
protected override bool DetectScreen() (line 54)
private void LogStateChange(string newState, string message = null) (line 160)
protected override void DiscoverElements() (line 168)
private void FindCardEntries(HashSet<GameObject> addedObjects) (line 191)
private void FindDeckEntries(HashSet<GameObject> addedObjects) (line 309)
private void FindTakeRewardButton(HashSet<GameObject> addedObjects) (line 405)
private void LogCustomButtonDetails(MonoBehaviour customButton) (line 478)
protected override string GetActivationAnnouncement() (line 514)
protected override void HandleInput() (line 525)
private void LogCurrentState() (line 606)
protected override bool ValidateElements() (line 634)
public override void OnSceneChanged(string sceneName) (line 646)
```
