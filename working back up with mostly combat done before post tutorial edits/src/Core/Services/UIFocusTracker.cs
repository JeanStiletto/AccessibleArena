using System;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Tracks UI focus changes using Unity's EventSystem and announces them via screen reader.
    /// Polls EventSystem.current.currentSelectedGameObject each frame to detect selection changes.
    /// </summary>
    public class UIFocusTracker
    {
        private readonly IAnnouncementService _announcer;
        private GameObject _lastSelected;
        private string _lastAnnouncedText;
        private bool _debugMode = true;

        /// <summary>
        /// Fired when focus changes. Parameters: (oldElement, newElement)
        /// </summary>
        public event Action<GameObject, GameObject> OnFocusChanged;

        /// <summary>
        /// Gets the currently focused element.
        /// </summary>
        public GameObject CurrentFocusedElement => _lastSelected;

        public UIFocusTracker(IAnnouncementService announcer)
        {
            _announcer = announcer;
        }

        private void Log(string message)
        {
            if (_debugMode)
                MelonLogger.Msg($"[FocusTracker] {message}");
        }

        /// <summary>
        /// Call this from OnUpdate to check for focus changes.
        /// </summary>
        public void Update()
        {
            // Debug: Log key presses for Tab, Shift+Tab, Enter, Space
            DebugKeyPresses();

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            var selected = eventSystem.currentSelectedGameObject;

            // Only announce if selection actually changed
            if (selected == _lastSelected)
                return;

            string oldName = _lastSelected != null ? _lastSelected.name : "null";
            string newName = selected != null ? selected.name : "null";
            Log($"Focus changed: {oldName} -> {newName}");

            var previousSelected = _lastSelected;
            _lastSelected = selected;

            // Fire focus change event
            OnFocusChanged?.Invoke(previousSelected, selected);

            if (selected == null)
                return;

            // Extract text from the selected UI element
            string text = UITextExtractor.GetText(selected);
            Log($"Extracted text: '{text}' from {selected.name}");

            // Avoid repeating the same announcement
            if (text == _lastAnnouncedText)
            {
                Log("Skipping duplicate announcement");
                return;
            }

            _lastAnnouncedText = text;

            if (!string.IsNullOrWhiteSpace(text))
            {
                Log($"Announcing: {text}");
                _announcer.Announce(text, Models.AnnouncementPriority.Normal);
            }
            else
            {
                Log("Text was empty, not announcing");
            }
        }

        private void DebugKeyPresses()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Log(shift ? "Shift+Tab pressed" : "Tab pressed");
                LogCurrentSelection();
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Log("Enter pressed");
                LogCurrentSelection();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                Log("Space pressed");
            }
        }

        private void LogCurrentSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Log("EventSystem is null");
                return;
            }

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
            {
                Log("No object selected in EventSystem");
                // Try to find focused input fields or other UI elements
                ScanForFocusedElements();
            }
            else
            {
                string path = GetGameObjectPath(selected);
                Log($"Currently selected: {path}");
            }
        }

        private void ScanForFocusedElements()
        {
            // Check for focused TMP_InputFields
            var inputFields = GameObject.FindObjectsOfType<TMPro.TMP_InputField>();
            foreach (var field in inputFields)
            {
                if (field.isFocused)
                {
                    Log($"Found focused TMP_InputField: {GetGameObjectPath(field.gameObject)}");
                    return;
                }
            }

            // Check for focused legacy InputFields
            var legacyFields = GameObject.FindObjectsOfType<UnityEngine.UI.InputField>();
            foreach (var field in legacyFields)
            {
                if (field.isFocused)
                {
                    Log($"Found focused InputField: {GetGameObjectPath(field.gameObject)}");
                    return;
                }
            }

            // Log all active selectables for debugging
            var selectables = GameObject.FindObjectsOfType<UnityEngine.UI.Selectable>();
            Log($"Found {selectables.Length} Selectable components in scene");

            int activeCount = 0;
            foreach (var sel in selectables)
            {
                if (sel.isActiveAndEnabled && sel.interactable)
                {
                    activeCount++;
                    if (activeCount <= 10)
                    {
                        string type = sel.GetType().Name;
                        string text = UITextExtractor.GetText(sel.gameObject);
                        Log($"  Selectable: {type} - {sel.gameObject.name} - Text: {text}");
                    }
                }
            }
            Log($"Total active/interactable: {activeCount}");

            // Also scan for any objects with click handlers (EventTrigger, IPointerClickHandler)
            ScanForClickableElements();
        }

        private void ScanForClickableElements()
        {
            // Find all GameObjects with EventTrigger component
            var eventTriggers = GameObject.FindObjectsOfType<UnityEngine.EventSystems.EventTrigger>();
            if (eventTriggers.Length > 0)
            {
                Log($"Found {eventTriggers.Length} EventTrigger components:");
                foreach (var trigger in eventTriggers)
                {
                    if (trigger.isActiveAndEnabled)
                    {
                        string text = UITextExtractor.GetText(trigger.gameObject);
                        Log($"  EventTrigger: {trigger.gameObject.name} - Text: {text}");
                    }
                }
            }

            // Find objects in the WelcomeGate panel
            var welcomeGate = GameObject.Find("Panel - WelcomeGate_Desktop_16x9(Clone)");
            if (welcomeGate != null)
            {
                Log("Scanning WelcomeGate panel children:");
                ScanChildren(welcomeGate.transform, 0);
            }
        }

        private void ScanChildren(Transform parent, int depth)
        {
            if (depth > 3) return; // Limit depth

            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                // Check if this has any interesting components
                var button = child.GetComponent<UnityEngine.UI.Button>();
                var tmpText = child.GetComponent<TMPro.TMP_Text>();
                var image = child.GetComponent<UnityEngine.UI.Image>();

                string indent = new string(' ', depth * 2);

                if (button != null)
                {
                    string text = UITextExtractor.GetText(child.gameObject);
                    Log($"{indent}[Button] {child.name}: {text}");
                }
                else if (tmpText != null && !string.IsNullOrWhiteSpace(tmpText.text))
                {
                    Log($"{indent}[Text] {child.name}: {tmpText.text}");
                }
                else if (image != null && image.raycastTarget)
                {
                    Log($"{indent}[Clickable Image] {child.name}");
                }

                ScanChildren(child, depth + 1);
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            int depth = 0;
            while (parent != null && depth < 3)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            return path;
        }

        /// <summary>
        /// Force re-announce the current selection.
        /// </summary>
        public void AnnounceCurrentSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                _announcer.Announce("No selection", Models.AnnouncementPriority.Normal);
                return;
            }

            string text = UITextExtractor.GetText(eventSystem.currentSelectedGameObject);
            _lastAnnouncedText = text;
            _announcer.AnnounceInterrupt(text);
        }

        /// <summary>
        /// Clear tracking state (call when context changes).
        /// </summary>
        public void Reset()
        {
            _lastSelected = null;
            _lastAnnouncedText = null;
        }
    }
}
