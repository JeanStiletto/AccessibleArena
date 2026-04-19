# DiscardNavigator.cs - Code Index

## File-level Comment
Handles card selection during discard phases.
Detects discard mode via "Submit X" button and allows:
- Enter to toggle card selection
- Space to submit or announce error
Zone navigation (C, arrows) is handled by ZoneNavigator.

## Classes

### DiscardNavigator (line 19)
```csharp
public class DiscardNavigator
```

#### Fields
- private readonly IAnnouncementService _announcer (line 21)
- private readonly ZoneNavigator _zoneNavigator (line 22)
- private static readonly Regex ButtonNumberPattern (line 25)
  - Language-agnostic: matches any text with a number at the end
- private static readonly Regex[] DiscardCountPatterns (line 30)
  - Multi-language patterns for required discard count
- private static readonly Regex[] DiscardOnePatterns (line 36)
  - Multi-language patterns for "discard one card"
- private int? _requiredCount = null (line 44)
  - Cached when entering discard mode
- private bool _hasLoggedTargetYield = false (line 45)
  - Throttle log spam

#### Constructor
- public DiscardNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator) (line 47)

#### Methods - Detection
- public bool IsDiscardModeActive() (line 56)
  - Checks if discard/selection mode is active by looking for "Submit X" button
  - Also checks that we're NOT in targeting mode (cards with HotHighlight)
- public int? GetRequiredDiscardCount() (line 87)
  - Gets the required discard count from the prompt text (language-agnostic)
- public (int count, GameObject button)? GetSubmitButtonInfo() (line 126)
  - Gets the Submit button info: count of selected cards and button GameObject

#### Methods - Selection State
- public bool IsCardSelectedForDiscard(GameObject card) (line 158)
  - Checks if a card is selected for discard (looks for selection visual indicators)
- public string GetSelectionStateText(GameObject card) (line 182)
  - Gets text to append to card announcement indicating selection state

#### Methods - Input Handling
- public bool HandleInput() (line 197)
  - Handles input during discard mode (Enter to toggle, Space to submit)
- private void ToggleCurrentCard() (line 231)
  - Toggles selection on the current card by clicking it
- private IEnumerator AnnounceSelectionCountDelayed() (line 259)
  - Waits for game to update Submit button, then announces the count
- private void TrySubmit() (line 276)
  - Attempts to submit the discard selection (checks if selected count matches required)
