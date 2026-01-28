using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the NPE (New Player Experience) reward screen.
    /// Shows unlocked cards after completing tutorial objectives.
    /// Uses Left/Right for navigation (like pack opening), Up/Down for card details.
    /// </summary>
    public class NPERewardNavigator : BaseNavigator
    {
        private GameObject _rewardsContainer;
        private int _totalCards;

        public override string NavigatorId => "NPEReward";
        public override string ScreenName => GetScreenName();
        public override int Priority => 75; // Above GeneralMenuNavigator (15), below BoosterOpenNavigator (80)

        public NPERewardNavigator(IAnnouncementService announcer) : base(announcer) { }

        private void Log(string message) => MelonLogger.Msg($"[{NavigatorId}] {message}");

        private string GetScreenName()
        {
            if (_totalCards > 0)
                return $"Card Unlocked, {_totalCards} {(_totalCards == 1 ? "card" : "cards")}";
            return "Card Unlocked";
        }

        private string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }

        protected override bool DetectScreen()
        {
            // Look for NPE-Rewards_Container
            var npeContainer = GameObject.Find("NPE-Rewards_Container");
            if (npeContainer == null)
            {
                _rewardsContainer = null;
                return false;
            }

            if (!npeContainer.activeInHierarchy)
            {
                Log($"NPE-Rewards_Container found but NOT active");
                _rewardsContainer = null;
                return false;
            }

            Log($"=== NPE REWARD SCREEN DETECTION ===");
            Log($"NPE-Rewards_Container: FOUND and ACTIVE");
            Log($"  Path: {GetPath(npeContainer.transform)}");

            // Log all direct children
            Log($"  Direct children ({npeContainer.transform.childCount}):");
            foreach (Transform child in npeContainer.transform)
            {
                Log($"    - {child.name} (active={child.gameObject.activeInHierarchy})");
            }

            // Check for ActiveContainer with RewardsCONTAINER (actual reward cards)
            var activeContainer = npeContainer.transform.Find("ActiveContainer");
            if (activeContainer == null)
            {
                Log($"ActiveContainer: NOT FOUND");
                _rewardsContainer = null;
                return false;
            }

            if (!activeContainer.gameObject.activeInHierarchy)
            {
                Log($"ActiveContainer: FOUND but NOT active");
                _rewardsContainer = null;
                return false;
            }

            Log($"ActiveContainer: FOUND and ACTIVE");
            Log($"  Direct children ({activeContainer.childCount}):");
            foreach (Transform child in activeContainer)
            {
                Log($"    - {child.name} (active={child.gameObject.activeInHierarchy})");
            }

            var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
            if (rewardsContainer == null)
            {
                Log($"RewardsCONTAINER: NOT FOUND");
                _rewardsContainer = null;
                return false;
            }

            if (!rewardsContainer.gameObject.activeInHierarchy)
            {
                Log($"RewardsCONTAINER: FOUND but NOT active");
                _rewardsContainer = null;
                return false;
            }

            Log($"RewardsCONTAINER: FOUND and ACTIVE");
            Log($"  Direct children ({rewardsContainer.childCount}):");
            foreach (Transform child in rewardsContainer)
            {
                string components = string.Join(", ", child.GetComponents<Component>().Select(c => c?.GetType().Name ?? "null"));
                Log($"    - {child.name} (active={child.gameObject.activeInHierarchy}) [{components}]");
            }

            // Verify we have actual card prefabs in the rewards container
            bool hasCards = false;
            int cardCount = 0;
            foreach (Transform child in rewardsContainer)
            {
                if (child.name.Contains("NPERewardPrefab_IndividualCard"))
                {
                    hasCards = true;
                    cardCount++;
                }
            }

            Log($"Card prefabs found: {cardCount}");

            if (!hasCards)
            {
                Log($"NO card prefabs found - screen not ready");
                _rewardsContainer = null;
                return false;
            }

            // Check for NPEContentControllerRewards (needed for button activation)
            bool foundController = false;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "NPEContentControllerRewards")
                {
                    foundController = true;
                    Log($"NPEContentControllerRewards: FOUND on {mb.gameObject.name}");
                    Log($"  Path: {GetPath(mb.transform)}");
                    break;
                }
            }
            if (!foundController)
            {
                Log($"NPEContentControllerRewards: NOT FOUND (button activation may fail!)");
            }

            _rewardsContainer = npeContainer;
            Log($"=== SCREEN DETECTION: SUCCESS ===");
            return true;
        }

        protected override void DiscoverElements()
        {
            Log($"=== DISCOVERING ELEMENTS ===");
            _totalCards = 0;
            var addedObjects = new HashSet<GameObject>();

            // Find card entries
            FindCardEntries(addedObjects);

            // Find take reward button
            FindTakeRewardButton(addedObjects);

            Log($"=== DISCOVERY COMPLETE: {_elements.Count} elements ===");
            for (int i = 0; i < _elements.Count; i++)
            {
                var el = _elements[i];
                Log($"  [{i}] {el.Label} -> {el.GameObject?.name ?? "NULL"} (ID:{el.GameObject?.GetInstanceID()})");
            }
        }

        private void FindCardEntries(HashSet<GameObject> addedObjects)
        {
            if (_rewardsContainer == null)
            {
                Log($"FindCardEntries: _rewardsContainer is NULL");
                return;
            }

            var cardEntries = new List<(GameObject obj, float sortOrder, string name, string typeLine)>();

            // Find NPE reward card prefabs in RewardsCONTAINER
            var activeContainer = _rewardsContainer.transform.Find("ActiveContainer");
            if (activeContainer == null)
            {
                Log($"FindCardEntries: ActiveContainer not found");
                return;
            }

            var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
            if (rewardsContainer == null)
            {
                Log($"FindCardEntries: RewardsCONTAINER not found");
                return;
            }

            Log($"Scanning RewardsCONTAINER for cards...");

            foreach (Transform child in rewardsContainer)
            {
                Log($"  Checking: {child.name}");

                if (!child.gameObject.activeInHierarchy)
                {
                    Log($"    SKIPPED: not active");
                    continue;
                }

                if (!child.name.Contains("NPERewardPrefab_IndividualCard"))
                {
                    Log($"    SKIPPED: name doesn't contain NPERewardPrefab_IndividualCard");
                    continue;
                }

                Log($"    MATCHED as card prefab");

                // Log hierarchy of this card prefab
                Log($"    Children of card prefab:");
                foreach (Transform cardChild in child)
                {
                    Log($"      - {cardChild.name} (active={cardChild.gameObject.activeInHierarchy})");
                }

                // Get the card anchor or the prefab itself for navigation
                var cardAnchor = child.Find("CardAnchor");
                GameObject cardObj = cardAnchor?.gameObject ?? child.gameObject;

                Log($"    Navigation target: {cardObj.name} (used CardAnchor={cardAnchor != null})");
                Log($"    Target path: {GetPath(cardObj.transform)}");

                // Log components on the navigation target
                var components = cardObj.GetComponents<Component>();
                Log($"    Target components ({components.Length}):");
                foreach (var comp in components)
                {
                    Log($"      - {comp?.GetType().Name ?? "null"}");
                }

                if (addedObjects.Contains(cardObj))
                {
                    Log($"    SKIPPED: already added");
                    continue;
                }

                // Extract card info with detailed logging
                Log($"    Extracting card info from: {child.gameObject.name}");
                var cardInfo = CardDetector.ExtractCardInfo(child.gameObject);
                Log($"    CardInfo.IsValid: {cardInfo.IsValid}");
                Log($"    CardInfo.Name: {cardInfo.Name ?? "null"}");
                Log($"    CardInfo.TypeLine: {cardInfo.TypeLine ?? "null"}");
                Log($"    CardInfo.ManaCost: {cardInfo.ManaCost ?? "null"}");
                Log($"    CardInfo.RulesText: {(cardInfo.RulesText?.Length > 50 ? cardInfo.RulesText.Substring(0, 50) + "..." : cardInfo.RulesText ?? "null")}");

                string cardName = cardInfo.IsValid ? cardInfo.Name : "Unknown card";
                string typeLine = cardInfo.IsValid ? cardInfo.TypeLine : "";

                // Sort by X position (left to right)
                float sortOrder = child.position.x;
                cardEntries.Add((cardObj, sortOrder, cardName, typeLine));
                addedObjects.Add(cardObj);

                Log($"    ADDED: {cardName} at x={sortOrder:F2}");
            }

            // Sort cards by position (left to right)
            cardEntries = cardEntries.OrderBy(x => x.sortOrder).ToList();

            _totalCards = cardEntries.Count;
            Log($"Total cards found: {_totalCards}");

            // Add cards to navigation
            int cardNum = 1;
            foreach (var (cardObj, sortX, cardName, typeLine) in cardEntries)
            {
                string label = _totalCards > 1
                    ? $"Unlocked card {cardNum}: {cardName}"
                    : $"{cardName}";

                if (!string.IsNullOrEmpty(typeLine))
                {
                    label += $", {typeLine}";
                }

                Log($"Adding element: '{label}' -> {cardObj.name}");
                AddElement(cardObj, label);
                cardNum++;
            }
        }

        private void FindTakeRewardButton(HashSet<GameObject> addedObjects)
        {
            if (_rewardsContainer == null)
            {
                Log($"FindTakeRewardButton: _rewardsContainer is NULL");
                return;
            }

            Log($"Searching for NullClaimButton...");

            // Find NullClaimButton - the "take reward" button
            GameObject foundButton = null;
            foreach (var transform in _rewardsContainer.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null) continue;

                if (transform.name == "NullClaimButton")
                {
                    Log($"  Found NullClaimButton:");
                    Log($"    Path: {GetPath(transform)}");
                    Log($"    Active: {transform.gameObject.activeInHierarchy}");

                    if (!transform.gameObject.activeInHierarchy)
                    {
                        Log($"    SKIPPED: not active");
                        continue;
                    }

                    // Log all components
                    var components = transform.GetComponents<Component>();
                    Log($"    Components ({components.Length}):");
                    bool hasCustomButton = false;
                    foreach (var comp in components)
                    {
                        string compName = comp?.GetType().Name ?? "null";
                        Log($"      - {compName}");
                        if (compName == "CustomButton")
                        {
                            hasCustomButton = true;
                            // Log CustomButton fields
                            LogCustomButtonDetails(comp as MonoBehaviour);
                        }
                    }

                    if (!hasCustomButton)
                    {
                        Log($"    WARNING: No CustomButton component found!");
                    }

                    if (addedObjects.Contains(transform.gameObject))
                    {
                        Log($"    SKIPPED: already added");
                        continue;
                    }

                    foundButton = transform.gameObject;
                    break;
                }
            }

            if (foundButton != null)
            {
                AddElement(foundButton, "Take reward, button");
                addedObjects.Add(foundButton);
                Log($"Take reward button ADDED");
            }
            else
            {
                Log($"NullClaimButton NOT FOUND in hierarchy!");
            }
        }

        private void LogCustomButtonDetails(MonoBehaviour customButton)
        {
            if (customButton == null) return;

            var type = customButton.GetType();
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            Log($"      CustomButton fields:");
            foreach (var field in type.GetFields(flags))
            {
                try
                {
                    var val = field.GetValue(customButton);
                    string valStr;
                    if (val == null)
                        valStr = "null";
                    else if (val is string || val is bool || val is int || val is float || val is System.Enum)
                        valStr = val.ToString();
                    else if (val is UnityEngine.Events.UnityEventBase eventBase)
                        valStr = $"<UnityEvent, listeners={eventBase.GetPersistentEventCount()}>";
                    else
                        valStr = $"<{val.GetType().Name}>";

                    // Only log interesting fields
                    if (field.Name.Contains("click") || field.Name.Contains("Click") ||
                        field.Name.Contains("event") || field.Name.Contains("Event") ||
                        field.Name.Contains("action") || field.Name.Contains("Action") ||
                        field.Name.Contains("interactable") || field.Name.Contains("Interactable"))
                    {
                        Log($"        {field.Name}: {valStr}");
                    }
                }
                catch { }
            }
        }

        protected override string GetActivationAnnouncement()
        {
            string cardInfo = _totalCards > 0 ? $"{_totalCards} {(_totalCards == 1 ? "card" : "cards")}. " : "";
            return $"{ScreenName}. {cardInfo}Left and Right to navigate, Up and Down for card details.";
        }

        protected override void HandleInput()
        {
            // Handle custom input first (F1 help, etc.)
            if (HandleCustomInput()) return;

            // Left/Right arrows for navigation between cards
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                Log($"Input: Left/A - MovePrevious (current={_currentIndex}, total={_elements.Count})");
                MovePrevious();
                LogCurrentState();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                Log($"Input: Right/D - MoveNext (current={_currentIndex}, total={_elements.Count})");
                MoveNext();
                LogCurrentState();
                return;
            }

            // Home/End for quick jump to first/last
            if (Input.GetKeyDown(KeyCode.Home))
            {
                Log($"Input: Home - MoveFirst");
                MoveFirst();
                LogCurrentState();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                Log($"Input: End - MoveLast");
                MoveLast();
                LogCurrentState();
                return;
            }

            // Tab/Shift+Tab also navigates (consistent with pack opening)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                Log($"Input: Tab (shift={shiftTab})");
                if (shiftTab)
                    MovePrevious();
                else
                    MoveNext();
                LogCurrentState();
                return;
            }

            // Enter activates (take reward or view card)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Log($"Input: Enter - ActivateCurrentElement");
                LogCurrentState();
                ActivateCurrentElement();
                return;
            }

            // Backspace dismisses (activates take reward button)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                Log($"Input: Backspace - Finding Take reward button");
                // Find and activate the take reward button
                foreach (var element in _elements)
                {
                    if (element.Label.Contains("Take reward"))
                    {
                        Log($"  Activating: {element.Label} -> {element.GameObject?.name}");
                        var result = UIActivator.Activate(element.GameObject);
                        Log($"  Activation result: Success={result.Success}, Message={result.Message}, Type={result.Type}");
                        return;
                    }
                }
                Log($"  Take reward button not found in elements!");
                return;
            }
        }

        private void LogCurrentState()
        {
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
            {
                var current = _elements[_currentIndex];
                Log($"  Current: [{_currentIndex}] {current.Label}");
                Log($"    GameObject: {current.GameObject?.name ?? "NULL"} (active={current.GameObject?.activeInHierarchy})");

                // Check if this is a card and if CardInfoNavigator should be prepared
                if (current.GameObject != null)
                {
                    bool isCard = CardDetector.IsCard(current.GameObject);
                    Log($"    IsCard: {isCard}");

                    var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                    if (cardNav != null)
                    {
                        Log($"    CardInfoNavigator.IsActive: {cardNav.IsActive}");
                        Log($"    CardInfoNavigator.CurrentCard: {cardNav.CurrentCard?.name ?? "null"}");
                    }
                }
            }
            else
            {
                Log($"  Current: INVALID INDEX {_currentIndex} (count={_elements.Count})");
            }
        }

        protected override bool ValidateElements()
        {
            // Check if rewards container is still active
            if (_rewardsContainer == null || !_rewardsContainer.activeInHierarchy)
            {
                Log($"ValidateElements: Rewards container no longer active");
                return false;
            }

            return base.ValidateElements();
        }

        public override void OnSceneChanged(string sceneName)
        {
            Log($"OnSceneChanged: {sceneName}");
            if (_isActive)
            {
                Deactivate();
            }
            _rewardsContainer = null;
        }
    }
}
