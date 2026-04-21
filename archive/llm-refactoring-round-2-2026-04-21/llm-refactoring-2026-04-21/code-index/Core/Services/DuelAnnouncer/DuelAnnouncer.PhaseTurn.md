# DuelAnnouncer.PhaseTurn.cs
Path: src/Core/Services/DuelAnnouncer/DuelAnnouncer.PhaseTurn.cs
Lines: 242

## Top-level comments
- Phase and turn tracking: announces turn changes (counts full cycles, not half-turns), announces phase/step transitions, announces life changes, debounces rapid phase changes; reads _currentPhase/_currentStep across other partials.

## public partial class DuelAnnouncer (line 15)

### Fields
- private int _userTurnCount (line 18) — counts full turns (game counts half-turns)
- private bool _isUserTurn (line 21)
- private string _currentPhase (line 24) — shared across partials (read by Combat for combat phase checks)
- private string _currentStep (line 25)
- private string _pendingPhaseAnnouncement (line 28) — debounced 100ms
- private float _phaseDebounceTimer (line 29)
- private float _lastPhaseChangeTime (line 33)

### Properties
- public string CurrentPhase (line 39)
- public bool IsUserTurn (line 44)
- public float TimeSinceLastPhaseChange (line 34)

### Methods
- public string GetTurnPhaseInfo() (line 50) — returns formatted "Your/Opponent's <phase>, turn N"
- private string BuildTurnChangeAnnouncement(object uxEvent) (line 59) — extracts ActivePlayer field
- private string BuildLifeChangeAnnouncement(object uxEvent) (line 94) — determines ownership via avatar string (LocalPlayer/Opponent) or AffectedId
- private string BuildPhaseChangeAnnouncement(object uxEvent) (line 168) — announces leaving DeclareAttack with attacker list; debounces phase announcement; resets NPE blocking reminder flag
