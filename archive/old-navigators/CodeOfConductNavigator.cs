using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System.Collections.Generic;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for terms/consent screens with multiple checkboxes.
    /// Adds custom "C" key to read scrollable content.
    /// </summary>
    public class CodeOfConductNavigator : BaseNavigator
    {
        public override string NavigatorId => "CodeOfConduct";
        public override string ScreenName => "Terms screen";
        public override int Priority => 50; // Medium priority

        public CodeOfConductNavigator(IAnnouncementService announcer) : base(announcer) { }

        protected override bool DetectScreen()
        {
            // Skip if other screens are present
            if (GameObject.Find("Panel - WelcomeGate_Desktop_16x9(Clone)") != null) return false;
            if (GameObject.Find("Panel - Log In_Desktop_16x9(Clone)") != null) return false;

            // Skip if Settings menu is open - GeneralMenuNavigator handles Settings submenus
            if (IsSettingsMenuOpen()) return false;

            // Skip if this is a deck selection screen (Play menu)
            if (IsDeckSelectionScreen()) return false;

            // Detect by presence of multiple toggles
            var toggles = FindValidToggles();
            return toggles.Count >= 2;
        }

        /// <summary>
        /// Check if this is a deck selection screen (Play menu with deck folders)
        /// </summary>
        private bool IsDeckSelectionScreen()
        {
            // Check for deck folder elements - these are in the Play menu, not terms screen
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>())
            {
                if (obj == null || !obj.activeInHierarchy) continue;
                string name = obj.name;
                if (name.Contains("DeckFolder") || name.Contains("Deck_Folder"))
                {
                    MelonLogger.Msg($"[{NavigatorId}] Skipping - deck selection screen detected: {name}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if the Settings menu is currently open
        /// </summary>
        private bool IsSettingsMenuOpen()
        {
            // Check for Settings content panel
            var settingsContent = GameObject.Find("Content - MainMenu");
            if (settingsContent != null && settingsContent.activeInHierarchy)
                return true;

            // Check for SettingsMenu controller
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "SettingsMenu")
                {
                    // Check IsOpen property
                    var isOpenProp = mb.GetType().GetProperty("IsOpen",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (isOpenProp != null)
                    {
                        try
                        {
                            if ((bool)isOpenProp.GetValue(mb))
                                return true;
                        }
                        catch { }
                    }
                }
            }

            return false;
        }

        protected override void DiscoverElements()
        {
            var toggles = FindValidToggles();
            int toggleNum = 1;

            foreach (var toggle in toggles)
            {
                string label = FindToggleLabel(toggle, toggleNum);
                AddElement(toggle.gameObject, label);
                toggleNum++;
            }

            // Find accept/continue button
            foreach (var button in GameObject.FindObjectsOfType<Button>())
            {
                if (!button.gameObject.activeInHierarchy || !button.interactable) continue;

                string name = button.gameObject.name.ToLower();
                if (name.Contains("scroll") || name.Contains("help")) continue;

                string label = GetButtonText(button.gameObject, "Continue");
                AddElement(button.gameObject, $"{label}, button");
            }
        }

        protected override string GetActivationAnnouncement()
        {
            int toggleCount = 0;
            foreach (var element in _elements)
            {
                if (element.GameObject?.GetComponent<Toggle>() != null)
                    toggleCount++;
            }
            return $"{ScreenName}. {Models.Strings.NavigateWithArrows}. Press C to read terms. {toggleCount} checkboxes.";
        }

        protected override bool HandleCustomInput()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                ReadScrollableContent();
                return true;
            }
            return false;
        }

        private List<Toggle> FindValidToggles()
        {
            var result = new List<Toggle>();
            foreach (var toggle in GameObject.FindObjectsOfType<Toggle>())
            {
                if (!toggle.gameObject.activeInHierarchy || !toggle.interactable) continue;

                string parentName = toggle.transform.parent?.name ?? "";
                if (parentName.Contains("Dropdown") || parentName.Contains("Item")) continue;

                result.Add(toggle);
            }
            return result;
        }

        private string FindToggleLabel(Toggle toggle, int index)
        {
            var parent = toggle.transform.parent;
            MelonLogger.Msg($"[{NavigatorId}] Toggle parent: {parent?.name ?? "none"}");

            // Strategy 1: Look for sibling text
            if (parent != null)
            {
                foreach (Transform sibling in parent)
                {
                    if (sibling == toggle.transform) continue;

                    var tmpText = sibling.GetComponent<TMPro.TMP_Text>();
                    if (tmpText != null && !string.IsNullOrWhiteSpace(tmpText.text))
                    {
                        return TruncateLabel(tmpText.text);
                    }
                }
            }

            // Strategy 2: Look in children of toggle
            var childText = toggle.GetComponentInChildren<TMPro.TMP_Text>();
            if (childText != null && !string.IsNullOrWhiteSpace(childText.text))
            {
                return TruncateLabel(childText.text);
            }

            // Strategy 3: Look in grandparent's children
            var grandparent = parent?.parent;
            if (grandparent != null)
            {
                foreach (Transform uncle in grandparent)
                {
                    var tmpText = uncle.GetComponent<TMPro.TMP_Text>();
                    if (tmpText != null && !string.IsNullOrWhiteSpace(tmpText.text))
                    {
                        return TruncateLabel(tmpText.text);
                    }

                    var nestedText = uncle.GetComponentInChildren<TMPro.TMP_Text>();
                    if (nestedText != null && !string.IsNullOrWhiteSpace(nestedText.text))
                    {
                        string text = nestedText.text.Trim();
                        if (text.Length > 3)
                            return TruncateLabel(text);
                    }
                }
            }

            return $"Accept terms {index}";
        }

        private void ReadScrollableContent()
        {
            var scrollRect = GameObject.FindObjectOfType<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
            {
                var contentParts = new List<string>();

                foreach (var text in scrollRect.content.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    if (!text.gameObject.activeInHierarchy) continue;
                    if (string.IsNullOrWhiteSpace(text.text)) continue;

                    string cleaned = text.text.Trim();
                    if (cleaned.Length > 5)
                    {
                        contentParts.Add(cleaned);
                    }
                }

                if (contentParts.Count > 0)
                {
                    string fullContent = string.Join(". ", contentParts);
                    if (fullContent.Length > 500)
                        fullContent = fullContent.Substring(0, 497) + "...";

                    _announcer.AnnounceInterrupt(fullContent);
                    return;
                }
            }

            // Fallback: find any long TMP_Text
            foreach (var text in GameObject.FindObjectsOfType<TMPro.TMP_Text>())
            {
                if (!text.gameObject.activeInHierarchy) continue;
                string content = text.text?.Trim();
                if (content != null && content.Length > 100)
                {
                    if (content.Length > 500)
                        content = content.Substring(0, 497) + "...";
                    _announcer.AnnounceInterrupt(content);
                    return;
                }
            }

            _announcer.Announce(Strings.NoTermsContentFound, AnnouncementPriority.Normal);
        }
    }
}
