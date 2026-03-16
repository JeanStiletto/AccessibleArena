# Registration Form Investigation

Status: **Mod now invokes Panel.OnAccept() directly for registration submit.** All Enter paths blocked, only mod activates the button. Registration succeeds (email sent) but post-registration auto-login fails with 403 — cause unknown, not related to Enter blocking or code path.

## Architecture: How Enter Reaches Registration

MTGA has **two independent input systems** that both detect Enter:

### Old Input System (`UnityEngine.Input`)
- `OldInputHandler.Update()` polls `Input.GetKeyDown(KeyCode.Return)` → fires `Accept` event → `ActionSystem` → `Panel.OnAccept()` → `_mainButton.Click()`
- `StandaloneInputModule` / `CustomStandaloneInputModule` → `SendSubmitEventToSelectedObject` → fires `ISubmitHandler.OnSubmit` on EventSystem selected object
- `KeyboardManager.PublishKeyDown(Return)` → notifies subscribers (Panel implements `IKeyDownSubscriber` but only handles Escape)

### New Input System (`UnityEngine.InputSystem`)
- `NewInputHandler.OnAccept(InputAction.CallbackContext)` fires via InputAction callback → fires `Accept` event → `ActionSystem` → `Panel.OnAccept()` → `_mainButton.Click()`
- `CustomUIInputModule` (extends `InputSystemUIInputModule`) → fires Submit events on EventSystem selected object. BUT `CustomButton` does NOT implement `ISubmitHandler` (only pointer handlers), so this path has no effect on buttons.

### Which system is active?
`ActionSystemFactory.UseNewInput` (feature toggle `"use_new_unity_input"`) determines which handler is created. `CustomInputModule.Start()` uses the same toggle to choose `CustomStandaloneInputModule` or `CustomUIInputModule`. Only ONE of each is active.

### Our mod's activation path
- `BaseNavigator.HandleInput` / `GeneralMenuNavigator.HandleCustomInput` detects Enter via `EnterPressedWhileBlocked` flag (set by GetKeyDown_Postfix)
- For the registration submit button: `UIActivator.TryInvokeLoginPanelAccept()` finds `RegistrationPanel` component, calls `Panel.OnAccept()` via reflection — identical to the game's own path
- For other Login scene buttons: `UIActivator.SimulatePointerClick()` (normal path)

## Decompiled Types (all in `llm-docs/decompiled/`)

- `OldInputHandler.cs` — `Core.Code.Input.OldInputHandler` (Core.dll): polls `Input.GetKeyDown` for all actions
- `NewInputHandler.cs` — `Core.Code.Input.NewInputHandler` (Core.dll): uses InputAction callbacks, `Update()` is empty
- `ActionSystem.cs` — `Core.Code.Input.ActionSystem` (Core.dll): subscribes to `IInputHandler.Accept` → calls `IAcceptActionHandler.OnAccept()`. No additional state management — just dispatches events to focused handlers.
- `ActionSystemFactory.cs` — `Core.Code.Input.ActionSystemFactory` (Core.dll): creates OldInputHandler or NewInputHandler based on feature toggle
- `CustomInputModule.cs` — `Wotc.Mtga.CustomInput.CustomInputModule` (Core.dll): adds CustomStandaloneInputModule or CustomUIInputModule
- `CustomUIInputModule.cs` — `Wotc.Mtga.CustomInput.CustomUIInputModule` (Core.dll): extends InputSystemUIInputModule, no Submit override
- `Panel.cs` — `Wotc.Mtga.Login.Panel` (Core.dll): implements IAcceptActionHandler, OnAccept() clicks _mainButton
- `RegistrationPanel.cs` — `Wotc.Mtga.Login.RegistrationPanel` (Core.dll): OnButton_SubmitRegistration() → DoRegistration()
- `CustomButton.cs` — `CustomButton` (Core.dll): implements IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler (NOT ISubmitHandler). `Click()` calls OnPointerDown + _onClick.Invoke() directly. `OnPointerUp()` checks `_mouseOver` before invoking _onClick.

## All Blocks on Login Scene

### 1. `Input.GetKeyDown(KeyCode.Return)` — BLOCKED (KeyDown only)
`EventSystemPatch.GetKeyDown_Postfix` (Harmony postfix on `UnityEngine.Input.GetKeyDown`):
- When `BlockSubmitForToggle == true` and `__result == true` for Return/KeypadEnter
- Sets `__result = false` (caller sees false), sets `EnterPressedWhileBlocked = true`
- Blocks `OldInputHandler.Update()` from seeing Enter → Accept event never fires
- Also blocks our own mod's `Input.GetKeyDown` calls (mod uses `EnterPressedWhileBlocked` instead)
- `Input.GetKeyUp(Return)` is NOT patched — Unity raw KeyUp still fires normally
- `Input.GetKey(Return)` is NOT patched — Unity raw key-held still fires normally

### 2. `EventSystem.SendSubmitEventToSelectedObject` — BLOCKED
`EventSystemPatch.SendSubmitEventToSelectedObject_Prefix` (Harmony prefix on `StandaloneInputModule`):
- Blocked when `BlockSubmitForToggle == true` (always true on Login scene for all elements)
- Also blocked by `DropdownStateManager.ShouldBlockEnterFromGame` (while dropdown is open)
- Also blocked by `DropdownStateManager.ShouldBlockSubmit()` (500ms timer after dropdown close/selection)
- The 500ms timer ONLY blocks this one method — does NOT block KeyUp, KeyDown, or PublishKeyDown/PublishKeyUp

### 3. `KeyboardManager.PublishKeyDown(Return)` — BLOCKED
`KeyboardManagerPatch.PublishKeyDown_Prefix` via `ShouldBlockKey`:
- Scene-based check: `_cachedSceneName == SceneNames.Login` → blocks Return/KeypadEnter unconditionally
- No dependency on button state — blocked regardless of interactable

### 4. `KeyboardManager.PublishKeyUp(Return)` — BLOCKED
`KeyboardManagerPatch.PublishKeyUp_Prefix` via `ShouldBlockKey`:
- Same scene-based check as KeyDown — blocks Return/KeypadEnter unconditionally on Login scene
- Game never sees Enter KeyUp through KeyboardManager, even if button is interactable
- `BlockNextEnterKeyUp` one-shot flag is redundant on Login scene (permanent block catches it)

### 5. `NewInputHandler.OnAccept()` — BLOCKED
`EventSystemPatch.NewInputHandlerOnAccept_Prefix` (runtime Harmony prefix):
- Blocks when scene is Login — prevents ActionSystem from calling Panel.OnAccept()
- Applied via `FindType("Core.Code.Input.NewInputHandler")` + `harmony.Patch()` at runtime

### 6. `BlockSubmitForToggle` for all Login scene elements
In `BaseNavigator.UpdateEventSystemSelection` and `GeneralMenuNavigator.UpdateEventSystemSelectionForGroupedElement`:
- Set to true for toggles, dropdowns, AND all Login scene elements
- Controls GetKeyDown_Postfix (#1) and SendSubmitEventToSelectedObject_Prefix (#2)

### Summary: what the game sees on Login scene
- `Input.GetKeyDown(Return)` → always false (patched)
- `Input.GetKey(Return)` → normal (NOT patched)
- `Input.GetKeyUp(Return)` → normal (NOT patched)
- `KeyboardManager.PublishKeyDown(Return)` → never fires (blocked)
- `KeyboardManager.PublishKeyUp(Return)` → never fires (blocked)
- `NewInputHandler.OnAccept()` → never fires (blocked)
- `SendSubmitEventToSelectedObject()` → never fires (blocked)
- **Only activation:** mod calls `Panel.OnAccept()` via reflection on `RegistrationPanel`

## Diagnostic Results

### Test: empty form submit attempt
```
Activating: MainButton_Register (ID:-2846, Label:Bestätigen)
CustomButton 'MainButton_Register' is NOT interactable - click blocked
Announce: Deaktiviert
```
Button correctly blocked — `_checkFields()` disables it when form is incomplete. `OnButton_SubmitRegistration` was NOT called.

### Test: filled form submit (Panel.OnAccept path)
```
Activating: MainButton_Register (ID:-2846, Label:Bestätigen)
Invoking RegistrationPanel.OnAccept() via Panel base
>>> OnButton_SubmitRegistration CALLED <<<
Stack: Panel.OnAccept() → CustomButton.Click() → UnityEvent.Invoke → OnButton_SubmitRegistration
Announce: Aktiviert
(~10 seconds later) Accessible Arena shutting down
```
- **Exactly ONE call** to `OnButton_SubmitRegistration`
- Stack trace confirms game's own code path: `Panel.OnAccept()` → `CustomButton.Click()`
- Registration succeeds (email sent), but 403 error appears instead of advancing to new player experience

### Test: filled form submit (SimulatePointerClick path, earlier build)
```
Activating: MainButton_Register (ID:-2846, Label:Bestätigen)
Simulating pointer events on: MainButton_Register
>>> OnButton_SubmitRegistration CALLED <<<
Stack: UIActivator.SimulatePointerClick → CustomButton.OnPointerUp → UnityEvent.Invoke → OnButton_SubmitRegistration
Announce: Aktiviert
(~4.5 seconds later) Accessible Arena shutting down
```
- Also exactly ONE call — same 403 result
- Different code path (OnPointerUp vs Click) but same outcome

## What's Been Ruled Out

1. **Double-submit** — Diagnostic proves exactly ONE `OnButton_SubmitRegistration` call in all tests
2. **Code path difference** — Tested both `Panel.OnAccept()` (game's path) and `SimulatePointerClick` (mod's path) — same 403 result
3. **Dropdown 500ms timer** — All dropdown `onValueChanged` properly suppressed/restored/fired. Timer expired 68+ seconds before submit. Only blocks `SendSubmitEventToSelectedObject`, not KeyUp/KeyDown
4. **Dropdown onValueChanged suppression** — Log shows every "Suppressed" has matching "Restored" and "Fired"
5. **`ShouldBlockEnterFromGame`** — Cleared when dropdown closed, false at submit time
6. **Leaked KeyUp event** — Both KeyDown and KeyUp permanently blocked on Login scene via `ShouldBlockKey` (scene-based, not button-state-based). Game never sees Enter through KeyboardManager.

## Remaining Unknown: Post-Registration 403

After `OnButton_SubmitRegistration()` → `DoRegistration()`:
```csharp
// From decompiled RegistrationPanel.cs
private Promise<CreateUserResponse> DoRegistration()
{
    _submitting = true;
    return _loginScene._accountClient.RegisterAsFullAccount(...)
        .ThenOnMainThreadIfSuccess(delegate { OnRegisterSuccess(email); })
        .ThenOnMainThreadIfError(delegate(Error e) { OnRegisterError(...); })
        .Then(delegate { _submitting = false; });
}

private void OnRegisterSuccess(string email)
{
    // ... tracking, experiments, remember me ...
    _loginScene.ConnectToFrontDoor(accountInformation);
}
```

Registration succeeds (email sent) but `ConnectToFrontDoor` (auto-login) fails with 403. This is a server callback, not input-driven. None of our Enter/input blocking should affect it.

**Not yet tested:** whether the 403 also occurs without the mod installed.

## Previous Attempts That Failed

### String-based Harmony patch on `Panel.OnAccept` — SILENTLY FAILED
```csharp
[HarmonyPatch("Wotc.Mtga.Login.Panel", "OnAccept")]
[HarmonyPrefix]
public static bool PanelOnAccept_Prefix() { ... }
```
Never fired — no log output. String-based `[HarmonyPatch]` attributes silently fail when the type isn't found at attribute processing time. Replaced with runtime patching via `FindType()` + `harmony.Patch()`.

### ConsumeKey for Login scene elements — NO EFFECT
`ConsumeKey(Return)` only blocks `KeyboardManager.PublishKeyDown` via `IsKeyConsumed()`. Doesn't block `NewInputHandler` callbacks or `InputSystemUIInputModule`.

### BlockNextEnterKeyUp pattern (craft fix) — INSUFFICIENT
Blocks Enter KeyUp from `KeyboardManager.PublishKeyUp`. Correct pattern but doesn't address the new Input System which uses InputAction callbacks, not KeyUp/KeyDown. Also redundant on Login scene where `ShouldBlockKey` permanently blocks.

## CRITICAL: Do NOT Globally Intercept Input.GetKeyDown(Return)

**Reverted in March 2026 after breaking all duel Enter handling.**

Commit `ff141d0` attempted to fix the registration phantom-submit by globally blocking
`Input.GetKeyDown(Return)` whenever any navigator was active (via `EventSystemPatch.GetKeyDown_Postfix`).

### Why it broke duels
- `Input.GetKeyDown` is a Unity API that **our own mod calls** (e.g., DuelNavigator's Enter guard)
- The Harmony postfix intercepts ALL callers, including our own code
- DuelNavigator's Enter guard sees `false` → Enter falls through to BaseNavigator → opens settings menu
- The `EnterPressedWhileBlocked` secondary channel was only checked by BaseNavigator

### The rule
**Never patch `Input.GetKeyDown` with a global scope.** Scope: `BlockSubmitForToggle == true` only.

## How Registration Validation Works (decompiled)

Source: `llm-docs/decompiled/RegistrationPanel.cs`

### _checkFields() — runs every frame
Enables button ONLY when ALL conditions pass:
1. Password >= 8 chars, no rule violations
2. Display name 3-23 chars AND `_validDisplayName == true`
3. Email not empty
4. Email matches email confirmation
5. Password matches password confirmation
6. All 3 required toggles on (terms, codeOfConduct, privacyPolicy)
7. `_submitting == false`

### _validDisplayName flow
- Defaults to `false`
- `_displayName_select` (onSelect callback): resets to `false` every time the display name field is focused
- `_displayName_endEdit` (onEndEdit callback): starts `Coroutine_ValidateUsername` (async server call)
- `Coroutine_ValidateUsername`: sets `_validDisplayName = false` at start, then on server success sets `true`
- If server rejects username: `_validDisplayName` stays `false`, button permanently disabled

### Panel.OnAccept (base class)
```csharp
public virtual void OnAccept()
{
    current.SetSelectedGameObject(_mainButton.gameObject);
    if (_mainButton.Interactable)
    {
        _mainButton.Click();
        EnableButton(enabled: false);
    }
}
```
Always clicks `_mainButton` regardless of current focus. `EnableButton(false)` immediately disables the button, preventing any subsequent OnAccept call (e.g., from a delayed KeyUp) from triggering a second click.

### OnButton_SubmitRegistration
```csharp
public void OnButton_SubmitRegistration()
{
    // Clear all feedback, disable button, send analytics
    EnableButton(enabled: false);
    DoRegistration();
    AudioManager.PlayAudio(WwiseEvents.sfx_ui_accept, base.gameObject);
}
```
Also calls `EnableButton(false)` — button is disabled by both Panel.OnAccept AND OnButton_SubmitRegistration.
