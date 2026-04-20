using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Utility class to extract readable text from Unity UI GameObjects.
    /// Checks various UI components in priority order to find the best text representation.
    /// </summary>
    public static partial class UITextExtractor
    {
        /// <summary>
        /// Centralized fallback labels for buttons that have no text components or tooltips.
        /// Checked just before the CleanObjectName fallback. Uses Func&lt;string&gt; because
        /// locale strings resolve at runtime via LocaleManager.Instance.
        /// </summary>
        private static readonly Dictionary<string, System.Func<string>> FallbackLabels =
            new Dictionary<string, System.Func<string>>
        {
            { "Invite Button", () => Models.Strings.ScreenInviteFriend },
            { "KickPlayer_SecondaryButton", () => Models.Strings.ChallengeKickOpponent },
            { "BlockPlayer_SecondaryButton", () => Models.Strings.ChallengeBlockOpponent },
            { "AddFriend_SecondaryButton", () => Models.Strings.ChallengeAddFriend },
        };

        // Pre-compiled regex patterns for text cleaning
        private static readonly Regex RichTextTagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Strips Unity rich text tags (e.g. &lt;color&gt;, &lt;b&gt;) from text.
        /// </summary>
        public static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return RichTextTagPattern.Replace(text, "");
        }

        /// <summary>
        /// Checks if the GameObject has actual text content (not just object name fallback).
        /// Used to distinguish elements with real labels from those with only internal names.
        /// </summary>
        public static bool HasActualText(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            // Check for input fields with content
            var tmpInputField = gameObject.GetComponent<TMP_InputField>();
            if (tmpInputField != null && !string.IsNullOrWhiteSpace(tmpInputField.text))
                return true;

            var inputField = gameObject.GetComponent<InputField>();
            if (inputField != null && !string.IsNullOrWhiteSpace(inputField.text))
                return true;

            // Check for TMP text
            var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string cleaned = CleanText(tmpText.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return true;
            }

            // Check for legacy Text
            var text = gameObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                string cleaned = CleanText(text.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the most relevant text from a UI GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to extract text from</param>
        /// <returns>The extracted text, or the GameObject name as fallback</returns>
        public static string GetText(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;

            // NavTokenController: extract token type and count for screen reader
            if (gameObject.name.Contains("NavTokenController") || gameObject.name.Contains("Nav_Token"))
            {
                string tokenLabel = TryGetNavTokenLabel(gameObject);
                if (!string.IsNullOrEmpty(tokenLabel))
                    return tokenLabel;
            }

            // Check for special label overrides (buttons with misleading game labels)
            string overrideLabel = GetLabelOverride(gameObject.name);
            if (overrideLabel != null)
                return overrideLabel;

            // Check if this is a currency display element (Gold, Gems, Wildcards)
            string currencyLabel = TryGetCurrencyLabel(gameObject);
            if (!string.IsNullOrEmpty(currencyLabel))
                return currencyLabel;

            // Check if this is a deck entry (MetaDeckView) - look for parent with input field containing deck name
            string deckName = TryGetDeckName(gameObject);
            if (!string.IsNullOrEmpty(deckName))
            {
                return deckName;
            }

            // Check if this is a booster pack element - look for CarouselBooster parent with pack name
            string boosterName = TryGetBoosterPackName(gameObject);
            if (!string.IsNullOrEmpty(boosterName))
            {
                return boosterName;
            }

            // Check if this is an objective element - extract full info including progress
            string objectiveText = TryGetObjectiveText(gameObject);
            if (!string.IsNullOrEmpty(objectiveText))
            {
                return objectiveText;
            }

            // Check if this is an NPE objective element (tutorial stages)
            string npeObjectiveText = TryGetNPEObjectiveText(gameObject);
            if (!string.IsNullOrEmpty(npeObjectiveText))
            {
                return npeObjectiveText;
            }

            // Check if this is a store item - extract label from StoreItemBase
            string storeItemLabel = TryGetStoreItemLabel(gameObject);
            if (!string.IsNullOrEmpty(storeItemLabel))
            {
                return storeItemLabel;
            }

            // Check if this is a play mode tab - extract mode from element name
            string playModeText = TryGetPlayModeTabText(gameObject);
            if (!string.IsNullOrEmpty(playModeText))
            {
                return playModeText;
            }

            // Check if this is an event tile - extract enriched label
            string eventTileLabel = TryGetEventTileLabel(gameObject);
            if (!string.IsNullOrEmpty(eventTileLabel))
            {
                return eventTileLabel;
            }

            // Check if this is a packet selection option - extract packet info
            string packetLabel = TryGetPacketLabel(gameObject);
            if (!string.IsNullOrEmpty(packetLabel))
            {
                return packetLabel;
            }

            // Check if this is a DeckManager icon button - extract function from element name
            string deckManagerButtonText = TryGetDeckManagerButtonText(gameObject);
            if (!string.IsNullOrEmpty(deckManagerButtonText))
            {
                return deckManagerButtonText;
            }

            // For Mailbox items, try to get the mail title (skip "Neu" badge) BEFORE generic text extraction.
            // GetComponentInChildren<TMP_Text> picks up the "Neu" badge first for unread items,
            // so the mailbox-specific extractor must run earlier.
            string mailboxTitle = TryGetMailboxItemTitle(gameObject);
            if (!string.IsNullOrEmpty(mailboxTitle))
            {
                return mailboxTitle;
            }

            // Check for input fields FIRST (they contain text children that we don't want to read directly)
            var tmpInputField = gameObject.GetComponent<TMP_InputField>();
            if (tmpInputField != null)
            {
                return GetInputFieldText(tmpInputField);
            }

            var inputField = gameObject.GetComponent<InputField>();
            if (inputField != null)
            {
                return GetInputFieldText(inputField);
            }

            // Try Toggle (checkbox)
            var toggle = gameObject.GetComponent<Toggle>();
            if (toggle != null)
            {
                return GetToggleText(toggle);
            }

            // Try Scrollbar
            var scrollbar = gameObject.GetComponent<Scrollbar>();
            if (scrollbar != null)
            {
                return GetScrollbarText(scrollbar);
            }

            // Try Slider
            var slider = gameObject.GetComponent<Slider>();
            if (slider != null)
            {
                return GetSliderText(slider);
            }

            // Try Dropdown
            var tmpDropdown = gameObject.GetComponent<TMP_Dropdown>();
            if (tmpDropdown != null)
            {
                return GetDropdownText(tmpDropdown);
            }

            var dropdown = gameObject.GetComponent<Dropdown>();
            if (dropdown != null)
            {
                return GetDropdownText(dropdown);
            }

            // Try TextMeshPro text
            var tmpText = gameObject.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                string cleaned = CleanText(tmpText.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }

            // Try legacy Unity UI Text
            var text = gameObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                string cleaned = CleanText(text.text);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }

            // No text found on element itself - check siblings for label text
            // This helps with UI patterns where buttons get labels from sibling elements
            string siblingText = TryGetSiblingLabel(gameObject);
            if (!string.IsNullOrEmpty(siblingText))
            {
                return siblingText;
            }

            // Try TooltipTrigger LocString as fallback for image-only buttons (e.g., Nav_Settings, Nav_Learn).
            // Check parents too because some clickable hitboxes have TooltipTrigger on a parent container.
            string tooltipText = TryGetTooltipText(gameObject);
            if (!string.IsNullOrEmpty(tooltipText))
            {
                return tooltipText;
            }

            // For FriendsWidget elements, use localized labels before object-name cleanup fallback.
            string friendsWidgetLabel = TryGetFriendsWidgetLabel(gameObject);
            if (!string.IsNullOrEmpty(friendsWidgetLabel))
            {
                return friendsWidgetLabel;
            }

            // Centralized fallback for known unlabeled buttons
            if (FallbackLabels.TryGetValue(gameObject.name, out var fallbackFunc))
                return fallbackFunc();

            // Try Localize component: reads TMP_Text.text from inactive children, or resolves
            // the locKey directly via ActiveLocProvider for icon-only buttons with Localize.
            string localizeText = TryGetLocalizeText(gameObject);
            if (!string.IsNullOrEmpty(localizeText))
                return localizeText;

            // Fallback to GameObject name (cleaned up)
            return CleanObjectName(gameObject.name);
        }

        /// <summary>
        /// Returns a label override for elements with misleading game labels.
        /// Used to provide better accessibility labels for buttons like the match end "Continue" button.
        /// </summary>
        private static string GetLabelOverride(string objectName)
        {
            // ExitMatchOverlayButton on MatchEndScene shows "View Battlefield" but actually continues to home
            if (objectName == "ExitMatchOverlayButton")
                return Strings.Continue;

            // DeckManager "NewDeckButton" shows "Enter deck name..." placeholder — override with localized "New Deck".
            if (objectName.Contains("NewDeckButton"))
                return ResolveLocKey("MainNav/DeckManager/DeckManager_Top_New") ?? Strings.NewDeck;

            // Deck builder title panel opens DeckDetailsPopup (name, format, cosmetics) — not a "new deck" action.
            if (objectName == "TitlePanel_MainDeck")
                return Strings.ChangeDeckDetails;

            // Image-only back buttons (e.g., DeckDetailsPopup header) — provide localized label.
            if (objectName == "BackButton")
                return Strings.Back;

            return null;
        }

        /// <summary>
        /// Tries to get a label from sibling elements when the element itself has no text.
        /// This handles UI patterns where a button's label comes from a sibling element.
        /// Example: Color Challenge buttons have an "INFO" sibling with the color name.
        /// </summary>
        private static string TryGetSiblingLabel(GameObject gameObject)
        {
            if (gameObject == null) return null;

            var parent = gameObject.transform.parent;
            if (parent == null) return null;

            // Look through siblings for text content
            foreach (Transform sibling in parent)
            {
                // Skip self
                if (sibling.gameObject == gameObject) continue;

                // Skip decorative/structural elements
                string sibName = sibling.name.ToUpper();
                if (sibName.Contains("MASK") ||
                    sibName.Contains("SHADOW") ||
                    sibName.Contains("DIVIDER") ||
                    sibName.Contains("BACKGROUND") ||
                    sibName.Contains("INDICATION"))
                {
                    continue;
                }

                // Try to get text from this sibling
                var tmpText = sibling.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    string cleaned = CleanText(tmpText.text);
                    // Must be meaningful text (not just single char or placeholder)
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 1)
                    {
                        return cleaned;
                    }
                }

                var legacyText = sibling.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    string cleaned = CleanText(legacyText.text);
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 1)
                    {
                        return cleaned;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to extract label text from a TooltipTrigger's LocString field.
        /// Used as a last-resort fallback for image-only buttons (e.g., Nav_Settings, Nav_Learn)
        /// that have no text content but have a localized tooltip.
        /// </summary>
        private static string TryGetTooltipText(GameObject gameObject)
        {
            if (gameObject == null) return null;

            Transform current = gameObject.transform;
            int maxLevels = 4; // self + up to 3 parents

            while (current != null && maxLevels > 0)
            {
                string text = TryGetTooltipTextFromObject(current.gameObject);
                if (!string.IsNullOrEmpty(text))
                    return text;

                current = current.parent;
                maxLevels--;
            }

            return null;
        }

        private static string TryGetTooltipTextFromObject(GameObject gameObject)
        {
            if (gameObject == null) return null;

            foreach (var comp in gameObject.GetComponents<MonoBehaviour>())
            {
                if (comp == null || comp.GetType().Name != "TooltipTrigger") continue;

                var locStringField = comp.GetType().GetField("LocString",
                    PublicInstance);
                if (locStringField == null) continue;

                var locString = locStringField.GetValue(comp);
                if (locString == null) continue;

                string text = locString.ToString();
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 1 && text.Length < 60)
                    return text;
            }

            return null;
        }

        private static string GetParentPath(GameObject gameObject)
        {
            if (gameObject == null) return "";
            var sb = new System.Text.StringBuilder();
            Transform current = gameObject.transform;
            while (current != null)
            {
                if (sb.Length > 0) sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the type of UI element for additional context.
        /// </summary>
        public static string GetElementType(GameObject gameObject)
        {
            if (gameObject == null)
                return "unknown";

            // Check for card first (before button, since cards may have button-like components)
            if (CardDetector.IsCard(gameObject))
                return "card";

            if (gameObject.GetComponent<Button>() != null)
                return "button";

            if (gameObject.GetComponent<TMP_InputField>() != null || gameObject.GetComponent<InputField>() != null)
                return "text field";

            if (gameObject.GetComponent<Toggle>() != null)
                return "checkbox";

            if (gameObject.GetComponent<TMP_Dropdown>() != null || gameObject.GetComponent<Dropdown>() != null)
                return "dropdown";

            if (gameObject.GetComponent<Slider>() != null)
                return "slider";

            if (gameObject.GetComponent<Scrollbar>() != null)
                return "scrollbar";

            if (gameObject.GetComponent<Selectable>() != null)
                return "control";

            return "item";
        }

        /// <summary>
        /// Cleans text by removing rich text tags, zero-width spaces, and normalizing whitespace.
        /// </summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove zero-width space (common in MTGA empty fields)
            text = text.Replace("\u200B", "");

            // Remove rich text tags like <color>, <b>, etc.
            text = RichTextTagPattern.Replace(text, "");

            // Normalize whitespace
            text = WhitespacePattern.Replace(text, " ");

            return text.Trim();
        }

        /// <summary>
        /// Finds a child transform by name recursively.
        /// </summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(name))
                    return child;

                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Checks if a transform is inside a button container.
        /// </summary>
        private static bool IsInsideButtonContainer(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                string name = current.name.ToLowerInvariant();
                if (name.Contains("button") || name.Contains("btn"))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static string CleanObjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "unknown";

            // Remove common Unity prefixes/suffixes
            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", " button");
            name = name.Replace("Btn", " button");
            name = name.Replace("Toggle", " checkbox");
            name = name.Replace("InputField", " text field");
            name = name.Replace("Dropdown", " dropdown");

            // Convert PascalCase/camelCase to spaces
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

            // Remove underscores
            name = name.Replace("_", " ");

            // Normalize whitespace
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");

            return name.Trim().ToLower();
        }

    }
}
