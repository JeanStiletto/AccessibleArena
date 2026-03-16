# Registration Form Investigation

Status: **Still double-submitting** — all known Enter paths blocked, but game still submits independently.

## Root Cause (confirmed March 2026)

### The problem was never `_validDisplayName`

Diagnostic logging (`DiagnoseRegistrationState`) confirmed ALL validation conditions pass:
```
_validDisplayName = True
_submitting = True          ← THE FAILING CONDITION
All fields filled correctly
All toggles isOn=True
```

The button is disabled because `_submitting = True` — meaning the registration was **already submitted by the game** before our mod processed Enter.

### How the race works

`Panel` (base class of `RegistrationPanel`) implements `IAcceptActionHandler`. The game's `OldInputHandler` polls `Input.GetKeyDown(Return)` every frame and calls `Panel.OnAccept()` when Enter is pressed. `OnAccept()` clicks `_mainButton` (the submit button) regardless of which element has focus — it's the standard "Enter submits the form" behavior.

Timeline when user presses Enter on the submit button:
1. **Game's `OldInputHandler.Update()`** runs first → `Input.GetKeyDown(Return)` → calls `Panel.OnAccept()`
2. **`Panel.OnAccept()`** checks `_mainButton.Interactable` → true (all conditions met) → `_mainButton.Click()`
3. **`OnButton_SubmitRegistration()`** → `DoRegistration()` → `_submitting = true` → server call starts
4. **`_checkFields()` (same frame)** → `_submitting == true` → `EnableButton(false)`
5. **Our mod's `OnUpdate()`** runs after → `GetEnterAndConsume()` → `ActivateCurrentElement()`
6. **`UIActivator.Activate()`** → checks `CustomButton.Interactable` → **false** → returns "Deaktiviert"

The game already submitted successfully. The user gets confirmation emails, the account is created. But our mod reports "Deaktiviert" and the user thinks nothing happened.

### Why this wasn't caught earlier

Before the `Interactable` check was added (commit `1173608`), `SimulatePointerClick` returned "Aktiviert" even though the `CustomButton.OnPointerUp` silently rejected the click (button already disabled by `_submitting=true`). The user heard "Aktiviert", the game had already submitted — everything seemed to work. The interactable check made the silent failure visible.

### Why it affects ALL elements, not just the submit button

`Panel.OnAccept()` always clicks `_mainButton`, regardless of which element has EventSystem focus. So pressing Enter on an input field, a toggle (when not blocked), or any other element would also trigger form submission if the button is interactable.

Our toggle handling (`BlockSubmitForToggle = true`) was already preventing this for toggles and dropdowns. The gap was: buttons and other non-toggle elements on the login scene did NOT set this flag.

## Fixes Applied (all deployed, double-submit persists)

### Fix 1: BlockSubmitForToggle for all Login scene elements

Extended `BlockSubmitForToggle` to include ALL elements on the Login scene:
```csharp
bool isLoginScene = SceneManager.GetActiveScene().name == "Login";
InputManager.BlockSubmitForToggle = isToggle || isDropdown || isLoginScene;
```

This blocks:
- `GetKeyDown_Postfix` blocks `Input.GetKeyDown(Return)` → `OldInputHandler` doesn't see Enter → `OnAccept()` doesn't fire
- `SendSubmitEventToSelectedObject_Prefix` blocks EventSystem Submit
- `EnterPressedWhileBlocked` flag lets our mod detect the blocked Enter
- Our mod handles activation exclusively via `UIActivator.Activate()`

Changed in both `GeneralMenuNavigator.UpdateEventSystemSelectionForGroupedElement` and `BaseNavigator.UpdateEventSystemSelection`.

**Bug in initial attempt**: Originally checked for scene name `"Bootstrap"` but the active scene during registration is actually `"Login"` (MTGA loads Bootstrap → AssetPrep → Login additively). Fixed to `"Login"`.

**Result**: Enter no longer leaks to game on input fields and toggles. Submit button still double-submits.

### Fix 2: Block Enter from KeyboardManager.PublishKeyDown on Login scene

Added Enter blocking in `KeyboardManagerPatch.ShouldBlockKey` for the Login scene, same as DuelScene:
```csharp
if (_cachedSceneName == SceneNames.Login)
{
    if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
        return true;
}
```

**Theory**: `KeyboardManager.PublishKeyDown(Return)` was a separate path from `Input.GetKeyDown` that wasn't being blocked. Even with `GetKeyDown_Postfix` blocking, the game could receive Enter through keyboard manager subscribers.

**Result**: Did NOT fix the double-submit. The game still submits the registration independently.

### Fix 3: ConsumeKey for all Login scene elements

Extended `ConsumeKey(Return/KeypadEnter)` in `BaseNavigator.HandleInput` to apply to ALL elements on Login scene (not just toggles/dropdowns):
```csharp
bool isLoginScene = SceneManager.GetActiveScene().name == "Login";
if (element != null && (isLoginScene || element.GetComponent<Toggle>() != null || UIFocusTracker.IsDropdown(element)))
{
    InputManager.ConsumeKey(KeyCode.Return);
    InputManager.ConsumeKey(KeyCode.KeypadEnter);
}
```

**Theory**: `KeyboardManagerPatch` falls back to `IsKeyConsumed()` check — without consuming Enter for buttons, it went through.

**Result**: Did NOT fix the double-submit.

## What's been ruled out

All three known Enter paths to the game are now blocked:
1. **`Input.GetKeyDown(Return)`** — blocked by `EventSystemPatch.GetKeyDown_Postfix` when `BlockSubmitForToggle=true`
2. **`EventSystem.SendSubmitEventToSelectedObject`** — blocked by `SendSubmitEventToSelectedObject_Prefix` when `BlockSubmitForToggle=true`
3. **`KeyboardManager.PublishKeyDown(Return)`** — blocked by `KeyboardManagerPatch` for Login scene

Yet the game still submits. This suggests there's a **fourth path** that hasn't been identified:
- Could be a direct Unity `Input.GetKey(Return)` poll (not `GetKeyDown`) — not patched
- Could be through a different input module or event system
- Could be through `CustomInputModule` (MTGA's custom input handler)
- The `OldInputHandler` might not use `Input.GetKeyDown` — needs decompilation to verify

## Next Steps

- Decompile `OldInputHandler` to verify how it detects Enter
- Decompile `CustomInputModule` to check for alternative Enter paths
- Search for all callers of `Panel.OnAccept()` and `IAcceptActionHandler`
- Consider: instead of blocking game's Enter, intercept `Panel.OnAccept()` directly with a Harmony prefix

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
- If server rejects username: `_validDisplayName` stays `false`, input field stays disabled (`InputField.enabled = false`)

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

Always clicks `_mainButton` regardless of current focus. Triggered by `OldInputHandler` via `Input.GetKeyDown(Return)`.

## CRITICAL: Do NOT Globally Intercept Input.GetKeyDown(Return)

**Reverted in March 2026 after breaking all duel Enter handling.**

Commit `ff141d0` attempted to fix the registration phantom-submit by globally blocking
`Input.GetKeyDown(Return)` whenever any navigator was active (via `EventSystemPatch.GetKeyDown_Postfix`).
This also broadened `SendSubmitEventToSelectedObject` to block whenever any navigator was active.

### Why it broke duels

- `Input.GetKeyDown` is a Unity API that **our own mod calls** (e.g., DuelNavigator's Enter
  guard at HandleCustomInput line 550).
- The Harmony postfix intercepts ALL callers, including our own code.
- DuelNavigator's Enter guard calls `Input.GetKeyDown(Return)` → patch returns `false` →
  guard never fires → Enter falls through to BaseNavigator → `ActivateCurrentElement()`
  clicks the settings gear button (element index 0) → SettingsMenu opens.
- The `EnterPressedWhileBlocked` secondary channel was only checked by BaseNavigator, not
  by the DuelNavigator guard or any other caller.
- Result: Enter was completely broken in duels — every Enter press opened the settings menu.

### The rule

**Never patch `Input.GetKeyDown` with a global scope.** Our mod depends on `Input.GetKeyDown`
returning the real Unity value. Intercepting it creates a circular dependency where our patch
breaks our own callers, requiring a fragile secondary signaling channel that every caller must
know about.

### Allowed scope for GetKeyDown_Postfix

- `BlockSubmitForToggle == true` (toggle/dropdown/login-panel activation) — scoped per-element by navigators.

## Key Decompiled Types

- `RegistrationPanel` — `llm-docs/decompiled/RegistrationPanel.cs`
- `Panel` (base class) — `llm-docs/decompiled/Panel.cs`
- `CustomButton` — `llm-docs/decompiled/CustomButton.cs`
