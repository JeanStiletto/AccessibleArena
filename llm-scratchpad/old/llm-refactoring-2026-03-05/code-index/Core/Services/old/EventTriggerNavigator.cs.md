# EventTriggerNavigator.cs - Code Index

## File-level Comment
Handles navigation for screens using EventTrigger or CustomButton components
instead of standard Unity UI Selectables (NPE, rewards, pack opening, etc.)

## Classes

### EventTriggerNavigator (line 16)
```csharp
public class EventTriggerNavigator : BaseNavigator
```

#### Fields
- private const float POST_CLICK_SCAN_DELAY = 2.0f (line 18)
- private static readonly System.Reflection.BindingFlags AllInstance (line 19)
- private bool _pendingChangeCheck (line 23)
- private float _changeCheckDelay (line 24)
- private bool _waitingForMainButton (line 25)
- private GameObject _currentContext (line 26)
- private string _contextType (line 27)
  - "CardReveal", "Rewards", "General"

#### Properties (Override)
- public override string NavigatorId (line 29) - Returns "EventTrigger"
- public override string ScreenName (line 30) - Returns context screen name
- public override int Priority (line 31) - Returns 10 (low priority - fallback navigator)

#### Constructor
- public EventTriggerNavigator(IAnnouncementService announcer) : base(announcer) (line 33)

#### Methods - Context Detection
- private string GetContextScreenName() (line 35)
  - Returns screen name based on context type

#### Methods (Override)
- protected override bool DetectScreen() (line 45)
  - Only activate if no standard selectables exist, check for specific contexts
- protected override void DiscoverElements() (line 84)
  - Dispatch to context-specific discovery methods
- public override void Update() (line 102)
  - Handle pending rescan and MainButton appearance
- protected override bool OnElementActivated(int index, GameObject element) (line 132)
  - Handle special NPE elements (chest, deck box) and schedule post-click scan

#### Methods - Card Reveal Context
- private void DiscoverCardRevealElements(HashSet<GameObject> addedObjects) (line 176)
- private void FindRevealedCards(GameObject container, HashSet<GameObject> addedObjects) (line 206)
- private string ExtractCardName(GameObject cardPrefab) (line 255)

#### Methods - Rewards Context
- private void DiscoverRewardElements(HashSet<GameObject> addedObjects) (line 298)
- private void FindRewardCards(HashSet<GameObject> addedObjects) (line 331)
- private string GetCardName(GameObject cardObject) (line 365)
- private void FindQuestElements(HashSet<GameObject> addedObjects) (line 397)
- private string FindGlobalQuestDescription() (line 441)
- private string GetQuestLabel(GameObject questObj, int stageNum, string globalDescription = null) (line 463)
- private void FindAllActiveButtons(HashSet<GameObject> addedObjects) (line 519)
- private string GetButtonLabel(GameObject obj, string name) (line 543)

#### Methods - General Context
- private void DiscoverGeneralElements(HashSet<GameObject> addedObjects) (line 568)
- private string CleanName(string name) (line 579)

#### Methods - Special NPE Handling
- private void HandleSpecialNPEElement(GameObject target, bool isChest, bool isDeckBox) (line 589)
  - Uses reflection to call NPEContentControllerRewards methods
- private GameObject FindClickTarget(GameObject parent) (line 649)
- private void TryInvokeMethod(object target, System.Type type, string methodName) (line 662)

#### Methods - Utility
- private void SchedulePostClickScan() (line 167)
- private void DumpUIElements() (line 688)
  - Debug: dump UI hierarchy to log
- private string GetGameObjectPath(GameObject obj) (line 747)
