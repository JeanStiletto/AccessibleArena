using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using TMPro;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides friend tile information for accessibility navigation.
    /// Reads display name, status, and available actions from social tile components.
    ///
    /// Runtime tile types (discovered via reflection):
    ///   FriendTile: _labelName, _labelStatus, _buttonRemoveFriend, _buttonBlockFriend,
    ///              _buttonChallengeFriend, _challengeEnabled, Callback_OpenChat, Friend property
    ///   InviteOutgoingTile: _labelName, _labelDateSent, _buttonCancel, Callback_Reject, Invite property
    ///   InviteIncomingTile: (expected similar pattern with accept/decline buttons)
    /// </summary>
    public static class FriendInfoProvider
    {
        // Action identifiers used in the action list
        private const string ActionChat = "chat";
        private const string ActionChallenge = "challenge";
        private const string ActionUnfriend = "unfriend";
        private const string ActionBlock = "block";
        private const string ActionRevoke = "revoke";
        private const string ActionAccept = "accept";
        private const string ActionDecline = "decline";
        private const string ActionUnblock = "unblock";

        // Known social tile component type names
        private static readonly string[] SocialTileTypeNames = new[]
        {
            "FriendTile",
            "InviteOutgoingTile",
            "InviteIncomingTile",
            "BlockedTile",
            "BlockedFriendTile"
        };

        // Cached reflection per tile type (keyed by type to handle multiple tile types)
        private static readonly Dictionary<Type, TileReflectionCache> _caches = new Dictionary<Type, TileReflectionCache>();

        private class TileReflectionCache
        {
            public FieldInfo LabelName;         // TMP_Text
            public FieldInfo LabelStatus;       // FriendTile: _labelStatus (Localize component, not TMP_Text)
            public FieldInfo LabelDateSent;     // InviteOutgoingTile: _labelDateSent (Localize component)
            public FieldInfo ChallengeEnabled;  // FriendTile: _challengeEnabled (bool)
            public FieldInfo ButtonRemoveFriend; // Button
            public FieldInfo ButtonBlockFriend;  // Button
            public FieldInfo ButtonChallengeFriend; // Button
            public FieldInfo ButtonCancel;       // InviteOutgoingTile: _buttonCancel (Button)
            public FieldInfo CallbackOpenChat;   // Action<SocialEntity>
            public PropertyInfo FriendProp;      // FriendTile: Friend (SocialEntity)
            public PropertyInfo InviteProp;      // InviteOutgoingTile: Invite
        }

        /// <summary>
        /// Get the friend's display label for screen reader announcement.
        /// Returns "name, status" (e.g., "wuternst, Online") or "name, date" for sent requests.
        /// </summary>
        public static string GetFriendLabel(GameObject element)
        {
            var tile = FindFriendTile(element);
            if (tile == null) return null;

            var cache = GetCache(tile);

            string name = ReadTMPField(tile, cache.LabelName);

            // _labelStatus and _labelDateSent are Localize components, not TMP_Text.
            // Read text from sibling/child TMP_Text component on the same GameObject.
            string status = ReadLocalizeField(tile, cache.LabelStatus);
            if (string.IsNullOrEmpty(status))
                status = ReadLocalizeField(tile, cache.LabelDateSent);

            if (string.IsNullOrEmpty(name)) return null;
            if (!string.IsNullOrEmpty(status))
                return $"{name}, {status}";
            return name;
        }

        /// <summary>
        /// Get available actions for left/right navigation on the current friend entry.
        /// Returns a list of (localized label, action identifier) pairs.
        /// </summary>
        public static List<(string label, string actionId)> GetFriendActions(GameObject element)
        {
            var actions = new List<(string label, string actionId)>();
            var tile = FindFriendTile(element);
            if (tile == null) return actions;

            var cache = GetCache(tile);
            string typeName = tile.GetType().Name;

            if (typeName == "FriendTile")
            {
                // Check Friend.IsOnline and Friend.HasChatHistory via reflection
                bool isOnline = false;
                bool hasChatHistory = false;
                bool challengeEnabled = false;

                var friend = GetFriendEntity(tile, cache);
                if (friend != null)
                {
                    try
                    {
                        var isOnlineProp = friend.GetType().GetProperty("IsOnline");
                        if (isOnlineProp != null)
                            isOnline = (bool)isOnlineProp.GetValue(friend);

                        var hasChatProp = friend.GetType().GetProperty("HasChatHistory");
                        if (hasChatProp != null)
                            hasChatHistory = (bool)hasChatProp.GetValue(friend);
                    }
                    catch { }
                }

                if (cache.ChallengeEnabled != null)
                {
                    try { challengeEnabled = (bool)cache.ChallengeEnabled.GetValue(tile); }
                    catch { }
                }

                // Chat available when friend is online or has chat history
                if (isOnline || hasChatHistory)
                    actions.Add((Strings.FriendActionChat, ActionChat));

                // Challenge available when challenge is enabled
                if (challengeEnabled)
                    actions.Add((Strings.FriendActionChallenge, ActionChallenge));

                actions.Add((Strings.FriendActionUnfriend, ActionUnfriend));
                actions.Add((Strings.FriendActionBlock, ActionBlock));
            }
            else if (typeName == "InviteOutgoingTile")
            {
                actions.Add((Strings.FriendActionRevoke, ActionRevoke));
            }
            else if (typeName == "InviteIncomingTile")
            {
                actions.Add((Strings.FriendActionAccept, ActionAccept));
                actions.Add((Strings.FriendActionDecline, ActionDecline));
                actions.Add((Strings.FriendActionBlock, ActionBlock));
            }
            else if (typeName.Contains("Blocked"))
            {
                actions.Add((Strings.FriendActionUnblock, ActionUnblock));
            }

            return actions;
        }

        /// <summary>
        /// Invoke an action on a social tile by clicking the appropriate button or callback.
        /// </summary>
        public static bool ActivateFriendAction(GameObject element, string actionId)
        {
            var tile = FindFriendTile(element);
            if (tile == null)
            {
                MelonLogger.Warning("[FriendInfoProvider] No tile found for action activation");
                return false;
            }

            var cache = GetCache(tile);

            try
            {
                switch (actionId)
                {
                    case ActionChat:
                        return InvokeCallback(tile, cache.CallbackOpenChat);

                    case ActionChallenge:
                        return ClickButton(tile, cache.ButtonChallengeFriend);

                    case ActionUnfriend:
                        return ClickButton(tile, cache.ButtonRemoveFriend);

                    case ActionBlock:
                        return ClickButton(tile, cache.ButtonBlockFriend);

                    case ActionRevoke:
                        return ClickButton(tile, cache.ButtonCancel);

                    case ActionAccept:
                    case ActionDecline:
                    case ActionUnblock:
                        // For tile types we haven't fully mapped yet,
                        // try finding a method by convention
                        return TryInvokeMethod(tile, actionId);

                    default:
                        MelonLogger.Warning($"[FriendInfoProvider] Unknown action: {actionId}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[FriendInfoProvider] Error activating {actionId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find a social tile component (FriendTile, InviteOutgoingTile, etc.) on or above the given element.
        /// </summary>
        public static Component FindFriendTile(GameObject element)
        {
            if (element == null) return null;

            Transform current = element.transform;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    string typeName = comp.GetType().Name;
                    foreach (var tileName in SocialTileTypeNames)
                    {
                        if (typeName == tileName)
                            return comp;
                    }
                }
                current = current.parent;
            }
            return null;
        }

        private static TileReflectionCache GetCache(Component tile)
        {
            var type = tile.GetType();
            if (_caches.TryGetValue(type, out var cached))
                return cached;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var cache = new TileReflectionCache
            {
                LabelName = type.GetField("_labelName", flags),
                LabelStatus = type.GetField("_labelStatus", flags),
                LabelDateSent = type.GetField("_labelDateSent", flags),
                ChallengeEnabled = type.GetField("_challengeEnabled", flags),
                ButtonRemoveFriend = type.GetField("_buttonRemoveFriend", flags),
                ButtonBlockFriend = type.GetField("_buttonBlockFriend", flags),
                ButtonChallengeFriend = type.GetField("_buttonChallengeFriend", flags),
                ButtonCancel = type.GetField("_buttonCancel", flags),
                CallbackOpenChat = type.GetField("Callback_OpenChat", flags),
                FriendProp = type.GetProperty("Friend", flags),
                InviteProp = type.GetProperty("Invite", flags)
            };

            _caches[type] = cache;
            return cache;
        }

        private static string ReadTMPField(Component tile, FieldInfo field)
        {
            if (field == null) return null;

            try
            {
                var tmpText = field.GetValue(tile) as TMP_Text;
                if (tmpText != null)
                {
                    string text = tmpText.text;
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }
            }
            catch
            {
                // Ignore
            }
            return null;
        }

        /// <summary>
        /// Read text from a Localize component field.
        /// Localize is not TMP_Text - it sets text on a sibling/child TMP_Text on the same GameObject.
        /// We get the Localize component's GameObject and find TMP_Text there.
        /// </summary>
        private static string ReadLocalizeField(Component tile, FieldInfo field)
        {
            if (field == null) return null;

            try
            {
                var localize = field.GetValue(tile) as Component;
                if (localize == null) return null;

                // The Localize component sits on a GameObject that also has (or has a child with) TMP_Text
                var tmpText = localize.GetComponent<TMP_Text>();
                if (tmpText == null)
                    tmpText = localize.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    string text = tmpText.text;
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }
            }
            catch
            {
                // Ignore
            }
            return null;
        }

        private static bool ClickButton(Component tile, FieldInfo buttonField)
        {
            if (buttonField == null)
            {
                MelonLogger.Warning($"[FriendInfoProvider] Button field not found on {tile.GetType().Name}");
                return false;
            }

            var button = buttonField.GetValue(tile) as Button;
            if (button == null)
            {
                MelonLogger.Warning($"[FriendInfoProvider] Button value is null: {buttonField.Name}");
                return false;
            }

            button.onClick?.Invoke();
            MelonLogger.Msg($"[FriendInfoProvider] Clicked button: {buttonField.Name}");
            return true;
        }

        /// <summary>
        /// Invoke a callback that takes SocialEntity or Invite as parameter.
        /// Reads the appropriate property (Friend or Invite) from the tile to get the argument.
        /// </summary>
        private static bool InvokeCallback(Component tile, FieldInfo callbackField)
        {
            if (callbackField == null)
            {
                MelonLogger.Warning($"[FriendInfoProvider] Callback field not found on {tile.GetType().Name}");
                return false;
            }

            var callback = callbackField.GetValue(tile);
            if (callback == null)
            {
                MelonLogger.Warning($"[FriendInfoProvider] Callback value is null: {callbackField.Name}");
                return false;
            }

            var cache = GetCache(tile);

            // Callbacks are typed (Action<SocialEntity> or Action<Invite>).
            // Get the entity to pass as parameter.
            var invokeMethod = callback.GetType().GetMethod("Invoke");
            if (invokeMethod != null)
            {
                var parameters = invokeMethod.GetParameters();
                if (parameters.Length == 1)
                {
                    // Need to pass the entity (Friend or Invite property)
                    object entity = GetFriendEntity(tile, cache)
                                 ?? GetInviteEntity(tile, cache);
                    if (entity != null)
                    {
                        invokeMethod.Invoke(callback, new[] { entity });
                        MelonLogger.Msg($"[FriendInfoProvider] Invoked callback: {callbackField.Name} with entity");
                        return true;
                    }
                    MelonLogger.Warning($"[FriendInfoProvider] No entity found for callback parameter: {callbackField.Name}");
                    return false;
                }
                else
                {
                    // Parameterless callback
                    invokeMethod.Invoke(callback, null);
                    MelonLogger.Msg($"[FriendInfoProvider] Invoked callback: {callbackField.Name}");
                    return true;
                }
            }

            MelonLogger.Warning($"[FriendInfoProvider] Cannot invoke callback: {callbackField.Name} (type: {callback.GetType().Name})");
            return false;
        }

        /// <summary>
        /// Get the SocialEntity from the Friend property of a FriendTile.
        /// </summary>
        private static object GetFriendEntity(Component tile, TileReflectionCache cache)
        {
            if (cache.FriendProp == null) return null;
            try { return cache.FriendProp.GetValue(tile); }
            catch { return null; }
        }

        /// <summary>
        /// Get the Invite from the Invite property of an InviteOutgoingTile.
        /// </summary>
        private static object GetInviteEntity(Component tile, TileReflectionCache cache)
        {
            if (cache.InviteProp == null) return null;
            try { return cache.InviteProp.GetValue(tile); }
            catch { return null; }
        }

        /// <summary>
        /// Try to invoke a method on the tile by searching common naming patterns.
        /// Fallback for tile types we haven't fully mapped.
        /// </summary>
        private static bool TryInvokeMethod(Component tile, string actionId)
        {
            // Try common button field names based on action
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            string[] fieldPatterns;

            switch (actionId)
            {
                case ActionAccept:
                    fieldPatterns = new[] { "_buttonAccept", "_buttonAcceptInvite" };
                    break;
                case ActionDecline:
                    fieldPatterns = new[] { "_buttonDecline", "_buttonDeclineInvite", "_buttonReject" };
                    break;
                case ActionUnblock:
                    fieldPatterns = new[] { "_buttonUnblock", "_buttonRemoveBlock" };
                    break;
                default:
                    return false;
            }

            foreach (var pattern in fieldPatterns)
            {
                var field = tile.GetType().GetField(pattern, flags);
                if (field != null)
                    return ClickButton(tile, field);
            }

            // Try callback fields
            var callbackField = tile.GetType().GetField($"Callback_{actionId}", flags)
                             ?? tile.GetType().GetField($"Callback_{char.ToUpper(actionId[0])}{actionId.Substring(1)}", flags);
            if (callbackField != null)
                return InvokeCallback(tile, callbackField);

            MelonLogger.Warning($"[FriendInfoProvider] No button/callback found for action '{actionId}' on {tile.GetType().Name}");
            return false;
        }
    }
}
