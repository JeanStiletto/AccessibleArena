# NPERewardNavigator.cs
Path: src/Core/Services/NPERewardNavigator.cs
Lines: 733

## Top-level comments
- Navigator for the NPE (New Player Experience) reward screen that shows unlocked cards after completing tutorial objectives. Uses Left/Right for navigation (like pack opening), Up/Down for card details.

## public class NPERewardNavigator : BaseNavigator (line 16)

### Fields
- private GameObject _rewardsContainer (line 18)
- private int _totalCards (line 19)
- private bool _isDeckReward (line 20)
- private string _lastDetectState (line 21)
- private int _discoveredDeckBoxCount (line 22)

### Properties
- public override string NavigatorId => "NPEReward" (line 24)
- public override string ScreenName => GetScreenName() (line 25)
- public override int Priority => 75 (line 26)

### Methods
- public NPERewardNavigator(IAnnouncementService announcer) (line 28)
- private void Log(string message) (line 30)
- private string GetScreenName() (line 32)
- private string GetPath(Transform t) (line 45)
- protected override bool DetectScreen() (line 56) — Verifies RewardsCONTAINER has active card prefabs or deck prefabs; distinguishes card vs deck reward mode
- private void LogStateChange(string newState, string message = null) (line 166)
- protected override void DiscoverElements() (line 174)
- private void FindCardEntries(HashSet<GameObject> addedObjects) (line 201)
- private void FindDeckEntries(HashSet<GameObject> addedObjects) (line 309)
- private void FindTakeRewardButton(HashSet<GameObject> addedObjects) (line 415)
- private void LogCustomButtonDetails(Transform buttonTransform) (line 477)
- public override string GetTutorialHint() (line 513)
- protected override string GetActivationAnnouncement() (line 516)
- protected override void HandleInput() (line 522)
- private bool IsClaimButton(int index) (line 608)
- private static string GetSafeName(GameObject go) (line 619) — Handles Unity-destroyed objects by wrapping go.name in try/catch
- private void LogCurrentState() (line 627)
- protected override bool ValidateElements() (line 655) — For deck rewards, forces re-detection when deck box count changes
- private int CountDeckBoxes() (line 701)
- public override void OnSceneChanged(string sceneName) (line 720)
