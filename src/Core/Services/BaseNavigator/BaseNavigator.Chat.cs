using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Models;
using System;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    public partial class BaseNavigator
    {
        #region Chat (F4)

        private static MethodInfo _showChatWindowMethod;
        private static bool _showChatWindowLookupDone;

        /// <summary>
        /// Open the chat window and switch to ChatNavigator.
        /// Called by F4 from any navigator (except GeneralMenuNavigator which uses F4 for friends panel).
        /// </summary>
        protected void OpenChat()
        {
            try
            {
                var socialPanel = GameObject.Find("SocialUI_V2_Desktop_16x9(Clone)");
                if (socialPanel == null)
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                    return;
                }

                MonoBehaviour socialUI = null;
                foreach (var comp in socialPanel.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == T.SocialUI)
                    {
                        socialUI = comp;
                        break;
                    }
                }
                if (socialUI == null)
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                    return;
                }

                // Cache ShowChatWindow method
                if (!_showChatWindowLookupDone)
                {
                    _showChatWindowLookupDone = true;
                    _showChatWindowMethod = socialUI.GetType().GetMethod("ShowChatWindow", PublicInstance);
                }

                if (_showChatWindowMethod == null)
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                    return;
                }

                // Restore SocialUI elements before opening chat (DuelNavigator deactivates them)
                var duelNavForRestore = NavigatorManager.Instance?.GetNavigator<DuelNavigator>();
                duelNavForRestore?.RestoreSocialUIBeforeChat();

                // ShowChatWindow(SocialEntity chatFriend = null) - pass null to open last conversation
                _showChatWindowMethod.Invoke(socialUI, new object[] { null });

                // Request ChatNavigator activation
                bool activated = NavigatorManager.Instance?.RequestActivation("Chat") == true;
                if (activated)
                {
                    // Mark DuelNavigator so it shows "Returned to duel" instead of full announcement
                    duelNavForRestore?.MarkPreemptedForChat();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BaseNavigator] OpenChat failed: {ex.Message}");
                _announcer.AnnounceInterrupt(Strings.ChatUnavailable);
            }
        }

        #endregion
    }
}
