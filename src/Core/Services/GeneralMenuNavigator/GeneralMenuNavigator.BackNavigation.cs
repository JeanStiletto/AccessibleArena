using UnityEngine;
using UnityEngine.UI;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.ElementGrouping;
using AccessibleArena.Core.Services.PanelDetection;
using System.Linq;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class GeneralMenuNavigator
    {
        /// <summary>
        /// Handle Backspace key - navigates back one level in the menu hierarchy.
        /// Uses OverlayDetector for overlay cases, GetCurrentForeground() for content panel cases.
        /// </summary>
        private bool HandleBackNavigation()
        {
            // First check if an overlay is active using the new OverlayDetector
            var activeOverlay = _overlayDetector.GetActiveOverlay();
            Log.Nav(NavigatorId, $"Backspace: activeOverlay = {activeOverlay?.ToString() ?? "none"}");

            if (activeOverlay != null)
            {
                // Friend panel sub-groups: handle backspace at group level as close panel
                if (activeOverlay.Value == ElementGroup.FriendsPanel || activeOverlay.Value.IsFriendPanelGroup())
                {
                    // If inside a group, let HandleGroupedBackspace exit to group level first
                    if (_groupedNavigator.Level == NavigationLevel.InsideGroup)
                        return HandleGroupedBackspace();
                    // At group level, close the panel
                    return CloseSocialPanel();
                }

                return activeOverlay switch
                {
                    ElementGroup.Popup => false, // Handled by base popup mode
                    ElementGroup.MailboxContent => CloseMailDetailView(), // Close mail, return to list
                    ElementGroup.MailboxList => CloseMailbox(), // Close mailbox entirely
                    // RewardsPopup handled by RewardPopupNavigator
                    ElementGroup.PlayBladeTabs => HandlePlayBladeBackspace(),
                    ElementGroup.PlayBladeContent => HandlePlayBladeBackspace(),
                    ElementGroup.NPE => HandleNPEBack(),
                    ElementGroup.SettingsMenu => false, // Settings handled by SettingsMenuNavigator
                    _ => false
                };
            }

            // No overlay - check content panel layer
            var layer = GetCurrentForeground();
            Log.Nav(NavigatorId, $"Backspace: layer = {layer}");

            return layer switch
            {
                ForegroundLayer.ContentPanel => HandleContentPanelBack(),
                ForegroundLayer.Home => TryGenericBackButton(),
                ForegroundLayer.None => TryGenericBackButton(),
                _ => false
            };
        }

        /// <summary>
        /// Handle back navigation in content panels.
        /// First try generic back button, then navigate to Home.
        /// </summary>
        private bool HandleContentPanelBack()
        {
            // Refresh content controller detection to ensure we have current state
            DetectActiveContentController();
            Log.Nav(NavigatorId, $"HandleContentPanelBack: controller = {_activeContentController ?? "null"}");

            // Special case: Color Challenge (CampaignGraph) has two levels
            // - Blade collapsed (specific color selected) → expand blade to return to color list
            // - Blade expanded (color list visible) → navigate Home
            if (_activeContentController == T.CampaignGraphContentController)
            {
                return HandleCampaignGraphBack();
            }

            // Special case: Deck Builder uses MainButton (Fertig/Done) to exit
            if (_activeContentController == T.WrapperDeckBuilder)
            {
                return HandleDeckBuilderBack();
            }

            // First try to find a dismiss button within the current content panel
            if (_activeControllerGameObject != null)
            {
                var dismissButton = FindDismissButtonInPanel(_activeControllerGameObject);
                if (dismissButton != null)
                {
                    Log.Nav(NavigatorId, $"Found dismiss button in content panel: {dismissButton.name}");
                    _announcer.AnnounceVerbose(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                    UIActivator.Activate(dismissButton);
                    TriggerRescan();
                    return true;
                }
            }

            // Content panels (Store, Collection, BoosterChamber, etc.) don't have back buttons.
            // Navigate to Home instead. Don't use FindGenericBackButton() here as it finds
            // buttons from unrelated panels (e.g., FullscreenZFBrowserCanvas).
            Log.Nav(NavigatorId, $"No dismiss button in content panel, navigating to Home");
            return NavigateToHome();
        }

        /// <summary>
        /// Handle back navigation in Color Challenge (CampaignGraph).
        /// Two-level back:
        /// - Blade collapsed (a color is selected, deck shown) → expand blade to return to color list.
        /// - Blade expanded (color list visible) → navigate Home to exit Color Challenge.
        /// </summary>
        private bool HandleCampaignGraphBack()
        {
            // Check if blade is currently collapsed by looking for the expand button
            var bladeButton = GetActiveCustomButtons()
                .FirstOrDefault(obj => obj.name.Contains("BladeHoverClosed") || obj.name.Contains("Btn_BladeIsClosed"));

            if (bladeButton != null)
            {
                // Blade is collapsed (a color is selected) — re-expand to show color list
                Log.Nav(NavigatorId, $"CampaignGraph back: blade collapsed, re-expanding color list");
                AutoExpandBlade();
                return true;
            }

            // Blade is expanded (color list visible) — exit Color Challenge
            Log.Nav(NavigatorId, $"CampaignGraph back: blade expanded, navigating Home");
            _announcer.AnnounceVerbose(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
            return NavigateToHome();
        }

        /// <summary>
        /// Find a dismiss/close button within a specific panel.
        /// Only matches explicit Close/Dismiss/Back buttons, not ModalFade
        /// (which is ambiguous - sometimes dismiss, sometimes other actions).
        /// </summary>
        private GameObject FindDismissButtonInPanel(GameObject panel)
        {
            if (panel == null) return null;

            // Look for explicit close/dismiss/back buttons within the panel
            // Note: ModalFade is NOT included - it's ambiguous (e.g., in BoosterChamber it's "Open x 10")
            foreach (var mb in panel.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "CustomButton") continue;

                string name = mb.gameObject.name;
                if (name.Contains("Close") ||
                    name.Contains("Dismiss") ||
                    (name.Contains("Back") && !name.Contains("Background")))
                {
                    return mb.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to find and click a generic back button.
        /// Returns false if no back button found (nothing to do).
        /// </summary>
        private bool TryGenericBackButton()
        {
            var backButton = FindGenericBackButton();
            if (backButton != null)
            {
                Log.Nav(NavigatorId, $"Found back button: {backButton.name}");
                _announcer.AnnounceVerbose(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                UIActivator.Activate(backButton);
                TriggerRescan();
                return true;
            }

            Log.Nav(NavigatorId, $"At top level, no back action");
            return false;
        }

        /// <summary>
        /// Handle back navigation in NPE (New Player Experience) screens.
        /// </summary>
        private bool HandleNPEBack()
        {
            Log.Nav(NavigatorId, $"Handling NPE back");
            return TryGenericBackButton();
        }

        /// <summary>
        /// Find a button matching the given predicate within a panel or scene-wide.
        /// Searches both Unity Button and CustomButton components.
        /// </summary>
        /// <param name="panel">Panel to search within, or null for scene-wide search</param>
        /// <param name="namePredicate">Predicate to match button names</param>
        /// <param name="customButtonFilter">Optional extra filter for CustomButtons (e.g., no text)</param>
        private GameObject FindButtonByPredicate(
            GameObject panel,
            System.Func<string, bool> namePredicate,
            System.Func<GameObject, bool> customButtonFilter = null)
        {
            // Search Unity Buttons
            var buttons = panel != null
                ? panel.GetComponentsInChildren<Button>(true)
                : GameObject.FindObjectsOfType<Button>();

            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                if (namePredicate(btn.gameObject.name))
                    return btn.gameObject;
            }

            // Search CustomButtons
            if (panel != null)
            {
                foreach (var mb in panel.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                    if (!IsCustomButtonType(mb.GetType().Name)) continue;
                    if (customButtonFilter != null && !customButtonFilter(mb.gameObject)) continue;
                    if (namePredicate(mb.gameObject.name))
                        return mb.gameObject;
                }
            }
            else
            {
                foreach (var btn in GetActiveCustomButtons())
                {
                    if (btn == null) continue;
                    if (customButtonFilter != null && !customButtonFilter(btn)) continue;
                    if (namePredicate(btn.name))
                        return btn;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a close/dismiss button within a panel.
        /// </summary>
        private GameObject FindCloseButtonInPanel(GameObject panel)
        {
            return FindButtonByPredicate(panel, name =>
            {
                var lower = name.ToLowerInvariant();
                return lower.Contains("close") || lower.Contains("dismiss") ||
                       lower.Contains("cancel") || lower.Contains("back");
            });
        }

        /// <summary>
        /// Find a generic back/close button on the current screen.
        /// </summary>
        private GameObject FindGenericBackButton()
        {
            return FindButtonByPredicate(
                panel: null,
                namePredicate: name => !name.Contains("DismissButton") && IsBackButtonName(name),
                customButtonFilter: btn => !UITextExtractor.HasActualText(btn)
            );
        }

        /// <summary>
        /// Check if a button name matches back/close patterns.
        /// </summary>
        private static bool IsBackButtonName(string name)
        {
            return name.IndexOf("back", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name == "MainButtonOutline" ||
                   name.IndexOf("Blade_Close", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Blade_Arrow", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Handle Backspace within PlayBlade when detected via overlay.
        /// Note: Most PlayBlade backspace cases are handled earlier by the helper.
        /// This is a fallback for overlay detection - just close the blade.
        /// </summary>
        private bool HandlePlayBladeBackspace()
        {
            return ClosePlayBlade();
        }

        /// <summary>
        /// Close the PlayBlade by finding and activating the dismiss button.
        /// </summary>
        private bool ClosePlayBlade()
        {
            Log.Nav(NavigatorId, $"Attempting to close PlayBlade");

            // Challenge screen: click Leave button but DON'T clear state immediately.
            // The game shows a confirmation dialog first. If user confirms, the game
            // closes the blade and panel detection handles cleanup naturally.
            // If user cancels, the blade stays open with challenge state intact.
            if (_challengeHelper.IsActive)
            {
                var leaveButton = GameObject.Find("MainButton_Leave");
                if (leaveButton != null && leaveButton.activeInHierarchy)
                {
                    Log.Nav(NavigatorId, $"Found MainButton_Leave, activating (awaiting confirmation)");
                    _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                    UIActivator.Activate(leaveButton);
                    // If user cancels the confirmation, rescan will consume this and re-enter ChallengeMain.
                    // If user confirms, OnChallengeClosed() → SetChallengeContext(false) clears it first.
                    _groupedNavigator.RequestChallengeMainEntry();
                    return true;
                }
            }

            var bladeIsOpenButton = GameObject.Find("Btn_BladeIsOpen");
            if (bladeIsOpenButton != null && bladeIsOpenButton.activeInHierarchy)
            {
                Log.Nav(NavigatorId, $"Found Btn_BladeIsOpen, activating");
                _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                UIActivator.Activate(bladeIsOpenButton);
                ClearBladeStateAndRescan();
                return true;
            }

            var dismissButton = GameObject.Find("Blade_DismissButton");
            if (dismissButton != null && dismissButton.activeInHierarchy)
            {
                Log.Nav(NavigatorId, $"Found Blade_DismissButton, activating");
                _announcer.Announce(Models.Strings.ClosingPlayBlade, Models.AnnouncementPriority.High);
                UIActivator.Activate(dismissButton);
                ClearBladeStateAndRescan();
                return true;
            }

            Log.Nav(NavigatorId, $"ClosePlayBlade called but no close button found - check foreground detection");
            return false;
        }

        /// <summary>
        /// Clear blade state and trigger immediate rescan.
        /// </summary>
        private void ClearBladeStateAndRescan()
        {
            Log.Nav(NavigatorId, $"Clearing blade state for immediate Home navigation");
            _playBladeHelper.OnPlayBladeClosed();
            if (_challengeHelper.IsActive)
                _challengeHelper.OnChallengeClosed();
            ReportPanelClosedByName("PlayBlade");
            PanelStateManager.Instance?.SetPlayBladeState(0);
            TriggerRescan();
        }
    }
}
