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
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the booster pack card list that appears after opening a pack.
    /// Uses the controller's _cardsToOpen field as the authoritative source for detection
    /// and GrpId-based lookup for card names (data-driven, immune to UI text timing).
    /// Does NOT skip the pack animation — skipping corrupted the booster carousel state.
    /// </summary>
    public class BoosterOpenNavigator : BaseNavigator
    {
        private Component _controller;
        private GameObject _chamberRoot;
        private bool _isSealedMode;
        private Component _sealedAnimation;
        private GameObject _revealAllButton;
        private int _totalCards;
        private int _expectedCardCount;

        // Cached reflection info (types don't change between scenes)
        private static FieldInfo _cardsToOpenField;
        private static PropertyInfo _hiddenProp;
        private static FieldInfo _onScreenHoldersField;
        private static PropertyInfo _cardViewsProp;
        private static FieldInfo _autoRevealField;  // CardDataAndRevealStatus.AutoReveal

        // Data-driven card info: read GrpId from _cardsToOpen entries
        private static FieldInfo _cardDataField;    // CardDataAndRevealStatus.CardData
        private static PropertyInfo _grpIdProp;     // CardData.GrpId
        private static PropertyInfo _rarityProp;    // CardData.Rarity
        private static PropertyInfo _revealedProp;  // CardDataAndRevealStatus.Revealed
        private Dictionary<GameObject, int> _cardDataIndices = new Dictionary<GameObject, int>();
        private List<int> _elementDataIndex = new List<int>(); // parallel to _elements: _cardsToOpen index, -1 for non-card

        // Pack music: game's opening animation calls ConditionalHoverOff() which stops
        // the pack-specific music. We restore it by calling AudioManager.SetRTPCValue directly.
        private static MethodInfo _setRTPCMethod;
        private static MethodInfo _playAudioStringMethod; // AudioManager.PlayAudio(string, GameObject)
        private static FieldInfo _chamberSetCodeField;
        private bool _packMusicRestored;

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
            // Find the BoosterOpenToScrollListController anywhere in the active scene.
            // Both regular booster chamber (BoosterChamber_v2_Desktop_16x9) and sealed
            // event opening (SealedBoosterOpen_v2_Desktop_16x9) host the same scroll
            // list controller, so we detect by component instead of GameObject name.
            Component controller = null;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.BoosterOpenToScrollListController)
                {
                    controller = mb;
                    break;
                }
            }
            if (controller == null)
            {
                _controller = null;
                _chamberRoot = null;
                _sealedAnimation = null;
                _isSealedMode = false;
                return false;
            }

            // Check if _cardsToOpen is populated (null = no pack opened, empty = cleared)
            var cards = GetCardsToOpen(controller);
            if (cards == null || cards.Count == 0)
            {
                _controller = null;
                _chamberRoot = null;
                _sealedAnimation = null;
                _isSealedMode = false;
                return false;
            }

            // Walk up to find the chamber root (parent containing "BoosterChamber" or "SealedBoosterOpen")
            GameObject chamberRoot = FindChamberRoot(controller);
            bool isSealed = chamberRoot != null && chamberRoot.name.Contains("SealedBoosterOpen");

            // Sealed flow populates _cardsToOpen during Init() — before the user clicks
            // "Öffnen". Activating now would steal focus from the Open button. Wait until
            // the Open button is hidden (set inactive on click in OpenButton_OnClick).
            Component sealedAnimation = null;
            if (isSealed)
            {
                sealedAnimation = FindSealedAnimation(chamberRoot);
                if (!IsSealedOpenButtonClicked(sealedAnimation))
                {
                    _controller = null;
                    return false;
                }
            }

            _controller = controller;
            _chamberRoot = chamberRoot;
            _sealedAnimation = sealedAnimation;
            _isSealedMode = isSealed;
            _expectedCardCount = cards.Count;

            // Clear AutoReveal immediately — before the animation starts spawning cards.
            // SpawnCard auto-reveals cards with AutoReveal=true and sets Revealed=true,
            // so we must clear it before any SpawnCard call happens.
            ClearAutoReveal();

            Log.Msg("{NavigatorId}", $"Pack opened with {cards.Count} cards (sealed={isSealed})");
            return true;
        }

        /// <summary>
        /// Walk up from the scroll list controller to find the chamber root GameObject.
        /// The root has name like "ContentController - BoosterChamber_v2_Desktop_16x9(Clone)"
        /// or "ContentController - SealedBoosterOpen_v2_Desktop_16x9(Clone)".
        /// </summary>
        private GameObject FindChamberRoot(Component controller)
        {
            Transform current = (controller as MonoBehaviour)?.transform;
            while (current != null)
            {
                if (current.name.Contains("BoosterChamber") || current.name.Contains("SealedBoosterOpen"))
                    return current.gameObject;
                current = current.parent;
            }
            // Fallback: return the controller's GameObject (better than null)
            return (controller as MonoBehaviour)?.gameObject;
        }

        /// <summary>
        /// Find the SealedBoosterOpenAnimation component within the chamber root.
        /// </summary>
        private Component FindSealedAnimation(GameObject chamberRoot)
        {
            if (chamberRoot == null) return null;
            foreach (var mb in chamberRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == T.SealedBoosterOpenAnimation)
                    return mb;
            }
            return null;
        }

        /// <summary>
        /// Check if the user has clicked the sealed Open button. The button is hidden
        /// (gameObject.SetActive(false)) inside OpenButton_OnClick, so we use its active
        /// state as the signal that the opening animation has started.
        /// </summary>
        private bool IsSealedOpenButtonClicked(Component sealedAnimation)
        {
            if (sealedAnimation == null) return false;
            var openBtnField = sealedAnimation.GetType().GetField("_openButton", PrivateInstance);
            var openBtn = openBtnField?.GetValue(sealedAnimation) as Component;
            if (openBtn == null) return true; // Field gone — assume animation is past Open
            return !openBtn.gameObject.activeSelf;
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
                    Log.Msg("{NavigatorId}", $"_cardsToOpen field not found");
                    return null;
                }
            }
            return _cardsToOpenField.GetValue(controller) as IList;
        }

        protected override void DiscoverElements()
        {
            _totalCards = 0;
            _elementDataIndex.Clear();
            _cardDataIndices.Clear();
            var addedObjects = new HashSet<GameObject>();

            // Find RevealAll button first (hidden from nav, no element added)
            FindRevealAllButton(addedObjects);

            // Find card entries from _cardsToOpen data (all cards, not just viewport)
            FindCardEntries(addedObjects);

            // Find dismiss/continue button
            int preDismiss = _elements.Count;
            FindDismissButton(addedObjects);
            for (int i = preDismiss; i < _elements.Count; i++)
                _elementDataIndex.Add(-1);
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
                    Log.Msg("{NavigatorId}", $"Found RevealAll button (hidden from nav): {name}");
                    return;
                }
            }
        }

        private void FindCardEntries(HashSet<GameObject> addedObjects)
        {
            var cards = GetCardsToOpen(_controller);
            if (cards == null || cards.Count == 0) return;

            // Get on-screen card holders for activation (viewport-limited, ~12 of 24)
            var onScreenDict = GetOnScreenHolders();

            Log.Msg("{NavigatorId}", $"Building card list from {cards.Count} data entries ({onScreenDict?.Count ?? 0} on-screen)");

            // Sort by descending index (common cards first in navigation, matches scroll layout)
            var sortedIndices = Enumerable.Range(0, cards.Count).OrderByDescending(i => i).ToList();

            int cardNum = 1;
            foreach (int dataIndex in sortedIndices)
            {
                var entry = cards[dataIndex];
                if (entry == null) continue;

                bool isRevealed = IsEntryRevealed(entry);

                // Get card info from GrpId (data-driven, works for all cards regardless of viewport)
                var cardInfo = GetCardInfoFromData(dataIndex) ?? default(CardInfo);

                // Check for on-screen holder (has visual card with flip sound)
                Component holder = null;
                onScreenDict?.TryGetValue(dataIndex, out holder);
                GameObject cardObj = (holder != null) ? GetFirstCardView(holder) ?? (holder as MonoBehaviour)?.gameObject : null;

                // Build label
                string label;
                if (!isRevealed)
                {
                    label = Strings.HiddenCard;
                }
                else
                {
                    string displayName = null;
                    if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.Name))
                        displayName = cardInfo.Name;

                    // For on-screen cards, fall back to UI text extraction (vault progress, etc.)
                    if (string.IsNullOrEmpty(displayName) && cardObj != null)
                        displayName = ExtractCardName(cardObj);

                    if (string.IsNullOrEmpty(displayName))
                        displayName = "Unknown card";

                    label = displayName;
                    if (cardInfo.IsValid && !string.IsNullOrEmpty(cardInfo.TypeLine))
                        label += $", {cardInfo.TypeLine}";
                }

                // Add to navigation: real GO for on-screen cards, TextBlock for off-screen
                int preCount = _elements.Count;
                if (cardObj != null && !addedObjects.Contains(cardObj))
                {
                    AddElement(cardObj, label);
                    addedObjects.Add(cardObj);
                    _cardDataIndices[cardObj] = dataIndex;
                }
                else if (cardObj == null)
                {
                    AddTextBlock(label);
                }

                if (_elements.Count > preCount)
                    _elementDataIndex.Add(dataIndex);

                cardNum++;
            }

            _totalCards = cardNum - 1;
            Log.Msg("{NavigatorId}", $"Total: {_totalCards} cards ({_elements.Count} elements, {onScreenDict?.Count ?? 0} with holders)");
        }

        /// <summary>
        /// Read on-screen card holders from the controller's _onScreenboosterCardHoldersWithIndex dictionary.
        /// Returns a Dictionary mapping _cardsToOpen index → BoosterCardHolder component.
        /// Only contains cards currently visible in the viewport (~12 of 24 for Open All packs).
        /// </summary>
        private Dictionary<int, Component> GetOnScreenHolders()
        {
            if (_controller == null) return null;

            if (_onScreenHoldersField == null)
                _onScreenHoldersField = _controller.GetType().GetField("_onScreenboosterCardHoldersWithIndex", PrivateInstance);
            if (_onScreenHoldersField == null) return null;

            var holdersObj = _onScreenHoldersField.GetValue(_controller);
            var dict = holdersObj as System.Collections.IDictionary;
            if (dict == null || dict.Count == 0) return null;

            var result = new Dictionary<int, Component>();
            foreach (DictionaryEntry entry in dict)
            {
                var holder = entry.Value as Component;
                if (holder != null)
                    result[(int)entry.Key] = holder;
            }
            return result;
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

                Log.Msg("{NavigatorId}", $"Detected vault progress: {label} (tags: {string.Join(", ", vaultTags)})");
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

        protected override bool IsCurrentCardHidden(GameObject cardElement)
        {
            // Prefer data-driven check (CardDataAndRevealStatus.Revealed) over the visual
            // BoosterCardHolder.Hidden property. When the first card is revealed via data
            // (RevealCardByData) before SpawnCard runs, the holder's Hidden flag can stay
            // stale while the card spawns face-up. The Revealed flag is the source of truth.
            if (IsValidIndex && _currentIndex < _elementDataIndex.Count)
            {
                int dataIndex = _elementDataIndex[_currentIndex];
                if (dataIndex >= 0)
                {
                    var cards = GetCardsToOpen(_controller);
                    if (cards != null && dataIndex < cards.Count)
                        return !IsEntryRevealed(cards[dataIndex]);
                }
                return false;
            }
            return cardElement != null && IsCardHidden(cardElement);
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
                        Log.Msg("{NavigatorId}", $"Found Dismiss button (hidden from nav): {name}");
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
                    Log.Msg("{NavigatorId}", $"Found dismiss button: {name} -> {label}");
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
                    current.name.Contains("SealedBoosterOpen") ||
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

        /// <summary>
        /// Provide card info for off-screen cards (TextBlocks) via GrpId lookup.
        /// Base UpdateCardNavigation deactivates CardInfoNavigator for null-GO elements,
        /// but off-screen booster cards have full data available via GrpId.
        /// For unrevealed off-screen cards, expose only a single "Hidden card" block so
        /// Arrow Up/Down doesn't leak card details before the player reveals them.
        /// </summary>
        private void UpdateCardInfoForOffScreenCard()
        {
            if (!IsValidIndex) return;
            var elem = _elements[_currentIndex];
            if (elem.GameObject != null) return; // On-screen card handled by base UpdateCardNavigation

            int dataIndex = (_currentIndex < _elementDataIndex.Count) ? _elementDataIndex[_currentIndex] : -1;
            if (dataIndex < 0) return; // Not a card element (e.g., button)

            var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNavigator == null) return;

            var cards = GetCardsToOpen(_controller);
            bool revealed = cards != null && dataIndex < cards.Count && IsEntryRevealed(cards[dataIndex]);

            if (!revealed)
            {
                var hiddenBlocks = new List<CardInfoBlock>
                {
                    new CardInfoBlock("", Strings.HiddenCard, isVerbose: false)
                };
                cardNavigator.PrepareForCardInfo(hiddenBlocks, Strings.HiddenCard);
                return;
            }

            var cardInfo = GetCardInfoFromData(dataIndex);
            if (!cardInfo.HasValue || !cardInfo.Value.IsValid) return;

            var blocks = CardDetector.BuildInfoBlocks(cardInfo.Value);
            if (blocks.Count > 0)
            {
                cardNavigator.PrepareForCardInfo(blocks, cardInfo.Value.Name ?? "Card");
            }
        }

        protected override void Move(int direction)
        {
            base.Move(direction);
            UpdateCardInfoForOffScreenCard();
        }

        protected override void MoveFirst()
        {
            base.MoveFirst();
            UpdateCardInfoForOffScreenCard();
        }

        protected override void MoveLast()
        {
            base.MoveLast();
            UpdateCardInfoForOffScreenCard();
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
                    return;
                }

                // Off-screen booster card (TextBlock, no GameObject) — use GrpId from _cardsToOpen.
                // Only allow for revealed cards so hidden cards don't leak details.
                if (extInfoNav != null && IsValidIndex)
                {
                    int dataIndex = (_currentIndex < _elementDataIndex.Count) ? _elementDataIndex[_currentIndex] : -1;
                    if (dataIndex >= 0)
                    {
                        var cards = GetCardsToOpen(_controller);
                        if (cards != null && dataIndex < cards.Count && IsEntryRevealed(cards[dataIndex]))
                        {
                            uint grpId = GetGrpIdFromEntry(cards[dataIndex]);
                            if (grpId != 0)
                            {
                                extInfoNav.Open(grpId);
                                return;
                            }
                        }
                    }
                }

                _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
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
                Log.Msg("{NavigatorId}", $"Enter pressed - index={_currentIndex}, count={_elements.Count}, valid={IsValidIndex}");
                if (IsValidIndex)
                {
                    var elem = _elements[_currentIndex];
                    Log.Msg("{NavigatorId}", $"Current element: {elem.GameObject?.name ?? "null"} (Label: {elem.Label})");

                    // Special handling for Skip to End / Reveal All buttons - trigger immediate rescan
                    if (elem.GameObject != null &&
                        (elem.GameObject.name.Contains("SkipToEnd") || elem.GameObject.name.Contains("RevealAll")))
                    {
                        ActivateCurrentElement();
                        // Trigger rescan to pick up all revealed cards
                        _rescanDone = false;
                        _rescanFrameCounter = 0;
                        Log.Msg("{NavigatorId}", $"Reveal/Skip activated, will rescan");
                        return;
                    }
                    // Check if this is a close/dismiss button
                    // Use ClosePackProperly to ensure correct game state cleanup
                    if (elem.GameObject != null &&
                        (elem.GameObject.name.Contains("ModalFade") ||
                         elem.GameObject.name.Contains("Dismiss_MainButton")))
                    {
                        Log.Msg("{NavigatorId}", $"Enter on close button: {elem.GameObject.name}");
                        ClosePackProperly();
                        TriggerCloseRescan();
                        return;
                    }
                    // Hidden card with on-screen holder: activate BoosterCardHolder
                    // to trigger OnClick() -> PlayFlipSound() + RevealCard()
                    if (elem.GameObject != null && elem.Label == Strings.HiddenCard)
                    {
                        var holder = FindBoosterCardHolder(elem.GameObject);
                        if (holder != null)
                        {
                            Log.Msg("{NavigatorId}", $"Revealing hidden card via BoosterCardHolder");
                            UIActivator.Activate(holder);
                            _rescanDone = false;
                            _rescanFrameCounter = 0;
                            return;
                        }
                    }
                    // Hidden card off-screen (TextBlock, no GO): reveal via data
                    if (elem.GameObject == null && elem.Label == Strings.HiddenCard)
                    {
                        int dataIdx = (_currentIndex < _elementDataIndex.Count) ? _elementDataIndex[_currentIndex] : -1;
                        if (dataIdx >= 0)
                        {
                            RevealCardByData(dataIdx);
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
                Log.Msg("{NavigatorId}", $"Backspace pressed - attempting close");
                ClosePackProperly();
                TriggerCloseRescan();
                return;
            }
        }

        /// <summary>
        /// Close the pack properly by calling BoosterChamberController.DismissCards() via reflection.
        /// This is the same method the game's own dismiss button invokes (through the scroll list
        /// controller delegate chain). It resets ThereIsABoosterOpened, triggers the dismiss
        /// animation, refreshes the carousel, and cleans up audio — the full proper close sequence.
        /// </summary>
        private void ClosePackProperly()
        {
            // Stop pack music by sending PointerExit to the pack hitbox
            StopPackMusic();

            // Force-reveal all unrevealed cards and update the game's reveal tracking.
            // ClearAutoReveal prevents the game from auto-revealing most cards, and the
            // viewport only shows ~12 of 24 cards — so the game thinks cards are unrevealed.
            // This ensures CardsRevealed=true on the animator before dismiss, preventing
            // the booster carousel from getting stuck in an invalid state.
            ForceRevealAllCards();

            // Primary: Call DismissCards on BoosterChamberController (the canonical close path)
            if (TryCloseChamberController())
            {
                Log.Msg("{NavigatorId}", $"Closed via BoosterChamberController.DismissCards");
                return;
            }

            // Fallback: Try scroll list controller's DismissCards or other close methods
            if (TryClosePackContents())
            {
                return;
            }

            // Last resort: click ModalFade (background dismiss area)
            foreach (var elem in _elements)
            {
                if (elem.GameObject != null && elem.GameObject.name.Contains("ModalFade"))
                {
                    Log.Msg("{NavigatorId}", $"Fallback to ModalFade: {elem.GameObject.name}");
                    UIActivator.Activate(elem.GameObject);
                    return;
                }
            }

            Log.Msg("{NavigatorId}", $"No close mechanism found");
        }

        /// <summary>
        /// Stop pack music by sending PointerExit to the currently active pack hitbox.
        /// Same approach used by GeneralMenuNavigator when switching packs in the carousel.
        /// Sealed event opening has no carousel/hover hitboxes — skip in that mode.
        /// </summary>
        private void StopPackMusic()
        {
            if (_isSealedMode || _chamberRoot == null) return;

            foreach (Transform t in _chamberRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == "Hitbox_BoosterMesh" && t.gameObject.activeInHierarchy)
                {
                    Log.Msg("{NavigatorId}", $"Stopping pack music: PointerExit to {t.name}");
                    UIActivator.SimulatePointerExit(t.gameObject);
                }
            }
        }

        /// <summary>
        /// Restore pack-specific music after the game's opening animation stops it.
        /// The SealedBoosterView.ConditionalHoverOff() runs during the OpenOutro animation,
        /// resetting the RTPC values to 0. We re-set them to 100 via AudioManager.
        /// </summary>
        private void RestorePackMusic()
        {
            if (_packMusicRestored) return;
            // Sealed event opening doesn't use the BoosterChamberController/RTPC audio path.
            if (_isSealedMode || _chamberRoot == null) return;

            var boosterChamber = _chamberRoot;

            Component chamberController = null;
            foreach (var mb in boosterChamber.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == T.BoosterChamberController)
                {
                    chamberController = mb;
                    break;
                }
            }
            if (chamberController == null) return;

            // Read _setCode from the controller
            if (_chamberSetCodeField == null)
                _chamberSetCodeField = chamberController.GetType().GetField("_setCode", PrivateInstance);
            if (_chamberSetCodeField == null) return;

            string setCode = _chamberSetCodeField.GetValue(chamberController) as string;
            if (string.IsNullOrEmpty(setCode)) return;

            // Find AudioManager.SetRTPCValue(string, float) static method
            if (_setRTPCMethod == null)
            {
                var audioManagerType = FindType("AudioManager");
                if (audioManagerType != null)
                    _setRTPCMethod = audioManagerType.GetMethod("SetRTPCValue",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new[] { typeof(string), typeof(float) }, null);
            }
            if (_setRTPCMethod == null) return;

            // Set RTPC values to restore pack-specific music (dashes → underscores for Wwise)
            string audioSetCode = setCode.Replace("-", "_");
            _setRTPCMethod.Invoke(null, new object[] { "booster_packrollover", 100f });
            _setRTPCMethod.Invoke(null, new object[] { "boosterpack_" + audioSetCode, 100f });
            _packMusicRestored = true;

            Log.Msg("{NavigatorId}", $"Restored pack music for set: {setCode}");
        }

        /// <summary>
        /// Force-reveal all unrevealed cards in _cardsToOpen and call UpdateRevealed() on
        /// BoosterChamberController. This puts the game in the same state as if the user had
        /// manually revealed every card, ensuring clean dismiss transitions.
        /// </summary>
        private void ForceRevealAllCards()
        {
            var cards = GetCardsToOpen(_controller);
            if (cards == null || cards.Count == 0) return;

            int revealed = 0;
            foreach (var card in cards)
            {
                if (card == null) continue;
                if (!IsEntryRevealed(card))
                {
                    if (_revealedProp == null)
                        _revealedProp = card.GetType().GetProperty("Revealed", PublicInstance);
                    if (_revealedProp != null)
                    {
                        _revealedProp.SetValue(card, true);
                        revealed++;
                    }
                }
            }

            if (revealed > 0)
            {
                Log.Msg("{NavigatorId}", $"Force-revealed {revealed}/{cards.Count} cards before close");
                CallUpdateRevealed();
            }
        }

        /// <summary>
        /// Call UpdateRevealed() on BoosterChamberController to sync CardsRevealed bool
        /// on the booster chamber animator. GetUnrevealedCardCount() should return 0 after
        /// ForceRevealAllCards(), causing CardsRevealed=true.
        /// </summary>
        private void CallUpdateRevealed()
        {
            // Only the regular booster chamber has BoosterChamberController/UpdateRevealed.
            if (_isSealedMode || _chamberRoot == null) return;
            var boosterChamber = _chamberRoot;

            Component chamberController = null;
            foreach (var mb in boosterChamber.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == T.BoosterChamberController)
                {
                    chamberController = mb;
                    break;
                }
            }
            if (chamberController == null) return;

            var updateMethod = chamberController.GetType().GetMethod("UpdateRevealed",
                PrivateInstance | System.Reflection.BindingFlags.Public);
            if (updateMethod != null && updateMethod.GetParameters().Length == 0)
            {
                updateMethod.Invoke(chamberController, null);
                Log.Msg("{NavigatorId}", $"Called UpdateRevealed on BoosterChamberController");
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
                    Log.Msg("{NavigatorId}", $"Found DismissCards on scroll list controller, invoking to close");
                    try
                    {
                        dismissCards.Invoke(controller, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Msg("{NavigatorId}", $"DismissCards failed: {ex.Message}");
                    }
                }

                if (closeMethod != null)
                {
                    Log.Msg("{NavigatorId}", $"Invoking {closeMethod.Name}() to close");
                    try
                    {
                        closeMethod.Invoke(controller, null);
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Msg("{NavigatorId}", $"{closeMethod.Name} failed: {ex.Message}");
                    }
                }
            }

            Log.Msg("{NavigatorId}", $"Could not close pack contents via controller");
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
            // Sealed event opening uses SealedBoosterOpenAnimation._doneButton instead.
            // The scroll list controller's DismissCards delegate routes to DoneButton_OnClick
            // for sealed (set in Init()), so the fallback path below handles that case.
            if (_isSealedMode) return false;
            if (_chamberRoot == null) return false;
            var boosterChamber = _chamberRoot;

            // Find the BoosterChamberController component (the NavContentController, not the scroll list)
            Component chamberController = null;
            foreach (var mb in boosterChamber.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == T.BoosterChamberController)
                {
                    chamberController = mb;
                    break;
                }
            }

            if (chamberController == null)
            {
                Log.Msg("{NavigatorId}", $"BoosterChamberController not found on chamber GO");
                return false;
            }

            // Find DismissCards (private method on BoosterChamberController)
            var dismissMethod = chamberController.GetType().GetMethod("DismissCards",
                PrivateInstance | System.Reflection.BindingFlags.Public);
            if (dismissMethod == null || dismissMethod.GetParameters().Length != 0)
            {
                Log.Msg("{NavigatorId}", $"DismissCards not found on BoosterChamberController");
                return false;
            }

            Log.Msg("{NavigatorId}", $"Found DismissCards on BoosterChamberController, invoking");
            try
            {
                dismissMethod.Invoke(chamberController, null);
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Msg("{NavigatorId}", $"BoosterChamberController.DismissCards failed: {ex.Message}");
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

            // Re-scan: works for both regular and sealed chambers
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.BoosterOpenToScrollListController)
                {
                    Log.Msg("{NavigatorId}", $"Re-found controller: {mb.GetType().Name}");
                    _controller = mb;
                    return mb;
                }
            }

            Log.Msg("{NavigatorId}", $"FindBoosterController: No controller found");
            return null;
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

            Log.Msg("{NavigatorId}", $"Cleared AutoReveal on {cleared}/{cards.Count} cards");
        }

        /// <summary>
        /// Get GrpId from a _cardsToOpen entry via reflection.
        /// CardDataAndRevealStatus.CardData (public field) → CardData.GrpId (public property).
        /// </summary>
        private uint GetGrpIdFromEntry(object entry)
        {
            if (entry == null) return 0;

            if (_cardDataField == null)
                _cardDataField = entry.GetType().GetField("CardData", PublicInstance);
            if (_cardDataField == null) return 0;

            var cardData = _cardDataField.GetValue(entry);
            if (cardData == null) return 0;

            if (_grpIdProp == null)
                _grpIdProp = cardData.GetType().GetProperty("GrpId", PublicInstance);
            if (_grpIdProp == null) return 0;

            var val = _grpIdProp.GetValue(cardData);
            return val is uint grpId ? grpId : 0;
        }

        /// <summary>
        /// Check if a _cardsToOpen entry has been revealed (face-up).
        /// Uses the CardDataAndRevealStatus.Revealed property (data-driven, not UI-based).
        /// </summary>
        private bool IsEntryRevealed(object entry)
        {
            if (entry == null) return false;

            if (_revealedProp == null)
                _revealedProp = entry.GetType().GetProperty("Revealed", PublicInstance);
            if (_revealedProp == null) return false;

            var val = _revealedProp.GetValue(entry);
            return val is bool revealed && revealed;
        }

        /// <summary>
        /// Get card info from _cardsToOpen data by index, using GrpId-based lookup.
        /// Returns null if the card data is not available or GrpId lookup fails.
        /// </summary>
        private CardInfo? GetCardInfoFromData(int dataIndex)
        {
            var cards = GetCardsToOpen(_controller);
            if (cards == null || dataIndex < 0 || dataIndex >= cards.Count) return null;

            var entry = cards[dataIndex];
            uint grpId = GetGrpIdFromEntry(entry);
            if (grpId == 0) return null;

            return CardModelProvider.GetCardInfoFromGrpId(grpId);
        }

        /// <summary>
        /// Reveal an off-screen card by setting Revealed=true on its _cardsToOpen data entry.
        /// Plays the flip sound manually (no on-screen BoosterCardHolder to run OnClick) and
        /// announces the card name.
        /// </summary>
        private void RevealCardByData(int dataIndex)
        {
            var cards = GetCardsToOpen(_controller);
            if (cards == null || dataIndex < 0 || dataIndex >= cards.Count) return;

            var entry = cards[dataIndex];
            if (entry == null || IsEntryRevealed(entry)) return;

            if (_revealedProp == null)
                _revealedProp = entry.GetType().GetProperty("Revealed", PublicInstance);
            if (_revealedProp == null) return;

            _revealedProp.SetValue(entry, true);
            Log.Msg("{NavigatorId}", $"Revealed off-screen card at data index {dataIndex}");

            // Mirror BoosterCardHolder.PlayFlipSound: pick the Wwise event by card rarity.
            PlayFlipSoundForEntry(entry);

            // Announce the card name
            var cardInfo = GetCardInfoFromData(dataIndex);
            if (cardInfo.HasValue && cardInfo.Value.IsValid && !string.IsNullOrEmpty(cardInfo.Value.Name))
            {
                string label = cardInfo.Value.Name;
                if (!string.IsNullOrEmpty(cardInfo.Value.TypeLine))
                    label += $", {cardInfo.Value.TypeLine}";
                _announcer.AnnounceInterrupt(label);
            }

            // Trigger rescan to update navigation labels
            _rescanDone = false;
            _rescanFrameCounter = 0;
        }

        /// <summary>
        /// Play the booster flip sound for a card by rarity, mirroring
        /// BoosterCardHolder.PlayFlipSound. Used for off-screen reveals where there's
        /// no holder to run OnClick().
        /// </summary>
        private void PlayFlipSoundForEntry(object entry)
        {
            if (_cardDataField == null)
                _cardDataField = entry.GetType().GetField("CardData", PublicInstance);
            var cardData = _cardDataField?.GetValue(entry);
            if (cardData == null) return;

            if (_rarityProp == null)
                _rarityProp = cardData.GetType().GetProperty("Rarity", PublicInstance);
            var rarity = _rarityProp?.GetValue(cardData);
            string rarityName = rarity?.ToString() ?? "";

            string eventName;
            if (rarityName == "MythicRare") eventName = "sfx_ui_boost_card_flip_mythic_rare";
            else if (rarityName == "Rare") eventName = "sfx_ui_boost_card_flip_rare";
            else eventName = "sfx_ui_boost_card_flip_common";

            if (_playAudioStringMethod == null)
            {
                var audioManagerType = FindType("AudioManager");
                if (audioManagerType != null)
                    _playAudioStringMethod = audioManagerType.GetMethod("PlayAudio",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new[] { typeof(string), typeof(GameObject) }, null);
            }
            if (_playAudioStringMethod == null) return;

            var target = (_controller as MonoBehaviour)?.gameObject;
            if (target == null) return;

            _playAudioStringMethod.Invoke(null, new object[] { eventName, target });
        }

        /// <summary>
        /// Override ForceRescan to preserve cursor position and suppress redundant announcements.
        /// The base implementation resets to index 0 and re-announces the full activation text
        /// on every rescan, which is disruptive during periodic polling.
        /// Uses _elementDataIndex for stable position restore across GO ↔ TextBlock transitions
        /// (cards moving on/off screen as the viewport changes).
        /// </summary>
        public override void ForceRescan()
        {
            if (!_isActive) return;

            int oldCount = _elements.Count;
            int oldIndex = _currentIndex;
            int oldDataIndex = (oldIndex >= 0 && oldIndex < _elementDataIndex.Count) ? _elementDataIndex[oldIndex] : -1;
            string oldLabel = IsValidIndex ? _elements[_currentIndex].Label : null;

            _elements.Clear();
            _currentIndex = -1;

            DiscoverElements();

            if (_elements.Count > 0)
            {
                // Restore position by matching data index (stable across GO ↔ TextBlock transitions)
                int restored = -1;
                if (oldDataIndex >= 0)
                {
                    for (int i = 0; i < _elementDataIndex.Count; i++)
                    {
                        if (_elementDataIndex[i] == oldDataIndex)
                        {
                            restored = i;
                            break;
                        }
                    }
                }

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
                    Log.Msg("{NavigatorId}", $"Rescan: {oldCount} -> {_elements.Count} elements");
                    _announcer.AnnounceInterrupt(GetActivationAnnouncement());
                }
                // Card revealed (label changed): announce card name
                else if (restored >= 0)
                {
                    string newLabel = _elements[restored].Label;
                    if (newLabel != oldLabel)
                        _announcer.AnnounceInterrupt(newLabel);
                }

                UpdateCardNavigation();
                UpdateCardInfoForOffScreenCard();
            }
            else
            {
                Log.Msg("{NavigatorId}", $"Rescan found no elements");
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

                    // Restore pack-specific music that the opening animation stopped.
                    // Called on early rescan ticks to handle ConditionalHoverOff timing.
                    if (_rescanAttempt <= 3)
                        RestorePackMusic();

                    // No animation skip — let the game's animation play naturally.
                    // Skipping corrupted the _openingBoosterPackAnimator state, breaking
                    // carousel interaction after closing the pack. Cards appear face-down
                    // (AutoReveal cleared in DetectScreen) and rescans pick them up gradually.

                    Log.Msg("{NavigatorId}", $"Rescanning for cards (attempt {_rescanAttempt}/{MaxRescanAttempts}, current: {oldCount})");
                    ForceRescan();

                    if (_totalCards > 0 || _rescanAttempt >= MaxRescanAttempts)
                    {
                        _rescanDone = true;
                        if (_totalCards > 0)
                            Log.Msg("{NavigatorId}", $"Found {_totalCards}/{_expectedCardCount} cards after {_rescanAttempt} rescans");
                        else
                            Log.Msg("{NavigatorId}", $"Timed out after {_rescanAttempt} rescans");
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
                    Log.Msg("{NavigatorId}", $"Checking if screen is still valid after close action");

                    // Re-check if we're still on pack contents (CardScroller must exist)
                    if (!DetectScreen())
                    {
                        Log.Msg("{NavigatorId}", $"Pack contents closed, deactivating navigator");
                        Deactivate();
                    }
                    else
                    {
                        Log.Msg("{NavigatorId}", $"Still on pack contents, rescanning");
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
            _closeTriggered = false;
            _closeRescanCounter = 0;
            _packMusicRestored = false;
            _cardDataIndices.Clear();
            _elementDataIndex.Clear();
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
                Log.Msg("{NavigatorId}", $"Controller no longer active");
                return false;
            }

            // Also check that _cardsToOpen is still populated (cleared on dismiss)
            var cards = GetCardsToOpen(_controller);
            if (cards == null || cards.Count == 0)
            {
                Log.Msg("{NavigatorId}", $"Cards cleared, pack dismissed");
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
            _chamberRoot = null;
            _sealedAnimation = null;
            _isSealedMode = false;
            _revealAllButton = null;
        }
    }
}
