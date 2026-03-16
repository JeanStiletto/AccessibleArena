# Registration Form Investigation

Status: **Diagnostic build deployed** — all known Enter paths blocked, but game still submits independently. Stack trace diagnostic on `OnButton_SubmitRegistration()` will reveal the remaining path.

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

### Our mod
- `BaseNavigator.HandleInput` / `GeneralMenuNavigator.HandleCustomInput` detects Enter via `Input.GetKeyDown(Return)` or `EnterPressedWhileBlocked` flag → calls `ActivateCurrentElement()` → `UIActivator.SimulatePointerClick()`

## Decompiled Types (all in `llm-docs/decompiled/`)

- `OldInputHandler.cs` — `Core.Code.Input.OldInputHandler` (Core.dll): polls `Input.GetKeyDown` for all actions
- `NewInputHandler.cs` — `Core.Code.Input.NewInputHandler` (Core.dll): uses InputAction callbacks, `Update()` is empty
- `ActionSystem.cs` — `Core.Code.Input.ActionSystem` (Core.dll): subscribes to `IInputHandler.Accept` → calls `IAcceptActionHandler.OnAccept()`
- `ActionSystemFactory.cs` — `Core.Code.Input.ActionSystemFactory` (Core.dll): creates OldInputHandler or NewInputHandler based on feature toggle
- `CustomInputModule.cs` — `Wotc.Mtga.CustomInput.CustomInputModule` (Core.dll): adds CustomStandaloneInputModule or CustomUIInputModule
- `CustomUIInputModule.cs` — `Wotc.Mtga.CustomInput.CustomUIInputModule` (Core.dll): extends InputSystemUIInputModule, no Submit override
- `Panel.cs` — `Wotc.Mtga.Login.Panel` (Core.dll): implements IAcceptActionHandler, OnAccept() clicks _mainButton
- `RegistrationPanel.cs` — `Wotc.Mtga.Login.RegistrationPanel` (Core.dll): OnButton_SubmitRegistration() → DoRegistration()
- `CustomButton.cs` — `CustomButton` (Core.dll): implements IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler (NOT ISubmitHandler)

## All Blocks Currently Applied

### 1. `Input.GetKeyDown(KeyCode.Return)` — BLOCKED
`EventSystemPatch.GetKeyDown_Postfix` (Harmony postfix on `UnityEngine.Input.GetKeyDown`):
- When `BlockSubmitForToggle == true` and `__result == true` for Return/KeypadEnter
- Sets `__result = false` (caller sees false), sets `EnterPressedWhileBlocked = true`
- Blocks `OldInputHandler.Update()` from seeing Enter
- Also blocks our own mod's `Input.GetKeyDown` calls (mod uses `EnterPressedWhileBlocked` instead)

### 2. `EventSystem.SendSubmitEventToSelectedObject` — BLOCKED
`EventSystemPatch.SendSubmitEventToSelectedObject_Prefix` (Harmony prefix on `StandaloneInputModule`):
- When `BlockSubmitForToggle == true` OR `DropdownStateManager.ShouldBlockEnterFromGame` OR post-dropdown window
- Only applies if `StandaloneInputModule` is active (old input system)

### 3. `KeyboardManager.PublishKeyDown(Return)` — BLOCKED
`KeyboardManagerPatch.PublishKeyDown_Prefix` via `ShouldBlockKey`:
- Blocks Return/KeypadEnter on Login scene and DuelScene
- Also blocks KeyUp via `PublishKeyUp_Prefix`

### 4. `NewInputHandler.OnAccept()` — BLOCKED
`EventSystemPatch.NewInputHandlerOnAccept_Prefix` (runtime Harmony prefix):
- Blocks when scene is Login
- Applied via `FindType("Core.Code.Input.NewInputHandler")` + `harmony.Patch()` at runtime
- Log confirmed patch loaded: `"Patched NewInputHandler.OnAccept()"`

### 5. `BlockNextEnterKeyUp` — SET
In `BaseNavigator.HandleInput` and `GeneralMenuNavigator.HandleCustomInput`:
- Set before `ActivateCurrentElement()` / `HandleGroupedEnter()` on Login scene
- `PublishKeyUp_Prefix` checks this one-shot flag and blocks Enter KeyUp

### 6. `BlockSubmitForToggle` for all Login scene elements
In `BaseNavigator.UpdateEventSystemSelection` and `GeneralMenuNavigator.UpdateEventSystemSelectionForGroupedElement`:
- Set to true for toggles, dropdowns, AND all Login scene elements
- Controls GetKeyDown_Postfix and SendSubmitEventToSelectedObject_Prefix

## What the Log Shows (latest test)

Patch confirmed loaded at startup:
```
[EventSystemPatch] Patched NewInputHandler.OnAccept()
[EventSystemPatch] Patched RegistrationPanel.OnButton_SubmitRegistration() (diagnostic)
```

Submit button activation (form fully filled, all toggles on):
```
09:34:02.480  BLOCKED Input.GetKeyDown(Return) x4
09:34:02.483  Activating: MainButton_Register (ID:-2846, Label:Bestätigen)
09:34:02.483  Simulating pointer events on: MainButton_Register
09:34:02.483  Set EventSystem selected object to: MainButton_Register
09:34:02.489  Announce: Aktiviert
09:34:12.428  Accessible Arena shutting down
```

No `NewInputHandler.OnAccept BLOCKED` message in log — either the new input system isn't active, or it doesn't fire on this frame.

User reports: registration succeeds (mail sent), but a 403 error appears after a few seconds instead of advancing to the new player experience.

## What's NOT Yet Ruled Out

1. **`CustomUIInputModule` (InputSystemUIInputModule) Submit path** — If the new UI module is active, it may fire Submit events through the new Input System, bypassing `StandaloneInputModule.SendSubmitEventToSelectedObject`. However, `CustomButton` doesn't implement `ISubmitHandler`, so Submit events shouldn't click the button.

2. **Unknown path** — The `OnButton_SubmitRegistration` diagnostic patch (with stack trace) will identify exactly which code path triggers registration. If the game calls it through a path we haven't blocked, the stack trace will reveal it.

3. **NOT a double-submit at all** — The 403 may come from a post-registration action (auto-login, token validation) that fails, not from a second registration attempt. The diagnostic will distinguish: one `OnButton_SubmitRegistration` call = not double-submit; two calls = double-submit with stack traces showing both paths.

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
Blocks Enter KeyUp from `KeyboardManager.PublishKeyUp`. Correct pattern but doesn't address the new Input System which uses InputAction callbacks, not KeyUp/KeyDown.

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
Always clicks `_mainButton` regardless of current focus.

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

## Next Step

Test with diagnostic build. The `OnButton_SubmitRegistration` stack trace log will reveal:
- Whether registration is called once or multiple times
- The exact code path that triggers each call
- Whether the 403 is from double-submit or a post-registration failure
