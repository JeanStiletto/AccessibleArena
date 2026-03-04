# RewardPopupNavigator.cs Code Index

## Summary
Navigator for rewards popup (from mail claims, store purchases, etc.). Extracted from GeneralMenuNavigator - uses exact same detection and discovery logic.

## Classes

### class RewardPopupNavigator : BaseNavigator (line 16)
```
public override string NavigatorId => "RewardPopup" (line 18)
public override string ScreenName => Strings.ScreenRewards (line 19)
public override int Priority => 86 (line 20)

private GameObject _activePopup (line 22)
private int _rewardCount (line 23)

private bool _lastRewardsPopupState (line 26)

private int _rescanFrameCounter (line 29)
private const int RescanDelayFrames = 30 (line 30)
private const int MaxRescanAttempts = 10 (line 31)
private int _rescanAttempts (line 32)

public RewardPopupNavigator(IAnnouncementService announcer) (line 34)

protected override bool DetectScreen() (line 38)
private bool CheckRewardsPopupOpenInternal() (line 56)
  // NOTE: Copied exactly from OverlayDetector.CheckRewardsPopupOpenInternal()
private Transform GetRewardsContainer() (line 124)
  // NOTE: Copied exactly from OverlayDetector.GetRewardsContainer()

protected override void DiscoverElements() (line 152)
private void DiscoverRewardElements(HashSet<GameObject> addedObjects) (line 170)
  // NOTE: Searches entire popup for reward prefabs, not just RewardsCONTAINER
private void DiscoverButtons(HashSet<GameObject> addedObjects) (line 252)
private string GetButtonLabel(GameObject button) (line 286)
private void DumpRewardPrefabStructure(GameObject rewardPrefab, string rewardType, int index) (line 306)
  // NOTE: Debug helper to understand available data
private void DumpChildStructure(Transform parent, int depth, int maxDepth) (line 341)
private string ExtractRewardLabel(GameObject rewardPrefab, string rewardType, int index) (line 359)
  // NOTE: Copied exactly from GeneralMenuNavigator.ExtractRewardLabel()
private string TryGetPackNameFromReward(GameObject rewardPrefab) (line 484)
  // NOTE: Extract pack/set name from NotificationPopupReward using reflection
private GameObject FindCardObjectInReward(GameObject rewardPrefab) (line 554)
  // NOTE: Copied exactly from GeneralMenuNavigator.FindCardObjectInReward()
private GameObject FindRewardClickTarget(GameObject rewardPrefab, string rewardType = null) (line 590)
  // NOTE: Copied exactly from GeneralMenuNavigator.FindRewardClickTarget()

protected override string GetActivationAnnouncement() (line 642)
protected override void HandleInput() (line 648)
private bool DismissRewardsPopup() (line 720)
  // NOTE: Copied exactly from GeneralMenuNavigator.DismissRewardsPopup()

public override void Update() (line 782)
  // NOTE: Rescan support for timing issues - rewards may load after popup appears
protected override void OnActivated() (line 806)
protected override bool ValidateElements() (line 818)
public override void OnSceneChanged(string sceneName) (line 829)
```
