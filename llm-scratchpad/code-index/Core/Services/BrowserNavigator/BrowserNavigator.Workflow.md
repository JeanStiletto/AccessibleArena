# BrowserNavigator.Workflow.cs
Path: src/Core/Services/BrowserNavigator/BrowserNavigator.Workflow.cs
Lines: 583

## Top-level comments
- Feature partial for confirm/cancel button routing. Implements workflow reflection submit/cancel paths (GameManager→WorkflowController→CurrentInteraction→_request) plus prioritized click fallbacks (button-by-name, button-by-pattern, PromptButton_Primary/Secondary). Also owns the two-press confirm guard flags used by the core HandleInput.

## public partial class BrowserNavigator (line 16)
### Fields
- private bool _browserConfirmWarning (line 19) — first Space press sets guard in MultiZone browsers
- private bool _browserConfirmWaitRelease (line 20) — blocks Space while still held from first press

### Methods
- private bool TrySubmitWorkflowViaReflection() (line 27) — navigates GameManager→WorkflowController→CurrentInteraction; tries _request.SubmitSolution then direct Submit/Confirm/Complete/Accept/Close methods; logs debug if enabled
- private bool TryCancelWorkflowViaReflection() (line 169) — tries ConfirmWidget.Cancel(), then _currentVariant.Cancelled action, then _request.Undo() (if AllowUndo)
- private void ClickConfirmButton() (line 303) — sets _pendingRescan=true; routes through workflow reflection, direct-choice, London (checks required bottom count), Mulligan, SelectCards workflow submit, button patterns, scaffold workflow submit, PromptButton_Primary
- private void ClickCancelButton() (line 433) — routes through workflow cancel reflection, MulliganButton (increments mulligan count), CancelPatterns, SingleButton (SelectCards decline), PromptButton_Secondary
- private bool TryClickButtonByName(string buttonName, out string clickedLabel) (line 492) — checks _browserButtons first, then scene fallback via BrowserDetector.FindActiveGameObject
- private bool TryClickButtonByPatterns(string[] patterns, out string clickedLabel) (line 531)
- private bool TryClickPromptButton(string prefix, out string clickedLabel) (line 554) — skips non-interactable Selectables; skips secondary buttons with short keyboard-hint text (<=4 chars and no space)
