using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Core.Services.ElementGrouping;
using System;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    public partial class GeneralMenuNavigator
    {
        // Friends panel: cached profile button info for label override
        private GameObject _profileButtonGO;
        private string _profileLabel;

        // Friend section sub-navigation state
        // Left/Right cycles available actions for the current friend entry
        private List<(string label, string actionId)> _friendActions;
        private int _friendActionIndex;

        /// <summary>
        /// Check if the Social/Friends panel is currently open.
        /// </summary>
        protected bool IsSocialPanelOpen() => _screenDetector.IsSocialPanelOpen();

        /// <summary>
        /// Close the Social (Friends) panel.
        /// </summary>
        private bool CloseSocialPanel()
        {
            LogDebug($"[{NavigatorId}] Closing Social panel");
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null) return TryGenericBackButton();

            var socialUI = socialPanel.GetComponent("SocialUI");
            if (socialUI == null) return TryGenericBackButton();

            var minimizeMethod = socialUI.GetType().GetMethod("Minimize", AllInstanceFlags);
            if (minimizeMethod == null) return TryGenericBackButton();

            minimizeMethod.Invoke(socialUI, null);
            LogDebug($"[{NavigatorId}] Called SocialUI.Minimize()");
            _announcer.AnnounceVerbose(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
            ReportPanelClosed(socialPanel);
            TriggerRescan();
            return true;
        }

        /// <summary>
        /// Toggle the Friends/Social panel by calling SocialUI methods directly.
        /// </summary>
        private void ToggleFriendsPanel()
        {
            LogDebug($"[{NavigatorId}] ToggleFriendsPanel called");

            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null)
            {
                LogDebug($"[{NavigatorId}] Social UI panel not found");
                return;
            }

            // Get the SocialUI component
            var socialUI = socialPanel.GetComponent("SocialUI");
            if (socialUI == null)
            {
                LogDebug($"[{NavigatorId}] SocialUI component not found");
                return;
            }

            bool isOpen = IsSocialPanelOpen();
            LogDebug($"[{NavigatorId}] Toggling Friends panel (isOpen: {isOpen})");

            try
            {
                if (isOpen)
                {
                    // Close the panel - SocialUI.Minimize() closes friends list + chat
                    var closeMethod = socialUI.GetType().GetMethod("Minimize",
                        AllInstanceFlags);
                    if (closeMethod != null)
                    {
                        closeMethod.Invoke(socialUI, null);
                        LogDebug($"[{NavigatorId}] Called SocialUI.Minimize()");
                        ReportPanelClosed(socialPanel);
                        TriggerRescan();
                    }
                }
                else
                {
                    // Open the panel
                    var showMethod = socialUI.GetType().GetMethod("ShowSocialEntitiesList",
                        AllInstanceFlags);
                    if (showMethod != null)
                    {
                        showMethod.Invoke(socialUI, null);
                        LogDebug($"[{NavigatorId}] Called SocialUI.ShowSocialEntitiesList()");
                        ReportPanelOpened("Social", socialPanel, PanelDetectionMethod.Reflection);
                        TriggerRescan();
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Error toggling Friends panel: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if element is inside the Social panel.
        /// </summary>
        private bool IsChildOfSocialPanel(GameObject obj)
        {
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            return socialPanel != null && IsChildOf(obj, socialPanel);
        }

        /// <summary>
        /// Discover social tile entries that were missed by the standard CustomButton scan.
        /// Handles two cases:
        /// 1. Tiles with non-CustomButton clickable components
        /// 2. Tiles in collapsed/inactive sections (Blocked section is collapsed by default)
        /// For collapsed sections, expands them first so entries become active and interactable.
        /// </summary>
        private void DiscoverSocialTileEntries(
            HashSet<GameObject> addedObjects,
            List<(GameObject obj, UIElementClassifier.ClassificationResult classification, float sortOrder)> discoveredElements,
            System.Func<GameObject, string> getParentPath)
        {
            // Find the social panel root
            var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
            if (socialPanel == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Social tile scan: social panel not found");
                return;
            }

            // Step 1: Ensure tiles exist for all sections (especially Blocked which is collapsed by default).
            // The game uses a virtualized scroll view - tiles only exist for viewport-visible entries.
            // For blocked users, we need to force-create tiles via the FriendsWidget.
            EnsureAllSocialTilesExist(socialPanel);

            // Step 2: Scan for tile components (now including newly created ones)
            string[] tileTypeNames = { "FriendTile", "InviteOutgoingTile", "InviteIncomingTile", "BlockTile", T.IncomingChallengeRequestTile, T.CurrentChallengeTile };
            int scanned = 0;
            int added = 0;

            foreach (var mb in socialPanel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;

                string typeName = mb.GetType().Name;
                bool isTile = false;
                foreach (var tileName in tileTypeNames)
                {
                    if (typeName == tileName) { isTile = true; break; }
                }
                if (!isTile) continue;
                scanned++;

                var tileObj = mb.gameObject;
                if (!tileObj.activeInHierarchy)
                {
                    MelonLogger.Msg($"[{NavigatorId}] Social tile scan: {typeName} '{tileObj.name}' is INACTIVE");
                    continue;
                }

                // Try to find the clickable child element (Backer_Hitbox is the standard pattern)
                GameObject clickable = null;
                var hitbox = tileObj.transform.Find("Backer_Hitbox");
                if (hitbox != null && hitbox.gameObject.activeInHierarchy)
                    clickable = hitbox.gameObject;

                // Fallback: search immediate children for any Button or CustomButton component
                if (clickable == null)
                {
                    foreach (Transform child in tileObj.transform)
                    {
                        if (!child.gameObject.activeInHierarchy) continue;
                        foreach (var comp in child.GetComponents<MonoBehaviour>())
                        {
                            if (comp != null && (IsCustomButtonType(comp.GetType().Name) || comp is Button))
                            {
                                clickable = child.gameObject;
                                break;
                            }
                        }
                        if (clickable != null) break;
                    }
                }

                // Last resort: use the tile's own GameObject
                if (clickable == null)
                    clickable = tileObj;

                // Skip if this element was already discovered by the standard scan
                if (addedObjects.Contains(clickable)) continue;

                // Get label from FriendInfoProvider for proper "name, status" format
                string label = FriendInfoProvider.GetFriendLabel(clickable);
                if (string.IsNullOrEmpty(label))
                    label = FriendInfoProvider.GetFriendLabel(tileObj);
                if (string.IsNullOrEmpty(label))
                    label = UITextExtractor.GetText(tileObj) ?? tileObj.name;

                var pos = clickable.transform.position;
                float sortOrder = -pos.y * 1000 + pos.x;

                discoveredElements.Add((clickable, new UIElementClassifier.ClassificationResult
                {
                    Role = UIElementClassifier.ElementRole.Button,
                    Label = label,
                    RoleLabel = Models.Strings.RoleButton,
                    IsNavigable = true,
                    ShouldAnnounce = true
                }, sortOrder));
                addedObjects.Add(clickable);
                added++;

                string parentPath = getParentPath(clickable);
                MelonLogger.Msg($"[{NavigatorId}] Social tile fallback: {typeName} -> {clickable.name}, label='{label}', path={parentPath}");
            }

            MelonLogger.Msg($"[{NavigatorId}] Social tile scan: {scanned} tiles found, {added} new entries added");

            // Step 3: Find the StatusButton (local player profile) and register it
            // The StatusButton is a CustomButton showing the player's display name.
            // We register its instance ID so the group assigner routes it to FriendsPanelProfile,
            // and cache the full username (with #number) for label override in the final addition loop.
            _profileButtonGO = null;
            _profileLabel = null;
            var statusButtonGO = FriendInfoProvider.GetStatusButton(socialPanel);
            if (statusButtonGO != null)
            {
                var (fullName, statusText) = FriendInfoProvider.GetLocalPlayerInfo(socialPanel);
                if (!string.IsNullOrEmpty(fullName))
                {
                    _profileButtonGO = statusButtonGO;
                    _profileLabel = !string.IsNullOrEmpty(statusText)
                        ? $"{fullName}, {statusText}"
                        : fullName;
                    _groupAssigner.SetProfileButtonId(statusButtonGO.GetInstanceID());
                    MelonLogger.Msg($"[{NavigatorId}] Profile button registered: {statusButtonGO.name} (ID:{statusButtonGO.GetInstanceID()}), label='{_profileLabel}'");
                }
            }
        }

        /// <summary>
        /// Ensure all social tile entries exist, especially for the Blocked section.
        /// The game uses a virtualized scroll view and the Blocked section is collapsed by default,
        /// so tiles may not exist. We force-create them via the FriendsWidget using reflection.
        /// </summary>
        private void EnsureAllSocialTilesExist(GameObject socialPanel)
        {
            // Find FriendsWidget component
            Component widget = null;
            foreach (var mb in socialPanel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && mb.GetType().Name == T.FriendsWidget)
                {
                    widget = mb;
                    break;
                }
            }

            if (widget == null)
            {
                MelonLogger.Msg($"[{NavigatorId}] FriendsWidget not found in social panel");
                return;
            }

            var widgetType = widget.GetType();
            var flags = AllInstanceFlags;

            // Open all collapsed sections by setting _isOpen directly (avoids triggering sound effects)
            foreach (var sectionName in new[] { "SectionBlocks", "SectionIncomingInvites", "SectionOutgoingInvites", "SectionFriends", "SectionIncomingChallengeRequest" })
            {
                var sectionField = widgetType.GetField(sectionName, flags);
                if (sectionField == null) continue;

                var header = sectionField.GetValue(widget) as Component;
                if (header == null) continue;

                var isOpenField = header.GetType().GetField("_isOpen", PrivateInstance);
                if (isOpenField != null)
                {
                    bool isOpen = (bool)isOpenField.GetValue(header);
                    if (!isOpen)
                    {
                        isOpenField.SetValue(header, true);
                        MelonLogger.Msg($"[{NavigatorId}] Opened social section: {sectionName}");
                    }
                }
            }

            // Force-create blocked user tiles.
            // The game's virtualized list only creates tiles within the viewport.
            // Blocked section is at the bottom and collapsed, so tiles never get created.
            EnsureBlockedTilesExist(widget, widgetType, flags);
            EnsureChallengeTilesExist(widget, widgetType, flags);
        }

        /// <summary>
        /// Ensure BlockTile instances exist for all blocked users.
        /// Creates tiles from the prefab and initializes them with Block data,
        /// mirroring what FriendsWidget.CreateWidget_Block + UpdateBlocksList do.
        /// </summary>
        private void EnsureBlockedTilesExist(Component widget, Type widgetType, BindingFlags flags)
        {
            try
            {
                // Access _socialManager from FriendsWidget
                var smField = widgetType.GetField("_socialManager", flags);
                var socialManager = smField?.GetValue(widget);
                if (socialManager == null) return;

                // Get blocked users list: _socialManager.Blocks
                var blocksProp = socialManager.GetType().GetProperty("Blocks");
                var blocks = blocksProp?.GetValue(socialManager) as System.Collections.IList;
                if (blocks == null || blocks.Count == 0) return;

                // Get the section header
                var sectionField = widgetType.GetField("SectionBlocks", flags);
                var section = sectionField?.GetValue(widget) as Component;
                if (section == null) return;

                // Ensure section header is visible (SetCount activates the header if count > 0)
                var setCount = section.GetType().GetMethod("SetCount");
                setCount?.Invoke(section, new object[] { blocks.Count });

                // Get the active block tiles pool
                var tilesField = widgetType.GetField("_activeBlockTiles", flags);
                var activeTiles = tilesField?.GetValue(widget) as System.Collections.IList;
                if (activeTiles == null) return;

                // Get the prefab for BlockTile
                var prefabField = widgetType.GetField("_prefabBlockTile", flags);
                var prefab = prefabField?.GetValue(widget) as Component;
                if (prefab == null) return;

                // Get RemoveBlock method from social manager for callback
                var removeBlockMethod = socialManager.GetType().GetMethod("RemoveBlock");

                // Get WidgetSize for tile positioning
                var widgetSizeField = widgetType.GetField("WidgetSize", flags);
                float widgetSize = widgetSizeField != null ? (float)widgetSizeField.GetValue(widget) : 60f;

                // Create tiles until we have enough for all blocked users
                int created = 0;
                for (int i = activeTiles.Count; i < blocks.Count; i++)
                {
                    var tile = UnityEngine.Object.Instantiate(prefab, section.transform);
                    activeTiles.Add(tile);
                    created++;

                    // Set Callback_RemoveBlock = _socialManager.RemoveBlock
                    if (removeBlockMethod != null)
                    {
                        var callbackField = tile.GetType().GetField("Callback_RemoveBlock", flags);
                        if (callbackField != null)
                        {
                            try
                            {
                                var callback = Delegate.CreateDelegate(callbackField.FieldType, socialManager, removeBlockMethod);
                                callbackField.SetValue(tile, callback);
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"[{NavigatorId}] Failed to set RemoveBlock callback: {ex.Message}");
                            }
                        }
                    }
                }

                // Initialize all tiles with their block data and position them
                var initMethod = prefab.GetType().GetMethod("Init", flags);
                for (int i = 0; i < blocks.Count && i < activeTiles.Count; i++)
                {
                    var tile = activeTiles[i] as Component;
                    if (tile == null) continue;

                    initMethod?.Invoke(tile, new[] { blocks[i] });

                    // Position tile for proper sort order within the group
                    var rt = tile.transform as RectTransform;
                    if (rt != null)
                        rt.anchoredPosition = new Vector2(0f, -i * widgetSize);
                }

                if (created > 0 || blocks.Count > 0)
                    MelonLogger.Msg($"[{NavigatorId}] Blocked tiles: {blocks.Count} blocked users, {created} new tiles created, {activeTiles.Count} total");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Error ensuring blocked tiles: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensure challenge tiles (IncomingChallengeRequestTile, CurrentChallengeTile) are created.
        /// The game uses a virtualized scroll view for challenges. Calling UpdateChallengeList()
        /// on the FriendsWidget forces tile creation based on viewport bounds.
        /// </summary>
        private void EnsureChallengeTilesExist(Component widget, Type widgetType, BindingFlags flags)
        {
            try
            {
                // Set scroll to top so all tiles are in the viewport for creation
                var scrollField = widgetType.GetField("ChallengeListScroll", PublicInstance);
                var scrollRect = scrollField?.GetValue(widget) as ScrollRect;
                if (scrollRect != null)
                    scrollRect.verticalNormalizedPosition = 1f;

                // Call UpdateChallengeList() to force tile creation
                var updateMethod = widgetType.GetMethod("UpdateChallengeList", AllInstanceFlags);
                if (updateMethod != null)
                {
                    updateMethod.Invoke(widget, null);
                    MelonLogger.Msg($"[{NavigatorId}] Called UpdateChallengeList to ensure challenge tiles");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Error ensuring challenge tiles: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if we're currently inside a FriendSection group (actual friends, incoming, outgoing, blocked).
        /// </summary>
        private bool IsFriendSectionActive()
        {
            return _groupedNavigationEnabled && _groupedNavigator.IsActive
                && _groupedNavigator.Level == NavigationLevel.InsideGroup
                && _groupedNavigator.CurrentGroup.HasValue
                && _groupedNavigator.CurrentGroup.Value.Group.IsFriendSectionGroup();
        }

        /// <summary>
        /// Initialize friend actions for the current friend entry.
        /// Called when entering a friend section or moving to a new friend.
        /// </summary>
        private void RefreshFriendActions()
        {
            _friendActions = null;
            _friendActionIndex = 0;

            var element = _groupedNavigator.CurrentElement;
            if (!element.HasValue || element.Value.GameObject == null) return;

            _friendActions = FriendInfoProvider.GetFriendActions(element.Value.GameObject);
            LogDebug($"[{NavigatorId}] Friend actions: {_friendActions?.Count ?? 0} for {element.Value.Label}");
        }

        /// <summary>
        /// Handle Left/Right navigation through friend actions.
        /// </summary>
        private void HandleFriendActionNavigation(bool isRight)
        {
            if (_friendActions == null || _friendActions.Count == 0)
            {
                _announcer.Announce(Strings.NoAlternateAction, AnnouncementPriority.Normal);
                return;
            }

            if (isRight)
            {
                if (_friendActionIndex >= _friendActions.Count - 1)
                {
                    // Re-announce current action at boundary (like GroupedNavigator does for elements)
                    AnnounceFriendActionPosition();
                    _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                    return;
                }
                _friendActionIndex++;
            }
            else
            {
                if (_friendActionIndex <= 0)
                {
                    // Re-announce current action at boundary
                    AnnounceFriendActionPosition();
                    _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                    return;
                }
                _friendActionIndex--;
            }

            AnnounceFriendActionPosition();
        }

        /// <summary>
        /// Announce the current friend action with its position (e.g., "Revoke, 1 of 1").
        /// </summary>
        private void AnnounceFriendActionPosition()
        {
            if (_friendActions == null || _friendActions.Count == 0) return;
            if (_friendActionIndex < 0 || _friendActionIndex >= _friendActions.Count) return;

            var (label, _) = _friendActions[_friendActionIndex];
            _announcer.AnnounceInterrupt(
                Strings.ItemPositionOf(_friendActionIndex + 1, _friendActions.Count, label));
        }

        /// <summary>
        /// Announce the first/default friend action after entering a section or moving to a new friend.
        /// Tells the user what pressing Enter will do.
        /// </summary>
        private void AnnounceFirstFriendAction()
        {
            if (_friendActions == null || _friendActions.Count == 0) return;

            var (label, _) = _friendActions[0];
            _announcer.Announce(
                Strings.ItemPositionOf(1, _friendActions.Count, label),
                AnnouncementPriority.Normal);
        }

        /// <summary>
        /// Announce the current friend entry with name and status.
        /// </summary>
        private void AnnounceFriendEntry()
        {
            var element = _groupedNavigator.CurrentElement;
            if (!element.HasValue || element.Value.GameObject == null) return;

            string friendLabel = FriendInfoProvider.GetFriendLabel(element.Value.GameObject);
            if (!string.IsNullOrEmpty(friendLabel))
            {
                int count = _groupedNavigator.CurrentGroup?.Count ?? 0;
                int idx = _groupedNavigator.CurrentElementIndex + 1;
                _announcer.AnnounceInterrupt(Strings.ItemPositionOf(idx, count, friendLabel));
            }
            else
            {
                // Fallback to standard announcement
                _announcer.AnnounceInterrupt(_groupedNavigator.GetCurrentAnnouncement());
            }
        }
    }
}
