# UIActivator.cs

## Overview
Centralized UI activation utilities.
Handles clicking buttons, toggling checkboxes, focusing input fields, and playing cards from hand.

## Class: UIActivator (static) (line 16)

### Constants
- private const float CardSelectDelay (line 21)
- private const float CardPickupDelay (line 22)
- private const float CardDropDelay (line 23)
- private const int MaxDeckSearchDepth (line 26)
- private const int MaxDeckViewSearchDepth (line 27)
- private const string CustomButtonTypeName (line 30)
- private const string DeckViewTypeName (line 31)
- private const string TooltipTriggerTypeName (line 32)
- private static readonly Regex SubmitButtonPattern (line 35)
- private const float TargetingCacheTimeout (line 38)
- private static float _lastTargetingScanTime (line 39)
- private static bool _cachedTargetingResult (line 40)

### CustomButton Detection
- private static bool IsCustomButton(MonoBehaviour mb) (line 45)

### Main Activation Methods
- public static ActivationResult Activate(GameObject obj) (line 77)
  - Main entry point, routes to specific handlers
- public static void ActivateButton(GameObject obj) (line 131)
- public static void ActivateToggle(GameObject obj) (line 192)
- public static void ActivateSlider(GameObject obj, int direction) (line 223)
- public static void ActivateDropdown(GameObject obj) (line 278)
- public static void ActivateInputField(GameObject obj) (line 308)

### Card Playing
- public static void PlayCardViaTwoClick(GameObject card, Action<bool, string> onComplete) (line 407)
  - Two-click approach: click card, then screen center
  - Note: Misleadingly named method - uses left-click simulation
- private static IEnumerator PlayCardSequence(GameObject card, Action<bool, string> onComplete) (line 419)

### Simulation Utilities
- public static ActivationResult SimulatePointerClick(GameObject target) (line 648)
- public static ActivationResult SimulateLeftClick(GameObject target) (line 679)
- private static IEnumerator ClickSequence(GameObject target, Action<bool, string> onComplete) (line 713)
- private static bool SendPointerEvent(GameObject target, PointerEventData.InputButton button, string eventType) (line 754)

### Detection Utilities
- public static bool IsTargetingMode() (line 816)
  - Cached check to avoid expensive scene scans
- private static GameObject FindSubmitButton() (line 849)
  - Finds main confirmation button using pattern matching

### Deck Interaction
- public static GameObject FindDeckViewParent(GameObject card) (line 888)
- private static bool IsDeckViewComponent(Component component) (line 916)

### Tooltip Support
- private static void SuppressTooltipsDuringActivation(GameObject obj, Action action) (line 947)

### Result Type
- public struct ActivationResult (line 992)
  - bool Success
  - string Message
