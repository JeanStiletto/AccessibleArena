using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Unified navigator for all HotHighlight-based navigation.
    /// Replaces TargetNavigator, HighlightNavigator, and DiscardNavigator.
    ///
    /// Key insight: The game correctly manages HotHighlight to show only what's
    /// relevant in the current context. We detect "selection mode" (discard, etc.)
    /// by checking for Submit buttons with counts, and use single-click instead
    /// of two-click for hand cards in that mode.
    ///
    /// - Hand cards in selection mode = single-click to toggle selection
    /// - Hand cards normally = two-click to play
    /// - Battlefield/Stack cards with HotHighlight = valid targets (single-click)
    /// - Player portraits with HotHighlight = player targets (single-click)
    /// </summary>
    public class HotHighlightNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ZoneNavigator _zoneNavigator;
        private BattlefieldNavigator _battlefieldNavigator;

        private List<HighlightedItem> _items = new List<HighlightedItem>();
        private int _currentIndex = -1;
        private int _opponentIndex = -1;
        private bool _isActive;
        private bool _wasInSelectionMode;
        private bool _snapshotValid;

        // Track last zone/row to detect zone changes on Tab
        private string _lastItemZone;

        // Track prompt button text to announce when meaningful choices appear
        private string _lastPromptButtonText;

        // Default primary-button text seen at start of the current combat phase
        // (e.g. "All Attack" / "Alle greifen an"). Used to suppress re-announcing the
        // default when the user deselects the last attacker/blocker, which would
        // otherwise sound like an action the user took.
        private string _combatPhaseDefaultButtonText;

        // Selection mode detection (discard, choose cards to exile, etc.)
        // Matches any number in button text: "Submit 2", "2 abwerfen", "0 bestätigen"
        private static readonly Regex ButtonNumberPattern = new Regex(@"(\d+)", RegexOptions.IgnoreCase);

        // Previous DIAG counts - only log when changed
        private int _lastDiagHandHighlighted = -1;
        private int _lastDiagBattlefieldHighlighted = -1;
        private int _lastDiscoveryCount = -1;

        // Avatar targeting reflection cache
        private sealed class AvatarViewHandles
        {
            public FieldInfo HighlightSystem;  // DuelScene_AvatarView._highlightSystem
            public PropertyInfo IsLocalPlayer; // DuelScene_AvatarView.IsLocalPlayer
            public FieldInfo PortraitButton;   // DuelScene_AvatarView.PortraitButton
        }

        private sealed class HighlightSystemHandles
        {
            public FieldInfo CurrentHighlight; // HighlightSystem._currentHighlightType
        }

        private static Type _avatarViewType;
        private static bool _avatarReflectionInitialized;

        private static readonly ReflectionCache<AvatarViewHandles> _avatarCache = new ReflectionCache<AvatarViewHandles>(
            builder: t => new AvatarViewHandles
            {
                HighlightSystem = t.GetField("_highlightSystem", PrivateInstance),
                IsLocalPlayer = t.GetProperty("IsLocalPlayer", PublicInstance),
                PortraitButton = t.GetField("PortraitButton", PrivateInstance),
            },
            validator: h => h.HighlightSystem != null && h.IsLocalPlayer != null && h.PortraitButton != null,
            logTag: "HotHighlightNavigator",
            logSubject: "DuelScene_AvatarView");

        private static readonly ReflectionCache<HighlightSystemHandles> _highlightSystemCache = new ReflectionCache<HighlightSystemHandles>(
            builder: t => new HighlightSystemHandles
            {
                CurrentHighlight = t.GetField("_currentHighlightType", PrivateInstance),
            },
            validator: h => h.CurrentHighlight != null,
            logTag: "HotHighlightNavigator",
            logSubject: "HighlightSystem");

        // Cached avatar view references (only 2 per duel: local + opponent)
        private readonly List<MonoBehaviour> _cachedAvatarViews = new List<MonoBehaviour>();

        public bool IsActive => _isActive;
        public int ItemCount => _items.Count;
        public HighlightedItem CurrentItem =>
            (_currentIndex >= 0 && _currentIndex < _items.Count)
                ? _items[_currentIndex]
                : null;

        /// <summary>
        /// Returns true if any battlefield/stack targets are highlighted.
        /// Used by other systems that need to know if targeting is active.
        /// </summary>
        public bool HasTargetsHighlighted => _items.Any(i =>
            i.Zone == "Battlefield" || i.Zone == "Stack" || i.IsPlayer);

        /// <summary>
        /// Returns true if hand cards are highlighted (playable).
        /// </summary>
        public bool HasPlayableHighlighted => _items.Any(i => i.Zone == "Hand");

        public HotHighlightNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = zoneNavigator;
        }

        public void SetBattlefieldNavigator(BattlefieldNavigator battlefieldNavigator)
        {
            _battlefieldNavigator = battlefieldNavigator;
        }

        public void Activate()
        {
            _isActive = true;
            Log.Msg("HotHighlightNavigator", "Activated");
        }

        public void Deactivate()
        {
            _isActive = false;
            _wasInSelectionMode = false;
            _snapshotValid = false;
            _items.Clear();
            _currentIndex = -1;
            _opponentIndex = -1;
            _lastItemZone = null;
            _lastPromptButtonText = null;
            _combatPhaseDefaultButtonText = null;
            _cachedAvatarViews.Clear();
            PhaseSkipGuard.Reset();
            Log.Msg("HotHighlightNavigator", "Deactivated");
        }

        /// <summary>
        /// Clears any stale highlight state without deactivating.
        /// Called when user navigates to a zone using shortcuts (C/G/X/S).
        /// </summary>
        public void ClearState()
        {
            if (_items.Count > 0)
            {
                Log.Msg("HotHighlightNavigator", "Clearing state due to zone navigation");
                _items.Clear();
                _currentIndex = -1;
                _opponentIndex = -1;
                _lastItemZone = null;
                _snapshotValid = false;
            }
        }

        /// <summary>
        /// Handles Tab/Enter/Backspace input for highlight navigation.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isActive) return false;

            // Ctrl+Tab / Ctrl+Shift+Tab - cycle through opponent targets only
            if (Input.GetKeyDown(KeyCode.Tab) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                RefreshOrRebuildHighlights();

                var opponentItems = new List<int>();
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].IsOpponent)
                        opponentItems.Add(i);
                }

                if (opponentItems.Count == 0)
                    return true; // No opponent targets, consume input silently

                // Cycle forward or backward through opponent items
                if (shift)
                {
                    _opponentIndex--;
                    if (_opponentIndex < 0)
                        _opponentIndex = opponentItems.Count - 1;
                }
                else
                {
                    _opponentIndex++;
                    if (_opponentIndex >= opponentItems.Count)
                        _opponentIndex = 0;
                }

                _currentIndex = opponentItems[_opponentIndex];
                AnnounceCurrentItem();
                return true;
            }

            // Tab - cycle through highlighted items
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                RefreshOrRebuildHighlights();

                if (_items.Count == 0)
                {
                    // Check if there's a primary button to show game state (Pass, Resolve, Next, etc.)
                    string primaryButtonText = GetPrimaryButtonText();
                    if (!string.IsNullOrEmpty(primaryButtonText))
                    {
                        _announcer.Announce(primaryButtonText, AnnouncementPriority.High);
                    }
                    else
                    {
                        _announcer.Announce(Strings.NoPlayableCards, AnnouncementPriority.High);
                    }
                    return true;
                }

                // Cycle through items
                if (shift)
                {
                    _currentIndex--;
                    if (_currentIndex < 0)
                        _currentIndex = _items.Count - 1;
                }
                else
                {
                    _currentIndex = (_currentIndex + 1) % _items.Count;
                }

                AnnounceCurrentItem();
                return true;
            }

            // Enter - activate current item (only if we still have zone ownership)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                // Ctrl+Enter is the stack-selection shortcut owned by BattlefieldNavigator.
                // Don't intercept it here, even if the primary button has a count (e.g. "3 Angreifer"
                // during Declare Attackers makes IsSelectionModeActive return true).
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                // In selection mode, handle Enter even if BattlefieldNavigator has zone ownership.
                // Tab navigates to battlefield cards which delegates to BattlefieldNavigator (Left/Right work),
                // but Enter should toggle selection rather than doing a regular click.
                var submitInfo = (!ctrl && IsSelectionModeActive()) ? GetSubmitButtonInfo() : null;
                if (submitInfo != null && _zoneNavigator.CurrentZoneOwner == ZoneOwner.BattlefieldNavigator
                    && _battlefieldNavigator != null)
                {
                    var card = _battlefieldNavigator.GetCurrentCard();
                    if (card != null)
                    {
                        int preClickCount = submitInfo.Value.count;
                        string cardName = CardDetector.GetCardName(card) ?? card.name;
                        bool wasSelected = IsCardSelected(card);
                        Log.Msg("HotHighlightNavigator", $"Toggling battlefield selection: {cardName} on {card.name} (was selected: {wasSelected}, preCount: {preClickCount})");
                        var result = ClickBattlefieldCard(card);
                        // Game may reshuffle stack membership in response to selection — invalidate
                        // BattlefieldNavigator's cached row layout and stack-size index.
                        _battlefieldNavigator?.MarkDirty();
                        if (result.Success)
                            MelonCoroutines.Start(AnnounceSelectionToggleDelayed(cardName, wasSelected, preClickCount, card.name));
                        else
                            _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);
                        return true;
                    }
                }

                // Check if we still have zone ownership - user may have navigated away
                // using zone shortcuts (C, G, X, S) or battlefield shortcuts (A, B, R)
                if (_zoneNavigator.CurrentZoneOwner != ZoneOwner.HighlightNavigator)
                {
                    // We lost ownership - clear stale state and let other handlers process Enter
                    if (_items.Count > 0)
                    {
                        Log.Msg("HotHighlightNavigator", $"Clearing stale state - zone owner is {_zoneNavigator.CurrentZoneOwner}");
                        _items.Clear();
                        _currentIndex = -1;
                        _opponentIndex = -1;
                        _snapshotValid = false;
                    }
                    return false;
                }

                if (_currentIndex >= 0 && _currentIndex < _items.Count)
                {
                    ActivateCurrentItem();
                    return true;
                }
                return false; // Let other handlers deal with Enter
            }

            // Space - click primary button when no highlights are available,
            // or when in selection mode (to submit the selection/discard).
            // Phase skip warning is handled at Input.GetKeyDown level by PhaseSkipGuard via
            // EventSystemPatch.GetKeyDown_Postfix — Space returns false while warning is pending,
            // so this block simply won't execute on a blocked press.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_items.Count == 0 || IsSelectionModeActive())
                {
                    var primaryButton = FindPrimaryButton();
                    if (primaryButton != null)
                    {
                        string buttonText = GetPrimaryButtonText();
                        Log.Msg("HotHighlightNavigator", $"Space pressed - clicking primary button: {buttonText}");
                        UIActivator.SimulatePointerClick(primaryButton);
                        _announcer.Announce(buttonText, AnnouncementPriority.Normal);
                        // Invalidate snapshot so next selection phase gets a full rebuild
                        // (e.g. discard → untap lands are completely different contexts)
                        _snapshotValid = false;
                        return true;
                    }
                }
            }

            // Backspace - undo/cancel during mana payment or auto-tap mode
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                // 1. Try UndoButton (undo a specific mana tap)
                var undoButton = FindUndoButton();
                if (undoButton != null)
                {
                    Log.Msg("HotHighlightNavigator", "Backspace - clicking UndoButton");
                    UIActivator.SimulatePointerClick(undoButton);
                    _announcer.Announce(Strings.SpellCancelled, AnnouncementPriority.Normal);
                    return true;
                }

                // 2. Try secondary button (Cancel in dual-button layout)
                var secondaryButton = FindSecondaryButton();
                if (secondaryButton != null && IsButtonVisible(secondaryButton))
                {
                    string text = GetButtonTextWithMana(secondaryButton);
                    Log.Msg("HotHighlightNavigator", $"Backspace - clicking secondary button: {text}");
                    UIActivator.SimulatePointerClick(secondaryButton);
                    _announcer.Announce(text ?? Strings.SpellCancelled, AnnouncementPriority.Normal);
                    return true;
                }

                // 3. No highlights and only primary button = Cancel (e.g. 0 lands to pay)
                if (_items.Count == 0)
                {
                    var primaryButton = FindPrimaryButton();
                    if (primaryButton != null && IsButtonVisible(primaryButton))
                    {
                        string text = GetButtonTextWithMana(primaryButton);
                        Log.Msg("HotHighlightNavigator", $"Backspace - clicking sole primary button: {text}");
                        UIActivator.SimulatePointerClick(primaryButton);
                        _announcer.Announce(text ?? Strings.SpellCancelled, AnnouncementPriority.Normal);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Decides whether to do a full rebuild or stable refresh of highlights.
        /// Full rebuild on first discovery, selection mode change, or after invalidation.
        /// Stable refresh preserves Tab order when cards move due to selection animations.
        /// </summary>
        private void RefreshOrRebuildHighlights()
        {
            bool selectionMode = IsSelectionModeActive();
            bool modeChanged = (selectionMode != _wasInSelectionMode);

            if (_items.Count == 0 || !_snapshotValid || modeChanged)
            {
                DiscoverAllHighlights();
            }
            else
            {
                RefreshHighlightsStable();
            }
        }

        /// <summary>
        /// Scans the scene for all currently highlighted objects (cards, avatars, selected).
        /// Returns a dictionary keyed by instance ID. Does not modify _items.
        /// Shared scanning logic used by both full discovery and stable refresh.
        /// </summary>
        private Dictionary<int, HighlightedItem> ScanCurrentHighlights(bool selectionMode)
        {
            var result = new Dictionary<int, HighlightedItem>();

            // Cache avatar views once, then reuse (only 2 per duel)
            if (_cachedAvatarViews.Count == 0 || _cachedAvatarViews.Any(v => v == null))
            {
                FindAndCacheAvatarViews();
                if (!_avatarReflectionInitialized && _avatarViewType != null)
                    InitializeAvatarReflection(_avatarViewType);
            }

            // Single scene scan: find HotHighlight objects by name, then walk up to the card
            foreach (var t in GameObject.FindObjectsOfType<Transform>())
            {
                if (t == null) continue;
                if (!t.gameObject.name.Contains("HotHighlight")) continue;

                string highlightName = t.gameObject.name;

                Transform ancestor = t.parent;
                GameObject cardGo = null;
                while (ancestor != null)
                {
                    if (CardDetector.IsCard(ancestor.gameObject))
                    {
                        cardGo = ancestor.gameObject;
                        break;
                    }
                    ancestor = ancestor.parent;
                }
                if (cardGo == null) continue;

                int id = cardGo.GetInstanceID();
                if (result.ContainsKey(id)) continue;

                var item = CreateHighlightedItem(cardGo, highlightName);
                if (item != null)
                    result[id] = item;
            }

            // Check cached player avatars for highlight state (2 objects, no scene scan needed)
            if (_avatarReflectionInitialized)
            {
                foreach (var avatar in _cachedAvatarViews)
                {
                    if (avatar == null || !avatar.gameObject.activeInHierarchy) continue;
                    var item = CreateAvatarTargetItem(avatar);
                    if (item != null)
                    {
                        int id = item.GameObject.GetInstanceID();
                        if (!result.ContainsKey(id))
                            result[id] = item;
                    }
                }
            }

            // Selection mode fallback: find cards that lost HotHighlight after being selected
            if (selectionMode)
            {
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    if (!CardDetector.IsCard(go)) continue;

                    int id = go.GetInstanceID();
                    if (result.ContainsKey(id)) continue;
                    if (!IsCardSelected(go)) continue;

                    var item = CreateHighlightedItem(go, "Selected");
                    if (item != null)
                        result[id] = item;
                }
            }

            // Hand-castable supplement: MTGA's visual HotHighlight isn't reliably refreshed on
            // hand cards in transitional states (notably immediately after a land play, where the
            // activated ability of a battlefield creature stays highlighted but the player's
            // still-castable hand cards lose their visual cue). We look up MTGA's own answer
            // instead — the active ActionsAvailableWorkflow tracks GreInteractions per card with
            // CanAffordToCast pre-computed for each — and add any hand card the workflow says is
            // currently castable. Selection mode (discard pick, etc.) keeps the existing
            // visual-scan-only behavior since arbitrary castable cards would just be noise there.
            if (!selectionMode)
            {
                var handHolder = DuelHolderCache.GetHolder("LocalHand");
                if (handHolder != null)
                {
                    foreach (Transform t in handHolder.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == null || !t.gameObject.activeInHierarchy) continue;
                        var go = t.gameObject;
                        if (!CardDetector.IsCard(go)) continue;

                        int id = go.GetInstanceID();
                        if (result.ContainsKey(id)) continue;

                        if (!IsCardCastableByGameState(go)) continue;

                        var item = CreateHighlightedItem(go, "CastableNow");
                        if (item != null)
                            result[id] = item;
                    }
                }

                // Battlefield supplement: the same staleness pattern affects the player's own
                // permanents with activated abilities (e.g., a creature with a `{T}: do X` ability
                // whose HotHighlight didn't refresh after an opposing spell resolved into the
                // graveyard). Opponent permanents are skipped — the workflow's
                // GetInteractionsForId never returns actions for them, so the per-card reflection
                // would just be wasted work. Battlefield-stacking dedup runs on the merged result
                // below, so we don't need to filter stack children here.
                var battlefieldHolder = DuelHolderCache.GetHolder("BattlefieldCardHolder");
                if (battlefieldHolder != null)
                {
                    foreach (Transform t in battlefieldHolder.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == null || !t.gameObject.activeInHierarchy) continue;
                        var go = t.gameObject;
                        if (!CardDetector.IsCard(go)) continue;

                        int id = go.GetInstanceID();
                        if (result.ContainsKey(id)) continue;

                        var (_, _, isOpponent) = CardDetector.GetCardCategory(go);
                        if (isOpponent) continue;

                        if (!IsCardCastableByGameState(go)) continue;

                        var item = CreateHighlightedItem(go, "ActivatableNow");
                        if (item != null)
                            result[id] = item;
                    }
                }
            }

            // Battlefield stacking: drop highlighted children of multi-card stacks so
            // Tab cycles through stack heads only (mirrors B/A/R row navigation).
            // The head card stays — its highlight represents the whole stack.
            if (AccessibleArenaMod.Instance?.Settings?.BattlefieldStacking == true)
            {
                BattlefieldStackProvider.BuildStackIndex();
                var childIds = BattlefieldStackProvider.StackChildIds;
                if (childIds.Count > 0)
                {
                    var toRemove = new List<int>();
                    foreach (var kvp in result)
                    {
                        var item = kvp.Value;
                        if (item?.Zone != "Battlefield" || item.GameObject == null) continue;
                        uint cardId = CardStateProvider.GetCardInstanceId(item.GameObject);
                        if (cardId != 0 && childIds.Contains(cardId))
                            toRemove.Add(kvp.Key);
                    }
                    foreach (var key in toRemove)
                        result.Remove(key);
                }
            }

            return result;
        }

        /// <summary>
        /// Full discovery: clears items, scans scene, sorts by zone/position.
        /// Used on first Tab press or when snapshot is invalidated.
        /// </summary>
        private void DiscoverAllHighlights()
        {
            _items.Clear();

            // Only log discovery if the previous pass found something, or this one will —
            // avoids a pair of "Discovering... / Found 0 items" every phase change with nothing to highlight.
            bool logThisPass = _lastDiscoveryCount != 0;
            if (logThisPass)
                Log.Nav("HotHighlightNavigator", "Discovering highlights (full rebuild)...");

            bool selectionMode = IsSelectionModeActive();
            CheckSelectionModeTransition(selectionMode);

            var scanned = ScanCurrentHighlights(selectionMode);
            _items.AddRange(scanned.Values);

            // Diagnostic counters
            int handHighlights = _items.Count(i => i.Zone == "Hand");
            int battlefieldHighlights = _items.Count(i => i.Zone == "Battlefield");
            if (handHighlights != _lastDiagHandHighlighted ||
                battlefieldHighlights != _lastDiagBattlefieldHighlighted)
            {
                Log.Nav("HotHighlightNavigator", $"Highlight state: hand={handHighlights}, battlefield={battlefieldHighlights}");
                _lastDiagHandHighlighted = handHighlights;
                _lastDiagBattlefieldHighlighted = battlefieldHighlights;
            }

            // When no card/player highlights, check for prompt button choices
            if (_items.Count == 0)
                DiscoverPromptButtons();

            // Sort: Hand cards first, then your permanents, then opponent's, then players
            _items = _items
                .OrderBy(i => i.Zone == "Hand" ? 0 : 1)
                .ThenBy(i => i.IsPlayer ? 1 : 0)
                .ThenBy(i => i.IsOpponent ? 1 : 0)
                .ThenBy(i => i.GameObject?.transform.position.x ?? 0)
                .ToList();

            if (logThisPass || _items.Count > 0)
                Log.Nav("HotHighlightNavigator", $"Found {_items.Count} highlighted items");
            _lastDiscoveryCount = _items.Count;

            // Reset indices if out of range
            if (_currentIndex >= _items.Count)
                _currentIndex = _items.Count > 0 ? 0 : -1;

            _snapshotValid = true;
        }

        /// <summary>
        /// Stable refresh: validates existing items and inserts new ones at the
        /// correct sorted position.  Preserves the Tab order established by
        /// DiscoverAllHighlights — existing items never move, so selection
        /// animations cannot cause cards to be skipped or revisited.
        /// New items are inserted into the correct ownership group so that
        /// own cards always come before opponent cards.
        /// </summary>
        private void RefreshHighlightsStable()
        {
            Log.Nav("HotHighlightNavigator", "Refreshing highlights (stable snapshot)...");

            bool selectionMode = IsSelectionModeActive();
            CheckSelectionModeTransition(selectionMode);

            var scanned = ScanCurrentHighlights(selectionMode);

            // Remove items that are no longer valid (destroyed or lost highlight)
            var survivingIds = new HashSet<int>();
            _items.RemoveAll(item =>
            {
                if (item.GameObject == null) return true;
                int id = item.GameObject.GetInstanceID();
                if (!scanned.ContainsKey(id)) return true;
                survivingIds.Add(id);
                return false;
            });

            // Insert new items at the correct sorted position (preserves grouping)
            int newCount = 0;
            foreach (var kvp in scanned)
            {
                if (survivingIds.Contains(kvp.Key)) continue;
                int insertAt = FindSortedInsertionIndex(kvp.Value);
                _items.Insert(insertAt, kvp.Value);
                // Adjust _currentIndex if insertion was before or at current position
                if (_currentIndex >= 0 && insertAt <= _currentIndex)
                    _currentIndex++;
                newCount++;
            }

            // Prompt buttons if empty
            if (_items.Count == 0)
                DiscoverPromptButtons();

            Log.Nav("HotHighlightNavigator", $"Stable refresh: {_items.Count} items ({survivingIds.Count} kept, {newCount} new)");

            // Fix index bounds
            if (_currentIndex >= _items.Count)
                _currentIndex = _items.Count > 0 ? 0 : -1;
        }

        /// <summary>
        /// Returns a sort key tuple matching the order used in DiscoverAllHighlights.
        /// Lower values come first: Hand before Battlefield, own before opponent, etc.
        /// Position.x is NOT included — existing items keep their snapshot position,
        /// new items just need to land in the correct ownership group.
        /// </summary>
        private static (int zone, int player, int opponent) GetSortKey(HighlightedItem item)
        {
            return (
                item.Zone == "Hand" ? 0 : 1,
                item.IsPlayer ? 1 : 0,
                item.IsOpponent ? 1 : 0
            );
        }

        /// <summary>
        /// Finds the index at which a new item should be inserted to maintain
        /// the sort order (Hand → own cards → opponent cards → players).
        /// Scans existing items and returns the first index where the new item's
        /// sort key is less than or equal to the existing item's key.
        /// </summary>
        private int FindSortedInsertionIndex(HighlightedItem newItem)
        {
            var newKey = GetSortKey(newItem);
            for (int i = 0; i < _items.Count; i++)
            {
                var existingKey = GetSortKey(_items[i]);
                if (newKey.CompareTo(existingKey) < 0)
                    return i;
            }
            return _items.Count;
        }

        /// <summary>
        /// Finds and caches DuelScene_AvatarView instances from the scene.
        /// Only 2 exist per duel (local + opponent). Caches both the type and references.
        /// </summary>
        private void FindAndCacheAvatarViews()
        {
            _cachedAvatarViews.Clear();
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "DuelScene_AvatarView")
                {
                    if (_avatarViewType == null)
                        _avatarViewType = mb.GetType();
                    _cachedAvatarViews.Add(mb);
                }
            }
        }

        /// <summary>
        /// Creates a HighlightedItem for a player avatar if it has a valid highlight.
        /// Returns null if no valid highlight.
        /// </summary>
        private HighlightedItem CreateAvatarTargetItem(MonoBehaviour avatarView)
        {
            var ah = _avatarCache.Handles;
            var highlightSystem = ah.HighlightSystem.GetValue(avatarView);
            if (highlightSystem == null) return null;

            if (!_highlightSystemCache.EnsureInitialized(highlightSystem.GetType()))
                return null;

            int highlightValue = (int)_highlightSystemCache.Handles.CurrentHighlight.GetValue(highlightSystem);

            // Accept Hot(3), Tepid(2), Cold(1) — skip None(0), Selected(5), others
            if (highlightValue != 1 && highlightValue != 2 && highlightValue != 3)
                return null;

            bool isLocal = (bool)ah.IsLocalPlayer.GetValue(avatarView);

            var portraitButton = ah.PortraitButton.GetValue(avatarView) as MonoBehaviour;
            if (portraitButton == null)
            {
                Log.Nav("HotHighlightNavigator", $"AvatarView has highlight={highlightValue} but no PortraitButton");
                return null;
            }

            string name = isLocal ? Strings.You : Strings.Opponent;
            Log.Nav("HotHighlightNavigator", $"Added {(isLocal ? "local" : "opponent")} player as target (highlight={highlightValue})");
            return new HighlightedItem
            {
                GameObject = portraitButton.gameObject,
                Name = name,
                Zone = "Player",
                HighlightType = $"AvatarHighlight({highlightValue})",
                IsOpponent = !isLocal,
                IsPlayer = true,
                CardType = "Player"
            };
        }

        // GreInteraction lookup cache — used by the hand-castable supplement to read MTGA's own
        // pre-computed affordability flag rather than the visual HotHighlight prefab. Each
        // GreInteraction is constructed by ActionsAvailableWorkflow with CanAffordToCast already
        // captured from Action.CanAffordToCast(), so we get the game's exact answer per card.
        private static bool _greInteractionCacheSearched;
        private static MethodInfo _getInteractionsForIdMethod;
        private static PropertyInfo _gameManagerCurrentInteractionProp;
        private static FieldInfo _greInteractionTypeField;
        private static FieldInfo _greInteractionCanAffordField;
        private static MonoBehaviour _cachedGameManager;

        /// <summary>
        /// Scans loaded assemblies for the named types, tolerating ReflectionTypeLoadException
        /// (the mod's reduced dependency set causes some game-DLL types to fail to load, which
        /// makes Assembly.GetTypes() throw; ReflectionUtils.FindType's generic catch then swallows
        /// the whole assembly's type list). Returns the first match per requested name.
        /// </summary>
        private static (Type a, Type b) ResolveGlobalTypesByName(string nameA, string nameB)
        {
            Type a = null, b = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle)
                {
                    types = rtle.Types?.Where(x => x != null).ToArray() ?? Array.Empty<Type>();
                }
                catch { continue; }

                foreach (var ty in types)
                {
                    if (ty == null) continue;
                    if (a == null && ty.Name == nameA) a = ty;
                    else if (b == null && ty.Name == nameB) b = ty;
                    if (a != null && b != null) return (a, b);
                }
            }
            return (a, b);
        }

        private void InitInteractionLookup()
        {
            try
            {
                // ActionsAvailableWorkflow and GreInteraction are both global-namespace types,
                // and at least one of the assemblies they live in throws ReflectionTypeLoadException
                // on GetTypes() against the mod's reduced dependency set — so FindType (which only
                // catches the generic Exception) silently skips them. A single robust scan
                // tolerates the partial-load case and resolves both types in one pass.
                var (workflowType, greType) = ResolveGlobalTypesByName("ActionsAvailableWorkflow", "GreInteraction");
                if (workflowType != null)
                {
                    _getInteractionsForIdMethod = workflowType.GetMethod(
                        "GetInteractionsForId", BindingFlags.Public | BindingFlags.Static);
                }
                if (greType != null)
                {
                    _greInteractionTypeField = greType.GetField("Type", PublicInstance);
                    _greInteractionCanAffordField = greType.GetField("CanAffordToCast", PublicInstance);
                }

                // GameManager exposes WorkflowBase CurrentInteraction => WorkflowController.CurrentWorkflow.
                var gmType = FindType("GameManager");
                if (gmType != null)
                    _gameManagerCurrentInteractionProp = gmType.GetProperty("CurrentInteraction", PublicInstance);

                bool ok = _getInteractionsForIdMethod != null
                       && _gameManagerCurrentInteractionProp != null
                       && _greInteractionTypeField != null
                       && _greInteractionCanAffordField != null;
                Log.Nav("HotHighlightNavigator",
                    ok ? "Hand-castable supplement: GreInteraction pipeline ready"
                       : $"Hand-castable supplement: GreInteraction pipeline NOT fully resolvable " +
                         $"(GetInteractionsForId={_getInteractionsForIdMethod != null}, " +
                         $"GameManager.CurrentInteraction={_gameManagerCurrentInteractionProp != null}, " +
                         $"GreInteraction.Type={_greInteractionTypeField != null}, " +
                         $"GreInteraction.CanAffordToCast={_greInteractionCanAffordField != null}) — disabled");
            }
            catch (Exception ex)
            {
                Log.Msg("HotHighlightNavigator", $"InitInteractionLookup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns true when MTGA's <c>ActionsAvailableWorkflow</c> has a playable
        /// <see cref="GreInteraction"/> for this card with <c>CanAffordToCast == true</c> —
        /// "playable" being any Cast variant, plain <c>Activate</c> (cycling, channel,
        /// foretell-the-card, plot-the-card, ninjutsu, …), or non-payment <c>Special</c>
        /// actions (see <see cref="IsPlayableActionType"/>). That's the game's own answer
        /// to "is the user able to do something with this card right now?", computed when
        /// the workflow built each interaction (via <c>Action.CanAffordToCast()</c> against
        /// the current <c>AutoTapSolution</c>).
        /// </summary>
        private bool IsCardCastableByGameState(GameObject card)
        {
            if (!_greInteractionCacheSearched)
            {
                _greInteractionCacheSearched = true;
                InitInteractionLookup();
            }
            if (_getInteractionsForIdMethod == null
                || _gameManagerCurrentInteractionProp == null
                || _greInteractionTypeField == null
                || _greInteractionCanAffordField == null)
                return false;

            if (_cachedGameManager == null || !_cachedGameManager)
            {
                foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "GameManager")
                    {
                        _cachedGameManager = mb;
                        break;
                    }
                }
                if (_cachedGameManager == null) return false;
            }

            object currentInteraction = _gameManagerCurrentInteractionProp.GetValue(_cachedGameManager);
            if (currentInteraction == null) return false;

            uint instanceId = CardStateProvider.GetCardInstanceId(card);
            if (instanceId == 0) return false;

            object result;
            try { result = _getInteractionsForIdMethod.Invoke(null, new object[] { instanceId, currentInteraction }); }
            catch (Exception ex)
            {
                Log.Msg("HotHighlightNavigator", $"GetInteractionsForId invoke failed: {ex.Message}");
                return false;
            }
            if (!(result is IEnumerable interactions)) return false;

            foreach (var interaction in interactions)
            {
                if (interaction == null) continue;
                var typeVal = _greInteractionTypeField.GetValue(interaction);
                if (typeVal == null) continue;
                string typeName = typeVal.ToString();
                if (!IsPlayableActionType(typeName)) continue;

                var afford = _greInteractionCanAffordField.GetValue(interaction);
                if (afford is bool b && b) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true for <c>ActionType</c> values that represent a player-initiated action
        /// the user would want surfaced in Tab. The enum lives in
        /// <c>Wotc.Mtgo.Gre.External.Messaging.ActionType</c> (24 values total) and covers a
        /// lot more than just casting:
        /// <list type="bullet">
        /// <item>All <c>Cast*</c> variants — normal cast, split halves (<c>CastLeft/Right</c>),
        /// adventure, MDFC back-face, prototype, Rooms (DSK), Omens (BLB). All put a card on
        /// the stack from hand / graveyard / exile / command zone.</item>
        /// <item><c>Activate</c> — hand-side and battlefield-side activated abilities. This is
        /// the big one beyond Cast: cycling, channel, ninjutsu, foretell-the-card,
        /// plot-the-card, transmute, etc. Excluded: <c>ActivateMana</c> (mana abilities; tabbing
        /// to one is noise — the auto-tap solution handles them) and <c>ActivateTest</c>
        /// (internal/debug).</item>
        /// <item><c>Special</c> + <c>SpecialTurnFaceUp</c> — generic special actions (conspire,
        /// monarch claim, dungeon advance) and morph/manifest face-up. Excluded:
        /// <c>SpecialPayment</c> (payment subaction, never standalone).</item>
        /// </list>
        /// Everything else (<c>Play*</c> for lands — Tab is for actions, not land drops;
        /// <c>Pass</c>, <c>None</c>; payment subactions <c>MakePayment</c> / <c>CombatCost</c> /
        /// <c>ResolutionCost</c> / <c>FloatMana</c> / <c>OpeningHandAction</c>) is filtered out.
        /// </summary>
        private static bool IsPlayableActionType(string typeName)
        {
            if (typeName.StartsWith("Cast")) return true;
            return typeName == "Activate"
                || typeName == "Special"
                || typeName == "SpecialTurnFaceUp";
        }

        /// <summary>
        /// Creates a HighlightedItem from a card GameObject.
        /// </summary>
        private HighlightedItem CreateHighlightedItem(GameObject go, string highlightType)
        {
            string zone = DetectZone(go);
            string cardName = CardDetector.GetCardName(go);

            if (cardName == "Unknown card") return null;

            var item = new HighlightedItem
            {
                GameObject = go,
                Name = cardName,
                Zone = zone,
                HighlightType = highlightType,
                IsOpponent = CardDetector.IsOpponentCard(go),
                IsPlayer = false
            };

            // Get additional info for battlefield cards
            if (zone == "Battlefield" || zone == "Stack")
            {
                var cardInfo = CardDetector.ExtractCardInfo(go);
                item.PowerToughness = cardInfo.PowerToughness;
                item.CardType = DetermineCardType(go);
            }

            return item;
        }

        /// <summary>
        /// <summary>
        /// Initializes reflection cache for DuelScene_AvatarView fields.
        /// </summary>
        private static void InitializeAvatarReflection(Type avatarType)
        {
            _avatarViewType = avatarType;
            if (_avatarCache.EnsureInitialized(avatarType))
                _avatarReflectionInitialized = true;
        }

        /// <summary>
        /// Announces the current highlighted item by syncing with zone/battlefield navigators.
        /// For card items, delegates to the appropriate navigator so Left/Right works afterwards.
        /// For player targets and prompt buttons, announces directly.
        /// </summary>
        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            // Prompt buttons and player targets - announce directly (not in any zone)
            // Must claim zone ownership so Enter is handled by HotHighlightNavigator
            if (item.IsPromptButton)
            {
                int position = _currentIndex + 1;
                int total = _items.Count;
                string announcement = Strings.ItemPositionOf(position, total, item.Name, force: true);
                _announcer.Announce(announcement, AnnouncementPriority.High);
                _zoneNavigator.SetCurrentZone(ZoneType.Hand, "HotHighlightNavigator");
                _lastItemZone = "Button";
                return;
            }

            if (item.IsPlayer)
            {
                int position = _currentIndex + 1;
                int total = _items.Count;
                string name = item.IsOpponent ? Strings.Opponent : Strings.You;
                string pos = Strings.PositionOf(position, total, force: true);
                _announcer.Announce($"{name}, player" + (pos != "" ? $", {pos}" : ""), AnnouncementPriority.High);
                _zoneNavigator.SetCurrentZone(ZoneType.Hand, "HotHighlightNavigator");
                _lastItemZone = "Player";
                return;
            }

            // Card items - delegate to zone/battlefield navigators for proper sync
            bool zoneChanged = _lastItemZone != item.Zone;

            if (item.Zone == "Battlefield" && _battlefieldNavigator != null)
            {
                // Delegate to BattlefieldNavigator - it finds the row, syncs index, announces
                if (_battlefieldNavigator.NavigateToSpecificCard(item.GameObject, zoneChanged))
                {
                    _lastItemZone = item.Zone;
                    return;
                }
            }

            // Non-battlefield zones (Hand, Stack, Graveyard, Exile) or battlefield fallback
            var zoneType = StringToZoneType(item.Zone);
            if (_zoneNavigator.NavigateToSpecificCard(zoneType, item.GameObject, zoneChanged))
            {
                _lastItemZone = item.Zone;
                return;
            }

            // Fallback: card not found in navigator lists (shouldn't happen normally)
            Log.Warn("HotHighlightNavigator", $"Card {item.Name} not found in zone navigators, using direct announcement");
            _announcer.Announce($"{item.Name}", AnnouncementPriority.High);

            if (item.GameObject != null)
                ZoneNavigator.SetFocusedGameObject(item.GameObject, "HotHighlightNavigator");

            _zoneNavigator.SetCurrentZone(zoneType, "HotHighlightNavigator");
            _lastItemZone = item.Zone;
        }

        /// <summary>
        /// Activates the current item based on its zone and current game mode.
        /// In selection mode (discard, etc.), hand cards use single-click to toggle.
        /// Otherwise, hand cards use two-click to play.
        /// </summary>
        private void ActivateCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            // Prompt button - click and clear
            if (item.IsPromptButton)
            {
                var result = UIActivator.SimulatePointerClick(item.GameObject);
                if (result.Success)
                {
                    _announcer.Announce(item.Name, AnnouncementPriority.Normal);
                    Log.Msg("HotHighlightNavigator", $"Clicked prompt button: {item.Name}");
                }
                _items.Clear();
                _currentIndex = -1;
                _snapshotValid = false;
                return;
            }

            bool selectionMode = IsSelectionModeActive();
            int preClickCount = selectionMode ? (GetSubmitButtonInfo()?.count ?? -1) : -1;
            Log.Msg("HotHighlightNavigator", $"Activating: {item.Name} in {item.Zone} (selection mode: {selectionMode})");

            if (item.Zone == "Hand")
            {
                if (selectionMode)
                {
                    // Selection mode (discard, etc.) - single click to toggle selection
                    // Check current state before clicking
                    bool wasSelected = IsCardSelected(item.GameObject);
                    Log.Msg("HotHighlightNavigator", $"Toggling selection on: {item.Name} (was selected: {wasSelected})");

                    var result = UIActivator.SimulatePointerClick(item.GameObject);
                    if (result.Success)
                    {
                        // Announce toggle result after game updates
                        MelonCoroutines.Start(AnnounceSelectionToggleDelayed(item.Name, wasSelected, preClickCount));
                    }
                    else
                    {
                        _announcer.Announce(Strings.CouldNotSelect(item.Name), AnnouncementPriority.High);
                    }
                }
                else
                {
                    // Normal mode - use two-click to play
                    UIActivator.PlayCardViaTwoClick(item.GameObject, (success, message) =>
                    {
                        if (success)
                        {
                            Log.Msg("HotHighlightNavigator", $"Card play initiated");
                        }
                        else
                        {
                            _announcer.Announce(Strings.CouldNotPlay(item.Name), AnnouncementPriority.High);
                            Log.Msg("HotHighlightNavigator", $"Card play failed: {message}");
                        }
                    });
                }
            }
            else
            {
                // Battlefield/Stack/Player target - single click to select/toggle
                if (selectionMode && !item.IsPlayer)
                {
                    // Selection mode on battlefield - toggle selection with count announcement
                    // Use position-aware click to avoid hitting wrong card in stacks
                    bool wasSelected = IsCardSelected(item.GameObject);
                    Log.Msg("HotHighlightNavigator", $"Toggling selection on: {item.Name} on {item.GameObject.name} (was selected: {wasSelected})");

                    var result = (item.Zone == "Battlefield")
                        ? ClickBattlefieldCard(item.GameObject)
                        : UIActivator.SimulatePointerClick(item.GameObject);
                    if (item.Zone == "Battlefield")
                        _battlefieldNavigator?.MarkDirty();
                    if (result.Success)
                        MelonCoroutines.Start(AnnounceSelectionToggleDelayed(item.Name, wasSelected, preClickCount, item.Zone == "Battlefield" ? item.GameObject.name : null));
                    else
                        _announcer.Announce(Strings.CouldNotSelect(item.Name), AnnouncementPriority.High);
                }
                else
                {
                    var result = UIActivator.SimulatePointerClick(item.GameObject);

                    if (result.Success)
                    {
                        string announcement = item.IsPlayer ? Strings.Target_Targeted(item.Name) : Strings.Target_Selected(item.Name);
                        _announcer.Announce(announcement, AnnouncementPriority.Normal);
                        Log.Msg("HotHighlightNavigator", $"{announcement}");
                    }
                    else
                    {
                        _announcer.Announce(Strings.CouldNotTarget(item.Name), AnnouncementPriority.High);
                        Log.Warn("HotHighlightNavigator", $"Click failed: {result.Message}");
                    }
                }
            }

            // In selection mode, preserve position so next Tab advances to the next card
            // Items will be refreshed via DiscoverAllHighlights() on next Tab press
            if (selectionMode)
                return;

            // Clear state after activation - highlights will update
            _items.Clear();
            _currentIndex = -1;
            _opponentIndex = -1;
            _snapshotValid = false;
        }

        /// <summary>
        /// Detects zone from parent hierarchy.
        /// </summary>
        private string DetectZone(GameObject obj)
        {
            Transform current = obj.transform;
            while (current != null)
            {
                string name = current.name;

                if (name.Contains("LocalHand") || name.Contains("Hand"))
                    return "Hand";
                if (name.Contains("StackCardHolder") || name.Contains("Stack"))
                    return "Stack";
                if (name.Contains("BattlefieldCardHolder") || name.Contains("Battlefield"))
                    return "Battlefield";
                if (name.Contains("Graveyard"))
                    return "Graveyard";
                if (name.Contains("Exile"))
                    return "Exile";

                current = current.parent;
            }
            return "Unknown";
        }

        /// <summary>
        /// Determines card type from model enum values (language-agnostic).
        /// </summary>
        private string DetermineCardType(GameObject go)
        {
            var (isCreature, isLand, _) = CardDetector.GetCardCategory(go);
            if (isCreature) return "Creature";
            if (isLand) return "Land";
            return "Permanent";
        }

        /// <summary>
        /// Clicks a battlefield card by finding and invoking CardInput directly.
        /// CardInput (the game's click handler) may be on a child object (e.g. collider child),
        /// not the CDC root. ExecuteEvents.Execute only searches the target object, not children,
        /// so sending events to the CDC root misses CardInput entirely. Instead, we find the
        /// actual IPointerClickHandler in the hierarchy and send events directly to its GameObject.
        /// CardInput uses its cached _cardViewCache (the DuelScene_CDC) to identify the card,
        /// NOT the pointer position, so this correctly selects the intended card even in stacks.
        /// </summary>
        private static ActivationResult ClickBattlefieldCard(GameObject card)
        {
            // Find the actual click handler (CardInput) which may be on a child object
            var clickHandler = card.GetComponentInChildren<IPointerClickHandler>();
            if (clickHandler != null)
            {
                var handlerGo = (clickHandler as MonoBehaviour)?.gameObject ?? card;

                // Use a valid screen position (only needed for CardInput's ScreenRect bounds check)
                Vector2 screenPos = Camera.main != null
                    ? (Vector2)Camera.main.WorldToScreenPoint(card.transform.position)
                    : new Vector2(Screen.width / 2f, Screen.height / 2f);

                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    clickCount = 1,
                    pointerPress = handlerGo,
                    pointerEnter = handlerGo,
                    position = screenPos,
                    pressPosition = screenPos
                };

                Log.Msg("HotHighlightNavigator", $"Direct CardInput click: handler on {handlerGo.name} for card {card.name}");

                // Set EventSystem selection to CDC root for mod's focus tracking
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(card);

                // Send full click sequence to the handler's GameObject
                ExecuteEvents.Execute(handlerGo, pointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(handlerGo, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(handlerGo, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(handlerGo, pointer, ExecuteEvents.pointerClickHandler);

                return new ActivationResult(true, Strings.ActivatedBare, ActivationType.PointerClick);
            }

            // Fallback: standard click if no CardInput found
            Log.Warn("HotHighlightNavigator", $"No IPointerClickHandler found on {card.name}, using fallback click");
            if (Camera.main != null)
            {
                Vector2 screenPos = Camera.main.WorldToScreenPoint(card.transform.position);
                return UIActivator.SimulatePointerClick(card, screenPos);
            }
            return UIActivator.SimulatePointerClick(card);
        }

        /// <summary>
        /// Converts zone string to ZoneType enum.
        /// </summary>
        private ZoneType StringToZoneType(string zone)
        {
            return zone switch
            {
                "Hand" => ZoneType.Hand,
                "Battlefield" => ZoneType.Battlefield,
                "Stack" => ZoneType.Stack,
                "Graveyard" => ZoneType.Graveyard,
                "Exile" => ZoneType.Exile,
                _ => ZoneType.Battlefield
            };
        }

        /// <summary>
        /// Gets the text of the primary prompt button if one exists.
        /// This indicates game state like "Pass", "Resolve", "Next", "End Turn", etc.
        /// Provides useful context when there are no playable cards.
        /// </summary>
        private string GetPrimaryButtonText()
        {
            return GetButtonTextWithMana(FindPrimaryButton());
        }

        /// <summary>
        /// Gets button text with mana sprite tags converted to readable names.
        /// Unlike UITextExtractor.GetButtonText which strips all tags (losing mana info),
        /// this method parses sprite tags into readable mana symbol names first.
        /// </summary>
        private string GetButtonTextWithMana(GameObject button)
        {
            if (button == null) return null;

            var tmpText = button.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string text = tmpText.text?.Trim();
                if (!string.IsNullOrEmpty(text) && text != "Ctrl")
                    return CardDetector.ReplaceSpriteTagsWithText(text);
            }

            var uiText = button.GetComponentInChildren<Text>();
            if (uiText != null)
            {
                string text = uiText.text?.Trim();
                if (!string.IsNullOrEmpty(text) && text != "Ctrl")
                    return CardDetector.ReplaceSpriteTagsWithText(text);
            }

            return null;
        }

        /// <summary>
        /// Finds the primary prompt button GameObject if one exists.
        /// </summary>
        private GameObject FindPrimaryButton()
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.name.Contains("PromptButton_Primary"))
                    return go;
            }
            return null;
        }

        /// <summary>
        /// Finds the secondary prompt button GameObject if one exists.
        /// </summary>
        private GameObject FindSecondaryButton()
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (go.name.Contains("PromptButton_Secondary"))
                    return go;
            }
            return null;
        }

        /// <summary>
        /// Finds the game's UndoButton if it exists and is visible.
        /// Present during mana payment / auto-tap when the player can undo the cast.
        /// </summary>
        private GameObject FindUndoButton()
        {
            var go = GameObject.Find("UndoButton");
            if (go != null && go.activeInHierarchy && IsButtonVisible(go))
                return go;
            return null;
        }

        /// <summary>
        /// Language-agnostic heuristic: short text without spaces = keyboard hints (Strg, Ctrl, Z, etc.)
        /// </summary>
        private bool IsMeaningfulButtonText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.Length <= 4 && !text.Contains(" ")) return false;
            return true;
        }

        /// <summary>
        /// Checks if a button is visible and interactable via its CanvasGroup.
        /// The game hides inactive buttons by setting CanvasGroup alpha=0 and
        /// interactable=false while keeping Selectable.interactable true.
        /// </summary>
        private bool IsButtonVisible(GameObject button)
        {
            return UIElementClassifier.IsVisibleViaCanvasGroup(button);
        }

        /// <summary>
        /// Discovers prompt buttons as navigable items when no card/player highlights exist.
        /// Only adds buttons when BOTH primary and secondary have meaningful text AND
        /// neither has a native keyboard hint (which indicates standard duel buttons
        /// already accessible via mod keybindings).
        /// </summary>
        private void DiscoverPromptButtons()
        {
            var primaryButton = FindPrimaryButton();
            string primaryText = GetButtonTextWithMana(primaryButton);

            var secondaryButton = FindSecondaryButton();
            string secondaryText = GetButtonTextWithMana(secondaryButton);

            // Only add when BOTH have meaningful text (sacrifice vs pay mana, etc.)
            if (!IsMeaningfulButtonText(primaryText) || !IsMeaningfulButtonText(secondaryText))
                return;

            // Check CanvasGroup visibility - the game hides inactive/status buttons by setting
            // CanvasGroup alpha=0 and interactable=false. Without this check, phase status
            // buttons like "Opponent's Turn" + "Cancel Attacks" appear as tappable choices.
            // Note: YesNo browser buttons are handled by BrowserNavigator, not here.
            if (!IsButtonVisible(primaryButton) || !IsButtonVisible(secondaryButton))
                return;

            _items.Add(new HighlightedItem
            {
                GameObject = primaryButton,
                Name = primaryText,
                Zone = "Button",
                IsPromptButton = true
            });

            _items.Add(new HighlightedItem
            {
                GameObject = secondaryButton,
                Name = secondaryText,
                Zone = "Button",
                IsPromptButton = true
            });

            Log.Msg("HotHighlightNavigator", $"Added prompt buttons: '{primaryText}' and '{secondaryText}'");
        }

        /// <summary>
        /// Polls prompt button state each frame. Announces the primary button text
        /// when meaningful choices first appear (both buttons visible with real text).
        /// During combat phases, suppresses the initial button appearance (null → text)
        /// since the phase announcement already informed the user. Subsequent text changes
        /// (e.g. "1 Angreifer" after clicking, or triggered ability prompts) are announced.
        /// Called from DuelNavigator's HandleCustomInput.
        /// </summary>
        public void MonitorPromptButtons(float timeSincePhaseChange, bool isInCombatPhase)
        {
            if (!_isActive) return;

            string currentText = null;

            var primaryButton = FindPrimaryButton();
            var secondaryButton = FindSecondaryButton();
            string primaryText = GetButtonTextWithMana(primaryButton);
            string secondaryText = GetButtonTextWithMana(secondaryButton);

            if (IsMeaningfulButtonText(primaryText) && IsMeaningfulButtonText(secondaryText)
                && IsButtonVisible(primaryButton) && IsButtonVisible(secondaryButton))
            {
                currentText = primaryText;
            }

            // Reset combat-phase default once we leave combat so a future combat phase
            // can capture its own default text (it may differ between attackers/blockers).
            if (!isInCombatPhase)
                _combatPhaseDefaultButtonText = null;

            if (currentText != _lastPromptButtonText)
            {
                bool shouldAnnounce = currentText != null;

                // During combat phases, suppress the initial button appearance (null → text).
                // The phase change already announced "Declare Attackers/Blockers".
                // Real changes (text → different text) like triggered ability prompts still announce.
                if (shouldAnnounce && isInCombatPhase && _lastPromptButtonText == null)
                {
                    // Capture this default text so we can suppress later transitions back to it
                    // (e.g. user deselects the last attacker → button reverts to "All Attack").
                    _combatPhaseDefaultButtonText = currentText;
                    shouldAnnounce = false;
                }

                // Suppress transitions back to the captured combat-phase default. The user
                // already gets per-card feedback ("can attack") from BattlefieldNavigator;
                // re-announcing "All Attack" sounds like an action they took, not a deselect.
                if (shouldAnnounce && isInCombatPhase
                    && _combatPhaseDefaultButtonText != null
                    && currentText == _combatPhaseDefaultButtonText)
                {
                    shouldAnnounce = false;
                }

                if (shouldAnnounce)
                    _announcer.Announce(currentText, AnnouncementPriority.High);
                _lastPromptButtonText = currentText;
            }
        }

        #region Selection Mode (Discard, etc.)

        /// <summary>
        /// Checks if we're in selection mode (discard, choose cards to exile, etc.).
        /// Selection mode is detected by a Submit button showing a count AND
        /// no valid targets on battlefield/stack (to distinguish from targeting mode).
        /// </summary>
        private bool IsSelectionModeActive()
        {
            var buttonInfo = GetSubmitButtonInfo();
            if (buttonInfo == null)
                return false;

            return true;
        }

        /// <summary>
        /// Gets the Submit button info: selected count and button GameObject.
        /// Returns null if no Submit button with a number found.
        /// </summary>
        private (int count, GameObject button)? GetSubmitButtonInfo()
        {
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                if (!selectable.gameObject.name.Contains("PromptButton_Primary"))
                    continue;

                string buttonText = UITextExtractor.GetButtonText(selectable.gameObject);
                if (string.IsNullOrEmpty(buttonText))
                    continue;

                // Match any number in the button text
                var match = ButtonNumberPattern.Match(buttonText);
                if (match.Success)
                {
                    int count = int.Parse(match.Groups[1].Value);
                    return (count, selectable.gameObject);
                }
            }

            return null;
        }

        /// <summary>
        /// After toggling a card selection, announces the toggle result and current count.
        /// Compares post-click count with pre-click count to detect single-target targeting
        /// (where the game resolves immediately and the count doesn't change).
        /// </summary>
        /// <param name="cardName">Name of the card that was toggled</param>
        /// <param name="wasSelected">Whether the card was selected before the click</param>
        /// <param name="preClickCount">The submit button count before the click (-1 if unknown)</param>
        private IEnumerator AnnounceSelectionToggleDelayed(string cardName, bool wasSelected, int preClickCount = -1, string clickedCdcName = null)
        {
            yield return new WaitForSeconds(0.2f);

            // Diagnostic: log which CDCs have selection indicators after the click
            if (clickedCdcName != null)
                LogSelectionIndicatorScan(clickedCdcName);

            var info = GetSubmitButtonInfo();
            if (info != null)
            {
                // If the count didn't change from pre-click, the game processed immediately
                // (single-target like Eluge's flood counter). Just announce "selected".
                if (preClickCount >= 0 && info.Value.count == preClickCount)
                {
                    string action = wasSelected ? Strings.Deselected : Strings.Selected;
                    _announcer.Announce($"{cardName} {action}", AnnouncementPriority.Normal);
                }
                else
                {
                    // Count changed = real multi-select (discard, untap lands, etc.)
                    int required = GetRequiredCountFromPrompt();
                    string progress = Strings.SelectionProgress(info.Value.count, required);
                    if (wasSelected)
                        _announcer.Announce($"{cardName} {Strings.Deselected}, {progress}", AnnouncementPriority.Normal);
                    else
                        _announcer.Announce($"{cardName}, {progress}", AnnouncementPriority.Normal);
                }
            }
            else
            {
                // Button gone = game resolved and moved on. Just announce "selected".
                string action = wasSelected ? Strings.Deselected : Strings.Selected;
                _announcer.Announce($"{cardName} {action}", AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Checks if a card is currently selected (for discard, exile, etc.).
        /// The game adds visual indicator children to selected cards with names containing
        /// "select", "chosen", or "pick".
        /// </summary>
        private bool IsCardSelected(GameObject card)
        {
            if (card == null) return false;

            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;

                string childName = child.name.ToLower();
                if (childName.Contains("select") || childName.Contains("chosen") || childName.Contains("pick"))
                {
                    Log.Msg("HotHighlightNavigator", $"Found selection indicator: {child.name} on {card.name}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Diagnostic: after clicking a battlefield card for selection, scan all battlefield CDCs
        /// to log which ones have selection indicators. Helps trace click-targeting mismatches
        /// where the game selects a different card than what we clicked.
        /// </summary>
        private void LogSelectionIndicatorScan(string clickedCdcName)
        {
            var battlefieldHolder = DuelHolderCache.GetHolder("BattlefieldCardHolder");
            if (battlefieldHolder == null) return;

            var selected = new List<string>();
            foreach (Transform child in battlefieldHolder.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (!CardDetector.IsCard(child.gameObject)) continue;

                if (IsCardSelected(child.gameObject))
                    selected.Add(child.gameObject.name);
            }

            Log.Msg("HotHighlightNavigator", $"DIAG click-target: clicked={clickedCdcName}, cards with selection indicators: [{string.Join(", ", selected)}]");
        }

        /// <summary>
        /// Detects transition into/out of selection mode and announces on entry.
        /// Uses the submit button's full text (language-agnostic since it comes from the game).
        /// </summary>
        private void CheckSelectionModeTransition(bool isActive)
        {
            if (isActive && !_wasInSelectionMode)
            {
                _wasInSelectionMode = true;

                // Find the prompt instruction text (e.g. "Discard a card" / "Wirf eine Karte ab")
                // Lives in PromptText_Desktop_16x9(Clone) - the game's localized instruction for the player
                string promptText = GetPromptInstructionText();
                if (!string.IsNullOrEmpty(promptText))
                {
                    Log.Msg("HotHighlightNavigator", $"Selection mode entered, prompt: {promptText}");
                    _announcer.Announce(promptText, AnnouncementPriority.High);
                }
                else
                {
                    // Fallback to submit button text if no prompt found
                    var info = GetSubmitButtonInfo();
                    if (info != null)
                    {
                        string buttonText = UITextExtractor.GetButtonText(info.Value.button);
                        Log.Msg("HotHighlightNavigator", $"Selection mode entered, button fallback: {buttonText}");
                        _announcer.Announce(buttonText, AnnouncementPriority.High);
                    }
                }
            }
            else if (!isActive && _wasInSelectionMode)
            {
                _wasInSelectionMode = false;
                Log.Msg("HotHighlightNavigator", "Selection mode exited");
            }
        }

        /// <summary>
        /// Finds the game's prompt instruction text (e.g. "Discard a card" / "Wirf eine Karte ab").
        /// The game displays this in a PromptText element that is language-agnostic.
        /// </summary>
        private string GetPromptInstructionText()
        {
            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.Contains("PromptText_")) continue;

                var tmp = go.GetComponentInChildren<TMP_Text>();
                if (tmp != null)
                {
                    string text = tmp.text?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        return CardDetector.ReplaceSpriteTagsWithText(text);
                }
            }
            return null;
        }

        /// <summary>
        /// Extracts the required selection count from the game's prompt text.
        /// First tries digit match (\d+), then falls back to number words from
        /// the "NumberWords" key in the language file (e.g. "zwei"=2 in German).
        /// Returns 1 if nothing found (prompts like "Discard a card" mean 1).
        /// </summary>
        private int GetRequiredCountFromPrompt()
        {
            string promptText = GetPromptInstructionText();
            if (!string.IsNullOrEmpty(promptText))
            {
                // Try digits first
                var match = Regex.Match(promptText, @"\d+");
                if (match.Success)
                    return int.Parse(match.Value);

                // Fall back to number words from language file
                int wordNum = LocaleManager.Instance.TryParseNumberWord(promptText);
                if (wordNum > 0)
                    return wordNum;
            }
            return 1;
        }

        /// <summary>
        /// Returns the selection state suffix for a card (e.g. ", selected" or "").
        /// Used by ZoneNavigator to announce selection state during zone navigation.
        /// Also checks for selection mode transition to announce on first detection.
        /// </summary>
        public string GetSelectionStateText(GameObject card)
        {
            bool active = IsSelectionModeActive();
            CheckSelectionModeTransition(active);
            if (!active) return "";
            return IsCardSelected(card) ? $", {Strings.Selected}" : "";
        }

        /// <summary>
        /// Returns true if selection mode (discard, exile choice, etc.) is currently active.
        /// Used by ZoneNavigator to adjust card indexing.
        /// </summary>
        public bool IsInSelectionMode() => IsSelectionModeActive();

        /// <summary>
        /// Returns true if the given card is currently selected (has visual selection indicator).
        /// Used by ZoneNavigator to determine selectable card count during discard.
        /// </summary>
        public bool IsCardCurrentlySelected(GameObject card) => IsCardSelected(card);

        /// <summary>
        /// Attempts to toggle selection on a card if selection mode (discard, exile, etc.) is active.
        /// Called by ZoneNavigator when the user presses Enter on a hand card navigated via zone shortcuts.
        /// Returns true if selection was handled, false if not in selection mode.
        /// </summary>
        public bool TryToggleSelection(GameObject card)
        {
            if (!IsSelectionModeActive()) return false;

            int preClickCount = GetSubmitButtonInfo()?.count ?? -1;
            string cardName = CardDetector.GetCardName(card) ?? card.name;
            bool wasSelected = IsCardSelected(card);
            Log.Msg("HotHighlightNavigator", $"Zone nav toggling selection: {cardName} on {card.name} (was selected: {wasSelected})");

            // Use position-aware click for battlefield cards to avoid hitting
            // wrong card in visual stacks (e.g. multiple Islands)
            string zone = DetectZone(card);
            var result = (zone == "Battlefield")
                ? ClickBattlefieldCard(card)
                : UIActivator.SimulatePointerClick(card);
            if (zone == "Battlefield")
                _battlefieldNavigator?.MarkDirty();
            if (result.Success)
                MelonCoroutines.Start(AnnounceSelectionToggleDelayed(cardName, wasSelected, preClickCount, zone == "Battlefield" ? card.name : null));
            else
                _announcer.Announce(Strings.CouldNotSelect(cardName), AnnouncementPriority.High);

            return true;
        }

        #endregion
    }

    /// <summary>
    /// Represents a highlighted item (card or player).
    /// </summary>
    public class HighlightedItem
    {
        public GameObject GameObject { get; set; }
        public string Name { get; set; }
        public string Zone { get; set; }
        public string HighlightType { get; set; }
        public bool IsOpponent { get; set; }
        public bool IsPlayer { get; set; }
        public string CardType { get; set; }
        public string PowerToughness { get; set; }
        public bool IsPromptButton { get; set; }
    }
}
