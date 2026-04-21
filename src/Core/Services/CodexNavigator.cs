using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the Codex of the Multiverse (Learn to Play) screen.
    /// Three modes: TOC (table of contents), Content (article paragraphs), Credits.
    ///
    /// TOC uses drill-down navigation:
    ///   Enter on a category → shows only its children
    ///   Backspace → returns to parent level
    ///   Backspace at top level → navigate Home
    /// </summary>
    public class CodexNavigator : BaseNavigator
    {
        #region Constants

        private const int CodexPriority = 50;

        #endregion

        #region Mode

        private enum CodexMode { TableOfContents, Content, Credits }
        private CodexMode _mode;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Codex";
        public override string ScreenName => Strings.ScreenCodex;
        public override int Priority => CodexPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => false;

        #endregion

        #region Navigation State

        // Current visible TOC items (changes on drill-down)
        private readonly List<TocItem> _tocItems = new List<TocItem>();
        private int _tocIndex;

        // Drill-down stack: each entry stores a parent level's items + position
        private readonly List<TocLevel> _navStack = new List<TocLevel>();

        // Content paragraphs
        private readonly List<string> _contentParagraphs = new List<string>();
        private int _contentIndex;

        // Credits paragraphs
        private readonly List<string> _creditsParagraphs = new List<string>();
        private int _creditsIndex;

        // Delayed drill-down after clicking a category (game needs time to expand children)
        private bool _pendingDrillDown;
        private float _drillDownTimer;
        private MonoBehaviour _drillDownSection; // the section component we clicked
        private string _drillDownLabel;

        #endregion

        #region TOC Item & Level

        private struct TocItem
        {
            public GameObject ButtonGameObject; // CustomButton GO to click
            public MonoBehaviour SectionComponent; // TableOfContentsSection (null for standalone)
            public string Label;
            public bool IsCategory; // has childAnchor = drillable category
            public bool IsStandalone; // Replay Tutorial, Credits
        }

        private struct TocLevel
        {
            public string Label; // parent category label
            public int SelectedIndex; // cursor position at that level
            public List<TocItem> Items; // items at that level
        }


        #endregion

        #region Cached Controller & Reflection

        private MonoBehaviour _controller;
        private GameObject _controllerGameObject;

        private sealed class CodexHandles
        {
            // LearnToPlayControllerV2
            public PropertyInfo IsOpen;
            public FieldInfo LearnToPlayRoot;
            public FieldInfo TableOfContents;
            public FieldInfo TableOfContentsTopics;
            public FieldInfo ContentView;
            public FieldInfo ReplayTutorialButton;
            public FieldInfo CreditsButton;
            public FieldInfo CreditsDisplay;

            // TableOfContentsSection
            public Type TocSectionType;
            public FieldInfo TocButton;
            public FieldInfo TocIntent;
            public FieldInfo TocChildAnchor;
            public FieldInfo TocSection;

            // LearnMoreSection
            public Type LearnMoreSectionType;
            public FieldInfo SectionTitle;
            public FieldInfo SectionId;
            public FieldInfo ChildSections;
        }

        private static readonly ReflectionCache<CodexHandles> _codexCache = new ReflectionCache<CodexHandles>(
            builder: t =>
            {
                var h = new CodexHandles
                {
                    IsOpen = t.GetProperty("IsOpen", AllInstanceFlags | BindingFlags.FlattenHierarchy),
                    LearnToPlayRoot = t.GetField("learnToPlayRoot", AllInstanceFlags),
                    TableOfContents = t.GetField("tableOfContents", AllInstanceFlags),
                    TableOfContentsTopics = t.GetField("tableOfContentsTopics", AllInstanceFlags),
                    ContentView = t.GetField("contentView", AllInstanceFlags),
                    ReplayTutorialButton = t.GetField("_replayTutorialButton", AllInstanceFlags),
                    CreditsButton = t.GetField("_creditsButton", AllInstanceFlags),
                    CreditsDisplay = t.GetField("_creditsDisplay", AllInstanceFlags),
                };

                h.TocSectionType = FindType("Core.MainNavigation.LearnToPlay.TableOfContentsSection");
                if (h.TocSectionType != null)
                {
                    h.TocButton = h.TocSectionType.GetField("button", AllInstanceFlags);
                    h.TocIntent = h.TocSectionType.GetField("buttonClickIntent", AllInstanceFlags);
                    h.TocChildAnchor = h.TocSectionType.GetField("childAnchor", AllInstanceFlags);
                    h.TocSection = h.TocSectionType.GetField("section", AllInstanceFlags);
                }

                h.LearnMoreSectionType = FindType("Core.MainNavigation.LearnToPlay.LearnMoreSection");
                if (h.LearnMoreSectionType != null)
                {
                    h.SectionTitle = h.LearnMoreSectionType.GetField("_title", AllInstanceFlags);
                    h.SectionId = h.LearnMoreSectionType.GetField("Id", PublicInstance);
                    h.ChildSections = ReflectionWalk.FindField(h.LearnMoreSectionType, "_childSections", AllInstanceFlags);
                }

                return h;
            },
            validator: h => h.LearnToPlayRoot != null && h.TableOfContents != null,
            logTag: "Codex",
            logSubject: "LearnToPlayControllerV2");

        #endregion

        #region Constructor

        public CodexNavigator(IAnnouncementService announcer) : base(announcer) { }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            var controller = FindController();
            if (controller != null && IsControllerOpen(controller))
            {
                _controller = controller;
                _controllerGameObject = controller.gameObject;
                return true;
            }

            return false;
        }

        private MonoBehaviour FindController()
        {
            // Use cached reference if still valid
            if (_controller != null && _controller.gameObject != null && _controller.gameObject.activeInHierarchy)
                return _controller;

            _controller = null;
            _controllerGameObject = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.LearnToPlayControllerV2)
                    return mb;
            }

            return null;
        }

        private bool IsControllerOpen(MonoBehaviour controller)
        {
            var type = controller.GetType();
            EnsureReflectionCached(type);

            if (_codexCache.Handles.IsOpen != null)
            {
                try
                {
                    return (bool)_codexCache.Handles.IsOpen.GetValue(controller);
                }
                catch { return false; }
            }

            return true;
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached(Type controllerType)
        {
            _codexCache.EnsureInitialized(controllerType);
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            DiscoverTopLevel();

            if (_tocItems.Count > 0 && _controllerGameObject != null)
            {
                // Add dummy element for BaseNavigator validation
                AddElement(_controllerGameObject, "Codex");
            }
        }

        /// <summary>
        /// Discover top-level TOC items (depth 0 categories + standalone buttons).
        /// Clears the navigation stack.
        /// </summary>
        private void DiscoverTopLevel()
        {
            _tocItems.Clear();
            _navStack.Clear();

            if (_controller == null) return;

            // Scan depth 0 bubbles only (top-level categories)
            var tocBubblesGo = GetFieldGameObject(_codexCache.Handles.TableOfContents);
            if (tocBubblesGo != null)
            {
                ScanContainerForItems(tocBubblesGo.transform);
            }

            // Add standalone buttons: Replay Tutorial and Credits
            AddStandaloneButton(_codexCache.Handles.ReplayTutorialButton, "Replay Tutorial");
            AddStandaloneButton(_codexCache.Handles.CreditsButton, "Credits");

            Log.Msg("Codex", $"Discovered {_tocItems.Count} top-level TOC items");
        }

        /// <summary>
        /// Scan a container's direct children for TableOfContentsSection components.
        /// Only scans one level to avoid picking up nested subcategories.
        /// </summary>
        private void ScanContainerForItems(Transform container)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                var tocSection = FindTocSectionComponent(child.gameObject);
                if (tocSection == null) continue;

                var buttonGo = GetCustomButtonGameObject(tocSection);
                if (buttonGo == null) continue;

                string label = ExtractSectionLabel(tocSection, buttonGo);

                // Determine if drillable: has childAnchor OR has child sections in LearnMoreSection
                var childAnchor = GetChildAnchor(tocSection);
                bool isCategory = childAnchor != null || HasChildSections(tocSection);

                Log.Msg("Codex", $"TOC item: '{label}' isCategory={isCategory} section='{tocSection.gameObject.name}'");

                _tocItems.Add(new TocItem
                {
                    ButtonGameObject = buttonGo,
                    SectionComponent = tocSection,
                    Label = label,
                    IsCategory = isCategory,
                    IsStandalone = false
                });
            }
        }

        private MonoBehaviour FindTocSectionComponent(GameObject go)
        {
            if (_codexCache.Handles.TocSectionType != null)
            {
                var comp = go.GetComponent(_codexCache.Handles.TocSectionType);
                return comp as MonoBehaviour;
            }

            // Fallback: find by type name (namespace lookup failed)
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "TableOfContentsSection")
                {
                    // Cache the type and all fields from this live instance
                    CacheTocSectionType(mb.GetType());
                    return mb;
                }
            }
            return null;
        }

        private void CacheTocSectionType(Type type)
        {
            _codexCache.Handles.TocSectionType = type;
            var flags = AllInstanceFlags;
            _codexCache.Handles.TocButton = type.GetField("button", flags);
            _codexCache.Handles.TocIntent = type.GetField("buttonClickIntent", flags);
            _codexCache.Handles.TocChildAnchor = type.GetField("childAnchor", flags);
            _codexCache.Handles.TocSection = type.GetField("section", flags);

            Log.Msg("Codex", $"Cached TOC section type from fallback: " +
                $"Button={_codexCache.Handles.TocButton != null}, Intent={_codexCache.Handles.TocIntent != null}, " +
                $"ChildAnchor={_codexCache.Handles.TocChildAnchor != null}, Section={_codexCache.Handles.TocSection != null}");

            // Also cache LearnMoreSection type from the section field
            if (_codexCache.Handles.TocSection != null && _codexCache.Handles.LearnMoreSectionType == null)
            {
                _codexCache.Handles.LearnMoreSectionType = _codexCache.Handles.TocSection.FieldType;
                _codexCache.Handles.SectionTitle = _codexCache.Handles.LearnMoreSectionType.GetField("_title", flags);
                _codexCache.Handles.SectionId = _codexCache.Handles.LearnMoreSectionType.GetField("Id", PublicInstance);
                Log.Msg("Codex", $"Cached LearnMoreSection type: Title={_codexCache.Handles.SectionTitle != null}, Id={_codexCache.Handles.SectionId != null}");
            }
        }

        private GameObject GetCustomButtonGameObject(MonoBehaviour tocSection)
        {
            if (_codexCache.Handles.TocButton != null)
            {
                try
                {
                    var btn = _codexCache.Handles.TocButton.GetValue(tocSection) as MonoBehaviour;
                    if (btn != null && btn.gameObject != null)
                        return btn.gameObject;
                }
                catch { /* Field may not exist on different game versions */ }
            }

            // Fallback: find CustomButton in children
            return FindCustomButton(tocSection.gameObject);
        }

        private GameObject GetChildAnchor(MonoBehaviour tocSection)
        {
            if (_codexCache.Handles.TocChildAnchor != null)
            {
                try
                {
                    return _codexCache.Handles.TocChildAnchor.GetValue(tocSection) as GameObject;
                }
                catch { /* Field may not exist on different game versions */ }
            }
            return null;
        }

        /// <summary>
        /// Check if a TOC section's LearnMoreSection has child sections (sub-category).
        /// Secondary topics don't have childAnchors but their LearnMoreSection
        /// has _childSections list populated for sub-categories.
        /// </summary>
        private bool HasChildSections(MonoBehaviour tocSection)
        {
            if (_codexCache.Handles.TocSection == null) return false;
            try
            {
                var learnMoreSection = _codexCache.Handles.TocSection.GetValue(tocSection);
                if (learnMoreSection == null) return false;

                // Cache _childSections field on first use
                if (_codexCache.Handles.ChildSections == null)
                {
                    var flags = AllInstanceFlags;
                    _codexCache.Handles.ChildSections = learnMoreSection.GetType().GetField("_childSections", flags);
                }

                if (_codexCache.Handles.ChildSections == null) return false;
                var list = _codexCache.Handles.ChildSections.GetValue(learnMoreSection) as System.Collections.IList;
                return list != null && list.Count > 0;
            }
            catch { return false; }
        }

        private string ExtractSectionLabel(MonoBehaviour tocSection, GameObject buttonGo)
        {
            // Try UITextExtractor on the whole section GO (catches Localize/TMP_Text)
            string label = UITextExtractor.GetText(tocSection.gameObject);

            // Fallback to button GO
            if (string.IsNullOrEmpty(label))
                label = UITextExtractor.GetText(buttonGo);

            // Fallback to GO name
            if (string.IsNullOrEmpty(label))
                label = tocSection.gameObject.name;

            return CleanLabel(label);
        }

        private static bool HasActiveChildren(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                if (t.GetChild(i).gameObject.activeInHierarchy)
                    return true;
            }
            return false;
        }

        private void AddStandaloneButton(FieldInfo field, string fallbackLabel)
        {
            if (field == null || _controller == null) return;

            try
            {
                var btn = field.GetValue(_controller) as MonoBehaviour;
                if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy) return;

                string label = UITextExtractor.GetText(btn.gameObject);
                if (string.IsNullOrEmpty(label))
                    label = fallbackLabel;
                label = CleanLabel(label);

                _tocItems.Add(new TocItem
                {
                    ButtonGameObject = btn.gameObject,
                    SectionComponent = null,
                    Label = label,
                    IsCategory = false,
                    IsStandalone = true
                });
            }
            catch { /* Reflection access to button field may fail */ }
        }

        private GameObject FindCustomButton(GameObject parent)
        {
            foreach (var mb in parent.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "CustomButton")
                    return mb.gameObject;
            }
            return null;
        }

        private GameObject GetFieldGameObject(FieldInfo field)
        {
            if (field == null || _controller == null) return null;
            try
            {
                return field.GetValue(_controller) as GameObject;
            }
            catch { return null; }
        }

        private static string CleanLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = UITextExtractor.StripRichText(text).Trim();
            text = Regex.Replace(text, @"\s+", " ");
            return text;
        }

        #endregion

        #region Content Extraction

        private void ExtractContentParagraphs()
        {
            _contentParagraphs.Clear();
            _contentIndex = 0;

            var contentViewGo = GetFieldGameObject(_codexCache.Handles.ContentView);
            if (contentViewGo == null || !contentViewGo.activeInHierarchy) return;

            int skippedCards = 0;
            var contentTransform = contentViewGo.transform;
            var texts = contentViewGo.GetComponentsInChildren<TMPro.TMP_Text>(false);
            foreach (var tmp in texts)
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;

                // Skip text inside embedded card displays
                if (IsInsideCardDisplay(tmp.transform, contentTransform))
                {
                    skippedCards++;
                    continue;
                }

                string text = tmp.text;
                if (string.IsNullOrEmpty(text)) continue;

                text = CleanLabel(text);
                if (string.IsNullOrEmpty(text) || text.Length < 3) continue;

                _contentParagraphs.Add(text);
            }

            Log.Msg("Codex", $"Extracted {_contentParagraphs.Count} content paragraphs (skipped {skippedCards} card texts)");
        }

        /// <summary>
        /// Check if a TMP_Text element is inside an embedded card display.
        /// Walks up the parent hierarchy looking for card-related components or names.
        /// </summary>
        private static bool IsInsideCardDisplay(Transform textTransform, Transform stopAt)
        {
            var current = textTransform.parent;
            while (current != null && current != stopAt)
            {
                // Check GO name for card display patterns
                string goName = current.gameObject.name;
                if (goName.Contains("CardAnchor") ||
                    goName.Contains("MetaCardView") ||
                    goName.Contains("DuelCardView") ||
                    goName.Contains("CardRenderer"))
                    return true;

                // Check component types for card-related MonoBehaviours
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string typeName = mb.GetType().Name;
                    if (typeName.Contains("CardView") ||
                        typeName.Contains("CardRenderer") ||
                        typeName.Contains("CDC"))
                        return true;
                }

                current = current.parent;
            }
            return false;
        }

        private void ExtractCreditsParagraphs()
        {
            _creditsParagraphs.Clear();
            _creditsIndex = 0;

            if (_codexCache.Handles.CreditsDisplay == null || _controller == null) return;

            try
            {
                var creditsDisplay = _codexCache.Handles.CreditsDisplay.GetValue(_controller) as MonoBehaviour;
                if (creditsDisplay == null || !creditsDisplay.gameObject.activeInHierarchy) return;

                var texts = creditsDisplay.GetComponentsInChildren<TMPro.TMP_Text>(false);
                foreach (var tmp in texts)
                {
                    if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                    string text = tmp.text;
                    if (string.IsNullOrEmpty(text)) continue;

                    text = CleanLabel(text);
                    if (string.IsNullOrEmpty(text) || text.Length < 3) continue;

                    _creditsParagraphs.Add(text);
                }
            }
            catch (Exception ex)
            {
                Log.Msg("Codex", $"Error extracting credits: {ex.Message}");
            }

            Log.Msg("Codex", $"Extracted {_creditsParagraphs.Count} credits paragraphs");
        }

        #endregion

        #region State Detection

        private bool IsCreditsActive()
        {
            var root = GetFieldGameObject(_codexCache.Handles.LearnToPlayRoot);
            if (root == null) return false;
            if (root.activeInHierarchy) return false;

            if (_codexCache.Handles.CreditsDisplay == null || _controller == null) return false;
            try
            {
                var creditsDisplay = _codexCache.Handles.CreditsDisplay.GetValue(_controller) as MonoBehaviour;
                return creditsDisplay != null && creditsDisplay.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        private bool IsContentActive()
        {
            var contentViewGo = GetFieldGameObject(_codexCache.Handles.ContentView);
            if (contentViewGo == null || !contentViewGo.activeInHierarchy) return false;

            // Check for LearnToPlayContents component (the active content prefab)
            foreach (var mb in contentViewGo.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb != null && mb.GetType().Name == "LearnToPlayContents")
                    return true;
            }
            return false;
        }

        #endregion

        #region Activation & Deactivation

        protected override void OnActivated()
        {
            _mode = CodexMode.TableOfContents;
            _tocIndex = 0;
            _navStack.Clear();
            _pendingDrillDown = false;
        }

        protected override void OnDeactivating()
        {
            _tocItems.Clear();
            _navStack.Clear();
            _contentParagraphs.Clear();
            _creditsParagraphs.Clear();
            _pendingDrillDown = false;
        }

        public override void OnSceneChanged(string sceneName)
        {
            _controller = null;
            _controllerGameObject = null;
            base.OnSceneChanged(sceneName);
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement()
        {
            return Strings.CodexActivation(_tocItems.Count);
        }

        protected override string GetElementAnnouncement(int index)
        {
            return "";
        }

        private void AnnounceTocItem()
        {
            if (_tocIndex < 0 || _tocIndex >= _tocItems.Count) return;

            var item = _tocItems[_tocIndex];
            string announcement;

            if (item.IsCategory && !item.IsStandalone)
            {
                // Category that opens a sub-list on Enter
                announcement = $"{item.Label}, {Strings.CodexSection}";
            }
            else
            {
                announcement = item.Label;
            }

            string pos = Strings.PositionOf(_tocIndex + 1, _tocItems.Count);
            if (pos != "") announcement += $", {pos}";
            _announcer.AnnounceInterrupt(announcement);
        }

        private void AnnounceContentBlock()
        {
            if (_contentIndex < 0 || _contentIndex >= _contentParagraphs.Count) return;

            string position = Strings.CodexContentBlock(_contentIndex + 1, _contentParagraphs.Count);
            _announcer.AnnounceInterrupt($"{_contentParagraphs[_contentIndex]}, {position}");
        }

        private void AnnounceCreditsBlock()
        {
            if (_creditsIndex < 0 || _creditsIndex >= _creditsParagraphs.Count) return;

            string position = Strings.CodexContentBlock(_creditsIndex + 1, _creditsParagraphs.Count);
            _announcer.AnnounceInterrupt($"{_creditsParagraphs[_creditsIndex]}, {position}");
        }

        #endregion

        #region Update Loop

        public override void Update()
        {
            if (!_isActive)
            {
                base.Update();
                return;
            }

            // Verify controller is still valid
            if (_controller == null || _controllerGameObject == null || !_controllerGameObject.activeInHierarchy)
            {
                Deactivate();
                return;
            }

            if (!IsControllerOpen(_controller))
            {
                Deactivate();
                return;
            }

            // Detect mode transitions
            if (_mode == CodexMode.TableOfContents)
            {
                if (IsContentActive())
                {
                    SwitchToContentMode();
                    return;
                }
                if (IsCreditsActive())
                {
                    SwitchToCreditsMode();
                    return;
                }
            }
            else if (_mode == CodexMode.Content)
            {
                if (!IsContentActive())
                {
                    ReturnToToc();
                    return;
                }
            }
            else if (_mode == CodexMode.Credits)
            {
                if (!IsCreditsActive())
                {
                    ReturnToToc();
                    return;
                }
            }

            HandleCodexInput();
        }

        protected override bool ValidateElements()
        {
            return _controller != null && _controllerGameObject != null && _controllerGameObject.activeInHierarchy;
        }

        #endregion

        #region Mode Switching

        private void SwitchToContentMode()
        {
            _mode = CodexMode.Content;
            ExtractContentParagraphs();

            if (_contentParagraphs.Count > 0)
            {
                _announcer.AnnounceInterrupt(Strings.CodexContentOpened(_contentParagraphs.Count));
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.CodexNoContent);
            }
        }

        private void SwitchToCreditsMode()
        {
            _mode = CodexMode.Credits;
            ExtractCreditsParagraphs();

            if (_creditsParagraphs.Count > 0)
            {
                _announcer.AnnounceInterrupt(
                    $"{Strings.CodexCreditsOpened}. {Strings.CodexContentBlock(1, _creditsParagraphs.Count)}: {_creditsParagraphs[0]}");
            }
            else
            {
                _announcer.AnnounceInterrupt(Strings.CodexCreditsOpened);
            }
        }

        /// <summary>
        /// Return to TOC mode from content/credits. Preserves current drill-down level and position.
        /// </summary>
        private void ReturnToToc()
        {
            _mode = CodexMode.TableOfContents;
            _contentParagraphs.Clear();
            _creditsParagraphs.Clear();

            // _tocItems and _tocIndex are preserved from before content was opened
            if (_tocItems.Count > 0 && _tocIndex >= 0 && _tocIndex < _tocItems.Count)
            {
                AnnounceTocItem();
            }
        }

        #endregion

        #region Drill-Down Navigation

        /// <summary>
        /// Drill into a category: push current items to stack, scan children as new list.
        /// Called after the game has had time to expand the section's children.
        /// </summary>
        private void DrillDown(MonoBehaviour parentSection, string parentLabel)
        {
            // Push current state to stack
            _navStack.Add(new TocLevel
            {
                Label = parentLabel,
                SelectedIndex = _tocIndex,
                Items = new List<TocItem>(_tocItems)
            });

            // Clear and scan children of the clicked section
            _tocItems.Clear();
            _tocIndex = 0;

            // First try: scan childAnchor of the parent section
            var childAnchor = GetChildAnchor(parentSection);
            if (childAnchor != null && HasActiveChildren(childAnchor.transform))
            {
                Log.Msg("Codex", $"DrillDown: scanning childAnchor '{childAnchor.name}' ({childAnchor.transform.childCount} direct children)");
                ScanContainerForItems(childAnchor.transform);
            }
            else
            {
                Log.Msg("Codex", $"DrillDown: no childAnchor (null={childAnchor == null}), trying tableOfContentsTopics");
            }

            // Also scan tableOfContentsTopics if childAnchor had no results
            // (secondary sub-categories put their children there instead of in childAnchor)
            if (_tocItems.Count == 0)
            {
                var topicsGo = GetFieldGameObject(_codexCache.Handles.TableOfContentsTopics);
                if (topicsGo != null && HasActiveChildren(topicsGo.transform))
                {
                    ScanContainerForItems(topicsGo.transform);
                }
            }

            Log.Msg("Codex", $"Drilled into '{parentLabel}': {_tocItems.Count} children, stack depth={_navStack.Count}");

            if (_tocItems.Count > 0)
            {
                // Announce: "CategoryName. FirstChild, 1 of N"
                var first = _tocItems[0];
                string firstLabel = first.IsCategory && !first.IsStandalone
                    ? $"{first.Label}, {Strings.CodexSection}"
                    : first.Label;
                string pos = Strings.PositionOf(1, _tocItems.Count);
                _announcer.AnnounceInterrupt($"{parentLabel}. {firstLabel}" + (pos != "" ? $", {pos}" : ""));
            }
            else
            {
                // No children found - pop back
                Log.Msg("Codex", $"No children found for '{parentLabel}', popping back");
                PopNavStack();
                _announcer.AnnounceInterrupt(Strings.CodexNoContent);
            }
        }

        /// <summary>
        /// Pop back to the parent level from the navigation stack.
        /// </summary>
        private void PopNavStack()
        {
            if (_navStack.Count == 0) return;

            var level = _navStack[_navStack.Count - 1];
            _navStack.RemoveAt(_navStack.Count - 1);

            _tocItems.Clear();
            _tocItems.AddRange(level.Items);
            _tocIndex = level.SelectedIndex;

            Log.Msg("Codex", $"Popped back to '{level.Label}' level, {_tocItems.Count} items, index={_tocIndex}");

            if (_tocIndex >= 0 && _tocIndex < _tocItems.Count)
            {
                AnnounceTocItem();
            }
        }

        #endregion

        #region Input Handling

        private void HandleTocInput()
        {
            if (_tocItems.Count == 0) return;

            // Up/Shift+Tab: Previous item
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                if (_tocIndex > 0)
                {
                    _tocIndex--;
                    AnnounceTocItem();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.BeginningOfList);
                }
                return;
            }

            // Down/Tab: Next item
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                if (_tocIndex < _tocItems.Count - 1)
                {
                    _tocIndex++;
                    AnnounceTocItem();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.EndOfList);
                }
                return;
            }

            // Home: Jump to first
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _tocIndex = 0;
                AnnounceTocItem();
                return;
            }

            // End: Jump to last
            if (Input.GetKeyDown(KeyCode.End))
            {
                _tocIndex = _tocItems.Count - 1;
                AnnounceTocItem();
                return;
            }

            // Enter: Activate selected TOC item
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivateTocItem();
                return;
            }

            // Backspace: Go back one level or navigate Home
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);

                if (_navStack.Count > 0)
                {
                    PopNavStack();
                }
                else
                {
                    NavigateToHome();
                }
                return;
            }
        }

        private void ActivateTocItem()
        {
            if (_tocIndex < 0 || _tocIndex >= _tocItems.Count) return;

            var item = _tocItems[_tocIndex];

            if (item.IsStandalone)
            {
                _announcer.AnnounceInterrupt(Strings.Activating(item.Label));
                UIActivator.Activate(item.ButtonGameObject);
                return;
            }

            // Click the button - game handles expand/open logic
            UIActivator.Activate(item.ButtonGameObject);

            if (item.IsCategory && item.SectionComponent != null)
            {
                // Category: schedule drill-down after game expands children
                _announcer.AnnounceInterrupt(Strings.Activating(item.Label));
                _pendingDrillDown = true;
                _drillDownTimer = 0.4f;
                _drillDownSection = item.SectionComponent;
                _drillDownLabel = item.Label;
            }
            // If not a category, Update() will detect content appearing via IsContentActive()
        }

        private void HandleContentInput()
        {
            // Up: Previous paragraph
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_contentParagraphs.Count == 0)
                {
                    _announcer.AnnounceInterrupt(Strings.CodexNoContent);
                    return;
                }

                if (_contentIndex > 0)
                {
                    _contentIndex--;
                    AnnounceContentBlock();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.BeginningOfList);
                }
                return;
            }

            // Down: Next paragraph
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_contentParagraphs.Count == 0)
                {
                    _announcer.AnnounceInterrupt(Strings.CodexNoContent);
                    return;
                }

                if (_contentIndex < _contentParagraphs.Count - 1)
                {
                    _contentIndex++;
                    AnnounceContentBlock();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.EndOfList);
                }
                return;
            }

            // Home: First paragraph
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_contentParagraphs.Count > 0)
                {
                    _contentIndex = 0;
                    AnnounceContentBlock();
                }
                return;
            }

            // End: Last paragraph
            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_contentParagraphs.Count > 0)
                {
                    _contentIndex = _contentParagraphs.Count - 1;
                    AnnounceContentBlock();
                }
                return;
            }

            // Backspace: Close content, return to TOC
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                CloseContent();
                return;
            }
        }

        private void HandleCreditsInput()
        {
            // Up: Previous credits block
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_creditsParagraphs.Count == 0) return;

                if (_creditsIndex > 0)
                {
                    _creditsIndex--;
                    AnnounceCreditsBlock();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.BeginningOfList);
                }
                return;
            }

            // Down: Next credits block
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_creditsParagraphs.Count == 0) return;

                if (_creditsIndex < _creditsParagraphs.Count - 1)
                {
                    _creditsIndex++;
                    AnnounceCreditsBlock();
                }
                else
                {
                    _announcer.AnnounceInterruptVerbose(Strings.EndOfList);
                }
                return;
            }

            // Home/End
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (_creditsParagraphs.Count > 0)
                {
                    _creditsIndex = 0;
                    AnnounceCreditsBlock();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (_creditsParagraphs.Count > 0)
                {
                    _creditsIndex = _creditsParagraphs.Count - 1;
                    AnnounceCreditsBlock();
                }
                return;
            }

            // Backspace: Close credits, return to TOC
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                CloseCredits();
                return;
            }
        }

        #endregion

        #region Close Content / Credits

        private void CloseContent()
        {
            // Find LearnToPlayContents component and click its backButton
            var contentViewGo = GetFieldGameObject(_codexCache.Handles.ContentView);
            if (contentViewGo != null)
            {
                foreach (var mb in contentViewGo.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null || mb.GetType().Name != "LearnToPlayContents") continue;

                    // Get backButton field from LearnToPlayContents
                    var backButtonField = mb.GetType().GetField("backButton",
                        AllInstanceFlags);
                    if (backButtonField != null)
                    {
                        try
                        {
                            var backBtn = backButtonField.GetValue(mb) as MonoBehaviour;
                            if (backBtn != null && backBtn.gameObject != null)
                            {
                                UIActivator.Activate(backBtn.gameObject);
                                return;
                            }
                        }
                        catch { /* Back button field may not exist on this component */ }
                    }

                    // Fallback: find CustomButton in content
                    var btn = FindCustomButton(mb.gameObject);
                    if (btn != null)
                    {
                        UIActivator.Activate(btn);
                        return;
                    }
                }
            }

            // Force return
            ReturnToToc();
        }

        private void CloseCredits()
        {
            if (_codexCache.Handles.CreditsDisplay != null && _controller != null)
            {
                try
                {
                    var creditsDisplay = _codexCache.Handles.CreditsDisplay.GetValue(_controller) as MonoBehaviour;
                    if (creditsDisplay != null)
                    {
                        var backBtn = FindCustomButton(creditsDisplay.gameObject);
                        if (backBtn != null)
                        {
                            UIActivator.Activate(backBtn);
                            return;
                        }

                        var button = creditsDisplay.GetComponentInChildren<UnityEngine.UI.Button>(false);
                        if (button != null && button.gameObject.activeInHierarchy)
                        {
                            UIActivator.SimulatePointerClick(button.gameObject);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Msg("Codex", $"Error closing credits: {ex.Message}");
                }
            }

            ReturnToToc();
        }

        #endregion

        #region Main Input Dispatch

        private void HandleCodexInput()
        {
            // Handle pending drill-down after clicking a category
            if (_pendingDrillDown)
            {
                _drillDownTimer -= Time.deltaTime;
                if (_drillDownTimer <= 0)
                {
                    _pendingDrillDown = false;
                    DrillDown(_drillDownSection, _drillDownLabel);
                    return;
                }
            }

            switch (_mode)
            {
                case CodexMode.TableOfContents:
                    HandleTocInput();
                    break;
                case CodexMode.Content:
                    HandleContentInput();
                    break;
                case CodexMode.Credits:
                    HandleCreditsInput();
                    break;
            }
        }

        #endregion
    }
}
