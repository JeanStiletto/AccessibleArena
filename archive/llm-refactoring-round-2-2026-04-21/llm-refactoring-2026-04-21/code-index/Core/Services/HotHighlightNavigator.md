# HotHighlightNavigator.cs
Path: src/Core/Services/HotHighlightNavigator.cs
Lines: 1484

## Top-level comments
- Unified navigator for all HotHighlight-based navigation (replaces TargetNavigator, HighlightNavigator, DiscardNavigator). Detects selection mode (discard etc.) by checking for Submit buttons with counts, and uses single-click instead of two-click for hand cards in that mode. Hand cards in selection mode single-click-toggle; battlefield/stack cards and player portraits with HotHighlight single-click.

## public class HotHighlightNavigator (line 32)

### Fields
- private readonly IAnnouncementService _announcer (line 34)
- private readonly ZoneNavigator _zoneNavigator (line 35)
- private BattlefieldNavigator _battlefieldNavigator (line 36)
- private List<HighlightedItem> _items = new List<HighlightedItem>() (line 38)
- private int _currentIndex = -1 (line 39)
- private int _opponentIndex = -1 (line 40)
- private bool _isActive (line 41)
- private bool _wasInSelectionMode (line 42)
- private bool _snapshotValid (line 43)
- private string _lastItemZone (line 46)
- private string _lastPromptButtonText (line 49)
- private static readonly Regex ButtonNumberPattern = new Regex(@"(\d+)", RegexOptions.IgnoreCase) (line 53)
- private int _lastDiagHandHighlighted = -1 (line 56)
- private int _lastDiagBattlefieldHighlighted = -1 (line 57)
- private static Type _avatarViewType (line 60)
- private static FieldInfo _highlightSystemField (line 61) — DuelScene_AvatarView._highlightSystem
- private static FieldInfo _currentHighlightField (line 62) — HighlightSystem._currentHighlightType
- private static PropertyInfo _isLocalPlayerProp (line 63) — DuelScene_AvatarView.IsLocalPlayer
- private static FieldInfo _portraitButtonField (line 64) — DuelScene_AvatarView.PortraitButton
- private static bool _avatarReflectionInitialized (line 65)
- private readonly List<MonoBehaviour> _cachedAvatarViews = new List<MonoBehaviour>() (line 68)

### Properties
- public bool IsActive => _isActive (line 70)
- public int ItemCount => _items.Count (line 71)
- public HighlightedItem CurrentItem (line 72)
- public bool HasTargetsHighlighted (line 81)
- public bool HasPlayableHighlighted (line 87)

### Methods
- public void SetBattlefieldNavigator(BattlefieldNavigator battlefieldNavigator) (line 95)
- public void Activate() (line 100)
- public void Deactivate() (line 106)
- public void ClearState() (line 125)
- public bool HandleInput() (line 142)
- private void RefreshOrRebuildHighlights() (line 341)
- private Dictionary<int, HighlightedItem> ScanCurrentHighlights(bool selectionMode) (line 361)
- private void DiscoverAllHighlights() (line 443)
- private void RefreshHighlightsStable() (line 495)
- private static (int zone, int player, int opponent) GetSortKey(HighlightedItem item) (line 545)
- private int FindSortedInsertionIndex(HighlightedItem newItem) (line 560)
- private void FindAndCacheAvatarViews() (line 576)
- private HighlightedItem CreateAvatarTargetItem(MonoBehaviour avatarView) (line 594)
- private HighlightedItem CreateHighlightedItem(GameObject go, string highlightType) (line 631)
- private static void InitializeAvatarReflection(Type avatarType) (line 663)
- private void AnnounceCurrentItem() (line 712)
- private void ActivateCurrentItem() (line 780)
- private string DetectZone(GameObject obj) (line 893)
- private string DetermineCardType(GameObject go) (line 919)
- private static ActivationResult ClickBattlefieldCard(GameObject card) (line 936)
- private ZoneType StringToZoneType(string zone) (line 988)
- private string GetPrimaryButtonText() (line 1006)
- private string GetButtonTextWithMana(GameObject button) (line 1016)
- private GameObject FindPrimaryButton() (line 1042)
- private GameObject FindSecondaryButton() (line 1056)
- private GameObject FindUndoButton() (line 1071)
- private bool IsMeaningfulButtonText(string text) (line 1082)
- private bool IsButtonVisible(GameObject button) (line 1094)
- private void DiscoverPromptButtons() (line 1105)
- public void MonitorPromptButtons(float timeSincePhaseChange, bool isInCombatPhase) (line 1151)
- private bool IsSelectionModeActive() (line 1191)
- private (int count, GameObject button)? GetSubmitButtonInfo() (line 1211)
- private IEnumerator AnnounceSelectionToggleDelayed(string cardName, bool wasSelected, int preClickCount = -1, string clickedCdcName = null) (line 1245)
- private bool IsCardSelected(GameObject card) (line 1287)
- private void LogSelectionIndicatorScan(string clickedCdcName) (line 1312)
- private void CheckSelectionModeTransition(bool isActive) (line 1334)
- private string GetPromptInstructionText() (line 1371)
- private int GetRequiredCountFromPrompt() (line 1395)
- public string GetSelectionStateText(GameObject card) (line 1418)
- public bool IsInSelectionMode() (line 1430)
- public bool IsCardCurrentlySelected(GameObject card) (line 1436)
- public bool TryToggleSelection(GameObject card) (line 1443)

## public class HighlightedItem (line 1472)

### Properties
- public GameObject GameObject { get; set; } (line 1474)
- public string Name { get; set; } (line 1475)
- public string Zone { get; set; } (line 1476)
- public string HighlightType { get; set; } (line 1477)
- public bool IsOpponent { get; set; } (line 1478)
- public bool IsPlayer { get; set; } (line 1479)
- public string CardType { get; set; } (line 1480)
- public string PowerToughness { get; set; } (line 1481)
- public bool IsPromptButton { get; set; } (line 1482)
