using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;

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
            Log.Nav(tag, $"=== UI DUMP FOR {sceneName} ===");

            // Find all CustomButtons
            var customButtons = getActiveCustomButtons()
                .Select(obj => (obj, text: UITextExtractor.GetText(obj) ?? "(no text)", path: GetGameObjectPath(obj)))
                .ToList();

            Log.Nav(tag, $"Found {customButtons.Count} CustomButtons:");
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

            Log.Nav(tag, $"=== END UI DUMP ===");
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

            Log.Nav(tag, $"  {path}");
            Log.Nav(tag, $"    Text: '{text}' | HasActualText: {hasActualText} | HasImage: {hasImage} | HasTextChild: {hasTextChild}{sizeInfo}{spriteInfo}");
            Log.Nav(tag, $"    Components: {components}");

            // Log TooltipTrigger details if present
            foreach (var comp in obj.GetComponents<MonoBehaviour>())
            {
                if (comp != null && comp.GetType().Name == "TooltipTrigger")
                {
                    LogTooltipTriggerDetails(tag, comp);
                    break;
                }
            }
        }

        /// <summary>
        /// Logs all fields and properties of a TooltipTrigger component for debugging.
        /// </summary>
        private static void LogTooltipTriggerDetails(string tag, MonoBehaviour tooltipTrigger)
        {
            if (tooltipTrigger == null) return;

            var type = tooltipTrigger.GetType();
            var flags = AllInstanceFlags;

            Log.Nav(tag, $"    === TooltipTrigger Details ===");

            // Log all fields
            foreach (var field in type.GetFields(flags))
            {
                try
                {
                    var val = field.GetValue(tooltipTrigger);
                    string valStr = FormatValueForLog(val);
                    Log.Nav(tag, $"      Field: {field.Name} ({field.FieldType.Name}) = {valStr}");
                }
                catch (System.Exception ex)
                {
                    Log.Nav(tag, $"      Field: {field.Name} = <error: {ex.Message}>");
                }
            }

            // Log all properties
            foreach (var prop in type.GetProperties(flags))
            {
                if (!prop.CanRead) continue;
                // Skip indexer properties
                if (prop.GetIndexParameters().Length > 0) continue;

                try
                {
                    var val = prop.GetValue(tooltipTrigger);
                    string valStr = FormatValueForLog(val);
                    Log.Nav(tag, $"      Property: {prop.Name} ({prop.PropertyType.Name}) = {valStr}");
                }
                catch (System.Exception ex)
                {
                    Log.Nav(tag, $"      Property: {prop.Name} = <error: {ex.Message}>");
                }
            }

            Log.Nav(tag, $"    === End TooltipTrigger ===");
        }

        /// <summary>
        /// Formats a value for logging, handling various types appropriately.
        /// Useful for debug inspection of unknown object values.
        /// </summary>
        public static string FormatValueForLog(object val)
        {
            if (val == null)
                return "null";

            if (val is string strVal)
                return $"\"{strVal}\"";

            if (val is bool || val is int || val is float || val is double || val is System.Enum)
                return val.ToString();

            if (val is UnityEngine.Object unityObj)
            {
                if (unityObj == null) return "null (destroyed)";
                return $"<{val.GetType().Name}: {unityObj.name}>";
            }

            // For collections, show count
            if (val is System.Collections.ICollection collection)
                return $"<{val.GetType().Name}, Count={collection.Count}>";

            // For other types, show type name and try ToString
            string str = val.ToString();
            if (str.Length > 100)
                str = str.Substring(0, 100) + "...";
            return $"<{val.GetType().Name}>: {str}";
        }

        private static void LogEventTriggers(string tag)
        {
            var eventTriggers = GameObject.FindObjectsOfType<EventTrigger>()
                .Where(e => e.gameObject.activeInHierarchy)
                .ToList();
            Log.Nav(tag, $"Found {eventTriggers.Count} EventTriggers:");
            foreach (var et in eventTriggers.Take(15))
            {
                string text = UITextExtractor.GetText(et.gameObject);
                Log.Nav(tag, $"  {et.gameObject.name} - '{text ?? "(no text)"}'");
            }
        }

        private static void LogStandardButtons(string tag)
        {
            var buttons = GameObject.FindObjectsOfType<Button>()
                .Where(b => b.gameObject.activeInHierarchy && b.interactable)
                .ToList();
            Log.Nav(tag, $"Found {buttons.Count} standard Buttons:");
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

                Log.Nav(tag, $"  {path} - '{text ?? "(no text)"}'{parentInfo}");
            }
        }

        private static void LogCustomToggles(string tag)
        {
            var customToggles = GameObject.FindObjectsOfType<MonoBehaviour>()
                .Where(mb => mb != null && mb.gameObject.activeInHierarchy &&
                       mb.GetType().Name.Contains("CustomToggle"))
                .ToList();
            Log.Nav(tag, $"Found {customToggles.Count} CustomToggle components:");
            foreach (var ct in customToggles)
            {
                string text = UITextExtractor.GetText(ct.gameObject);
                string path = GetGameObjectPath(ct.gameObject);
                var toggle = ct.gameObject.GetComponent<Toggle>();
                string toggleState = toggle != null ? (toggle.isOn ? "ON" : "OFF") : "no Toggle component";
                Log.Nav(tag, $"  {path} - '{text ?? "(no text)"}' - {toggleState}");
            }
        }

        private static void LogScrollbars(string tag)
        {
            var scrollbars = GameObject.FindObjectsOfType<Scrollbar>()
                .Where(sb => sb != null && sb.gameObject.activeInHierarchy)
                .ToList();
            Log.Nav(tag, $"Found {scrollbars.Count} Scrollbars:");
            foreach (var sb in scrollbars)
            {
                string path = GetGameObjectPath(sb.gameObject);
                Log.Nav(tag, $"  {path} - value: {sb.value:F2}, size: {sb.size:F2}, interactable: {sb.interactable}");
            }
        }

        private static void LogScrollRects(string tag)
        {
            var scrollRects = GameObject.FindObjectsOfType<ScrollRect>()
                .Where(sr => sr != null && sr.gameObject.activeInHierarchy)
                .ToList();
            Log.Nav(tag, $"Found {scrollRects.Count} ScrollRect components:");
            foreach (var sr in scrollRects)
            {
                string path = GetGameObjectPath(sr.gameObject);
                Log.Nav(tag, $"  {path} - vertical: {sr.vertical}, horizontal: {sr.horizontal}");
            }
        }

        private static void LogUnityToggles(string tag)
        {
            var toggles = GameObject.FindObjectsOfType<Toggle>()
                .Where(t => t != null && t.gameObject.activeInHierarchy)
                .ToList();
            Log.Nav(tag, $"Found {toggles.Count} standard Unity Toggles:");
            foreach (var t in toggles)
            {
                string text = UITextExtractor.GetText(t.gameObject);
                string path = GetGameObjectPath(t.gameObject);
                Log.Nav(tag, $"  {path} - '{text ?? "(no text)"}' - {(t.isOn ? "ON" : "OFF")} - interactable: {t.interactable}");
            }
        }

        private static void LogTmpDropdowns(string tag)
        {
            var tmpDropdowns = GameObject.FindObjectsOfType<TMP_Dropdown>()
                .Where(d => d != null && d.gameObject.activeInHierarchy)
                .ToList();
            Log.Nav(tag, $"Found {tmpDropdowns.Count} TMP_Dropdown components:");
            foreach (var d in tmpDropdowns)
            {
                string path = GetGameObjectPath(d.gameObject);
                string selectedText = d.options.Count > d.value ? d.options[d.value].text : "(no selection)";
                Log.Nav(tag, $"  {path} - selected: '{selectedText}' - interactable: {d.interactable}");
            }
        }

        private static void LogCustomDropdowns(string tag)
        {
            var customDropdowns = GameObject.FindObjectsOfType<MonoBehaviour>()
                .Where(mb => mb != null && mb.gameObject.activeInHierarchy &&
                       (mb.GetType().Name.Contains("Dropdown") || mb.GetType().Name.Contains("Selector")))
                .ToList();
            Log.Nav(tag, $"Found {customDropdowns.Count} Custom Dropdown/Selector components:");
            foreach (var cd in customDropdowns)
            {
                string path = GetGameObjectPath(cd.gameObject);
                string typeName = cd.GetType().Name;
                string text = UITextExtractor.GetText(cd.gameObject);
                Log.Nav(tag, $"  {path} - Type: {typeName} - '{text ?? "(no text)"}'");
            }
        }

        /// <summary>
        /// Dump the current UI hierarchy to log. Triggered by F12 key.
        /// </summary>
        /// <param name="tag">Log tag</param>
        /// <param name="announcer">Announcement service for completion message</param>
        public static void DumpUIHierarchy(string tag, IAnnouncementService announcer)
        {
            Log.Nav(tag, $"=== F12 DEBUG: UI HIERARCHY DUMP ===");

            // Find all active Canvases
            var canvases = GameObject.FindObjectsOfType<Canvas>();
            Log.Nav(tag, $"Found {canvases.Length} active Canvases");

            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.gameObject.activeInHierarchy)
                    continue;

                Log.Nav(tag, $"Canvas: {canvas.name} (sortingOrder: {canvas.sortingOrder})");
                DumpGameObjectChildren(tag, canvas.gameObject, 1, 3);
            }

            // Check for any panel controllers that might be open
            Log.Nav(tag, $"=== Checking Panel Controllers ===");
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
                        catch { /* IsOpen property may throw on some component types */ }
                    }

                    Log.Nav(tag, $"  {typeName} on {mb.gameObject.name}{isOpenStr}");
                }
            }

            Log.Nav(tag, $"=== END DEBUG DUMP ===");
            announcer?.Announce(Models.Strings.DebugDumpComplete, Models.AnnouncementPriority.High);
        }

        /// <summary>
        /// Deep dump of the challenge blade widget hierarchy.
        /// Finds FriendChallengeBladeWidget and prints ALL children recursively
        /// with components and text content. Used to discover hidden/non-interactive elements.
        /// </summary>
        public static void DumpChallengeBlade(string tag, IAnnouncementService announcer)
        {
            Log.Msg("{tag}", $"=== CHALLENGE BLADE DEEP DUMP ===");

            // Find UnifiedChallengeBladeWidget by type name
            GameObject bladeWidget = null;
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "UnifiedChallengeBladeWidget" && mb.gameObject.activeInHierarchy)
                {
                    bladeWidget = mb.gameObject;
                    break;
                }
            }

            if (bladeWidget == null)
            {
                Log.Msg("{tag}", $"UnifiedChallengeBladeWidget not found or not active");
                Log.Msg("{tag}", $"=== END CHALLENGE BLADE DUMP ===");
                announcer?.Announce("Challenge blade not found", Models.AnnouncementPriority.High);
                return;
            }

            Log.Msg("{tag}", $"Found: {bladeWidget.name} [{bladeWidget.GetType().Name}]");

            // Also dump the parent up to Popout canvas for context
            var parent = bladeWidget.transform.parent;
            if (parent != null)
                Log.Msg("{tag}", $"Parent: {parent.name}");

            // Deep recursive dump - no depth limit
            DumpDeepChildren(tag, bladeWidget, 1);

            // Also dump the Container_Buttons canvas (main button + status text)
            var containerButtons = GameObject.Find("Container_Buttons");
            if (containerButtons != null)
            {
                Log.Msg("{tag}", $"--- Container_Buttons ---");
                DumpDeepChildren(tag, containerButtons, 1);
            }

            // Also dump UnifiedChallengesCONTAINER (leave, invite, player cards)
            var challengesContainer = GameObject.Find("UnifiedChallengesCONTAINER");
            if (challengesContainer != null)
            {
                Log.Msg("{tag}", $"--- UnifiedChallengesCONTAINER ---");
                DumpDeepChildren(tag, challengesContainer, 1);
            }

            Log.Msg("{tag}", $"=== END CHALLENGE BLADE DUMP ===");
            announcer?.Announce("Challenge blade dump complete", Models.AnnouncementPriority.High);
        }

        /// <summary>
        /// Recursively dump ALL children with full detail (components, text, active state).
        /// No depth limit - dumps the entire subtree.
        /// </summary>
        private static void DumpDeepChildren(string tag, GameObject parent, int depth)
        {
            if (parent == null) return;
            string indent = new string(' ', depth * 2);

            foreach (Transform child in parent.transform)
            {
                if (child == null) continue;

                bool active = child.gameObject.activeInHierarchy;
                string activeStr = active ? "" : " [INACTIVE]";

                // Components
                var components = child.GetComponents<Component>();
                var componentNames = new List<string>();
                foreach (var c in components)
                {
                    if (c != null && !(c is Transform) && !(c is RectTransform))
                        componentNames.Add(c.GetType().Name);
                }
                string componentsStr = componentNames.Count > 0 ? $" [{string.Join(", ", componentNames)}]" : "";

                // Text content
                string textInfo = "";
                if (active)
                {
                    // Try TMP_Text directly on this object
                    var tmpText = child.GetComponent<TMP_Text>();
                    if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                        textInfo = $" TEXT=\"{tmpText.text.Replace("\n", "\\n")}\"";

                    // Try UITextExtractor for Localize and other text sources
                    if (string.IsNullOrEmpty(textInfo))
                    {
                        string extracted = UITextExtractor.GetText(child.gameObject);
                        if (!string.IsNullOrEmpty(extracted) && extracted != child.name.ToLower().Replace("_", " "))
                            textInfo = $" TEXT=\"{extracted.Replace("\n", "\\n")}\"";
                    }
                }

                Log.Msg("{tag}", $"{indent}{child.name}{componentsStr}{activeStr}{textInfo}");

                // Recurse into children
                DumpDeepChildren(tag, child.gameObject, depth + 1);
            }
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
                Log.Msg("{tag}", $"{indent}{child.name}{componentsStr}");

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
                Log.Nav(tag, $"{indent}{child.name}{active}{textInfo}");
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

        /// <summary>
        /// Dump detailed information about a card GameObject. Triggered by F11 key.
        /// Useful for debugging cards that fail text extraction (e.g., "Unknown card").
        /// </summary>
        /// <param name="tag">Log tag</param>
        /// <param name="cardObj">The card GameObject to inspect</param>
        /// <param name="announcer">Announcement service for completion message</param>
        public static void DumpCardDetails(string tag, GameObject cardObj, IAnnouncementService announcer)
        {
            if (cardObj == null)
            {
                Log.Msg("{tag}", $"DumpCardDetails: No card object provided");
                announcer?.Announce("No card to inspect.", Models.AnnouncementPriority.High);
                return;
            }

            Log.Msg("{tag}", $"===========================================");
            Log.Msg("{tag}", $"=== F11 DEBUG: CARD DETAILS DUMP ===");
            Log.Msg("{tag}", $"===========================================");
            Log.Msg("{tag}", $"Card object: {cardObj.name}");
            Log.Msg("{tag}", $"Full path: {GetFullPath(cardObj.transform)}");
            Log.Msg("{tag}", $"Active: {cardObj.activeInHierarchy}");
            Log.Msg("{tag}", $"InstanceID: {cardObj.GetInstanceID()}");

            // Dump all components on the card root
            Log.Msg("{tag}", $"=== Components on root ===");
            foreach (var comp in cardObj.GetComponents<Component>())
            {
                if (comp == null) continue;
                Log.Msg("{tag}", $"  {comp.GetType().FullName}");
            }

            // Dump all TMP_Text elements
            Log.Msg("{tag}", $"=== TMP_Text elements (includeInactive=true) ===");
            var texts = cardObj.GetComponentsInChildren<TMP_Text>(true);
            Log.Msg("{tag}", $"Found {texts.Length} TMP_Text components");
            if (texts.Length == 0)
            {
                Log.Msg("{tag}", $"  (none found)");
            }
            foreach (var text in texts)
            {
                if (text == null) continue;
                string objName = text.gameObject.name;
                string rawContent = text.text ?? "(null)";
                bool isActive = text.gameObject.activeInHierarchy;
                string parentName = text.transform.parent?.name ?? "(no parent)";
                string grandparentName = text.transform.parent?.parent?.name ?? "";

                // Build parent chain for context
                string parentChain = grandparentName != "" ? $"{grandparentName}/{parentName}" : parentName;

                // Truncate very long text
                if (rawContent.Length > 200)
                    rawContent = rawContent.Substring(0, 200) + "...";

                // Flag interesting elements for vault progress debugging
                string marker = "";
                if (objName.Contains("Progress") || objName.Contains("Quantity") ||
                    objName.Contains("Title") || objName.Contains("TAG") ||
                    parentName.Contains("TAG") || parentName.Contains("Progress"))
                {
                    marker = " *** VAULT RELEVANT ***";
                }

                Log.Msg("{tag}", $"  [{(isActive ? "ON" : "OFF")}] '{objName}' (parent: {parentChain}){marker}");
                Log.Msg("{tag}", $"      Content: '{rawContent}'");
            }

            // Dump key MonoBehaviour components that might indicate card type
            Log.Msg("{tag}", $"=== Card-related MonoBehaviours ===");
            var monoBehaviours = cardObj.GetComponentsInChildren<MonoBehaviour>(true);
            string[] cardPatterns = { "Card", "Meta", "CDC", "Booster", "View", "Display" };
            foreach (var mb in monoBehaviours)
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                bool isRelevant = false;
                foreach (var pattern in cardPatterns)
                {
                    if (typeName.Contains(pattern))
                    {
                        isRelevant = true;
                        break;
                    }
                }
                if (isRelevant)
                {
                    Log.Msg("{tag}", $"  {typeName} on {mb.gameObject.name}");
                }
            }

            // Dump immediate children hierarchy (2 levels deep)
            Log.Msg("{tag}", $"=== Child hierarchy (2 levels) ===");
            DumpGameObjectChildren(tag, cardObj, 1, 2);

            Log.Msg("{tag}", $"===========================================");
            Log.Msg("{tag}", $"=== END CARD DETAILS DUMP ===");
            Log.Msg("{tag}", $"===========================================");
            announcer?.Announce(Models.Strings.CardDetailsDumped, Models.AnnouncementPriority.High);
        }

        /// <summary>
        /// Dump detailed information about a booster pack GameObject.
        /// Investigates the CarouselBooster and all its components to find set/product data.
        /// </summary>
        /// <param name="tag">Log tag</param>
        /// <param name="packObj">The pack hitbox/button GameObject to inspect</param>
        /// <param name="announcer">Announcement service for completion message</param>
        public static void DumpBoosterPackDetails(string tag, GameObject packObj, IAnnouncementService announcer)
        {
            if (packObj == null)
            {
                Log.Nav(tag, $"DumpBoosterPackDetails: No pack object provided");
                announcer?.Announce(Models.Strings.NoPackToInspect, Models.AnnouncementPriority.High);
                return;
            }

            Log.Msg("{tag}", $"=================================");
            Log.Msg("{tag}", $"=== BOOSTER PACK INVESTIGATION ===");
            Log.Msg("{tag}", $"=================================");
            Log.Msg("{tag}", $"Starting object: {packObj.name}");
            Log.Msg("{tag}", $"Full path: {GetFullPath(packObj.transform)}");

            // Walk up to find CarouselBooster parent
            Transform current = packObj.transform;
            Transform carouselBooster = null;
            int maxLevels = 8;

            while (current != null && maxLevels > 0)
            {
                Log.Msg("{tag}", $"Checking parent: {current.name}");

                if (current.name.Contains("CarouselBooster"))
                {
                    carouselBooster = current;
                    Log.Msg("{tag}", $">>> Found CarouselBooster: {current.name}");
                    break;
                }

                // Log components at each level
                var comps = current.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null || c is Transform) continue;
                    Log.Msg("{tag}", $"  - Component: {c.GetType().Name}");
                }

                current = current.parent;
                maxLevels--;
            }

            if (carouselBooster == null)
            {
                Log.Msg("{tag}", $"Could not find CarouselBooster parent!");
                announcer?.Announce(Models.Strings.CouldNotFindPackParent, Models.AnnouncementPriority.High);
                return;
            }

            // Dump ALL components on CarouselBooster
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"=== CarouselBooster Components ===");
            var allComponents = carouselBooster.GetComponents<Component>();
            foreach (var comp in allComponents)
            {
                if (comp == null || comp is Transform) continue;
                Log.Msg("{tag}", $"Component: {comp.GetType().FullName}");
            }

            // Look for data-holding MonoBehaviours and dump their fields/properties
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"=== MonoBehaviour Details (searching for set/product data) ===");
            var flags = AllInstanceFlags;

            var allMbs = carouselBooster.GetComponentsInChildren<MonoBehaviour>(true);
            string[] relevantPatterns = { "Booster", "Carousel", "Product", "Pack", "Set", "Item", "Data", "View", "Controller" };

            foreach (var mb in allMbs)
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;

                bool isRelevant = false;
                foreach (var pattern in relevantPatterns)
                {
                    if (typeName.Contains(pattern))
                    {
                        isRelevant = true;
                        break;
                    }
                }

                if (!isRelevant) continue;

                Log.Msg("{tag}", $"");
                Log.Msg("{tag}", $">>> {typeName} on {mb.gameObject.name} <<<");

                // Dump all fields
                var fields = mb.GetType().GetFields(flags);
                foreach (var field in fields)
                {
                    try
                    {
                        var val = field.GetValue(mb);
                        string valStr = FormatValueForLog(val);

                        // Highlight fields that might contain set/product info
                        bool isInteresting = field.Name.ToLower().Contains("set") ||
                                            field.Name.ToLower().Contains("name") ||
                                            field.Name.ToLower().Contains("product") ||
                                            field.Name.ToLower().Contains("title") ||
                                            field.Name.ToLower().Contains("id") ||
                                            field.Name.ToLower().Contains("code") ||
                                            field.Name.ToLower().Contains("booster") ||
                                            field.Name.ToLower().Contains("pack");

                        string marker = isInteresting ? " *** INTERESTING ***" : "";
                        Log.Msg("{tag}", $"  Field: {field.Name} ({field.FieldType.Name}) = {valStr}{marker}");

                        // If it's an object, try to dump its properties too
                        if (val != null && isInteresting && !field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                        {
                            DumpObjectProperties(tag, val, "      ");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Msg("{tag}", $"  Field: {field.Name} = <error: {ex.Message}>");
                    }
                }

                // Dump all properties
                var props = mb.GetType().GetProperties(flags);
                foreach (var prop in props)
                {
                    if (!prop.CanRead) continue;
                    if (prop.GetIndexParameters().Length > 0) continue;

                    try
                    {
                        var val = prop.GetValue(mb);
                        string valStr = FormatValueForLog(val);

                        bool isInteresting = prop.Name.ToLower().Contains("set") ||
                                            prop.Name.ToLower().Contains("name") ||
                                            prop.Name.ToLower().Contains("product") ||
                                            prop.Name.ToLower().Contains("title") ||
                                            prop.Name.ToLower().Contains("id") ||
                                            prop.Name.ToLower().Contains("code") ||
                                            prop.Name.ToLower().Contains("booster") ||
                                            prop.Name.ToLower().Contains("pack");

                        string marker = isInteresting ? " *** INTERESTING ***" : "";
                        Log.Msg("{tag}", $"  Property: {prop.Name} ({prop.PropertyType.Name}) = {valStr}{marker}");

                        // If it's an object, try to dump its properties too
                        if (val != null && isInteresting && !prop.PropertyType.IsPrimitive && prop.PropertyType != typeof(string))
                        {
                            DumpObjectProperties(tag, val, "      ");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Msg("{tag}", $"  Property: {prop.Name} = <error: {ex.Message}>");
                    }
                }
            }

            // Also check for LocalizedString components that might have the set name
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"=== TMP_Text Elements ===");
            var texts = carouselBooster.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text == null) continue;
                string content = text.text ?? "(null)";
                bool isActive = text.gameObject.activeInHierarchy;
                Log.Msg("{tag}", $"  [{(isActive ? "ON" : "OFF")}] {text.gameObject.name}: '{content}'");

                // Check for Localize component
                var localize = text.GetComponent<MonoBehaviour>();
                if (localize != null && localize.GetType().Name == "Localize")
                {
                    Log.Msg("{tag}", $"    [Has Localize component]");
                }
            }

            // Dump child hierarchy
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"=== Child Hierarchy (3 levels) ===");
            DumpGameObjectChildren(tag, carouselBooster.gameObject, 0, 3);

            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"=================================");
            Log.Msg("{tag}", $"=== END BOOSTER PACK INVESTIGATION ===");
            Log.Msg("{tag}", $"=================================");

            announcer?.Announce(Models.Strings.PackDetailsDumped, Models.AnnouncementPriority.High);
        }

        /// <summary>
        /// Dumps properties of an object for deep inspection.
        /// </summary>
        private static void DumpObjectProperties(string tag, object obj, string indent)
        {
            if (obj == null) return;

            var type = obj.GetType();
            var flags = AllInstanceFlags;

            // Limit to 10 properties to avoid huge logs
            int count = 0;
            foreach (var prop in type.GetProperties(flags))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                if (count++ > 10) break;

                try
                {
                    var val = prop.GetValue(obj);
                    string valStr = FormatValueForLog(val);
                    Log.Msg("{tag}", $"{indent}.{prop.Name} = {valStr}");
                }
                catch { /* Some properties throw on access; skip for debug dump */ }
            }

            // Also check fields
            count = 0;
            foreach (var field in type.GetFields(flags))
            {
                if (count++ > 10) break;

                try
                {
                    var val = field.GetValue(obj);
                    string valStr = FormatValueForLog(val);
                    Log.Msg("{tag}", $"{indent}.{field.Name} = {valStr}");
                }
                catch { /* Some fields throw on access; skip for debug dump */ }
            }
        }

        /// <summary>
        /// Comprehensive debug dump for workflow/ability activation system.
        /// Call this when WorkflowBrowser is active to capture all relevant info.
        /// </summary>
        public static void DumpWorkflowSystemDebug(string tag, GameObject workflowBrowser = null)
        {
            var flags = AllInstanceFlags;

            Log.Msg("{tag}", $"╔══════════════════════════════════════════════════════════════╗");
            Log.Msg("{tag}", $"║       COMPREHENSIVE WORKFLOW SYSTEM DEBUG DUMP              ║");
            Log.Msg("{tag}", $"╚══════════════════════════════════════════════════════════════╝");

            // ═══════════════════════════════════════════════════════════════
            // SECTION 1: GameManager and WorkflowController
            // ═══════════════════════════════════════════════════════════════
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"══════ SECTION 1: GameManager & WorkflowController ══════");

            MonoBehaviour gameManager = null;
            object workflowController = null;
            object currentInteraction = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                {
                    gameManager = mb;
                    break;
                }
            }

            if (gameManager != null)
            {
                Log.Msg("{tag}", $"GameManager: FOUND on '{gameManager.gameObject.name}'");
                Log.Msg("{tag}", $"  Type: {gameManager.GetType().FullName}");

                // Dump ALL properties that might be workflow-related
                Log.Msg("{tag}", $"  --- Workflow-related properties ---");
                foreach (var prop in gameManager.GetType().GetProperties(flags))
                {
                    string propName = prop.Name.ToLower();
                    if (propName.Contains("workflow") || propName.Contains("interaction") ||
                        propName.Contains("current") || propName.Contains("active") ||
                        propName.Contains("controller") || propName.Contains("manager"))
                    {
                        try
                        {
                            var val = prop.GetValue(gameManager);
                            Log.Msg("{tag}", $"  Property: {prop.Name} ({prop.PropertyType.Name}) = {val?.GetType()?.FullName ?? "null"}");

                            if (prop.Name == "WorkflowController" && val != null)
                                workflowController = val;
                            if (prop.Name == "CurrentInteraction" && val != null)
                                currentInteraction = val;
                        }
                        catch (Exception ex)
                        {
                            Log.Msg("{tag}", $"  Property: {prop.Name} = [error: {ex.Message}]");
                        }
                    }
                }

                // Also check fields
                Log.Msg("{tag}", $"  --- Workflow-related fields ---");
                foreach (var field in gameManager.GetType().GetFields(flags))
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("workflow") || fieldName.Contains("interaction") ||
                        fieldName.Contains("current") || fieldName.Contains("active") ||
                        fieldName.Contains("controller"))
                    {
                        try
                        {
                            var val = field.GetValue(gameManager);
                            Log.Msg("{tag}", $"  Field: {field.Name} ({field.FieldType.Name}) = {val?.GetType()?.FullName ?? "null"}");

                            if (field.Name.Contains("WorkflowController") && val != null)
                                workflowController = val;
                        }
                        catch (Exception ex)
                        {
                            Log.Msg("{tag}", $"  Field: {field.Name} = [error: {ex.Message}]");
                        }
                    }
                }
            }
            else
            {
                Log.Msg("{tag}", $"GameManager: NOT FOUND");
            }

            // ═══════════════════════════════════════════════════════════════
            // SECTION 2: WorkflowController deep inspection
            // ═══════════════════════════════════════════════════════════════
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"══════ SECTION 2: WorkflowController Deep Inspection ══════");

            if (workflowController != null)
            {
                var wcType = workflowController.GetType();
                Log.Msg("{tag}", $"WorkflowController type: {wcType.FullName}");
                Log.Msg("{tag}", $"Base type: {wcType.BaseType?.FullName ?? "none"}");

                // List ALL interfaces
                var interfaces = wcType.GetInterfaces();
                if (interfaces.Length > 0)
                {
                    Log.Msg("{tag}", $"  Interfaces: {string.Join(", ", interfaces.Select(i => i.Name))}");
                }

                // ALL properties
                Log.Msg("{tag}", $"  --- ALL Properties ---");
                foreach (var prop in wcType.GetProperties(flags))
                {
                    try
                    {
                        var val = prop.GetValue(workflowController);
                        string valStr = val?.GetType()?.Name ?? "null";
                        if (val is bool b) valStr = b.ToString();
                        if (val is int i) valStr = i.ToString();
                        if (val is string s) valStr = $"\"{s}\"";
                        Log.Msg("{tag}", $"  {prop.Name} ({prop.PropertyType.Name}) = {valStr}");

                        if (prop.Name.Contains("Current") && val != null)
                            currentInteraction = val;
                    }
                    catch (Exception ex)
                    {
                        Log.Msg("{tag}", $"  {prop.Name} = [error: {ex.Message}]");
                    }
                }

                // ALL fields
                Log.Msg("{tag}", $"  --- ALL Fields ---");
                foreach (var field in wcType.GetFields(flags))
                {
                    try
                    {
                        var val = field.GetValue(workflowController);
                        string valStr = val?.GetType()?.Name ?? "null";
                        if (val is bool b) valStr = b.ToString();
                        if (val is int i) valStr = i.ToString();
                        Log.Msg("{tag}", $"  {field.Name} ({field.FieldType.Name}) = {valStr}");

                        if (field.Name.Contains("current") || field.Name.Contains("Current"))
                            if (val != null) currentInteraction = val;
                    }
                    catch (Exception ex)
                    {
                        Log.Msg("{tag}", $"  {field.Name} = [error: {ex.Message}]");
                    }
                }

                // Key methods
                Log.Msg("{tag}", $"  --- Submit/Execute Methods ---");
                foreach (var method in wcType.GetMethods(flags))
                {
                    string mName = method.Name.ToLower();
                    if (mName.Contains("submit") || mName.Contains("execute") || mName.Contains("confirm") ||
                        mName.Contains("accept") || mName.Contains("complete") || mName.Contains("select"))
                    {
                        var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Log.Msg("{tag}", $"  {method.Name}({paramStr})");
                    }
                }
            }
            else
            {
                Log.Msg("{tag}", $"WorkflowController: NOT FOUND (not on GameManager)");
            }

            // ═══════════════════════════════════════════════════════════════
            // SECTION 3: Current Interaction/Workflow
            // ═══════════════════════════════════════════════════════════════
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"══════ SECTION 3: Active Workflow/Interaction ══════");

            if (currentInteraction != null)
            {
                var ciType = currentInteraction.GetType();
                Log.Msg("{tag}", $"CurrentInteraction: {ciType.FullName}");
                Log.Msg("{tag}", $"Base types: {GetTypeHierarchy(ciType)}");

                // Check for _request field (used by AutoTapActionsWorkflow)
                Log.Msg("{tag}", $"  --- Looking for _request field ---");
                var requestField = ciType.GetField("_request", flags);
                if (requestField != null)
                {
                    var request = requestField.GetValue(currentInteraction);
                    Log.Msg("{tag}", $"  _request: {request?.GetType()?.FullName ?? "null"}");

                    if (request != null)
                    {
                        // Dump request object
                        var reqType = request.GetType();
                        Log.Msg("{tag}", $"    --- Request methods ---");
                        foreach (var method in reqType.GetMethods(flags))
                        {
                            string mName = method.Name.ToLower();
                            if (mName.Contains("submit") || mName.Contains("cancel") || mName.Contains("solution"))
                            {
                                var paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                                Log.Msg("{tag}", $"    {method.Name}({paramStr})");
                            }
                        }
                    }
                }
                else
                {
                    Log.Msg("{tag}", $"  _request: NOT FOUND");
                }

                // All fields on the workflow
                Log.Msg("{tag}", $"  --- Workflow fields ---");
                foreach (var field in ciType.GetFields(flags))
                {
                    try
                    {
                        var val = field.GetValue(currentInteraction);
                        Log.Msg("{tag}", $"  {field.Name} ({field.FieldType.Name}) = {FormatValueForLog(val)}");
                    }
                    catch { /* Some fields throw on access; skip for debug dump */ }
                }

                // All methods
                Log.Msg("{tag}", $"  --- Workflow methods ---");
                foreach (var method in ciType.GetMethods(flags))
                {
                    string mName = method.Name.ToLower();
                    if (mName.Contains("submit") || mName.Contains("confirm") || mName.Contains("execute") ||
                        mName.Contains("complete") || mName.Contains("accept") || mName.Contains("select") ||
                        mName.Contains("solution") || mName.Contains("close") || mName.Contains("open"))
                    {
                        var paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                        Log.Msg("{tag}", $"  {method.Name}({paramStr})");
                    }
                }
            }
            else
            {
                Log.Msg("{tag}", $"CurrentInteraction: NULL (no active workflow)");
            }

            // ═══════════════════════════════════════════════════════════════
            // SECTION 4: Scene search for workflow-related objects
            // ═══════════════════════════════════════════════════════════════
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"══════ SECTION 4: Scene Search (Workflow/AutoTap objects) ══════");

            var allMonoBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>();
            string[] searchPatterns = { "Workflow", "AutoTap", "Interaction", "ManaPayment", "ActionSource" };

            foreach (var pattern in searchPatterns)
            {
                var matches = allMonoBehaviours.Where(mb => mb != null && mb.GetType().Name.Contains(pattern)).ToList();
                if (matches.Count > 0)
                {
                    Log.Msg("{tag}", $"  '{pattern}' matches ({matches.Count}):");
                    foreach (var mb in matches.Take(5))
                    {
                        Log.Msg("{tag}", $"    - {mb.GetType().Name} on '{mb.gameObject.name}'");
                    }
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // SECTION 5: WorkflowBrowser UI structure
            // ═══════════════════════════════════════════════════════════════
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"══════ SECTION 5: WorkflowBrowser UI Structure ══════");

            if (workflowBrowser == null)
            {
                // Try to find it
                foreach (var go in GameObject.FindObjectsOfType<GameObject>())
                {
                    if (go != null && go.activeInHierarchy && go.name == "WorkflowBrowser")
                    {
                        workflowBrowser = go;
                        break;
                    }
                }
            }

            if (workflowBrowser != null)
            {
                Log.Msg("{tag}", $"WorkflowBrowser: {workflowBrowser.name}");
                Log.Msg("{tag}", $"  Path: {GetFullPath(workflowBrowser.transform)}");
                Log.Msg("{tag}", $"  Text: '{UITextExtractor.GetText(workflowBrowser)}'");

                // Siblings (same parent level)
                Log.Msg("{tag}", $"  --- Siblings ---");
                var parent = workflowBrowser.transform.parent;
                if (parent != null)
                {
                    foreach (Transform sibling in parent)
                    {
                        if (sibling.gameObject == workflowBrowser) continue;
                        if (!sibling.gameObject.activeInHierarchy) continue;

                        var compNames = sibling.GetComponents<Component>()
                            .Where(c => c != null && !(c is Transform))
                            .Select(c => c.GetType().Name).ToList();

                        bool hasClickHandler = sibling.GetComponent<IPointerClickHandler>() != null;
                        bool hasButton = sibling.GetComponent<Button>() != null;
                        string clickInfo = hasClickHandler ? " [CLICKABLE]" : "";
                        clickInfo += hasButton ? " [BUTTON]" : "";

                        string sibText = UITextExtractor.GetText(sibling.gameObject);
                        Log.Msg("{tag}", $"    {sibling.name}{clickInfo}: [{string.Join(", ", compNames)}] text='{sibText}'");
                    }
                }
            }
            else
            {
                Log.Msg("{tag}", $"WorkflowBrowser: NOT FOUND in scene");
            }

            // ═══════════════════════════════════════════════════════════════
            // SECTION 6: PromptButtons detailed inspection
            // ═══════════════════════════════════════════════════════════════
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"══════ SECTION 6: PromptButtons Detailed Inspection ══════");

            var promptButtons = GameObject.FindObjectsOfType<Selectable>()
                .Where(s => s != null && s.gameObject.activeInHierarchy && s.gameObject.name.Contains("PromptButton"))
                .ToList();

            Log.Msg("{tag}", $"Found {promptButtons.Count} PromptButtons");
            foreach (var btn in promptButtons)
            {
                Log.Msg("{tag}", $"  Button: {btn.gameObject.name}");
                Log.Msg("{tag}", $"    Text: '{UITextExtractor.GetText(btn.gameObject)}'");
                Log.Msg("{tag}", $"    Interactable: {btn.interactable}");
                Log.Msg("{tag}", $"    Path: {GetGameObjectPath(btn.gameObject)}");

                // Check for Button component and its onClick
                var button = btn as Button;
                if (button != null)
                {
                    int listenerCount = button.onClick.GetPersistentEventCount();
                    Log.Msg("{tag}", $"    onClick persistent listeners: {listenerCount}");

                    // Try to get non-persistent listeners count via reflection
                    try
                    {
                        var callsField = typeof(UnityEventBase).GetField("m_Calls", flags);
                        if (callsField != null)
                        {
                            var calls = callsField.GetValue(button.onClick);
                            if (calls != null)
                            {
                                var countProp = calls.GetType().GetProperty("Count");
                                if (countProp != null)
                                {
                                    var count = countProp.GetValue(calls);
                                    Log.Msg("{tag}", $"    onClick runtime listeners (m_Calls.Count): {count}");
                                }
                            }
                        }
                    }
                    catch { /* Unity internal m_Calls field may not be accessible */ }
                }

                // Check all MonoBehaviours for callback-like fields
                foreach (var mb in btn.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var mbType = mb.GetType();
                    if (mbType.Name == "Button" || mbType.Namespace?.StartsWith("UnityEngine") == true) continue;

                    Log.Msg("{tag}", $"    MonoBehaviour: {mbType.Name}");
                    foreach (var field in mbType.GetFields(flags))
                    {
                        string fName = field.Name.ToLower();
                        if (fName.Contains("callback") || fName.Contains("action") || fName.Contains("click") ||
                            fName.Contains("event") || fName.Contains("delegate") || fName.Contains("submit"))
                        {
                            try
                            {
                                var val = field.GetValue(mb);
                                Log.Msg("{tag}", $"      {field.Name} ({field.FieldType.Name}) = {FormatValueForLog(val)}");
                            }
                            catch { /* Callback field may throw on access; skip for debug dump */ }
                        }
                    }
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // SECTION 7: UnderStack area (where WorkflowBrowser lives)
            // ═══════════════════════════════════════════════════════════════
            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"══════ SECTION 7: UnderStack Area Objects ══════");

            var underStackObjects = GameObject.FindObjectsOfType<GameObject>()
                .Where(go => go != null && go.activeInHierarchy && GetFullPath(go.transform).Contains("UnderStack"))
                .ToList();

            Log.Msg("{tag}", $"Found {underStackObjects.Count} active objects under UnderStack");
            foreach (var go in underStackObjects.Take(20))
            {
                var comps = go.GetComponents<Component>()
                    .Where(c => c != null && !(c is Transform) && !(c is RectTransform))
                    .Select(c => c.GetType().Name).ToList();

                bool hasClickHandler = go.GetComponent<IPointerClickHandler>() != null;
                string clickInfo = hasClickHandler ? " [CLICKABLE]" : "";

                string goText = UITextExtractor.GetText(go);
                if (!string.IsNullOrEmpty(goText) || comps.Count > 0 || hasClickHandler)
                {
                    Log.Msg("{tag}", $"  {go.name}{clickInfo}: [{string.Join(", ", comps)}] text='{goText}'");
                }
            }

            Log.Msg("{tag}", $"");
            Log.Msg("{tag}", $"╔══════════════════════════════════════════════════════════════╗");
            Log.Msg("{tag}", $"║              END WORKFLOW SYSTEM DEBUG DUMP                 ║");
            Log.Msg("{tag}", $"╚══════════════════════════════════════════════════════════════╝");
        }

        /// <summary>
        /// Gets the type hierarchy as a string (Type -> BaseType -> BaseType...).
        /// </summary>
        private static string GetTypeHierarchy(Type type)
        {
            var types = new List<string>();
            var current = type;
            while (current != null && current != typeof(object))
            {
                types.Add(current.Name);
                current = current.BaseType;
            }
            return string.Join(" -> ", types);
        }
    }
}
