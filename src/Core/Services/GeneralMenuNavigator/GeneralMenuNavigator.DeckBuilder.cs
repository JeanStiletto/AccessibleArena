using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.ElementGrouping;
using System;
using System.Collections.Generic;
using System.Linq;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    public partial class GeneralMenuNavigator
    {
        // Deck builder card count announcement - when true, PerformRescan announces just the card count
        // instead of the full rescan announcement (set when adding/removing a card)
        private bool _announceDeckCountOnRescan;
        // Card count captured BEFORE activation (before game processes add/remove)
        // so we can detect actual changes in PerformRescan
        private string _deckCountBeforeActivation;

        // ReadOnly deck builder mode (starter/precon decks)
        private bool _isDeckBuilderReadOnly;

        // Rename edit mode tracking: set when user activates the Rename action
        private bool _isInRenameMode;

        // Cached DeckManagerController reference for reading deck favorite state
        private MonoBehaviour _cachedDeckManagerController;

        // 2D sub-navigation state for DeckBuilderInfo group
        // Rows navigated with Up/Down, entries within rows navigated with Left/Right
        private List<(string label, List<string> entries)> _deckInfoRows;
        private int _deckInfoEntryIndex;

        // Group types to cycle between in the deck builder with Tab/Shift+Tab
        private static readonly ElementGroup[] DeckBuilderCycleGroups = new[]
        {
            ElementGroup.DeckBuilderCollection,  // Card pool (Collection)
            ElementGroup.DeckBuilderSideboard,   // Sideboard cards (draft/sealed)
            ElementGroup.DeckBuilderDeckList,    // Deck list cards (cards in your deck)
            ElementGroup.DeckBuilderInfo,        // Deck info (card count, mana curve, types, colors)
            ElementGroup.Filters,                // Filter controls
            ElementGroup.Content,                // Other deck builder controls
            ElementGroup.PlayBladeContent        // Play options
        };

        /// <summary>
        /// Buttons inside MainButtons that work without a deck selected (standalone).
        /// Everything else in MainButtons is deck-specific and will be hidden from navigation.
        /// </summary>
        private static readonly string[] StandaloneMainButtonNames = { "Import", "Sammlung", "Collection" };

        /// <summary>
        /// Structure to hold deck toolbar buttons for attached actions.
        /// </summary>
        private struct DeckToolbarButtons
        {
            public GameObject EditButton;       // EditDeck_MainButtonBlue
            public GameObject DeleteButton;     // Delete_MainButton_Round
            public GameObject ExportButton;     // Export_MainButton_Round
            public GameObject FavoriteButton;   // Favorite button
            public GameObject CloneButton;      // Clone button
            public GameObject DetailsButton;    // DeckDetails_MainButton_Round
            /// <summary>All deck-specific buttons to hide from top-level navigation.</summary>
            public HashSet<GameObject> AllDeckSpecificButtons;
        }

        /// <summary>
        /// Handle back navigation in the deck builder.
        /// The deck builder uses MainButton (labeled "Fertig"/"Done") to exit editing mode.
        /// </summary>
        private bool HandleDeckBuilderBack()
        {
            LogDebug($"[{NavigatorId}] Deck Builder detected, looking for Done button");

            // Find MainButton within DeckBuilderWidget (not the Home page's MainButton)
            // The deck builder's button is at: DeckBuilderWidget_Desktop_16x9/BottomRight/Buttons/MainButton
            var deckBuilderWidget = GameObject.Find("DeckBuilderWidget_Desktop_16x9");
            if (deckBuilderWidget != null)
            {
                var mainButton = deckBuilderWidget.transform.Find("BottomRight/Buttons/MainButton");
                if (mainButton != null && mainButton.gameObject.activeInHierarchy)
                {
                    LogDebug($"[{NavigatorId}] Found deck builder Done button: {mainButton.name}");
                    _announcer.Announce(Models.Strings.ExitingDeckBuilder, Models.AnnouncementPriority.High);
                    UIActivator.Activate(mainButton.gameObject);
                    TriggerRescan();
                    return true;
                }
                LogDebug($"[{NavigatorId}] DeckBuilderWidget found but MainButton not at expected path");
            }
            else
            {
                LogDebug($"[{NavigatorId}] DeckBuilderWidget_Desktop_16x9 not found");
            }

            // Fallback: navigate to Home if MainButton not found
            LogDebug($"[{NavigatorId}] Deck Builder Done button not found, navigating to Home");
            return NavigateToHome();
        }

        /// <summary>
        /// Find the DeckView_Base parent for a deck entry element.
        /// Used to group UI and TextBox elements that belong to the same deck.
        /// </summary>
        private Transform FindDeckViewParent(Transform element)
        {
            Transform current = element;
            int maxLevels = 5;

            while (current != null && maxLevels > 0)
            {
                // Only match DeckView_Base - NOT Blade_ListItem (those are play mode options, not decks)
                if (current.name.Contains("DeckView_Base"))
                {
                    return current;
                }
                current = current.parent;
                maxLevels--;
            }

            return null;
        }

        /// <summary>
        /// Find the deck toolbar buttons in the DeckManager screen.
        /// Collects all deck-specific buttons (for attached actions + filtering from navigation)
        /// and whitelists standalone buttons like Import and Sammlung.
        /// </summary>
        private DeckToolbarButtons FindDeckToolbarButtons()
        {
            var result = new DeckToolbarButtons();
            result.AllDeckSpecificButtons = new HashSet<GameObject>();

            // Find MainButtons container in DeckManager
            var mainButtonsContainer = GameObject.FindObjectsOfType<Transform>()
                .FirstOrDefault(t => t.name == "MainButtons" &&
                                    t.parent != null &&
                                    t.parent.name == "SafeArea" &&
                                    t.gameObject.activeInHierarchy);

            if (mainButtonsContainer == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] DeckManager MainButtons container not found");
                return result;
            }

            // Categorize each button: standalone (keep) vs deck-specific (hide + use as actions)
            foreach (Transform child in mainButtonsContainer)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                // Check if this is a standalone button (works without a deck selected)
                bool isStandalone = StandaloneMainButtonNames.Any(s =>
                    child.name.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) >= 0);

                if (isStandalone)
                    continue; // Keep in navigation

                // Deck-specific button - collect for filtering and actions
                result.AllDeckSpecificButtons.Add(child.gameObject);

                if (child.name.Contains("Delete"))
                    result.DeleteButton = child.gameObject;
                else if (child.name.Contains("EditDeck"))
                    result.EditButton = child.gameObject;
                else if (child.name.Contains("Export"))
                    result.ExportButton = child.gameObject;
                else if (child.name.Contains("Favorite"))
                    result.FavoriteButton = child.gameObject;
                else if (child.name.Contains("Clone"))
                    result.CloneButton = child.gameObject;
                else if (child.name.Contains("DeckDetails"))
                    result.DetailsButton = child.gameObject;
            }

            if (result.AllDeckSpecificButtons.Count > 0)
                MelonLogger.Msg($"[{NavigatorId}] DeckManager: {result.AllDeckSpecificButtons.Count} deck-specific buttons hidden from navigation");

            return result;
        }

        /// <summary>
        /// Build attached actions list for a deck element.
        /// </summary>
        private List<AttachedAction> BuildDeckAttachedActions(DeckToolbarButtons toolbarButtons, GameObject renameButton)
        {
            var actions = new List<AttachedAction>();

            // Rename (TextBox button on the deck) — no game loc key, use our lang file
            if (renameButton != null)
                actions.Add(new AttachedAction { Id = "Rename", Label = Models.Strings.DeckActionRename, TargetButton = renameButton });

            // Edit (open deck builder) — game reads button text via SetText with loc key
            if (toolbarButtons.EditButton != null)
                actions.Add(new AttachedAction { Id = "Edit", Label = ResolveDeckActionLabel("MainNav/DeckManager/DeckManager_Top_Edit", toolbarButtons.EditButton, "Edit"), TargetButton = toolbarButtons.EditButton });

            // Deck Details — no game loc key, use our lang file
            if (toolbarButtons.DetailsButton != null)
                actions.Add(new AttachedAction { Id = "Details", Label = Models.Strings.DeckActionDetails, TargetButton = toolbarButtons.DetailsButton });

            // Favorite
            if (toolbarButtons.FavoriteButton != null)
                actions.Add(new AttachedAction { Id = "Favorite", Label = ResolveDeckActionLabel("MainNav/DeckManager/DeckManager_Top_Favorite", toolbarButtons.FavoriteButton, "Favorite"), TargetButton = toolbarButtons.FavoriteButton });

            // Clone
            if (toolbarButtons.CloneButton != null)
                actions.Add(new AttachedAction { Id = "Clone", Label = ResolveDeckActionLabel("MainNav/DeckManager/DeckManager_Top_Clone", toolbarButtons.CloneButton, "Clone"), TargetButton = toolbarButtons.CloneButton });

            // Export
            if (toolbarButtons.ExportButton != null)
                actions.Add(new AttachedAction { Id = "Export", Label = ResolveDeckActionLabel("MainNav/DeckManager/DeckManager_Top_Export", toolbarButtons.ExportButton, "Export"), TargetButton = toolbarButtons.ExportButton });

            // Delete (last, as it's destructive)
            if (toolbarButtons.DeleteButton != null)
                actions.Add(new AttachedAction { Id = "Delete", Label = ResolveDeckActionLabel("MainNav/DeckManager/DeckManager_Top_Delete", toolbarButtons.DeleteButton, "Delete"), TargetButton = toolbarButtons.DeleteButton });

            return actions;
        }

        /// <summary>
        /// Resolve a deck action label: try game loc key first, then read button text, then English fallback.
        /// </summary>
        private static string ResolveDeckActionLabel(string locKey, GameObject button, string fallback)
        {
            // Try game's localization system
            string resolved = UITextExtractor.ResolveLocKey(locKey);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;

            // Try reading text already set on the button (game may have called SetText)
            if (button != null)
            {
                var tmp = button.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmp != null)
                {
                    string text = tmp.text?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }

            return fallback;
        }

        /// <summary>
        /// Handle Rename and Favorite specially.
        /// Rename: enter edit mode directly on the embedded TMP_InputField instead of clicking TextBox,
        ///   to avoid anti-autofocus code immediately deactivating the field.
        /// Favorite: read current IsFavorite state before activating, then announce the new state.
        /// </summary>
        protected override bool HandleAttachedAction(AttachedAction action)
        {
            string id = action.Id ?? action.Label;

            if (id == "Rename" && action.TargetButton != null)
            {
                var inputField = action.TargetButton.GetComponentInChildren<TMPro.TMP_InputField>(true);
                if (inputField != null)
                {
                    string currentName = inputField.text;
                    MelonLogger.Msg($"[{NavigatorId}] Rename: entering edit mode on {inputField.gameObject.name}, current name: '{currentName}'");
                    _isInRenameMode = true;
                    EnterInputFieldEditModeDirectly(inputField.gameObject,
                        $"Renaming {currentName}. Type new name, Enter to confirm, Escape to cancel.");
                    return true;
                }
            }

            if (id == "Favorite" && action.TargetButton != null)
            {
                bool? isFavorited = TryGetDeckFavoriteState();
                UIActivator.Activate(action.TargetButton);
                string announcement = isFavorited.HasValue
                    ? (isFavorited.Value ? "Removed from favorites." : "Added to favorites.")
                    : Models.Strings.ActivatedBare;
                _announcer.Announce(announcement, AnnouncementPriority.Normal);
                return true;
            }

            if (id == "Clone" && action.TargetButton != null)
            {
                UIActivator.Activate(action.TargetButton);
                _announcer.Announce("Deck cloned. Re-enter decks to see it.", AnnouncementPriority.Normal);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Intercept Enter during rename edit mode to announce the new deck name.
        /// The key is NOT consumed so the game's TMP_InputField still submits the rename.
        /// </summary>
        protected override void HandleInputFieldNavigation()
        {
            if (_isInRenameMode)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    string newName = GetEditingFieldText()?.Trim() ?? "";
                    _isInRenameMode = false;
                    if (!string.IsNullOrEmpty(newName))
                        _announcer.Announce($"Renamed to {newName}.", AnnouncementPriority.Normal);
                    // Deactivate the field — this fires onEndEdit so the game saves the rename.
                    // We return early so the Enter key is not passed to the now-deactivated field.
                    ForceExitFieldEditMode();
                    return;
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _isInRenameMode = false;
                    // Let base handle Escape (exits edit mode, announces cancelled)
                }
            }
            base.HandleInputFieldNavigation();
        }

        /// <summary>
        /// Read IsFavorite from the selected deck in DeckManagerController via reflection.
        /// Returns null if the state cannot be determined.
        /// </summary>
        private bool? TryGetDeckFavoriteState()
        {
            // Re-use cached reference if still valid
            if (_cachedDeckManagerController == null || !_cachedDeckManagerController)
            {
                _cachedDeckManagerController = null;
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "DeckManagerController")
                    {
                        _cachedDeckManagerController = mb;
                        break;
                    }
                }
            }

            if (_cachedDeckManagerController == null) return null;

            try
            {
                var selectedDeckField = _cachedDeckManagerController.GetType()
                    .GetField("_selectedDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var selectedDeck = selectedDeckField?.GetValue(_cachedDeckManagerController);
                if (selectedDeck == null) return null;

                var summaryProp = selectedDeck.GetType()
                    .GetProperty("Summary", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var summary = summaryProp?.GetValue(selectedDeck);
                if (summary == null) return null;

                var isFavProp = summary.GetType()
                    .GetProperty("IsFavorite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (isFavProp == null) return null;

                return (bool)isFavProp.GetValue(summary);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Auto-focus the unlocked card on NPE reward screens for better UX.
        /// The card should be the first thing focused so users can read its info.
        /// </summary>
        private void AutoFocusUnlockedCard()
        {
            // Only on NPE rewards screen
            if (!_screenDetector.IsNPERewardsScreenActive())
                return;

            // Find unlocked card elements (they have "Unlocked card" in their label)
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].Label != null &&
                    _elements[i].Label.StartsWith("Unlocked card"))
                {
                    // Move this element to the front
                    if (i > 0)
                    {
                        var cardElement = _elements[i];
                        _elements.RemoveAt(i);
                        _elements.Insert(0, cardElement);
                        LogDebug($"[{NavigatorId}] Moved unlocked card to first position: {cardElement.Label}");
                    }
                    _currentIndex = 0;
                    break;
                }
            }
        }

        /// <summary>
        /// Captures deck card count before a collection card click.
        /// Called before UIActivator.Activate so the count reflects pre-click state.
        /// </summary>
        protected override void OnDeckBuilderCardCountCapture()
        {
            if (_activeContentController == T.WrapperDeckBuilder)
            {
                _deckCountBeforeActivation = DeckInfoProvider.GetCardCountText();
            }
        }

        /// <summary>
        /// Called after a deck builder card is activated (collection or deck list).
        /// Triggers rescan to update the deck/collection display.
        /// </summary>
        protected override void OnDeckBuilderCardActivated()
        {
            if (_activeContentController == T.WrapperDeckBuilder)
            {
                LogDebug($"[{NavigatorId}] Deck builder card activated - scheduling rescan to update lists");
                // _deckCountBeforeActivation already captured by OnDeckBuilderCardCountCapture
                _announceDeckCountOnRescan = true;
                TriggerRescan();

                // Invalidate cached card info so Owned/InDeck/Quantity updates on next arrow press
                AccessibleArenaMod.Instance?.CardNavigator?.InvalidateBlocks();
            }
        }

        /// <summary>
        /// Find NPE (New Player Experience) reward cards that are displayed on screen.
        /// These cards aren't buttons but should be navigable for accessibility.
        /// </summary>
        private void FindNPERewardCards(HashSet<GameObject> addedObjects)
        {
            // Note: Settings check removed - SettingsMenuNavigator takes priority when settings is open

            // Check if we're on the NPE rewards screen
            if (!_screenDetector.IsNPERewardsScreenActive())
                return;

            LogDebug($"[{NavigatorId}] NPE Rewards screen detected, searching for reward cards...");

            // Debug: Log the RewardsCONTAINER specifically (where the actual cards are)
            var npeContainer = GameObject.Find("NPE-Rewards_Container");
            if (npeContainer != null)
            {
                var activeContainer = npeContainer.transform.Find("ActiveContainer");
                if (activeContainer != null)
                {
                    var rewardsContainer = activeContainer.Find("RewardsCONTAINER");
                    if (rewardsContainer != null)
                    {
                        LogDebug($"[{NavigatorId}] RewardsCONTAINER hierarchy (depth 6):");
                        if (DebugLogging) LogHierarchy(rewardsContainer, "  ", 6);
                    }
                    else
                    {
                        LogDebug($"[{NavigatorId}] ActiveContainer hierarchy (depth 5):");
                        if (DebugLogging) LogHierarchy(activeContainer, "  ", 5);
                    }
                }
            }

            // Find NPE reward cards - ONLY inside NPE-Rewards_Container, not deck boxes
            var cardPrefabs = new List<GameObject>();

            if (npeContainer != null)
            {
                // Search within NPE-Rewards_Container only
                foreach (var transform in npeContainer.GetComponentsInChildren<Transform>(false))
                {
                    if (transform == null || !transform.gameObject.activeInHierarchy)
                        continue;

                    string name = transform.name;
                    string path = GetFullPath(transform);

                    // Skip deck box cards (background elements)
                    if (path.Contains("DeckBox") || path.Contains("Deckbox") || path.Contains("RewardChest"))
                        continue;

                    // NPE reward cards - try multiple patterns
                    if (name.Contains("NPERewardPrefab_IndividualCard") ||
                        name.Contains("CardReward") ||
                        name.Contains("CardAnchor") ||
                        name.Contains("RewardCard") ||
                        name.Contains(T.MetaCardView) ||
                        name.Contains("CDC"))
                    {
                        LogDebug($"[{NavigatorId}] Found potential NPE card element: {name} at {path}");
                        if (!addedObjects.Contains(transform.gameObject))
                        {
                            cardPrefabs.Add(transform.gameObject);
                        }
                    }
                }
            }

            if (cardPrefabs.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No NPE reward cards found in NPE-Rewards_Container");
                return;
            }

            // Sort cards by X position (left to right)
            cardPrefabs = cardPrefabs.OrderBy(c => c.transform.position.x).ToList();

            LogDebug($"[{NavigatorId}] Found {cardPrefabs.Count} NPE reward card(s)");

            int cardNum = 1;
            foreach (var cardPrefab in cardPrefabs)
            {
                // Find the CardAnchor child which holds the actual card visuals
                var cardAnchor = cardPrefab.transform.Find("CardAnchor");
                GameObject cardObj = cardAnchor?.gameObject ?? cardPrefab;

                // Extract card info using CardDetector
                var cardInfo = CardDetector.ExtractCardInfo(cardPrefab);
                string cardName = cardInfo.IsValid ? cardInfo.Name : "Unknown card";

                // Build label with card number if multiple cards
                string label = cardPrefabs.Count > 1
                    ? $"Unlocked card {cardNum}: {cardName}"
                    : $"Unlocked card: {cardName}";

                // Add type line if available
                if (!string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                LogDebug($"[{NavigatorId}] Adding NPE reward card: {label}");

                // Add as navigable element (even though it's not a button)
                // Using the card prefab so arrow up/down can read card info blocks
                AddElement(cardObj, label);
                addedObjects.Add(cardPrefab);
                cardNum++;
            }

            // Add the NullClaimButton as "Take reward" button
            // This button has a CustomButton that dismisses the reward screen when clicked
            // NOTE: The button name starts with "Null" which is normally filtered out,
            // so we explicitly add it here to make NPE rewards accessible
            // Search within entire npeContainer hierarchy (more robust than Transform.Find)
            if (npeContainer != null)
            {
                Transform claimButton = null;
                foreach (var transform in npeContainer.GetComponentsInChildren<Transform>(false))
                {
                    if (transform != null && transform.name == "NullClaimButton" && transform.gameObject.activeInHierarchy)
                    {
                        claimButton = transform;
                        break;
                    }
                }

                if (claimButton != null && !addedObjects.Contains(claimButton.gameObject))
                {
                    LogDebug($"[{NavigatorId}] Adding NullClaimButton as 'Take reward' button (path: {GetFullPath(claimButton)})");
                    AddElement(claimButton.gameObject, "Take reward, button");
                    addedObjects.Add(claimButton.gameObject);
                }
                else if (claimButton == null)
                {
                    LogDebug($"[{NavigatorId}] NullClaimButton not found in NPE-Rewards_Container hierarchy");
                }
                else
                {
                    LogDebug($"[{NavigatorId}] NullClaimButton already in addedObjects (ID:{claimButton.gameObject.GetInstanceID()})");
                }
            }
            else
            {
                LogDebug($"[{NavigatorId}] NPE-Rewards_Container not found for NullClaimButton lookup");
            }
        }

        /// <summary>
        /// Find collection cards in the deck builder's PoolHolder canvas.
        /// Uses CardPoolAccessor to get only the current page's cards directly from the game's
        /// CardPoolHolder API, instead of scanning the entire hierarchy.
        /// </summary>
        private void FindPoolHolderCards(HashSet<GameObject> addedObjects)
        {
            // Only active in deck builder
            if (_activeContentController != T.WrapperDeckBuilder)
                return;

            LogDebug($"[{NavigatorId}] Deck Builder detected, searching for collection cards via CardPoolAccessor...");

            // Try direct API access via CardPoolAccessor
            var poolHolder = CardPoolAccessor.FindCardPoolHolder();
            if (poolHolder == null)
            {
                LogDebug($"[{NavigatorId}] CardPoolHolder not found, falling back to hierarchy scan");
                FindPoolHolderCardsFallback(addedObjects);
                return;
            }

            // Get only the current page's cards
            var cardObjects = CardPoolAccessor.GetCurrentPageCards();
            if (cardObjects == null || cardObjects.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No cards on current page");
                return;
            }

            int pageIndex = CardPoolAccessor.GetCurrentPageIndex();
            int pageCount = CardPoolAccessor.GetPageCount();
            LogDebug($"[{NavigatorId}] Page {pageIndex + 1} of {pageCount}: {cardObjects.Count} card view(s)");

            int cardNum = 1;
            int skippedCount = 0;
            foreach (var cardObj in cardObjects)
            {
                if (cardObj == null || addedObjects.Contains(cardObj)) continue;

                // Extract card info using CardDetector
                var cardInfo = CardDetector.ExtractCardInfo(cardObj);

                // Skip unloaded/placeholder cards (GrpId = 0, typically named "CDC #0")
                if (!cardInfo.IsValid)
                {
                    skippedCount++;
                    continue;
                }

                // Collection style: "CardName, TypeLine, ManaCost"
                string label = cardInfo.Name;

                if (!string.IsNullOrEmpty(cardInfo.TypeLine))
                {
                    label += $", {cardInfo.TypeLine}";
                }

                if (!string.IsNullOrEmpty(cardInfo.ManaCost))
                {
                    label += $", {cardInfo.ManaCost}";
                }

                addedObjects.Add(cardObj);
                AddElement(cardObj, label);
                cardNum++;
            }

            if (skippedCount > 0)
            {
                LogDebug($"[{NavigatorId}] Skipped {skippedCount} placeholder cards");
            }
        }

        /// <summary>
        /// Fallback: scan PoolHolder hierarchy for collection cards.
        /// Used when CardPoolAccessor cannot find the CardPoolHolder component.
        /// </summary>
        private void FindPoolHolderCardsFallback(HashSet<GameObject> addedObjects)
        {
            var poolHolderObj = GameObject.Find("PoolHolder");
            if (poolHolderObj == null)
            {
                LogDebug($"[{NavigatorId}] PoolHolder not found");
                return;
            }

            var cardViews = new List<(GameObject obj, float sortOrder)>();

            foreach (var mb in poolHolderObj.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                string typeName = mb.GetType().Name;
                if (typeName == T.PagesMetaCardView || typeName == T.MetaCardView)
                {
                    var cardObj = mb.gameObject;
                    if (!addedObjects.Contains(cardObj))
                    {
                        float sortOrder = cardObj.transform.position.x + (-cardObj.transform.position.y * 1000);
                        cardViews.Add((cardObj, sortOrder));
                        addedObjects.Add(cardObj);
                    }
                }
            }

            if (cardViews.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No collection cards found in PoolHolder (fallback)");
                return;
            }

            cardViews = cardViews.OrderBy(c => c.sortOrder).ToList();
            LogDebug($"[{NavigatorId}] Found {cardViews.Count} collection card(s) in PoolHolder (fallback)");

            foreach (var (cardObj, _) in cardViews)
            {
                var cardInfo = CardDetector.ExtractCardInfo(cardObj);
                if (!cardInfo.IsValid) continue;

                string label = cardInfo.Name;
                if (!string.IsNullOrEmpty(cardInfo.TypeLine))
                    label += $", {cardInfo.TypeLine}";
                if (!string.IsNullOrEmpty(cardInfo.ManaCost))
                    label += $", {cardInfo.ManaCost}";

                AddElement(cardObj, label);
            }
        }

        /// <summary>
        /// Find commander/companion cards in Brawl deck builder (ListCommanderHolder).
        /// These are the commander and companion cards shown in PinnedCards above the deck list.
        /// </summary>
        private void FindCommanderCards(HashSet<GameObject> addedObjects)
        {
            MelonLogger.Msg($"[{NavigatorId}] FindCommanderCards: activeCC={_activeContentController}");

            // Only active in deck builder
            if (_activeContentController != T.WrapperDeckBuilder)
                return;

            var commanderCards = DeckCardProvider.GetCommanderCards();
            MelonLogger.Msg($"[{NavigatorId}] FindCommanderCards: {commanderCards.Count} commander card(s)");
            if (commanderCards.Count == 0)
                return;

            foreach (var cmdCard in commanderCards)
            {
                if (!cmdCard.IsValid) continue;

                // Use TileButton as the navigable element (same as regular deck list cards)
                var tileBtn = cmdCard.TileButton;
                MelonLogger.Msg($"[{NavigatorId}] FindCommanderCards: TileBtn={tileBtn?.name}, inAddedObjects={addedObjects.Contains(tileBtn)}");
                if (tileBtn == null || addedObjects.Contains(tileBtn))
                    continue;

                // Get card name from GrpId
                string cardName = CardModelProvider.GetNameFromGrpId(cmdCard.GrpId);
                if (string.IsNullOrEmpty(cardName))
                    cardName = $"Card #{cmdCard.GrpId}";

                // Build label with commander/companion prefix
                string prefix = cmdCard.IsCompanion
                    ? Models.Strings.DeckBuilderCompanion
                    : Models.Strings.DeckBuilderCommander;
                string label = $"{prefix}: {cardName}";

                LogDebug($"[{NavigatorId}] Adding commander card: {label}");

                AddElement(tileBtn, label);
                addedObjects.Add(tileBtn);

                // Mark TagButton and other siblings for exclusion from generic scan
                if (cmdCard.TagButton != null)
                    addedObjects.Add(cmdCard.TagButton);
                if (cmdCard.CardGameObject != null)
                    addedObjects.Add(cmdCard.CardGameObject);
            }
        }

        private void FindDeckListCards(HashSet<GameObject> addedObjects)
        {
            // Only active in deck builder
            if (_activeContentController != T.WrapperDeckBuilder)
                return;

            LogDebug($"[{NavigatorId}] Deck Builder detected, searching for deck list cards...");

            // Get deck list cards from CardModelProvider
            var deckCards = DeckCardProvider.GetDeckListCards();
            if (deckCards.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No deck list cards found");
                return;
            }

            LogDebug($"[{NavigatorId}] Found {deckCards.Count} deck list card(s)");

            int cardNum = 1;
            foreach (var deckCard in deckCards)
            {
                if (!deckCard.IsValid) continue;

                // Use the TileButton (card name button) as the navigable element
                var cardObj = deckCard.TileButton;
                if (cardObj == null || addedObjects.Contains(cardObj))
                    continue;

                // Get card name from GrpId
                string cardName = CardModelProvider.GetNameFromGrpId(deckCard.GrpId);
                if (string.IsNullOrEmpty(cardName))
                    cardName = $"Card #{deckCard.GrpId}";

                // Build label with quantity and card name
                string label = $"{deckCard.Quantity}x {cardName}";

                LogDebug($"[{NavigatorId}] Adding deck list card {cardNum}: {label}");

                // Add as navigable element
                AddElement(cardObj, label);
                addedObjects.Add(cardObj);
                cardNum++;
            }
        }

        /// <summary>
        /// Find sideboard cards in draft/sealed deck builder.
        /// These are cards in non-MainDeck holders inside MetaCardHolders_Container.
        /// </summary>
        private void FindSideboardCards(HashSet<GameObject> addedObjects)
        {
            // Only active in deck builder
            if (_activeContentController != T.WrapperDeckBuilder)
                return;

            var sideboardCards = DeckCardProvider.GetSideboardCards();
            if (sideboardCards.Count == 0)
                return;

            LogDebug($"[{NavigatorId}] Found {sideboardCards.Count} sideboard card(s)");

            int cardNum = 1;
            foreach (var sideCard in sideboardCards)
            {
                if (!sideCard.IsValid) continue;

                var cardObj = sideCard.TileButton;
                if (cardObj == null || addedObjects.Contains(cardObj))
                    continue;

                string cardName = CardModelProvider.GetNameFromGrpId(sideCard.GrpId);
                if (string.IsNullOrEmpty(cardName))
                    cardName = $"Card #{sideCard.GrpId}";

                string label = $"{sideCard.Quantity}x {cardName}";

                LogDebug($"[{NavigatorId}] Adding sideboard card {cardNum}: {label}");

                AddElement(cardObj, label);
                addedObjects.Add(cardObj);
                cardNum++;
            }
        }

        /// <summary>
        /// Find cards in read-only deck builder (StaticColumnMetaCardView in column view).
        /// Only runs when the normal deck list is empty (no MainDeck_MetaCardHolder with list view).
        /// </summary>
        private void FindReadOnlyDeckCards(HashSet<GameObject> addedObjects)
        {
            // Only active in deck builder
            if (_activeContentController != T.WrapperDeckBuilder)
                return;

            // Reset flag each scan - will be re-set if read-only cards found
            _isDeckBuilderReadOnly = false;

            // Only try if the normal deck list didn't find cards
            var normalDeckCards = DeckCardProvider.GetDeckListCards();
            if (normalDeckCards.Count > 0)
                return;

            LogDebug($"[{NavigatorId}] Normal deck list empty, checking for read-only column view...");

            var readOnlyCards = DeckCardProvider.GetReadOnlyDeckCards();
            if (readOnlyCards.Count == 0)
            {
                LogDebug($"[{NavigatorId}] No read-only deck cards found");
                return;
            }

            _isDeckBuilderReadOnly = true;
            LogDebug($"[{NavigatorId}] Found {readOnlyCards.Count} read-only deck card(s)");

            int cardNum = 1;
            foreach (var deckCard in readOnlyCards)
            {
                if (!deckCard.IsValid) continue;

                var cardObj = deckCard.CardGameObject;
                if (cardObj == null || addedObjects.Contains(cardObj))
                    continue;

                // Get card name from GrpId
                string cardName = CardModelProvider.GetNameFromGrpId(deckCard.GrpId);
                if (string.IsNullOrEmpty(cardName))
                    cardName = $"Card #{deckCard.GrpId}";

                // Build label with quantity and card name
                string label = $"{deckCard.Quantity}x {cardName}";

                LogDebug($"[{NavigatorId}] Adding read-only deck card {cardNum}: {label}");

                AddElement(cardObj, label);
                addedObjects.Add(cardObj);
                cardNum++;
            }
        }

        /// <summary>
        /// Injects a virtual DeckBuilderInfo group into the grouped navigator.
        /// Contains informational elements (card count, mana curve, etc.) read from game UI.
        /// </summary>
        private void InjectDeckInfoGroup()
        {
            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return;

            var infoItems = DeckInfoProvider.GetDeckInfoElements();
            if (infoItems == null || infoItems.Count == 0)
            {
                MelonLogger.Msg($"[{NavigatorId}] DeckInfoProvider returned no info elements");
                return;
            }
            MelonLogger.Msg($"[{NavigatorId}] Injecting {infoItems.Count} deck info elements");

            var virtualElements = new List<GroupedElement>();
            foreach (var (label, text) in infoItems)
            {
                virtualElements.Add(new GroupedElement
                {
                    GameObject = null,
                    Label = $"{label}: {text}",
                    Group = ElementGroup.DeckBuilderInfo
                });
            }

            _groupedNavigator.AddVirtualGroup(
                ElementGroup.DeckBuilderInfo,
                virtualElements,
                insertAfter: ElementGroup.DeckBuilderDeckList
            );
        }

        /// <summary>
        /// Refreshes the labels of all DeckBuilderInfo virtual elements with fresh data.
        /// Called when user presses Enter on an info element to re-read from game UI.
        /// </summary>
        private void RefreshDeckInfoLabels()
        {
            var infoItems = DeckInfoProvider.GetDeckInfoElements();
            if (infoItems == null) return;

            for (int i = 0; i < infoItems.Count; i++)
            {
                var (label, text) = infoItems[i];
                _groupedNavigator.UpdateElementLabel(ElementGroup.DeckBuilderInfo, i, $"{label}: {text}");
            }

            LogDebug($"[{NavigatorId}] Refreshed {infoItems.Count} deck info labels");
        }

        /// <summary>
        /// Check if we're currently in the DeckBuilderInfo group with 2D sub-navigation active.
        /// </summary>
        private bool IsDeckInfoSubNavActive()
        {
            return _groupedNavigationEnabled && _groupedNavigator.IsActive
                && _groupedNavigator.Level == NavigationLevel.InsideGroup
                && _groupedNavigator.CurrentGroup.HasValue
                && _groupedNavigator.CurrentGroup.Value.Group == ElementGroup.DeckBuilderInfo
                && _deckInfoRows != null && _deckInfoRows.Count > 0;
        }

        /// <summary>
        /// Initialize the 2D sub-navigation state when entering the DeckBuilderInfo group.
        /// Loads row data from DeckInfoProvider.
        /// </summary>
        private void InitializeDeckInfoSubNav()
        {
            _deckInfoRows = DeckInfoProvider.GetDeckInfoRows();
            _deckInfoEntryIndex = 0;
            LogDebug($"[{NavigatorId}] Initialized DeckInfo sub-nav: {_deckInfoRows.Count} rows");
        }

        /// <summary>
        /// Handle Left/Right navigation within a DeckBuilderInfo row.
        /// Returns true if handled.
        /// </summary>
        private bool HandleDeckInfoEntryNavigation(bool isRight)
        {
            if (_deckInfoRows == null) return false;

            int rowIndex = _groupedNavigator.CurrentElementIndex;
            if (rowIndex < 0 || rowIndex >= _deckInfoRows.Count) return false;

            var entries = _deckInfoRows[rowIndex].entries;
            if (entries == null || entries.Count == 0) return false;

            if (isRight)
            {
                if (_deckInfoEntryIndex >= entries.Count - 1)
                {
                    _announcer.AnnounceVerbose(Models.Strings.EndOfList, Models.AnnouncementPriority.Normal);
                    return true;
                }
                _deckInfoEntryIndex++;
            }
            else
            {
                if (_deckInfoEntryIndex <= 0)
                {
                    _announcer.AnnounceVerbose(Models.Strings.BeginningOfList, Models.AnnouncementPriority.Normal);
                    return true;
                }
                _deckInfoEntryIndex--;
            }

            AnnounceDeckInfoEntry(includeRowName: false);
            return true;
        }

        /// <summary>
        /// Announce the current DeckBuilderInfo sub-entry.
        /// When includeRowName is true, prefixes with row label (e.g., "Cards. 35 von 60").
        /// </summary>
        private void AnnounceDeckInfoEntry(bool includeRowName)
        {
            if (_deckInfoRows == null) return;

            int rowIndex = _groupedNavigator.CurrentElementIndex;
            if (rowIndex < 0 || rowIndex >= _deckInfoRows.Count) return;

            var (label, entries) = _deckInfoRows[rowIndex];
            if (entries == null || entries.Count == 0) return;

            int entryIdx = Math.Min(_deckInfoEntryIndex, entries.Count - 1);
            string entryText = entries[entryIdx];

            string announcement;
            if (includeRowName)
                announcement = $"{label}. {entryText}";
            else
                announcement = entryText;

            _announcer.AnnounceInterrupt(announcement);
        }

        /// <summary>
        /// Refresh the 2D sub-navigation data for DeckBuilderInfo (re-reads from game UI).
        /// Preserves current row and entry position where possible.
        /// </summary>
        private void RefreshDeckInfoSubNav()
        {
            _deckInfoRows = DeckInfoProvider.GetDeckInfoRows();
            // Clamp entry index to new data bounds
            int rowIndex = _groupedNavigator.CurrentElementIndex;
            if (_deckInfoRows != null && rowIndex >= 0 && rowIndex < _deckInfoRows.Count)
            {
                var entries = _deckInfoRows[rowIndex].entries;
                if (_deckInfoEntryIndex >= entries.Count)
                    _deckInfoEntryIndex = Math.Max(0, entries.Count - 1);
            }
            // Also refresh the element labels for the group
            RefreshDeckInfoLabels();
        }
    }
}
