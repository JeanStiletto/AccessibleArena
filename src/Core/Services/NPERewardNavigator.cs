using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Linq;
using static AccessibleArena.Core.Utils.ReflectionUtils;

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
        private bool _isDeckReward;
        private string _lastDetectState;
        private int _discoveredDeckBoxCount;

        public override string NavigatorId => "NPEReward";
        public override string ScreenName => GetScreenName();
        public override int Priority => 75; // Above GeneralMenuNavigator (15), below BoosterOpenNavigator (80)

        public NPERewardNavigator(IAnnouncementService announcer) : base(announcer) { }

        private string GetScreenName()
        {
            if (_isDeckReward)
            {
                if (_totalCards > 0)
                    return Strings.ScreenDecksUnlockedCount(_totalCards);
                return Strings.ScreenDecksUnlocked;
            }
            if (_totalCards > 0)
                return Strings.ScreenCardUnlockedCount(_totalCards);
            return Strings.ScreenCardUnlocked;
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
                LogStateChange("not_found");
                _rewardsContainer = null;
                return false;
            }

            if (!npeContainer.activeInHierarchy)
            {
                LogStateChange("inactive", "NPE-Rewards_Container found but NOT active");
                _rewardsContainer = null;
                return false;
            }

            // Check for ActiveContainer with RewardsCONTAINER (actual reward cards)
            var activeContainer = npeContainer.transform.Find("ActiveContainer");
            if (activeContainer == null)
            {
                LogStateChange("no_active_container", "ActiveContainer: NOT FOUND");
                _rewardsContainer = null;
                return false;
            }

            if (!activeContainer.gameObject.activeInHierarchy)
            {
                LogStateChange("active_container_inactive", "ActiveContainer: FOUND but NOT active");
                _rewardsContainer = null;
                return false;
            }

            var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
            if (rewardsContainer == null)
            {
                LogStateChange("no_rewards_container", "RewardsCONTAINER: NOT FOUND");
                _rewardsContainer = null;
                return false;
            }

            if (!rewardsContainer.gameObject.activeInHierarchy)
            {
                LogStateChange("rewards_inactive", "RewardsCONTAINER: FOUND but NOT active");
                _rewardsContainer = null;
                return false;
            }

            // Verify we have actual card prefabs or deck prefabs in the rewards container
            int cardCount = 0;
            int deckCount = 0;
            foreach (Transform child in rewardsContainer)
            {
                if (child.name.Contains("NPERewardPrefab_IndividualCard"))
                    cardCount++;
                else if (child.gameObject.activeInHierarchy)
                {
                    var hitbox = FindChildByName(child, "Hitbox_LidOpen");
                    if (hitbox != null)
                        deckCount++;
                }
            }

            if (cardCount == 0 && deckCount == 0)
            {
                LogStateChange("no_cards", "NO card or deck prefabs found - screen not ready");
                _rewardsContainer = null;
                return false;
            }

            _isDeckReward = cardCount == 0 && deckCount > 0;

            // Only log full details on state change to "success"
            string successState = _isDeckReward ? "success_deck" : "success";
            if (_lastDetectState != successState && Log.LogNavigation)
            {
                Log.Nav(NavigatorId, $"=== NPE REWARD SCREEN DETECTION: SUCCESS ({(_isDeckReward ? "DECK" : "CARD")} mode) ===");
                Log.Nav(NavigatorId, $"  Path: {GetPath(npeContainer.transform)}");
                Log.Nav(NavigatorId, $"  Card prefabs found: {cardCount}, Deck prefabs found: {deckCount}");

                Log.Nav(NavigatorId, $"  RewardsCONTAINER children ({rewardsContainer.childCount}):");
                foreach (Transform child in rewardsContainer)
                {
                    string components = string.Join(", ", child.GetComponents<Component>().Select(c => c?.GetType().Name ?? "null"));
                    Log.Nav(NavigatorId, $"    - {child.name} (active={child.gameObject.activeInHierarchy}) [{components}]");
                }

                // Check for NPEContentControllerRewards (needed for button activation)
                bool foundController = false;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "NPEContentControllerRewards")
                    {
                        foundController = true;
                        Log.Nav(NavigatorId, $"  NPEContentControllerRewards: FOUND on {mb.gameObject.name}");
                        break;
                    }
                }
                if (!foundController)
                {
                    Log.Nav(NavigatorId, $"  NPEContentControllerRewards: NOT FOUND (button activation may fail!)");
                }
            }

            _lastDetectState = successState;
            _rewardsContainer = npeContainer;
            return true;
        }

        private void LogStateChange(string newState, string message = null)
        {
            if (_lastDetectState == newState) return;
            _lastDetectState = newState;
            if (message != null)
                Log.Nav(NavigatorId, message);
        }

        protected override void DiscoverElements()
        {
            Log.Nav(NavigatorId, $"=== DISCOVERING ELEMENTS ({(_isDeckReward ? "DECK" : "CARD")} mode) ===");
            _totalCards = 0;
            var addedObjects = new HashSet<GameObject>();

            // Find card or deck entries
            if (_isDeckReward)
                FindDeckEntries(addedObjects);
            else
                FindCardEntries(addedObjects);

            // Find take reward button
            FindTakeRewardButton(addedObjects);

            // Track deck box count for change detection in ValidateElements
            if (_isDeckReward)
                _discoveredDeckBoxCount = CountDeckBoxes();

            Log.Nav(NavigatorId, $"=== DISCOVERY COMPLETE: {_elements.Count} elements ===");
            for (int i = 0; i < _elements.Count; i++)
            {
                var el = _elements[i];
                Log.Nav(NavigatorId, $"  [{i}] {el.Label} -> {el.GameObject?.name ?? "NULL"} (ID:{el.GameObject?.GetInstanceID()})");
            }
        }

        private void FindCardEntries(HashSet<GameObject> addedObjects)
        {
            if (_rewardsContainer == null)
            {
                Log.Nav(NavigatorId, $"FindCardEntries: _rewardsContainer is NULL");
                return;
            }

            var cardEntries = new List<(GameObject obj, float sortOrder, string name, string typeLine)>();

            // Find NPE reward card prefabs in RewardsCONTAINER
            var activeContainer = _rewardsContainer.transform.Find("ActiveContainer");
            if (activeContainer == null)
            {
                Log.Nav(NavigatorId, $"FindCardEntries: ActiveContainer not found");
                return;
            }

            var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
            if (rewardsContainer == null)
            {
                Log.Nav(NavigatorId, $"FindCardEntries: RewardsCONTAINER not found");
                return;
            }

            Log.Nav(NavigatorId, $"Scanning RewardsCONTAINER for cards...");

            foreach (Transform child in rewardsContainer)
            {
                Log.Nav(NavigatorId, $"  Checking: {child.name}");

                if (!child.gameObject.activeInHierarchy)
                {
                    Log.Nav(NavigatorId, $"    SKIPPED: not active");
                    continue;
                }

                if (!child.name.Contains("NPERewardPrefab_IndividualCard"))
                {
                    Log.Nav(NavigatorId, $"    SKIPPED: name doesn't contain NPERewardPrefab_IndividualCard");
                    continue;
                }

                // Get the card anchor or the prefab itself for navigation
                var cardAnchor = child.Find("CardAnchor");
                GameObject cardObj = cardAnchor?.gameObject ?? child.gameObject;

                if (Log.LogNavigation)
                {
                    Log.Nav(NavigatorId, $"    MATCHED as card prefab");
                    foreach (Transform cardChild in child)
                        Log.Nav(NavigatorId, $"      - {cardChild.name} (active={cardChild.gameObject.activeInHierarchy})");
                    Log.Nav(NavigatorId, $"    Navigation target: {cardObj.name} (used CardAnchor={cardAnchor != null})");
                    Log.Nav(NavigatorId, $"    Target path: {GetPath(cardObj.transform)}");
                    var components = cardObj.GetComponents<Component>();
                    Log.Nav(NavigatorId, $"    Target components ({components.Length}):");
                    foreach (var comp in components)
                        Log.Nav(NavigatorId, $"      - {comp?.GetType().Name ?? "null"}");
                }

                if (addedObjects.Contains(cardObj))
                {
                    Log.Nav(NavigatorId, $"    SKIPPED: already added");
                    continue;
                }

                var cardInfo = CardDetector.ExtractCardInfo(child.gameObject);
                if (Log.LogNavigation)
                {
                    Log.Nav(NavigatorId, $"    CardInfo: Name={cardInfo.Name ?? "null"}, Valid={cardInfo.IsValid}, Type={cardInfo.TypeLine ?? "null"}");
                }

                string cardName = cardInfo.IsValid ? cardInfo.Name : Strings.NPE_UnknownCard;
                string typeLine = cardInfo.IsValid ? cardInfo.TypeLine : "";

                // Sort by X position (left to right)
                float sortOrder = child.position.x;
                cardEntries.Add((cardObj, sortOrder, cardName, typeLine));
                addedObjects.Add(cardObj);

                Log.Nav(NavigatorId, $"    ADDED: {cardName} at x={sortOrder:F2}");
            }

            // Sort cards by position (left to right)
            cardEntries = cardEntries.OrderBy(x => x.sortOrder).ToList();

            _totalCards = cardEntries.Count;
            Log.Nav(NavigatorId, $"Total cards found: {_totalCards}");

            // Add cards to navigation
            int cardNum = 1;
            foreach (var (cardObj, sortX, cardName, typeLine) in cardEntries)
            {
                string label = _totalCards > 1
                    ? Strings.NPE_UnlockedCardNumber(cardNum, cardName)
                    : cardName;

                if (!string.IsNullOrEmpty(typeLine))
                {
                    label += $", {typeLine}";
                }

                Log.Nav(NavigatorId, $"Adding element: '{label}' -> {cardObj.name}");
                AddElement(cardObj, label);
                cardNum++;
            }
        }

        private void FindDeckEntries(HashSet<GameObject> addedObjects)
        {
            if (_rewardsContainer == null)
            {
                Log.Nav(NavigatorId, $"FindDeckEntries: _rewardsContainer is NULL");
                return;
            }

            var activeContainer = _rewardsContainer.transform.Find("ActiveContainer");
            if (activeContainer == null)
            {
                Log.Nav(NavigatorId, $"FindDeckEntries: ActiveContainer not found");
                return;
            }

            var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
            if (rewardsContainer == null)
            {
                Log.Nav(NavigatorId, $"FindDeckEntries: RewardsCONTAINER not found");
                return;
            }

            Log.Nav(NavigatorId, $"Scanning RewardsCONTAINER for deck prefabs...");

            var deckEntries = new List<(GameObject hitbox, float sortOrder, string name)>();

            foreach (Transform child in rewardsContainer)
            {
                Log.Nav(NavigatorId, $"  Checking: {child.name} (active={child.gameObject.activeInHierarchy})");

                if (!child.gameObject.activeInHierarchy)
                {
                    Log.Nav(NavigatorId, $"    SKIPPED: not active");
                    continue;
                }

                // Look for Hitbox_LidOpen descendant (deck box click target)
                var hitboxObj = FindChildByName(child, "Hitbox_LidOpen");
                if (hitboxObj == null)
                {
                    Log.Nav(NavigatorId, $"    SKIPPED: no Hitbox_LidOpen found");
                    continue;
                }

                if (Log.LogNavigation)
                {
                    Log.Nav(NavigatorId, $"    MATCHED as deck prefab (Hitbox_LidOpen found)");
                    foreach (Transform deckChild in child)
                        Log.Nav(NavigatorId, $"      - {deckChild.name} (active={deckChild.gameObject.activeInHierarchy})");
                }

                if (addedObjects.Contains(hitboxObj))
                {
                    Log.Nav(NavigatorId, $"    SKIPPED: already added");
                    continue;
                }

                // Extract deck name: only from opened boxes (closed ones have placeholder text like "Enter deck name...")
                string deckName;
                bool isBoxOpen = !hitboxObj.activeInHierarchy;
                if (isBoxOpen)
                {
                    deckName = UITextExtractor.GetText(child.gameObject);
                    if (string.IsNullOrEmpty(deckName))
                        deckName = Strings.DeckNumber(deckEntries.Count + 1);
                }
                else
                {
                    deckName = Strings.DeckNumber(deckEntries.Count + 1);
                }

                float sortOrder = child.position.x;
                deckEntries.Add((hitboxObj, sortOrder, deckName));
                addedObjects.Add(hitboxObj);

                Log.Nav(NavigatorId, $"    ADDED: {deckName} at x={sortOrder:F2}");
            }

            // Sort decks by position (left to right)
            deckEntries = deckEntries.OrderBy(x => x.sortOrder).ToList();

            _totalCards = deckEntries.Count;
            Log.Nav(NavigatorId, $"Total decks found: {_totalCards}");

            // Add decks to navigation (re-number after sorting)
            int deckNum = 1;
            foreach (var (hitbox, sortX, name) in deckEntries)
            {
                // If the name was a fallback "Deck N", re-generate with sorted index
                string label = name;
                bool isFallbackName = false;
                for (int i = 1; i <= deckEntries.Count; i++)
                {
                    if (name == Strings.DeckNumber(i)) { isFallbackName = true; break; }
                }
                if (isFallbackName)
                {
                    label = Strings.DeckNumber(deckNum);
                }

                Log.Nav(NavigatorId, $"Adding element: '{label}' -> {hitbox.name}");
                AddElement(hitbox, label);
                deckNum++;
            }
        }

        private void FindTakeRewardButton(HashSet<GameObject> addedObjects)
        {
            if (_rewardsContainer == null)
            {
                Log.Nav(NavigatorId, $"FindTakeRewardButton: _rewardsContainer is NULL");
                return;
            }

            Log.Nav(NavigatorId, $"Searching for NullClaimButton...");

            // Find NullClaimButton - the "take reward" button
            GameObject foundButton = null;
            foreach (var transform in _rewardsContainer.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null) continue;

                if (transform.name == "NullClaimButton")
                {
                    if (!transform.gameObject.activeInHierarchy)
                    {
                        Log.Nav(NavigatorId, $"  NullClaimButton: SKIPPED (not active)");
                        continue;
                    }

                    if (addedObjects.Contains(transform.gameObject))
                    {
                        Log.Nav(NavigatorId, $"  NullClaimButton: SKIPPED (already added)");
                        continue;
                    }

                    if (Log.LogNavigation)
                    {
                        Log.Nav(NavigatorId, $"  Found NullClaimButton: {GetPath(transform)}");
                        foreach (var comp in transform.GetComponents<Component>())
                            Log.Nav(NavigatorId, $"    - {comp?.GetType().Name ?? "null"}");
                        LogCustomButtonDetails(transform);
                    }

                    foundButton = transform.gameObject;
                    break;
                }
            }

            if (foundButton != null)
            {
                string buttonLabel = _isDeckReward
                    ? BuildLabel(Strings.Continue, Strings.RoleButton, UIElementClassifier.ElementRole.Button)
                    : BuildLabel(Strings.NPE_TakeReward, Strings.RoleButton, UIElementClassifier.ElementRole.Button);
                AddElement(foundButton, buttonLabel);
                addedObjects.Add(foundButton);
                Log.Nav(NavigatorId, $"{buttonLabel} ADDED");
            }
            else
            {
                Log.Nav(NavigatorId, $"NullClaimButton NOT FOUND in hierarchy!");
            }
        }

        /// <summary>
        /// Logs interesting CustomButton fields on a transform (debug only).
        /// Caller must gate with Log.LogNavigation.
        /// </summary>
        private void LogCustomButtonDetails(Transform buttonTransform)
        {
            if (buttonTransform == null) return;

            MonoBehaviour customButton = null;
            foreach (var mb in buttonTransform.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "CustomButton")
                {
                    customButton = mb;
                    break;
                }
            }
            if (customButton == null) return;

            foreach (var field in customButton.GetType().GetFields(AllInstanceFlags))
            {
                if (!field.Name.Contains("click") && !field.Name.Contains("Click") &&
                    !field.Name.Contains("event") && !field.Name.Contains("Event") &&
                    !field.Name.Contains("action") && !field.Name.Contains("Action") &&
                    !field.Name.Contains("interactable") && !field.Name.Contains("Interactable"))
                    continue;

                try
                {
                    var val = field.GetValue(customButton);
                    string valStr = val == null ? "null"
                        : val is UnityEngine.Events.UnityEventBase ue ? $"<UnityEvent, listeners={ue.GetPersistentEventCount()}>"
                        : val is string || val is bool || val is int || val is float || val is System.Enum ? val.ToString()
                        : $"<{val.GetType().Name}>";
                    Log.Nav(NavigatorId, $"    CustomButton.{field.Name}: {valStr}");
                }
                catch { /* Some fields may throw when read via reflection */ }
            }
        }

        public override string GetTutorialHint() =>
            LocaleManager.Instance.Get(_isDeckReward ? "NPERewardDeckHint" : "NPERewardCardHint");

        protected override string GetActivationAnnouncement()
        {
            string hintKey = _isDeckReward ? "NPERewardDeckHint" : "NPERewardCardHint";
            return Strings.WithHint(ScreenName, hintKey);
        }

        protected override void HandleInput()
        {
            // Handle custom input first (F1 help, etc.)
            if (HandleCustomInput()) return;

            // Left/Right arrows for navigation between cards (hold-to-repeat)
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => {
                Log.Nav(NavigatorId, $"Input: Left - MovePrevious (current={_currentIndex}, total={_elements.Count})");
                MovePrevious(); LogCurrentState(); return true;
            })) return;

            if (_holdRepeater.Check(KeyCode.RightArrow, () => {
                Log.Nav(NavigatorId, $"Input: Right - MoveNext (current={_currentIndex}, total={_elements.Count})");
                MoveNext(); LogCurrentState(); return true;
            })) return;

            // Home/End for quick jump to first/last
            if (Input.GetKeyDown(KeyCode.Home))
            {
                Log.Nav(NavigatorId, $"Input: Home - MoveFirst");
                MoveFirst();
                LogCurrentState();
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                Log.Nav(NavigatorId, $"Input: End - MoveLast");
                MoveLast();
                LogCurrentState();
                return;
            }

            // Tab/Shift+Tab also navigates (consistent with pack opening)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shiftTab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                Log.Nav(NavigatorId, $"Input: Tab (shift={shiftTab})");
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
                Log.Nav(NavigatorId, $"Input: Enter - ActivateCurrentElement");
                LogCurrentState();
                if (IsClaimButton(_currentIndex))
                {
                    ActivateCurrentElement();
                    Log.Nav(NavigatorId, $"Claim button activated - deactivating navigator");
                    Deactivate();
                }
                else
                {
                    ActivateCurrentElement();
                }
                return;
            }

            // Backspace dismisses (activates take reward / continue button)
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                Log.Nav(NavigatorId, $"Input: Backspace - Finding dismiss button");
                // Find and activate the take reward / continue button
                for (int i = 0; i < _elements.Count; i++)
                {
                    var element = _elements[i];
                    if (IsClaimButton(i))
                    {
                        Log.Nav(NavigatorId, $"  Activating: {element.Label} -> {GetSafeName(element.GameObject)}");
                        UIActivator.Activate(element.GameObject);
                        Log.Nav(NavigatorId, $"Claim button activated via Backspace - deactivating navigator");
                        Deactivate();
                        return;
                    }
                }
                Log.Nav(NavigatorId, $"  Take reward button not found in elements!");
                return;
            }
        }

        private bool IsClaimButton(int index)
        {
            if (index < 0 || index >= _elements.Count) return false;
            var go = _elements[index].GameObject;
            return go != null && go.name == "NullClaimButton";
        }

        /// <summary>
        /// Safe name access that handles Unity-destroyed objects (native side gone but C# reference exists).
        /// The ?. operator only checks C# null, not Unity's overloaded == null for destroyed objects.
        /// </summary>
        private static string GetSafeName(GameObject go)
        {
            // Unity overloads == to return true for destroyed objects
            if (go == null) return "NULL";
            try { return go.name; }
            catch { return "DESTROYED"; }
        }

        private void LogCurrentState()
        {
            if (_currentIndex >= 0 && _currentIndex < _elements.Count)
            {
                var current = _elements[_currentIndex];
                Log.Nav(NavigatorId, $"  Current: [{_currentIndex}] {current.Label}");
                Log.Nav(NavigatorId, $"    GameObject: {GetSafeName(current.GameObject)} (active={current.GameObject?.activeInHierarchy})");

                // Check if this is a card and if CardInfoNavigator should be prepared
                if (current.GameObject != null)
                {
                    bool isCard = CardDetector.IsCard(current.GameObject);
                    Log.Nav(NavigatorId, $"    IsCard: {isCard}");

                    var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
                    if (cardNav != null)
                    {
                        Log.Nav(NavigatorId, $"    CardInfoNavigator.IsActive: {cardNav.IsActive}");
                        Log.Nav(NavigatorId, $"    CardInfoNavigator.CurrentCard: {GetSafeName(cardNav.CurrentCard)}");
                    }
                }
            }
            else
            {
                Log.Nav(NavigatorId, $"  Current: INVALID INDEX {_currentIndex} (count={_elements.Count})");
            }
        }

        protected override bool ValidateElements()
        {
            // Check if rewards container is still active
            if (_rewardsContainer == null || !_rewardsContainer.activeInHierarchy)
            {
                Log.Nav(NavigatorId, $"ValidateElements: Rewards container no longer active");
                return false;
            }

            // Check if any element GameObjects are still alive and active
            // After claiming, card objects get destroyed and button gets deactivated
            bool anyAlive = false;
            for (int i = 0; i < _elements.Count; i++)
            {
                var go = _elements[i].GameObject;
                if (go != null && go.activeInHierarchy)
                {
                    anyAlive = true;
                    break;
                }
            }
            if (!anyAlive)
            {
                Log.Nav(NavigatorId, $"ValidateElements: No active elements remaining (reward dismissed)");
                return false;
            }

            // For deck rewards, force re-detection when deck box count changes
            // (handles animation where boxes appear one-by-one, and boxes opening after click)
            if (_isDeckReward)
            {
                int currentCount = CountDeckBoxes();
                if (currentCount != _discoveredDeckBoxCount)
                {
                    Log.Nav(NavigatorId, $"ValidateElements: Deck box count changed {_discoveredDeckBoxCount} -> {currentCount}, forcing re-detection");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Counts active deck box children in RewardsCONTAINER that have a Hitbox_LidOpen descendant.
        /// Used to detect when new boxes appear (animation) or boxes open (user click).
        /// </summary>
        private int CountDeckBoxes()
        {
            if (_rewardsContainer == null) return 0;
            var activeContainer = _rewardsContainer.transform.Find("ActiveContainer");
            if (activeContainer == null) return 0;
            var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
            if (rewardsContainer == null) return 0;

            int count = 0;
            foreach (Transform child in rewardsContainer)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.name.Contains("NPERewardPrefab_IndividualCard")) continue;
                if (FindChildByName(child, "Hitbox_LidOpen") != null)
                    count++;
            }
            return count;
        }

        public override void OnSceneChanged(string sceneName)
        {
            Log.Nav(NavigatorId, $"OnSceneChanged: {sceneName}");
            if (_isActive)
            {
                Deactivate();
            }
            _rewardsContainer = null;
            _isDeckReward = false;
            _lastDetectState = null;
            _discoveredDeckBoxCount = 0;
        }
    }
}
