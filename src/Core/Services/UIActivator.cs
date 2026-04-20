using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Utils;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using SceneNames = AccessibleArena.Core.Constants.SceneNames;
namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Centralized UI activation utilities for GENERIC UI elements.
    /// Handles clicking buttons, toggling checkboxes, focusing input fields,
    /// and playing cards from the duel hand.
    ///
    /// Scope: any Unity GameObject with a CustomButton / Toggle / TMP_Dropdown
    /// / TMP_InputField / EventTrigger — i.e. UI that is not specifically a
    /// card tile in the collection/deck-builder.
    ///
    /// For card-tile activation (collection grid, commander/companion/partner
    /// slots, deck-list entries) use <see cref="CardTileActivator"/> instead.
    /// </summary>
    public static class UIActivator
    {
        #region Constants

        // Timing delays for card play sequence
        private const float CardSelectDelay = 0.1f;
        private const float CardPickupDelay = 0.5f;
        private const float CardDropDelay = 0.6f;

        // Type names for reflection
        private const string CustomButtonTypeName = "CustomButton";
        private const string TooltipTriggerTypeName = "TooltipTrigger";

        // Compiled regex for Submit button detection
        private static readonly Regex SubmitButtonPattern = new Regex(@"^Submit\s*\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Targeting mode detection cache (avoids expensive FindObjectsOfType every call)
        private const float TargetingCacheTimeout = 0.1f;
        private static float _lastTargetingScanTime;
        private static bool _cachedTargetingResult;

        /// <summary>
        /// Checks if a MonoBehaviour is a CustomButton by type name.
        /// Internal so CardTileActivator can reuse it without duplication.
        /// </summary>
        internal static bool IsCustomButton(MonoBehaviour mb)
        {
            return mb != null && mb.GetType().Name == CustomButtonTypeName;
        }

        #endregion

        #region General UI Activation

        /// <summary>
        /// Activates a UI element based on its type (button, toggle, input field, etc.).
        /// </summary>
        public static ActivationResult Activate(GameObject element)
        {
            if (element == null)
                return new ActivationResult(false, "Element is null");

            // Special handling for collection cards (PagesMetaCardView/MetaCardView in deck builder)
            // These are card display views - the actual click handler is on a child element
            if (CardTileActivator.IsCollectionCard(element))
            {
                Log.Activation("UIActivator", $"Detected collection card: {element.name}");
                var result = CardTileActivator.TryActivateCollectionCard(element);
                if (result.Success)
                    return result;
                Log.Activation("UIActivator", $"Collection card activation failed, trying fallback");
            }

            // Deck list cards (cards in your deck) may open the card viewer popup when
            // the Craft filter is active. Block the Enter KeyUp to prevent PopupManager.HandleKeyUp
            // from calling CardViewerController.OnEnter() → OnCraftClicked() (auto-craft).
            if (CardTileActivator.IsDeckListCard(element))
            {
                InputManager.BlockNextEnterKeyUp = true;
            }

            // Commander/companion/partner card tiles in Brawl deck builder:
            // SimulatePointerClick fires pointerDown→pointerUp→pointerClick on the same element.
            // pointerUp triggers _onClick → OnRemoveClicked → destroys the card instance (and tile).
            // Then pointerClick fires on the destroyed object, and EventSystem.selectedGameObject
            // points to a destroyed reference, leaving the game in a broken state.
            // Fix: invoke OnClick directly — no EventSystem selection, no pointerClick on destroyed object.
            if (CardTileActivator.IsCommanderSlotCard(element))
            {
                var cb = FindComponentByName(element, CustomButtonTypeName);
                if (cb != null)
                {
                    var onClickProp = cb.GetType().GetProperty("OnClick", PublicInstance);
                    if (onClickProp != null)
                    {
                        var onClick = onClickProp.GetValue(cb, null) as UnityEngine.Events.UnityEvent;
                        if (onClick != null)
                        {
                            Log.Activation("UIActivator", $"Commander slot card: invoking OnClick directly on '{element.name}'");
                            onClick.Invoke();
                            return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
                        }
                    }
                }
                Log.Activation("UIActivator", $"Commander slot card: OnClick not found, falling back to SimulatePointerClick");
                return SimulatePointerClick(element);
            }

            // Special handling for deck entries - they need direct selection via DeckViewSelector
            // because MTGA's CustomButton onClick on decks doesn't reliably trigger selection
            if (CardTileActivator.IsDeckEntry(element))
            {
                Log.Activation("UIActivator", $"Detected deck entry, trying specialized deck selection");
                if (CardTileActivator.TrySelectDeck(element))
                {
                    return new ActivationResult(true, "Deck Selected", ActivationType.Button);
                }
                // Fall through to standard activation if specialized selection fails
                Log.Activation("UIActivator", $"Specialized deck selection failed, falling back to standard activation");
            }

            // Try TMP_InputField
            var tmpInput = element.GetComponent<TMP_InputField>();
            if (tmpInput != null)
            {
                tmpInput.ActivateInputField();
                tmpInput.Select();
                return new ActivationResult(true, "Editing", ActivationType.InputField);
            }

            // Try legacy InputField
            var inputField = element.GetComponent<InputField>();
            if (inputField != null)
            {
                inputField.ActivateInputField();
                inputField.Select();
                return new ActivationResult(true, "Editing", ActivationType.InputField);
            }

            // Try Toggle
            var toggle = element.GetComponent<Toggle>();
            if (toggle != null)
            {
                // Check if this toggle also has CustomButton (like deck folder toggles)
                bool toggleHasCustomButton = HasCustomButtonComponent(element);
                if (toggleHasCustomButton)
                {
                    // For Toggle+CustomButton combo:
                    // 1. Toggle.OnPointerClick - changes state
                    // 2. CustomButton onClick - triggers game filtering logic
                    bool oldState = toggle.isOn;
                    Log.Activation("UIActivator", $"Toggle with CustomButton: current state {oldState}");

                    // Step 1: Toggle the checkbox state
                    var pointer = CreatePointerEventData(element);
                    ((IPointerClickHandler)toggle).OnPointerClick(pointer);

                    bool newState = toggle.isOn;
                    string state = newState ? "Checked" : "Unchecked";
                    Log.Activation("UIActivator", $"Toggle state after click: {oldState} -> {newState} ({state})");

                    // Step 2: Trigger CustomButton to run game filtering logic
                    TryInvokeCustomButtonOnClick(element);

                    return new ActivationResult(true, state, ActivationType.Toggle);
                }
                else
                {
                    // Simple toggle without CustomButton.
                    // EventSystemPatch blocks Unity's Submit when we consume Enter/Space,
                    // so we always handle toggling directly here.
                    toggle.isOn = !toggle.isOn;
                    string state = toggle.isOn ? "Checked" : "Unchecked";
                    Log.Activation("UIActivator", $"Toggled {element.name} to {state}");
                    return new ActivationResult(true, state, ActivationType.Toggle);
                }
            }

            // Check for CustomButton BEFORE standard Button
            // MTGA uses CustomButton for most interactions - the game logic responds to pointer events,
            // not to Button.onClick. If element has CustomButton, use pointer simulation.
            // This is critical for buttons like Link_LogOut that have both Button and CustomButton.
            bool hasCustomButton = HasCustomButtonComponent(element);
            if (hasCustomButton)
            {
                // Special handling for Nav_Mail button - onClick has no listeners
                // NavBarController.MailboxButton_OnClick() must be invoked directly
                if (element.name == "Nav_Mail")
                {
                    if (TryInvokeNavBarMailboxClick(element, out var mailResult))
                        return mailResult;
                }

                // Special handling for NPE reward claim button - onClick listener is broken
                // Must invoke OnClaimClicked_Unity on NPEContentControllerRewards instead
                if (TryInvokeNPERewardClaim(element, out var npeResult))
                    return npeResult;

                // Special handling for UpdatePoliciesPanel - the button's onClick has broken listener reference
                // so we invoke OnAccept directly on the panel controller
                if (TryInvokeUpdatePoliciesAccept(element, out var acceptResult))
                    return acceptResult;

                // Special handling for SystemMessageButtonView (popup dialog buttons)
                // Click() triggers the button's callback and starts the game's Hide() animation.
                // The game handles dismissal itself via _onHide -> DismissCurrentMessage().
                // Do NOT call ClearMessageQueue() — it would destroy any follow-up popup
                // queued by the callback (e.g. "Unlock All Modes" has two chained confirmations).
                var systemMsgButton = FindComponentByName(element, "SystemMessageButtonView");
                if (systemMsgButton != null)
                {
                    Log.Activation("UIActivator", $"SystemMessageButtonView detected on: {element.name}");
                    TryInvokeMethod(systemMsgButton, "Click");
                    return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
                }

                // Check if the CustomButton is interactable before sending pointer events.
                // When not interactable (e.g. registration form validation fails),
                // CustomButton.OnPointerUp returns early and _onClick won't fire,
                // but SimulatePointerClick would still return success, misleading the user.
                var customButtonComp = FindComponentByName(element, CustomButtonTypeName);
                if (customButtonComp != null)
                {
                    var interactableProp = customButtonComp.GetType().GetProperty("Interactable", PublicInstance);
                    if (interactableProp != null)
                    {
                        bool isInteractable = (bool)interactableProp.GetValue(customButtonComp);
                        if (!isInteractable)
                        {
                            Log.Activation("UIActivator", $"CustomButton '{element.name}' is NOT interactable - click blocked");
                            // Diagnostic: check registration form validation state
                            DiagnoseRegistrationState(element);
                            return new ActivationResult(false, Models.Strings.ItemDisabled);
                        }
                    }
                }

                // Login scene buttons: game handles activation natively via
                // ActionSystem → Panel.OnAccept(). No mod intervention needed.

                // Empty slot buttons (commander/companion in Brawl deck builder):
                // The game auto-enables the Commanders filter when there's no commander.
                // SimulatePointerClick fires _onClick which TOGGLES the filter.
                // If already ON, this would toggle it OFF — the opposite of what the user expects.
                // Read the filter state first: if already ON, skip the click entirely.
                if (element.name == "CustomButton - EmptySlot" && CardTileActivator.IsInCommanderContainer(element))
                {
                    bool? filterState = CardTileActivator.IsCommandersFilterActive();
                    if (filterState == true)
                    {
                        Log.Activation("UIActivator", $"Empty slot: Commanders filter already active, skipping click to avoid toggle-off");
                        return new ActivationResult(true, Models.Strings.Activated(Models.Strings.DeckBuilderCommander), ActivationType.Button);
                    }
                    // Filter is OFF or unknown — click to toggle ON
                    Log.Activation("UIActivator", $"Empty slot: Commanders filter is {(filterState == false ? "OFF" : "unknown")}, clicking to activate");
                    SimulatePointerClick(element);
                    return new ActivationResult(true, Models.Strings.Activated(Models.Strings.DeckBuilderCommander), ActivationType.Button);
                }

                // Special handling for deck builder MainButton - invoke directly instead of SimulatePointerClick
                // to avoid double activation (pointer click + direct call both trigger the Done action)
                // Only apply when the button is NOT inside a popup (e.g. AdvancedFiltersPopup also has a "MainButton")
                if (element.name == "MainButton" && !IsInsidePopup(element))
                {
                    var deckBuilderController = FindDeckBuilderController();
                    if (deckBuilderController != null)
                    {
                        Log.Activation("UIActivator", $"Found deck builder controller: {deckBuilderController.GetType().Name}");
                        if (TryInvokeMethod(deckBuilderController, "OnDeckbuilderDoneButtonClicked"))
                        {
                            Log.Activation("UIActivator", $"WrapperDeckBuilder.OnDeckbuilderDoneButtonClicked() invoked successfully");
                            return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
                        }
                        Log.Activation("UIActivator", $"Could not invoke OnDeckbuilderDoneButtonClicked, falling back to pointer click");
                    }
                }

                return SimulatePointerClick(element);
            }

            // Try standard Unity Button (only if no CustomButton)
            var button = element.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.Invoke();
                return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
            }

            // Try child Button
            var childButton = element.GetComponentInChildren<Button>();
            if (childButton != null)
            {
                Log.Activation("UIActivator", $"Using child Button: {childButton.gameObject.name}");
                childButton.onClick.Invoke();
                return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
            }

            // Try to find the actual clickable element in hierarchy
            // Some containers (like HomeBanner) have the CustomButton on a child, not the root
            var clickableTarget = FindClickableInHierarchy(element);
            if (clickableTarget != null && clickableTarget != element)
            {
                Log.Activation("UIActivator", $"Found clickable child: {clickableTarget.name}, sending pointer events there");
                return SimulatePointerClick(clickableTarget);
            }

            // Special handling for SystemMessageButtonView (confirmation dialog buttons)
            // These buttons need their Click method invoked directly
            var systemMessageButton = FindComponentByName(element, "SystemMessageButtonView");
            if (systemMessageButton != null)
            {
                Log.Activation("UIActivator", $"SystemMessageButtonView detected, trying Click method");
                if (TryInvokeMethod(systemMessageButton, "Click"))
                {
                    return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
                }
                if (TryInvokeMethod(systemMessageButton, "OnClick"))
                {
                    return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
                }
                if (TryInvokeMethod(systemMessageButton, "OnButtonClicked"))
                {
                    return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
                }
            }

            // For non-CustomButton elements (CustomButton was handled earlier), try onClick reflection then pointer fallback
            var customButtonResult = TryInvokeCustomButtonOnClick(element);
            if (customButtonResult.Success)
            {
                return customButtonResult;
            }

            // Final fallback: Pointer event simulation
            return SimulatePointerClick(element);
        }

        /// <summary>
        /// Activates a choice button by directly calling CustomButton.Click(),
        /// which bypasses the _mouseOver state check in OnPointerUp.
        /// Falls back to SimulatePointerClick if no CustomButton found.
        /// </summary>
        public static ActivationResult ActivateViaCustomButtonClick(GameObject element)
        {
            if (element == null)
                return new ActivationResult(false, "Element is null");

            var customButton = FindComponentByName(element, CustomButtonTypeName);
            if (customButton != null)
            {
                Log.Activation("UIActivator", $"Invoking CustomButton.Click() on: {element.name}");
                if (TryInvokeMethod(customButton, "Click"))
                    return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
            }

            // Fallback: try _onClick.Invoke() via reflection
            var onClickResult = TryInvokeCustomButtonOnClick(element);
            if (onClickResult.Success) return onClickResult;

            // Final fallback: pointer simulation
            return SimulatePointerClick(element);
        }

        #endregion

        #region Pointer Simulation

        /// <summary>
        /// Simulates a full pointer click sequence (enter, down, up, click) on an element.
        /// </summary>
        public static ActivationResult SimulatePointerClick(GameObject element)
        {
            if (element == null)
                return new ActivationResult(false, "Element is null");

            var pointer = CreatePointerEventData(element);

            Log.Activation("UIActivator", $"Simulating pointer events on: {element.name}");

            // Set as selected object in EventSystem - some UI elements require this
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(element);
                Log.Activation("UIActivator", $"Set EventSystem selected object to: {element.name}");
            }

            // Full click sequence - mimics a real mouse click
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerClickHandler);

            return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.PointerClick);
        }

        /// <summary>
        /// Simulates a pointer click on an element using a specific screen position.
        /// Used for battlefield cards where the default screen-center position can hit
        /// the wrong overlapping token's collider.
        /// </summary>
        public static ActivationResult SimulatePointerClick(GameObject element, Vector2 screenPosition)
        {
            if (element == null)
                return new ActivationResult(false, "Element is null");

            var pointer = CreatePointerEventData(element, screenPosition);

            Log.Activation("UIActivator", $"Simulating pointer events on: {element.name} at position ({screenPosition.x:F0}, {screenPosition.y:F0})");

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(element);
                Log.Activation("UIActivator", $"Set EventSystem selected object to: {element.name}");
            }

            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerClickHandler);

            return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.PointerClick);
        }

        /// <summary>
        /// Simulates a pointer exit event on an element.
        /// Used to tell the game that the mouse has left an element (e.g., to stop music/effects).
        /// </summary>
        public static void SimulatePointerExit(GameObject element)
        {
            if (element == null) return;

            var pointer = CreatePointerEventData(element);
            ExecuteEvents.Execute(element, pointer, ExecuteEvents.pointerExitHandler);
            Log.Activation("UIActivator", $"Sent PointerExit to: {element.name}");
        }

        /// <summary>
        /// Activates a Popout stepper by invoking OnNextValue/OnPreviousValue on the
        /// parent's Spinner_OptionSelector component via reflection.
        /// Avoids full pointer simulation which would open a submenu (DeckSelectBlade).
        /// </summary>
        public static void SimulateHover(GameObject hoverControl, bool isNext = true)
        {
            if (hoverControl == null) return;

            // Go to Popout_* parent to find the Spinner_OptionSelector
            Transform popoutParent = hoverControl.transform.parent;
            if (popoutParent == null)
            {
                Log.Activation("UIActivator", $"No parent found for {hoverControl.name}");
                return;
            }

            var spinner = popoutParent.GetComponents<MonoBehaviour>()
                .FirstOrDefault(c => c != null && c.GetType().Name == "Spinner_OptionSelector");

            if (spinner == null)
            {
                Log.Activation("UIActivator", $"No Spinner_OptionSelector found on {popoutParent.name}");
                return;
            }

            string methodName = isNext ? "OnNextValue" : "OnPreviousValue";
            var method = spinner.GetType().GetMethod(methodName,
                PrivateInstance);

            if (method != null)
            {
                method.Invoke(spinner, null);
                Log.Activation("UIActivator", $"Invoked {methodName} on {popoutParent.name}");
            }
            else
            {
                Log.Activation("UIActivator", $"Method {methodName} not found on Spinner_OptionSelector");
            }
        }

        /// <summary>
        /// Simulates a click at a specific screen position using raycast.
        /// If no target is found via raycast, still sends the pointer events
        /// as the game may process position-based clicks (e.g., dropping held cards).
        /// </summary>
        public static ActivationResult SimulateClickAtPosition(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Log.Activation("UIActivator", "No EventSystem found");
                return new ActivationResult(false, "No EventSystem");
            }

            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPosition,
                pressPosition = screenPosition
            };

            // Raycast to find what's at this position
            var raycastResults = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointer, raycastResults);

            if (raycastResults.Count > 0)
            {
                var target = raycastResults[0].gameObject;
                pointer.pointerPress = target;
                pointer.pointerEnter = target;

                Log.Activation("UIActivator", $"Clicking at position {screenPosition}: {target.name}");

                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);

                return new ActivationResult(true, $"Clicked {target.name}", ActivationType.PointerClick);
            }

            // No UI raycast hit at this position
            // Don't send "global clicks" to EventSystem - this was causing MTGA to interpret
            // them as "pass priority" during combat phases, leading to unwanted phase auto-skip.
            // The game processes card drops based on Input.mousePosition (real cursor),
            // which DuelNavigator centers when the duel starts. No additional click needed.
            Log.Activation("UIActivator", $"No raycast hit at {screenPosition}, skipping global click");

            return new ActivationResult(true, "No UI target at position", ActivationType.Unknown);
        }

        /// <summary>
        /// Simulates a click at screen center. Used for "click anywhere to continue" interactions.
        /// </summary>
        public static ActivationResult SimulateScreenCenterClick()
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var result = SimulateClickAtPosition(screenCenter);

            // Fallback: try clicking CustomButtons if raycast failed
            if (!result.Success)
            {
                Log.Activation("UIActivator", "No raycast hits, trying CustomButton fallback...");
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (!IsCustomButton(mb) || !mb.gameObject.activeInHierarchy) continue;

                    var type = mb.GetType();
                    var onClickField = type.GetField("onClick",
                        AllInstanceFlags);

                    if (onClickField != null)
                    {
                        var onClick = onClickField.GetValue(mb);
                        if (onClick != null)
                        {
                            var invokeMethod = onClick.GetType().GetMethod("Invoke", System.Type.EmptyTypes);
                            if (invokeMethod != null)
                            {
                                Log.Activation("UIActivator", $"Invoking onClick on CustomButton: {mb.gameObject.name}");
                                invokeMethod.Invoke(onClick, null);
                                return new ActivationResult(true, $"Invoked {mb.gameObject.name}", ActivationType.Button);
                            }
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Card Playing

        /// <summary>
        /// Plays a card from hand using the double-click approach:
        /// 1. Click card (selects it)
        /// 2. Wait briefly
        /// 3. Click card again (picks it up)
        /// 4. Wait briefly
        /// 5. Click screen center (confirms play for non-targeted cards)
        /// 6. Enter targeting mode (for targeted cards, Tab cycles through targets)
        /// </summary>
        public static void PlayCardViaTwoClick(GameObject card, System.Action<bool, string> callback = null)
        {
            if (card == null)
            {
                callback?.Invoke(false, "Card is null");
                return;
            }

            Log.Activation("UIActivator", $"=== PLAYING CARD: {card.name} ===");
            MelonCoroutines.Start(PlayCardCoroutine(card, callback));
        }

        private static IEnumerator PlayCardCoroutine(GameObject card, System.Action<bool, string> callback)
        {
            // Step 0: Check if card is a land (lands use simplified double-click, no screen center needed)
            bool isLand = CardStateProvider.IsLandCard(card);

            // Step 1: First click (select)
            Log.Activation("UIActivator", "Step 1: First click (select)");
            var click1 = SimulatePointerClick(card);
            if (!click1.Success)
            {
                Log.Activation("UIActivator", "Failed at step 1");
                callback?.Invoke(false, "First click failed");
                yield break;
            }

            // Brief wait for selection to register
            yield return new WaitForSeconds(CardSelectDelay);

            // Step 1.5: Check if we're in discard/selection mode BEFORE second click
            // If "Submit X" button is showing, first click selected the card for discard
            // Second click would break the discard state, so abort here
            if (IsTargetingModeActive())
            {
                Log.Activation("UIActivator", "Submit button detected - in discard/selection mode, not playing card");
                Log.Activation("UIActivator", "First click selected card for discard. Use Submit button to confirm.");
                callback?.Invoke(false, "Discard mode - card selected for discard");
                yield break;
            }

            // Step 2: Second click (pick up for spells, or play for lands)
            Log.Activation("UIActivator", "Step 2: Second click (pick up/play)");
            var click2 = SimulatePointerClick(card);
            if (!click2.Success)
            {
                Log.Activation("UIActivator", "Failed at step 2");
                callback?.Invoke(false, "Second click failed");
                yield break;
            }

            // For lands: Double-click is enough to play them, no need to drag to center
            // Exit immediately to avoid unnecessary clicks
            if (isLand)
            {
                Log.Activation("UIActivator", "Land played via double-click");
                Log.Activation("UIActivator", "=== CARD PLAY COMPLETE ===");
                callback?.Invoke(true, "Land played");
                yield break;
            }

            // For spells: Wait for card to be held, then drop at screen center
            yield return new WaitForSeconds(CardPickupDelay);

            // Step 3: Click screen center via Unity events (confirm play)
            Log.Activation("UIActivator", "Step 3: Click screen center via Unity events (confirm play)");

            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            SimulateClickAtPosition(center);

            // Step 4: Wait for game to process the play and UI to update
            // Need enough time for targeting UI (Cancel/Submit buttons) to appear
            yield return new WaitForSeconds(CardDropDelay);

            // Note: Targeting mode is now handled by DuelNavigator's auto-detection
            // which checks for spell on stack + HotHighlight targets.
            // This avoids false positives from activated abilities that don't target.

            Log.Activation("UIActivator", "=== CARD PLAY COMPLETE ===");
            callback?.Invoke(true, "Card played");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Special handling for Nav_Mail button - the CustomButton._onClick has no listeners.
        /// We need to find NavBarController in the parent hierarchy and invoke MailboxButton_OnClick() directly.
        /// </summary>
        private static bool TryInvokeNavBarMailboxClick(GameObject element, out ActivationResult result)
        {
            result = default;

            // Search up the hierarchy for NavBarController
            var current = element.transform.parent;
            MonoBehaviour navBarController = null;
            int depth = 0;

            while (current != null && depth < 10)
            {
                foreach (var comp in current.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == "NavBarController")
                    {
                        navBarController = comp;
                        break;
                    }
                }
                if (navBarController != null) break;
                current = current.parent;
                depth++;
            }

            if (navBarController == null)
            {
                Log.Activation("UIActivator", "Nav_Mail: NavBarController not found in parent hierarchy");
                return false;
            }

            Log.Activation("UIActivator", $"Nav_Mail: Found NavBarController on {navBarController.gameObject.name}");

            // Invoke MailboxButton_OnClick()
            var type = navBarController.GetType();
            var method = type.GetMethod("MailboxButton_OnClick",
                AllInstanceFlags);

            if (method != null)
            {
                try
                {
                    Log.Activation("UIActivator", "Nav_Mail: Invoking NavBarController.MailboxButton_OnClick()");
                    method.Invoke(navBarController, null);
                    result = new ActivationResult(true, "Mailbox opened", ActivationType.Button);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log.Activation("UIActivator", $"Nav_Mail: MailboxButton_OnClick() error: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            else
            {
                Log.Activation("UIActivator", "Nav_Mail: MailboxButton_OnClick method not found on NavBarController");
            }

            return false;
        }

        /// <summary>
        /// Special handling for NPE reward claim button (NullClaimButton).
        /// Invokes PutAwayRewards() on NPEContentControllerRewards - this is the same method
        /// the game uses when the user presses Escape on the reward screen.
        /// Falls back to OnClaimClicked_Unity() on the base class if PutAwayRewards is not found.
        /// </summary>
        private static bool TryInvokeNPERewardClaim(GameObject element, out ActivationResult result)
        {
            result = default;

            // Only handle NullClaimButton
            if (element.name != "NullClaimButton")
                return false;

            // Verify we're inside NPE-Rewards_Container
            bool insideNPE = false;
            var current = element.transform.parent;
            while (current != null)
            {
                if (current.name == "NPE-Rewards_Container")
                {
                    insideNPE = true;
                    break;
                }
                current = current.parent;
            }

            if (!insideNPE)
                return false;

            Log.Activation("UIActivator", $"NullClaimButton detected in NPE rewards, looking for NPEContentControllerRewards");

            // Find NPEContentControllerRewards component
            foreach (var behaviour in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name != "NPEContentControllerRewards") continue;

                Log.Activation("UIActivator", $"Found NPEContentControllerRewards on: {behaviour.gameObject.name}");

                var type = behaviour.GetType();

                // Try PutAwayRewards first - this is the NPE-specific dismiss method
                // (same as what the game calls on Escape key)
                var putAwayMethod = type.GetMethod("PutAwayRewards", AllInstanceFlags);
                if (putAwayMethod != null)
                {
                    try
                    {
                        Log.Activation("UIActivator", $"Invoking NPEContentControllerRewards.PutAwayRewards()");
                        putAwayMethod.Invoke(behaviour, null);
                        result = new ActivationResult(true, Models.Strings.NPE_RewardClaimed, ActivationType.Button);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Activation("UIActivator", $"PutAwayRewards error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                // Fallback to OnClaimClicked_Unity on base class
                var claimMethod = type.GetMethod("OnClaimClicked_Unity", AllInstanceFlags);
                if (claimMethod != null)
                {
                    try
                    {
                        Log.Activation("UIActivator", $"Invoking OnClaimClicked_Unity() (fallback)");
                        claimMethod.Invoke(behaviour, null);
                        result = new ActivationResult(true, Models.Strings.NPE_RewardClaimed, ActivationType.Button);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Activation("UIActivator", $"OnClaimClicked_Unity error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
                else
                {
                    Log.Activation("UIActivator", $"Neither PutAwayRewards nor OnClaimClicked_Unity found");
                }
            }

            Log.Activation("UIActivator", $"NPEContentControllerRewards not found, falling back to standard activation");
            return false;
        }

        /// <summary>
        /// Special handling for UpdatePoliciesPanel - the button's onClick listener has a broken/null target reference.
        /// We find the panel controller and invoke OnAccept directly.
        /// </summary>
        private static bool TryInvokeUpdatePoliciesAccept(GameObject element, out ActivationResult result)
        {
            result = default;

            // Only handle buttons named MainButton_Register on UpdatePolicies panel
            if (!element.name.Contains("MainButton_Register"))
                return false;

            // Search up from the button to find the panel
            var panel = element.transform;
            while (panel != null && !panel.name.Contains("UpdatePolicies"))
                panel = panel.parent;

            if (panel == null)
                return false;

            // Find UpdatePoliciesPanel component and invoke OnAccept
            foreach (var comp in panel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if (comp.GetType().Name != "UpdatePoliciesPanel") continue;

                var onAcceptMethod = comp.GetType().GetMethod("OnAccept",
                    AllInstanceFlags);

                if (onAcceptMethod != null)
                {
                    try
                    {
                        Log.Activation("UIActivator", $"Invoking UpdatePoliciesPanel.OnAccept directly");
                        onAcceptMethod.Invoke(comp, null);
                        result = new ActivationResult(true, "Accepted", ActivationType.PointerClick);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Activation("UIActivator", $"UpdatePoliciesPanel.OnAccept threw: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to invoke CustomButton onClick via reflection.
        /// Some CustomButtons don't respond reliably to pointer events on first click.
        /// </summary>
        private static ActivationResult TryInvokeCustomButtonOnClick(GameObject element)
        {
            foreach (var mb in element.GetComponents<MonoBehaviour>())
            {
                if (!IsCustomButton(mb))
                    continue;

                var type = mb.GetType();

                // Try to find _onClick field (CustomButton uses underscore prefix)
                var onClickField = type.GetField("_onClick",
                    AllInstanceFlags);

                if (onClickField != null)
                {
                    var onClick = onClickField.GetValue(mb);
                    if (onClick != null)
                    {
                        var invokeMethod = onClick.GetType().GetMethod("Invoke", System.Type.EmptyTypes);
                        if (invokeMethod != null)
                        {
                            Log.Activation("UIActivator", $"Invoking CustomButton.onClick via reflection on: {element.name}");
                            try
                            {
                                invokeMethod.Invoke(onClick, null);
                                return new ActivationResult(true, Models.Strings.ActivatedBare, ActivationType.Button);
                            }
                            catch (System.Exception ex)
                            {
                                // Game's event handler may throw (e.g., HomePageBillboard not initialized)
                                // Fall back to pointer simulation
                                Log.Activation("UIActivator", $"CustomButton.onClick threw exception, falling back to pointer: {ex.InnerException?.Message ?? ex.Message}");
                                return SimulatePointerClick(element);
                            }
                        }
                        else
                        {
                            Log.Activation("UIActivator", $"CustomButton.onClick has no Invoke() method on: {element.name}");
                        }
                    }
                    else
                    {
                        Log.Activation("UIActivator", $"CustomButton.onClick is null on: {element.name}");
                    }
                }
                else
                {
                    Log.Activation("UIActivator", $"CustomButton has no _onClick field on: {element.name}");
                }
            }

            return new ActivationResult(false, "No CustomButton onClick found");
        }

        /// <summary>
        /// Searches the element and its descendants for the actual clickable target.
        /// Returns the first GameObject that has a CustomButton or IPointerClickHandler.
        /// This handles cases where the navigable element is a container (like HomeBanner)
        /// but the actual click handler is on a child.
        /// </summary>
        private static GameObject FindClickableInHierarchy(GameObject root)
        {
            if (root == null) return null;

            // Check root first
            if (HasClickHandler(root))
                return root;

            // Search children recursively (breadth-first to find closest match)
            var queue = new System.Collections.Generic.Queue<Transform>();
            foreach (Transform child in root.transform)
            {
                queue.Enqueue(child);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !current.gameObject.activeInHierarchy)
                    continue;

                if (HasClickHandler(current.gameObject))
                {
                    return current.gameObject;
                }

                // Add children to queue
                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a GameObject has a click handler (CustomButton or IPointerClickHandler).
        /// </summary>
        private static bool HasClickHandler(GameObject obj)
        {
            // Check for CustomButton component
            foreach (var mb in obj.GetComponents<MonoBehaviour>())
            {
                if (IsCustomButton(mb))
                    return true;
            }

            // Check for IPointerClickHandler (excluding TooltipTrigger which just shows tooltips)
            var clickHandlers = obj.GetComponents<IPointerClickHandler>();
            foreach (var handler in clickHandlers)
            {
                // TooltipTrigger implements IPointerClickHandler but doesn't activate buttons
                if (handler.GetType().Name != TooltipTriggerTypeName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a GameObject has a CustomButton component.
        /// Used to determine activation strategy - CustomButtons need pointer events first.
        /// </summary>
        private static bool HasCustomButtonComponent(GameObject obj)
        {
            foreach (var mb in obj.GetComponents<MonoBehaviour>())
            {
                if (IsCustomButton(mb))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Finds a MonoBehaviour component by type name.
        /// </summary>
        private static MonoBehaviour FindComponentByName(GameObject obj, string typeName)
        {
            foreach (var mb in obj.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == typeName)
                    return mb;
            }
            return null;
        }

        /// <summary>
        /// Finds the popup root GameObject by traversing up the hierarchy.
        /// Looks for SystemMessageView or similar popup container names.
        /// </summary>
        private static GameObject FindPopupRoot(GameObject element)
        {
            if (element == null) return null;

            var current = element.transform.parent;
            while (current != null)
            {
                string name = current.gameObject.name;
                if (name.Contains("SystemMessageView") ||
                    name.Contains("Popup") ||
                    name.Contains("Dialog") ||
                    name.Contains("Modal"))
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Finds the SystemMessageView component in the element's hierarchy.
        /// Searches both up the hierarchy and in parent's children.
        /// </summary>
        private static MonoBehaviour FindSystemMessageView(GameObject element)
        {
            if (element == null) return null;

            // First, search up the hierarchy
            var current = element.transform;
            while (current != null)
            {
                var systemMsgView = FindComponentByName(current.gameObject, "SystemMessageView");
                if (systemMsgView != null)
                    return systemMsgView;

                current = current.parent;
            }

            // Also check the popup root and its children
            var popupRoot = FindPopupRoot(element);
            if (popupRoot != null)
            {
                // Search in the popup root's hierarchy
                foreach (var mb in popupRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "SystemMessageView")
                        return mb;
                }

                // Also search in parent of popup root
                var parent = popupRoot.transform.parent;
                while (parent != null)
                {
                    foreach (var mb in parent.GetComponents<MonoBehaviour>())
                    {
                        if (mb != null && mb.GetType().Name == "SystemMessageView")
                            return mb;
                    }
                    parent = parent.parent;
                }
            }

            // Last resort: find any active SystemMessageView in scene
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "SystemMessageView" && mb.gameObject.activeInHierarchy)
                {
                    Log.Activation("UIActivator", $"Found SystemMessageView via scene search: {mb.gameObject.name}");
                    return mb;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to find and invoke the internal handleOnClick callback stored in SystemMessageButtonView.
        /// The callback is passed during Init() and stored in a private field.
        /// </summary>
        private static bool TryInvokeStoredCallback(MonoBehaviour systemMsgButton)
        {
            if (systemMsgButton == null) return false;

            var type = systemMsgButton.GetType();
            var flags = PrivateInstance;

            // Look for private fields that might store the callback
            string[] possibleCallbackFields = { "_handleOnClick", "_callback", "_onClick", "handleOnClick", "callback", "m_handleOnClick" };
            string[] possibleDataFields = { "_buttonData", "_data", "buttonData", "m_buttonData" };

            object callbackObj = null;
            object buttonDataObj = null;

            // Find callback field
            foreach (var fieldName in possibleCallbackFields)
            {
                var field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    callbackObj = field.GetValue(systemMsgButton);
                    if (callbackObj != null)
                    {
                        Log.Activation("UIActivator", $"Found callback in field '{fieldName}': {callbackObj.GetType().Name}");
                        break;
                    }
                }
            }

            // Find buttonData field
            foreach (var fieldName in possibleDataFields)
            {
                var field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    buttonDataObj = field.GetValue(systemMsgButton);
                    if (buttonDataObj != null)
                    {
                        Log.Activation("UIActivator", $"Found buttonData in field '{fieldName}': {buttonDataObj.GetType().Name}");
                        break;
                    }
                }
            }

            // Log all fields for debugging
            Log.Activation("UIActivator", $"Listing all fields on {type.Name}:");
            foreach (var field in type.GetFields(flags | System.Reflection.BindingFlags.Public))
            {
                var val = field.GetValue(systemMsgButton);
                Log.Activation("UIActivator", $"  Field: {field.Name} ({field.FieldType.Name}) = {(val != null ? val.ToString() : "null")}");
            }

            // Try to invoke the callback if found
            if (callbackObj != null)
            {
                var invokeMethod = callbackObj.GetType().GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    try
                    {
                        var parameters = invokeMethod.GetParameters();
                        if (parameters.Length == 0)
                        {
                            Log.Activation("UIActivator", $"Invoking callback with no parameters");
                            invokeMethod.Invoke(callbackObj, null);
                            return true;
                        }
                        else if (parameters.Length == 1 && buttonDataObj != null)
                        {
                            Log.Activation("UIActivator", $"Invoking callback with buttonData");
                            invokeMethod.Invoke(callbackObj, new object[] { buttonDataObj });
                            return true;
                        }
                        else if (parameters.Length == 1)
                        {
                            Log.Activation("UIActivator", $"Invoking callback with null parameter");
                            invokeMethod.Invoke(callbackObj, new object[] { null });
                            return true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Activation("UIActivator", $"Error invoking callback: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to invoke OnClickedReturnSource on CustomButton component.
        /// </summary>
        private static bool TryInvokeOnClickedReturnSource(GameObject element)
        {
            foreach (var mb in element.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "CustomButton") continue;

                var type = mb.GetType();
                var flags = AllInstanceFlags;

                // Look for OnClickedReturnSource field
                var field = type.GetField("OnClickedReturnSource", flags);
                if (field == null)
                    field = type.GetField("_onClickedReturnSource", flags);
                if (field == null)
                    field = type.GetField("onClickedReturnSource", flags);

                if (field != null)
                {
                    var actionObj = field.GetValue(mb);
                    if (actionObj != null)
                    {
                        Log.Activation("UIActivator", $"Found OnClickedReturnSource: {actionObj.GetType().Name}");
                        var invokeMethod = actionObj.GetType().GetMethod("Invoke");
                        if (invokeMethod != null)
                        {
                            try
                            {
                                var parameters = invokeMethod.GetParameters();
                                if (parameters.Length == 0)
                                {
                                    invokeMethod.Invoke(actionObj, null);
                                    return true;
                                }
                                else if (parameters.Length == 1)
                                {
                                    // Pass the CustomButton itself as source
                                    invokeMethod.Invoke(actionObj, new object[] { mb });
                                    return true;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Log.Activation("UIActivator", $"Error invoking OnClickedReturnSource: {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Log.Activation("UIActivator", $"OnClickedReturnSource field found but value is null");
                    }
                }

                // Log all fields on CustomButton for debugging
                Log.Activation("UIActivator", $"Listing all fields on CustomButton:");
                foreach (var f in type.GetFields(flags))
                {
                    try
                    {
                        var val = f.GetValue(mb);
                        string valStr = val?.ToString() ?? "null";
                        if (valStr.Length > 50) valStr = valStr.Substring(0, 50) + "...";
                        Log.Activation("UIActivator", $"  Field: {f.Name} ({f.FieldType.Name}) = {valStr}");
                    }
                    catch
                    {
                        Log.Activation("UIActivator", $"  Field: {f.Name} ({f.FieldType.Name}) = <error reading>");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to dismiss popup via SystemMessageManager singleton.
        /// </summary>
        private static bool TryDismissViaSystemMessageManager()
        {
            // Find SystemMessageManager type in loaded assemblies
            System.Type managerType = FindType("SystemMessageManager");

            if (managerType == null) return false;

            // Get singleton Instance
            var instanceProp = managerType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp == null) return false;

            var instance = instanceProp.GetValue(null);
            if (instance == null) return false;

            // Try ClearMessageQueue() - this dismisses the current popup
            var flags = AllInstanceFlags;
            var clearMethod = managerType.GetMethod("ClearMessageQueue", flags, null, System.Type.EmptyTypes, null);
            if (clearMethod != null)
            {
                try
                {
                    clearMethod.Invoke(instance, null);
                    Log.Activation("UIActivator", $"SystemMessageManager.ClearMessageQueue() succeeded");
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log.Activation("UIActivator", $"ClearMessageQueue() failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// Try to find PopupManager and close the current popup.
        /// </summary>
        private static bool TryCloseViaPopupManager()
        {
            // Try to find PopupManager singleton
            var popupManagerType = FindType("Core.Meta.MainNavigation.PopUps.PopupManager")
                                ?? FindType("PopupManager");

            if (popupManagerType == null)
            {
                Log.Activation("UIActivator", $"PopupManager type not found");
                return false;
            }

            // Get Instance property
            var instanceProp = popupManagerType.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp == null)
            {
                Log.Activation("UIActivator", $"PopupManager.Instance property not found");
                return false;
            }

            var instance = instanceProp.GetValue(null);
            if (instance == null)
            {
                Log.Activation("UIActivator", $"PopupManager.Instance is null");
                return false;
            }

            Log.Activation("UIActivator", $"Found PopupManager instance: {instance}");

            // Try to find close/dismiss methods
            var flags = AllInstanceFlags;

            string[] methodNames = { "Close", "ClosePopup", "Dismiss", "DismissPopup", "Hide", "HidePopup", "OnBack", "CloseCurrentPopup" };
            foreach (var methodName in methodNames)
            {
                var method = popupManagerType.GetMethod(methodName, flags, null, System.Type.EmptyTypes, null);
                if (method != null)
                {
                    try
                    {
                        Log.Activation("UIActivator", $"Trying PopupManager.{methodName}()");
                        method.Invoke(instance, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Activation("UIActivator", $"PopupManager.{methodName}() failed: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            // Try OnBack with null ActionContext
            var onBackMethod = popupManagerType.GetMethod("OnBack", flags);
            if (onBackMethod != null)
            {
                try
                {
                    Log.Activation("UIActivator", $"Trying PopupManager.OnBack(null)");
                    onBackMethod.Invoke(instance, new object[] { null });
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log.Activation("UIActivator", $"PopupManager.OnBack() failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // Log available methods
            Log.Activation("UIActivator", $"Available methods on PopupManager:");
            foreach (var m in popupManagerType.GetMethods(flags))
            {
                Log.Activation("UIActivator", $"  {m.Name}({string.Join(", ", System.Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
            }

            return false;
        }

        /// <summary>
        /// Try to invoke HandleKeyDown(KeyCode, Modifiers) on a component.
        /// This simulates a key press through the game's input handling.
        /// </summary>
        private static bool TryInvokeHandleKeyDown(MonoBehaviour component, KeyCode keyCode)
        {
            if (component == null) return false;

            var type = component.GetType();
            var flags = AllInstanceFlags;

            // Find HandleKeyDown method
            foreach (var method in type.GetMethods(flags))
            {
                if (method.Name == "HandleKeyDown")
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length >= 1)
                    {
                        try
                        {
                            Log.Activation("UIActivator", $"Found HandleKeyDown with {parameters.Length} params, invoking with {keyCode}");

                            if (parameters.Length == 1)
                            {
                                // HandleKeyDown(KeyCode)
                                method.Invoke(component, new object[] { keyCode });
                                return true;
                            }
                            else if (parameters.Length == 2)
                            {
                                // HandleKeyDown(KeyCode, Modifiers) - pass default modifiers
                                var modifiersType = parameters[1].ParameterType;
                                object modifiersValue;

                                if (modifiersType.IsEnum)
                                {
                                    modifiersValue = System.Enum.ToObject(modifiersType, 0);
                                }
                                else if (modifiersType.IsValueType)
                                {
                                    // Struct - create default instance
                                    modifiersValue = System.Activator.CreateInstance(modifiersType);
                                }
                                else
                                {
                                    // Reference type - pass null
                                    modifiersValue = null;
                                }

                                Log.Activation("UIActivator", $"Modifiers type: {modifiersType.Name}, IsEnum: {modifiersType.IsEnum}, IsValueType: {modifiersType.IsValueType}");
                                method.Invoke(component, new object[] { keyCode, modifiersValue });
                                return true;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Activation("UIActivator", $"HandleKeyDown error: {ex.InnerException?.Message ?? ex.Message}");
                        }
                    }
                }
            }

            Log.Activation("UIActivator", $"HandleKeyDown method not found on {type.Name}");
            return false;
        }

        /// <summary>
        /// Tries to invoke OnBack(ActionContext) on a component.
        /// ActionContext can be null - the game handles this gracefully.
        /// </summary>
        private static bool TryInvokeOnBack(MonoBehaviour component)
        {
            if (component == null) return false;

            var type = component.GetType();

            // Try OnBack(ActionContext) first - pass null for context
            var onBackMethod = type.GetMethod("OnBack",
                AllInstanceFlags,
                null,
                new System.Type[] { typeof(object) }, // ActionContext
                null);

            if (onBackMethod == null)
            {
                // Try to find the method with any parameter type
                foreach (var method in type.GetMethods(AllInstanceFlags))
                {
                    if (method.Name == "OnBack")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1)
                        {
                            onBackMethod = method;
                            break;
                        }
                    }
                }
            }

            if (onBackMethod != null)
            {
                try
                {
                    Log.Activation("UIActivator", $"Invoking {type.Name}.OnBack(null)");
                    onBackMethod.Invoke(component, new object[] { null });
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log.Activation("UIActivator", $"Error invoking OnBack: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            else
            {
                Log.Activation("UIActivator", $"OnBack method not found on {type.Name}");
            }

            return false;
        }

        /// <summary>
        /// Checks if an element is inside a popup (e.g. AdvancedFiltersPopup).
        /// Used to prevent MainButton special handling from firing on popup buttons.
        /// </summary>
        private static bool IsInsidePopup(GameObject element)
        {
            var t = element.transform.parent;
            while (t != null)
            {
                if (t.name.Contains("Popup(Clone)"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>
        /// Finds the WrapperDeckBuilder controller component which handles deck builder button clicks.
        /// The MainButton's OnPlayButton listener has a NULL target, so we need to find it manually.
        /// </summary>
        private static MonoBehaviour FindDeckBuilderController()
        {
            // Look for WrapperDeckBuilder component in the scene
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == T.WrapperDeckBuilder)
                {
                    return mb;
                }
            }

            // Fallback: try to find DeckBuilderController or similar
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name.Contains("DeckBuilder") && mb.GetType().Name.Contains("Controller"))
                {
                    return mb;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to invoke a parameterless method on a component by name.
        /// </summary>
        private static bool TryInvokeMethod(MonoBehaviour component, string methodName)
        {
            if (component == null) return false;

            var type = component.GetType();
            var method = type.GetMethod(methodName,
                AllInstanceFlags,
                null,
                System.Type.EmptyTypes,
                null);

            if (method != null)
            {
                try
                {
                    Log.Activation("UIActivator", $"Invoking {type.Name}.{methodName}()");
                    method.Invoke(component, null);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log.Activation("UIActivator", $"Error invoking {methodName}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            return false;
        }

        /// <summary>
        /// Diagnostic: when registration button is not interactable, log which validation
        /// condition in RegistrationPanel._checkFields() is failing.
        /// </summary>
        private static void DiagnoseRegistrationState(GameObject element)
        {
            try
            {
                // Only run for the registration submit button
                if (element.name != "MainButton_Register") return;

                // Find the RegistrationPanel in the hierarchy
                var regPanelType = FindType("Wotc.Mtga.Login.RegistrationPanel");
                if (regPanelType == null) return;

                var panel = Object.FindObjectOfType(regPanelType);
                if (panel == null) return;

                // Check _validDisplayName (private bool)
                var validDnField = regPanelType.GetField("_validDisplayName", PrivateInstance);
                if (validDnField != null)
                {
                    bool validDn = (bool)validDnField.GetValue(panel);
                    Log.Activation("UIActivator", $"  _validDisplayName = {validDn}");
                }

                // Check _submitting
                var submittingField = regPanelType.GetField("_submitting", PrivateInstance);
                if (submittingField != null)
                {
                    bool submitting = (bool)submittingField.GetValue(panel);
                    Log.Activation("UIActivator", $"  _submitting = {submitting}");
                }

                // Check input field texts via the UIWidget fields
                string[] fieldNames = { "displayName_inputField", "email_inputField", "email2_inputField", "password_inputField", "password2_inputField" };
                foreach (var fn in fieldNames)
                {
                    var widgetField = regPanelType.GetField(fn, PrivateInstance);
                    if (widgetField != null)
                    {
                        var widget = widgetField.GetValue(panel);
                        if (widget != null)
                        {
                            var inputFieldProp = widget.GetType().GetProperty("InputField", PublicInstance);
                            if (inputFieldProp != null)
                            {
                                var inputField = inputFieldProp.GetValue(widget) as TMPro.TMP_InputField;
                                if (inputField != null)
                                {
                                    bool isPassword = fn.Contains("password");
                                    string text = isPassword ? $"(len={inputField.text?.Length ?? 0})" : $"'{inputField.text}'";
                                    Log.Activation("UIActivator", $"  {fn}: {text}, enabled={inputField.enabled}");
                                }
                            }
                        }
                    }
                }

                // Check required toggles
                string[] toggleNames = { "termsAndConditions_Toggle", "codeOfConduct_Toggle", "privacyPolicy_Toggle" };
                foreach (var tn in toggleNames)
                {
                    var toggleField = regPanelType.GetField(tn, PrivateInstance);
                    if (toggleField != null)
                    {
                        var toggle = toggleField.GetValue(panel) as Toggle;
                        if (toggle != null)
                        {
                            Log.Activation("UIActivator", $"  {tn}: isOn={toggle.isOn}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Activation("UIActivator", $"  DiagnoseRegistrationState error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a PointerEventData centered on the given element.
        /// </summary>
        private static PointerEventData CreatePointerEventData(GameObject element)
        {
            Vector2 screenPos = GetScreenPosition(element);
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                pointerPress = element,
                pointerEnter = element,
                position = screenPos,
                pressPosition = screenPos
            };
        }

        /// <summary>
        /// Creates a PointerEventData with a specific screen position override.
        /// Used for battlefield cards to ensure the position matches the card's actual location.
        /// </summary>
        private static PointerEventData CreatePointerEventData(GameObject element, Vector2 screenPosition)
        {
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                pointerPress = element,
                pointerEnter = element,
                position = screenPosition,
                pressPosition = screenPosition
            };
        }

        /// <summary>
        /// Gets the screen position of a UI element's center.
        /// </summary>
        private static Vector2 GetScreenPosition(GameObject obj)
        {
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                Vector3 center = (corners[0] + corners[2]) / 2f;

                var canvas = obj.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
                    return canvas.worldCamera.WorldToScreenPoint(center);

                return center;
            }

            return new Vector2(Screen.width / 2, Screen.height / 2);
        }

        /// <summary>
        /// Checks if the game is waiting for target selection.
        /// Detection: Looks for "Submit X" button (e.g., "Submit 0", "Submit 1").
        /// Note: Cancel button alone is not reliable - can appear for other purposes.
        /// Results are cached for TargetingCacheTimeout seconds to avoid expensive scans.
        /// </summary>
        private static bool IsTargetingModeActive()
        {
            // Return cached result if still valid
            float currentTime = Time.time;
            if (currentTime - _lastTargetingScanTime < TargetingCacheTimeout)
            {
                return _cachedTargetingResult;
            }

            // Perform the expensive scan
            _lastTargetingScanTime = currentTime;
            _cachedTargetingResult = ScanForSubmitButton();
            return _cachedTargetingResult;
        }

        /// <summary>
        /// Scans all text components for a Submit button pattern.
        /// </summary>
        private static bool ScanForSubmitButton()
        {
            // Check TMP_Text components (used by StyledButton)
            foreach (var tmpText in GameObject.FindObjectsOfType<TMP_Text>())
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                if (IsSubmitButton(tmpText.text, tmpText.transform))
                    return true;
            }

            // Also check legacy Text components
            foreach (var uiText in GameObject.FindObjectsOfType<Text>())
            {
                if (uiText == null || !uiText.gameObject.activeInHierarchy)
                    continue;

                if (IsSubmitButton(uiText.text, uiText.transform))
                    return true;
            }

            // Note: Cancel button alone is NOT a reliable indicator - there can be Cancel buttons
            // for other purposes (Cancel Attacks, etc.). Only the "Submit X" pattern reliably
            // indicates discard/selection mode.
            return false;
        }

        /// <summary>
        /// Checks if text matches the Submit button pattern and is inside a button/prompt element.
        /// </summary>
        private static bool IsSubmitButton(string text, Transform textTransform)
        {
            string trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return false;

            // Check for "Submit X" pattern (e.g., "Submit 0", "Submit 1")
            if (!SubmitButtonPattern.IsMatch(trimmed))
                return false;

            // Verify it's inside a button or prompt element
            var parent = textTransform.parent;
            while (parent != null)
            {
                string parentName = parent.name.ToLower();
                if (parentName.Contains("button") || parentName.Contains("prompt"))
                {
                    Log.Activation("UIActivator", $"Found Submit button: '{trimmed}' on {parent.name}");
                    return true;
                }
                parent = parent.parent;
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// Result of a UI element activation attempt.
    /// </summary>
    public struct ActivationResult
    {
        public bool Success { get; }
        public string Message { get; }
        public ActivationType Type { get; }

        public ActivationResult(bool success, string message, ActivationType type = ActivationType.Unknown)
        {
            Success = success;
            Message = message;
            Type = type;
        }
    }

    /// <summary>
    /// Types of UI elements that can be activated.
    /// </summary>
    public enum ActivationType
    {
        Unknown,
        Button,
        Toggle,
        InputField,
        PointerClick
    }
}
