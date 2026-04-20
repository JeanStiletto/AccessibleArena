using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class BaseNavigator
    {
        /// <summary>
        /// Discovers challenge-invite friend tiles (ChallengeInviteWindowEntityPlayer)
        /// and already-invited entries (ChallengeInviteWindowEntityInvited). Friend tiles
        /// become navigable Toggle elements whose activation flips the game's _inviteToggle
        /// (triggering OnInviteToggleChanged → Player.Invited = true). Already-invited
        /// entries become read-only text blocks.
        /// </summary>
        private void DiscoverChallengeInviteTiles(GameObject popup, HashSet<GameObject> addedObjects, List<Transform> skipTransforms)
        {
            // Pass 1: Already-invited read-only entries
            var invitedDiscovered = new List<(string label, float sortOrder)>();
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != T.ChallengeInviteWindowEntityInvited) continue;

                string playerName = GetChallengeInvitePlayerName(mb);
                if (string.IsNullOrEmpty(playerName)) continue;

                string label = $"{playerName}, {Strings.ChallengeInvited}";
                var pos = mb.gameObject.transform.position;
                invitedDiscovered.Add((label, -pos.y * 1000 + pos.x));
                addedObjects.Add(mb.gameObject);
            }

            // Emit the "Eingeladen:" section heading BEFORE the invited entries, but only
            // when entries exist. This keeps the heading grouped with the list it labels
            // instead of appearing as a dangling text block further down.
            if (invitedDiscovered.Count > 0)
            {
                string headingText = Strings.ChallengeInvited + ":";
                var headingObj = FindInvitedSectionHeading(popup, headingText);
                _elements.Add(new NavigableElement
                {
                    GameObject = null,
                    Label = headingText,
                    Role = UIElementClassifier.ElementRole.TextBlock
                });
                Log.Msg("{NavigatorId}", $"Popup: invited section heading: {headingText}");
                if (headingObj != null)
                    skipTransforms.Add(headingObj.transform);
            }

            foreach (var (label, _) in invitedDiscovered.OrderBy(x => x.sortOrder))
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = null,
                    Label = label,
                    Role = UIElementClassifier.ElementRole.TextBlock
                });
                Log.Msg("{NavigatorId}", $"Popup: invited entry: {label}");
            }

            // Pass 2: Friend tiles (toggleable)
            var friendDiscovered = new List<(GameObject obj, string label, float sortOrder)>();
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name != T.ChallengeInviteWindowEntityPlayer) continue;

                var toggle = GetChallengeInviteToggle(mb);
                if (toggle == null) continue;

                string playerName = GetChallengeInvitePlayerName(mb);
                if (string.IsNullOrEmpty(playerName)) playerName = mb.gameObject.name;

                string stateLabel = toggle.isOn ? Strings.ChallengeInvited : Strings.ChallengeNotInvited;
                string label = $"{playerName}, {stateLabel}";
                var pos = toggle.gameObject.transform.position;
                friendDiscovered.Add((toggle.gameObject, label, -pos.y * 1000 + pos.x));
                addedObjects.Add(toggle.gameObject);
                addedObjects.Add(mb.gameObject);
            }
            foreach (var (obj, label, _) in friendDiscovered.OrderBy(x => x.sortOrder))
            {
                _elements.Add(new NavigableElement
                {
                    GameObject = obj,
                    Label = label,
                    Role = UIElementClassifier.ElementRole.Toggle
                });
                Log.Msg("{NavigatorId}", $"Popup: friend tile: {label}");
            }
        }

        /// <summary>
        /// Reads Player.PlayerName via reflection from a ChallengeInviteWindowEntityPlayer
        /// or ChallengeInviteWindowEntityInvited component.
        /// </summary>
        private static string GetChallengeInvitePlayerName(MonoBehaviour tile)
        {
            if (tile == null) return null;
            var type = tile.GetType();
            var playerProp = type.GetProperty("Player", AllInstanceFlags);
            object player = playerProp?.GetValue(tile);
            if (player == null) return null;
            var nameProp = player.GetType().GetProperty("PlayerName", AllInstanceFlags);
            if (nameProp == null) return null;
            string raw = nameProp.GetValue(player) as string;
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }

        /// <summary>
        /// Reads _inviteToggle (private Toggle field) from a ChallengeInviteWindowEntityPlayer.
        /// </summary>
        private static Toggle GetChallengeInviteToggle(MonoBehaviour tile)
        {
            if (tile == null) return null;
            var field = tile.GetType().GetField("_inviteToggle", AllInstanceFlags);
            return field?.GetValue(tile) as Toggle;
        }

        /// <summary>
        /// True if a transform is inside a ChallengeInviteWindowEntityPlayer or
        /// ChallengeInviteWindowEntityInvited tile - used so text block discovery
        /// doesn't duplicate the player name TMP_Text child.
        /// </summary>
        private static bool IsInsideChallengeInviteTile(Transform child, Transform stopAt)
        {
            Transform current = child;
            while (current != null && current != stopAt)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName == T.ChallengeInviteWindowEntityPlayer ||
                        typeName == T.ChallengeInviteWindowEntityInvited)
                        return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Finds the TMP_Text serving as the "Eingeladen:" section heading in the invite popup
        /// by content match against the localized ChallengeInvited + ":" string. Returns null
        /// if no such text exists (e.g. non-invite popup, or first-open with no heading rendered).
        /// </summary>
        private static GameObject FindInvitedSectionHeading(GameObject popup, string headingText)
        {
            if (popup == null || string.IsNullOrEmpty(headingText)) return null;
            foreach (var tmp in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                string text = UITextExtractor.CleanText(tmp.text);
                if (string.IsNullOrEmpty(text)) continue;
                if (string.Equals(text.Trim(), headingText, StringComparison.Ordinal))
                    return tmp.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Returns the _recentChallengesDropDown GameObject from a ChallengeInviteWindowPopup,
        /// or null if this popup is not a ChallengeInviteWindowPopup.
        /// </summary>
        private static GameObject GetChallengeInviteRecentDropdown(GameObject popup)
        {
            if (popup == null) return null;
            foreach (var mb in popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name != T.ChallengeInviteWindowPopup) continue;

                var field = mb.GetType().GetField("_recentChallengesDropDown", AllInstanceFlags);
                var dropdown = field?.GetValue(mb) as MonoBehaviour;
                return dropdown?.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Rebuilds a friend-tile label with the current toggle state. Matches labels ending
        /// with ", {ChallengeInvited}" or ", {ChallengeNotInvited}" and swaps to the live state.
        /// </summary>
        private static string RefreshChallengeInviteToggleLabel(GameObject obj, string label)
        {
            var toggle = obj?.GetComponent<Toggle>();
            if (toggle == null || string.IsNullOrEmpty(label)) return label;

            string invited = ", " + Strings.ChallengeInvited;
            string notInvited = ", " + Strings.ChallengeNotInvited;
            string stateLabel = toggle.isOn ? Strings.ChallengeInvited : Strings.ChallengeNotInvited;

            if (label.EndsWith(invited, StringComparison.Ordinal))
                return label.Substring(0, label.Length - invited.Length) + ", " + stateLabel;
            if (label.EndsWith(notInvited, StringComparison.Ordinal))
                return label.Substring(0, label.Length - notInvited.Length) + ", " + stateLabel;
            return label;
        }
    }
}
