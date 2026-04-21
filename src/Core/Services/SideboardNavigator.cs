using UnityEngine;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine.EventSystems;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using static AccessibleArena.Core.Constants.SceneNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the Bo3 sideboard screen that appears between games in DuelScene.
    /// Detects SideboardInterface and provides zone-based navigation:
    ///   C = Pool (cards available to swap in)
    ///   D = Deck (current main deck cards)
    ///   Enter = Move card (add to deck / remove from deck)
    ///   Space = Submit sideboard (click Done)
    ///   T = Timer, L = Match score
    ///   PageUp/PageDown = Pool page navigation
    ///   Backspace = Toggle battlefield/deck view
    /// Priority 72 preempts DuelNavigator (70) when sideboard is active.
    /// </summary>
    public class SideboardNavigator : BaseNavigator
    {
        private bool _isWatching;
        private MonoBehaviour _sideboardInterface;
        private MonoBehaviour _navBar;

        // Navigation zones
        private enum SideboardZone { Pool, Deck, Info }
        private SideboardZone _currentZone = SideboardZone.Pool;
        private int _poolIndex = -1;
        private int _deckIndex = -1;

        // Cached pool/deck card lists
        private List<GameObject> _poolCards = new List<GameObject>();
        private List<DeckCardProvider.DeckListCardInfo> _deckCards = new List<DeckCardProvider.DeckListCardInfo>();

        // Post-activation rescan
        private int _rescanFrameCountdown = -1;
        private const int RescanDelayFrames = 8;

        private sealed class SideboardHandles
        {
            public FieldInfo DeckBuilder;
            public FieldInfo ShowHideToggle;
            public FieldInfo NavBar;
            public FieldInfo IntroText;
        }

        private sealed class DeckBuilderHandles
        {
            public FieldInfo DoneButton;
        }

        private sealed class NavBarHandles
        {
            public FieldInfo PlayerName;
            public FieldInfo OpponentName;
            public FieldInfo PlayerWinPips;
            public FieldInfo OpponentWinPips;
            public FieldInfo TimerText;
        }

        private static readonly ReflectionCache<SideboardHandles> _sideboardCache = new ReflectionCache<SideboardHandles>(
            builder: t => new SideboardHandles
            {
                DeckBuilder = t.GetField("_deckBuilder", PrivateInstance),
                ShowHideToggle = t.GetField("_showHideToggle", PrivateInstance),
                NavBar = t.GetField("_navBar", PrivateInstance),
                IntroText = t.GetField("_sideboardIntroText", PrivateInstance),
            },
            validator: h => h.DeckBuilder != null && h.NavBar != null,
            logTag: "SideboardNavigator",
            logSubject: "SideboardInterface");

        private static readonly ReflectionCache<DeckBuilderHandles> _deckBuilderCache = new ReflectionCache<DeckBuilderHandles>(
            builder: t => new DeckBuilderHandles
            {
                DoneButton = t.GetField("_doneButton", PrivateInstance),
            },
            validator: h => h.DoneButton != null,
            logTag: "SideboardNavigator",
            logSubject: "DeckBuilderWidget");

        private static readonly ReflectionCache<NavBarHandles> _navBarCache = new ReflectionCache<NavBarHandles>(
            builder: t => new NavBarHandles
            {
                PlayerName = t.GetField("PlayerName", PublicInstance),
                OpponentName = t.GetField("OpponentName", PublicInstance),
                PlayerWinPips = t.GetField("PlayerWinPips", PublicInstance),
                OpponentWinPips = t.GetField("OpponentWinPips", PublicInstance),
                TimerText = t.GetField("TimerText", PublicInstance),
            },
            validator: h => h.PlayerName != null && h.OpponentName != null,
            logTag: "SideboardNavigator",
            logSubject: "SideboardNavBar");

        public override string NavigatorId => "Sideboard";
        public override string ScreenName => Strings.ScreenSideboard;
        public override int Priority => 72;
        protected override bool AcceptSpaceKey => false;
        protected override bool SupportsLetterNavigation => false;

        public SideboardNavigator(IAnnouncementService announcer) : base(announcer) { }

        /// <summary>
        /// Called by AccessibleArenaMod when DuelScene loads. Enables detection.
        /// </summary>
        public void OnDuelSceneLoaded()
        {
            Log.Msg("{NavigatorId}", $"DuelScene loaded - watching for sideboard");
            _isWatching = true;
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (sceneName != DuelScene)
            {
                _isWatching = false;
                _sideboardInterface = null;
                _navBar = null;
            }
            base.OnSceneChanged(sceneName);
        }

        protected override bool DetectScreen()
        {
            if (!_isWatching) return false;
            return FindSideboardInterface();
        }

        protected override bool ValidateElements()
        {
            if (_sideboardInterface == null) return false;
            try
            {
                return _sideboardInterface.gameObject != null
                    && _sideboardInterface.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _currentZone = SideboardZone.Pool;
            _poolIndex = -1;
            _deckIndex = -1;
            CardPoolAccessor.ClearCache();
            DeckCardProvider.ClearDeckListCache();
        }

        protected override void OnDeactivating()
        {
            base.OnDeactivating();
            _sideboardInterface = null;
            _navBar = null;
            _poolCards.Clear();
            _deckCards.Clear();
        }

        public override string GetTutorialHint() => LocaleManager.Instance.Get("SideboardHint");

        protected override string GetActivationAnnouncement()
        {
            string introText = GetIntroText();
            string playerName = GetPlayerName();
            int playerWins = GetPlayerWins();
            string opponentName = GetOpponentName();
            int opponentWins = GetOpponentWins();

            RefreshPoolCards();
            RefreshDeckCards();
            int poolCount = _poolCards.Count;
            int deckCount = _deckCards.Count;

            string core = Strings.Sideboard_Activated(playerName, playerWins, opponentName, opponentWins, poolCount, deckCount);
            if (!string.IsNullOrEmpty(introText))
                core = $"{introText}. {core}";
            return Strings.WithHint(core, "SideboardHint");
        }

        protected override void DiscoverElements()
        {
            // SideboardNavigator uses custom zone navigation, not BaseNavigator's element list.
            // Add a single dummy element so ValidateElements doesn't fail on empty list.
            if (_sideboardInterface != null)
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = _sideboardInterface.gameObject,
                    Label = ScreenName
                });
            }
        }

        protected override string GetElementAnnouncement(int index)
        {
            // We handle all announcements through zone navigation
            return GetCurrentCardAnnouncement();
        }

        #region Custom Input

        public override void Update()
        {
            if (!_isActive)
            {
                base.Update();
                return;
            }

            // Handle post-card-move rescan
            if (_rescanFrameCountdown > 0)
            {
                _rescanFrameCountdown--;
                if (_rescanFrameCountdown == 0)
                {
                    _rescanFrameCountdown = -1;
                    OnPostMoveRescan();
                }
            }

            // Validate sideboard is still active
            if (!ValidateElements())
            {
                Deactivate();
                return;
            }

            HandleInput();
        }

        private new void HandleInput()
        {
            // Prevent Unity's EventSystem from navigating raw buttons with arrow keys
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                eventSystem.SetSelectedGameObject(null);

            // Card info navigation (Up/Down for detail blocks)
            var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNav != null && cardNav.IsActive)
            {
                if (cardNav.HandleInput()) return;
            }

            // Zone shortcuts
            if (Input.GetKeyDown(KeyCode.C))
            {
                NavigateToZone(SideboardZone.Pool);
                return;
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                NavigateToZone(SideboardZone.Deck);
                return;
            }

            // Timer
            if (Input.GetKeyDown(KeyCode.T))
            {
                AnnounceTimer();
                return;
            }

            // Score
            if (Input.GetKeyDown(KeyCode.L))
            {
                AnnounceScore();
                return;
            }

            // Navigation within zone
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                NavigateInZone(1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                NavigateInZone(-1);
                return;
            }

            // Home/End
            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpToZoneEdge(first: true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                JumpToZoneEdge(first: false);
                return;
            }

            // Page navigation for pool
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                ScrollPoolPage(next: true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                ScrollPoolPage(next: false);
                return;
            }

            // Enter = activate card (move between pool and deck)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateCurrentCard();
                return;
            }

            // Up/Down = card detail blocks
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                ActivateCardDetails();
                return;
            }

            // Space = submit sideboard (click Done)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SubmitSideboard();
                return;
            }

            // Backspace = toggle battlefield view
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ToggleBattlefieldView();
                return;
            }

            // Tab = cycle between zones (Pool -> Deck -> Info -> Pool)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                SideboardZone nextZone;
                if (shift)
                {
                    nextZone = _currentZone == SideboardZone.Pool ? SideboardZone.Info
                             : _currentZone == SideboardZone.Deck ? SideboardZone.Pool
                             : SideboardZone.Deck;
                }
                else
                {
                    nextZone = _currentZone == SideboardZone.Pool ? SideboardZone.Deck
                             : _currentZone == SideboardZone.Deck ? SideboardZone.Info
                             : SideboardZone.Pool;
                }
                NavigateToZone(nextZone);
                return;
            }
        }

        #endregion

        #region Zone Navigation

        private void NavigateToZone(SideboardZone zone)
        {
            _currentZone = zone;

            if (zone == SideboardZone.Pool)
            {
                RefreshPoolCards();
                if (_poolCards.Count == 0)
                {
                    _announcer.AnnounceInterrupt(Strings.ZoneEmpty(Strings.Sideboard_PoolZone(0)));
                    return;
                }
                _poolIndex = Math.Max(0, Math.Min(_poolIndex, _poolCards.Count - 1));
                if (_poolIndex < 0) _poolIndex = 0;
                AnnounceZoneEntry(zone);
            }
            else if (zone == SideboardZone.Deck)
            {
                RefreshDeckCards();
                if (_deckCards.Count == 0)
                {
                    _announcer.AnnounceInterrupt(Strings.ZoneEmpty(Strings.Sideboard_DeckZone(0)));
                    return;
                }
                _deckIndex = Math.Max(0, Math.Min(_deckIndex, _deckCards.Count - 1));
                if (_deckIndex < 0) _deckIndex = 0;
                AnnounceZoneEntry(zone);
            }
            else // Info
            {
                AnnounceInfoZone();
            }
        }

        private void AnnounceZoneEntry(SideboardZone zone)
        {
            string zoneName;
            int count;
            if (zone == SideboardZone.Pool)
            {
                count = _poolCards.Count;
                zoneName = Strings.Sideboard_PoolZone(count);
            }
            else
            {
                count = _deckCards.Count;
                zoneName = Strings.Sideboard_DeckZone(count);
            }
            string cardAnn = GetCurrentCardAnnouncement();
            _announcer.AnnounceInterrupt($"{zoneName}. {cardAnn}");
            ActivateCardInfoForCurrent();
        }

        private void NavigateInZone(int direction)
        {
            if (_currentZone == SideboardZone.Pool)
            {
                RefreshPoolCards();
                if (_poolCards.Count == 0) return;

                int newIndex = _poolIndex + direction;
                if (newIndex < 0)
                {
                    _announcer.AnnounceInterrupt(Strings.BeginningOfZone);
                    return;
                }
                if (newIndex >= _poolCards.Count)
                {
                    _announcer.AnnounceInterrupt(Strings.EndOfZone);
                    return;
                }
                _poolIndex = newIndex;
            }
            else
            {
                RefreshDeckCards();
                if (_deckCards.Count == 0) return;

                int newIndex = _deckIndex + direction;
                if (newIndex < 0)
                {
                    _announcer.AnnounceInterrupt(Strings.BeginningOfZone);
                    return;
                }
                if (newIndex >= _deckCards.Count)
                {
                    _announcer.AnnounceInterrupt(Strings.EndOfZone);
                    return;
                }
                _deckIndex = newIndex;
            }

            _announcer.AnnounceInterrupt(GetCurrentCardAnnouncement());
            ActivateCardInfoForCurrent();
        }

        private void JumpToZoneEdge(bool first)
        {
            if (_currentZone == SideboardZone.Pool)
            {
                RefreshPoolCards();
                if (_poolCards.Count == 0) return;
                _poolIndex = first ? 0 : _poolCards.Count - 1;
            }
            else
            {
                RefreshDeckCards();
                if (_deckCards.Count == 0) return;
                _deckIndex = first ? 0 : _deckCards.Count - 1;
            }

            _announcer.AnnounceInterrupt(GetCurrentCardAnnouncement());
            ActivateCardInfoForCurrent();
        }

        #endregion

        #region Card Announcements

        private string GetCurrentCardAnnouncement()
        {
            if (_currentZone == SideboardZone.Pool)
            {
                if (_poolIndex < 0 || _poolIndex >= _poolCards.Count) return "";
                var go = _poolCards[_poolIndex];
                string name = CardModelProvider.ExtractCardInfoFromModel(go)?.Name ?? UITextExtractor.GetText(go);
                return Strings.CardPosition(name, null, _poolIndex + 1, _poolCards.Count, force: true);
            }
            else
            {
                if (_deckIndex < 0 || _deckIndex >= _deckCards.Count) return "";
                var deckCard = _deckCards[_deckIndex];
                string name = CardModelProvider.GetNameFromGrpId(deckCard.GrpId) ?? $"Card #{deckCard.GrpId}";
                string qty = deckCard.Quantity > 1 ? $" x{deckCard.Quantity}" : "";
                string pos = Strings.PositionOf(_deckIndex + 1, _deckCards.Count, force: true);
                return $"{name}{qty}" + (pos != "" ? $", {pos}" : "");
            }
        }

        private void ActivateCardInfoForCurrent()
        {
            var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNav == null) return;

            if (_currentZone == SideboardZone.Pool)
            {
                // Pool cards are full card GameObjects - use standard PrepareForCard
                var go = GetCurrentPoolCard();
                if (go != null && CardDetector.IsCard(go))
                    cardNav.PrepareForCard(go);
            }
            else if (_currentZone == SideboardZone.Deck)
            {
                // Deck cards may have null ViewGameObject - use GrpId-based info blocks
                PrepareDeckCardInfo(cardNav);
            }
        }

        /// <summary>
        /// Prepares CardInfoNavigator for a deck card using GrpId-based lookup.
        /// Deck list cards often have null ViewGameObject (only CardTileBase available),
        /// which isn't recognized by CardDetector.IsCard. Use PrepareForCardInfo instead.
        /// </summary>
        private void PrepareDeckCardInfo(CardInfoNavigator cardNav)
        {
            if (_deckIndex < 0 || _deckIndex >= _deckCards.Count) return;
            var deckCard = _deckCards[_deckIndex];

            // Try ViewGameObject first (if available and recognized as card)
            if (deckCard.ViewGameObject != null && CardDetector.IsCard(deckCard.ViewGameObject))
            {
                cardNav.PrepareForCard(deckCard.ViewGameObject);
                return;
            }

            // Build info blocks from GrpId
            var cardInfo = CardModelProvider.GetCardInfoFromGrpId(deckCard.GrpId);
            if (cardInfo.HasValue)
            {
                var info = cardInfo.Value;
                info.Quantity = deckCard.Quantity;
                var blocks = CardDetector.BuildInfoBlocks(info);
                if (blocks.Count > 0)
                {
                    cardNav.PrepareForCardInfo(blocks, info.Name ?? $"Card #{deckCard.GrpId}");
                    return;
                }
            }

            // Minimal fallback: just name + quantity
            string name = CardModelProvider.GetNameFromGrpId(deckCard.GrpId) ?? $"Card #{deckCard.GrpId}";
            var minimalBlocks = new List<CardInfoBlock>
            {
                new CardInfoBlock(Strings.CardInfoName, name)
            };
            if (deckCard.Quantity > 1)
                minimalBlocks.Add(new CardInfoBlock(Strings.CardInfoQuantity, deckCard.Quantity.ToString(), false));
            cardNav.PrepareForCardInfo(minimalBlocks, name);
        }

        private void ActivateCardDetails()
        {
            if (_currentZone == SideboardZone.Info) return;

            var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNav == null) return;

            if (_currentZone == SideboardZone.Pool)
            {
                var go = GetCurrentPoolCard();
                if (go != null)
                    AccessibleArenaMod.Instance?.ActivateCardDetails(go);
            }
            else if (_currentZone == SideboardZone.Deck)
            {
                // For deck cards, ensure info is prepared then activate
                if (!cardNav.IsActive)
                    PrepareDeckCardInfo(cardNav);
                if (cardNav.IsActive)
                    cardNav.HandleInput();
            }
        }

        /// <summary>
        /// Gets the current pool card GameObject (for pool zone only).
        /// </summary>
        private GameObject GetCurrentPoolCard()
        {
            if (_poolIndex >= 0 && _poolIndex < _poolCards.Count)
                return _poolCards[_poolIndex];
            return null;
        }

        /// <summary>
        /// Gets the best activation target for the current card.
        /// For pool cards: the card GameObject.
        /// For deck cards: TileButton preferred (intended click target), then CardTileBase.
        /// </summary>
        private GameObject GetCurrentActivationTarget()
        {
            if (_currentZone == SideboardZone.Pool)
            {
                return GetCurrentPoolCard();
            }
            else
            {
                if (_deckIndex >= 0 && _deckIndex < _deckCards.Count)
                {
                    var info = _deckCards[_deckIndex];
                    return info.TileButton ?? info.CardTileBase ?? info.ViewGameObject;
                }
            }
            return null;
        }

        #endregion

        #region Card Activation (Move between pool/deck)

        private void ActivateCurrentCard()
        {
            bool isPool = _currentZone == SideboardZone.Pool;

            // Get card name before activation for announcement
            string cardName;
            if (isPool)
            {
                var go = GetCurrentPoolCard();
                if (go == null)
                {
                    _announcer.AnnounceInterrupt(Strings.NoCardSelected);
                    return;
                }
                cardName = CardModelProvider.ExtractCardInfoFromModel(go)?.Name ?? UITextExtractor.GetText(go);
            }
            else
            {
                if (_deckIndex < 0 || _deckIndex >= _deckCards.Count)
                {
                    _announcer.AnnounceInterrupt(Strings.NoCardSelected);
                    return;
                }
                cardName = CardModelProvider.GetNameFromGrpId(_deckCards[_deckIndex].GrpId) ?? "Card";
            }

            // Get the activation target (pool card GO or deck card TileButton/CardTileBase)
            var target = GetCurrentActivationTarget();
            if (target == null)
            {
                _announcer.AnnounceInterrupt(Strings.NoCardSelected);
                return;
            }

            // Click the card to toggle it between pool and deck
            UIActivator.Activate(target);

            // Announce what happened
            if (isPool)
            {
                _announcer.AnnounceInterrupt(Strings.Sideboard_CardAdded(cardName));
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.Sideboard_CardRemoved(cardName));
            }

            // Deactivate card info nav (blocks are stale after move)
            AccessibleArenaMod.Instance?.CardNavigator?.Deactivate();

            // Schedule a rescan to refresh card lists
            _rescanFrameCountdown = RescanDelayFrames;
        }

        private void OnPostMoveRescan()
        {
            DeckCardProvider.ClearDeckListCache();
            CardPoolAccessor.ClearCache();
            RefreshPoolCards();
            RefreshDeckCards();

            // Clamp indices
            if (_currentZone == SideboardZone.Pool)
            {
                if (_poolCards.Count == 0)
                {
                    _poolIndex = -1;
                }
                else if (_poolIndex >= _poolCards.Count)
                {
                    _poolIndex = _poolCards.Count - 1;
                }
            }
            else
            {
                if (_deckCards.Count == 0)
                {
                    _deckIndex = -1;
                }
                else if (_deckIndex >= _deckCards.Count)
                {
                    _deckIndex = _deckCards.Count - 1;
                }
            }

            Log.Msg("{NavigatorId}", $"Post-move rescan: pool={_poolCards.Count}, deck={_deckCards.Count}");
        }

        #endregion

        #region Pool Paging

        private void ScrollPoolPage(bool next)
        {
            // Ensure we have a valid pool holder
            CardPoolAccessor.FindCardPoolHolder();
            if (!CardPoolAccessor.IsValid())
            {
                Log.Msg("{NavigatorId}", $"No valid pool holder for paging");
                return;
            }

            bool success = next ? CardPoolAccessor.ScrollNext() : CardPoolAccessor.ScrollPrevious();
            if (success)
            {
                // Switch to pool zone if not there
                _currentZone = SideboardZone.Pool;
                _poolIndex = 0;

                // Delay refresh to let scroll animation complete
                _rescanFrameCountdown = RescanDelayFrames;

                int currentPage = CardPoolAccessor.GetCurrentPageIndex() + (next ? 1 : -1) + 1; // +1 for 1-based display
                int totalPages = CardPoolAccessor.GetPageCount();
                _announcer.AnnounceInterrupt(Strings.Sideboard_PageInfo(currentPage, totalPages));
            }
            else
            {
                string edge = next ? Strings.EndOfZone : Strings.BeginningOfZone;
                _announcer.AnnounceInterrupt(edge);
            }
        }

        #endregion

        #region Submit & Toggle

        private void SubmitSideboard()
        {
            var doneButton = GetDoneButton();
            if (doneButton == null)
            {
                Log.Warn("{NavigatorId}", $"Done button not found");
                return;
            }

            _announcer.AnnounceInterrupt(Strings.Sideboard_Submitted);
            UIActivator.Activate(doneButton);
        }

        private void ToggleBattlefieldView()
        {
            if (_sideboardInterface == null) return;

            if (!EnsureSideboardReflection()) return;
            var h = _sideboardCache.Handles;
            if (h.ShowHideToggle == null) return;

            try
            {
                var toggleButton = h.ShowHideToggle.GetValue(_sideboardInterface) as UnityEngine.UI.Button;
                if (toggleButton != null)
                {
                    toggleButton.onClick.Invoke();

                    // Check which view is now active by reading _deckBuilderVisible
                    var visibleField = _sideboardInterface.GetType().GetField("_deckBuilderVisible", PrivateInstance);
                    bool deckVisible = true;
                    if (visibleField != null)
                    {
                        deckVisible = (bool)visibleField.GetValue(_sideboardInterface);
                    }
                    _announcer.AnnounceInterrupt(deckVisible ? Strings.Sideboard_ViewDeck : Strings.Sideboard_ViewBattlefield);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{NavigatorId}", $"ToggleBattlefieldView failed: {ex.Message}");
            }
        }

        #endregion

        #region Info Announcements

        private void AnnounceTimer()
        {
            string timerText = GetTimerText();
            if (string.IsNullOrEmpty(timerText))
            {
                _announcer.AnnounceInterrupt(Strings.TimerNotAvailable);
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.Sideboard_Timer(timerText));
            }
        }

        private void AnnounceScore()
        {
            string playerName = GetPlayerName();
            int playerWins = GetPlayerWins();
            string opponentName = GetOpponentName();
            int opponentWins = GetOpponentWins();
            _announcer.AnnounceInterrupt(Strings.Sideboard_Score(playerName, playerWins, opponentName, opponentWins));
        }

        private void AnnounceInfoZone()
        {
            string introText = GetIntroText();
            string score = Strings.Sideboard_Score(GetPlayerName(), GetPlayerWins(), GetOpponentName(), GetOpponentWins());
            string timerText = GetTimerText();

            var parts = new List<string>();
            parts.Add(Strings.Sideboard_InfoZone);
            if (!string.IsNullOrEmpty(introText))
                parts.Add(introText);
            parts.Add(score);
            if (!string.IsNullOrEmpty(timerText))
                parts.Add(Strings.Sideboard_Timer(timerText));

            RefreshPoolCards();
            RefreshDeckCards();
            parts.Add(Strings.Sideboard_PoolZone(_poolCards.Count));
            parts.Add(Strings.Sideboard_DeckZone(_deckCards.Count));

            _announcer.AnnounceInterrupt(string.Join(". ", parts));
        }

        #endregion

        #region Card Data Refresh

        private void RefreshPoolCards()
        {
            CardPoolAccessor.FindCardPoolHolder();
            _poolCards = CardPoolAccessor.GetCurrentPageCards();
        }

        private void RefreshDeckCards()
        {
            DeckCardProvider.ClearDeckListCache();
            _deckCards = DeckCardProvider.GetDeckListCards();
        }

        #endregion

        #region Detection & Reflection

        private bool FindSideboardInterface()
        {
            // Search for SideboardInterface MonoBehaviour
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.SideboardInterface)
                {
                    try
                    {
                        if (mb.gameObject.activeInHierarchy)
                        {
                            _sideboardInterface = mb;
                            EnsureSideboardReflection();
                            FindNavBar();
                            return true;
                        }
                    }
                    catch { }
                }
            }
            return false;
        }

        private void FindNavBar()
        {
            if (_sideboardInterface == null) return;

            if (!EnsureSideboardReflection()) return;
            var h = _sideboardCache.Handles;
            if (h.NavBar == null) return;

            try
            {
                _navBar = h.NavBar.GetValue(_sideboardInterface) as MonoBehaviour;
                if (_navBar != null)
                {
                    EnsureNavBarReflection(_navBar.GetType());
                }
            }
            catch (Exception ex)
            {
                Log.Error("{NavigatorId}", $"FindNavBar failed: {ex.Message}");
            }
        }

        private static bool EnsureSideboardReflection()
        {
            var type = FindType("SideboardInterface");
            return type != null && _sideboardCache.EnsureInitialized(type);
        }

        private static bool EnsureDeckBuilderReflection()
        {
            var type = FindType("DeckBuilderWidget");
            return type != null && _deckBuilderCache.EnsureInitialized(type);
        }

        private static bool EnsureNavBarReflection(Type seed)
        {
            return seed != null && _navBarCache.EnsureInitialized(seed);
        }

        private GameObject GetDoneButton()
        {
            if (_sideboardInterface == null) return null;

            if (!EnsureSideboardReflection()) return null;
            if (!EnsureDeckBuilderReflection()) return null;

            var sh = _sideboardCache.Handles;
            var dh = _deckBuilderCache.Handles;
            if (sh.DeckBuilder == null || dh.DoneButton == null) return null;

            try
            {
                var deckBuilder = sh.DeckBuilder.GetValue(_sideboardInterface);
                if (deckBuilder == null) return null;

                var doneButton = dh.DoneButton.GetValue(deckBuilder);
                if (doneButton is Component comp)
                    return comp.gameObject;
            }
            catch (Exception ex)
            {
                Log.Error("{NavigatorId}", $"GetDoneButton failed: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region NavBar Data Access

        private string GetPlayerName()
        {
            var h = _navBarCache.Handles;
            if (_navBar == null || h?.PlayerName == null) return "You";
            try
            {
                var tmp = h.PlayerName.GetValue(_navBar) as TMP_Text;
                return tmp?.text ?? "You";
            }
            catch { return "You"; }
        }

        private string GetOpponentName()
        {
            var h = _navBarCache.Handles;
            if (_navBar == null || h?.OpponentName == null) return "Opponent";
            try
            {
                var tmp = h.OpponentName.GetValue(_navBar) as TMP_Text;
                return tmp?.text ?? "Opponent";
            }
            catch { return "Opponent"; }
        }

        private int GetPlayerWins()
        {
            return CountActivePips(_navBarCache.Handles?.PlayerWinPips);
        }

        private int GetOpponentWins()
        {
            return CountActivePips(_navBarCache.Handles?.OpponentWinPips);
        }

        private int CountActivePips(FieldInfo pipsField)
        {
            if (_navBar == null || pipsField == null) return 0;
            try
            {
                var pips = pipsField.GetValue(_navBar) as GameObject[];
                if (pips == null) return 0;
                int count = 0;
                foreach (var pip in pips)
                {
                    if (pip != null && pip.activeSelf) count++;
                }
                return count;
            }
            catch { return 0; }
        }

        private string GetTimerText()
        {
            var h = _navBarCache.Handles;
            if (_navBar == null || h?.TimerText == null) return null;
            try
            {
                var tmp = h.TimerText.GetValue(_navBar) as TMP_Text;
                if (tmp != null && tmp.gameObject.activeInHierarchy)
                    return tmp.text;
            }
            catch { }
            return null;
        }

        private string GetIntroText()
        {
            var h = _sideboardCache.Handles;
            if (_sideboardInterface == null || h?.IntroText == null) return null;
            try
            {
                var tmp = h.IntroText.GetValue(_sideboardInterface) as TMP_Text;
                if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                    return tmp.text;
            }
            catch { }
            return null;
        }

        #endregion
    }
}
