using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Handles navigation for screens using EventTrigger or CustomButton components
    /// instead of standard Unity UI Selectables (NPE, rewards, pack opening, etc.)
    /// </summary>
    public class EventTriggerNavigator : BaseNavigator
    {
        private const float POST_CLICK_SCAN_DELAY = 2.0f;
        private static readonly System.Reflection.BindingFlags AllInstance =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;

        private bool _pendingChangeCheck;
        private float _changeCheckDelay;
        private bool _waitingForMainButton;
        private GameObject _currentContext;
        private string _contextType; // "CardReveal", "Rewards", "General"

        public override string NavigatorId => "EventTrigger";
        public override string ScreenName => GetContextScreenName();
        public override int Priority => 10; // Low priority - fallback navigator

        public EventTriggerNavigator(IAnnouncementService announcer) : base(announcer) { }

        private string GetContextScreenName()
        {
            return _contextType switch
            {
                "CardReveal" => "Unlocked Cards",
                "Rewards" => "Rewards screen",
                _ => "Interactive screen"
            };
        }

        protected override bool DetectScreen()
        {
            // Only activate if no standard selectables exist
            var selectables = GameObject.FindObjectsOfType<Selectable>();
            foreach (var s in selectables)
            {
                if (s.isActiveAndEnabled && s.interactable)
                    return false;
            }

            // Check for specific contexts
            var deckInspection = GameObject.Find("DeckInspection_Container");
            if (deckInspection != null && deckInspection.activeInHierarchy)
            {
                _currentContext = deckInspection;
                _contextType = "CardReveal";
                return true;
            }

            var rewardsContainer = GameObject.Find("NPE-Rewards_Container");
            if (rewardsContainer != null && rewardsContainer.activeInHierarchy)
            {
                _currentContext = rewardsContainer;
                _contextType = "Rewards";
                return true;
            }

            // Fallback: check for any EventTriggers
            var triggers = GameObject.FindObjectsOfType<EventTrigger>();
            if (triggers.Any(t => t.gameObject.activeInHierarchy))
            {
                _currentContext = null;
                _contextType = "General";
                return true;
            }

            return false;
        }

        protected override void DiscoverElements()
        {
            var addedObjects = new HashSet<GameObject>();

            switch (_contextType)
            {
                case "CardReveal":
                    DiscoverCardRevealElements(addedObjects);
                    break;
                case "Rewards":
                    DiscoverRewardElements(addedObjects);
                    break;
                default:
                    DiscoverGeneralElements(addedObjects);
                    break;
            }
        }

        public override void Update()
        {
            // Check for pending rescan
            if (_pendingChangeCheck)
            {
                _changeCheckDelay -= Time.deltaTime;
                if (_changeCheckDelay <= 0)
                {
                    _pendingChangeCheck = false;
                    MelonLogger.Msg($"[{NavigatorId}] Post-click rescan...");
                    DumpUIElements();
                    _isActive = false; // Force re-detection
                }
            }

            // Check for MainButton appearing
            if (_waitingForMainButton && _isActive)
            {
                var mainButton = GameObject.Find("MainButton");
                if (mainButton != null && mainButton.activeInHierarchy)
                {
                    MelonLogger.Msg($"[{NavigatorId}] MainButton appeared - rescanning");
                    _waitingForMainButton = false;
                    _isActive = false; // Force rescan
                }
            }

            base.Update();
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            string label = _elements[index].Label;

            bool isChest = label == "Reward Chest" || element.name.Contains("RewardChest");
            bool isDeckBox = label.StartsWith("Deck");
            bool isContinue = label.StartsWith("Continue") && label.Contains("click anywhere");

            if (isChest || isDeckBox)
            {
                GameObject clickTarget = FindClickTarget(element) ?? element;
                HandleSpecialNPEElement(clickTarget, isChest, isDeckBox);
                SchedulePostClickScan();
                return true;
            }

            if (isContinue)
            {
                MelonLogger.Msg($"[{NavigatorId}] Using screen center click for: {element.name}");
                UIActivator.SimulateScreenCenterClick();
                SchedulePostClickScan();
                return true;
            }

            // Standard activation - but still schedule rescan
            var result = UIActivator.Activate(element);
            if (result.Type == ActivationType.Toggle)
                _announcer.AnnounceInterrupt(result.Message);
            else
                _announcer.Announce(result.Message, AnnouncementPriority.Normal);

            SchedulePostClickScan();
            return true; // We handled it (with rescan)
        }

        private void SchedulePostClickScan()
        {
            MelonLogger.Msg($"[{NavigatorId}] Scheduling rescan after activation");
            _isActive = false;
            _pendingChangeCheck = true;
            _changeCheckDelay = POST_CLICK_SCAN_DELAY;
        }

        #region Card Reveal Context

        private void DiscoverCardRevealElements(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Card reveal context detected");

            // Find revealed cards in UnlockedCardsCONTAINER
            var unlockedContainer = _currentContext.transform.Find("UnlockedCardsCONTAINER");
            if (unlockedContainer != null)
            {
                FindRevealedCards(unlockedContainer.gameObject, addedObjects);
            }

            // Find Back button
            var backButton = _currentContext.transform.Find("BackButton");
            if (backButton != null && backButton.gameObject.activeInHierarchy)
            {
                AddElement(backButton.gameObject, "Back, button");
                addedObjects.Add(backButton.gameObject);
            }

            // Find Play/Continue button (MainButton)
            var mainButton = GameObject.Find("MainButton");
            if (mainButton != null && mainButton.activeInHierarchy && !addedObjects.Contains(mainButton))
            {
                string label = GetButtonText(mainButton, "Play");
                AddElement(mainButton, $"{label}, button");
                addedObjects.Add(mainButton);
            }
        }

        private void FindRevealedCards(GameObject container, HashSet<GameObject> addedObjects)
        {
            var cardPrefabs = new List<GameObject>();

            foreach (Transform child in container.transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                if (child.name.Contains("NPERewardPrefab_IndividualCard"))
                {
                    cardPrefabs.Add(child.gameObject);
                }
                else if (child.name.Contains("LockedCard"))
                {
                    var stageText = child.Find("UnlockOnStageText");
                    string stage = "?";
                    if (stageText != null)
                    {
                        var tmp = stageText.GetComponent<TMPro.TMP_Text>();
                        if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                            stage = tmp.text.Trim();
                    }

                    AddElement(child.gameObject, $"Locked card (unlocks at stage {stage})");
                    addedObjects.Add(child.gameObject);
                }
            }

            // Sort cards by X position (left to right)
            cardPrefabs = cardPrefabs.OrderBy(c => c.transform.position.x).ToList();

            int cardNum = 1;
            foreach (var cardPrefab in cardPrefabs)
            {
                var cardAnchor = cardPrefab.transform.Find("CardAnchor");
                if (cardAnchor == null)
                    cardAnchor = cardPrefab.transform;

                string cardName = ExtractCardName(cardPrefab);
                string label = !string.IsNullOrEmpty(cardName)
                    ? $"Card {cardNum}: {cardName}"
                    : $"Card {cardNum}";

                AddElement(cardAnchor.gameObject, label);
                addedObjects.Add(cardAnchor.gameObject);
                cardNum++;
            }
        }

        private string ExtractCardName(GameObject cardPrefab)
        {
            var texts = cardPrefab.GetComponentsInChildren<TMPro.TMP_Text>(true);

            // Look for Title element specifically
            foreach (var text in texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy) continue;
                if (text.gameObject.name == "Title")
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "");
                        return content.Trim();
                    }
                }
            }

            // Fallback: look for any reasonable text
            foreach (var text in texts)
            {
                if (text == null) continue;
                string content = text.text?.Trim();
                if (string.IsNullOrEmpty(content)) continue;

                string objName = text.gameObject.name.ToLower();
                if (objName.Contains("artist") || objName.Contains("mana") ||
                    objName.Contains("type") || objName.Contains("power")) continue;

                content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                if (content.Length >= 2 && content.Length <= 40 && !content.Contains("\n"))
                    return content;
            }

            return null;
        }

        #endregion

        #region Rewards Context

        private void DiscoverRewardElements(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Reward context detected");

            // Find the chest
            var chestTrigger = GameObject.Find("NPE_RewardChest");
            if (chestTrigger != null && chestTrigger.activeInHierarchy)
            {
                AddElement(chestTrigger, "Reward Chest");
                addedObjects.Add(chestTrigger);
            }

            // Find reward cards/deck boxes
            FindRewardCards(addedObjects);

            // Find quest/objective elements
            FindQuestElements(addedObjects);

            // Find all active CustomButtons
            FindAllActiveButtons(addedObjects);

            // Check if MainButton exists
            var mainButton = GameObject.Find("MainButton");
            if (mainButton == null || !mainButton.activeInHierarchy)
            {
                MelonLogger.Msg($"[{NavigatorId}] MainButton not found - will rescan when it appears");
                _waitingForMainButton = true;
            }
            else
            {
                _waitingForMainButton = !addedObjects.Contains(mainButton);
            }
        }

        private void FindRewardCards(HashSet<GameObject> addedObjects)
        {
            var allTransforms = GameObject.FindObjectsOfType<Transform>();
            var cardParents = new List<GameObject>();

            foreach (var t in allTransforms)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                var lidChild = t.Find("Hitbox_lid");
                if (lidChild != null && lidChild.gameObject.activeInHierarchy)
                {
                    if (t.Find("Chest_Hitbox") != null) continue;
                    if (addedObjects.Contains(t.gameObject)) continue;
                    cardParents.Add(t.gameObject);
                }
            }

            cardParents = cardParents.OrderBy(c => c.transform.position.x).ToList();

            int cardNum = 1;
            foreach (var card in cardParents)
            {
                string cardName = GetCardName(card);
                string label = !string.IsNullOrEmpty(cardName)
                    ? $"Deck {cardNum}: {cardName}"
                    : $"Deck box {cardNum}";

                AddElement(card, label);
                addedObjects.Add(card);
                cardNum++;
            }
        }

        private string GetCardName(GameObject cardObject)
        {
            var texts = cardObject.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy) continue;
                string content = text.text?.Trim();
                if (string.IsNullOrEmpty(content)) continue;

                string lower = content.ToLower();
                if (lower == "0" || lower == "1" || content.StartsWith("\u200B")) continue;
                if (content.Length < 2 || content.Length > 50) continue;

                string objName = text.gameObject.name.ToLower();
                if (objName.Contains("name") || objName.Contains("title") || objName.Contains("card"))
                    return content;
            }

            foreach (var text in texts)
            {
                if (text == null) continue;
                string content = text.text?.Trim();
                if (!string.IsNullOrEmpty(content) && content.Length > 2 && content.Length < 50)
                {
                    if (!content.StartsWith("\u200B") && content != "0" && content != "1")
                        return content;
                }
            }

            return null;
        }

        private void FindQuestElements(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Searching for quest/objective elements...");

            string globalDescription = FindGlobalQuestDescription();
            var objectives = new List<(GameObject obj, int stageNum)>();
            var seenObjects = new HashSet<GameObject>();

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                var go = mb.gameObject;
                if (seenObjects.Contains(go)) continue;
                seenObjects.Add(go);

                if (addedObjects.Contains(go)) continue;

                string name = go.name;
                if (name.Contains("Container") || name.Contains("Clone") || name.Contains("Desktop"))
                    continue;

                if (!name.StartsWith("Objective_NPE")) continue;

                int stageNum = 0;
                var match = System.Text.RegularExpressions.Regex.Match(name, @"\((\d+)\)");
                if (match.Success)
                    stageNum = int.Parse(match.Groups[1].Value);

                objectives.Add((go, stageNum));
            }

            objectives = objectives.OrderBy(x => x.stageNum).ToList();

            foreach (var (obj, stageNum) in objectives)
            {
                string descForThisStage = (stageNum == 0) ? globalDescription : null;
                string label = GetQuestLabel(obj, stageNum, descForThisStage);

                AddElement(obj, label);
                addedObjects.Add(obj);
            }
        }

        private string FindGlobalQuestDescription()
        {
            foreach (var text in GameObject.FindObjectsOfType<TMPro.TMP_Text>())
            {
                if (text == null || !text.gameObject.activeInHierarchy) continue;

                string objName = text.gameObject.name.ToLower();
                if (objName.Contains("clicktocontinue") || objName.Contains("description") ||
                    objName.Contains("goalprogress") || objName.Contains("objective"))
                {
                    string content = text.text?.Trim();
                    if (!string.IsNullOrEmpty(content) && content != "\u200B" && content.Length > 5)
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                        if (!string.IsNullOrEmpty(content))
                            return content;
                    }
                }
            }
            return null;
        }

        private string GetQuestLabel(GameObject questObj, int stageNum, string globalDescription = null)
        {
            var texts = questObj.GetComponentsInChildren<TMPro.TMP_Text>(true);

            string romanNumeral = null;
            string description = globalDescription;
            bool isCompleted = false;
            bool isLocked = false;

            foreach (var text in texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy) continue;
                string content = text.text?.Trim();
                if (string.IsNullOrEmpty(content) || content == "\u200B") continue;

                content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "").Trim();
                if (string.IsNullOrEmpty(content)) continue;

                string objNameLower = text.gameObject.name.ToLower();

                if (objNameLower.Contains("roman") || objNameLower.Contains("numeral"))
                    romanNumeral = content;
                else if (objNameLower.Contains("desc") || objNameLower.Contains("goal") ||
                         objNameLower.Contains("progress"))
                {
                    if (!string.IsNullOrEmpty(content) && content.Length > 3)
                        description = content;
                }
            }

            foreach (Transform child in questObj.transform)
            {
                string childName = child.name.ToLower();
                if ((childName.Contains("complete") || childName.Contains("check") || childName.Contains("done"))
                    && child.gameObject.activeInHierarchy)
                    isCompleted = true;
                if (childName.Contains("lock") && child.gameObject.activeInHierarchy)
                    isLocked = true;
            }

            string stageLabel = stageNum == 0 ? "Current stage" : $"Stage {stageNum}";
            if (!string.IsNullOrEmpty(romanNumeral))
                stageLabel = $"Stage {romanNumeral}";

            string result = stageLabel;
            if (!string.IsNullOrEmpty(description))
                result += $". {description}";

            if (isCompleted)
                result += ". Completed";
            else if (isLocked)
                result += ". Locked";

            return result;
        }

        private void FindAllActiveButtons(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] Searching for all active buttons...");

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;
                if (addedObjects.Contains(mb.gameObject)) continue;

                string name = mb.gameObject.name;

                if (name.StartsWith("Hitbox_") && !name.Contains("LidOpen")) continue;
                if (name == "NPE-Rewards_Container") continue;
                if (name == "Chest_Hitbox") continue;
                if (name.Contains("Objective") || name.Contains("Quest") || name.Contains("Mission")) continue;

                string label = GetButtonLabel(mb.gameObject, name);

                AddElement(mb.gameObject, $"{label}, button");
                addedObjects.Add(mb.gameObject);
            }
        }

        private string GetButtonLabel(GameObject obj, string name)
        {
            var texts = obj.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;
                string content = text.text?.Trim();
                if (!string.IsNullOrEmpty(content) && content.Length > 1 && content.Length < 50)
                {
                    if (!content.StartsWith("\u200B"))
                        return content;
                }
            }

            string label = name.Replace("_", " ").Replace("NPE", "").Trim();
            if (label.StartsWith("Main Button")) label = "Play";
            if (label.StartsWith("Options Button")) label = "Options";

            return string.IsNullOrEmpty(label) ? name : label;
        }

        #endregion

        #region General Context

        private void DiscoverGeneralElements(HashSet<GameObject> addedObjects)
        {
            foreach (var trigger in GameObject.FindObjectsOfType<EventTrigger>())
            {
                if (!trigger.gameObject.activeInHierarchy) continue;
                string label = CleanName(trigger.gameObject.name);
                AddElement(trigger.gameObject, label);
                addedObjects.Add(trigger.gameObject);
            }
        }

        private string CleanName(string name)
        {
            name = name.Replace("NPE_", "").Replace("_", " ");
            return System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        #endregion

        #region Special NPE Handling

        private void HandleSpecialNPEElement(GameObject target, bool isChest, bool isDeckBox)
        {
            foreach (var behaviour in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name != "NPEContentControllerRewards") continue;

                MelonLogger.Msg($"[{NavigatorId}] Found NPEContentControllerRewards");
                var type = behaviour.GetType();

                if (isChest)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Chest click - starting unlock animation");
                    var coroutineMethod = type.GetMethod("Coroutine_UnlockAnimation", AllInstance);
                    if (coroutineMethod != null)
                    {
                        try
                        {
                            var coroutine = coroutineMethod.Invoke(behaviour, null) as System.Collections.IEnumerator;
                            if (coroutine != null)
                            {
                                behaviour.StartCoroutine(coroutine);
                                MelonLogger.Msg($"[{NavigatorId}] Coroutine_UnlockAnimation started!");
                            }
                        }
                        catch (System.Exception e)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] Coroutine_UnlockAnimation error: {e.Message}");
                        }
                    }
                    TryInvokeMethod(behaviour, type, "AwardAllKeys");
                    return;
                }

                if (isDeckBox)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Deck box click - enabling auto-flip");
                    var setAutoFlip = type.GetMethod("set_AutoFlipping", AllInstance);
                    if (setAutoFlip != null)
                    {
                        try
                        {
                            setAutoFlip.Invoke(behaviour, new object[] { true });
                            MelonLogger.Msg($"[{NavigatorId}] AutoFlipping enabled");
                        }
                        catch (System.Exception e)
                        {
                            MelonLogger.Msg($"[{NavigatorId}] set_AutoFlipping error: {e.Message}");
                        }
                    }
                    TryInvokeMethod(behaviour, type, "OnClaimClicked_Unity");
                    return;
                }
            }

            // Fallback
            MelonLogger.Msg($"[{NavigatorId}] NPE controller not found, falling back to standard activation");
            UIActivator.Activate(target);
        }

        private GameObject FindClickTarget(GameObject parent)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.ToLower();
                if (name.Contains("hitbox") || name.Contains("button"))
                    return child.gameObject;
            }

            var image = parent.GetComponentInChildren<Image>();
            return image?.gameObject;
        }

        private void TryInvokeMethod(object target, System.Type type, string methodName)
        {
            var method = type.GetMethod(methodName, AllInstance);
            if (method == null) return;

            var parameters = method.GetParameters();
            MelonLogger.Msg($"[{NavigatorId}] Trying {methodName} with {parameters.Length} params");

            try
            {
                if (parameters.Length == 0)
                {
                    method.Invoke(target, null);
                    MelonLogger.Msg($"[{NavigatorId}] {methodName}() called successfully");
                }
            }
            catch (System.Exception e)
            {
                MelonLogger.Msg($"[{NavigatorId}] {methodName} error: {e.Message}");
            }
        }

        #endregion

        #region Debug

        private void DumpUIElements()
        {
            MelonLogger.Msg($"[{NavigatorId}] === UI DUMP START ===");

            var allObjects = GameObject.FindObjectsOfType<GameObject>();

            // Log containers
            var containers = new HashSet<string>();
            foreach (var obj in allObjects)
            {
                if (obj == null || !obj.activeInHierarchy) continue;

                string name = obj.name.ToLower();
                if (name.Contains("container") || name.Contains("panel") ||
                    name.Contains("overlay") || name.Contains("screen") ||
                    name.Contains("reward") || name.Contains("card") ||
                    name.Contains("unlock"))
                {
                    string path = GetGameObjectPath(obj);
                    if (!containers.Contains(path))
                    {
                        containers.Add(path);
                        MelonLogger.Msg($"[{NavigatorId}] Container: {path}");
                    }
                }
            }

            // Log CustomButtons (limit to 20)
            int buttonCount = 0;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton")
                {
                    buttonCount++;
                    if (buttonCount <= 20)
                        MelonLogger.Msg($"[{NavigatorId}] CustomButton: {GetGameObjectPath(mb.gameObject)}");
                }
            }
            if (buttonCount > 20)
                MelonLogger.Msg($"[{NavigatorId}] ... and {buttonCount - 20} more CustomButtons");

            // Log EventTriggers
            foreach (var et in GameObject.FindObjectsOfType<EventTrigger>())
            {
                if (et == null || !et.gameObject.activeInHierarchy) continue;
                MelonLogger.Msg($"[{NavigatorId}] EventTrigger: {GetGameObjectPath(et.gameObject)}");
            }

            // Log Selectables
            foreach (var s in GameObject.FindObjectsOfType<Selectable>())
            {
                if (s == null || !s.gameObject.activeInHierarchy || !s.interactable) continue;
                MelonLogger.Msg($"[{NavigatorId}] Selectable ({s.GetType().Name}): {s.gameObject.name}");
            }

            MelonLogger.Msg($"[{NavigatorId}] === UI DUMP END ===");
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            int depth = 0;
            while (parent != null && depth < 5)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            if (parent != null)
                path = ".../" + path;
            return path;
        }

        #endregion
    }
}
