using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    public partial class BrowserNavigator
    {
        // SelectGroup browser state (Fact or Fiction pile selection)
        private bool _isSelectGroup;
        private object _selectGroupBrowserRef;
        private List<object> _pile1CDCs = new List<object>(); // top group CDCs
        private List<object> _pile2CDCs = new List<object>(); // bottom group CDCs
        private Dictionary<GameObject, (int pile, int indexInPile, int pileTotal)> _selectGroupCardMap
            = new Dictionary<GameObject, (int, int, int)>();

        /// <summary>
        /// Caches state for the SelectGroup browser: browser ref, pile 1 and pile 2 CDC lists.
        /// </summary>
        private void CacheSelectGroupState()
        {
            try
            {
                if (!TryGetCurrentBrowser("SelectGroup", out var currentBrowser))
                    return;
                if (!currentBrowser.GetType().Name.Contains("SelectGroup"))
                {
                    MelonLogger.Msg($"[BrowserNavigator] SelectGroup: CurrentBrowser is {currentBrowser.GetType().Name}");
                    return;
                }

                _selectGroupBrowserRef = currentBrowser;
                MelonLogger.Msg($"[BrowserNavigator] SelectGroup: Found browser {currentBrowser.GetType().Name}");

                // Call GetCardGroups() → List<List<DuelScene_CDC>>
                var getCardGroupsMethod = currentBrowser.GetType().GetMethod("GetCardGroups", ReflFlags);
                if (getCardGroupsMethod == null)
                {
                    MelonLogger.Msg("[BrowserNavigator] SelectGroup: GetCardGroups method not found");
                    return;
                }

                var groups = getCardGroupsMethod.Invoke(currentBrowser, null);
                if (groups is IList groupList && groupList.Count >= 2)
                {
                    var pile1 = groupList[0] as IList;
                    var pile2 = groupList[1] as IList;

                    _pile1CDCs.Clear();
                    _pile2CDCs.Clear();

                    if (pile1 != null)
                    {
                        foreach (var cdc in pile1)
                            _pile1CDCs.Add(cdc);
                    }
                    if (pile2 != null)
                    {
                        foreach (var cdc in pile2)
                            _pile2CDCs.Add(cdc);
                    }

                    MelonLogger.Msg($"[BrowserNavigator] SelectGroup: Pile 1 has {_pile1CDCs.Count} cards, Pile 2 has {_pile2CDCs.Count} cards");
                }
                else
                {
                    MelonLogger.Msg("[BrowserNavigator] SelectGroup: GetCardGroups returned unexpected result");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] SelectGroup cache error: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers cards from cached SelectGroup pile CDCs.
        /// Includes face-down cards that would normally be filtered out.
        /// Orders: pile 1 cards first, then pile 2 cards.
        /// Also populates _selectGroupCardMap for fast pile lookup.
        /// </summary>
        private void DiscoverSelectGroupCards()
        {
            _selectGroupCardMap.Clear();

            // Process pile 1 CDCs
            for (int i = 0; i < _pile1CDCs.Count; i++)
            {
                AddSelectGroupCard(_pile1CDCs[i], 1, i, _pile1CDCs.Count);
            }
            // Process pile 2 CDCs
            for (int i = 0; i < _pile2CDCs.Count; i++)
            {
                AddSelectGroupCard(_pile2CDCs[i], 2, i, _pile2CDCs.Count);
            }
        }

        private void AddSelectGroupCard(object cdc, int pileNumber, int indexInPile, int pileTotal)
        {
            if (cdc == null) return;

            try
            {
                // CDC is a MonoBehaviour (DuelScene_CDC), get its gameObject
                var goProp = cdc.GetType().GetProperty("gameObject", BindingFlags.Public | BindingFlags.Instance);
                var go = goProp?.GetValue(cdc) as GameObject;
                if (go == null) return;

                if (!go.activeInHierarchy) return;
                if (BrowserDetector.IsDuplicateCard(go, _browserCards)) return;

                string pileLabel = pileNumber == 1 ? "Pile 1" : "Pile 2";
                string cardName = CardDetector.GetCardName(go);
                // Include face-down cards (don't filter by IsValidCardName)
                if (string.IsNullOrEmpty(cardName) || !BrowserDetector.IsValidCardName(cardName))
                {
                    MelonLogger.Msg($"[BrowserNavigator] SelectGroup: Face-down card in {pileLabel}: {go.name}");
                }
                else
                {
                    MelonLogger.Msg($"[BrowserNavigator] SelectGroup: Card in {pileLabel}: {cardName}");
                }

                _browserCards.Add(go);
                _selectGroupCardMap[go] = (pileNumber, indexInPile + 1, pileTotal);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BrowserNavigator] SelectGroup: Error adding card from pile {pileNumber}: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces a card in the SelectGroup browser with pile membership.
        /// </summary>
        private void AnnounceSelectGroupCard(GameObject card)
        {
            if (card == null) return;

            // Try to get card name; use face-down label if extraction fails
            var info = CardDetector.ExtractCardInfo(card);
            string cardName = info.Name;
            bool isFaceDown = string.IsNullOrEmpty(cardName) || !BrowserDetector.IsValidCardName(cardName);
            if (isFaceDown)
            {
                cardName = Strings.SelectGroupFaceDown;
            }

            // Look up pile membership from cached map
            string pileName;
            int pileIndex = 1, pileTotal = 1;
            if (_selectGroupCardMap.TryGetValue(card, out var pileInfo))
            {
                pileName = pileInfo.pile == 1 ? Strings.SelectGroupPile1 : Strings.SelectGroupPile2;
                pileIndex = pileInfo.indexInPile;
                pileTotal = pileInfo.pileTotal;
            }
            else
            {
                pileName = Strings.SelectGroupPile1; // fallback
            }

            string announcement = Strings.SelectGroupCardInPile(cardName, pileName, pileIndex, pileTotal);

            _announcer.Announce(announcement, AnnouncementPriority.High);

            // Prepare card details for Up/Down navigation (if face-up)
            if (!isFaceDown)
            {
                AccessibleArenaMod.Instance?.CardNavigator?.PrepareForCard(card, ZoneType.Library);
            }
        }
    }
}
