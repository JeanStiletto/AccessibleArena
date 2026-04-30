using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using AccessibleArena.Core.Utils;
using TMPro;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Takes over input when the deck builder's <c>CardViewerController</c> popup is open.
    /// Two modes:
    ///   - <b>Cosmetic</b> (style picker): announce the active style, allow Enter to apply
    ///     the current selector via <c>_selectButton</c>, or move through carousel positions
    ///     via <c>SetCurrentSelector(int)</c> when the popup populated multiple skins.
    ///   - <b>Craft</b>: announce craft state and let Enter activate the craft button.
    /// Backspace activates <c>_cancelButton</c>.
    /// </summary>
    public class CardViewerNavigator : BaseNavigator
    {
        public override string NavigatorId => "CardViewer";
        public override string ScreenName => Strings.ScreenCardViewer;
        public override int Priority => 87; // Same band as DeckDetailsNavigator
        protected override bool SupportsCardNavigation => false;

        // PanelStateManager (Harmony) exposes this popup by its type name, not the raw
        // prefab name. The prefab is something like "Home_CardViewer_Desktop_16x9(Clone)";
        // we detect by Name == PanelTypeName and read the GameObject off the panel stack.
        private const string PanelTypeName = "CardViewerController";

        private GameObject _popup;
        private MonoBehaviour _controller;
        private bool _lastPopupState;

        // Mode (recomputed each discovery)
        private bool _isCosmeticMode;
        private bool _isReadOnly;

        // Maps element index → carousel selector index for cosmetic-mode entries.
        // Set when DiscoverElements adds a selector entry; -1 for non-selector elements.
        private readonly Dictionary<int, int> _selectorElementIndex = new Dictionary<int, int>();

        public CardViewerNavigator(IAnnouncementService announcer) : base(announcer) { }

        #region Detection

        protected override bool DetectScreen()
        {
            var psm = PanelStateManager.Instance;
            bool active = psm?.IsPanelActive(PanelTypeName) == true;

            if (active)
            {
                if (_popup == null || !_popup.activeInHierarchy)
                {
                    var stack = psm.GetPanelStack();
                    if (stack != null)
                    {
                        foreach (var p in stack)
                        {
                            if (p?.Name == PanelTypeName && p.IsValid)
                            {
                                _popup = p.GameObject;
                                break;
                            }
                        }
                    }
                }
                if (_controller == null) _controller = FindControllerComponent();
            }
            else
            {
                // Fallback: direct lookup of CardViewerController in case the panel state
                // hasn't fired yet (race during initial Show()).
                var fallback = FindControllerComponent();
                if (fallback != null && fallback.gameObject.activeInHierarchy && IsControllerShowing(fallback))
                {
                    _controller = fallback;
                    _popup = fallback.gameObject;
                    if (!_lastPopupState) { _lastPopupState = true; Log.Msg(NavigatorId, "CardViewer popup detected (component scan)"); }
                    return true;
                }
                _popup = null; _controller = null;
            }

            if (active != _lastPopupState)
            {
                _lastPopupState = active;
                Log.Msg(NavigatorId, $"CardViewer popup open: {active} (go={_popup?.name})");
            }

            return active && _popup != null;
        }

        private static MonoBehaviour FindControllerComponent()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "CardViewerController")
                    return mb;
            }
            return null;
        }

        private static bool IsControllerShowing(MonoBehaviour controller)
        {
            // PopupBase has IsShowing property; check it via reflection
            var prop = controller.GetType().GetProperty("IsShowing",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (prop != null)
            {
                try { return (bool)prop.GetValue(controller); } catch { }
            }
            return controller.gameObject.activeInHierarchy;
        }

        #endregion

        #region Discovery

        protected override void DiscoverElements()
        {
            _elements.Clear();
            _selectorElementIndex.Clear();
            if (_popup == null || _controller == null) return;
            if (_controller == null) _controller = FindControllerComponent();
            if (_controller == null) return;

            _isReadOnly = DeckCosmeticsReader.IsReadOnly();
            _isCosmeticMode = ReadIsCosmeticMode();

            // 1) Mode-specific state summary (text blocks the user can re-read with Up/Down).
            if (_isCosmeticMode)
                AddCosmeticStateBlocks();
            else
                AddCraftStateBlocks();

            // 2) Cosmetic mode: expose carousel selectors as Stepper elements (Enter switches selector).
            if (_isCosmeticMode)
                AddCosmeticSelectors();

            // 3) All other interactive elements come from a generic sweep of the popup so the user
            // sees the same buttons sighted players see — craft pips, currency tabs, store, cancel,
            // etc. — without us hand-listing every field. Sorted top-to-bottom for natural order.
            AddPopupButtons();

            Log.Msg(NavigatorId, $"Discovered {_elements.Count} elements; mode={(_isCosmeticMode ? "cosmetic" : "craft")}, readOnly={_isReadOnly}");
        }

        private void AddCosmeticStateBlocks()
        {
            int currentIdx = ReadIntField("_currentSelector");
            var selectorsField = _controller.GetType().GetField("_cosmeticSelectors", PrivateInstance);
            var selectors = selectorsField?.GetValue(_controller) as IList;
            int total = selectors?.Count ?? 0;
            string currentLabel = total > 0 && currentIdx >= 0 && currentIdx < total
                ? BuildSelectorLabel(selectors[currentIdx], currentIdx, total, true)
                : Strings.CosmeticsDefaultArt;
            AddTextBlock(Strings.CardViewerCosmeticMode(currentLabel));
            if (_isReadOnly)
                AddTextBlock(Strings.CardViewerReadOnly);
        }

        private void AddCraftStateBlocks()
        {
            int requested = ReadIntField("_requestedQuantity");
            AddTextBlock(Strings.CardViewerCraftMode(requested));

            // Wildcard total / subtitle / red label expose the same status text the popup shows.
            string wildcardTotal = ReadTmpFieldText("_wildcardTotalLabel");
            if (!string.IsNullOrEmpty(wildcardTotal))
                AddTextBlock(wildcardTotal);

            string subtitle = ReadTmpFieldText("_subtitleLabel");
            if (!string.IsNullOrEmpty(subtitle))
                AddTextBlock(subtitle);

            string subtitleRed = ReadTmpFieldText("_subtitleRedLabel");
            if (!string.IsNullOrEmpty(subtitleRed))
                AddTextBlock(subtitleRed);

            string craftCount = ReadTmpFieldText("_craftCountLabel");
            if (!string.IsNullOrEmpty(craftCount))
                AddTextBlock(craftCount);

            // Currency totals so the user knows what they have to spend.
            string gold = ReadTmpFieldText("_currencyGoldLabel");
            string gems = ReadTmpFieldText("_currencyGemsLabel");
            if (!string.IsNullOrEmpty(gold) || !string.IsNullOrEmpty(gems))
                AddTextBlock($"Gold: {gold ?? "0"}, Gems: {gems ?? "0"}");
        }

        private void AddCosmeticSelectors()
        {
            var selectorsField = _controller.GetType().GetField("_cosmeticSelectors", PrivateInstance);
            var selectors = selectorsField?.GetValue(_controller) as IList;

            int selectorCount = selectors?.Count ?? 0;
            if (selectorCount == 0)
            {
                AddTextBlock(Strings.CardViewerNoAlternatives);
                return;
            }

            int currentIdx = ReadIntField("_currentSelector");
            for (int i = 0; i < selectorCount; i++)
            {
                string label = BuildSelectorLabel(selectors[i], i, selectorCount, i == currentIdx);
                GameObject host = GetSelectorGameObject(selectors[i]);
                if (host == null)
                {
                    AddTextBlock(label);
                    continue;
                }
                int beforeAdd = _elements.Count;
                AddElement(host, label, default, null, null, UIElementClassifier.ElementRole.Stepper);
                if (_elements.Count == beforeAdd + 1)
                    _selectorElementIndex[beforeAdd] = i;
            }
        }

        /// <summary>
        /// Generic discovery of all currently-active CustomButtons in the popup, ordered
        /// top-to-bottom. Mirrors what BaseNavigator's popup mode would have given us if we
        /// hadn't preempted it. Skips elements already added in earlier discovery passes.
        /// </summary>
        private void AddPopupButtons()
        {
            var seen = new HashSet<int>();
            foreach (var e in _elements)
            {
                if (e.GameObject != null) seen.Add(e.GameObject.GetInstanceID());
            }

            var found = new List<(GameObject obj, string label, float sortKey)>();
            foreach (var mb in _popup.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                var go = mb.gameObject;
                if (seen.Contains(go.GetInstanceID())) continue;

                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != T.CustomButtonWithTooltip) continue;

                // Drop the in-popup card-style toggle in cosmetic mode — its only use is
                // visual examination, not a navigable action; including it adds noise.
                if (go.name.IndexOf("CardStyle", StringComparison.OrdinalIgnoreCase) >= 0
                    && _isCosmeticMode) continue;

                string label = BuildButtonLabel(go);
                var pos = go.transform.position;
                float sortKey = -pos.y * 1000f + pos.x;
                found.Add((go, label, sortKey));
                seen.Add(go.GetInstanceID());
            }

            foreach (var (obj, label, _) in found.OrderBy(x => x.sortKey))
                AddElement(obj, label, default, null, null, UIElementClassifier.ElementRole.Button);
        }

        private string BuildButtonLabel(GameObject buttonObj)
        {
            string text = UITextExtractor.GetButtonText(buttonObj, null);
            if (!string.IsNullOrEmpty(text))
                return $"{text}, button";

            // Fall back to the GameObject name with role info so the user has *something*.
            string n = buttonObj.name;
            if (n.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Cancel, button";
            if (n.IndexOf("Craft", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Craft, button";
            if (n.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Apply style, button";
            if (n.IndexOf("Store", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Open store, button";
            if (n.IndexOf("Pip", StringComparison.OrdinalIgnoreCase) >= 0)
                return $"{n}, button";
            return $"{n}, button";
        }

        private string ReadTmpFieldText(string fieldName)
        {
            try
            {
                var f = _controller.GetType().GetField(fieldName, AllInstanceFlags);
                if (f == null) return null;
                var tmp = f.GetValue(_controller) as TMP_Text;
                if (tmp == null || !tmp.gameObject.activeInHierarchy) return null;
                string t = tmp.text;
                return string.IsNullOrWhiteSpace(t) ? null : UITextExtractor.CleanText(t);
            }
            catch { return null; }
        }

        private string BuildSelectorLabel(object cardSelector, int index, int total, bool isCurrent)
        {
            if (cardSelector == null) return $"Style {index + 1} of {total}";

            string variant = null;
            string status = isCurrent ? Strings.CardViewerStyleStatusActive : null;
            string source = null;

            try
            {
                var skinField = cardSelector.GetType().GetField("CardSkin", AllInstanceFlags)
                              ?? cardSelector.GetType().GetField("_cardSkin", AllInstanceFlags);
                var collectedField = cardSelector.GetType().GetField("Collected", AllInstanceFlags)
                                  ?? cardSelector.GetType().GetField("_collected", AllInstanceFlags);

                bool collected = false;
                if (collectedField != null) { try { collected = (bool)collectedField.GetValue(cardSelector); } catch { } }

                var skinEntry = skinField?.GetValue(cardSelector);
                if (skinEntry != null)
                {
                    var variantField = skinEntry.GetType().GetField("Variant", AllInstanceFlags);
                    variant = variantField?.GetValue(skinEntry) as string;

                    var sourceField = skinEntry.GetType().GetField("Source", AllInstanceFlags);
                    var srcVal = sourceField?.GetValue(skinEntry);
                    source = MapAcquisitionSource(srcVal);
                }

                if (status == null)
                    status = collected ? Strings.CardViewerStyleStatusOwned : Strings.CardViewerStyleStatusNotOwned;
            }
            catch { }

            string name = string.IsNullOrEmpty(variant) ? Strings.CosmeticsDefaultArt : DeckCosmeticsReader.HumanizeId(variant);
            string label = name;
            if (!string.IsNullOrEmpty(status)) label += $", {status}";
            if (!string.IsNullOrEmpty(source)) label += $", {source}";
            label += $", {index + 1} of {total}";
            return label;
        }

        private static GameObject GetSelectorGameObject(object cardSelector)
        {
            if (cardSelector == null) return null;
            // CardSelector is a MonoBehaviour with the standard .gameObject accessor.
            try
            {
                var prop = cardSelector.GetType().GetProperty("gameObject", PublicInstance);
                return prop?.GetValue(cardSelector) as GameObject;
            }
            catch { return null; }
        }

        private static string MapAcquisitionSource(object sourceValue)
        {
            if (sourceValue == null) return null;
            string s = sourceValue.ToString();
            if (string.IsNullOrEmpty(s) || s == "0" || s == "None") return null;
            if (s.IndexOf("BattlePass", StringComparison.OrdinalIgnoreCase) >= 0)
                return Strings.CardViewerStyleSourceBattlePass;
            if (s.IndexOf("CodeRedemption", StringComparison.OrdinalIgnoreCase) >= 0)
                return Strings.CardViewerStyleSourceCode;
            if (s.IndexOf("Event", StringComparison.OrdinalIgnoreCase) >= 0)
                return Strings.CardViewerStyleSourceEvent;
            if (s.IndexOf("SeasonReward", StringComparison.OrdinalIgnoreCase) >= 0)
                return Strings.CardViewerStyleSourceSeasonReward;
            return null;
        }

        #endregion

        #region Reflection helpers

        private bool ReadIsCosmeticMode()
        {
            // CardViewerController._craftMode is a private bool; cosmetic mode = !_craftMode.
            try
            {
                var field = _controller.GetType().GetField("_craftMode", PrivateInstance);
                if (field == null) return false;
                bool craftMode = (bool)field.GetValue(_controller);
                return !craftMode;
            }
            catch { return false; }
        }

        private int ReadIntField(string name)
        {
            try
            {
                var f = _controller.GetType().GetField(name, AllInstanceFlags);
                if (f == null) return 0;
                return Convert.ToInt32(f.GetValue(_controller));
            }
            catch { return 0; }
        }

        private GameObject ReadButtonField(string fieldName)
        {
            try
            {
                var f = _controller.GetType().GetField(fieldName, AllInstanceFlags);
                if (f == null) return null;
                var mb = f.GetValue(_controller) as MonoBehaviour;
                return mb?.gameObject;
            }
            catch { return null; }
        }

        #endregion

        #region Activation

        protected override bool OnElementActivated(int index, GameObject element)
        {
            if (element == null) return false;
            if (_controller == null) return false;

            // Carousel selector entry → switch the popup's active selector and rescan.
            if (_isCosmeticMode && _selectorElementIndex.TryGetValue(index, out int targetIndex))
            {
                InvokeSetCurrentSelector(targetIndex);
                ForceRescan();
                return true;
            }

            // _selectButton: read-only deck blocks the apply.
            if (_isReadOnly && element.name.IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _announcer.AnnounceInterrupt(Strings.CardViewerReadOnly);
                return true;
            }

            return false; // Default activation handles buttons
        }

        private void InvokeSetCurrentSelector(int index)
        {
            try
            {
                var m = _controller.GetType().GetMethod("SetCurrentSelector", PrivateInstance);
                m?.Invoke(_controller, new object[] { index });
            }
            catch (Exception ex)
            {
                Log.Warn(NavigatorId, $"SetCurrentSelector({index}) failed: {ex.Message}");
            }
        }

        #endregion

        #region Input

        protected override bool HandleCustomInput()
        {
            // Backspace closes the popup via _cancelButton
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                var cancelBtn = ReadButtonField("_cancelButton");
                if (cancelBtn != null) UIActivator.Activate(cancelBtn);
                else if (_popup != null) _popup.SetActive(false);
                _announcer.AnnounceInterrupt(Strings.DeckDetailsClosed);
                return true;
            }

            return false;
        }

        #endregion

        #region Lifecycle

        protected override bool ValidateElements()
        {
            return _popup != null && _popup.activeInHierarchy;
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive) Deactivate();
            _popup = null;
            _controller = null;
        }

        protected override string GetActivationAnnouncement()
        {
            if (_isCosmeticMode)
            {
                int currentIdx = ReadIntField("_currentSelector");
                var selectorsField = _controller?.GetType().GetField("_cosmeticSelectors", PrivateInstance);
                var selectors = selectorsField?.GetValue(_controller) as IList;
                int total = selectors?.Count ?? 0;
                string currentLabel = Strings.CosmeticsDefaultArt;
                if (selectors != null && currentIdx >= 0 && currentIdx < total)
                    currentLabel = BuildSelectorLabel(selectors[currentIdx], currentIdx, total, true);
                return Strings.CardViewerCosmeticMode(currentLabel);
            }
            return Strings.CardViewerCraftMode(ReadIntField("_requestedQuantity"));
        }

        #endregion
    }
}
