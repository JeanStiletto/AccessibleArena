using UnityEngine;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    public partial class GeneralMenuNavigator
    {
        // Mail detail view tracking
        private bool _isInMailDetailView;

        // Mail content field navigation
        private UITextExtractor.MailContentParts _mailContentParts;

        // Letter list change polling (see CheckForMailboxListChanges)
        private const float MailboxListCheckInterval = 0.5f;
        private const string PlayerInboxLetterList = "PlayerInboxLetterList";
        private float _mailboxListCheckTimer;
        private string _lastMailboxListSig;

        /// <summary>
        /// Handler for PanelStatePatch.OnMailLetterSelected - fires when a mail is opened in the mailbox.
        /// </summary>
        private void OnMailLetterSelected(Guid letterId, string title, string body, bool hasAttachments, bool isClaimed)
        {
            if (!_isActive) return;

            Log.Nav(NavigatorId, $"Mail letter selected: {letterId}");

            // Track that we're now in the mail detail view
            _isInMailDetailView = true;

            // Trigger rescan to discover mail content fields and buttons
            // The mail content fields will be added during element discovery
            TriggerRescan();
        }

        /// <summary>
        /// Add mail content fields (title, date, body) as navigable elements.
        /// These are actual TMP_Text GameObjects from the mail UI, inserted before buttons.
        /// </summary>
        private void AddMailContentFieldsAsElements()
        {
            // Extract mail content from UI (includes actual GameObjects)
            _mailContentParts = UITextExtractor.GetMailContentParts();

            Log.Nav(NavigatorId, $"Mail content - Title: '{_mailContentParts.Title}' (obj: {_mailContentParts.TitleObject?.name}), Date: '{_mailContentParts.Date}', Body length: {_mailContentParts.Body?.Length ?? 0}");

            if (!_mailContentParts.HasContent)
                return;

            // Create list of mail field elements (to be inserted at the beginning)
            var mailFieldElements = new List<NavigableElement>();

            if (_mailContentParts.TitleObject != null && !string.IsNullOrEmpty(_mailContentParts.Title))
            {
                mailFieldElements.Add(new NavigableElement
                {
                    GameObject = _mailContentParts.TitleObject,
                    Label = $"Title: {_mailContentParts.Title}"
                });
            }

            if (_mailContentParts.DateObject != null && !string.IsNullOrEmpty(_mailContentParts.Date))
            {
                mailFieldElements.Add(new NavigableElement
                {
                    GameObject = _mailContentParts.DateObject,
                    Label = $"Date: {_mailContentParts.Date}"
                });
            }

            if (_mailContentParts.BodyObject != null && !string.IsNullOrEmpty(_mailContentParts.Body))
            {
                mailFieldElements.Add(new NavigableElement
                {
                    GameObject = _mailContentParts.BodyObject,
                    Label = $"Body: {_mailContentParts.Body}"
                });
            }

            // Insert mail fields at the beginning
            if (mailFieldElements.Count > 0)
            {
                _elements.InsertRange(0, mailFieldElements);
                Log.Nav(NavigatorId, $"Added {mailFieldElements.Count} mail content fields as navigable elements");
            }
        }

        /// <summary>
        /// Add the notice an empty letter list shows as a readable element. Since 2026.63.10 the
        /// inbox has a Store Messages tab whose empty list explains why it is empty ("Messages
        /// from your Favorited Stores will appear here.", or that store messages are switched
        /// off) above a Store Locator / Enable Store Messages button; the Arena tab says
        /// "No Messages.". Only the button was reachable, so the reason was never spoken.
        /// Each notice goes right before the first element of its own list, so it is heard
        /// ahead of the button it explains.
        /// </summary>
        private void AddMailboxListNotices()
        {
            foreach (var list in GetActiveLetterLists())
            {
                if (ListHasLetters(list)) continue;

                var notices = new List<NavigableElement>();
                foreach (var tmp in list.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (tmp == null || IsInsideSelectable(tmp.transform, list.transform)) continue;
                    string text = UITextExtractor.CleanText(tmp.text);
                    if (string.IsNullOrEmpty(text)) continue;
                    notices.Add(new NavigableElement { GameObject = tmp.gameObject, Label = text });
                }
                if (notices.Count == 0) continue;

                int insertAt = _elements.FindIndex(e => e.GameObject != null && e.GameObject.transform.IsChildOf(list.transform));
                if (insertAt < 0) insertAt = _elements.Count;
                _elements.InsertRange(insertAt, notices);
                Log.Nav(NavigatorId, $"Added {notices.Count} empty-list notice(s) for {list.name}");
            }
        }

        /// <summary>
        /// The inbox fills its letter lists asynchronously (inbox request, tab switch, store
        /// messages being switched on) and fires no panel event, so poll a signature of the
        /// visible lists while browsing them and rescan when it changes.
        /// </summary>
        private void CheckForMailboxListChanges()
        {
            _mailboxListCheckTimer -= Time.deltaTime;
            if (_mailboxListCheckTimer > 0) return;
            _mailboxListCheckTimer = MailboxListCheckInterval;

            bool browsingList = !_isInMailDetailView && _screenDetector.IsMailboxOpen();
            if (!browsingList)
            {
                _lastMailboxListSig = null;
                return;
            }
            if (_rescanDelay > 0) return;

            string sig = GetMailboxListSignature();
            if (_lastMailboxListSig == null)
            {
                // First poll after opening: the open itself already triggered a scan.
                _lastMailboxListSig = sig;
                return;
            }
            if (sig != _lastMailboxListSig)
            {
                Log.Nav(NavigatorId, $"Mailbox list changed, triggering rescan");
                _lastMailboxListSig = sig;
                TriggerRescan();
            }
        }

        private string GetMailboxListSignature()
        {
            var parts = new List<string>();
            foreach (var list in GetActiveLetterLists())
            {
                int active = 0;
                string first = null;
                foreach (Transform child in list.transform)
                {
                    if (!child.gameObject.activeSelf) continue;
                    active++;
                    if (first == null) first = child.name;
                }
                parts.Add($"{list.transform.parent?.parent?.name}:{active}:{first}");
            }
            return string.Join("|", parts);
        }

        private static List<MonoBehaviour> GetActiveLetterLists()
        {
            var lists = new List<MonoBehaviour>();
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel == null) return lists;
            foreach (var mb in mailboxPanel.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb != null && mb.GetType().Name == PlayerInboxLetterList)
                    lists.Add(mb);
            }
            return lists;
        }

        private static bool ListHasLetters(MonoBehaviour list)
        {
            foreach (var mb in list.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb != null && mb.GetType().Name == "PlayerInboxBladeItemDisplay")
                    return true;
            }
            return false;
        }

        private static bool IsInsideSelectable(Transform t, Transform stopAt)
        {
            for (var current = t; current != null && current != stopAt; current = current.parent)
            {
                if (current.GetComponent<UnityEngine.UI.Selectable>() != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Reset mail field navigation state.
        /// </summary>
        private void ResetMailFieldNavigation()
        {
            _mailContentParts = default;
        }

        /// <summary>
        /// Close the Mailbox panel or close current mail detail view.
        /// If viewing a mail, closes the mail and returns to the mail list.
        /// If viewing the mail list, closes the entire mailbox panel.
        /// </summary>
        private bool CloseMailbox()
        {
            // If we're in the mail detail view, close just the mail (not the whole mailbox)
            if (_isInMailDetailView)
            {
                return CloseMailDetailView();
            }

            Log.Nav(NavigatorId, $"Closing Mailbox panel");

            // Find NavBarController and invoke HideInboxIfActive()
            var navBar = GameObject.Find("NavBar_Desktop_16x9(Clone)");
            if (navBar != null)
            {
                foreach (var mb in navBar.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "NavBarController")
                    {
                        var method = mb.GetType().GetMethod("HideInboxIfActive",
                            AllInstanceFlags);

                        if (method != null)
                        {
                            try
                            {
                                Log.Nav(NavigatorId, $"Invoking NavBarController.HideInboxIfActive()");
                                method.Invoke(mb, null);
                                _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                                TriggerRescan();
                                return true;
                            }
                            catch (System.Exception ex)
                            {
                                Log.Nav(NavigatorId, $"HideInboxIfActive() error: {ex.Message}");
                            }
                        }
                        break;
                    }
                }
            }

            // Fallback: try to find and click dismiss button in mailbox panel
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel != null)
            {
                var closeButton = FindCloseButtonInPanel(mailboxPanel);
                if (closeButton != null)
                {
                    _announcer.Announce(Models.Strings.NavigatingBack, Models.AnnouncementPriority.High);
                    UIActivator.Activate(closeButton);
                    TriggerRescan();
                    return true;
                }
            }

            return TryGenericBackButton();
        }

        /// <summary>
        /// Close the current mail detail view and return to the mail list.
        /// </summary>
        private bool CloseMailDetailView()
        {
            Log.Nav(NavigatorId, $"Closing mail detail view");

            // Find the active ContentControllerPlayerInbox and invoke CloseOpenLetter()
            // (2026.63: raises Message_InboxLetterClosed, which hides the letter view).
            var inboxType = FindType(T.ContentControllerPlayerInboxFQ);
            var inbox = inboxType != null ? UnityEngine.Object.FindAnyObjectByType(inboxType) : null;
            var method = inbox?.GetType().GetMethod("CloseOpenLetter", PublicInstance);
            if (method != null)
            {
                try
                {
                    Log.Nav(NavigatorId, $"Invoking ContentControllerPlayerInbox.CloseOpenLetter()");
                    method.Invoke(inbox, null);
                    _isInMailDetailView = false;
                    ResetMailFieldNavigation();
                    _announcer.Announce(Models.Strings.BackToMailList, Models.AnnouncementPriority.High);
                    TriggerRescan();
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log.Nav(NavigatorId, $"CloseOpenLetter() error: {ex.Message}");
                }
            }

            // Fallback: reset flag anyway and try generic back
            _isInMailDetailView = false;
            ResetMailFieldNavigation();
            return TryGenericBackButton();
        }

        /// <summary>
        /// Check if element is a button in mail content that has no actual text content.
        /// Filters out buttons like "SecondaryButton_v2" which only show their object name.
        /// </summary>
        private bool IsMailContentButtonWithNoText(GameObject obj)
        {
            if (obj == null) return false;

            // Check if inside mail content view
            Transform current = obj.transform;
            bool inMailContent = false;
            while (current != null)
            {
                if (current.name.Contains("Mailbox_ContentView") || current.name.Contains("CONTENT_Mailbox_Letter"))
                {
                    inMailContent = true;
                    break;
                }
                current = current.parent;
            }

            if (!inMailContent)
                return false;

            // Check if this is a button
            var customButton = obj.GetComponent<MonoBehaviour>();
            bool isButton = false;
            if (customButton != null)
            {
                foreach (var comp in obj.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == "CustomButton")
                    {
                        isButton = true;
                        break;
                    }
                }
            }

            if (!isButton)
                return false;

            // Check if the button has actual text content
            bool hasActualText = UITextExtractor.HasActualText(obj);

            // If no actual text, this is a button with only its object name - filter it out
            if (!hasActualText)
                return true;

            // Also filter if the extracted text is just the object name cleaned up
            string extractedText = UITextExtractor.GetText(obj);
            string objNameCleaned = obj.name.ToLowerInvariant().Replace("_", " ").Replace("-", " ");
            string extractedCleaned = extractedText?.ToLowerInvariant().Replace($", {Models.Strings.RoleButton.ToLowerInvariant()}", "").Trim() ?? "";

            // If the extracted text matches the object name (with minor variations), filter it
            if (!string.IsNullOrEmpty(extractedCleaned) && objNameCleaned.Contains(extractedCleaned))
                return true;

            return false;
        }
    }
}
