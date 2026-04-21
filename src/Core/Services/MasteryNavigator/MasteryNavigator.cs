using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Standalone navigator for the MTGA Mastery/Rewards screen (RewardTrack scene).
    /// Split across partials:
    ///   - MasteryNavigator.cs (this file): orchestration, mode routing, lifecycle.
    ///   - MasteryNavigator.Levels.cs: levels view data + reflection + input.
    ///   - MasteryNavigator.PrizeWall.cs: prize wall items + input.
    ///   - MasteryNavigator.ConfirmationModal.cs: purchase confirmation modal handling.
    /// </summary>
    public partial class MasteryNavigator : BaseNavigator
    {
        #region Constants

        private const int MasteryPriority = 60;

        #endregion

        #region Mode

        private enum MasteryMode { Levels, PrizeWall }
        private MasteryMode _mode;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Mastery";
        public override string ScreenName => _mode == MasteryMode.PrizeWall ? Strings.ScreenPrizeWall : Strings.ScreenMastery;
        public override int Priority => MasteryPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => false;

        #endregion

        #region Constructor

        public MasteryNavigator(IAnnouncementService announcer) : base(announcer)
        {
        }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            // Check for ProgressionTracksContentController (Levels mode)
            var levelsController = FindLevelsController();
            if (levelsController != null && IsControllerOpen(levelsController))
            {
                _mode = MasteryMode.Levels;
                _controller = levelsController;
                _controllerGameObject = levelsController.gameObject;
                return true;
            }

            // Check for ContentController_PrizeWall (PrizeWall mode)
            var prizeWall = FindPrizeWallController();
            if (prizeWall != null && IsPrizeWallOpen(prizeWall))
            {
                _mode = MasteryMode.PrizeWall;
                _prizeWallController = prizeWall;
                _prizeWallGameObject = prizeWall.gameObject;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a panel should be excluded from popup handling.
        /// These are benign game overlays that aren't real popups.
        /// </summary>
        protected override bool IsPopupExcluded(PanelInfo panel)
        {
            if (base.IsPopupExcluded(panel)) return true;
            string name = panel.Name;
            // ObjectivePopup: daily quest overlay
            // FullscreenZFBrowser: embedded browser canvas
            // Rewards: empty Rewards controller active alongside Prize Wall confirmation modal
            return name.Contains("ObjectivePopup") || name.Contains("FullscreenZFBrowser") ||
                   name.Contains("Rewards");
        }

        protected override void OnPopupClosed()
        {
            // Re-announce current position based on mode
            if (_mode == MasteryMode.PrizeWall)
                AnnouncePrizeWallItem();
            else
                AnnounceCurrentLevel();
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            if (_mode == MasteryMode.PrizeWall)
            {
                DiscoverPrizeWallItems();
                return;
            }

            // Levels mode: Build level data and action buttons from the view
            BuildLevelData();
            BuildActionButtons();

            // Insert virtual status item at position 0 with XP info + action buttons as tiers
            InsertStatusItem();

            if (_levelData.Count > 0)
            {
                // Add a dummy element for BaseNavigator validation
                AddElement(_controllerGameObject, "Mastery");
            }
        }

        #endregion

        #region Activation & Deactivation

        protected override void OnActivated()
        {
            _currentTierIndex = 0;

            if (_mode == MasteryMode.PrizeWall)
            {
                _prizeWallIndex = 0;
            }
            else
            {
                // Start at current player level (skips status item at 0)
                _currentLevelIndex = _currentPlayerLevel >= 0 ? _currentPlayerLevel : 0;
            }

            EnablePopupDetection();
        }

        protected override void OnDeactivating()
        {
            DisablePopupDetection();
            _levelData.Clear();
            _actionButtons.Clear();
            _prizeWallItems.Clear();
            _confirmationModalGameObject = null;
            _wasModalOpen = false;
            _isConfirmationModalActive = false;
            _modalElements.Clear();
            _confirmationModalMb = null;
        }

        public override void OnSceneChanged(string sceneName)
        {
            _controller = null;
            _controllerGameObject = null;
            _levelsCache.Clear();
            _prizeWallController = null;
            _prizeWallGameObject = null;
            _prizeWallCache.Clear();

            base.OnSceneChanged(sceneName);
        }

        #endregion

        #region Announcements (mode-aware)

        public override string GetTutorialHint() => LocaleManager.Instance.Get("MasteryHint");

        protected override string GetActivationAnnouncement()
        {
            if (_mode == MasteryMode.PrizeWall)
            {
                string prizeCore = Strings.PrizeWallActivation(_prizeWallItems.Count, _sphereCount);
                return Strings.WithHint(prizeCore, "MasteryHint");
            }

            if (_levelData.Count == 0)
                return $"{_trackTitle}. No levels found.";

            // Build XP string for current level
            string xpStr = "";
            if (_currentPlayerLevel >= 0 && _currentPlayerLevel < _levelData.Count)
            {
                var level = _levelData[_currentPlayerLevel];
                if (level.XpToComplete > 0 && !level.IsComplete)
                    xpStr = $"{level.ExpProgress}/{level.XpToComplete} XP";
                else if (level.IsComplete)
                    xpStr = "completed";
            }

            string core = Strings.MasteryActivation(_trackTitle, _levelData[_currentPlayerLevel].LevelNumber, _totalLevels, xpStr);
            return Strings.WithHint(core, "MasteryHint");
        }

        protected override string GetElementAnnouncement(int index)
        {
            // Not used - we handle our own announcements
            return "";
        }

        #endregion

        #region Update Loop

        public override void Update()
        {
            if (!_isActive)
            {
                base.Update();
                return;
            }

            if (_mode == MasteryMode.PrizeWall)
            {
                // Verify PrizeWall controller is still valid
                if (_prizeWallController == null || _prizeWallGameObject == null ||
                    !_prizeWallGameObject.activeInHierarchy)
                {
                    Deactivate();
                    return;
                }

                if (!IsPrizeWallOpen(_prizeWallController))
                {
                    Deactivate();
                    return;
                }
            }
            else
            {
                // Verify levels controller is still valid
                if (_controller == null || _controllerGameObject == null || !_controllerGameObject.activeInHierarchy)
                {
                    Deactivate();
                    return;
                }

                if (!IsControllerOpen(_controller))
                {
                    Deactivate();
                    return;
                }
            }

            // Check confirmation modal state transitions (PrizeWall mode).
            // Modal is reused (not re-instantiated), so PanelStateManager doesn't fire events
            // after the first time. We poll activeInHierarchy and handle with custom elements
            // (same pattern as StoreNavigator — not base popup mode).
            if (_mode == MasteryMode.PrizeWall && _confirmationModalGameObject != null)
            {
                bool modalOpen = _confirmationModalGameObject.activeInHierarchy;
                if (modalOpen && !_wasModalOpen)
                {
                    _wasModalOpen = true;
                    Log.Msg("Mastery", "Confirmation modal opened");
                    _isConfirmationModalActive = true;
                    _confirmationModalMb = GetConfirmationModalMb();
                    DiscoverConfirmationModalElements();
                    AnnounceConfirmationModal();
                    return;
                }
                else if (!modalOpen && _wasModalOpen)
                {
                    _wasModalOpen = false;
                    if (_isConfirmationModalActive)
                    {
                        Log.Msg("Mastery", "Confirmation modal closed");
                        _isConfirmationModalActive = false;
                        _modalElements.Clear();
                        _confirmationModalMb = null;
                        AnnouncePrizeWallItem();
                    }
                }
            }

            // Base popup mode for other popups (not confirmation modal)
            if (IsInPopupMode)
            {
                base.Update();
                return;
            }

            HandleMasteryInput();
        }

        protected override bool ValidateElements()
        {
            if (_mode == MasteryMode.PrizeWall)
                return _prizeWallController != null && _prizeWallGameObject != null && _prizeWallGameObject.activeInHierarchy;

            return _controller != null && _controllerGameObject != null && _controllerGameObject.activeInHierarchy;
        }

        #endregion

        #region Input Routing

        private void HandleMasteryInput()
        {
            if (_mode == MasteryMode.PrizeWall)
            {
                HandlePrizeWallInput();
                return;
            }

            HandleLevelInput();
        }

        #endregion
    }
}
