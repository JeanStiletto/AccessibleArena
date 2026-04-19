# CombatNavigator.cs
Path: src/Core/Services/CombatNavigator.cs
Lines: 763

## Top-level comments
- Handles combat phase navigation. Declare Attackers: Space = All Attack/X Attack, Backspace = No Attacks. Declare Blockers: Space = confirm (X Blocker/Next), Backspace = No Blocks/Cancel Blocks. Tracks selected/assigned blockers and announces combined power/toughness.

## public class CombatNavigator (line 22)
### Fields
- private readonly IAnnouncementService _announcer (line 24)
- private readonly DuelAnnouncer _duelAnnouncer (line 25)
- private HashSet<int> _previousSelectedBlockerIds = new HashSet<int>() (line 28)
- private Dictionary<int, GameObject> _previousSelectedBlockerObjects = new Dictionary<int, GameObject>() (line 29)
- private HashSet<int> _previousAssignedBlockerIds = new HashSet<int>() (line 32)
- private Dictionary<int, GameObject> _previousAssignedBlockerObjects = new Dictionary<int, GameObject>() (line 33)
- private bool _wasInBlockersPhase = false (line 36)
- private const float BlockerScanIntervalSeconds = 0.15f (line 39)
- private float _lastBlockerScanTime (line 40)
- private float _confirmHintTime (line 43)
- private bool _confirmHintPending (line 44)
- private bool _debugAttackerCards = false (line 55)
### Properties
- public bool IsInCombatPhase (line 46)
### Methods
- public CombatNavigator(IAnnouncementService announcer, DuelAnnouncer duelAnnouncer) (line 48)
- public bool IsCreatureAttacking(GameObject card) (line 61) — Note: model-based check first, UI fallback for Declared/Selected/Lobbed transitional states
- private void LogAttackerRelevantChildren(GameObject card) (line 96) — Note: debug logger gated on _debugAttackerCards
- public bool IsCreatureBlocking(GameObject card) (line 123)
- public bool IsCreatureSelectedAsBlocker(GameObject card) (line 148) — Note: requires BOTH CombatIcon_BlockerFrame AND SelectedHighlightBattlefield
- private List<GameObject> FindSelectedBlockers() (line 173)
- private List<GameObject> FindAssignedBlockers() (line 174)
- private List<GameObject> FindCardsByPredicate(Func<GameObject, bool> predicate) (line 180) — Note: uses DuelHolderCache.GetHolder("BattlefieldCardHolder") to avoid full scene scan
- private (int power, int toughness) GetPowerToughness(GameObject card) (line 202)
- private (int totalPower, int totalToughness) CalculateCombinedStats(List<GameObject> blockers) (line 227)
- public void UpdateBlockerSelection() (line 247) — Note: called each frame during declare blockers; resets tracking only when ENTERING phase (not exiting) because game can briefly report exit during assignment; scan throttled to 150ms
- public string GetCombatStateText(GameObject card) (line 407) — Note: priority is attacking > selected-to-attack > can-attack (similarly for blocking); blocked-by names resolved via instance IDs; summoning sickness skipped during declare blockers
- private string GetAttackTargetText(GameObject card) (line 501) — Note: returns "attacking {CardName}" for planeswalker/battle targets, null for player target
- private string GetBlockingText(GameObject card) (line 521)
- private string GetBlockedByText(GameObject card) (line 524) — Note: resolves instance IDs to card names with P/T, uses 1/2/many formatter
- private string GetCombatRelationText(GameObject card, Func<object, List<uint>> getIds, Func<string, string> formatText) (line 556)
- public bool HandleInput() (line 590) — Note: calls UpdateBlockerSelection every frame; guards against transitional UI state by checking HasPrimaryButtonText; does NOT handle main-phase Space (game handles natively)
- private bool TryClickPrimaryButton() (line 639)
- private bool TryClickSecondaryButton() (line 640)
- private bool HasPrimaryButtonText() (line 642)
- private bool TryClickPromptButton(bool isPrimary) (line 654) — Note: language-agnostic; identifies button by GO name, announces localized text
- private GameObject FindPromptButton(bool isPrimary) (line 684) — Note: prefers button with highest parent CanvasGroup alpha to skip stale buttons fading out from previous phase
- private float GetParentCanvasGroupAlpha(GameObject obj) (line 720) — Note: returns minimum alpha in 10 ancestors; 1.0 if no CanvasGroup found
- private bool IsInEmotePanel(GameObject obj) (line 741) — Note: walks 8 ancestors for EmoteOptionsPanel/CommunicationOptionsPanel/EmoteView/NavArrow
