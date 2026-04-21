# PreBattleNavigator.cs
Path: src/Core/Services/PreBattleNavigator.cs
Lines: 189

## Top-level comments
- Navigator for the pre-game VS screen shown before a duel starts (PreGameScene). Handles PromptButton_Primary/Secondary which use StyledButton (pointer events) for the "Continue to battle" / "Cancel" prompt.

## public class PreBattleNavigator : BaseNavigator (line 15)
### Fields
- private bool _isWatching (line 17)
- private bool _waitingForTransition (line 18)
- private float _activationTime (line 19)
- private const float TRANSITION_WAIT = 2.0f (line 20)

### Properties
- public override string NavigatorId (line 22)
- public override string ScreenName (line 23)
- public override int Priority (line 24)
- protected override bool AcceptSpaceKey (line 27) — Note: returns false; only Enter confirms battle (Space is not an activator here).

### Methods
- public PreBattleNavigator(IAnnouncementService announcer) (line 29)
- public void OnDuelSceneLoaded() (line 35)
- public override void OnSceneChanged(string sceneName) (line 43)
- protected override bool DetectScreen() (line 55)
- protected override void DiscoverElements() (line 64) — Note: inserts primary button at index 0 so it is announced first; discovers secondary and tertiary prompts plus Nav_Settings.
- public override void Update() (line 109)
- private void HandleTransitionWait() (line 130) — Note: re-enables navigation silently on timeout by re-discovering elements without announcing.
- protected override bool OnElementActivated(int index, GameObject element) (line 154) — Note: uses UIActivator.SimulatePointerClick (StyledButton responds to pointer events, not onClick); starts a transition-wait window.
- private void FullDeactivate() (line 168)
- private bool HasPromptButtons() (line 176)
