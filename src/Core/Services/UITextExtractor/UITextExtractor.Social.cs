using UnityEngine;
using TMPro;

namespace AccessibleArena.Core.Services
{
    public static partial class UITextExtractor
    {
        /// <summary>
        /// Represents the structured parts of a mail message.
        /// </summary>
        public struct MailContentParts
        {
            public string Title;
            public string Date;
            public string Body;
            public GameObject TitleObject;
            public GameObject DateObject;
            public GameObject BodyObject;
            public bool HasContent => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Date) || !string.IsNullOrEmpty(Body);
        }

        /// <summary>
        /// Tries to get a label from parent object names for FriendsWidget elements.
        /// The friends panel uses Backer_Hitbox children inside parent containers like Button_AddFriend.
        /// Pattern: Button_AddFriend/Backer_Hitbox -> "Add Friend"
        /// </summary>
        private static string TryGetFriendsWidgetLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if we're inside FriendsWidget
            Transform current = gameObject.transform;
            bool insideFriendsWidget = false;
            int maxLevels = 10;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("FriendsWidget"))
                {
                    insideFriendsWidget = true;
                    break;
                }
                current = current.parent;
                maxLevels--;
            }

            if (!insideFriendsWidget) return null;

            // Get the immediate parent name and try to extract a label from it
            var parent = gameObject.transform.parent;
            if (parent == null) return null;

            string parentName = parent.name;

            // Prefer explicit localization keys for known FriendsWidget action buttons.
            if (parentName.Contains("AddFriend"))
                return LocaleManager.Instance?.Get("GroupFriendsPanelAddFriend") ?? "Add Friend";
            if (parentName.Contains("AddChallenge") || parentName.Contains("Challenge"))
                return LocaleManager.Instance?.Get("GroupFriendsPanelChallenge") ?? "Challenge";

            // Pattern: Button_Something -> "Something"
            if (parentName.StartsWith("Button_"))
            {
                string label = parentName.Substring(7); // Remove "Button_"
                // Clean up: AddFriend -> "Add Friend"
                label = CleanObjectName(label);
                return label;
            }

            // Pattern: Something_Button -> "Something"
            if (parentName.EndsWith("_Button"))
            {
                string label = parentName.Substring(0, parentName.Length - 7); // Remove "_Button"
                label = CleanObjectName(label);
                return label;
            }

            // For other patterns, check if parent has meaningful TMP_Text children
            // that might not be direct children of our element
            var parentTmpText = parent.GetComponentInChildren<TMP_Text>();
            if (parentTmpText != null)
            {
                string text = CleanText(parentTmpText.text);
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                {
                    return text;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to get the mail title from a mailbox item, skipping the "Neu/New" badge.
        /// Mailbox items have structure: Mailbox_Blade_ListItem_Base/Button with children containing
        /// the title text and a "Neu" badge for unread items.
        /// </summary>
        private static string TryGetMailboxItemTitle(GameObject gameObject)
        {
            if (gameObject == null) return null;

            // Check if we're inside a Mailbox context
            string path = GetParentPath(gameObject);
            if (!path.Contains("Mailbox")) return null;

            // Walk up to find the Mailbox_Blade_ListItem_Base container
            Transform current = gameObject.transform;
            Transform listItemContainer = null;
            int maxLevels = 5;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("Mailbox_Blade_ListItem"))
                {
                    listItemContainer = current;
                    break;
                }
                current = current.parent;
                maxLevels--;
            }

            if (listItemContainer == null) return null;

            // Get all TMP_Text children and find the title (skip "Neu"/"New" badges)
            var textComponents = listItemContainer.GetComponentsInChildren<TMP_Text>(true);
            string bestTitle = null;
            int bestLength = 0;
            string badgeText = null;

            foreach (var tmp in textComponents)
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                string text = CleanText(tmp.text);
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Detect badge/indicator texts but remember them for prefix
                string lower = text.ToLower();
                if (lower == "neu" || lower == "new" || lower == "unread")
                {
                    badgeText = text;
                    continue;
                }
                if (lower == "gelesen" || lower == "read" || text.Length <= 3)
                    continue;

                // Prefer longer text (title is usually longer than other labels)
                if (text.Length > bestLength)
                {
                    bestTitle = text;
                    bestLength = text.Length;
                }
            }

            // Prepend unread badge if present so the user knows the mail is new
            if (bestTitle != null && badgeText != null)
                return $"{badgeText}, {bestTitle}";

            return bestTitle;
        }

        /// <summary>
        /// Extracts structured mail content parts (title, date, body) from an opened mail message.
        /// </summary>
        public static MailContentParts GetMailContentParts()
        {
            var parts = new MailContentParts();

            // Find the mailbox content view
            var mailboxPanel = GameObject.Find("ContentController - Mailbox_Base(Clone)");
            if (mailboxPanel == null)
                return parts;

            // Find the letter content container
            var letterBase = FindChildRecursive(mailboxPanel.transform, "Mailbox_Letter_Base");
            if (letterBase == null)
            {
                // Try alternate path
                var contentView = mailboxPanel.transform.Find("SafeArea/ViewSection/Mailbox_ContentView");
                if (contentView != null)
                    letterBase = FindChildRecursive(contentView, "CONTENT_Mailbox_Letter");
            }

            if (letterBase == null)
                return parts;

            // Get all TMP_Text components
            var texts = letterBase.GetComponentsInChildren<TMP_Text>(true);

            foreach (var tmpText in texts)
            {
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy)
                    continue;

                string text = CleanText(tmpText.text);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                string objName = tmpText.gameObject.name.ToLowerInvariant();
                string parentName = tmpText.transform.parent?.name.ToLowerInvariant() ?? "";
                string grandparentName = tmpText.transform.parent?.parent?.name.ToLowerInvariant() ?? "";

                // Skip button labels
                if (IsInsideButtonContainer(tmpText.transform))
                    continue;

                // Identify by object/parent naming patterns
                if (objName.Contains("title") || parentName.Contains("title") ||
                    objName.Contains("header") || parentName.Contains("header") ||
                    objName.Contains("subject"))
                {
                    if (string.IsNullOrEmpty(parts.Title))
                    {
                        parts.Title = text;
                        parts.TitleObject = tmpText.gameObject;
                    }
                }
                else if (objName.Contains("date") || parentName.Contains("date") ||
                         objName.Contains("time") || parentName.Contains("time"))
                {
                    if (string.IsNullOrEmpty(parts.Date))
                    {
                        parts.Date = text;
                        parts.DateObject = tmpText.gameObject;
                    }
                }
                else if (objName.Contains("body") || parentName.Contains("body") ||
                         objName.Contains("content") || parentName.Contains("content") ||
                         objName.Contains("message") || parentName.Contains("message") ||
                         grandparentName.Contains("body") || grandparentName.Contains("content"))
                {
                    // Append to body if we find multiple body texts
                    if (string.IsNullOrEmpty(parts.Body))
                    {
                        parts.Body = text;
                        parts.BodyObject = tmpText.gameObject;
                    }
                    else if (!parts.Body.Contains(text))
                        parts.Body += " " + text;
                }
                else
                {
                    // If no specific match and text is substantial, treat as body
                    if (text.Length > 20 && string.IsNullOrEmpty(parts.Body))
                    {
                        parts.Body = text;
                        parts.BodyObject = tmpText.gameObject;
                    }
                    else if (text.Length > 5 && text.Length <= 50 && string.IsNullOrEmpty(parts.Title))
                    {
                        // Short text without specific container might be title
                        parts.Title = text;
                        parts.TitleObject = tmpText.gameObject;
                    }
                }
            }

            return parts;
        }

    }
}
