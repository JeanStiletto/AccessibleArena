using System;
using UnityEngine;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.ElementGrouping;
using System.Collections.Generic;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class GeneralMenuNavigator
    {
        // Page rescan: frame counter after page scroll (waits for animation + card data update)
        private int _pendingPageRescanFrames;

        // Packet selection sub-navigation state
        // Left/Right cycles info blocks for the current packet
        private List<CardInfoBlock> _packetBlocks;
        private int _packetBlockIndex;

        /// <summary>
        /// Navigate to the next or previous collection page using CardPoolAccessor.
        /// Announces "Page X of Y" and schedules rescan for updated card views.
        /// </summary>
        private bool ActivateCollectionPageButton(bool next)
        {
            Log.Nav(NavigatorId, $"Collection page navigation: {(next ? "Next" : "Previous")}");

            // Try direct CardPoolHolder API access
            var poolHolder = CardPoolAccessor.FindCardPoolHolder();
            if (poolHolder != null)
            {
                if (CardPoolAccessor.IsScrolling())
                {
                    Log.Nav(NavigatorId, $"Page scroll animation still in progress, ignoring");
                    return true; // Consume input but don't scroll
                }

                bool success = next
                    ? CardPoolAccessor.ScrollNext()
                    : CardPoolAccessor.ScrollPrevious();

                if (success)
                {
                    int currentPage = CardPoolAccessor.GetCurrentPageIndex() + 1; // 1-based for user
                    int totalPages = CardPoolAccessor.GetPageCount();
                    _announcer.Announce(Models.Strings.PageOf(currentPage, totalPages), Models.AnnouncementPriority.Normal);

                    // Save group state for restoration after rescan
                    // Reset element index so new page starts at first card
                    if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
                    {
                        _groupedNavigator.SaveCurrentGroupForRestore();
                        _groupedNavigator.ResetPendingElementIndex();
                    }

                    SchedulePageRescan();
                    return true;
                }
                else
                {
                    // At boundary
                    string edgeMsg = next ? "Last page" : "First page";
                    _announcer.Announce(edgeMsg, Models.AnnouncementPriority.Normal);
                    return true;
                }
            }

            // Fallback to old button-search method
            Log.Nav(NavigatorId, $"CardPoolAccessor unavailable, falling back to button search");
            return ActivateCollectionPageButtonFallback(next);
        }

        /// <summary>
        /// Schedule a rescan after page scroll animation completes.
        /// Uses frame counter with IsScrolling() short-circuit for responsiveness.
        /// </summary>
        private void SchedulePageRescan()
        {
            // 8 frames as safety floor (~430ms at 18fps game rate)
            // In practice, the IsScrolling() short-circuit in Update() fires after ~250ms
            // when the scroll animation completes, so the actual delay is usually shorter
            _pendingPageRescanFrames = 8;
        }

        // Section label to speak once the cross-page rescan has restored focus onto
        // the jump target (Ctrl+Page section jumps that land on another pool page)
        private string _pendingSectionAnnouncement;

        /// <summary>
        /// Ctrl+PageUp/PageDown: jump to section starts inside the deck builder's card
        /// groups. Collection: the game's own color-sort buckets (basic lands, mono
        /// colors, multicolor, colorless). Deck list / sideboard: mana-value runs with
        /// lands as the final section. Forward jumps to the next section start;
        /// backward first to the current section's start, then to the previous one.
        /// </summary>
        private bool HandleSectionJump(bool forward)
        {
            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return false;

            var groupInfo = _groupedNavigator.CurrentGroup;
            bool insideCardGroup = groupInfo?.Group.IsDeckBuilderCardGroup() == true
                && _groupedNavigator.CurrentElement != null
                && _groupedNavigator.CurrentElementIndex >= 0;

            if (!insideCardGroup)
            {
                _announcer.Announce(Strings.NoAlternateAction, AnnouncementPriority.Normal);
                return true;
            }

            if (groupInfo.Value.Group == ElementGroup.DeckBuilderCollection)
                return HandlePoolSectionJump(forward);

            return HandleListSectionJump(forward, groupInfo.Value.Group == ElementGroup.DeckBuilderSideboard);
        }

        /// <summary>
        /// Section jump within the collection pool. Works on the game's full sorted
        /// card list (all pages), so a jump may scroll to another page; focus is then
        /// restored onto the section's first card after the page rescan.
        /// </summary>
        private bool HandlePoolSectionJump(bool forward)
        {
            var poolHolder = CardPoolAccessor.FindCardPoolHolder();
            int pageSize = CardPoolAccessor.GetPageSize();
            var keys = DeckSectionProvider.GetPoolSectionKeys();

            if (poolHolder == null || pageSize <= 0 || keys.Count == 0)
            {
                _announcer.Announce(Strings.NoAlternateAction, AnnouncementPriority.Normal);
                return true;
            }

            if (CardPoolAccessor.IsScrolling())
                return true; // consume, same as plain page turns mid-animation

            int page = CardPoolAccessor.GetCurrentPageIndex();
            int current = Math.Min(page * pageSize + Math.Max(_groupedNavigator.CurrentElementIndex, 0), keys.Count - 1);

            int target = forward ? SectionJump.Next(keys, current) : SectionJump.Previous(keys, current);
            if (target < 0)
            {
                _announcer.AnnounceVerbose(forward ? Strings.EndOfList : Strings.BeginningOfList, AnnouncementPriority.Normal);
                return true;
            }

            string label = DeckSectionProvider.PoolSectionLabel(keys[target]);
            int targetPage = target / pageSize;
            int targetIndexInPage = target % pageSize;
            Log.Nav(NavigatorId, $"Pool section jump {(forward ? "forward" : "back")}: index {current} -> {target} (page {targetPage}, slot {targetIndexInPage}, '{label}')");

            if (targetPage == page)
            {
                int count = _groupedNavigator.CurrentGroup?.Count ?? 0;
                _groupedNavigator.JumpToElementByIndex(Math.Min(targetIndexInPage, Math.Max(count - 1, 0)));
                UpdateEventSystemSelectionForGroupedElement();
                UpdateCardNavigationForGroupedElement();
                _groupedNavigator.AnnounceCurrentElementWithPrefix(label);
                return true;
            }

            if (!CardPoolAccessor.ScrollToPage(targetPage))
                return true;

            _groupedNavigator.SaveCurrentGroupForRestore();
            _groupedNavigator.SetPendingElementIndex(targetIndexInPage);
            _pendingSectionAnnouncement = label;
            SchedulePageRescan();
            return true;
        }

        /// <summary>
        /// Section jump within the deck list or sideboard blade: one section per
        /// mana value, X spells after them, lands last (the blade's own sort order).
        /// </summary>
        private bool HandleListSectionJump(bool forward, bool sideboard)
        {
            var groupInfo = _groupedNavigator.CurrentGroup;
            var elements = groupInfo?.Elements;
            if (elements == null || elements.Count == 0)
            {
                _announcer.Announce(Strings.NoAlternateAction, AnnouncementPriority.Normal);
                return true;
            }

            // Map tile buttons (the group's element objects) to their GrpIds in one pass
            var cards = sideboard ? DeckCardProvider.GetSideboardCards() : DeckCardProvider.GetDeckListCards();
            var grpIdByTile = new Dictionary<GameObject, uint>();
            foreach (var card in cards)
            {
                if (card.IsValid && card.TileButton != null)
                    grpIdByTile[card.TileButton] = card.GrpId;
            }

            // One key per element; elements without card data (e.g. the empty-deck
            // placeholder) inherit their neighbor's key so they never fabricate a boundary
            var keys = new List<long>(elements.Count);
            long previous = long.MinValue;
            bool anyKey = false;
            foreach (var element in elements)
            {
                long key = previous;
                if (element.GameObject != null
                    && grpIdByTile.TryGetValue(element.GameObject, out uint grpId)
                    && DeckSectionProvider.TryGetDeckCardKey(grpId, out long cardKey))
                {
                    key = cardKey;
                    anyKey = true;
                }
                keys.Add(key);
                previous = key;
            }

            if (!anyKey)
            {
                _announcer.Announce(Strings.NoAlternateAction, AnnouncementPriority.Normal);
                return true;
            }

            int current = Math.Max(_groupedNavigator.CurrentElementIndex, 0);
            int target = forward ? SectionJump.Next(keys, current) : SectionJump.Previous(keys, current);
            if (target < 0)
            {
                _announcer.AnnounceVerbose(forward ? Strings.EndOfList : Strings.BeginningOfList, AnnouncementPriority.Normal);
                return true;
            }

            Log.Nav(NavigatorId, $"{(sideboard ? "Sideboard" : "Deck list")} section jump {(forward ? "forward" : "back")}: index {current} -> {target}");
            _groupedNavigator.JumpToElementByIndex(target);
            UpdateEventSystemSelectionForGroupedElement();
            UpdateCardNavigationForGroupedElement();
            string label = keys[target] == long.MinValue ? null : DeckSectionProvider.DeckSectionLabel(keys[target]);
            _groupedNavigator.AnnounceCurrentElementWithPrefix(label);
            return true;
        }

        /// <summary>
        /// Speak the pending "section: card" announcement after a cross-page section
        /// jump's rescan has restored focus. No-op when no jump is pending.
        /// </summary>
        private void AnnouncePendingSectionJump()
        {
            if (_pendingSectionAnnouncement == null) return;
            string label = _pendingSectionAnnouncement;
            _pendingSectionAnnouncement = null;

            UpdateEventSystemSelectionForGroupedElement();
            UpdateCardNavigationForGroupedElement();
            _groupedNavigator.AnnounceCurrentElementWithPrefix(label);
        }

        /// <summary>
        /// Fallback: search for page buttons by label/name when CardPoolAccessor is unavailable.
        /// </summary>
        private bool ActivateCollectionPageButtonFallback(bool next)
        {
            string targetLabel = next ? "Next" : "Previous";

            foreach (var element in _elements)
            {
                if (element.GameObject == null) continue;

                string label = element.Label?.ToLower() ?? "";
                string objName = element.GameObject.name.ToLower();

                bool isTarget = next
                    ? (label.Contains("next") || objName.Contains("next"))
                    : (label.Contains("previous") || label.Contains("prev") || objName.Contains("previous") || objName.Contains("prev"));

                if (isTarget && (label.Contains("navigation") || objName.Contains("navigation") || objName.Contains("arrow") || objName.Contains("page")))
                {
                    var result = UIActivator.Activate(element.GameObject);
                    if (result.Success)
                    {
                        _announcer.Announce(Models.Strings.PageLabel(targetLabel), Models.AnnouncementPriority.Normal);
                        TriggerRescan();
                        return true;
                    }
                }
            }

            var buttonNames = next
                ? new[] { "NextPageButton", "Next_Button", "ArrowRight", "NextArrow" }
                : new[] { "PreviousPageButton", "Previous_Button", "Prev_Button", "ArrowLeft", "PrevArrow" };

            foreach (var btnName in buttonNames)
            {
                var btn = GameObject.Find(btnName);
                if (btn != null && btn.activeInHierarchy)
                {
                    var result = UIActivator.Activate(btn);
                    if (result.Success)
                    {
                        _announcer.Announce(Models.Strings.PageLabel(targetLabel), Models.AnnouncementPriority.Normal);
                        TriggerRescan();
                        return true;
                    }
                }
            }

            Log.Nav(NavigatorId, $"No '{targetLabel}' page button found (fallback)");
            return false;
        }

        /// <summary>
        /// Check if we're in a context where cards should navigate with Left/Right arrows.
        /// This includes DeckBuilderCollection, DeckBuilderDeckList and similar card-grid contexts.
        /// </summary>
        private bool IsInCollectionCardContext()
        {
            // Check if current element is in any deck builder card group (collection, sideboard, or deck list)
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                var currentGroup = _groupedNavigator.CurrentGroup;
                if (currentGroup?.Group.IsDeckBuilderCardGroup() == true)
                    return true;
            }

            // Also check if current element is a card (for ungrouped mode)
            if (IsValidIndex && _elements[_currentIndex].GameObject != null)
            {
                var currentElement = _elements[_currentIndex].GameObject;
                if (CardDetector.IsCard(currentElement))
                {
                    // Check if parent hierarchy contains PoolHolder (collection cards)
                    Transform t = currentElement.transform;
                    while (t != null)
                    {
                        if (t.name.Contains("PoolHolder"))
                            return true;
                        t = t.parent;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if we're in packet selection context (Jump In) where Left/Right navigates packets.
        /// </summary>
        private bool IsInPacketSelectionContext()
        {
            if (_activeContentController != "PacketSelectContentController")
                return false;

            if (!_groupedNavigationEnabled || !_groupedNavigator.IsActive)
                return false;

            var currentElement = _groupedNavigator.CurrentElement;
            if (currentElement == null) return false;

            var go = currentElement.Value.GameObject;
            return go != null && EventAccessor.IsInsideJumpStartPacket(go);
        }

        /// <summary>
        /// Activate a filter option by index (0-9 for options 1-10).
        /// Number keys 1-9 activate options 1-9, 0 activates option 10.
        /// </summary>
        private bool ActivateFilterByIndex(int index)
        {
            var filterElement = _groupedNavigator.GetElementFromGroup(ElementGroup.Filters, index);
            if (filterElement != null)
            {
                // Get the element label for announcement
                var filterGroup = _groupedNavigator.GetGroupByType(ElementGroup.Filters);
                string label = "Filter";
                if (filterGroup.HasValue && index < filterGroup.Value.Count)
                {
                    label = filterGroup.Value.Elements[index].Label ?? "Filter";
                }

                Log.Nav(NavigatorId, $"Activating filter {index + 1}: {label}");

                // Activate the filter
                var result = UIActivator.Activate(filterElement);
                if (result.Success)
                {
                    // Check if it's a toggle to announce the new state
                    var toggle = filterElement.GetComponent<UnityEngine.UI.Toggle>();
                    if (toggle != null)
                    {
                        // Toggle state will be inverted after activation
                        string state = toggle.isOn ? "off" : "on"; // Inverted because it hasn't changed yet
                        _announcer.Announce(Models.Strings.FilterLabel(label, state), Models.AnnouncementPriority.High);
                    }
                    else
                    {
                        _announcer.Announce(Models.Strings.Activated(label), Models.AnnouncementPriority.High);
                    }

                    // Trigger rescan to update UI state
                    TriggerRescan();
                    return true;
                }
            }
            else
            {
                // No filter at this index
                int filterCount = _groupedNavigator.GetGroupElementCount(ElementGroup.Filters);
                if (filterCount > 0)
                {
                    _announcer.Announce(Models.Strings.NoFilter(index + 1, filterCount), Models.AnnouncementPriority.Normal);
                }
                else
                {
                    _announcer.Announce(Models.Strings.NoFiltersAvailable, Models.AnnouncementPriority.Normal);
                }
            }

            return false;
        }

        /// <summary>
        /// Refresh the info blocks for the current packet element.
        /// Called when grouped navigator moves to a packet element.
        /// </summary>
        private void RefreshPacketBlocks(GameObject element)
        {
            _packetBlocks = EventAccessor.GetPacketInfoBlocks(element);
            _packetBlockIndex = 0;
            Log.Nav(NavigatorId, $"Packet blocks: {_packetBlocks?.Count ?? 0}");
        }

        /// <summary>
        /// Handle Left/Right navigation through packet info blocks.
        /// </summary>
        private void HandlePacketBlockNavigation(bool isRight)
        {
            if (_packetBlocks == null || _packetBlocks.Count == 0)
            {
                _announcer.Announce(Strings.NoAlternateAction, AnnouncementPriority.Normal);
                return;
            }

            if (isRight)
            {
                if (_packetBlockIndex >= _packetBlocks.Count - 1)
                {
                    AnnouncePacketBlock();
                    _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                    return;
                }
                _packetBlockIndex++;
            }
            else
            {
                if (_packetBlockIndex <= 0)
                {
                    AnnouncePacketBlock();
                    _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                    return;
                }
                _packetBlockIndex--;
            }

            AnnouncePacketBlock();
        }

        /// <summary>
        /// Announce the current packet block.
        /// </summary>
        private void AnnouncePacketBlock()
        {
            if (_packetBlocks == null || _packetBlocks.Count == 0) return;
            if (_packetBlockIndex < 0 || _packetBlockIndex >= _packetBlocks.Count) return;

            var block = _packetBlocks[_packetBlockIndex];
            _announcer.AnnounceInterrupt($"{block.Label}: {block.Content}");
        }
    }
}
