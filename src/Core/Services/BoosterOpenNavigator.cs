using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the booster pack card list that appears after opening a pack.
    /// Detects BoosterOpenToScrollListController and makes cards navigable.
    /// </summary>
    public class BoosterOpenNavigator : BaseNavigator
    {
        private GameObject _scrollListController;
        private GameObject _revealAllButton;
        private int _totalCards;

        // Single rescan after reveal animation completes (~1.5 seconds)
        private int _rescanFrameCounter;
        private const int RescanDelayFrames = 90; // ~1.5 seconds at 60fps
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
                _scrollListController = null;
                return false;
            }

            // Key check: CardScroller is the definitive indicator of pack contents view
            // RevealAll button exists on BOTH pack selection and pack contents, so it's not reliable alone
            bool hasCardScroller = false;

            // Check for CardScroller (only exists when cards are displayed)
            // IMPORTANT: Use false to only search active objects
            foreach (Transform child in boosterChamber.GetComponentsInChildren<Transform>(false))
            {
                if (child.name == "CardScroller" && child.gameObject.activeInHierarchy)
                {
                    hasCardScroller = true;
                    MelonLogger.Msg($"[{NavigatorId}] Found CardScroller");
                    break;
                }
            }

            // CardScroller is REQUIRED for pack contents detection
            // Without it, we're on pack selection screen (not pack contents)
            if (!hasCardScroller)
            {
                _scrollListController = null;
                return false;
            }

            // Find the scroll list controller
            var safeArea = boosterChamber.transform.Find("SafeArea");
            if (safeArea != null)
            {
                foreach (Transform child in safeArea)
                {
                    if (child.name.Contains("BoosterOpenToScrollListController"))
                    {
                        if (child.gameObject.activeInHierarchy)
                        {
                            _scrollListController = child.gameObject;
                            MelonLogger.Msg($"[{NavigatorId}] Found pack contents (RevealAll visible): {child.name}");
                            return true;
                        }
                    }
                }
            }

            // Fallback: use booster chamber itself as controller reference
            _scrollListController = boosterChamber;
            MelonLogger.Msg($"[{NavigatorId}] Found pack contents (RevealAll visible, using booster chamber)");
            return true;
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

            // Primary search: Look in CardScroller content area
            FindCardsInCardScroller(cardEntries, addedObjects);

            // Fallback: Search EntryRoot containers
            if (cardEntries.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] No cards in CardScroller, searching EntryRoot containers");
                FindCardsInEntryRoots(cardEntries, addedObjects);
            }

            // Last resort: Search entire booster chamber
            if (cardEntries.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] No EntryRoot cards found, searching entire booster chamber");
                FindCardsDirectly(cardEntries, addedObjects);
            }

            // Sort cards by position (left to right for horizontal scroll)
            cardEntries = cardEntries.OrderBy(x => x.sortOrder).ToList();

            MelonLogger.Msg($"[{NavigatorId}] Found {cardEntries.Count} entries (cards + vault progress)");

            // Add cards to navigation
            int cardNum = 1;
            foreach (var (cardObj, _) in cardEntries)
            {
                string cardName = ExtractCardName(cardObj);
                var cardInfo = CardDetector.ExtractCardInfo(cardObj);

                // Prefer our extracted name, fall back to CardDetector
                string displayName = !string.IsNullOrEmpty(cardName) ? cardName :
                                     (cardInfo.IsValid ? cardInfo.Name : "Unknown card");

                // Log unknown cards for debugging (use F11 on this card for full details)
                if (displayName == "Unknown card")
                {
                    MelonLogger.Msg($"[{NavigatorId}] Card {cardNum} extraction failed: {cardObj.name} - press F11 while focused for details");
                }

                // Check if this is vault progress (not a real card)
                bool isVaultProgress = displayName.StartsWith("Vault Progress");

                string label;
                if (isVaultProgress)
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
        /// Search for cards in the CardScroller content area.
        /// Path: CardScroller/Viewport/Centerer/Content/Prefab - BoosterMetaCardView_v2
        /// </summary>
        private void FindCardsInCardScroller(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects)
        {
            var boosterChamber = GameObject.Find("ContentController - BoosterChamber_v2_Desktop_16x9(Clone)");
            if (boosterChamber == null) return;

            // Find the CardScroller
            Transform cardScroller = null;
            foreach (Transform t in boosterChamber.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CardScroller" && t.gameObject.activeInHierarchy)
                {
                    cardScroller = t;
                    break;
                }
            }

            if (cardScroller == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] CardScroller not found");
                return;
            }

            // Navigate to Content container: CardScroller/Viewport/Centerer/Content
            Transform content = null;
            var viewport = cardScroller.Find("Viewport");
            if (viewport != null)
            {
                var centerer = viewport.Find("Centerer");
                if (centerer != null)
                {
                    content = centerer.Find("Content");
                }
            }

            if (content == null)
            {
                // Fallback: search all children for "Content"
                foreach (Transform t in cardScroller.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Content")
                    {
                        content = t;
                        break;
                    }
                }
            }

            if (content == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Content container not found in CardScroller");
                return;
            }

            MelonLogger.Msg($"[{NavigatorId}] Searching CardScroller content: {content.name} ({content.childCount} children)");

            // Debug: Log the hierarchy to understand the structure
            foreach (Transform child in content)
            {
                if (child == null) continue;
                MelonLogger.Msg($"[{NavigatorId}] Content child: {child.name} (active={child.gameObject.activeInHierarchy})");
            }

            // Search for BoosterMetaCardView prefabs - they're nested, not direct children
            // Use a HashSet of GrpIds to deduplicate cards that appear twice (3D + flat view)
            var seenGrpIds = new HashSet<int>();

            foreach (Transform t in content.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                string tName = t.name;
                if (tName.Contains("BoosterMetaCardView"))
                {
                    if (addedObjects.Contains(t.gameObject)) continue;

                    // Try to get GrpId to deduplicate
                    int grpId = GetCardGrpId(t.gameObject);
                    if (grpId > 0)
                    {
                        if (seenGrpIds.Contains(grpId))
                        {
                            MelonLogger.Msg($"[{NavigatorId}] Skipping duplicate card (GrpId={grpId}): {tName}");
                            continue;
                        }
                        seenGrpIds.Add(grpId);
                    }

                    float sortOrder = t.position.x;
                    cardEntries.Add((t.gameObject, sortOrder));
                    addedObjects.Add(t.gameObject);

                    string parentPath = GetParentPath(t, 3);
                    MelonLogger.Msg($"[{NavigatorId}] Found card: {tName} (GrpId={grpId}, parent={parentPath})");
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] Found {cardEntries.Count} cards in CardScroller");
        }

        /// <summary>
        /// Search for cards in EntryRoot containers.
        /// </summary>
        private void FindCardsInEntryRoots(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects)
        {
            var entryRoots = new List<Transform>();

            // Search for EntryRoot in the scroll view structure
            var allTransforms = GameObject.FindObjectsOfType<Transform>();
            foreach (var t in allTransforms)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                if (t.name == "EntryRoot" || t.name.Contains("EntryRoot"))
                {
                    entryRoots.Add(t);
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] Found {entryRoots.Count} EntryRoot containers");

            // Process each EntryRoot to find card entries
            foreach (var entryRoot in entryRoots)
            {
                foreach (Transform child in entryRoot)
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    // Check if this child is a card entry
                    var cardObj = FindCardInEntry(child.gameObject);
                    if (cardObj != null && !addedObjects.Contains(cardObj))
                    {
                        float sortOrder = child.position.x;
                        cardEntries.Add((cardObj, sortOrder));
                        addedObjects.Add(cardObj);
                    }
                }
            }
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
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                        if (!string.IsNullOrEmpty(content))
                            title = content;
                    }
                }

                // Check for vault/duplicate progress indicator (e.g., "+99")
                // This appears when you get a 5th+ copy of a common/uncommon
                if (objName.Contains("Progress") && objName.Contains("Quantity"))
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                        progressQuantity = content;
                }

                // Collect tags from TAG parent elements (these describe the vault progress type)
                // Structure: Text_1 (parent=TAG_1): 'Alchemy', Text_2 (parent=TAG_2): 'Bonus', etc.
                if (parentName.StartsWith("TAG"))
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
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

        private GameObject FindCardInEntry(GameObject entry)
        {
            // Check if the entry itself is a card
            if (CardDetector.IsCard(entry))
                return entry;

            // Search children for card elements (include inactive)
            foreach (Transform child in entry.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (CardDetector.IsCard(child.gameObject))
                    return child.gameObject;
            }

            // Look for BoosterMetaCardView component (include inactive)
            foreach (var mb in entry.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "BoosterMetaCardView" ||
                    typeName == "MetaCardView" ||
                    typeName == "Meta_CDC")
                {
                    return mb.gameObject;
                }
            }

            return null;
        }

        private void FindCardsDirectly(List<(GameObject obj, float sortOrder)> cardEntries, HashSet<GameObject> addedObjects)
        {
            // Fallback: Search for BoosterMetaCardView in entire booster chamber
            var boosterChamber = GameObject.Find("ContentController - BoosterChamber_v2_Desktop_16x9(Clone)");
            if (boosterChamber == null) return;

            foreach (var t in boosterChamber.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                // Only match BoosterMetaCardView prefabs
                if (t.name.Contains("BoosterMetaCardView"))
                {
                    // Skip if parent is also a BoosterMetaCardView (avoid nested duplicates)
                    if (t.parent != null && t.parent.name.Contains("BoosterMetaCardView"))
                        continue;

                    if (!addedObjects.Contains(t.gameObject))
                    {
                        float sortOrder = t.position.x;
                        cardEntries.Add((t.gameObject, sortOrder));
                        addedObjects.Add(t.gameObject);
                        MelonLogger.Msg($"[{NavigatorId}] Found card (fallback): {t.name}");
                    }
                }
            }
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
                if (typeName == "CustomButton" || typeName == "CustomButtonWithTooltip")
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
                if (mb.GetType().Name == "Meta_CDC")
                {
                    // Try to get GrpId field/property
                    var grpIdField = mb.GetType().GetField("GrpId",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (grpIdField != null)
                    {
                        var value = grpIdField.GetValue(mb);
                        if (value is int intVal) return intVal;
                    }

                    var grpIdProp = mb.GetType().GetProperty("GrpId",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (grpIdProp != null)
                    {
                        var value = grpIdProp.GetValue(mb);
                        if (value is int intVal) return intVal;
                    }
                }
            }
            return 0; // Not found or not a card (e.g., vault progress)
        }

        /// <summary>
        /// Get parent path for debugging hierarchy.
        /// </summary>
        private string GetParentPath(Transform t, int levels)
        {
            var parts = new List<string>();
            Transform current = t.parent;
            for (int i = 0; i < levels && current != null; i++)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        protected override string GetActivationAnnouncement()
        {
            string countInfo = _totalCards > 0 ? $" {_totalCards} cards." : "";
            return $"Pack Contents. Left and Right to navigate cards, Up and Down for card details.{countInfo}";
        }

        protected override void HandleInput()
        {
            // Handle custom input first (F1 help, etc.)
            if (HandleCustomInput()) return;

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

            // Left/Right arrows for navigation between cards (horizontal layout)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                MovePrevious();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                MoveNext();
                return;
            }

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
                        _rescanFrameCounter = RescanDelayFrames - 15; // Rescan soon
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
        /// </summary>
        private System.Collections.IEnumerator ClickDismissAfterDelay()
        {
            // Wait for reveal animation to complete and Dismiss_MainButton to appear
            yield return new WaitForSeconds(0.5f);

            // Find and click Dismiss_MainButton
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton" && mb.gameObject.name.Contains("Dismiss_MainButton"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Delayed click on Dismiss_MainButton: {mb.gameObject.name}");
                    UIActivator.Activate(mb.gameObject);
                    yield break;
                }
            }

            // If still no Dismiss_MainButton, use controller fallback
            MelonLogger.Msg($"[{NavigatorId}] Dismiss_MainButton not found after delay, using controller fallback");
            TryClosePackContents();
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
        /// Try to invoke the Skip to End functionality via controller reflection.
        /// Uses method iteration instead of GetMethod for better IL2CPP compatibility.
        /// </summary>
        private bool TryInvokeSkipToEnd()
        {
            var controller = FindBoosterController();
            if (controller == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Controller not found for SkipToEnd");
                return false;
            }

            MelonLogger.Msg($"[{NavigatorId}] Found controller: {controller.GetType().Name}");
            var controllerType = controller.GetType();

            // Iterate through all methods to find skip-related ones (IL2CPP compatible)
            var allMethods = controllerType.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            System.Reflection.MethodInfo setSkipActive = null;
            System.Reflection.MethodInfo onSkipToggle = null;
            System.Reflection.MethodInfo revealComplete = null;

            foreach (var method in allMethods)
            {
                string methodName = method.Name;
                if (methodName == "SetSkipActive")
                    setSkipActive = method;
                else if (methodName == "OnSkipAnimationToggle")
                    onSkipToggle = method;
                else if (methodName == "RevealSequenceComplete")
                    revealComplete = method;
            }

            // Try SetSkipActive(true) - enables skip mode
            if (setSkipActive != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Found SetSkipActive, invoking with true");
                try
                {
                    setSkipActive.Invoke(controller, new object[] { true });
                    return true;
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Msg($"[{NavigatorId}] SetSkipActive failed: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Msg($"[{NavigatorId}] SetSkipActive not found in {allMethods.Length} methods");
            }

            // Try OnSkipAnimationToggle(true) - toggles skip animation
            if (onSkipToggle != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Found OnSkipAnimationToggle, invoking with true");
                try
                {
                    onSkipToggle.Invoke(controller, new object[] { true });
                    return true;
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Msg($"[{NavigatorId}] OnSkipAnimationToggle failed: {ex.Message}");
                }
            }

            // Try RevealSequenceComplete() - completes the reveal
            if (revealComplete != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Found RevealSequenceComplete, invoking");
                try
                {
                    revealComplete.Invoke(controller, null);
                    return true;
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Msg($"[{NavigatorId}] RevealSequenceComplete failed: {ex.Message}");
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] No skip method found on controller ({allMethods.Length} methods checked)");
            return false;
        }

        /// <summary>
        /// Try to close the pack contents view.
        /// Uses method iteration instead of GetMethod for better IL2CPP compatibility.
        /// </summary>
        private bool TryClosePackContents()
        {
            var controller = FindBoosterController();
            if (controller != null)
            {
                var controllerType = controller.GetType();

                // Iterate through all methods to find close-related ones (IL2CPP compatible)
                var allMethods = controllerType.GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                System.Reflection.MethodInfo dismissCards = null;
                System.Reflection.MethodInfo closeMethod = null;

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

                // Try DismissCards() - this is the actual method on BoosterOpenToScrollListController
                if (dismissCards != null)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Found DismissCards, invoking to close");
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
                else
                {
                    MelonLogger.Msg($"[{NavigatorId}] DismissCards not found in {allMethods.Length} methods");
                }

                // Fallback: try other close method
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
        /// Find the booster scroll list controller component.
        /// </summary>
        private Component FindBoosterController()
        {
            // Search for the scroll list controller in the scene
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "BoosterOpenToScrollListController" ||
                    typeName == "BoosterChamberController" ||
                    typeName.Contains("BoosterOpen") && typeName.Contains("Controller"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] FindBoosterController: Found {typeName}");
                    return mb;
                }
            }

            // Fallback: check the scroll list controller object
            if (_scrollListController != null)
            {
                foreach (var mb in _scrollListController.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name.Contains("Controller"))
                    {
                        MelonLogger.Msg($"[{NavigatorId}] FindBoosterController (fallback): Found {mb.GetType().Name}");
                        return mb;
                    }
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] FindBoosterController: No controller found");
            return null;
        }

        #region Single delayed rescan after reveal animation

        public override void Update()
        {
            // Single rescan after ~1.5 seconds to catch all revealed cards
            if (_isActive && !_rescanDone)
            {
                _rescanFrameCounter++;
                if (_rescanFrameCounter >= RescanDelayFrames)
                {
                    _rescanDone = true;
                    int oldCount = _totalCards;

                    MelonLogger.Msg($"[{NavigatorId}] Rescanning after reveal animation (current count: {oldCount})");
                    ForceRescan();

                    int newCards = _totalCards - oldCount;
                    if (newCards > 0)
                    {
                        MelonLogger.Msg($"[{NavigatorId}] Found {newCards} additional cards, {_totalCards} total");
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
            _rescanDone = false;
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
            // Check if scroll list controller is still active
            if (_scrollListController == null || !_scrollListController.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Scroll list controller no longer active");
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
            _scrollListController = null;
            _revealAllButton = null;
        }
    }
}
