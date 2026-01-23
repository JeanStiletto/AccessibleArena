using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Helper class for menu debugging and logging.
    /// Extracts verbose debug methods from GeneralMenuNavigator to reduce file size.
    /// </summary>
    public static class MenuDebugHelper
    {
        /// <summary>
        /// Log all available UI elements in the current scene.
        /// Useful for debugging element discovery and classification.
        /// </summary>
        /// <param name="tag">Log tag (e.g., NavigatorId)</param>
        /// <param name="sceneName">Current scene name</param>
        /// <param name="getActiveCustomButtons">Function to get active CustomButton GameObjects</param>
        public static void LogAvailableUIElements(string tag, string sceneName, Func<IEnumerable<GameObject>> getActiveCustomButtons)
        {
            MelonLogger.Msg($"[{tag}] === UI DUMP FOR {sceneName} ===");

            // Find all CustomButtons
            var customButtons = getActiveCustomButtons()
                .Select(obj => (obj, text: UITextExtractor.GetText(obj) ?? "(no text)", path: GetGameObjectPath(obj)))
                .ToList();

            MelonLogger.Msg($"[{tag}] Found {customButtons.Count} CustomButtons:");
            foreach (var (obj, text, path) in customButtons.OrderBy(x => x.path).Take(40))
            {
                LogCustomButtonDetails(tag, obj, text, path);
            }

            // Find EventTriggers
            LogEventTriggers(tag);

            // Find standard Buttons
            LogStandardButtons(tag);

            // Find CustomToggle components
            LogCustomToggles(tag);

            // Find Scrollbars
            LogScrollbars(tag);

            // Find ScrollRect components
            LogScrollRects(tag);

            // Find standard Unity Toggles
            LogUnityToggles(tag);

            // Find TMP_Dropdown components
            LogTmpDropdowns(tag);

            // Find custom Dropdown/Selector components
            LogCustomDropdowns(tag);

            MelonLogger.Msg($"[{tag}] === END UI DUMP ===");
        }

        private static void LogCustomButtonDetails(string tag, GameObject obj, string text, string path)
        {
            bool hasActualText = UITextExtractor.HasActualText(obj);
            var componentTypes = obj.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .ToList();
            string components = string.Join(", ", componentTypes);

            // Get size from RectTransform
            string sizeInfo = "";
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                sizeInfo = $" | Size: {rectTransform.sizeDelta.x:F0}x{rectTransform.sizeDelta.y:F0}";
            }

            // Check for Image components
            bool hasImage = obj.GetComponent<Image>() != null || obj.GetComponent<RawImage>() != null;
            bool hasTextChild = obj.GetComponentInChildren<TMP_Text>() != null;

            // Get sprite name if this is a Color Challenge button
            string spriteInfo = "";
            if (path.Contains("ColorMastery") || path.Contains("PlayBlade_Item"))
            {
                var image = obj.GetComponent<Image>();
                if (image != null && image.sprite != null)
                {
                    spriteInfo = $" | Sprite: {image.sprite.name}";
                }
                var parent = obj.transform.parent;
                if (parent != null)
                {
                    spriteInfo += $" | Parent: {parent.name}";
                    foreach (Transform sibling in parent)
                    {
                        if (sibling.gameObject != obj)
                        {
                            var sibText = UITextExtractor.GetText(sibling.gameObject);
                            if (!string.IsNullOrEmpty(sibText) && sibText.Length > 1)
                            {
                                spriteInfo += $" | Sibling[{sibling.name}]: {sibText}";
                            }
                        }
                    }
                }
            }

            MelonLogger.Msg($"[{tag}]   {path}");
            MelonLogger.Msg($"[{tag}]     Text: '{text}' | HasActualText: {hasActualText} | HasImage: {hasImage} | HasTextChild: {hasTextChild}{sizeInfo}{spriteInfo}");
            MelonLogger.Msg($"[{tag}]     Components: {components}");
        }

        private static void LogEventTriggers(string tag)
        {
            var eventTriggers = GameObject.FindObjectsOfType<EventTrigger>()
                .Where(e => e.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {eventTriggers.Count} EventTriggers:");
            foreach (var et in eventTriggers.Take(15))
            {
                string text = UITextExtractor.GetText(et.gameObject);
                MelonLogger.Msg($"[{tag}]   {et.gameObject.name} - '{text ?? "(no text)"}'");
            }
        }

        private static void LogStandardButtons(string tag)
        {
            var buttons = GameObject.FindObjectsOfType<Button>()
                .Where(b => b.gameObject.activeInHierarchy && b.interactable)
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {buttons.Count} standard Buttons:");
            foreach (var btn in buttons.Take(20))
            {
                string text = UITextExtractor.GetText(btn.gameObject);
                string path = GetGameObjectPath(btn.gameObject);

                string parentInfo = "";
                if (btn.gameObject.name.Contains("Increment") || btn.gameObject.name.Contains("Decrement"))
                {
                    var parent = btn.transform.parent;
                    if (parent != null)
                    {
                        parentInfo = $" | Parent: {parent.name}";
                        foreach (Transform sibling in parent)
                        {
                            if (sibling.gameObject != btn.gameObject)
                            {
                                var sibText = sibling.GetComponentInChildren<TMP_Text>();
                                if (sibText != null && !string.IsNullOrEmpty(sibText.text) && sibText.text.Length > 1)
                                {
                                    parentInfo += $" | Sibling[{sibling.name}]: '{sibText.text}'";
                                }
                            }
                        }
                    }
                }

                MelonLogger.Msg($"[{tag}]   {path} - '{text ?? "(no text)"}'{parentInfo}");
            }
        }

        private static void LogCustomToggles(string tag)
        {
            var customToggles = GameObject.FindObjectsOfType<MonoBehaviour>()
                .Where(mb => mb != null && mb.gameObject.activeInHierarchy &&
                       mb.GetType().Name.Contains("CustomToggle"))
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {customToggles.Count} CustomToggle components:");
            foreach (var ct in customToggles)
            {
                string text = UITextExtractor.GetText(ct.gameObject);
                string path = GetGameObjectPath(ct.gameObject);
                var toggle = ct.gameObject.GetComponent<Toggle>();
                string toggleState = toggle != null ? (toggle.isOn ? "ON" : "OFF") : "no Toggle component";
                MelonLogger.Msg($"[{tag}]   {path} - '{text ?? "(no text)"}' - {toggleState}");
            }
        }

        private static void LogScrollbars(string tag)
        {
            var scrollbars = GameObject.FindObjectsOfType<Scrollbar>()
                .Where(sb => sb != null && sb.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {scrollbars.Count} Scrollbars:");
            foreach (var sb in scrollbars)
            {
                string path = GetGameObjectPath(sb.gameObject);
                MelonLogger.Msg($"[{tag}]   {path} - value: {sb.value:F2}, size: {sb.size:F2}, interactable: {sb.interactable}");
            }
        }

        private static void LogScrollRects(string tag)
        {
            var scrollRects = GameObject.FindObjectsOfType<ScrollRect>()
                .Where(sr => sr != null && sr.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {scrollRects.Count} ScrollRect components:");
            foreach (var sr in scrollRects)
            {
                string path = GetGameObjectPath(sr.gameObject);
                MelonLogger.Msg($"[{tag}]   {path} - vertical: {sr.vertical}, horizontal: {sr.horizontal}");
            }
        }

        private static void LogUnityToggles(string tag)
        {
            var toggles = GameObject.FindObjectsOfType<Toggle>()
                .Where(t => t != null && t.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {toggles.Count} standard Unity Toggles:");
            foreach (var t in toggles)
            {
                string text = UITextExtractor.GetText(t.gameObject);
                string path = GetGameObjectPath(t.gameObject);
                MelonLogger.Msg($"[{tag}]   {path} - '{text ?? "(no text)"}' - {(t.isOn ? "ON" : "OFF")} - interactable: {t.interactable}");
            }
        }

        private static void LogTmpDropdowns(string tag)
        {
            var tmpDropdowns = GameObject.FindObjectsOfType<TMP_Dropdown>()
                .Where(d => d != null && d.gameObject.activeInHierarchy)
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {tmpDropdowns.Count} TMP_Dropdown components:");
            foreach (var d in tmpDropdowns)
            {
                string path = GetGameObjectPath(d.gameObject);
                string selectedText = d.options.Count > d.value ? d.options[d.value].text : "(no selection)";
                MelonLogger.Msg($"[{tag}]   {path} - selected: '{selectedText}' - interactable: {d.interactable}");
            }
        }

        private static void LogCustomDropdowns(string tag)
        {
            var customDropdowns = GameObject.FindObjectsOfType<MonoBehaviour>()
                .Where(mb => mb != null && mb.gameObject.activeInHierarchy &&
                       (mb.GetType().Name.Contains("Dropdown") || mb.GetType().Name.Contains("Selector")))
                .ToList();
            MelonLogger.Msg($"[{tag}] Found {customDropdowns.Count} Custom Dropdown/Selector components:");
            foreach (var cd in customDropdowns)
            {
                string path = GetGameObjectPath(cd.gameObject);
                string typeName = cd.GetType().Name;
                string text = UITextExtractor.GetText(cd.gameObject);
                MelonLogger.Msg($"[{tag}]   {path} - Type: {typeName} - '{text ?? "(no text)"}'");
            }
        }

        /// <summary>
        /// Dump the current UI hierarchy to log. Triggered by F12 key.
        /// </summary>
        /// <param name="tag">Log tag</param>
        /// <param name="announcer">Announcement service for completion message</param>
        public static void DumpUIHierarchy(string tag, IAnnouncementService announcer)
        {
            MelonLogger.Msg($"[{tag}] === F12 DEBUG: UI HIERARCHY DUMP ===");

            // Find all active Canvases
            var canvases = GameObject.FindObjectsOfType<Canvas>();
            MelonLogger.Msg($"[{tag}] Found {canvases.Length} active Canvases");

            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.gameObject.activeInHierarchy)
                    continue;

                MelonLogger.Msg($"[{tag}] Canvas: {canvas.name} (sortingOrder: {canvas.sortingOrder})");
                DumpGameObjectChildren(tag, canvas.gameObject, 1, 3);
            }

            // Check for any panel controllers that might be open
            MelonLogger.Msg($"[{tag}] === Checking Panel Controllers ===");
            var allMonoBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in allMonoBehaviours)
            {
                if (mb == null || !mb.gameObject.activeInHierarchy)
                    continue;

                string typeName = mb.GetType().Name;
                if (typeName.Contains("Controller") || typeName.Contains("Panel") ||
                    typeName.Contains("Overlay") || typeName.Contains("Browser") ||
                    typeName.Contains("Popup") || typeName.Contains("Viewer"))
                {
                    var isOpenProp = mb.GetType().GetProperty("IsOpen");
                    string isOpenStr = "";
                    if (isOpenProp != null)
                    {
                        try
                        {
                            bool isOpen = (bool)isOpenProp.GetValue(mb);
                            isOpenStr = $" (IsOpen: {isOpen})";
                        }
                        catch { }
                    }

                    MelonLogger.Msg($"[{tag}]   {typeName} on {mb.gameObject.name}{isOpenStr}");
                }
            }

            MelonLogger.Msg($"[{tag}] === END DEBUG DUMP ===");
            announcer?.Announce("Debug dump complete. Check log.", Models.AnnouncementPriority.High);
        }

        /// <summary>
        /// Recursively dump children of a GameObject to the log.
        /// </summary>
        public static void DumpGameObjectChildren(string tag, GameObject parent, int currentDepth, int maxDepth)
        {
            if (currentDepth > maxDepth || parent == null)
                return;

            string indent = new string(' ', currentDepth * 2);

            foreach (Transform child in parent.transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                var components = child.GetComponents<Component>();
                var componentNames = new List<string>();
                foreach (var c in components)
                {
                    if (c != null && !(c is Transform))
                        componentNames.Add(c.GetType().Name);
                }

                string componentsStr = componentNames.Count > 0 ? $" [{string.Join(", ", componentNames)}]" : "";
                MelonLogger.Msg($"[{tag}] {indent}{child.name}{componentsStr}");

                DumpGameObjectChildren(tag, child.gameObject, currentDepth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Log the hierarchy of a transform for debugging purposes.
        /// </summary>
        public static void LogHierarchy(string tag, Transform parent, string indent, int maxDepth)
        {
            if (maxDepth <= 0) return;

            foreach (Transform child in parent)
            {
                if (child == null) continue;
                string active = child.gameObject.activeInHierarchy ? "" : " [INACTIVE]";
                string text = UITextExtractor.GetText(child.gameObject);
                string textInfo = string.IsNullOrEmpty(text) ? "" : $" - \"{text}\"";
                MelonLogger.Msg($"[{tag}] {indent}{child.name}{active}{textInfo}");
                LogHierarchy(tag, child, indent + "  ", maxDepth - 1);
            }
        }

        /// <summary>
        /// Get a short path for a GameObject (up to 4 levels deep).
        /// </summary>
        public static string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            int depth = 0;
            while (parent != null && depth < 4)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            if (parent != null)
                path = ".../" + path;
            return path;
        }

        /// <summary>
        /// Get the full hierarchy path of a transform.
        /// </summary>
        public static string GetFullPath(Transform t)
        {
            if (t == null) return "null";
            if (t.parent == null) return t.name;
            return GetFullPath(t.parent) + "/" + t.name;
        }
    }
}
