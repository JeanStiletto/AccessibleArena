# GeneralMenuNavigator.Social.cs
Path: src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.Social.cs
Lines: 553

## Top-level comments
- Partial class hosting the Friends/Social panel: open/close via SocialUI reflection, tile discovery (including virtualized Blocked and Challenge sections), profile button registration, and friend-action Left/Right sub-navigation.

## public partial class GeneralMenuNavigator (line 22)
### Fields
- private GameObject _profileButtonGO (line 25)
- private string _profileLabel (line 26)
- private List<(string label, string actionId)> _friendActions (line 30)
- private int _friendActionIndex (line 31)

### Methods
- protected bool IsSocialPanelOpen() (line 36)
- private bool CloseSocialPanel() (line 41) — Note: calls SocialUI.Minimize() via reflection, falls back to generic back button
- private void ToggleFriendsPanel() (line 64) — Note: F4 handler; calls SocialUI.ShowSocialEntitiesList / Minimize based on current state
- private bool IsChildOfSocialPanel(GameObject obj) (line 124)
- private void DiscoverSocialTileEntries(HashSet<GameObject> addedObjects, List<(GameObject obj, UIElementClassifier.ClassificationResult classification, float sortOrder)> discoveredElements, System.Func<GameObject, string> getParentPath) (line 137) — Note: scans FriendTile/InviteOutgoingTile/InviteIncomingTile/BlockTile/Challenge tiles, falls back past Backer_Hitbox for BlockTile, registers the StatusButton profile for label override
- private void EnsureAllSocialTilesExist(GameObject socialPanel) (line 265) — Note: force-opens collapsed section headers (_isOpen) and triggers tile creation for Blocked + Challenge sections
- private void EnsureBlockedTilesExist(Component widget, Type widgetType, BindingFlags flags) (line 320) — Note: instantiates _prefabBlockTile, calls Init with each Block, positions tiles via WidgetSize, wires Callback_RemoveBlock delegate
- private void EnsureChallengeTilesExist(Component widget, Type widgetType, BindingFlags flags) (line 416) — Note: resets ChallengeListScroll to top and calls UpdateChallengeList() to force tile creation
- private bool IsFriendSectionActive() (line 443)
- private void RefreshFriendActions() (line 455)
- private void HandleFriendActionNavigation(bool isRight) (line 470) — Note: at boundaries re-announces current action then announces End/Beginning of list
- private void AnnounceFriendActionPosition() (line 507)
- private void AnnounceFirstFriendAction() (line 521) — Note: announces the default action so the user knows what Enter will do
- private void AnnounceFriendEntry() (line 534)
