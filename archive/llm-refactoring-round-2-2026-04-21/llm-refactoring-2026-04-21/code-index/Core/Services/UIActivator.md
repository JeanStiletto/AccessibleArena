# UIActivator.cs
Path: src/Core/Services/UIActivator.cs
Lines: 1834

## Top-level comments
- Centralized UI activation utilities: handles clicking buttons, toggling checkboxes, focusing input fields, and playing cards from hand.

## public static class UIActivator (line 20)
### Fields
- private const float CardSelectDelay = 0.1f (line 25)
- private const float CardPickupDelay = 0.5f (line 26)
- private const float CardDropDelay = 0.6f (line 27)
- private const string CustomButtonTypeName = "CustomButton" (line 30)
- private const string TooltipTriggerTypeName = "TooltipTrigger" (line 31)
- private static readonly Regex SubmitButtonPattern (line 34)
- private const float TargetingCacheTimeout = 0.1f (line 37)
- private static float _lastTargetingScanTime (line 38)
- private static bool _cachedTargetingResult (line 39)

### Methods
- internal static bool IsCustomButton(MonoBehaviour mb) (line 45) — type-name check; shared with CardTileActivator
- public static ActivationResult Activate(GameObject element) (line 57) — Note: large dispatcher; special-cases collection cards, deck-list cards, commander-slot tiles, deck entries, input fields, toggles (with/without CustomButton), Nav_Mail, NPE rewards, UpdatePolicies, SystemMessageButtonView, empty-slot commander filter, deck-builder MainButton; mutates InputManager.BlockNextEnterKeyUp
- public static ActivationResult ActivateViaCustomButtonClick(GameObject element) (line 342) — Note: calls CustomButton.Click() directly to bypass the _mouseOver check
- public static ActivationResult SimulatePointerClick(GameObject element) (line 370) — Note: also sets EventSystem.selected and sends enter/down/up/click
- public static ActivationResult SimulatePointerClick(GameObject element, Vector2 screenPosition) (line 401) — Note: overload that uses an explicit screen position
- public static void SimulatePointerExit(GameObject element) (line 429)
- public static void SimulateHover(GameObject hoverControl, bool isNext = true) (line 443) — Note: misleading name; actually invokes Spinner_OptionSelector.OnNextValue/OnPreviousValue on parent, not a hover
- public static ActivationResult SimulateClickAtPosition(Vector2 screenPosition) (line 484) — Note: does NOT send a global click when raycast misses (to avoid unintended pass-priority in duels)
- public static ActivationResult SimulateScreenCenterClick() (line 533) — Note: falls back to iterating all active CustomButtons if the raycast click fails
- public static void PlayCardViaTwoClick(GameObject card, System.Action&lt;bool,string&gt; callback = null) (line 583)
- private static IEnumerator PlayCardCoroutine(GameObject card, System.Action&lt;bool,string&gt; callback) (line 595) — Note: aborts second click if targeting/discard Submit button is detected
- private static bool TryInvokeNavBarMailboxClick(GameObject element, out ActivationResult result) (line 673)
- private static bool TryInvokeNPERewardClaim(GameObject element, out ActivationResult result) (line 738) — Note: only triggers for name "NullClaimButton" inside NPE-Rewards_Container; prefers PutAwayRewards over OnClaimClicked_Unity
- private static bool TryInvokeUpdatePoliciesAccept(GameObject element, out ActivationResult result) (line 822)
- private static ActivationResult TryInvokeCustomButtonOnClick(GameObject element) (line 870) — Note: on exception falls back to SimulatePointerClick
- private static GameObject FindClickableInHierarchy(GameObject root) (line 930)
- private static bool HasClickHandler(GameObject obj) (line 969) — Note: explicitly ignores TooltipTrigger even though it implements IPointerClickHandler
- private static bool HasCustomButtonComponent(GameObject obj) (line 994)
- private static MonoBehaviour FindComponentByName(GameObject obj, string typeName) (line 1007)
- private static GameObject FindPopupRoot(GameObject element) (line 1021)
- private static MonoBehaviour FindSystemMessageView(GameObject element) (line 1045) — Note: falls back to FindObjectsOfType if not found via hierarchy
- private static bool TryInvokeStoredCallback(MonoBehaviour systemMsgButton) (line 1101)
- private static bool TryInvokeOnClickedReturnSource(GameObject element) (line 1194)
- private static bool TryDismissViaSystemMessageManager() (line 1270)
- private static bool TryCloseViaPopupManager() (line 1308)
- private static bool TryInvokeHandleKeyDown(MonoBehaviour component, KeyCode keyCode) (line 1390)
- private static bool TryInvokeOnBack(MonoBehaviour component) (line 1457)
- private static bool IsInsidePopup(GameObject element) (line 1512)
- private static MonoBehaviour FindDeckBuilderController() (line 1528)
- private static bool TryInvokeMethod(MonoBehaviour component, string methodName) (line 1554)
- private static void DiagnoseRegistrationState(GameObject element) (line 1585) — Note: diagnostic-only; logs registration panel field validity
- private static PointerEventData CreatePointerEventData(GameObject element) (line 1664)
- private static PointerEventData CreatePointerEventData(GameObject element, Vector2 screenPosition) (line 1682)
- private static Vector2 GetScreenPosition(GameObject obj) (line 1698)
- private static void Log(string message) (line 1717)
- private static bool IsTargetingModeActive() (line 1728) — Note: cached for TargetingCacheTimeout (0.1s) to avoid repeated FindObjectsOfType
- private static bool ScanForSubmitButton() (line 1746)
- private static bool IsSubmitButton(string text, Transform textTransform) (line 1777)

## public struct ActivationResult (line 1809)
### Properties
- public bool Success { get; } (line 1811)
- public string Message { get; } (line 1812)
- public ActivationType Type { get; } (line 1813)
### Methods
- public ActivationResult(bool success, string message, ActivationType type = ActivationType.Unknown) (line 1815)

## public enum ActivationType (line 1826)
- Unknown (line 1828)
- Button (line 1829)
- Toggle (line 1830)
- InputField (line 1831)
- PointerClick (line 1832)
