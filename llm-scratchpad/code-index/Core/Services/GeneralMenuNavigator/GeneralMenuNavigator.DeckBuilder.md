# GeneralMenuNavigator.DeckBuilder.cs
Path: src/Core/Services/GeneralMenuNavigator/GeneralMenuNavigator.DeckBuilder.cs
Lines: 1063

## Top-level comments
- Partial class hosting deck builder and deck-manager concerns: deck toolbar attached actions, rename/favorite/clone special handling, card-pool/deck-list/commander/sideboard/read-only card discovery, DeckBuilderInfo 2D sub-navigation, and NPE reward card discovery.

## public partial class GeneralMenuNavigator (line 22)
### Fields
- private bool _announceDeckCountOnRescan (line 26)
- private string _deckCountBeforeActivation (line 29)
- private bool _isDeckBuilderReadOnly (line 32)
- private bool _isInRenameMode (line 35)
- private MonoBehaviour _cachedDeckManagerController (line 38)
- private List<(string label, List<string> entries)> _deckInfoRows (line 42)
- private int _deckInfoEntryIndex (line 43)
- private static readonly ElementGroup[] DeckBuilderCycleGroups (line 46)
- private static readonly string[] StandaloneMainButtonNames (line 61)

### Methods
- private bool HandleDeckBuilderBack() (line 82) — Note: activates DeckBuilderWidget's MainButton ("Fertig"/"Done") to exit; falls back to NavigateToHome
- private Transform FindDeckViewParent(Transform element) (line 116)
- private DeckToolbarButtons FindDeckToolbarButtons() (line 140) — Note: whitelists Import/Sammlung/Collection as standalone; rest get hidden from navigation and surfaced as deck attached actions
- private List<AttachedAction> BuildDeckAttachedActions(DeckToolbarButtons toolbarButtons, GameObject renameButton) (line 196)
- private static string ResolveDeckActionLabel(string locKey, GameObject button, string fallback) (line 234)
- protected override bool HandleAttachedAction(AttachedAction action) (line 262) — Note: Rename enters edit mode on TMP_InputField directly (avoids anti-autofocus); Favorite reads IsFavorite before activating; Clone announces re-enter reminder
- protected override void HandleInputFieldNavigation() (line 305) — Note: in rename mode, intercepts Enter to force-exit edit mode and announce the new name
- private bool? TryGetDeckFavoriteState() (line 333)
- private void AutoFocusUnlockedCard() (line 379) — Note: NPE rewards screen only; moves the unlocked card to index 0
- protected override void OnDeckBuilderCardCountCapture() (line 409)
- protected override void OnDeckBuilderCardActivated() (line 421) — Note: sets _announceDeckCountOnRescan so PerformRescan announces only the count change; invalidates CardNavigator blocks
- private void FindNPERewardCards(HashSet<GameObject> addedObjects) (line 439) — Note: searches NPE-Rewards_Container for card prefabs/CDCs, skips DeckBox/Deckbox/RewardChest children, also adds NullClaimButton as "Take reward"
- private void FindPoolHolderCards(HashSet<GameObject> addedObjects) (line 590) — Note: uses CardPoolAccessor to get only the current page's cards (3-page rotation)
- private void FindPoolHolderCardsFallback(HashSet<GameObject> addedObjects) (line 663)
- private void FindCommanderCards(HashSet<GameObject> addedObjects) (line 719) — Note: uses TileButton as navigable element; marks TagButton + CardGameObject as excluded from generic scan
- private void FindDeckListCards(HashSet<GameObject> addedObjects) (line 766)
- private void FindSideboardCards(HashSet<GameObject> addedObjects) (line 815)
- private void FindReadOnlyDeckCards(HashSet<GameObject> addedObjects) (line 854) — Note: only runs when the normal deck list is empty; sets _isDeckBuilderReadOnly flag
- private void InjectDeckInfoGroup() (line 909) — Note: adds virtual DeckBuilderInfo group (card count, mana curve, etc.) after DeckBuilderDeckList
- private void RefreshDeckInfoLabels() (line 944)
- private bool IsDeckInfoSubNavActive() (line 961)
- private void InitializeDeckInfoSubNav() (line 974)
- private bool HandleDeckInfoEntryNavigation(bool isRight) (line 985) — Note: Left/Right navigates entries within the current DeckBuilderInfo row
- private void AnnounceDeckInfoEntry(bool includeRowName) (line 1022)
- private void RefreshDeckInfoSubNav() (line 1048)

## private struct DeckToolbarButtons (nested in GeneralMenuNavigator) (line 66)
### Fields
- public GameObject EditButton (line 68)
- public GameObject DeleteButton (line 69)
- public GameObject ExportButton (line 70)
- public GameObject FavoriteButton (line 71)
- public GameObject CloneButton (line 72)
- public GameObject DetailsButton (line 73)
- public HashSet<GameObject> AllDeckSpecificButtons (line 75)
