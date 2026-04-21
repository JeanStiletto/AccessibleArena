# KeyHoldRepeater.cs
Path: src/Core/Utils/KeyHoldRepeater.cs
Lines: 90

## Top-level comments
- Tracks a single held key and fires repeated actions after an initial delay (0.5s) at a repeat interval (0.1s). Used by navigators for hold-to-repeat arrow key navigation.

## class KeyHoldRepeater (line 10)
### Fields
- private const float InitialDelay = 0.5f (line 12)
- private const float RepeatInterval = 0.1f (line 13)
- private KeyCode _heldKey (line 15)
- private float _holdTimer (line 16)
- private bool _isHolding (line 17)
### Methods
- public bool Check(KeyCode key, Func<bool> action) (line 24) — Note: returns true if key consumed; action returns false to stop hold-repeat (boundary); hold tracking only starts if initial press action returned true
- public bool Check(KeyCode key, Action action) (line 75) — Note: overload for actions that always continue repeating; wraps action in lambda returning true
- public void Reset() (line 82)
