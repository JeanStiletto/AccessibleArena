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

        public override string NavigatorId => "BoosterOpen";
        public override string ScreenName => GetScreenName();
        public override int Priority => 80; // Higher than GeneralMenuNavigator (15), below OverlayNavigator (85)

        public BoosterOpenNavigator(IAnnouncementService announcer) : base(announcer) { }

        private string GetScreenName()
        {
            if (_totalCards > 0)
                return $"Pack Contents, {_totalCards} cards";
            return "Pack Contents";
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

            // Key check: RevealAll button only appears when viewing pack contents (not pack selection)
            // This is the most reliable indicator that we're in pack contents view
            bool hasRevealAllButton = false;
            bool hasCardScroller = false;

            // Check for CardScroller (only exists when cards are displayed)
            foreach (Transform child in boosterChamber.GetComponentsInChildren<Transform>(false))
            {
                if (child.name == "CardScroller" && child.gameObject.activeInHierarchy)
                {
                    hasCardScroller = true;
                    MelonLogger.Msg($"[{NavigatorId}] Found CardScroller");
                    break;
                }
            }

            // Check for RevealAll button
            foreach (var mb in boosterChamber.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton" && mb.gameObject.name.Contains("RevealAll"))
                {
                    hasRevealAllButton = true;
                    MelonLogger.Msg($"[{NavigatorId}] Found RevealAll button: {mb.gameObject.name}");
                    break;
                }
            }

            // Either RevealAll button OR CardScroller indicates pack contents
            if (!hasRevealAllButton && !hasCardScroller)
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
            var customButtons = FindCustomButtonsInScene();

            foreach (var button in customButtons)
            {
                string name = button.name;
                if (name.Contains("RevealAll") || name.Contains("Reveal_All"))
                {
                    if (addedObjects.Contains(button)) continue;

                    string label = UITextExtractor.GetButtonText(button, "Reveal All");
                    AddElement(button, $"{label}, button");
                    addedObjects.Add(button);
                    _revealAllButton = button;
                    MelonLogger.Msg($"[{NavigatorId}] Found RevealAll button: {name}");
                    return;
                }
            }
        }

        private void FindCardEntries(HashSet<GameObject> addedObjects)
        {
            // Find EntryRoot containers that hold card entries
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
            var cardEntries = new List<(GameObject obj, float sortOrder)>();

            foreach (var entryRoot in entryRoots)
            {
                foreach (Transform child in entryRoot)
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    // Check if this child is a card entry
                    var cardObj = FindCardInEntry(child.gameObject);
                    if (cardObj != null && !addedObjects.Contains(cardObj))
                    {
                        float sortOrder = -child.position.y * 1000 + child.position.x;
                        cardEntries.Add((cardObj, sortOrder));
                        addedObjects.Add(cardObj);
                    }
                }
            }

            // If no EntryRoot found, try to find cards directly
            if (cardEntries.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] No EntryRoot cards found, searching directly for card patterns");
                FindCardsDirectly(cardEntries, addedObjects);
            }

            // Sort cards by position (top to bottom, left to right)
            cardEntries = cardEntries.OrderBy(x => x.sortOrder).ToList();
            _totalCards = cardEntries.Count;

            MelonLogger.Msg($"[{NavigatorId}] Found {_totalCards} cards");

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

                string label = $"Card {cardNum}: {displayName}";
                if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                AddElement(cardObj, label);
                cardNum++;
            }
        }

        private string ExtractCardName(GameObject cardObj)
        {
            // Try to find the Title text element directly
            var texts = cardObj.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;

                string objName = text.gameObject.name;

                // "Title" is the card name element
                if (objName == "Title")
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        // Clean up any markup
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                        if (!string.IsNullOrEmpty(content))
                            return content;
                    }
                }
            }

            return null;
        }

        private GameObject FindCardInEntry(GameObject entry)
        {
            // Check if the entry itself is a card
            if (CardDetector.IsCard(entry))
                return entry;

            // Search children for card elements
            foreach (Transform child in entry.GetComponentsInChildren<Transform>(false))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (CardDetector.IsCard(child.gameObject))
                    return child.gameObject;
            }

            // Look for BoosterMetaCardView component
            foreach (var mb in entry.GetComponentsInChildren<MonoBehaviour>(false))
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
            // Search for card patterns in the entire booster chamber
            var boosterChamber = GameObject.Find("ContentController - BoosterChamber_v2_Desktop_16x9(Clone)");
            if (boosterChamber == null) return;

            foreach (var mb in boosterChamber.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName == "BoosterMetaCardView" ||
                    typeName == "MetaCardView" ||
                    typeName == "Meta_CDC")
                {
                    var cardObj = mb.gameObject;
                    if (!addedObjects.Contains(cardObj))
                    {
                        float sortOrder = -cardObj.transform.position.y * 1000 + cardObj.transform.position.x;
                        cardEntries.Add((cardObj, sortOrder));
                        addedObjects.Add(cardObj);
                    }
                }
            }

            // Also check by name patterns
            foreach (var t in boosterChamber.GetComponentsInChildren<Transform>(false))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                string name = t.name;
                if (name.Contains("MetaCardView") ||
                    name.Contains("CardAnchor") ||
                    name.Contains("Prefab - BoosterMetaCardView"))
                {
                    if (!addedObjects.Contains(t.gameObject))
                    {
                        float sortOrder = -t.position.y * 1000 + t.position.x;
                        cardEntries.Add((t.gameObject, sortOrder));
                        addedObjects.Add(t.gameObject);
                    }
                }
            }
        }

        private void FindDismissButton(HashSet<GameObject> addedObjects)
        {
            // Look for continue/dismiss/close buttons
            var customButtons = FindCustomButtonsInScene();

            string[] dismissPatterns = { "Continue", "Close", "Done", "ModalFade", "MainButton" };

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

        protected override string GetActivationAnnouncement()
        {
            string countInfo = _totalCards > 0 ? $"{_totalCards} cards. " : "";
            return $"{ScreenName}. {countInfo}Left and Right to navigate cards, Up and Down for card details.";
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
                    _announcer?.Announce("No card selected to inspect.", Models.AnnouncementPriority.High);
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

            // Enter activates (view card details)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                MelonLogger.Msg($"[{NavigatorId}] Enter pressed - index={_currentIndex}, count={_elements.Count}, valid={IsValidIndex}");
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    MelonLogger.Msg($"[{NavigatorId}] Current element: {elem.GameObject?.name ?? "null"} (Label: {elem.Label})");
                }
                ActivateCurrentElement();
                return;
            }

            // Backspace to go back/close
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                // Try to activate any dismiss button (Close, Continue, Done)
                string[] dismissPatterns = { "Close", "Continue", "Done" };
                foreach (var element in _elements)
                {
                    foreach (var pattern in dismissPatterns)
                    {
                        if (element.Label.Contains(pattern))
                        {
                            UIActivator.Activate(element.GameObject);
                            return;
                        }
                    }
                }
                // Consume the key even if no dismiss button found
                return;
            }
        }

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
