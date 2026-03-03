using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using System;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// A reusable confirmation popup for crafting cards with wildcards.
    /// Creates a Unity UI overlay with body text, OK, and Cancel buttons.
    /// Base popup mode discovers the buttons via GetComponentsInChildren&lt;Button&gt;()
    /// and the body text via TMP_Text, so standard popup navigation works out of the box.
    /// </summary>
    public class CraftConfirmationPopup
    {
        private GameObject _root;
        private TextMeshProUGUI _bodyText;
        private Button _okButton;
        private Button _cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        /// <summary>Root GameObject for popup mode integration.</summary>
        public GameObject GameObject => _root;

        /// <summary>Whether the popup is currently visible.</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>
        /// Creates the GO hierarchy once. Call before first Show().
        /// Safe to call multiple times - only builds on first call.
        /// </summary>
        public void Initialize()
        {
            if (_root != null) return;

            // Root: Canvas (ScreenSpace - Overlay) at high sort order so it covers everything
            _root = new GameObject("CraftConfirmationPopup");
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            _root.AddComponent<CanvasScaler>();
            _root.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(_root);

            // Panel: semi-transparent black background, stretched to fill
            var panel = CreateChild(_root, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            StretchToFill(panelRect);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.7f);

            // Content: centered container
            var content = CreateChild(panel, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(500f, 250f);
            contentRect.anchoredPosition = Vector2.zero;

            var contentImage = content.AddComponent<Image>();
            contentImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Body text
            var bodyGO = CreateChild(content, "BodyText");
            var bodyRect = bodyGO.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.1f, 0.45f);
            bodyRect.anchorMax = new Vector2(0.9f, 0.9f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
            _bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            _bodyText.fontSize = 24;
            _bodyText.alignment = TextAlignmentOptions.Center;
            _bodyText.color = Color.white;
            _bodyText.text = "";

            // Cancel button (left, matching game popup layout)
            _cancelButton = CreateButton(content, "ButtonCancel", Models.Strings.CraftConfirmCancel,
                new Vector2(0.15f, 0.08f), new Vector2(0.45f, 0.35f));
            _cancelButton.onClick.AddListener((UnityEngine.Events.UnityAction)OnCancelClicked);

            // OK button (right, matching game popup layout)
            _okButton = CreateButton(content, "ButtonOK", Models.Strings.CraftConfirmOK,
                new Vector2(0.55f, 0.08f), new Vector2(0.85f, 0.35f));
            _okButton.onClick.AddListener((UnityEngine.Events.UnityAction)OnOKClicked);

            _root.SetActive(false);

            MelonLogger.Msg("[CraftConfirmationPopup] Initialized");
        }

        /// <summary>
        /// Show the popup with the given body text and callbacks.
        /// </summary>
        public void Show(string bodyText, Action onConfirm, Action onCancel)
        {
            if (_root == null)
            {
                MelonLogger.Warning("[CraftConfirmationPopup] Not initialized");
                return;
            }

            _bodyText.text = bodyText;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _root.SetActive(true);

            MelonLogger.Msg($"[CraftConfirmationPopup] Showing: {bodyText}");
        }

        /// <summary>
        /// Hide the popup and clear callbacks.
        /// </summary>
        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);

            _onConfirm = null;
            _onCancel = null;
        }

        private void OnOKClicked()
        {
            MelonLogger.Msg("[CraftConfirmationPopup] OK clicked");
            var callback = _onConfirm;
            Hide();
            callback?.Invoke();
        }

        private void OnCancelClicked()
        {
            MelonLogger.Msg("[CraftConfirmationPopup] Cancel clicked");
            var callback = _onCancel;
            Hide();
            callback?.Invoke();
        }

        #region UI Helpers

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Button CreateButton(GameObject parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var btnGO = CreateChild(parent, name);
            var btnRect = btnGO.GetComponent<RectTransform>();
            btnRect.anchorMin = anchorMin;
            btnRect.anchorMax = anchorMax;
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            var button = btnGO.AddComponent<Button>();
            button.targetGraphic = btnImage;

            // Label text child
            var textGO = CreateChild(btnGO, "Text");
            var textRect = textGO.GetComponent<RectTransform>();
            StretchToFill(textRect);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return button;
        }

        #endregion
    }
}
