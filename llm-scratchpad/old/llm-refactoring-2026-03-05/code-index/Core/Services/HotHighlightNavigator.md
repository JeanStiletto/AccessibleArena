# HotHighlightNavigator.cs

Unified navigator for all HotHighlight-based navigation. Replaces TargetNavigator, HighlightNavigator, and DiscardNavigator. The game correctly manages HotHighlight to show only what's relevant in the current context.

## class HotHighlightNavigator (line 31)

### Private Fields
- _announcer (IAnnouncementService) (line 33)
- _zoneNavigator (ZoneNavigator) (line 34)
- _battlefieldNavigator (BattlefieldNavigator) (line 35)
- _items (List<HighlightedItem>) (line 37)
- _currentIndex (int) (line 38)
- _opponentIndex (int) (line 39)
- _isActive (bool) (line 40)
- _wasInSelectionMode (bool) (line 41)
- _lastItemZone (string) (line 44)
- _lastPromptButtonText (string) (line 47)
- _lastDiagHandHighlighted (int) (line 54)
- _lastDiagBattlefieldHighlighted (int) (line 55)
- _cachedAvatarViews (List<MonoBehaviour>) (line 70)

### Constants
- ButtonNumberPattern (Regex) (line 51) - Matches any number in button text

### Private Static Fields - Avatar Reflection Cache (line 57)
- PrivateInstance (BindingFlags) (line 57)
- PublicInstance (BindingFlags) (line 60)
- _avatarViewType (Type) (line 62)
- _highlightSystemField (FieldInfo) (line 63)
- _currentHighlightField (FieldInfo) (line 64)
- _isLocalPlayerProp (PropertyInfo) (line 65)
- _portraitButtonField (FieldInfo) (line 66)
- _avatarReflectionInitialized (bool) (line 67)

### Public Properties
- IsActive → bool (line 72)
- ItemCount → int (line 73)
- CurrentItem → HighlightedItem (line 74)
- HasTargetsHighlighted → bool (line 82) - Note: battlefield/stack targets
- HasPlayableHighlighted → bool (line 88)

### Constructor
- HotHighlightNavigator(IAnnouncementService, ZoneNavigator) (line 91)

### Public Methods
- SetBattlefieldNavigator(BattlefieldNavigator) (line 97)
- Activate() (line 102)
- Deactivate() (line 108)
- ClearState() (line 125) - Note: called when user navigates to a zone using shortcuts
- HandleInput() → bool (line 141)
- MonitorPromptButtons(float) (line 921) - Note: announces primary button text when meaningful choices appear

### Private Methods - Discovery (line 276)
- DiscoverAllHighlights() (line 276) - Note: single scene scan via Transform.name matching, no child traversals
- DiscoverSelectedCards(HashSet<int>) (line 392) - Note: fallback for cards that lost HotHighlight after being selected
- FindAndCacheAvatarViews() (line 418)
- TryAddPlayerTarget(MonoBehaviour, HashSet<int>) (line 435)
- CreateHighlightedItem(GameObject, string) → HighlightedItem (line 477)
- InitializeAvatarReflection(Type) (line 509)
- DiscoverPromptButtons() (line 876) - Note: only adds when both buttons have meaningful text

### Private Methods - Announcement (line 558)
- AnnounceCurrentItem() (line 558) - Note: delegates to zone/battlefield navigators for sync

### Private Methods - Activation (line 625)
- ActivateCurrentItem() (line 625) - Note: uses two-click for hand cards normally, single-click in selection mode

### Private Methods - Zone Detection (line 716)
- DetectZone(GameObject) → string (line 717)
- DetermineCardType(GameObject) → string (line 743)
- StringToZoneType(string) → ZoneType (line 754)

### Private Methods - Button Handling (line 771)
- GetPrimaryButtonText() → string (line 772)
- FindPrimaryButton() → GameObject (line 799)
- FindSecondaryButton() → GameObject (line 813)
- GetButtonText(GameObject) → string (line 827)
- IsMeaningfulButtonText(string) → bool (line 853) - Note: short text without spaces = keyboard hints
- IsButtonVisible(GameObject) → bool (line 865) - Note: checks CanvasGroup alpha and interactable

### Public Methods - Selection Mode (line 948)
- GetSelectionStateText(GameObject) → string (line 1143) - Note: used by ZoneNavigator
- IsInSelectionMode() → bool (line 1155)
- IsCardCurrentlySelected(GameObject) → bool (line 1161)
- TryToggleSelection(GameObject) → bool (line 1168) - Note: called by ZoneNavigator for Enter on hand cards

### Private Methods - Selection Mode (line 955)
- IsSelectionModeActive() → bool (line 955) - Note: detected by Submit button with count
- GetSubmitButtonInfo() → (int, GameObject)? (line 975)
- AnnounceSelectionToggleDelayed(string, bool) → IEnumerator (line 1006)
- IsCardSelected(GameObject) → bool (line 1035) - Note: checks for "select"/"chosen"/"pick" child objects
- CheckSelectionModeTransition(bool) (line 1059) - Note: announces on entry
- GetPromptInstructionText() → string (line 1096)
- GetRequiredCountFromPrompt() → int (line 1120) - Note: tries digits then number words

## class HighlightedItem (line 1191)

### Properties
- GameObject (GameObject) (line 1193)
- Name (string) (line 1194)
- Zone (string) (line 1195)
- HighlightType (string) (line 1196)
- IsOpponent (bool) (line 1197)
- IsPlayer (bool) (line 1198)
- CardType (string) (line 1199)
- PowerToughness (string) (line 1200)
- IsPromptButton (bool) (line 1201)
