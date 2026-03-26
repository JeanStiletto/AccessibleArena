using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the booster pack card list that appears after opening a pack.
    /// Uses the controller's _cardsToOpen field as the authoritative source for detection.
    /// </summary>
    public class BoosterOpenNavigator : BaseNavigator
    {
        private Component _controller;
        private GameObject _revealAllButton;
        private int _totalCards;
        private int _expectedCardCount;
        private bool _animSkipped;

        // Cached reflection info (types don't change between scenes)
        private static FieldInfo _cardsToOpenField;
        private static PropertyInfo _hiddenProp;
        private static FieldInfo _onScreenHoldersField;
        private static PropertyInfo _cardViewsProp;
        private static FieldInfo _animActiveField;
        private static MethodInfo _stopAnimMethod;
        private static FieldInfo _autoRevealField;

        // Periodic rescan until cards are found (animation event spawns cards ~2.5s after detection)
        private int _rescanFrameCounter;
        private const int RescanIntervalFrames = 30; // ~0.5 seconds at 60fps
        private int _rescanAttempt;
        private const int MaxRescanAttempts = 20; // ~10 seconds total
        private bool _rescanDone;

        // Delayed rescan after close action
        private bool _closeTriggered;
        private int _closeRescanCounter;

        public override string NavigatorId => "BoosterOpen";
        public override string ScreenName => GetScreenName();
        public override int Priority => 80; // Higher than GeneralMenuNavigator (15), below OverlayNavigator (85)

        public BoosterOpenNavigator(IAnnouncementService announcer) : base(announcer) { }

        private string GetScreenName()
        {
            if (_totalCards > 0)
                return Strings.ScreenPackContentsCount(_totalCards);
            return Strings.ScreenPackContents;
        }

        protected override bool DetectScreen()
        {
            // Look for the BoosterChamber
            var boosterChamber = GameObject.Find("ContentController - BoosterChamber_v2_Desktop_16x9(Clone)");
            if (boosterChamber == null || !boosterChamber.activeInHierarchy)
            {
                _controller = null;
                return false;
            }

            // Find the BoosterOpenToScrollListController component
            var controller = FindScrollListController(boosterChamber);
            if (controller == null)
            {
                _controller = null;
                return false;
            }

            // Check if _cardsToOpen is populated (null = no pack opened, empty = cleared)
            var cards = GetCardsToOpen(controller);
            if (cards == null || cards.Count == 0)
            {
                _controller = null;
                return false;
            }

            _controller = controller;
            _expectedCardCount = cards.Count;

            // Clear AutoReveal immediately — before the animation starts spawning cards.
            // SpawnCard auto-reveals cards with AutoReveal=true and sets Revealed=true,
            // so we must clear it before any SpawnCard call happens.
            ClearAutoReveal();

            MelonLogger.Msg($"[{NavigatorId}] Pack opened with {cards.Count} cards");
            return true;
        }

        /// <summary>
        /// Find the BoosterOpenToScrollListController component in the booster chamber.
        /// </summary>
        private Component FindScrollListController(GameObject boosterChamber)
        {
            foreach (var mb in boosterChamber.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == T.BoosterOpenToScrollListController)
                    return mb;
            }
            return null;
        }

        /// <summary>
        /// Read the _cardsToOpen list from the controller via reflection.
        /// Returns null if field not found or list is null.
        /// </summary>
        private IList GetCardsToOpen(Component controller)
        {
            if (_cardsToOpenField == null)
            {
                _cardsToOpenField = controller.GetType().GetField("_cardsToOpen", PrivateInstance);
                if (_cardsToOpenField == null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] _cardsToOpen field not found");
                    return null;
                }
            }
            return _cardsToOpenField.GetValue(controller) as IList;
        }

        protected override void DiscoverElements()
        {
            _totalCards = 0;
            var addedObjects = new HashSet<GameObject>();

            // Find RevealAll button first
            FindRevealAllButton(addedObjects);

            // Find card entries
            FindCardEntries(addedObjects);

            // Find dismiss/continue button
            FindDismissButton(addedObjects);
        }

        private void FindRevealAllButton(HashSet<GameObject> addedObjects)
        {
            // Look for RevealAll_MainButtonOutline_v2 or similar
            // Note: We track this button for auto-closing but don't add it to navigation
            // since blind users don't need to manually click it - we auto-click it when closing
            var customButtons = FindCustomButtonsInScene();

            foreach (var button in customButtons)
            {
                string name = button.name;
                if (name.Contains("RevealAll") || name.Contains("Reveal_All"))
                {
                    addedObjects.Add(button); // Mark as processed so it's not added elsewhere
                    _revealAllButton = button;
                    MelonLogger.Msg($"[{NavigatorId}] Found RevealAll button (hidden from nav): {name}");
                    return;
                }
            }
        }

        private void FindCardEntries(HashSet<GameObject> addedObjects)
        {
            var cardEntries = new List<(GameObject obj, float sortOrder)>();

            // Primary: read on-screen card holders from controller's dictionary
            FindCardsFromController(cardEntries, addedObjects);

            // Fallback: search for BoosterCardHolder components in the scene
            if (cardEntries.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] No cards from controller, searching by component type");
                FindCardsByComponentType(cardEntries, addedObjects);
            }

            // Sort cards by descending index (common cards first, rare last)
            cardEntries = cardEntries.OrderByDescending(x => x.sortOrder).ToList();

            MelonLogger.Msg($"[{NavigatorId}] Found {cardEntries.Count} entries (cards + vault progress)");

            // Add cards to navigation
            int cardNum = 1;
            foreach (var (cardObj, _) in cardEntries)
            {
                // Check if card is face-down (hidden) via BoosterCardHolder.Hidden
                bool isHidden = IsCardHidden(cardObj);

                var cardInfo = CardDetector.ExtractCardInfo(cardObj);
                string cardName = ExtractCardName(cardObj);

                // Prefer model-based name (authoritative, immune to stale/not-yet-populated UI text)
                // Fall back to UI-extracted name only when model has no data (e.g., vault progress entries)
                string displayName;
                if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.Name))
                    displayName = cardInfo.Name;
                else if (!string.IsNullOrEmpty(cardName))
                    displayName = cardName;
                else
                    displayName = "Unknown card";

                // Log unknown cards for debugging (use F11 on this card for full details)
                if (displayName == "Unknown card" && !isHidden)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Card {cardNum} extraction failed: {cardObj.name} - press F11 while focused for details");
                }

                // Check if this is vault progress (not a real card)
                bool isVaultProgress = displayName.Contains("Vault Progress");

                string label;
                if (isHidden)
                {
                    // Face-down card - always show as hidden, regardless of other text
                    label = Strings.HiddenCard;
                    cardNum++;
                }
                else if (isVaultProgress)
                {
                    label = displayName;
                }
                else
                {
                    // Just card name and type, no "Card X:" prefix
                    label = displayName;
                    if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.TypeLine))
                    {
                        label += $", {cardInfo.TypeLine}";
                    }
                    cardNum++;
                }

                AddElement(cardObj, label);
            }

            // Set total cards count (excluding vault progress)
            _totalCards = cardNum - 1;
            MelonLogger.Msg($"[{NavigatorId}] Total: {_totalCards} cards");
        }

        /// <summary>
        /// Primary card discovery: read on-screen card holders from the controller's
        /// _onScreenboosterCardHoldersWithIndex dictionary (Dictionary of int, BoosterCardHolder).
        /// Uses CardViews[0] (BoosterMetaCardView) as the navigable element for card info extraction.
        /// </summary>
        private void FindCardsFromController(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects)
        {
            if (_controller == null) return;

            if (_onScreenHoldersField == null)
                _onScreenHoldersField = _controller.GetType().GetField("_onScreenboosterCardHoldersWithIndex", PrivateInstance);

            if (_onScreenHoldersField == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] _onScreenboosterCardHoldersWithIndex field not found");
                return;
            }

            var holdersObj = _onScreenHoldersField.GetValue(_controller);
            var dict = holdersObj as System.Collections.IDictionary;
            if (dict == null || dict.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] No on-screen card holders (count={dict?.Count ?? -1})");
                return;
            }

            MelonLogger.Msg($"[{NavigatorId}] Found {dict.Count} on-screen card holders");

            foreach (DictionaryEntry entry in dict)
            {
                int index = (int)entry.Key;
                var holder = entry.Value as Component;
                if (holder == null) continue;

                // Get the first CardView from the holder - needed for card info extraction
                var cardObj = GetFirstCardView(holder);
                if (cardObj == null)
                {
                    cardObj = holder.gameObject;
                    MelonLogger.Msg($"[{NavigatorId}] No CardView for index {index}, using holder");
                }

                if (!addedObjects.Contains(cardObj))
                {
                    cardEntries.Add((cardObj, (float)index));
                    addedObjects.Add(cardObj);
                }
            }
        }

        /// <summary>
        /// Get the first BoosterMetaCardView GameObject from a BoosterCardHolder's CardViews property.
        /// </summary>
        private GameObject GetFirstCardView(Component holder)
        {
            if (_cardViewsProp == null)
                _cardViewsProp = holder.GetType().GetProperty("CardViews", PublicInstance);

            if (_cardViewsProp != null)
            {
                var cardViews = _cardViewsProp.GetValue(holder) as IList;
                if (cardViews != null && cardViews.Count > 0)
                {
                    var firstView = cardViews[0] as Component;
                    if (firstView != null)
                        return firstView.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Fallback: search entire scene for BoosterCardHolder components by type name.
        /// </summary>
        private void FindCardsByComponentType(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects)
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != T.BoosterCardHolder) continue;

                var cardObj = GetFirstCardView(mb);
                if (cardObj == null) cardObj = mb.gameObject;
                if (addedObjects.Contains(cardObj)) continue;

                float sortOrder = mb.transform.position.x;
                cardEntries.Add((cardObj, sortOrder));
                addedObjects.Add(cardObj);
            }

            MelonLogger.Msg($"[{NavigatorId}] Found {cardEntries.Count} cards by component type search");
        }

        private string ExtractCardName(GameObject cardObj)
        {
            // Try to find the Title text element directly
            string title = null;
            string progressQuantity = null;
            var vaultTags = new System.Collections.Generic.List<string>();

            // Include inactive text elements (true) for animation timing
            var texts = cardObj.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;

                string objName = text.gameObject.name;
                string parentName = text.transform.parent?.name ?? "";

                // "Title" is the card name element
                if (objName == "Title")
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        // Clean up any markup
                        content = UITextExtractor.StripRichText(content).Trim();
                        if (!string.IsNullOrEmpty(content))
                            title = content;
                    }
                }

                // Check for vault/duplicate progress indicator (e.g., "+99")
                // This appears when you get a 5th+ copy of a common/uncommon
                // Only check ACTIVE elements - the prefab has these on all cards but inactive when not relevant
                if (objName.Contains("Progress") && objName.Contains("Quantity") && text.gameObject.activeInHierarchy)
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                        progressQuantity = content;
                }

                // Collect tags from TAG parent elements (these describe the vault progress type)
                // Structure: Text_1 (parent=TAG_1): 'Alchemy', Text_2 (parent=TAG_2): 'Bonus', etc.
                // Only check ACTIVE elements
                if (parentName.StartsWith("TAG") && text.gameObject.activeInHierarchy)
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        content = UITextExtractor.StripRichText(content).Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            string contentLower = content.ToLowerInvariant();
                            // Skip generic/unhelpful tags
                            if (contentLower == "new" || contentLower == "neu" ||
                                contentLower == "first" || contentLower == "erste" ||
                                contentLower == "faction" || contentLower == "fraktion")
                            {
                                continue;
                            }
                            // Keep meaningful tags (Alchemy, Bonus, rarity names, etc.)
                            if (!vaultTags.Contains(content))
                            {
                                vaultTags.Add(content);
                            }
                        }
                    }
                }
            }

            // If we have a title, return it
            if (!string.IsNullOrEmpty(title))
                return title;

            // If no title but we have progress quantity, this is vault progress (duplicate protection)
            if (!string.IsNullOrEmpty(progressQuantity))
            {
                // Build informative vault progress label
                // Format: "Alchemy Bonus Vault Progress +99" or just "Vault Progress +99"
                string label;
                if (vaultTags.Count > 0)
                {
                    // Combine tags: "Alchemy Bonus" + "Vault Progress" + "+99"
                    string tagPrefix = string.Join(" ", vaultTags);
                    label = $"{tagPrefix} Vault Progress {progressQuantity}";
                }
                else
                {
                    label = $"Vault Progress {progressQuantity}";
                }

                MelonLogger.Msg($"[{NavigatorId}] Detected vault progress: {label} (tags: {string.Join(", ", vaultTags)})");
                return label;
            }

            return null;
        }


        /// <summary>
        /// Check if a card is face-down by reading BoosterCardHolder.Hidden on its parent.
        /// </summary>
        private bool IsCardHidden(GameObject cardObj)
        {
            // BoosterCardHolder is the parent wrapper of BoosterMetaCardView
            Transform current = cardObj.transform;
            while (current != null)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == T.BoosterCardHolder)
                    {
                        if (_hiddenProp == null)
                            _hiddenProp = mb.GetType().GetProperty("Hidden", PublicInstance);
                        if (_hiddenProp != null)
                        {
                            var val = _hiddenProp.GetValue(mb);
                            if (val is bool hidden)
                                return hidden;
                        }
                    }
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Find the parent BoosterCardHolder GameObject for a card view.
        /// The holder has the CustomButton that triggers OnClick -> reveal.
        /// </summary>
        private GameObject FindBoosterCardHolder(GameObject cardObj)
        {
            Transform current = cardObj.transform;
            while (current != null)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == T.BoosterCardHolder)
                        return current.gameObject;
                }
                current = current.parent;
            }
            return null;
        }

        private void FindDismissButton(HashSet<GameObject> addedObjects)
        {
            // Look for continue/dismiss/close buttons
            var customButtons = FindCustomButtonsInScene();

            string[] dismissPatterns = { "Continue", "Close", "Done", "ModalFade", "SkipToEnd", "MainButton" };

            foreach (var button in customButtons)
            {
                if (addedObjects.Contains(button)) continue;

                string name = button.name;
                string buttonText = UITextExtractor.GetButtonText(button, null);

                bool isDismiss = dismissPatterns.Any(p =>
                    name.Contains(p) ||
                    (!string.IsNullOrEmpty(buttonText) && buttonText.Contains(p)));

                if (isDismiss && button != _revealAllButton)
                {
                    // Skip Dismiss_MainButton - Backspace already closes properly via ClosePackProperly()
                    if (name.Contains("Dismiss_MainButton"))
                    {
                        addedObjects.Add(button);
                        MelonLogger.Msg($"[{NavigatorId}] Found Dismiss button (hidden from nav): {name}");
                        continue;
                    }
                    // Use readable label - ModalFade is the background dismiss area
                    string label;
                    if (name.Contains("ModalFade"))
                    {
                        label = "Close";
                    }
                    else if (name.Contains("SkipToEnd"))
                    {
                        // Use the actual button text for Skip to End
                        label = !string.IsNullOrEmpty(buttonText) ? buttonText : "Skip to End";
                    }
                    else if (!string.IsNullOrEmpty(buttonText) && !buttonText.Contains("x10") && !buttonText.Contains("x 10"))
                    {
                        label = buttonText;
                    }
                    else
                    {
                        label = CleanButtonName(name);
                    }

                    AddElement(button, $"{label}, button");
                    addedObjects.Add(button);
                    MelonLogger.Msg($"[{NavigatorId}] Found dismiss button: {name} -> {label}");
                }
            }
        }

        private IEnumerable<GameObject> FindCustomButtonsInScene()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip)
                {
                    // Only return buttons in the booster chamber area
                    if (IsInBoosterChamber(mb.gameObject))
                        yield return mb.gameObject;
                }
            }
        }

        private bool IsInBoosterChamber(GameObject obj)
        {
            Transform current = obj.transform;
            while (current != null)
            {
                if (current.name.Contains("BoosterChamber") ||
                    current.name.Contains("Menu_FooterButtons"))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private string CleanButtonName(string name)
        {
            name = name.Replace("_", " ").Replace("Button", "").Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            if (name.StartsWith("Main ")) name = name.Substring(5);
            if (string.IsNullOrEmpty(name)) name = "Continue";
            return name;
        }

        /// <summary>
        /// Get GrpId from a card object for deduplication.
        /// </summary>
        private int GetCardGrpId(GameObject cardObj)
        {
            // Try to find Meta_CDC component and get GrpId
            foreach (var mb in cardObj.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.MetaCDC)
                {
                    // Try to get GrpId field/property
                    var grpIdField = mb.GetType().GetField("GrpId",
                        AllInstanceFlags);
                    if (grpIdField != null)
                    {
                        var value = grpIdField.GetValue(mb);
                        if (value is int intVal) return intVal;
                    }

                    var grpIdProp = mb.GetType().GetProperty("GrpId",
                        AllInstanceFlags);
                    if (grpIdProp != null)
                    {
                        var value = grpIdProp.GetValue(mb);
                        if (value is int intVal) return intVal;
                    }
                }
            }
            return 0; // Not found or not a card (e.g., vault progress)
        }

        public override string GetTutorialHint() => LocaleManager.Instance.Get("BoosterOpenHint");

        protected override string GetActivationAnnouncement()
        {
            string countInfo = _totalCards > 0 ? $" {_totalCards} cards." : "";
            string core = $"Pack Contents.{countInfo}".TrimEnd();
            return Strings.WithHint(core, "BoosterOpenHint");
        }

        protected override void HandleInput()
        {
            // Handle custom input first (F1 help, etc.)
            if (HandleCustomInput()) return;

            // I key: Extended card info (keyword descriptions + other faces)
            if (Input.GetKeyDown(KeyCode.I))
            {
                var extInfoNav = AccessibleArenaMod.Instance?.ExtendedInfoNavigator;
                var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                if (extInfoNav != null && cardNav != null && cardNav.IsActive && cardNav.CurrentCard != null)
                {
                    extInfoNav.Open(cardNav.CurrentCard);
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                }
                return;
            }

            // F11: Dump current card details for debugging (helps identify "Unknown card" issues)
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (IsValidIndex && _elements[_currentIndex].GameObject != null)
                {
                    MenuDebugHelper.DumpCardDetails(NavigatorId, _elements[_currentIndex].GameObject, _announcer);
                }
                else
                {
                    _announcer?.Announce(Models.Strings.NoCardToInspect, Models.AnnouncementPriority.High);
                }
                return;
            }

            // Left/Right arrows for navigation between cards (horizontal layout, hold-to-repeat)
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => MovePrevious())) return;
            if (_holdRepeater.Check(KeyCode.RightArrow, () => MoveNext())) return;

            // Home/End for quick jump to first/last
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveFirst();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                MoveLast();
                return;
            }

            // Tab/Shift+Tab also navigates
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Enter activates (view card details or button)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                MelonLogger.Msg($"[{NavigatorId}] Enter pressed - index={_currentIndex}, count={_elements.Count}, valid={IsValidIndex}");
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    MelonLogger.Msg($"[{NavigatorId}] Current element: {elem.GameObject?.name ?? "null"} (Label: {elem.Label})");

                    // Special handling for Skip to End / Reveal All buttons - trigger immediate rescan
                    if (elem.GameObject != null &&
                        (elem.GameObject.name.Contains("SkipToEnd") || elem.GameObject.name.Contains("RevealAll")))
                    {
                        ActivateCurrentElement();
                        // Trigger rescan to pick up all revealed cards
                        _rescanDone = false;
                        _rescanFrameCounter = 0;
                        MelonLogger.Msg($"[{NavigatorId}] Reveal/Skip activated, will rescan");
                        return;
                    }
                    // Check if this is a close/dismiss button
                    // Use ClosePackProperly to ensure correct game state cleanup
                    if (elem.GameObject != null &&
                        (elem.GameObject.name.Contains("ModalFade") ||
                         elem.GameObject.name.Contains("Dismiss_MainButton")))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Enter on close button: {elem.GameObject.name}");
                        ClosePackProperly();
                        TriggerCloseRescan();
                        return;
                    }
                    // Hidden card: activate the parent BoosterCardHolder (has the CustomButton)
                    // to trigger OnClick() -> PlayFlipSound() + RevealCard()
                    if (elem.GameObject != null && elem.Label == Strings.HiddenCard)
                    {
                        var holder = FindBoosterCardHolder(elem.GameObject);
                        if (holder != null)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] Revealing hidden card via BoosterCardHolder");
                            UIActivator.Activate(holder);
                            // Rescan after flip animation to update label with card name
                            _rescanDone = false;
                            _rescanFrameCounter = 0;
                            return;
                        }
                    }
                }
                // Default: just activate whatever is selected (cards, buttons, etc.)
                ActivateCurrentElement();
                return;
            }

            // Backspace to go back/close
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                MelonLogger.Msg($"[{NavigatorId}] Backspace pressed - attempting close");
                ClosePackProperly();
                TriggerCloseRescan();
                return;
            }
        }

        /// <summary>
        /// Close the pack properly using the game's expected flow.
        /// Priority: Dismiss_MainButton (Weiter) > controller DismissCards > ModalFade
        /// ModalFade alone doesn't properly reset the UI state.
        /// </summary>
        private void ClosePackProperly()
        {
            // Stop pack music by sending PointerExit to the pack hitbox
            StopPackMusic();

            // Priority 1: Find and click Dismiss_MainButton (the proper "Continue/Weiter" button)
            // This triggers the game's full close sequence
            foreach (var elem in _elements)
            {
                if (elem.GameObject != null && elem.GameObject.name.Contains("Dismiss_MainButton"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Clicking proper close button: {elem.GameObject.name}");
                    UIActivator.Activate(elem.GameObject);
                    return; // Game will handle the rest
                }
            }

            // Priority 2: If Dismiss_MainButton not available, click RevealAll first to trigger reveal
            // This puts the game in the correct state for closing
            // The _revealAllButton field tracks the RevealAll button
            if (_revealAllButton != null && _revealAllButton.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Clicking RevealAll first to enable proper close: {_revealAllButton.name}");
                UIActivator.Activate(_revealAllButton);
                // After revealing, we need to wait for Dismiss_MainButton to appear
                // Start a coroutine to click it after a short delay
                MelonLoader.MelonCoroutines.Start(ClickDismissAfterDelay());
                return;
            }

            // Priority 3: Fallback - try controller's DismissCards method
            MelonLogger.Msg($"[{NavigatorId}] No Dismiss_MainButton or RevealAll found, using controller fallback");
            if (TryClosePackContents())
            {
                return;
            }

            // Priority 4: Last resort - click ModalFade (background close)
            // This may not fully reset UI state but at least dismisses visually
            foreach (var elem in _elements)
            {
                if (elem.GameObject != null && elem.GameObject.name.Contains("ModalFade"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Fallback to ModalFade: {elem.GameObject.name}");
                    UIActivator.Activate(elem.GameObject);
                    return;
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] No close mechanism found");
        }

        /// <summary>
        /// Coroutine to click the Dismiss_MainButton after RevealAll animation completes.
        /// Retries multiple times since "Open All" with many cards can have long reveal animations.
        /// </summary>
        private System.Collections.IEnumerator ClickDismissAfterDelay()
        {
            const int maxAttempts = 10; // Up to 5 seconds total (10 × 0.5s)
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                yield return new WaitForSeconds(0.5f);

                // Find and click Dismiss_MainButton
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (mb.GetType().Name == T.CustomButton && mb.gameObject.name.Contains("Dismiss_MainButton"))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Delayed click on Dismiss_MainButton (attempt {attempt + 1}): {mb.gameObject.name}");
                        UIActivator.Activate(mb.gameObject);
                        yield break;
                    }
                }
            }

            // If still no Dismiss_MainButton after all retries, use chamber controller fallback
            MelonLogger.Msg($"[{NavigatorId}] Dismiss_MainButton not found after {maxAttempts} attempts, using controller fallback");
            TryCloseChamberController();
        }

        /// <summary>
        /// Stop pack music by sending PointerExit to the currently active pack hitbox.
        /// Same approach used by GeneralMenuNavigator when switching packs in the carousel.
        /// </summary>
        private void StopPackMusic()
        {
            // Find all pack hitboxes in the carousel and send PointerExit to stop their music
            var boosterChamber = GameObject.Find("ContentController - BoosterChamber_v2_Desktop_16x9(Clone)");
            if (boosterChamber == null) return;

            foreach (Transform t in boosterChamber.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == "Hitbox_BoosterMesh" && t.gameObject.activeInHierarchy)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Stopping pack music: PointerExit to {t.name}");
                    UIActivator.SimulatePointerExit(t.gameObject);
                }
            }
        }

        /// <summary>
        /// Try to close the pack contents view.
        /// Prefers the BoosterChamberController's DismissCards (proper full cleanup),
        /// then falls back to the scroll list controller.
        /// </summary>
        private bool TryClosePackContents()
        {
            // Priority 1: Call DismissCards on the BoosterChamberController (parent)
            // This resets ThereIsABoosterOpened, triggers animator, refreshes carousel
            if (TryCloseChamberController())
                return true;

            // Priority 2: Fall back to scroll list controller methods
            var controller = FindBoosterController();
            if (controller != null)
            {
                var controllerType = controller.GetType();
                var allMethods = controllerType.GetMethods(AllInstanceFlags);

                MethodInfo dismissCards = null;
                MethodInfo closeMethod = null;

                foreach (var method in allMethods)
                {
                    string methodName = method.Name;
                    if (methodName == "DismissCards" && method.GetParameters().Length == 0)
                        dismissCards = method;
                    else if ((methodName == "Close" || methodName == "OnCloseClicked" ||
                              methodName == "Hide" || methodName == "Dismiss") &&
                             method.GetParameters().Length == 0)
                        closeMethod = method;
                }

                if (dismissCards != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Found DismissCards on scroll list controller, invoking to close");
                    try
                    {
                        dismissCards.Invoke(controller, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] DismissCards failed: {ex.Message}");
                    }
                }

                if (closeMethod != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Invoking {closeMethod.Name}() to close");
                    try
                    {
                        closeMethod.Invoke(controller, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] {closeMethod.Name} failed: {ex.Message}");
                    }
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] Could not close pack contents via controller");
            return false;
        }

        /// <summary>
        /// Call DismissCards on the BoosterChamberController (the parent NavContentController).
        /// This is the proper close method that resets ThereIsABoosterOpened, triggers the
        /// dismiss animation, and refreshes the booster carousel — unlike the scroll list
        /// controller's DismissCards which only clears the card list.
        /// </summary>
        private bool TryCloseChamberController()
        {
            var boosterChamber = GameObject.Find("ContentController - BoosterChamber_v2_Desktop_16x9(Clone)");
            if (boosterChamber == null) return false;

            // Find the BoosterChamberController component (the NavContentController, not the scroll list)
            Component chamberController = null;
            foreach (var mb in boosterChamber.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "BoosterChamberController")
                {
                    chamberController = mb;
                    break;
                }
            }

            if (chamberController == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] BoosterChamberController not found on chamber GO");
                return false;
            }

            // Find DismissCards (private method on BoosterChamberController)
            var dismissMethod = chamberController.GetType().GetMethod("DismissCards",
                PrivateInstance | System.Reflection.BindingFlags.Public);
            if (dismissMethod == null || dismissMethod.GetParameters().Length != 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] DismissCards not found on BoosterChamberController");
                return false;
            }

            MelonLogger.Msg($"[{NavigatorId}] Found DismissCards on BoosterChamberController, invoking");
            try
            {
                dismissMethod.Invoke(chamberController, null);
                return true;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Msg($"[{NavigatorId}] BoosterChamberController.DismissCards failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the cached controller, or re-find it if needed.
        /// </summary>
        private Component FindBoosterController()
        {
            if (_controller != null)
                return _controller;

            var boosterChamber = GameObject.Find("ContentController - BoosterChamber_v2_Desktop_16x9(Clone)");
            if (boosterChamber != null)
            {
                var ctrl = FindScrollListController(boosterChamber);
                if (ctrl != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Re-found controller: {ctrl.GetType().Name}");
                    _controller = ctrl;
                    return ctrl;
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] FindBoosterController: No controller found");
            return null;
        }

        /// <summary>
        /// Auto-skip the card reveal animation so all cards appear at once (face-down).
        /// AutoReveal is already cleared in DetectScreen before the animation starts.
        /// </summary>
        private void TrySkipAnimation()
        {
            if (_animSkipped || _controller == null) return;

            // Read _animationSequenceActiveField (private bool)
            if (_animActiveField == null)
                _animActiveField = _controller.GetType().GetField("_animationSequenceActiveField", PrivateInstance);

            if (_animActiveField == null) return;

            bool isActive = (bool)_animActiveField.GetValue(_controller);
            if (!isActive) return;

            // Animation is active - skip it to spawn all cards immediately
            if (_stopAnimMethod == null)
            {
                foreach (var m in _controller.GetType().GetMethods(PublicInstance))
                {
                    if (m.Name == "StopBoosterOpenAnimationSequence" && m.GetParameters().Length == 0)
                    {
                        _stopAnimMethod = m;
                        break;
                    }
                }
            }

            if (_stopAnimMethod != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Auto-skipping pack animation for accessibility");
                try
                {
                    _stopAnimMethod.Invoke(_controller, null);
                    _animSkipped = true;
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Failed to skip animation: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Set AutoReveal = false on all cards in _cardsToOpen so they spawn face-down.
        /// AutoReveal is a public bool field on CardDataAndRevealStatus.
        /// </summary>
        private void ClearAutoReveal()
        {
            var cards = GetCardsToOpen(_controller);
            if (cards == null || cards.Count == 0) return;

            int cleared = 0;
            foreach (var card in cards)
            {
                if (card == null) continue;

                if (_autoRevealField == null)
                    _autoRevealField = card.GetType().GetField("AutoReveal", PublicInstance);

                if (_autoRevealField != null)
                {
                    bool wasAutoReveal = (bool)_autoRevealField.GetValue(card);
                    if (wasAutoReveal)
                    {
                        _autoRevealField.SetValue(card, false);
                        cleared++;
                    }
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] Cleared AutoReveal on {cleared}/{cards.Count} cards");
        }

        /// <summary>
        /// Override ForceRescan to preserve cursor position and suppress redundant announcements.
        /// The base implementation resets to index 0 and re-announces the full activation text
        /// on every rescan, which is disruptive during periodic polling.
        /// </summary>
        public override void ForceRescan()
        {
            if (!_isActive) return;

            int oldCount = _elements.Count;
            int oldIndex = _currentIndex;
            GameObject oldObj = IsValidIndex ? _elements[_currentIndex].GameObject : null;
            string oldLabel = IsValidIndex ? _elements[_currentIndex].Label : null;

            _elements.Clear();
            _currentIndex = -1;

            DiscoverElements();

            if (_elements.Count > 0)
            {
                // Restore position by matching the same GameObject (stable across label changes)
                int restored = -1;
                if (oldObj != null)
                {
                    for (int i = 0; i < _elements.Count; i++)
                    {
                        if (_elements[i].GameObject == oldObj)
                        {
                            restored = i;
                            break;
                        }
                    }
                }

                // Fall back to old index (clamped) or 0
                if (restored >= 0)
                    _currentIndex = restored;
                else if (oldIndex >= 0 && oldIndex < _elements.Count)
                    _currentIndex = oldIndex;
                else
                    _currentIndex = 0;

                // When cards are first discovered (count jumps from just buttons to cards+buttons),
                // reset cursor to first card instead of restoring to the old Close button position
                if (_elements.Count != oldCount && oldCount <= 2)
                {
                    _currentIndex = 0;
                }

                // Cards first discovered: announce activation text
                if (_elements.Count != oldCount && oldCount <= 2)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Rescan: {oldCount} -> {_elements.Count} elements");
                    _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                }
                // Card revealed (label changed): announce card name
                else if (restored >= 0 && oldObj != null)
                {
                    string newLabel = _elements[restored].Label;
                    if (newLabel != oldLabel)
                        _announcer.AnnounceInterrupt(newLabel);
                }

                // Update card navigation so Up/Down works immediately after reveal
                UpdateCardNavigation();
            }
            else
            {
                MelonLogger.Msg($"[{NavigatorId}] Rescan found no elements");
            }
        }

        #region Periodic rescan until cards are found

        public override void Update()
        {
            // Periodic rescan every ~0.5s until cards are found
            // Cards are spawned by an animation event ~2.5s after pack opening
            if (_isActive && !_rescanDone)
            {
                _rescanFrameCounter++;
                if (_rescanFrameCounter >= RescanIntervalFrames)
                {
                    _rescanFrameCounter = 0;
                    _rescanAttempt++;
                    int oldCount = _totalCards;

                    // Try to skip animation first - makes all cards available at once
                    TrySkipAnimation();

                    MelonLogger.Msg($"[{NavigatorId}] Rescanning for cards (attempt {_rescanAttempt}/{MaxRescanAttempts}, current: {oldCount})");
                    ForceRescan();

                    if (_totalCards > 0 || _rescanAttempt >= MaxRescanAttempts)
                    {
                        _rescanDone = true;
                        if (_totalCards > 0)
                            MelonLogger.Msg($"[{NavigatorId}] Found {_totalCards}/{_expectedCardCount} cards after {_rescanAttempt} rescans");
                        else
                            MelonLogger.Msg($"[{NavigatorId}] Timed out after {_rescanAttempt} rescans");
                    }
                }
            }

            // Rescan after close action (~1 second) to detect screen change
            if (_isActive && _closeTriggered)
            {
                _closeRescanCounter++;
                if (_closeRescanCounter >= 60) // ~1 second at 60fps
                {
                    _closeTriggered = false;
                    MelonLogger.Msg($"[{NavigatorId}] Checking if screen is still valid after close action");

                    // Re-check if we're still on pack contents (CardScroller must exist)
                    if (!DetectScreen())
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Pack contents closed, deactivating navigator");
                        Deactivate();
                    }
                    else
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Still on pack contents, rescanning");
                        ForceRescan();
                    }
                }
            }

            base.Update();
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _rescanFrameCounter = 0;
            _rescanAttempt = 0;
            _rescanDone = false;
            _animSkipped = false;
            _closeTriggered = false;
            _closeRescanCounter = 0;
        }

        /// <summary>
        /// Trigger a rescan after close button is clicked.
        /// </summary>
        private void TriggerCloseRescan()
        {
            _closeTriggered = true;
            _closeRescanCounter = 0;
        }

        #endregion

        protected override bool ValidateElements()
        {
            // Check if controller is still valid and has cards
            if (_controller == null || !(_controller is MonoBehaviour mb) || !mb.gameObject.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Controller no longer active");
                return false;
            }

            // Also check that _cardsToOpen is still populated (cleared on dismiss)
            var cards = GetCardsToOpen(_controller);
            if (cards == null || cards.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] Cards cleared, pack dismissed");
                return false;
            }

            return base.ValidateElements();
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive)
            {
                Deactivate();
            }
            _controller = null;
            _revealAllButton = null;
        }
    }
}
