# EventTriggerNavigator.cs
Path: src/Core/Services/old/EventTriggerNavigator.cs
Lines: 766

## Top-level comments
- Handles navigation for screens using EventTrigger or CustomButton components instead of standard Unity UI Selectables (NPE, rewards, pack opening, etc.).

## public class EventTriggerNavigator (line 16)
Extends BaseNavigator.

### Fields
- private const float POST_CLICK_SCAN_DELAY = 2.0f (line 18)
- private static readonly System.Reflection.BindingFlags AllInstance (line 19)
- private bool _pendingChangeCheck (line 23)
- private float _changeCheckDelay (line 24)
- private bool _waitingForMainButton (line 25)
- private GameObject _currentContext (line 26)
- private string _contextType (line 27) — Note: "CardReveal", "Rewards", "General"

### Properties
- public override string NavigatorId => "EventTrigger" (line 29)
- public override string ScreenName => GetContextScreenName() (line 30)
- public override int Priority => 10 (line 31)

### Methods
- public EventTriggerNavigator(IAnnouncementService announcer) (line 33)
- private string GetContextScreenName() (line 35)
- protected override bool DetectScreen() (line 45)
- protected override void DiscoverElements() (line 84)
- public override void Update() (line 102) — Note: drives post-click rescan timer and MainButton polling
- protected override bool OnElementActivated(int index, GameObject element) (line 132)
- private void SchedulePostClickScan() (line 167) — Note: sets _isActive=false to force re-detection
- private void DiscoverCardRevealElements(HashSet<GameObject> addedObjects) (line 177)
- private void FindRevealedCards(GameObject container, HashSet<GameObject> addedObjects) (line 206)
- private string ExtractCardName(GameObject cardPrefab) (line 255)
- private void DiscoverRewardElements(HashSet<GameObject> addedObjects) (line 297)
- private void FindRewardCards(HashSet<GameObject> addedObjects) (line 331)
- private string GetCardName(GameObject cardObject) (line 365)
- private void FindQuestElements(HashSet<GameObject> addedObjects) (line 397)
- private string FindGlobalQuestDescription() (line 441)
- private string GetQuestLabel(GameObject questObj, int stageNum, string globalDescription = null) (line 463)
- private void FindAllActiveButtons(HashSet<GameObject> addedObjects) (line 519)
- private string GetButtonLabel(GameObject obj, string name) (line 543)
- private void DiscoverGeneralElements(HashSet<GameObject> addedObjects) (line 568)
- private string CleanName(string name) (line 579)
- private void HandleSpecialNPEElement(GameObject target, bool isChest, bool isDeckBox) (line 589) — Note: reflects into NPEContentControllerRewards for unlock animation / auto-flip
- private GameObject FindClickTarget(GameObject parent) (line 649)
- private void TryInvokeMethod(object target, System.Type type, string methodName) (line 662)
- private void DumpUIElements() (line 688) — Note: debug logging dump
- private string GetGameObjectPath(GameObject obj) (line 747)
