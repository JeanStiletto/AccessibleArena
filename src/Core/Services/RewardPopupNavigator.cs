using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.ElementGrouping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for rewards popup (from mail claims, store purchases, etc.).
    /// Extracted from GeneralMenuNavigator - uses exact same detection and discovery logic.
    /// </summary>
    public class RewardPopupNavigator : BaseNavigator
    {
        public override string NavigatorId => "RewardPopup";
        public override string ScreenName => Strings.ScreenRewards;
        public override int Priority => 86; // Higher than Overlay (85), below SettingsMenu (90)

        private GameObject _activePopup;
        private int _rewardCount;

        // Pre-extracted pack set names from ContentControllerRewards._packReward data
        // Uses List+index instead of Queue because rescans re-discover elements but
        // the game's ToAdd queue gets consumed after first display
        private List<string> _packSetNames = new List<string>();
        private int _packSetNameIndex;

        // Cache to avoid logging spam
        private bool _lastRewardsPopupState = false;

        // Rescan mechanism for timing issues - rewards may not be loaded immediately
        private int _rescanFrameCounter;
        private const int RescanDelayFrames = 30; // ~0.5 seconds at 60fps
        private const int MaxRescanAttempts = 10;
        private int _rescanAttempts;

        public RewardPopupNavigator(IAnnouncementService announcer) : base(announcer) { }

        #region Detection - Copied from OverlayDetector

        protected override bool DetectScreen()
        {
            bool result = CheckRewardsPopupOpenInternal();

            // Only log when state changes to reduce spam
            if (result != _lastRewardsPopupState)
            {
                _lastRewardsPopupState = result;
                MelonLogger.Msg($"[{NavigatorId}] IsRewardsPopupOpen changed to: {result}");
            }

            return result;
        }

        /// <summary>
        /// Check if the rewards popup is currently open.
        /// Copied exactly from OverlayDetector.CheckRewardsPopupOpenInternal().
        /// </summary>
        private bool CheckRewardsPopupOpenInternal()
        {
            // Look for the rewards controller in Screenspace Popups canvas
            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
                return false;

            // Find the rewards controller - it should have ContentController and Rewards in its name
            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    _activePopup = child.gameObject;

                    // Check for Container child
                    var container = child.Find("Container");
                    if (container == null)
                    {
                        // Try searching deeper in case Container is nested
                        foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "Container")
                            {
                                container = t;
                                break;
                            }
                        }
                    }

                    if (container == null)
                    {
                        // Fallback: if controller is active and has any active children with reward prefabs, consider it open
                        foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.gameObject.activeInHierarchy && t.name.Contains("RewardPrefab"))
                                return true;
                        }
                        continue;
                    }

                    // Check for RewardsCONTAINER or Buttons
                    var rewardsContainer = container.Find("RewardsCONTAINER");
                    var buttons = container.Find("Buttons");

                    bool rewardsActive = rewardsContainer != null && rewardsContainer.gameObject.activeInHierarchy;
                    bool buttonsActive = buttons != null && buttons.gameObject.activeInHierarchy;

                    if (rewardsActive || buttonsActive)
                        return true;

                    // Additional fallback: check for any RewardPrefab children directly
                    foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.gameObject.activeInHierarchy && t.name.Contains("RewardPrefab"))
                            return true;
                    }
                }
            }

            _activePopup = null;
            return false;
        }

        /// <summary>
        /// Get the rewards container transform for element discovery.
        /// Copied exactly from OverlayDetector.GetRewardsContainer().
        /// </summary>
        private Transform GetRewardsContainer()
        {
            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
                return null;

            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    var container = child.Find("Container");
                    if (container != null)
                    {
                        var rewardsContainer = container.Find("RewardsCONTAINER");
                        if (rewardsContainer != null && rewardsContainer.gameObject.activeInHierarchy)
                            return rewardsContainer;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Discovery - Copied from GeneralMenuNavigator

        protected override void DiscoverElements()
        {
            _rewardCount = 0;
            var addedObjects = new HashSet<GameObject>();

            // Pre-extract pack set names from controller data before discovering UI elements
            ExtractPackSetNames();

            // Discover reward elements
            DiscoverRewardElements(addedObjects);

            // Also discover buttons (ClaimButton, etc.)
            DiscoverButtons(addedObjects);

            MelonLogger.Msg($"[{NavigatorId}] Discovered {_elements.Count} elements ({_rewardCount} rewards)");
        }

        /// <summary>
        /// Discover reward elements in the rewards popup.
        /// Searches the entire popup for reward prefabs, not just RewardsCONTAINER.
        /// </summary>
        private void DiscoverRewardElements(HashSet<GameObject> addedObjects)
        {
            if (_activePopup == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] DiscoverRewardElements: No active popup");
                return;
            }

            MelonLogger.Msg($"[{NavigatorId}] DiscoverRewardElements: Searching entire popup '{_activePopup.name}' for reward prefabs");

            var rewardPrefabs = new List<(Transform prefab, string type, float sortOrder)>();

            // Find all reward prefabs in the entire popup (not just RewardsCONTAINER)
            foreach (Transform child in _activePopup.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                string name = child.name;
                if (!name.StartsWith("RewardPrefab_")) continue;

                // Determine reward type from prefab name pattern: RewardPrefab_<Type>(Clone)
                string rewardType = null;
                if (name.Contains("Pack"))
                    rewardType = "Pack";
                else if (name.Contains("IndividualCard"))
                    rewardType = "Card";
                else if (name.Contains("CardSleeve"))
                    rewardType = "CardSleeve";
                else if (name.Contains("Gold") || name.Contains("Gems") || name.Contains("Coins"))
                    rewardType = "Currency";
                else if (name.Contains("XP"))
                    rewardType = "XP";
                else if (name.Contains("Avatar"))
                    rewardType = "Avatar";
                else
                    rewardType = "Reward";

                // Sort by horizontal position (left to right)
                float sortOrder = child.position.x;
                rewardPrefabs.Add((child, rewardType, sortOrder));
            }

            // Sort rewards by position
            rewardPrefabs = rewardPrefabs.OrderBy(r => r.sortOrder).ToList();

            foreach (var (prefab, rewardType, sortOrder) in rewardPrefabs)
            {
                _rewardCount++;

                // Debug: dump reward prefab structure
                DumpRewardPrefabStructure(prefab.gameObject, rewardType, _rewardCount);

                string label = ExtractRewardLabel(prefab.gameObject, rewardType, _rewardCount);

                // Find a clickable element for this reward
                GameObject clickTarget = FindRewardClickTarget(prefab.gameObject, rewardType);
                if (clickTarget == null)
                    clickTarget = prefab.gameObject;

                if (addedObjects.Contains(clickTarget)) continue;

                // Debug: log card detection info for card rewards
                bool isCard = CardDetector.IsCard(clickTarget);
                MelonLogger.Msg($"[{NavigatorId}] Adding reward: {label} (target: {clickTarget.name}, IsCard: {isCard})");

                AddElement(clickTarget, label);
                addedObjects.Add(clickTarget);

                // Add ALL elements inside this reward prefab to addedObjects to prevent duplicates
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null && child.gameObject.activeInHierarchy)
                        addedObjects.Add(child.gameObject);
                }
            }

            MelonLogger.Msg($"[{NavigatorId}] DiscoverRewardElements: Added {_rewardCount} rewards");
        }

        /// <summary>
        /// Pre-extract pack set names from ContentControllerRewards._packReward.ToAdd data.
        /// Pack rewards display in CollationId order, so we queue names in the same order.
        /// </summary>
        private void ExtractPackSetNames()
        {
            // Reset consumption index for each discovery pass
            _packSetNameIndex = 0;

            // Only extract once - the game's ToAdd queue gets consumed after display,
            // so re-extraction on rescan would find an empty queue
            if (_packSetNames.Count > 0) return;

            if (_activePopup == null) return;

            try
            {
                // Find ContentControllerRewards component on the popup
                MonoBehaviour rewardsController = null;
                foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == "ContentControllerRewards")
                    {
                        rewardsController = mb;
                        break;
                    }
                }
                if (rewardsController == null) return;

                // Get _packReward field
                var packRewardField = rewardsController.GetType().GetField("_packReward", PrivateInstance);
                if (packRewardField == null) return;
                var packReward = packRewardField.GetValue(rewardsController);
                if (packReward == null) return;

                // Get ToAdd field (public Queue<InventoryBooster> on ItemReward<T, P>)
                var toAddField = packReward.GetType().GetField("ToAdd", PublicInstance);
                if (toAddField == null) return;
                var toAdd = toAddField.GetValue(packReward) as IEnumerable;
                if (toAdd == null) return;

                // Extract CollationId from each InventoryBooster, sorted by CollationId (matching display order)
                var collationIds = new List<int>();
                FieldInfo collationIdField = null;

                foreach (var booster in toAdd)
                {
                    if (booster == null) continue;
                    if (collationIdField == null)
                        collationIdField = booster.GetType().GetField("CollationId", PublicInstance);
                    if (collationIdField == null) break;

                    int collationId = (int)collationIdField.GetValue(booster);
                    collationIds.Add(collationId);
                }

                // Sort by CollationId (same order as PackReward.DisplayRewards)
                collationIds.Sort();

                // Convert CollationId to set code via CollationMapping enum ToString()
                var collationMappingType = FindType("Wotc.Mtga.Wrapper.CollationMapping");

                foreach (int collationId in collationIds)
                {
                    string setCode = null;
                    if (collationMappingType != null && Enum.IsDefined(collationMappingType, collationId))
                        setCode = Enum.ToObject(collationMappingType, collationId).ToString();

                    string setName = !string.IsNullOrEmpty(setCode)
                        ? UITextExtractor.MapSetCodeToName(setCode)
                        : null;

                    _packSetNames.Add(setName ?? $"Pack #{collationId}");
                    MelonLogger.Msg($"[{NavigatorId}] Pack set name: CollationId={collationId}, SetCode={setCode}, Name={setName}");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Msg($"[{NavigatorId}] ExtractPackSetNames failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Discover buttons in the rewards popup (ClaimButton, etc.).
        /// </summary>
        private void DiscoverButtons(HashSet<GameObject> addedObjects)
        {
            if (_activePopup == null) return;

            // Find CustomButtons
            foreach (var mb in _activePopup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != T.CustomButtonWithTooltip) continue;

                string label = GetButtonLabel(mb.gameObject);
                MelonLogger.Msg($"[{NavigatorId}] Adding button: {label} ({mb.gameObject.name})");
                AddElement(mb.gameObject, label);
                addedObjects.Add(mb.gameObject);
            }

            // Find EventTriggers (like Background_ClickBlocker)
            foreach (var trigger in _activePopup.GetComponentsInChildren<EventTrigger>(true))
            {
                if (trigger == null || !trigger.gameObject.activeInHierarchy) continue;
                if (addedObjects.Contains(trigger.gameObject)) continue;

                string name = trigger.gameObject.name;
                if (name == "Background_ClickBlocker")
                {
                    AddElement(trigger.gameObject, "Continue, button");
                    addedObjects.Add(trigger.gameObject);
                }
            }
        }

        private string GetButtonLabel(GameObject button)
        {
            string text = UITextExtractor.GetButtonText(button, null);
            if (!string.IsNullOrEmpty(text) && text.Length < 30)
                return $"{text}, button";

            string name = button.name;
            if (name.Contains("ClaimButton") || name.Contains("Mehr"))
                return "More, button";
            if (name.Contains("Close") || name.Contains("Dismiss"))
                return "Close, button";
            if (name.Contains("Continue") || name.Contains("Done"))
                return "Continue, button";

            return "Button";
        }

        /// <summary>
        /// Debug: Dump the structure of a reward prefab to understand what data is available.
        /// </summary>
        private void DumpRewardPrefabStructure(GameObject rewardPrefab, string rewardType, int index)
        {
            MelonLogger.Msg($"[{NavigatorId}] === REWARD PREFAB DEBUG #{index} ===");
            MelonLogger.Msg($"[{NavigatorId}] Prefab name: {rewardPrefab.name}");
            MelonLogger.Msg($"[{NavigatorId}] Detected type: {rewardType}");

            // List all TMP_Text components
            var texts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
            MelonLogger.Msg($"[{NavigatorId}] TMP_Text components: {texts.Length}");
            foreach (var text in texts)
            {
                if (text != null)
                {
                    string content = text.text?.Trim() ?? "(null)";
                    MelonLogger.Msg($"[{NavigatorId}]   Text in '{text.gameObject.name}': '{content}' (active: {text.gameObject.activeInHierarchy})");
                }
            }

            // List key child objects and their components
            MelonLogger.Msg($"[{NavigatorId}] Child structure (depth 3):");
            DumpChildStructure(rewardPrefab.transform, 0, 3);

            // List all MonoBehaviour types
            var monoBehaviours = rewardPrefab.GetComponentsInChildren<MonoBehaviour>(true);
            var typeNames = new HashSet<string>();
            foreach (var mb in monoBehaviours)
            {
                if (mb != null)
                    typeNames.Add(mb.GetType().Name);
            }
            MelonLogger.Msg($"[{NavigatorId}] Component types: {string.Join(", ", typeNames)}");

            MelonLogger.Msg($"[{NavigatorId}] === END REWARD PREFAB DEBUG #{index} ===");
        }

        private void DumpChildStructure(Transform parent, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;

            string indent = new string(' ', depth * 2);
            foreach (Transform child in parent)
            {
                if (child == null) continue;
                string activeStatus = child.gameObject.activeInHierarchy ? "" : " [INACTIVE]";
                MelonLogger.Msg($"[{NavigatorId}]   {indent}- {child.name}{activeStatus}");
                DumpChildStructure(child, depth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Extract a readable label for a reward prefab.
        /// Copied exactly from GeneralMenuNavigator.ExtractRewardLabel().
        /// </summary>
        private string ExtractRewardLabel(GameObject rewardPrefab, string rewardType, int index)
        {
            switch (rewardType)
            {
                case "Card":
                    var cardObj = FindCardObjectInReward(rewardPrefab);
                    if (cardObj != null)
                    {
                        var cardInfo = CardDetector.ExtractCardInfo(cardObj);
                        if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.Name))
                        {
                            string cardLabel = $"Card {index}: {cardInfo.Name}";
                            if (!string.IsNullOrEmpty(cardInfo.TypeLine))
                                cardLabel += $", {cardInfo.TypeLine}";
                            return cardLabel;
                        }
                    }
                    var cardTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in cardTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content) && content != "+99" && !content.StartsWith("+"))
                                return $"Card {index}: {content}";
                        }
                    }
                    return $"Card {index}";

                case "Pack":
                    // Use pre-extracted set name from controller data
                    if (_packSetNameIndex < _packSetNames.Count)
                    {
                        string setName = _packSetNames[_packSetNameIndex++];
                        // Check for count text (e.g., "x3")
                        string countText = null;
                        var packTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                        foreach (var text in packTexts)
                        {
                            if (text != null && text.gameObject.activeInHierarchy)
                            {
                                string content = text.text?.Trim();
                                if (!string.IsNullOrEmpty(content) && content.StartsWith("x"))
                                {
                                    countText = content;
                                    break;
                                }
                            }
                        }
                        return countText != null ? $"{setName} Pack {countText}" : $"{setName} Pack";
                    }
                    return "Booster Pack";

                case "CardSleeve":
                    var sleeveTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in sleeveTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content))
                                return $"Card Sleeve: {content}";
                        }
                    }
                    return $"Card Sleeve {index}";

                case "Currency":
                    // Determine currency type from prefab name
                    string currencyType = "Gold";
                    if (rewardPrefab.name.Contains("Gems"))
                        currencyType = "Gems";
                    else if (rewardPrefab.name.Contains("Coins") || rewardPrefab.name.Contains("Gold"))
                        currencyType = "Gold";

                    // Find quantity in Text_Quantity
                    var currencyTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in currencyTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy && text.gameObject.name == "Text_Quantity")
                        {
                            string quantity = text.text?.Trim();
                            if (!string.IsNullOrEmpty(quantity))
                                return $"{quantity} {currencyType}";
                        }
                    }
                    // Fallback: find any active text
                    foreach (var text in currencyTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content) && char.IsDigit(content[0]))
                                return $"{content} {currencyType}";
                        }
                    }
                    return currencyType;

                case "XP":
                    // Find quantity in Text_Quantity
                    var xpTexts = rewardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                    foreach (var text in xpTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy && text.gameObject.name == "Text_Quantity")
                        {
                            string quantity = text.text?.Trim();
                            if (!string.IsNullOrEmpty(quantity))
                                return $"{quantity} XP";
                        }
                    }
                    // Fallback: find any active numeric text
                    foreach (var text in xpTexts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text?.Trim();
                            if (!string.IsNullOrEmpty(content) && char.IsDigit(content[0]))
                                return $"{content} XP";
                        }
                    }
                    return "XP";

                default:
                    return $"Reward {index}";
            }
        }

        // TryGetPackNameFromReward removed - NotificationPopupReward has no reward data fields.
        // Pack names are now pre-extracted from ContentControllerRewards._packReward.ToAdd
        // via ExtractPackSetNames() during discovery.

        /// <summary>
        /// Find the actual card object inside a reward prefab.
        /// Copied exactly from GeneralMenuNavigator.FindCardObjectInReward().
        /// </summary>
        private GameObject FindCardObjectInReward(GameObject rewardPrefab)
        {
            foreach (var mb in rewardPrefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName == T.BoosterMetaCardView ||
                    typeName == T.RewardDisplayCard ||
                    typeName == T.PagesMetaCardView ||
                    typeName == T.MetaCardView ||
                    typeName == T.MetaCDC ||
                    typeName == T.CardView ||
                    typeName == T.DuelCardView)
                {
                    return mb.gameObject;
                }
            }

            var cardAnchor = rewardPrefab.transform.Find("CardAnchor");
            if (cardAnchor != null)
            {
                foreach (Transform child in cardAnchor.GetComponentsInChildren<Transform>(true))
                {
                    if (child.gameObject.activeInHierarchy && CardDetector.IsCard(child.gameObject))
                        return child.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a clickable target within a reward prefab.
        /// Copied exactly from GeneralMenuNavigator.FindRewardClickTarget().
        /// </summary>
        private GameObject FindRewardClickTarget(GameObject rewardPrefab, string rewardType = null)
        {
            if (rewardType == "Card")
            {
                var cardObj = FindCardObjectInReward(rewardPrefab);
                if (cardObj != null)
                    return cardObj;
            }

            foreach (Transform child in rewardPrefab.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                string name = child.name;
                if (name == "Hitbox_Middle" || name.Contains("Hitbox"))
                {
                    foreach (var mb in child.GetComponents<MonoBehaviour>())
                    {
                        string typeName = mb?.GetType().Name;
                        if (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip)
                            return child.gameObject;
                    }
                }
            }

            foreach (var mb in rewardPrefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string typeName = mb?.GetType().Name;
                if (mb != null && mb.gameObject.activeInHierarchy &&
                    (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip))
                    return mb.gameObject;
            }

            var cardAnchor = rewardPrefab.transform.Find("CardAnchor");
            if (cardAnchor != null && cardAnchor.gameObject.activeInHierarchy)
            {
                foreach (var mb in cardAnchor.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    string typeName = mb?.GetType().Name;
                    if (mb != null && mb.gameObject.activeInHierarchy &&
                        (typeName == T.CustomButton || typeName == T.CustomButtonWithTooltip))
                        return mb.gameObject;
                }
            }

            return null;
        }

        #endregion

        #region Input Handling

        protected override string GetActivationAnnouncement()
        {
            string rewardInfo = _rewardCount > 0 ? $" {_rewardCount} rewards." : "";
            return $"Rewards. Left and Right to navigate. Up and Down for card details. Backspace to continue.{rewardInfo}";
        }

        protected override void HandleInput()
        {
            if (HandleCustomInput()) return;

            // Left/Right navigation
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

            // Home/End
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

            // Tab navigation
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                return;
            }

            // Enter activates current element
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    MelonLogger.Msg($"[{NavigatorId}] Activating: {elem.Label}");
                    UIActivator.Activate(elem.GameObject);

                    // Always rescan after activation - claiming a reward destroys the
                    // GameObject, which leaves stale references in _elements
                    MelonLogger.Msg($"[{NavigatorId}] Scheduling rescan after activation");
                    ForceRescan();
                }
                return;
            }

            // Backspace dismisses the popup
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                DismissRewardsPopup();
                return;
            }
        }

        /// <summary>
        /// Dismiss the rewards popup by clicking the Background_ClickBlocker.
        /// Copied exactly from GeneralMenuNavigator.DismissRewardsPopup().
        /// </summary>
        private bool DismissRewardsPopup()
        {
            MelonLogger.Msg($"[{NavigatorId}] Dismissing rewards popup");

            var screenspacePopups = GameObject.Find("Canvas - Screenspace Popups");
            if (screenspacePopups == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Canvas - Screenspace Popups not found");
                return false;
            }

            Transform rewardsController = null;
            foreach (Transform child in screenspacePopups.transform)
            {
                if (child.name.Contains("ContentController") && child.name.Contains("Rewards") &&
                    child.gameObject.activeInHierarchy)
                {
                    rewardsController = child;
                    break;
                }
            }

            if (rewardsController == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Rewards controller not found");
                return false;
            }

            var clickBlocker = rewardsController.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Background_ClickBlocker" && t.gameObject.activeInHierarchy);

            if (clickBlocker != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Clicking Background_ClickBlocker to dismiss rewards popup");
                _announcer.Announce(Strings.Continuing, AnnouncementPriority.Normal);
                UIActivator.SimulatePointerClick(clickBlocker.gameObject);
                ForceRescan();
                return true;
            }

            var dismissButton = rewardsController.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => (t.name.Contains("Dismiss") || t.name.Contains("Close") ||
                                      t.name.Contains("Continue") || t.name.Contains("Back")) &&
                                     t.gameObject.activeInHierarchy);

            if (dismissButton != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Clicking {dismissButton.name} to dismiss rewards popup");
                _announcer.Announce(Strings.Continuing, AnnouncementPriority.Normal);
                UIActivator.Activate(dismissButton.gameObject);
                ForceRescan();
                return true;
            }

            MelonLogger.Msg($"[{NavigatorId}] No dismiss element found in rewards popup");
            return false;
        }

        #endregion

        #region Update with rescan support

        public override void Update()
        {
            // Check if we need to rescan for rewards (timing issue - rewards may load after popup appears)
            if (_isActive && _rewardCount == 0 && _rescanAttempts < MaxRescanAttempts)
            {
                _rescanFrameCounter++;
                if (_rescanFrameCounter >= RescanDelayFrames)
                {
                    _rescanFrameCounter = 0;
                    _rescanAttempts++;
                    MelonLogger.Msg($"[{NavigatorId}] Rescanning for rewards (attempt {_rescanAttempts}/{MaxRescanAttempts})");
                    ForceRescan();

                    // If we found rewards this time, announce them
                    if (_rewardCount > 0)
                    {
                        _announcer.AnnounceInterrupt(Strings.FoundRewards(_rewardCount));
                    }
                }
            }

            base.Update();
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            // Reset rescan counters on activation
            _rescanFrameCounter = 0;
            _rescanAttempts = 0;
        }

        #endregion

        #region Lifecycle

        protected override bool ValidateElements()
        {
            if (_activePopup == null || !_activePopup.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] Popup no longer active");
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
            _activePopup = null;
            _packSetNames.Clear();
            _packSetNameIndex = 0;
        }

        #endregion
    }
}
