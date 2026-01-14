using UnityEngine;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Handles target selection during spells and abilities.
    /// When the game enters targeting mode, Tab cycles through valid targets.
    /// Enter selects the current target, Escape cancels.
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
        /// Called when the game enters targeting mode.
        /// Discovers valid targets and announces the mode.
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
                                     "Press Tab to cycle, Enter to select, Escape to cancel.";
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

            if (Input.GetKeyDown(KeyCode.Escape))
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

            // Prepare CardInfoNavigator for arrow key navigation on this target
            var cardNavigator = MTGAAccessibilityMod.Instance?.CardNavigator;
            if (cardNavigator != null && target.GameObject != null)
            {
                cardNavigator.PrepareForCard(target.GameObject, ZoneType.Battlefield);
            }
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

                // Check for HotHighlight - indicates valid target
                if (HasTargetingHighlight(go))
                {
                    var target = CreateTargetFromCard(go);
                    if (target != null && !_validTargets.Any(t => t.InstanceId == target.InstanceId))
                    {
                        _validTargets.Add(target);
                    }
                }
            }

            // Sort: your permanents first, then opponent's, then players
            _validTargets = _validTargets
                .OrderBy(t => t.IsOpponent ? 1 : 0)
                .ThenBy(t => t.Type == CardTargetType.Player ? 1 : 0)
                .ThenBy(t => t.Name)
                .ToList();

            MelonLogger.Msg($"[TargetNavigator] Found {_validTargets.Count} valid targets");
        }

        /// <summary>
        /// Checks if a card has an active HotHighlight child, indicating it's a valid target.
        /// </summary>
        private bool HasTargetingHighlight(GameObject card)
        {
            foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.gameObject == card) continue;
                if (!child.gameObject.activeInHierarchy) continue;

                if (child.name.Contains("HotHighlight"))
                {
                    return true;
                }
            }
            return false;
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
