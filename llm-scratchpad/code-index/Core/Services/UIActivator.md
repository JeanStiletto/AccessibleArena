# UIActivator.cs
Path: src/Core/Services/UIActivator.cs
Lines: 2745

## Top-level comments
- Centralized UI activation utilities: handles clicking buttons, toggling checkboxes, focusing input fields, and playing cards from hand.

## public static class UIActivator (line 20)
### Fields
- private const float CardSelectDelay = 0.1f (line 25)
- private const float CardPickupDelay = 0.5f (line 26)
- private const float CardDropDelay = 0.6f (line 27)
- private const int MaxDeckSearchDepth = 5 (line 30)
- private const int MaxDeckViewSearchDepth = 6 (line 31)
- private const string CustomButtonTypeName = "CustomButton" (line 34)
- private const string DeckViewTypeName = "DeckView" (line 35)
- private const string TooltipTriggerTypeName = "TooltipTrigger" (line 36)
- private static readonly Regex SubmitButtonPattern (line 39)
- private const float TargetingCacheTimeout = 0.1f (line 42)
- private static float _lastTargetingScanTime (line 43)
- private static bool _cachedTargetingResult (line 44)
- private static System.Reflection.MethodInfo _pantryGetFilterProviderMethod (line 47)
- private static System.Reflection.PropertyInfo _filterProperty (line 48)
- private static System.Reflection.MethodInfo _isSetMethod (line 49)
- private static object _commandersEnumValue (line 50)
- private static bool _filterReflectionInit (line 51)

### Methods
- private static bool IsCustomButton(MonoBehaviour mb) (line 56)
- public static ActivationResult Activate(GameObject element) (line 68) — Note: large dispatcher; special-cases collection cards, deck-list cards, commander-slot tiles, deck entries, input fields, toggles (with/without CustomButton), Nav_Mail, NPE rewards, UpdatePolicies, SystemMessageButtonView, empty-slot commander filter, deck-builder MainButton; mutates InputManager.BlockNextEnterKeyUp
- public static ActivationResult ActivateViaCustomButtonClick(GameObject element) (line 353) — Note: calls CustomButton.Click() directly to bypass the _mouseOver check
- public static ActivationResult SimulatePointerClick(GameObject element) (line 381) — Note: also sets EventSystem.selected and sends enter/down/up/click
- public static ActivationResult SimulatePointerClick(GameObject element, Vector2 screenPosition) (line 412) — Note: overload that uses an explicit screen position
- public static void SimulatePointerExit(GameObject element) (line 440)
- public static void SimulateHover(GameObject hoverControl, bool isNext = true) (line 454) — Note: misleading name; actually invokes Spinner_OptionSelector.OnNextValue/OnPreviousValue on parent, not a hover
- public static ActivationResult SimulateClickAtPosition(Vector2 screenPosition) (line 495) — Note: does NOT send a global click when raycast misses (to avoid unintended pass-priority in duels)
- public static ActivationResult SimulateScreenCenterClick() (line 544) — Note: falls back to iterating all active CustomButtons if the raycast click fails
- public static void PlayCardViaTwoClick(GameObject card, System.Action&lt;bool,string&gt; callback = null) (line 594)
- private static IEnumerator PlayCardCoroutine(GameObject card, System.Action&lt;bool,string&gt; callback) (line 606) — Note: aborts second click if targeting/discard Submit button is detected
- private static bool TryInvokeNavBarMailboxClick(GameObject element, out ActivationResult result) (line 684)
- private static bool TryInvokeNPERewardClaim(GameObject element, out ActivationResult result) (line 749) — Note: only triggers for name "NullClaimButton" inside NPE-Rewards_Container; prefers PutAwayRewards over OnClaimClicked_Unity
- private static bool TryInvokeUpdatePoliciesAccept(GameObject element, out ActivationResult result) (line 833)
- private static ActivationResult TryInvokeCustomButtonOnClick(GameObject element) (line 881) — Note: on exception falls back to SimulatePointerClick
- private static GameObject FindClickableInHierarchy(GameObject root) (line 941)
- private static bool HasClickHandler(GameObject obj) (line 980) — Note: explicitly ignores TooltipTrigger even though it implements IPointerClickHandler
- private static bool HasCustomButtonComponent(GameObject obj) (line 1005)
- private static MonoBehaviour FindComponentByName(GameObject obj, string typeName) (line 1018)
- private static GameObject FindPopupRoot(GameObject element) (line 1032)
- private static MonoBehaviour FindSystemMessageView(GameObject element) (line 1056) — Note: falls back to FindObjectsOfType if not found via hierarchy
- private static bool TryInvokeStoredCallback(MonoBehaviour systemMsgButton) (line 1112)
- private static bool TryInvokeOnClickedReturnSource(GameObject element) (line 1205)
- private static bool TryDismissViaSystemMessageManager() (line 1281)
- private static bool TryCloseViaPopupManager() (line 1319)
- private static bool TryInvokeHandleKeyDown(MonoBehaviour component, KeyCode keyCode) (line 1401)
- private static bool TryInvokeOnBack(MonoBehaviour component) (line 1468)
- private static bool IsInsidePopup(GameObject element) (line 1523)
- private static MonoBehaviour FindDeckBuilderController() (line 1539)
- private static bool TryInvokeMethod(MonoBehaviour component, string methodName) (line 1565)
- private static void DiagnoseRegistrationState(GameObject element) (line 1596) — Note: diagnostic-only; logs registration panel field validity
- private static PointerEventData CreatePointerEventData(GameObject element) (line 1675)
- private static PointerEventData CreatePointerEventData(GameObject element, Vector2 screenPosition) (line 1693)
- private static Vector2 GetScreenPosition(GameObject obj) (line 1709)
- private static void Log(string message) (line 1728)
- private static bool IsTargetingModeActive() (line 1739) — Note: cached for TargetingCacheTimeout (0.1s) to avoid repeated FindObjectsOfType
- private static bool ScanForSubmitButton() (line 1757)
- private static bool IsSubmitButton(string text, Transform textTransform) (line 1788)
- private static void DebugInspectNavMailButton(GameObject element) (line 1818) — Note: diagnostic-only
- private static void InspectUnityEvent(object unityEvent, string eventName) (line 2001) — Note: diagnostic-only
- private static void InspectDelegate(object del, string fieldName) (line 2077) — Note: diagnostic-only
- public static bool IsCollectionCard(GameObject element) (line 2105)
- public static bool IsInCommanderContainer(GameObject element) (line 2146)
- private static bool? IsCommandersFilterActive() (line 2168) — Note: lazy-initializes reflection via Pantry and caches MethodInfo/PropertyInfo
- public static bool IsCommanderSlotCard(GameObject element) (line 2217)
- public static bool IsDeckListCard(GameObject element) (line 2242)
- public static ActivationResult TryActivateCollectionCard(GameObject cardElement) (line 2267)
- private static MonoBehaviour FindMetaCardView(GameObject cardElement) (line 2284)
- private static bool TryOpenCardViewerDirectly(MonoBehaviour metaCardView) (line 2304)
- private static GameObject FindCustomButtonInHierarchy(GameObject root) (line 2379)
- public static bool TrySelectDeck(GameObject deckElement) (line 2417)
- public static bool IsDeckEntry(GameObject element) (line 2465)
- private static MonoBehaviour FindDeckViewInParents(GameObject element) (line 2487)
- public static bool IsDeckSelected(GameObject deckElement) (line 2514)
- public static string GetDeckInvalidStatus(GameObject deckElement) (line 2533)
- public static string GetDeckInvalidTooltip(GameObject deckElement) (line 2564)
- private static T GetFieldValue&lt;T&gt;(System.Type type, object instance, string fieldName, System.Reflection.BindingFlags flags) (line 2607)
- private static MonoBehaviour GetSelectedDeckView() (line 2621)
- public static void DebugInspectDeckView(GameObject deckElement) (line 2664) — Note: diagnostic-only

## public struct ActivationResult (line 2720)
### Properties
- public bool Success { get; } (line 2722)
- public string Message { get; } (line 2723)
- public ActivationType Type { get; } (line 2724)
### Methods
- public ActivationResult(bool success, string message, ActivationType type = ActivationType.Unknown) (line 2726)

## public enum ActivationType (line 2737)
- Unknown (line 2739)
- Button (line 2740)
- Toggle (line 2741)
- InputField (line 2742)
- PointerClick (line 2743)
