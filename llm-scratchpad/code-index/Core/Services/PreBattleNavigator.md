# PreBattleNavigator.cs Code Index

## Summary
Navigator for the pre-game VS screen shown before a duel starts (PreGameScene). Handles PromptButton_Primary/Secondary which use StyledButton (pointer events). This handles the "Continue to battle" / "Cancel" prompt before the actual duel.

## Classes

### class PreBattleNavigator : BaseNavigator (line 14)
```
private bool _isWatching (line 16)
private bool _waitingForTransition (line 17)
private float _activationTime (line 18)
private const float TRANSITION_WAIT = 2.0f (line 19)

public override string NavigatorId => "PreBattle" (line 21)
public override string ScreenName => Strings.ScreenPreGame (line 22)
public override int Priority => 80 (line 23)
protected override bool AcceptSpaceKey => false (line 26)

public PreBattleNavigator(IAnnouncementService announcer) (line 28)
public void OnDuelSceneLoaded() (line 34)
  // NOTE: Called by AccessibleArenaMod when DuelScene loads, starts watching for buttons
public override void OnSceneChanged(string sceneName) (line 42)
protected override bool DetectScreen() (line 54)
protected override void DiscoverElements() (line 63)
public override void Update() (line 108)
private void HandleTransitionWait() (line 129)
protected override bool OnElementActivated(int index, GameObject element) (line 153)
private void FullDeactivate() (line 167)
private bool HasPromptButtons() (line 175)
```
