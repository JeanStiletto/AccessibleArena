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

        // Reflection caches for SideboardInterface
        private static FieldInfo _deckBuilderField;
        private static FieldInfo _showHideToggleField;
        private static FieldInfo _navBarField;
        private static FieldInfo _introTextField;
        private static bool _sideboardReflectionInit;

        // Reflection caches for DeckBuilderWidget
        private static FieldInfo _doneButtonField;
        private static bool _deckBuilderReflectionInit;

        // Reflection caches for SideboardNavBar
        private static FieldInfo _playerNameField;
        private static FieldInfo _opponentNameField;
        private static FieldInfo _playerWinPipsField;
        private static FieldInfo _opponentWinPipsField;
        private static FieldInfo _timerTextField;
        private static bool _navBarReflectionInit;

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
            MelonLogger.Msg($"[{NavigatorId}] DuelScene loaded - watching for sideboard");
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

            string announcement = Strings.Sideboard_Activated(playerName, playerWins, opponentName, opponentWins, poolCount, deckCount);
            if (!string.IsNullOrEmpty(introText))
                announcement = $"{introText}. {announcement}";
            return announcement;
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
                return Strings.CardPosition(name, null, _poolIndex + 1, _poolCards.Count);
            }
            else
            {
                if (_deckIndex < 0 || _deckIndex >= _deckCards.Count) return "";
                var deckCard = _deckCards[_deckIndex];
                string name = CardModelProvider.GetNameFromGrpId(deckCard.GrpId) ?? $"Card #{deckCard.GrpId}";
                string qty = deckCard.Quantity > 1 ? $" x{deckCard.Quantity}" : "";
                return $"{name}{qty}, {_deckIndex + 1} of {_deckCards.Count}";
            }
        }

        private void ActivateCardInfoForCurrent()
        {
            GameObject cardGo = GetCurrentCardGameObject();
            if (cardGo == null) return;

            var cardNav = AccessibleArenaMod.Instance?.CardNavigator;
            if (cardNav != null && CardDetector.IsCard(cardGo))
            {
                cardNav.PrepareForCard(cardGo);
            }
        }

        private void ActivateCardDetails()
        {
            GameObject cardGo = GetCurrentCardGameObject();
            if (cardGo == null) return;

            AccessibleArenaMod.Instance?.ActivateCardDetails(cardGo);
        }

        private GameObject GetCurrentCardGameObject()
        {
            if (_currentZone == SideboardZone.Pool)
            {
                if (_poolIndex >= 0 && _poolIndex < _poolCards.Count)
                    return _poolCards[_poolIndex];
            }
            else
            {
                if (_deckIndex >= 0 && _deckIndex < _deckCards.Count)
                {
                    var info = _deckCards[_deckIndex];
                    return info.ViewGameObject ?? info.TileButton ?? info.CardTileBase;
                }
            }
            return null;
        }

        #endregion

        #region Card Activation (Move between pool/deck)

        private void ActivateCurrentCard()
        {
            GameObject cardGo = GetCurrentCardGameObject();
            if (cardGo == null)
            {
                _announcer.AnnounceInterrupt(Strings.NoCardSelected);
                return;
            }

            // Get card name before activation for announcement
            string cardName;
            bool isPool = _currentZone == SideboardZone.Pool;
            if (isPool)
            {
                cardName = CardModelProvider.ExtractCardInfoFromModel(cardGo)?.Name ?? UITextExtractor.GetText(cardGo);
            }
            else
            {
                var deckCard = _deckCards[_deckIndex];
                cardName = CardModelProvider.GetNameFromGrpId(deckCard.GrpId) ?? "Card";
            }

            // Click the card to toggle it between pool and deck
            UIActivator.Activate(cardGo);

            // Announce what happened
            if (isPool)
            {
                _announcer.AnnounceInterrupt(Strings.Sideboard_CardAdded(cardName));
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.Sideboard_CardRemoved(cardName));
            }

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

            MelonLogger.Msg($"[{NavigatorId}] Post-move rescan: pool={_poolCards.Count}, deck={_deckCards.Count}");
        }

        #endregion

        #region Pool Paging

        private void ScrollPoolPage(bool next)
        {
            // Ensure we have a valid pool holder
            CardPoolAccessor.FindCardPoolHolder();
            if (!CardPoolAccessor.IsValid())
            {
                MelonLogger.Msg($"[{NavigatorId}] No valid pool holder for paging");
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
                MelonLogger.Warning($"[{NavigatorId}] Done button not found");
                return;
            }

            _announcer.AnnounceInterrupt(Strings.Sideboard_Submitted);
            UIActivator.Activate(doneButton);
        }

        private void ToggleBattlefieldView()
        {
            if (_sideboardInterface == null) return;

            EnsureSideboardReflection();
            if (_showHideToggleField == null) return;

            try
            {
                var toggleButton = _showHideToggleField.GetValue(_sideboardInterface) as UnityEngine.UI.Button;
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
                MelonLogger.Error($"[{NavigatorId}] ToggleBattlefieldView failed: {ex.Message}");
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

            EnsureSideboardReflection();
            if (_navBarField != null)
            {
                try
                {
                    _navBar = _navBarField.GetValue(_sideboardInterface) as MonoBehaviour;
                    if (_navBar != null)
                    {
                        EnsureNavBarReflection();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[{NavigatorId}] FindNavBar failed: {ex.Message}");
                }
            }
        }

        private static void EnsureSideboardReflection()
        {
            if (_sideboardReflectionInit) return;
            _sideboardReflectionInit = true;

            var type = FindType("SideboardInterface");
            if (type == null) return;

            _deckBuilderField = type.GetField("_deckBuilder", PrivateInstance);
            _showHideToggleField = type.GetField("_showHideToggle", PrivateInstance);
            _navBarField = type.GetField("_navBar", PrivateInstance);
            _introTextField = type.GetField("_sideboardIntroText", PrivateInstance);

            MelonLogger.Msg($"[SideboardNavigator] Reflection init: " +
                $"_deckBuilder={_deckBuilderField != null}, " +
                $"_showHideToggle={_showHideToggleField != null}, " +
                $"_navBar={_navBarField != null}, " +
                $"_introText={_introTextField != null}");
        }

        private static void EnsureDeckBuilderReflection()
        {
            if (_deckBuilderReflectionInit) return;
            _deckBuilderReflectionInit = true;

            var type = FindType("DeckBuilderWidget");
            if (type == null) return;

            _doneButtonField = type.GetField("_doneButton", PrivateInstance);

            MelonLogger.Msg($"[SideboardNavigator] DeckBuilder reflection: " +
                $"_doneButton={_doneButtonField != null}");
        }

        private static void EnsureNavBarReflection()
        {
            if (_navBarReflectionInit) return;
            _navBarReflectionInit = true;

            var type = FindType("SideboardNavBar");
            if (type == null) return;

            _playerNameField = type.GetField("PlayerName", PublicInstance);
            _opponentNameField = type.GetField("OpponentName", PublicInstance);
            _playerWinPipsField = type.GetField("PlayerWinPips", PublicInstance);
            _opponentWinPipsField = type.GetField("OpponentWinPips", PublicInstance);
            _timerTextField = type.GetField("TimerText", PublicInstance);

            MelonLogger.Msg($"[SideboardNavigator] NavBar reflection: " +
                $"PlayerName={_playerNameField != null}, OpponentName={_opponentNameField != null}, " +
                $"PlayerWinPips={_playerWinPipsField != null}, OpponentWinPips={_opponentWinPipsField != null}, " +
                $"TimerText={_timerTextField != null}");
        }

        private GameObject GetDoneButton()
        {
            if (_sideboardInterface == null) return null;

            EnsureSideboardReflection();
            EnsureDeckBuilderReflection();

            if (_deckBuilderField == null || _doneButtonField == null) return null;

            try
            {
                var deckBuilder = _deckBuilderField.GetValue(_sideboardInterface);
                if (deckBuilder == null) return null;

                var doneButton = _doneButtonField.GetValue(deckBuilder);
                if (doneButton is Component comp)
                    return comp.gameObject;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[{NavigatorId}] GetDoneButton failed: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region NavBar Data Access

        private string GetPlayerName()
        {
            if (_navBar == null || _playerNameField == null) return "You";
            try
            {
                var tmp = _playerNameField.GetValue(_navBar) as TMP_Text;
                return tmp?.text ?? "You";
            }
            catch { return "You"; }
        }

        private string GetOpponentName()
        {
            if (_navBar == null || _opponentNameField == null) return "Opponent";
            try
            {
                var tmp = _opponentNameField.GetValue(_navBar) as TMP_Text;
                return tmp?.text ?? "Opponent";
            }
            catch { return "Opponent"; }
        }

        private int GetPlayerWins()
        {
            return CountActivePips(_playerWinPipsField);
        }

        private int GetOpponentWins()
        {
            return CountActivePips(_opponentWinPipsField);
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
            if (_navBar == null || _timerTextField == null) return null;
            try
            {
                var tmp = _timerTextField.GetValue(_navBar) as TMP_Text;
                if (tmp != null && tmp.gameObject.activeInHierarchy)
                    return tmp.text;
            }
            catch { }
            return null;
        }

        private string GetIntroText()
        {
            if (_sideboardInterface == null || _introTextField == null) return null;
            try
            {
                var tmp = _introTextField.GetValue(_sideboardInterface) as TMP_Text;
                if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                    return tmp.text;
            }
            catch { }
            return null;
        }

        #endregion
    }
}
