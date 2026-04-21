# RewardPopupNavigator.cs
Path: src/Core/Services/RewardPopupNavigator.cs
Lines: 1402

## Top-level comments
- Navigator for the rewards popup (from mail claims, store purchases, etc.). Extracted from GeneralMenuNavigator and uses the same detection and discovery logic.

## public class RewardPopupNavigator : BaseNavigator (line 21)
### Fields
- private GameObject _activePopup (line 27)
- private int _rewardCount (line 28)
- private int _cardIndex (line 29) — Note: card-only counter for numbering cards separately from other rewards.
- private int _seasonEndState (line 30) — Note: 0=None, 1=OldRank, 2=Rewards, 3=NewRank.
- private string _seasonDisplayText (line 31)
- private int _lastRescanElementCount (line 32)
- private List<string> _packSetNames (line 37)
- private int _packSetNameIndex (line 38)
- private float _popupDetectedTime (line 42)
- private bool _timeoutFallbackFired (line 46)
- private bool _revealingWasSeen (line 53)
- private bool _lastRewardsPopupState (line 56)

### Properties
- public override string NavigatorId (line 23)
- public override string ScreenName (line 24)
- public override int Priority (line 25) — Note: 86 (higher than Overlay 85, below SettingsMenu 90).

### Methods
- public RewardPopupNavigator(IAnnouncementService announcer) (line 58)
- protected override bool DetectScreen() (line 62)
- private bool CheckRewardsPopupOpenInternal() (line 81)
- private void ClearPopupState() (line 199)
- private int GetSeasonEndState() (line 216)
- private bool HasActiveSeasonDisplay() (line 247)
- private bool? ReadStillRevealingFlag() (line 291) — Note: returns null when the flag is not readable (caller should treat as unknown).
- private Transform GetRewardsContainer() (line 320)
- protected override void DiscoverElements() (line 348)
- private void DiscoverSeasonRankElements(HashSet<GameObject> addedObjects) (line 384)
- private string ExtractSeasonRankText() (line 402)
- private void DiscoverRewardElements(HashSet<GameObject> addedObjects) (line 501)
- private void DiscoverRewardsFromControllerData(HashSet<GameObject> addedObjects) (line 589)
- private static string MapRewardTypeToLabel(string typeName) (line 659)
- private void ExtractPackSetNames() (line 692)
- private void DiscoverButtons(HashSet<GameObject> addedObjects) (line 773)
- private string GetButtonLabel(GameObject button) (line 796)
- private void DumpRewardPrefabStructure(GameObject rewardPrefab, string rewardType, int index) (line 816)
- private void DumpChildStructure(Transform parent, int depth, int maxDepth) (line 851)
- private string ExtractRewardLabel(GameObject rewardPrefab, string rewardType, int index) (line 869)
- private static string DetectRewardTypeByComponent(GameObject prefab) (line 1080)
- private GameObject FindCardObjectInReward(GameObject rewardPrefab) (line 1101)
- private GameObject FindRewardClickTarget(GameObject rewardPrefab, string rewardType = null) (line 1137)
- public override string GetTutorialHint() (line 1189)
- protected override string GetActivationAnnouncement() (line 1191)
- protected override void HandleInput() (line 1206)
- private bool DismissRewardsPopup() (line 1267)
- public override void ForceRescan() (line 1333) — Note: suppresses duplicate announcements when the element count hasn't changed.
- protected override void OnActivated() (line 1362)
- protected override bool ValidateElements() (line 1372)
- public override void OnSceneChanged(string sceneName) (line 1383)
