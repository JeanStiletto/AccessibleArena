# BrowserNavigator.AssignDamage.cs
Path: src/Core/Services/BrowserNavigator/BrowserNavigator.AssignDamage.cs
Lines: 581

## Top-level comments
- Feature partial for the AssignDamage browser (combat damage assignment UI). Handles state caching from the game's AssignDamageBrowser / AssignDamageWorkflow, spinner control via Up/Down, submit via DoneAction, undo via UndoAction, and custom per-card announcements with P/T and lethal flag.

## public partial class BrowserNavigator (line 16)
### Fields
- private bool _isAssignDamage (line 19)
- private object _assignDamageBrowserRef (line 20) — AssignDamageBrowser instance
- private System.Collections.IDictionary _spinnerMap (line 21) — InstanceId → SpinnerAnimated
- private uint _totalDamage (line 22)
- private bool _totalDamageCached (line 23)
- private int _assignerIndex (line 24) — 1-based index of current assigner
- private int _assignerTotal (line 25) — total number of damage assigners in this combat
- private const BindingFlags ReflFlags (line 26) — AllInstanceFlags alias shared with other feature partials

### Methods
- private void CacheAssignDamageState() (line 31) — GameManager→BrowserManager→CurrentBrowser; caches _idToSpinnerMap from browser; deactivates CardInfoNavigator so Up/Down reach spinner
- private bool HandleAssignDamageInput() (line 100) — Up/Down→AdjustDamageSpinner; Enter consumed (no-op); Space→SubmitAssignDamage; Backspace→UndoAssignDamage
- private void EnsureTotalDamageCached() (line 148) — lazy init: GameManager→WorkflowController→CurrentWorkflow→_damageAssigner→TotalDamage; also reads _handledAssigners/_unhandledAssigners for _assignerIndex/_assignerTotal
- private void SubmitAssignDamage() (line 252) — invokes DoneAction field on browser; fallback to OnButtonCallback("DoneButton")
- private void UndoAssignDamage() (line 297) — invokes UndoAction field on browser
- private object GetSpinnerForCurrentCard() (line 329) — looks up current card InstanceId in _spinnerMap
- private uint GetCardInstanceId(GameObject card) (line 347) — scans MonoBehaviours for CDC components; tries InstanceId property then Model.Instance.InstanceId chain
- private void AdjustDamageSpinner(bool increase) (line 395) — calls EnsureTotalDamageCached; clicks _upButton/_downButton on spinner; reads Value property; appends lethal state
- private bool IsSpinnerLethal(object spinner) (line 457) — reads _valueText color; gold check: r>0.9, 0.6<g<0.8, b<0.1
- private void AnnounceAssignDamageCard(GameObject card) (line 486) — announces name, P/T, lethal, position; does NOT call PrepareForCard to keep Up/Down free for spinner
- private string GetAssignDamageEntryAnnouncement(int cardCount, string fallbackName) (line 518) — reads _layout._attacker name and _blockers count from AssignDamage browser; appends assigner position
