using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

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

        // Track last zone/row to detect zone changes on Tab
        private string _lastItemZone;

        // Selection mode detection (discard, choose cards to exile, etc.)
        // Matches any number in button text: "Submit 2", "2 abwerfen", "0 bestätigen"
        private static readonly Regex ButtonNumberPattern = new Regex(@"(\d+)", RegexOptions.IgnoreCase);

        // Avatar targeting reflection cache
        private static readonly BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;
        private static Type _avatarViewType;
        private static FieldInfo _highlightSystemField;    // DuelScene_AvatarView._highlightSystem
        private static FieldInfo _currentHighlightField;   // HighlightSystem._currentHighlightType
        private static PropertyInfo _isLocalPlayerProp;    // DuelScene_AvatarView.IsLocalPlayer
        private static FieldInfo _portraitButtonField;     // DuelScene_AvatarView.PortraitButton
        private static bool _avatarReflectionInitialized;

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
            MelonLogger.Msg("[HotHighlightNavigator] Activated");
        }

        public void Deactivate()
        {
            _isActive = false;
            _wasInSelectionMode = false;
            _items.Clear();
            _currentIndex = -1;
            _opponentIndex = -1;
            _lastItemZone = null;
            MelonLogger.Msg("[HotHighlightNavigator] Deactivated");
        }

        /// <summary>
        /// Clears any stale highlight state without deactivating.
        /// Called when user navigates to a zone using shortcuts (C/G/X/S).
        /// </summary>
        public void ClearState()
        {
            if (_items.Count > 0)
            {
                MelonLogger.Msg("[HotHighlightNavigator] Clearing state due to zone navigation");
                _items.Clear();
                _currentIndex = -1;
                _opponentIndex = -1;
                _lastItemZone = null;
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
                DiscoverAllHighlights();

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

                // Refresh highlights on each Tab press
                DiscoverAllHighlights();

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
                // Check if we still have zone ownership - user may have navigated away
                // using zone shortcuts (C, G, X, S) or battlefield shortcuts (A, B, R)
                if (_zoneNavigator.CurrentZoneOwner != ZoneOwner.HighlightNavigator)
                {
                    // We lost ownership - clear stale state and let other handlers process Enter
                    if (_items.Count > 0)
                    {
                        MelonLogger.Msg($"[HotHighlightNavigator] Clearing stale state - zone owner is {_zoneNavigator.CurrentZoneOwner}");
                        _items.Clear();
                        _currentIndex = -1;
                        _opponentIndex = -1;
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
            // or when in selection mode (to submit the selection/discard)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_items.Count == 0 || IsSelectionModeActive())
                {
                    var primaryButton = FindPrimaryButton();
                    if (primaryButton != null)
                    {
                        string buttonText = GetPrimaryButtonText();
                        MelonLogger.Msg($"[HotHighlightNavigator] Space pressed - clicking primary button: {buttonText}");
                        UIActivator.SimulatePointerClick(primaryButton);
                        _announcer.Announce(buttonText, AnnouncementPriority.Normal);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Discovers ALL items with HotHighlight across all zones.
        /// No zone filtering - we trust the game to highlight only what's relevant.
        /// </summary>
        private void DiscoverAllHighlights()
        {
            _items.Clear();
            var addedIds = new HashSet<int>();

            MelonLogger.Msg("[HotHighlightNavigator] Discovering highlights...");

            // Pre-check selection mode once (expensive call, don't repeat per-object)
            bool selectionMode = IsSelectionModeActive();
            CheckSelectionModeTransition(selectionMode);

            // DIAGNOSTIC: Count hand cards and their HotHighlight status
            int handCardsTotal = 0;
            int handCardsWithHighlight = 0;
            int battlefieldCardsWithHighlight = 0;

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // DIAGNOSTIC: Check if this is a hand card
                bool isInHand = false;
                Transform current = go.transform;
                while (current != null)
                {
                    if (current.name.Contains("LocalHand"))
                    {
                        isInHand = true;
                        break;
                    }
                    current = current.parent;
                }

                // Count all hand cards for diagnostic
                if (isInHand && CardDetector.IsCard(go))
                {
                    handCardsTotal++;
                    // Check HotHighlight status specifically for hand cards - including inactive ones
                    var (hasActive, hasInactive, highlightName) = CardDetector.GetHotHighlightDiagnostic(go);
                    if (hasActive)
                    {
                        handCardsWithHighlight++;
                        MelonLogger.Msg($"[HotHighlightNavigator] DIAG: Hand card WITH ACTIVE highlight: {go.name}, type={highlightName}");
                    }
                    else if (hasInactive)
                    {
                        // This is the key diagnostic - HotHighlight exists but is INACTIVE
                        MelonLogger.Warning($"[HotHighlightNavigator] DIAG: Hand card has INACTIVE highlight: {go.name}, type={highlightName}");
                    }
                }

                // Check for HotHighlight
                string highlightType = CardDetector.GetHotHighlightType(go);

                // No HotHighlight - but in selection mode, include already-selected hand cards
                // (game removes HotHighlight from selected cards, making them un-navigable)
                if (highlightType == null)
                {
                    if (isInHand && selectionMode && CardDetector.IsCard(go) && IsCardSelected(go))
                    {
                        highlightType = "Selected";
                    }
                    else
                    {
                        continue;
                    }
                }

                // Only process actual cards (skip parent containers that also have the highlight)
                if (!CardDetector.IsCard(go)) continue;

                // DIAGNOSTIC: Track battlefield cards
                bool isOnBattlefield = false;
                current = go.transform;
                while (current != null)
                {
                    if (current.name.Contains("BattlefieldCardHolder"))
                    {
                        isOnBattlefield = true;
                        break;
                    }
                    current = current.parent;
                }
                if (isOnBattlefield) battlefieldCardsWithHighlight++;

                // Avoid duplicates
                int id = go.GetInstanceID();
                if (addedIds.Contains(id)) continue;

                var item = CreateHighlightedItem(go, highlightType);
                if (item != null)
                {
                    _items.Add(item);
                    addedIds.Add(id);
                }
            }

            // DIAGNOSTIC: Log summary
            MelonLogger.Msg($"[HotHighlightNavigator] DIAG: Hand cards total={handCardsTotal}, withHighlight={handCardsWithHighlight}, battlefield withHighlight={battlefieldCardsWithHighlight}");

            // Also check for player targets
            DiscoverPlayerTargets(addedIds);

            // When no card/player highlights, check for prompt button choices
            if (_items.Count == 0)
            {
                DiscoverPromptButtons();
            }

            // Sort: Hand cards first, then your permanents, then opponent's, then players
            _items = _items
                .OrderBy(i => i.Zone == "Hand" ? 0 : 1)
                .ThenBy(i => i.IsPlayer ? 1 : 0)
                .ThenBy(i => i.IsOpponent ? 1 : 0)
                .ThenBy(i => i.GameObject?.transform.position.x ?? 0)
                .ToList();

            MelonLogger.Msg($"[HotHighlightNavigator] Found {_items.Count} highlighted items");

            // Reset indices if out of range
            if (_currentIndex >= _items.Count)
                _currentIndex = _items.Count > 0 ? 0 : -1;
            // _opponentIndex is validated at use site against filtered list
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
                item.CardType = DetermineCardType(cardInfo.TypeLine);
            }

            return item;
        }

        /// <summary>
        /// Discovers player portraits as targets using DuelScene_AvatarView reflection.
        /// The game highlights player avatars via HighlightSystem._currentHighlightType,
        /// NOT via HotHighlight child GameObjects.
        /// </summary>
        private void DiscoverPlayerTargets(HashSet<int> addedIds)
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != "DuelScene_AvatarView") continue;

                // Initialize reflection cache on first encounter
                if (!_avatarReflectionInitialized)
                {
                    InitializeAvatarReflection(mb.GetType());
                    if (!_avatarReflectionInitialized) return;
                }

                // Read highlight state: _highlightSystem → _currentHighlightType
                var highlightSystem = _highlightSystemField?.GetValue(mb);
                if (highlightSystem == null) continue;

                int highlightValue = (int)_currentHighlightField.GetValue(highlightSystem);

                // Accept Hot(3), Tepid(2), Cold(1) — skip None(0), Selected(5), others
                if (highlightValue != 1 && highlightValue != 2 && highlightValue != 3)
                    continue;

                // Determine if local or opponent
                bool isLocal = (bool)_isLocalPlayerProp.GetValue(mb);

                // Get clickable element: PortraitButton.gameObject
                var portraitButton = _portraitButtonField?.GetValue(mb) as MonoBehaviour;
                if (portraitButton == null)
                {
                    MelonLogger.Warning($"[HotHighlightNavigator] AvatarView has highlight={highlightValue} but no PortraitButton");
                    continue;
                }

                GameObject clickable = portraitButton.gameObject;
                int id = clickable.GetInstanceID();
                if (addedIds.Contains(id)) continue;

                string name = isLocal ? Strings.You : Strings.Opponent;
                _items.Add(new HighlightedItem
                {
                    GameObject = clickable,
                    Name = name,
                    Zone = "Player",
                    HighlightType = $"AvatarHighlight({highlightValue})",
                    IsOpponent = !isLocal,
                    IsPlayer = true,
                    CardType = "Player"
                });
                addedIds.Add(id);
                MelonLogger.Msg($"[HotHighlightNavigator] Added {(isLocal ? "local" : "opponent")} player as target (highlight={highlightValue})");
            }
        }

        /// <summary>
        /// Initializes reflection cache for DuelScene_AvatarView fields.
        /// </summary>
        private static void InitializeAvatarReflection(Type avatarType)
        {
            try
            {
                _avatarViewType = avatarType;

                _highlightSystemField = avatarType.GetField("_highlightSystem", PrivateInstance);
                if (_highlightSystemField == null)
                {
                    MelonLogger.Warning("[HotHighlightNavigator] Could not find _highlightSystem field on DuelScene_AvatarView");
                    return;
                }

                Type highlightSystemType = _highlightSystemField.FieldType;
                _currentHighlightField = highlightSystemType.GetField("_currentHighlightType", PrivateInstance);
                if (_currentHighlightField == null)
                {
                    MelonLogger.Warning($"[HotHighlightNavigator] Could not find _currentHighlightType on {highlightSystemType.Name}");
                    return;
                }

                _isLocalPlayerProp = avatarType.GetProperty("IsLocalPlayer", PublicInstance);
                if (_isLocalPlayerProp == null)
                {
                    MelonLogger.Warning("[HotHighlightNavigator] Could not find IsLocalPlayer property on DuelScene_AvatarView");
                    return;
                }

                _portraitButtonField = avatarType.GetField("PortraitButton", PrivateInstance);
                if (_portraitButtonField == null)
                {
                    MelonLogger.Warning("[HotHighlightNavigator] Could not find PortraitButton field on DuelScene_AvatarView");
                    return;
                }

                _avatarReflectionInitialized = true;
                MelonLogger.Msg($"[HotHighlightNavigator] Avatar reflection initialized: HighlightSystem={highlightSystemType.Name}, HighlightField={_currentHighlightField.FieldType.Name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HotHighlightNavigator] Failed to initialize avatar reflection: {ex.Message}");
            }
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
            if (item.IsPromptButton)
            {
                int position = _currentIndex + 1;
                int total = _items.Count;
                string announcement = total > 1 ? $"{item.Name}, {position} of {total}" : item.Name;
                _announcer.Announce(announcement, AnnouncementPriority.High);
                _lastItemZone = "Button";
                return;
            }

            if (item.IsPlayer)
            {
                int position = _currentIndex + 1;
                int total = _items.Count;
                string name = item.IsOpponent ? Strings.Opponent : Strings.You;
                _announcer.Announce($"{name}, player, {position} of {total}", AnnouncementPriority.High);
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
            MelonLogger.Warning($"[HotHighlightNavigator] Card {item.Name} not found in zone navigators, using direct announcement");
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
                    MelonLogger.Msg($"[HotHighlightNavigator] Clicked prompt button: {item.Name}");
                }
                _items.Clear();
                _currentIndex = -1;
                return;
            }

            bool selectionMode = IsSelectionModeActive();
            MelonLogger.Msg($"[HotHighlightNavigator] Activating: {item.Name} in {item.Zone} (selection mode: {selectionMode})");

            if (item.Zone == "Hand")
            {
                if (selectionMode)
                {
                    // Selection mode (discard, etc.) - single click to toggle selection
                    // Check current state before clicking
                    bool wasSelected = IsCardSelected(item.GameObject);
                    MelonLogger.Msg($"[HotHighlightNavigator] Toggling selection on: {item.Name} (was selected: {wasSelected})");

                    var result = UIActivator.SimulatePointerClick(item.GameObject);
                    if (result.Success)
                    {
                        // Announce toggle result after game updates
                        MelonCoroutines.Start(AnnounceSelectionToggleDelayed(item.Name, wasSelected));
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
                            MelonLogger.Msg($"[HotHighlightNavigator] Card play initiated");
                        }
                        else
                        {
                            _announcer.Announce(Strings.CouldNotPlay(item.Name), AnnouncementPriority.High);
                            MelonLogger.Msg($"[HotHighlightNavigator] Card play failed: {message}");
                        }
                    });
                }
            }
            else
            {
                // Battlefield/Stack/Player target - single click to select
                var result = UIActivator.SimulatePointerClick(item.GameObject);

                if (result.Success)
                {
                    string announcement = item.IsPlayer ? Strings.Target_Targeted(item.Name) : Strings.Target_Selected(item.Name);
                    _announcer.Announce(announcement, AnnouncementPriority.Normal);
                    MelonLogger.Msg($"[HotHighlightNavigator] {announcement}");
                }
                else
                {
                    _announcer.Announce(Strings.CouldNotTarget(item.Name), AnnouncementPriority.High);
                    MelonLogger.Warning($"[HotHighlightNavigator] Click failed: {result.Message}");
                }
            }

            // In selection mode, preserve position so next Tab advances to the next card
            // Items will be refreshed via DiscoverAllHighlights() on next Tab press
            if (selectionMode && item.Zone == "Hand")
                return;

            // Clear state after activation - highlights will update
            _items.Clear();
            _currentIndex = -1;
            _opponentIndex = -1;
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
        /// Determines card type from type line.
        /// </summary>
        private string DetermineCardType(string typeLine)
        {
            if (string.IsNullOrEmpty(typeLine)) return "Permanent";

            string lower = typeLine.ToLower();

            if (lower.Contains("creature")) return "Creature";
            if (lower.Contains("planeswalker")) return "Planeswalker";
            if (lower.Contains("artifact")) return "Artifact";
            if (lower.Contains("enchantment")) return "Enchantment";
            if (lower.Contains("land")) return "Land";
            if (lower.Contains("instant")) return "Instant";
            if (lower.Contains("sorcery")) return "Sorcery";

            return "Permanent";
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
            var button = FindPrimaryButton();
            if (button == null) return null;

            var tmpText = button.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string text = tmpText.text?.Trim();
                if (!string.IsNullOrEmpty(text) && text != "Ctrl")
                    return text;
            }

            var uiText = button.GetComponentInChildren<Text>();
            if (uiText != null)
            {
                string text = uiText.text?.Trim();
                if (!string.IsNullOrEmpty(text) && text != "Ctrl")
                    return text;
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
        /// Gets text from a button GameObject (TMP_Text or Text component).
        /// </summary>
        private string GetButtonText(GameObject button)
        {
            if (button == null) return null;

            var tmpText = button.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string text = tmpText.text?.Trim();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            var uiText = button.GetComponentInChildren<Text>();
            if (uiText != null)
            {
                string text = uiText.text?.Trim();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

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
            if (button == null) return false;
            var cg = button.GetComponent<CanvasGroup>();
            if (cg == null) return true; // No CanvasGroup = assume visible
            return cg.alpha > 0 && cg.interactable;
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
            string primaryText = GetButtonText(primaryButton);

            var secondaryButton = FindSecondaryButton();
            string secondaryText = GetButtonText(secondaryButton);

            // Only add when BOTH have meaningful text (sacrifice vs pay mana, etc.)
            if (!IsMeaningfulButtonText(primaryText) || !IsMeaningfulButtonText(secondaryText))
                return;

            // Check CanvasGroup visibility - the game hides inactive buttons by setting
            // CanvasGroup alpha=0 and interactable=false, even though Selectable.interactable
            // remains true. Buttons with alpha=0 are invisible and not real choices.
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

            MelonLogger.Msg($"[HotHighlightNavigator] Added prompt buttons: '{primaryText}' and '{secondaryText}'");
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

            // COMMENTED OUT: "Targeting mode" concept removed - we just check for Submit button with number
            // The distinction between targeting and selection wasn't useful since:
            // - Game handles targeting cancel via its own undo
            // - Battlefield HotHighlight can be activated abilities, not just spell targets
            // if (CardDetector.HasValidTargetsOnBattlefield())
            //     return false;

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
        /// </summary>
        /// <param name="cardName">Name of the card that was toggled</param>
        /// <param name="wasSelected">Whether the card was selected before the click</param>
        private IEnumerator AnnounceSelectionToggleDelayed(string cardName, bool wasSelected)
        {
            yield return new WaitForSeconds(0.2f);

            // Announce toggle action (opposite of what it was)
            string action = wasSelected ? Strings.Deselected : Strings.Selected;
            string toggleAnnouncement = $"{cardName} {action}";

            var info = GetSubmitButtonInfo();
            if (info != null)
            {
                // Announce both the toggle and the count
                _announcer.Announce($"{toggleAnnouncement}, {Strings.CardsSelected(info.Value.count)}", AnnouncementPriority.Normal);
            }
            else
            {
                _announcer.Announce(toggleAnnouncement, AnnouncementPriority.Normal);
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
                    MelonLogger.Msg($"[HotHighlightNavigator] Found selection indicator: {child.name} on {card.name}");
                    return true;
                }
            }

            return false;
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
                    MelonLogger.Msg($"[HotHighlightNavigator] Selection mode entered, prompt: {promptText}");
                    _announcer.Announce(promptText, AnnouncementPriority.High);
                }
                else
                {
                    // Fallback to submit button text if no prompt found
                    var info = GetSubmitButtonInfo();
                    if (info != null)
                    {
                        string buttonText = UITextExtractor.GetButtonText(info.Value.button);
                        MelonLogger.Msg($"[HotHighlightNavigator] Selection mode entered, button fallback: {buttonText}");
                        _announcer.Announce(buttonText, AnnouncementPriority.High);
                    }
                }
            }
            else if (!isActive && _wasInSelectionMode)
            {
                _wasInSelectionMode = false;
                MelonLogger.Msg("[HotHighlightNavigator] Selection mode exited");
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
                        return text;
                }
            }
            return null;
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
        /// Attempts to toggle selection on a card if selection mode (discard, exile, etc.) is active.
        /// Called by ZoneNavigator when the user presses Enter on a hand card navigated via zone shortcuts.
        /// Returns true if selection was handled, false if not in selection mode.
        /// </summary>
        public bool TryToggleSelection(GameObject card)
        {
            if (!IsSelectionModeActive()) return false;

            string cardName = CardDetector.GetCardName(card) ?? card.name;
            bool wasSelected = IsCardSelected(card);
            MelonLogger.Msg($"[HotHighlightNavigator] Zone nav toggling selection: {cardName} (was selected: {wasSelected})");

            var result = UIActivator.SimulatePointerClick(card);
            if (result.Success)
                MelonCoroutines.Start(AnnounceSelectionToggleDelayed(cardName, wasSelected));
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
