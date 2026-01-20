using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Handles target selection during spells and abilities.
    /// When the game enters targeting mode, Tab cycles through valid targets.
    /// Enter selects the current target, Backspace cancels.
    /// </summary>
    public class TargetNavigator
    {
        private readonly IAnnouncementService _announcer;
        private readonly ZoneNavigator _zoneNavigator;

        private bool _isTargeting;
        private List<TargetInfo> _validTargets = new List<TargetInfo>();
        private int _currentIndex = -1;

        public bool IsTargeting => _isTargeting;
        public int TargetCount => _validTargets.Count;
        public TargetInfo CurrentTarget =>
            (_currentIndex >= 0 && _currentIndex < _validTargets.Count)
                ? _validTargets[_currentIndex]
                : null;

        public TargetNavigator(IAnnouncementService announcer, ZoneNavigator zoneNavigator)
        {
            _announcer = announcer;
            _zoneNavigator = zoneNavigator;
        }

        /// <summary>
        /// Unified entry point for targeting mode. Performs all necessary checks before entering.
        /// Use this instead of calling EnterTargetMode() directly from DuelNavigator or DuelAnnouncer.
        /// </summary>
        /// <param name="requireValidTargets">If true, won't enter targeting mode without valid targets.
        /// Set to false when responding to game events where targets may not be visible yet.</param>
        /// <returns>True if targeting mode was entered, false if conditions not met.</returns>
        public bool TryEnterTargetMode(bool requireValidTargets = true)
        {
            // Don't re-enter if already targeting
            if (_isTargeting)
            {
                MelonLogger.Msg("[TargetNavigator] TryEnterTargetMode: already in targeting mode");
                return false;
            }

            // Discover valid targets first
            DiscoverValidTargets();

            // Check if we have valid targets (when required)
            if (requireValidTargets && _validTargets.Count == 0)
            {
                MelonLogger.Msg("[TargetNavigator] TryEnterTargetMode: no valid targets found");
                return false;
            }

            // Enter targeting mode
            _isTargeting = true;

            if (_validTargets.Count > 0)
            {
                _currentIndex = 0;
                string announcement = $"Select a target. {_validTargets.Count} valid target{(_validTargets.Count != 1 ? "s" : "")}. " +
                                     "Press Tab to cycle, Enter to select, Backspace to cancel.";
                _announcer.AnnounceInterrupt(announcement);
                AnnounceCurrentTarget();
            }
            else
            {
                // Event-based entry without visible targets yet - announce generic message
                _announcer.AnnounceInterrupt(Strings.SelectTargetNoValid);
                MelonLogger.Msg("[TargetNavigator] Entered targeting mode (event-based, no visible targets yet)");
            }

            MelonLogger.Msg($"[TargetNavigator] TryEnterTargetMode: entered with {_validTargets.Count} targets");
            return true;
        }

        /// <summary>
        /// Called when the game enters targeting mode.
        /// Discovers valid targets and announces the mode.
        /// NOTE: Prefer using TryEnterTargetMode() which performs additional checks.
        /// </summary>
        public void EnterTargetMode()
        {
            if (_isTargeting)
            {
                MelonLogger.Msg("[TargetNavigator] Already in targeting mode, refreshing targets");
            }

            _isTargeting = true;
            DiscoverValidTargets();

            if (_validTargets.Count > 0)
            {
                _currentIndex = 0;
                string announcement = $"Select a target. {_validTargets.Count} valid target{(_validTargets.Count != 1 ? "s" : "")}. " +
                                     "Press Tab to cycle, Enter to select, Backspace to cancel.";
                _announcer.AnnounceInterrupt(announcement);
                AnnounceCurrentTarget();
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.SelectTargetNoValid);
                MelonLogger.Warning("[TargetNavigator] No valid targets discovered");
            }
        }

        /// <summary>
        /// Called when targeting mode ends (target selected or cancelled).
        /// </summary>
        public void ExitTargetMode()
        {
            if (!_isTargeting) return;

            MelonLogger.Msg("[TargetNavigator] Exiting targeting mode");
            _isTargeting = false;
            _validTargets.Clear();
            _currentIndex = -1;
        }

        /// <summary>
        /// Handles input during targeting mode.
        /// Returns true if input was consumed.
        /// </summary>
        public bool HandleInput()
        {
            if (!_isTargeting) return false;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift)
                    PreviousTarget();
                else
                    NextTarget();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SelectCurrentTarget();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                CancelTargeting();
                return true;
            }

            return false;
        }

        public void NextTarget()
        {
            if (_validTargets.Count == 0)
            {
                _announcer.Announce(Strings.NoValidTargets, AnnouncementPriority.High);
                return;
            }

            _currentIndex = (_currentIndex + 1) % _validTargets.Count;
            AnnounceCurrentTarget();
        }

        public void PreviousTarget()
        {
            if (_validTargets.Count == 0)
            {
                _announcer.Announce(Strings.NoValidTargets, AnnouncementPriority.High);
                return;
            }

            _currentIndex--;
            if (_currentIndex < 0)
                _currentIndex = _validTargets.Count - 1;

            AnnounceCurrentTarget();
        }

        public void SelectCurrentTarget()
        {
            if (_currentIndex < 0 || _currentIndex >= _validTargets.Count)
            {
                _announcer.Announce(Strings.NoTargetSelected, AnnouncementPriority.High);
                return;
            }

            var target = _validTargets[_currentIndex];
            MelonLogger.Msg($"[TargetNavigator] Selecting target: {target.Name}");

            var result = UIActivator.SimulatePointerClick(target.GameObject);

            if (result.Success)
            {
                _announcer.Announce(Strings.Targeted(target.Name), AnnouncementPriority.Normal);
                MelonLogger.Msg($"[TargetNavigator] Target selected successfully, exiting targeting mode");
                // NOTE: For multi-target spells, this exits after each target selection.
                // The auto-detect in DuelNavigator should re-enter targeting mode if more
                // targets are needed (hasValidTargets + hasSpellOnStack). This may cause
                // a brief "gap" in announcements. If this is a problem, we need a way to
                // distinguish targeting HotHighlight from activatable ability HotHighlight.
                ExitTargetMode();
            }
            else
            {
                _announcer.Announce(Strings.CouldNotTarget(target.Name), AnnouncementPriority.High);
                MelonLogger.Warning($"[TargetNavigator] Click failed: {result.Message}");
            }
        }

        public void CancelTargeting()
        {
            MelonLogger.Msg("[TargetNavigator] Cancelling targeting");
            _announcer.Announce(Strings.TargetingCancelled, AnnouncementPriority.Normal);
            ExitTargetMode();
        }

        private void AnnounceCurrentTarget()
        {
            if (_currentIndex < 0 || _currentIndex >= _validTargets.Count) return;

            var target = _validTargets[_currentIndex];
            int position = _currentIndex + 1;
            int total = _validTargets.Count;

            string ownerInfo = target.IsOpponent ? "opponent's " : "";
            string announcement = $"{target.GetAnnouncement()}, {ownerInfo}{target.Type}, {position} of {total}";

            // Use High priority to bypass duplicate check - user explicitly pressed Tab
            _announcer.Announce(announcement, AnnouncementPriority.High);

            // Set EventSystem focus to the target
            if (target.GameObject != null)
            {
                ZoneNavigator.SetFocusedGameObject(target.GameObject, "TargetNavigator");
            }

            // Update zone context based on actual target location (not just assuming battlefield)
            // This allows targets on stack, graveyard, exile to be properly identified
            if (target.Type != CardTargetType.Player)
            {
                var targetZone = DetermineTargetZone(target);
                _zoneNavigator.SetCurrentZone(targetZone, "TargetNavigator");

                // Prepare CardInfoNavigator for arrow key navigation on this target
                var cardNavigator = AccessibleArenaMod.Instance?.CardNavigator;
                if (cardNavigator != null && target.GameObject != null)
                {
                    cardNavigator.PrepareForCard(target.GameObject, targetZone);
                }
            }
        }

        /// <summary>
        /// Determines the zone type from a target's parent hierarchy.
        /// Checks for Stack, Graveyard, Exile, Hand before defaulting to Battlefield.
        /// </summary>
        private ZoneType DetermineTargetZone(TargetInfo target)
        {
            if (target.Type == CardTargetType.Player) return ZoneType.Battlefield; // Players don't have a zone

            if (target.GameObject == null) return ZoneType.Battlefield; // Default fallback

            // Check parent hierarchy for zone indicators
            var current = target.GameObject.transform;
            while (current != null)
            {
                var name = current.name;

                // Check for specific zone patterns (order matters - more specific first)
                if (name.Contains("StackCardHolder") || name.Contains("Stack"))
                {
                    MelonLogger.Msg($"[TargetNavigator] Target {target.Name} detected in Stack zone");
                    return ZoneType.Stack;
                }
                if (name.Contains("LocalGraveyard") || name.Contains("OpponentGraveyard"))
                {
                    bool isOpponent = name.Contains("Opponent");
                    MelonLogger.Msg($"[TargetNavigator] Target {target.Name} detected in {(isOpponent ? "Opponent " : "")}Graveyard zone");
                    return isOpponent ? ZoneType.OpponentGraveyard : ZoneType.Graveyard;
                }
                if (name.Contains("ExileCardHolder") || name.Contains("Exile"))
                {
                    MelonLogger.Msg($"[TargetNavigator] Target {target.Name} detected in Exile zone");
                    return ZoneType.Exile;
                }
                if (name.Contains("LocalHand") || name.Contains("Hand"))
                {
                    MelonLogger.Msg($"[TargetNavigator] Target {target.Name} detected in Hand zone");
                    return ZoneType.Hand;
                }
                if (name.Contains("BattlefieldCardHolder") || name.Contains("Battlefield"))
                {
                    // Don't log battlefield - it's the expected default
                    return ZoneType.Battlefield;
                }

                current = current.parent;
            }

            // Default to battlefield if no zone indicator found
            return ZoneType.Battlefield;
        }

        /// <summary>
        /// Discovers valid targets by scanning for cards with HotHighlight indicators.
        /// The game shows HotHighlightBattlefield on cards that are valid targets.
        /// </summary>
        private void DiscoverValidTargets()
        {
            _validTargets.Clear();
            _zoneNavigator.DiscoverZones();

            // Scan battlefield and stack for cards with targeting highlights
            string[] targetZones = new[] { "BattlefieldCardHolder", "StackCardHolder" };

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                // Check if card is in a targetable zone
                bool inTargetZone = false;
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var zone in targetZones)
                    {
                        if (current.name.Contains(zone))
                        {
                            inTargetZone = true;
                            break;
                        }
                    }
                    if (inTargetZone) break;
                    current = current.parent;
                }

                if (!inTargetZone) continue;
                if (!CardDetector.IsCard(go)) continue;

                // Check for HotHighlight - indicates valid target (uses unified method)
                if (CardDetector.HasHotHighlight(go))
                {
                    var target = CreateTargetFromCard(go);
                    if (target != null && !_validTargets.Any(t => t.InstanceId == target.InstanceId))
                    {
                        _validTargets.Add(target);
                    }
                }
            }

            // Also check for player targets (avatars with HotHighlight)
            DiscoverPlayerTargets();

            // Sort: your permanents first, then opponent's, then players
            _validTargets = _validTargets
                .OrderBy(t => t.IsOpponent ? 1 : 0)
                .ThenBy(t => t.Type == CardTargetType.Player ? 1 : 0)
                .ThenBy(t => t.Name)
                .ToList();

            MelonLogger.Msg($"[TargetNavigator] Found {_validTargets.Count} valid targets");
        }

        /// <summary>
        /// Discovers player avatars as valid targets when they have HotHighlight.
        /// Players use MatchTimer objects with structure: MatchTimer > Icon > HoverArea
        /// HotHighlight appears on MatchTimer, but we click HoverArea/Icon to target.
        /// </summary>
        private void DiscoverPlayerTargets()
        {
            // Find MatchTimer objects for local player and opponent
            var (localTimer, localClickable) = FindPlayerMatchTimer(isOpponent: false);
            var (opponentTimer, opponentClickable) = FindPlayerMatchTimer(isOpponent: true);

            // Check local player - HotHighlight is on MatchTimer, click target is HoverArea/Icon
            if (localTimer != null && localClickable != null && CardDetector.HasHotHighlight(localTimer))
            {
                _validTargets.Add(new TargetInfo
                {
                    GameObject = localClickable,
                    Name = Strings.You,
                    InstanceId = (uint)localClickable.GetInstanceID(),
                    Type = CardTargetType.Player,
                    IsOpponent = false,
                    Details = ""
                });
                MelonLogger.Msg("[TargetNavigator] Added local player as valid target");
            }

            // Check opponent - HotHighlight is on MatchTimer, click target is HoverArea/Icon
            if (opponentTimer != null && opponentClickable != null && CardDetector.HasHotHighlight(opponentTimer))
            {
                _validTargets.Add(new TargetInfo
                {
                    GameObject = opponentClickable,
                    Name = Strings.Opponent,
                    InstanceId = (uint)opponentClickable.GetInstanceID(),
                    Type = CardTargetType.Player,
                    IsOpponent = true,
                    Details = ""
                });
                MelonLogger.Msg("[TargetNavigator] Added opponent as valid target");
            }
        }

        /// <summary>
        /// Finds the player target for targeting spells.
        /// Searches multiple player-related objects for HotHighlight:
        /// - MatchTimer (LocalPlayerMatchTimer/OpponentMatchTimer)
        /// - Player containers (LocalPlayer/Opponent under BattleFieldStaticElementsLayout)
        /// - PlayerHpContainer, LifeFrameContainer, AvatarContainer
        /// Returns (highlightRoot, clickableElement) - check HotHighlight on highlightRoot, click clickableElement.
        /// Note: Color Challenge (tutorial) does not support player targeting.
        /// </summary>
        private (GameObject highlightRoot, GameObject clickable) FindPlayerMatchTimer(bool isOpponent)
        {
            string prefix = isOpponent ? "Opponent" : "LocalPlayer";
            GameObject foundWithHighlight = null;
            GameObject clickable = null;

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;

                string goName = go.name;

                // Check various player-related objects
                bool isPlayerObject = false;
                bool isMatchTimer = false;

                if (goName.Contains(prefix) && goName.Contains("MatchTimer"))
                {
                    isPlayerObject = true;
                    isMatchTimer = true;
                }
                else if (goName.Contains(prefix) && (goName.Contains("PlayerHp") || goName.Contains("LifeFrame") || goName.Contains("Avatar")))
                {
                    isPlayerObject = true;
                }
                // Also check for generic player containers in BattleFieldStaticElementsLayout
                else if (goName == prefix || goName == (isOpponent ? "Opponent" : "LocalPlayer"))
                {
                    var parent = go.transform.parent;
                    if (parent != null && (parent.name == "Base" || parent.name.Contains("BattleField")))
                    {
                        isPlayerObject = true;
                    }
                }

                if (!isPlayerObject) continue;

                bool hasHighlight = CardDetector.HasHotHighlight(go);

                if (hasHighlight && foundWithHighlight == null)
                {
                    foundWithHighlight = go;
                }

                // For MatchTimer, try to get the clickable HoverArea
                if (isMatchTimer && clickable == null)
                {
                    var iconTransform = go.transform.Find("Icon");
                    if (iconTransform != null)
                    {
                        var hoverArea = iconTransform.Find("HoverArea");
                        clickable = hoverArea != null ? hoverArea.gameObject : iconTransform.gameObject;
                    }
                    else
                    {
                        clickable = go;
                    }
                }
                // For other containers with highlight, use the container itself as clickable
                else if (hasHighlight && clickable == null)
                {
                    clickable = go;
                }
            }

            // Return the object with highlight
            if (foundWithHighlight != null)
            {
                return (foundWithHighlight, clickable ?? foundWithHighlight);
            }

            return (null, null);
        }

        private TargetInfo CreateTargetFromCard(GameObject cardObj)
        {
            var cardInfo = CardDetector.ExtractCardInfo(cardObj);
            if (!cardInfo.IsValid)
            {
                string name = CardDetector.GetCardName(cardObj);
                if (name == "Unknown card")
                    return null;

                return new TargetInfo
                {
                    GameObject = cardObj,
                    Name = name,
                    InstanceId = (uint)cardObj.GetInstanceID(),
                    Type = CardTargetType.Permanent,
                    IsOpponent = CardDetector.IsOpponentCard(cardObj),
                    Details = ""
                };
            }

            var targetType = DetermineCardTargetType(cardInfo.TypeLine);
            string details = !string.IsNullOrEmpty(cardInfo.PowerToughness) ? cardInfo.PowerToughness : "";

            return new TargetInfo
            {
                GameObject = cardObj,
                Name = cardInfo.Name,
                InstanceId = (uint)cardObj.GetInstanceID(),
                Type = targetType,
                IsOpponent = CardDetector.IsOpponentCard(cardObj),
                Details = details
            };
        }

        private CardTargetType DetermineCardTargetType(string typeLine)
        {
            if (string.IsNullOrEmpty(typeLine))
                return CardTargetType.Permanent;

            string lower = typeLine.ToLower();

            if (lower.Contains("creature"))
                return CardTargetType.Creature;
            if (lower.Contains("planeswalker"))
                return CardTargetType.Planeswalker;
            if (lower.Contains("artifact"))
                return CardTargetType.Artifact;
            if (lower.Contains("enchantment"))
                return CardTargetType.Enchantment;
            if (lower.Contains("land"))
                return CardTargetType.Land;

            return CardTargetType.Permanent;
        }
    }
}
