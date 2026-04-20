using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class StoreNavigator
    {
        #region Details Detection

        private bool HasItemDetails(MonoBehaviour storeItemBase)
        {
            // Check tooltip mTerm
            if (HasTooltipText(storeItemBase))
                return true;

            // Check for StoreDisplayPreconDeck or StoreDisplayCardViewBundle child
            if (_storeItemDisplayType != null)
            {
                var display = GetItemDisplay(storeItemBase);
                if (display != null)
                {
                    var displayType = display.GetType();
                    if ((_storeDisplayPreconDeckType != null && _storeDisplayPreconDeckType.IsAssignableFrom(displayType)) ||
                        (_storeDisplayCardViewBundleType != null && _storeDisplayCardViewBundleType.IsAssignableFrom(displayType)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasTooltipText(MonoBehaviour storeItemBase)
        {
            if (_tooltipTriggerField == null || _locStringField == null || _locStringMTermField == null)
                return false;

            try
            {
                var tooltip = _tooltipTriggerField.GetValue(storeItemBase);
                if (tooltip == null) return false;

                var locString = _locStringField.GetValue(tooltip);
                if (locString == null) return false;

                string mTerm = _locStringMTermField.GetValue(locString) as string;
                return !string.IsNullOrEmpty(mTerm) && mTerm != "MainNav/General/Empty_String";
            }
            catch { return false; }
        }

        private MonoBehaviour GetItemDisplay(MonoBehaviour storeItemBase)
        {
            if (_itemDisplayField == null) return null;
            try
            {
                return _itemDisplayField.GetValue(storeItemBase) as MonoBehaviour;
            }
            catch { return null; }
        }

        #endregion

        #region Details View Open/Close

        private void OpenDetailsView(ItemInfo item)
        {
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsCardIndex = 0;
            _detailsBlockIndex = 0;

            // Extract tooltip description
            _detailsDescription = ExtractTooltipDescription(item.StoreItemBase);

            // Extract card list from display
            ExtractCardEntries(item.StoreItemBase, _detailsCards);

            if (string.IsNullOrEmpty(_detailsDescription) && _detailsCards.Count == 0)
            {
                _announcer.Announce(Strings.NoDetailsAvailable, AnnouncementPriority.Normal);
                return;
            }

            _isDetailsViewActive = true;

            // Build announcement
            var parts = new List<string>();
            parts.Add("Details");

            if (!string.IsNullOrEmpty(_detailsDescription))
                parts.Add(_detailsDescription);

            if (_detailsCards.Count > 0)
            {
                string cardCount = _detailsCards.Count == 1 ? "1 card" : $"{_detailsCards.Count} cards";
                parts.Add(cardCount);
                // Announce first card
                parts.Add(FormatCardAnnouncement(_detailsCards[0], 0));
            }

            _announcer.AnnounceInterrupt(string.Join(". ", parts));
            MelonLogger.Msg($"[Store] Opened details view: {_detailsCards.Count} cards, description={!string.IsNullOrEmpty(_detailsDescription)}");
        }

        private void CloseDetailsView()
        {
            // Close extended info if open
            AccessibleArenaMod.Instance?.ExtendedInfoNavigator?.Close();

            _isDetailsViewActive = false;
            _detailsCards.Clear();
            _detailsCardBlocks.Clear();
            _detailsDescription = null;
            _detailsCardIndex = 0;
            _detailsBlockIndex = 0;

            // Re-announce current item
            AnnounceCurrentElement();
        }

        #endregion

        #region Details Extraction

        private string ExtractTooltipDescription(MonoBehaviour storeItemBase)
        {
            if (_tooltipTriggerField == null || _locStringField == null ||
                _locStringMTermField == null || _locStringToStringMethod == null)
                return null;

            try
            {
                var tooltip = _tooltipTriggerField.GetValue(storeItemBase);
                if (tooltip == null) return null;

                var locString = _locStringField.GetValue(tooltip);
                if (locString == null) return null;

                string mTerm = _locStringMTermField.GetValue(locString) as string;
                if (string.IsNullOrEmpty(mTerm) || mTerm == "MainNav/General/Empty_String")
                    return null;

                // Call ToString() on the LocalizedString struct which resolves the term
                string resolved = _locStringToStringMethod.Invoke(locString, null) as string;
                if (!string.IsNullOrEmpty(resolved) && !resolved.StartsWith("$"))
                {
                    // Clean rich text tags
                    resolved = UITextExtractor.StripRichText(resolved).Trim();
                    return resolved;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Store] Error extracting tooltip description: {ex.Message}");
            }

            return null;
        }

        private void ExtractCardEntries(MonoBehaviour storeItemBase, List<DetailCardEntry> entries)
        {
            var display = GetItemDisplay(storeItemBase);
            if (display == null) return;

            var displayType = display.GetType();

            try
            {
                // PreconDeck path: CardData property returns List<CardDataForTile>
                if (_storeDisplayPreconDeckType != null &&
                    _storeDisplayPreconDeckType.IsAssignableFrom(displayType) &&
                    _preconCardDataProp != null)
                {
                    var cardDataList = _preconCardDataProp.GetValue(display);
                    if (cardDataList is System.Collections.IList list)
                        ExtractFromCardDataList(list, entries);
                }
                // CardViewBundle path: BundleCardViews returns List<StoreCardView>
                else if (_storeDisplayCardViewBundleType != null &&
                         _storeDisplayCardViewBundleType.IsAssignableFrom(displayType) &&
                         _bundleCardViewsProp != null)
                {
                    var cardViews = _bundleCardViewsProp.GetValue(display);
                    if (cardViews is System.Collections.IList viewList)
                        ExtractFromBundleCardViews(viewList, entries);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[Store] Error extracting card entries: {ex.Message}");
            }
        }

        private void ExtractFromCardDataList(System.Collections.IList list, List<DetailCardEntry> entries)
        {
            if (_cardDataForTileCardProp == null || _cardDataForTileQuantityProp == null || _cardDataGrpIdProp == null)
                return;

            foreach (var item in list)
            {
                try
                {
                    var cardData = _cardDataForTileCardProp.GetValue(item);
                    if (cardData == null) continue;

                    uint grpId = (uint)_cardDataGrpIdProp.GetValue(cardData);
                    if (grpId == 0) continue;

                    int quantity = (int)_cardDataForTileQuantityProp.GetValue(item);
                    string name = CardModelProvider.GetNameFromGrpId(grpId);
                    if (string.IsNullOrEmpty(name))
                        name = $"Card #{grpId}";

                    string manaCost = null;
                    if (_cardDataManaTextProp != null)
                    {
                        try { manaCost = _cardDataManaTextProp.GetValue(cardData) as string; }
                        catch { /* Property may not exist on all types */ }
                    }

                    entries.Add(new DetailCardEntry
                    {
                        GrpId = grpId,
                        Quantity = quantity,
                        Name = name,
                        ManaCost = !string.IsNullOrEmpty(manaCost) ? CardModelProvider.ParseManaSymbolsInText(manaCost) : null,
                        CardDataObj = cardData
                    });
                }
                catch { /* Reflection may fail on different game versions */ }
            }
        }

        private void ExtractFromBundleCardViews(System.Collections.IList viewList, List<DetailCardEntry> entries)
        {
            if (_cardDataGrpIdProp == null) return;

            foreach (var view in viewList)
            {
                try
                {
                    var viewMb = view as MonoBehaviour;
                    if (viewMb == null || !viewMb.gameObject.activeInHierarchy) continue;

                    var viewType = viewMb.GetType();
                    var cardProp = viewType.GetProperty("Card", PublicInstance);
                    if (cardProp == null) continue;

                    var cardData = cardProp.GetValue(viewMb);
                    if (cardData == null) continue;

                    uint grpId = (uint)_cardDataGrpIdProp.GetValue(cardData);
                    if (grpId == 0) continue;

                    string name = CardModelProvider.GetNameFromGrpId(grpId);
                    if (string.IsNullOrEmpty(name))
                        name = $"Card #{grpId}";

                    string manaCost = null;
                    if (_cardDataManaTextProp != null)
                    {
                        try { manaCost = _cardDataManaTextProp.GetValue(cardData) as string; }
                        catch { /* Property may not exist on all types */ }
                    }

                    entries.Add(new DetailCardEntry
                    {
                        GrpId = grpId,
                        Quantity = 1,
                        Name = name,
                        ManaCost = !string.IsNullOrEmpty(manaCost) ? CardModelProvider.ParseManaSymbolsInText(manaCost) : null,
                        CardDataObj = cardData
                    });
                }
                catch { /* Reflection may fail on different game versions */ }
            }
        }

        private string FormatCardAnnouncement(DetailCardEntry card, int index)
        {
            var parts = new List<string>();
            parts.Add(card.Name);
            if (card.Quantity > 1)
                parts.Add($"times {card.Quantity}");
            if (!string.IsNullOrEmpty(card.ManaCost))
                parts.Add(card.ManaCost);
            string pos = Strings.PositionOf(index + 1, _detailsCards.Count);
            if (pos != "") parts.Add(pos);
            return string.Join(", ", parts);
        }

        #endregion

        #region Details Input

        private void HandleDetailsInput()
        {
            // Extended info navigator takes over when active (I key menu)
            var extInfoNav = AccessibleArenaMod.Instance?.ExtendedInfoNavigator;
            if (extInfoNav != null && extInfoNav.IsActive)
            {
                extInfoNav.HandleInput();
                return;
            }

            // I key: open extended card info (keywords, linked faces, tokens)
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (extInfoNav != null && _detailsCards.Count > 0 &&
                    _detailsCardIndex >= 0 && _detailsCardIndex < _detailsCards.Count)
                {
                    extInfoNav.Open(_detailsCards[_detailsCardIndex].GrpId);
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.NoCardToInspect);
                }
                return;
            }

            // Left/Right: navigate between cards (hold-to-repeat)
            if (_holdRepeater.Check(KeyCode.LeftArrow, () => {
                int b = _detailsCardIndex; MoveDetailsCard(-1); return _detailsCardIndex != b;
            })) return;

            if (_holdRepeater.Check(KeyCode.RightArrow, () => {
                int b = _detailsCardIndex; MoveDetailsCard(1); return _detailsCardIndex != b;
            })) return;

            // Up/Down: navigate card info blocks (hold-to-repeat)
            if (_holdRepeater.Check(KeyCode.UpArrow, () => {
                int b = _detailsBlockIndex; MoveDetailsBlock(-1); return _detailsBlockIndex != b;
            })) return;

            if (_holdRepeater.Check(KeyCode.DownArrow, () => {
                int b = _detailsBlockIndex; MoveDetailsBlock(1); return _detailsBlockIndex != b;
            })) return;

            // Home/End: jump to first/last card
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_detailsCards.Count > 0 && _detailsCardIndex != 0)
                {
                    _detailsCardIndex = 0;
                    _detailsCardBlocks.Clear();
                    _detailsBlockIndex = 0;
                    _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[0], 0));
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_detailsCards.Count > 0 && _detailsCardIndex != _detailsCards.Count - 1)
                {
                    _detailsCardIndex = _detailsCards.Count - 1;
                    _detailsCardBlocks.Clear();
                    _detailsBlockIndex = 0;
                    _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[_detailsCardIndex], _detailsCardIndex));
                }
                return;
            }

            // Tab/Shift+Tab: navigate cards like Left/Right
            if (InputManager.GetKeyDownAndConsume(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                MoveDetailsCard(shift ? -1 : 1);
                return;
            }

            // Enter/Space: re-read current card or description
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool spacePressed = InputManager.GetKeyDownAndConsume(KeyCode.Space);
            if (enterPressed || spacePressed)
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                if (_detailsCards.Count > 0 && _detailsCardIndex >= 0 && _detailsCardIndex < _detailsCards.Count)
                    _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[_detailsCardIndex], _detailsCardIndex));
                else if (!string.IsNullOrEmpty(_detailsDescription))
                    _announcer.AnnounceInterrupt(_detailsDescription);
                return;
            }

            // Backspace: close details view
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                CloseDetailsView();
                return;
            }
        }

        private void MoveDetailsCard(int direction)
        {
            if (_detailsCards.Count == 0)
            {
                if (!string.IsNullOrEmpty(_detailsDescription))
                    _announcer.AnnounceInterrupt(_detailsDescription);
                return;
            }

            int newIndex = _detailsCardIndex + direction;

            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _detailsCards.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _detailsCardIndex = newIndex;
            _detailsCardBlocks.Clear();
            _detailsBlockIndex = 0;
            _announcer.AnnounceInterrupt(FormatCardAnnouncement(_detailsCards[_detailsCardIndex], _detailsCardIndex));
        }

        private void MoveDetailsBlock(int direction)
        {
            if (_detailsCards.Count == 0) return;

            // Lazy-load card info blocks on first Up/Down press
            if (_detailsCardBlocks.Count == 0)
            {
                var card = _detailsCards[_detailsCardIndex];
                CardInfo info = default;
                if (card.CardDataObj != null)
                {
                    info = CardModelProvider.ExtractCardInfoFromObject(card.CardDataObj);
                }
                if (!info.IsValid)
                {
                    var cardInfo = CardModelProvider.GetCardInfoFromGrpId(card.GrpId);
                    if (cardInfo.HasValue)
                        info = cardInfo.Value;
                }

                if (info.IsValid)
                {
                    info.Quantity = card.Quantity;
                    _detailsCardBlocks = CardDetector.BuildInfoBlocks(info);
                }

                if (_detailsCardBlocks.Count == 0)
                {
                    _announcer.Announce(Strings.NoCardDetails, AnnouncementPriority.Normal);
                    return;
                }

                // Start at first block for Down, last for Up
                _detailsBlockIndex = direction > 0 ? 0 : _detailsCardBlocks.Count - 1;
                AnnounceDetailsBlock();
                return;
            }

            int newIndex = _detailsBlockIndex + direction;

            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return;
            }

            if (newIndex >= _detailsCardBlocks.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return;
            }

            _detailsBlockIndex = newIndex;
            AnnounceDetailsBlock();
        }

        private void AnnounceDetailsBlock()
        {
            if (_detailsBlockIndex < 0 || _detailsBlockIndex >= _detailsCardBlocks.Count) return;

            var block = _detailsCardBlocks[_detailsBlockIndex];
            bool showLabel = !block.IsVerbose ||
                             (AccessibleArenaMod.Instance?.Settings?.VerboseAnnouncements != false);
            _announcer.AnnounceInterrupt(showLabel ? $"{block.Label}: {block.Content}" : block.Content);
        }

        #endregion
    }
}
