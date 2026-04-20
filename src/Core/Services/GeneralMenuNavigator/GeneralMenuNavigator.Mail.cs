using UnityEngine;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class GeneralMenuNavigator
    {
        // Mail detail view tracking
        private bool _isInMailDetailView;

        // Mail content field navigation
        private UITextExtractor.MailContentParts _mailContentParts;

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

            // Find ContentControllerPlayerInbox and invoke CloseCurrentLetter()
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel != null)
            {
                foreach (var mb in mailboxPanel.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "ContentControllerPlayerInbox")
                    {
                        var method = mb.GetType().GetMethod("CloseCurrentLetter",
                            AllInstanceFlags);

                        if (method != null)
                        {
                            try
                            {
                                Log.Nav(NavigatorId, $"Invoking ContentControllerPlayerInbox.CloseCurrentLetter()");
                                method.Invoke(mb, null);
                                _isInMailDetailView = false;
                                ResetMailFieldNavigation();
                                _announcer.Announce(Models.Strings.BackToMailList, Models.AnnouncementPriority.High);
                                TriggerRescan();
                                return true;
                            }
                            catch (System.Exception ex)
                            {
                                Log.Nav(NavigatorId, $"CloseCurrentLetter() error: {ex.Message}");
                            }
                        }
                        break;
                    }
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
