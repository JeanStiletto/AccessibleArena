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

        // Article blocks in reading order (paragraphs, example cards, link buttons)
        private readonly List<ContentBlock> _contentBlocks = new List<ContentBlock>();
        private int _contentIndex;

        // Credits paragraphs
        private readonly List<string> _creditsParagraphs = new List<string>();
        private int _creditsIndex;

        // Drill-down runs on the frame after the click. The game builds and activates the
        // children synchronously inside the button's click handler, so one frame is enough
        // for the new TOC sections to be active in the hierarchy.
        private bool _pendingDrillDown;
        private MonoBehaviour _drillDownSection; // the section component we clicked
        private string _drillDownLabel;

        // Credits: the game's "Universes Beyond" button scrolls to that section of the roll.
        // We mirror it as Enter, jumping our block cursor to the first block that contains the
        // search text the game itself uses.
        private GameObject _creditsJumpButton;
        private string _creditsJumpLabel;
        private string _creditsJumpSearchText;

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

        /// <summary>One announceable unit of an article. Link buttons carry the GameObject Enter activates.</summary>
        private struct ContentBlock
        {
            public string Text;
            public GameObject Link; // OpenUrlBehaviour button, null for plain text and cards
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
            public PropertyInfo TocShowNewFlag; // drives the game's unread badge

            // LearnMoreSection
            public Type LearnMoreSectionType;
            public FieldInfo SectionTitle;
            public FieldInfo SectionId;
            public FieldInfo ChildSections;

            // CreditsDisplay
            public FieldInfo CreditsUbButton;
            public FieldInfo CreditsUbSearchText;
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

                h.TocSectionType = FindType("Assets.Core.Meta.LearnMore.TableOfContentsSection");
                if (h.TocSectionType != null)
                    CacheTocSectionFields(h, h.TocSectionType);

                if (h.CreditsDisplay != null)
                {
                    h.CreditsUbButton = h.CreditsDisplay.FieldType.GetField("_universesBeyondButton", AllInstanceFlags);
                    h.CreditsUbSearchText = h.CreditsDisplay.FieldType.GetField("_ubSearchText", AllInstanceFlags);
                }

                h.LearnMoreSectionType = FindType("Wotc.Mtga.LearnMore.LearnMoreSection");
                if (h.LearnMoreSectionType != null)
                    CacheLearnMoreSectionFields(h, h.LearnMoreSectionType);

                return h;
            },
            validator: h => h.LearnToPlayRoot != null && h.TableOfContents != null,
            logTag: "Codex",
            logSubject: "LearnToPlayControllerV2");

        private static void CacheTocSectionFields(CodexHandles h, Type type)
        {
            h.TocSectionType = type;
            h.TocButton = type.GetField("button", AllInstanceFlags);
            h.TocIntent = type.GetField("buttonClickIntent", AllInstanceFlags);
            h.TocChildAnchor = type.GetField("childAnchor", AllInstanceFlags);
            h.TocSection = type.GetField("section", AllInstanceFlags);
            h.TocShowNewFlag = type.GetProperty("ShowNewFlag", PublicInstance);
        }

        private static void CacheLearnMoreSectionFields(CodexHandles h, Type type)
        {
            h.LearnMoreSectionType = type;
            h.SectionTitle = type.GetField("_title", AllInstanceFlags);
            h.SectionId = type.GetField("Id", PublicInstance);
            h.ChildSections = ReflectionWalk.FindField(type, "_childSections", AllInstanceFlags);
        }

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
            var h = _codexCache.Handles;
            CacheTocSectionFields(h, type);

            Log.Msg("Codex", $"Cached TOC section type from fallback: " +
                $"Button={h.TocButton != null}, Intent={h.TocIntent != null}, " +
                $"ChildAnchor={h.TocChildAnchor != null}, Section={h.TocSection != null}, NewFlag={h.TocShowNewFlag != null}");

            // Also cache LearnMoreSection type from the section field
            if (h.TocSection != null && h.LearnMoreSectionType == null)
            {
                CacheLearnMoreSectionFields(h, h.TocSection.FieldType);
                Log.Msg("Codex", $"Cached LearnMoreSection type: Title={h.SectionTitle != null}, Id={h.SectionId != null}");
            }
        }

        /// <summary>
        /// Whether the game shows its unread badge on this TOC entry. Read live at announce time:
        /// opening an article clears the flag, and a category's flag turns off once every child
        /// has been read. Locked articles never get here: the game hides them entirely and
        /// shows no locked marker to sighted players either.
        /// </summary>
        private bool IsUnread(TocItem item)
        {
            if (item.SectionComponent == null || _codexCache.Handles.TocShowNewFlag == null) return false;
            try
            {
                return (bool)_codexCache.Handles.TocShowNewFlag.GetValue(item.SectionComponent, null);
            }
            catch { return false; }
        }

        /// <summary>Label plus the "section" and "unread" markers, as spoken in the TOC.</summary>
        private string DescribeTocItem(TocItem item)
        {
            string text = item.Label;
            if (item.IsCategory && !item.IsStandalone)
                text += $", {Strings.CodexSection}";
            if (IsUnread(item))
                text += $", {Strings.CodexUnread}";
            return text;
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
            _contentBlocks.Clear();
            _contentIndex = 0;

            var contentViewGo = GetFieldGameObject(_codexCache.Handles.ContentView);
            if (contentViewGo == null || !contentViewGo.activeInHierarchy) return;

            var stats = new ContentStats();
            CollectContentBlocks(contentViewGo.transform, _contentBlocks, ref stats);

            Log.Msg("Codex", $"Extracted {_contentBlocks.Count} content blocks ({stats.Cards} example cards, {stats.Links} links)");
        }

        private struct ContentStats { public int Cards; public int Links; }

        /// <summary>
        /// Walks the article view in hierarchy order (which is the reading order) and collects
        /// one block per paragraph. Three things interrupt the plain-text walk, each becoming a
        /// single block in the place where a sighted reader sees it, and none is walked further:
        /// - a link button (OpenUrlBehaviour): "Link: label", activatable with Enter
        /// - an embedded example card: name, cost, type, P/T and rules text
        /// Only UI text (TextMeshProUGUI) counts as prose. World-space TextMeshPro belongs to the
        /// 3D mock cards of the animated demos (the "tapping" illustration shows a Plains) and
        /// would leak a stray card title into the article.
        /// </summary>
        private static void CollectContentBlocks(Transform node, List<ContentBlock> blocks, ref ContentStats stats)
        {
            if (!node.gameObject.activeSelf) return;

            if (IsUrlButton(node.gameObject))
            {
                stats.Links++;
                string label = CleanLabel(UITextExtractor.GetText(node.gameObject));
                if (string.IsNullOrEmpty(label)) label = node.gameObject.name;
                blocks.Add(new ContentBlock
                {
                    Text = $"{Strings.CodexLink(label)}, {Strings.PhysicalPrizeOpensBrowser}",
                    Link = node.gameObject
                });
                return;
            }

            if (IsCardDisplayRoot(node.gameObject))
            {
                stats.Cards++;
                blocks.Add(new ContentBlock { Text = Strings.CodexExampleCard(DescribeExampleCard(node.gameObject)) });
                return;
            }

            var tmp = node.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                foreach (var text in SplitTextBlocks(tmp.text))
                    blocks.Add(new ContentBlock { Text = text });
            }

            for (int i = 0; i < node.childCount; i++)
                CollectContentBlocks(node.GetChild(i), blocks, ref stats);
        }

        private static bool IsUrlButton(GameObject go)
        {
            bool hasUrl = false, hasButton = false;
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "OpenUrlBehaviour") hasUrl = true;
                else if (typeName == "CustomButton") hasButton = true;
            }
            return hasUrl && hasButton;
        }

        /// <summary>
        /// Turns one text component into announcement blocks: line-break tags become lines
        /// before the rich text goes (the splitter needs them for bullet lists), mana sprites
        /// become words, then the splitter groups lines. Blocks under three characters (stray
        /// glyphs such as the "=" of the mana demo) are dropped.
        /// </summary>
        private static List<string> SplitTextBlocks(string rawText)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(rawText)) return result;

            string text = LineBreakTag.Replace(rawText, "\n");
            text = CardDetector.ReplaceSpriteTagsWithText(text);
            foreach (var block in CodexTextSplitter.Split(text))
            {
                if (block.Length >= 3)
                    result.Add(block);
            }
            return result;
        }

        private static readonly Regex LineBreakTag = new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Is this GameObject the root of an embedded card display?</summary>
        private static bool IsCardDisplayRoot(GameObject go)
        {
            string goName = go.name;
            if (goName.Contains("CardAnchor") ||
                goName.Contains("MetaCardView") ||
                goName.Contains("DuelCardView") ||
                goName.Contains("CardRenderer"))
                return true;

            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName.Contains("CardView") ||
                    typeName.Contains("CardRenderer") ||
                    typeName.Contains("CDC"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Describes an example card the way the article shows it: name, mana cost, type line,
        /// power/toughness and rules text. The display root may wrap the actual card view, so
        /// the first descendant the card detector recognises is what gets read; falls back to
        /// the display's own text when no model is reachable.
        /// </summary>
        private static string DescribeExampleCard(GameObject displayRoot)
        {
            GameObject cardGo = CardDetector.IsCard(displayRoot) ? displayRoot : null;
            if (cardGo == null)
            {
                foreach (var t in displayRoot.GetComponentsInChildren<Transform>(false))
                {
                    if (CardDetector.IsCard(t.gameObject)) { cardGo = t.gameObject; break; }
                }
            }

            if (cardGo != null)
            {
                var info = CardDetector.ExtractCardInfo(cardGo);
                if (info.IsValid && !string.IsNullOrEmpty(info.Name))
                {
                    var head = new List<string> { info.Name };
                    if (!string.IsNullOrEmpty(info.ManaCost)) head.Add(info.ManaCost);
                    if (!string.IsNullOrEmpty(info.TypeLine)) head.Add(info.TypeLine);
                    if (!string.IsNullOrEmpty(info.PowerToughness)) head.Add(info.PowerToughness);
                    string description = string.Join(", ", head);
                    if (!string.IsNullOrEmpty(info.RulesText))
                        description += ". " + CleanLabel(info.RulesText);
                    return description;
                }
            }

            string text = CleanLabel(UITextExtractor.GetText(displayRoot));
            return string.IsNullOrEmpty(text) ? Strings.NPE_UnknownCard : text;
        }

        private void ExtractCreditsParagraphs()
        {
            _creditsParagraphs.Clear();
            _creditsIndex = 0;
            _creditsJumpButton = null;
            _creditsJumpLabel = null;
            _creditsJumpSearchText = null;

            if (_codexCache.Handles.CreditsDisplay == null || _controller == null) return;

            try
            {
                var creditsDisplay = _codexCache.Handles.CreditsDisplay.GetValue(_controller) as MonoBehaviour;
                if (creditsDisplay == null || !creditsDisplay.gameObject.activeInHierarchy) return;

                CacheCreditsJumpButton(creditsDisplay);

                // The whole roll is one TMP_Text; the jump button's own label is not part of it.
                foreach (var tmp in creditsDisplay.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (tmp == null || !tmp.gameObject.activeInHierarchy) continue;
                    if (_creditsJumpButton != null && tmp.transform.IsChildOf(_creditsJumpButton.transform)) continue;
                    _creditsParagraphs.AddRange(SplitTextBlocks(tmp.text));
                }
            }
            catch (Exception ex)
            {
                Log.Msg("Codex", $"Error extracting credits: {ex.Message}");
            }

            Log.Msg("Codex", $"Extracted {_creditsParagraphs.Count} credits blocks (jump button: {_creditsJumpLabel ?? "none"})");
        }

        private void CacheCreditsJumpButton(MonoBehaviour creditsDisplay)
        {
            var h = _codexCache.Handles;
            if (h.CreditsUbButton == null) return;

            var btn = h.CreditsUbButton.GetValue(creditsDisplay) as MonoBehaviour;
            if (btn == null || btn.gameObject == null || !btn.gameObject.activeInHierarchy) return;

            _creditsJumpButton = btn.gameObject;
            _creditsJumpLabel = CleanLabel(UITextExtractor.GetText(btn.gameObject));
            if (string.IsNullOrEmpty(_creditsJumpLabel)) _creditsJumpLabel = btn.gameObject.name;
            _creditsJumpSearchText = h.CreditsUbSearchText?.GetValue(creditsDisplay) as string;
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
            StartArticleDump();
        }

        protected override void OnDeactivating()
        {
            _tocItems.Clear();
            _navStack.Clear();
            _contentBlocks.Clear();
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

            string announcement = DescribeTocItem(_tocItems[_tocIndex]);
            string pos = Strings.PositionOf(_tocIndex + 1, _tocItems.Count);
            if (pos != "") announcement += $", {pos}";
            _announcer.AnnounceInterrupt(announcement);
        }

        private void AnnounceContentBlock()
        {
            if (_contentIndex < 0 || _contentIndex >= _contentBlocks.Count) return;

            string position = Strings.CodexContentBlock(_contentIndex + 1, _contentBlocks.Count);
            _announcer.AnnounceInterrupt($"{_contentBlocks[_contentIndex].Text}, {position}");
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

            // Diagnostic article dump owns the screen while it runs
            if (StepArticleDump())
                return;

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

            if (_contentBlocks.Count > 0)
            {
                _announcer.AnnounceInterrupt(Strings.CodexContentOpened(_contentBlocks.Count));
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

            string opened = Strings.CodexCreditsOpened;
            if (_creditsJumpButton != null)
                opened += $". {Strings.CodexCreditsJumpHint(_creditsJumpLabel)}";

            if (_creditsParagraphs.Count > 0)
            {
                _announcer.AnnounceInterrupt(
                    $"{opened}. {Strings.CodexContentBlock(1, _creditsParagraphs.Count)}: {_creditsParagraphs[0]}");
            }
            else
            {
                _announcer.AnnounceInterrupt(opened);
            }
        }

        /// <summary>
        /// Enter in credits: the game's jump button scrolls the roll to the Universes Beyond
        /// section. Click it so the view follows, and move our cursor to the first block that
        /// contains the game's own search text.
        /// </summary>
        private void JumpToCreditsSection()
        {
            if (_creditsJumpButton == null)
            {
                _announcer.AnnounceInterrupt(Strings.NoAlternateAction);
                return;
            }

            UIActivator.Activate(_creditsJumpButton);

            int target = -1;
            if (!string.IsNullOrEmpty(_creditsJumpSearchText))
                target = _creditsParagraphs.FindIndex(b => b.IndexOf(_creditsJumpSearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            if (target < 0)
            {
                Log.Msg("Codex", $"Credits jump: search text '{_creditsJumpSearchText}' not found in {_creditsParagraphs.Count} blocks");
                _announcer.AnnounceInterrupt(_creditsJumpLabel);
                return;
            }

            _creditsIndex = target;
            AnnounceCreditsBlock();
        }

        /// <summary>
        /// Return to TOC mode from content/credits. Preserves current drill-down level and position.
        /// </summary>
        private void ReturnToToc()
        {
            _mode = CodexMode.TableOfContents;
            _contentBlocks.Clear();
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
                string firstLabel = DescribeTocItem(_tocItems[0]);
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
            if (KeyInput.GetKeyDown(KeyCode.UpArrow) ||
                (KeyInput.GetKeyDown(KeyCode.Tab) && (KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift))))
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
            if (KeyInput.GetKeyDown(KeyCode.DownArrow) ||
                (KeyInput.GetKeyDown(KeyCode.Tab) && !KeyInput.GetKey(KeyCode.LeftShift) && !KeyInput.GetKey(KeyCode.RightShift)))
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
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                _tocIndex = 0;
                AnnounceTocItem();
                return;
            }

            // End: Jump to last
            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                _tocIndex = _tocItems.Count - 1;
                AnnounceTocItem();
                return;
            }

            // Enter: Activate selected TOC item
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                ActivateTocItem();
                return;
            }

            // Backspace: Go back one level or navigate Home
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
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
                // Category: drill down next frame, once the game's click handler has built
                // and activated the children (no announcement in between - DrillDown speaks)
                _pendingDrillDown = true;
                _drillDownSection = item.SectionComponent;
                _drillDownLabel = item.Label;
            }
            // If not a category, Update() will detect content appearing via IsContentActive()
        }

        private void HandleContentInput()
        {
            // Up: Previous block
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                if (_contentBlocks.Count == 0)
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

            // Down: Next block
            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                if (_contentBlocks.Count == 0)
                {
                    _announcer.AnnounceInterrupt(Strings.CodexNoContent);
                    return;
                }

                if (_contentIndex < _contentBlocks.Count - 1)
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

            // Home: First block
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                if (_contentBlocks.Count > 0)
                {
                    _contentIndex = 0;
                    AnnounceContentBlock();
                }
                return;
            }

            // End: Last block
            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                if (_contentBlocks.Count > 0)
                {
                    _contentIndex = _contentBlocks.Count - 1;
                    AnnounceContentBlock();
                }
                return;
            }

            // Enter: follow a link block (the game's own button opens the web browser)
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                if (_contentIndex >= 0 && _contentIndex < _contentBlocks.Count && _contentBlocks[_contentIndex].Link != null)
                {
                    _announcer.AnnounceInterrupt(Strings.Activating(_contentBlocks[_contentIndex].Text));
                    UIActivator.Activate(_contentBlocks[_contentIndex].Link);
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.NoAlternateAction);
                }
                return;
            }

            // Backspace: Close content, return to TOC
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                CloseContent();
                return;
            }
        }

        private void HandleCreditsInput()
        {
            // Up: Previous credits block
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
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
            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
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
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                if (_creditsParagraphs.Count > 0)
                {
                    _creditsIndex = 0;
                    AnnounceCreditsBlock();
                }
                return;
            }

            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                if (_creditsParagraphs.Count > 0)
                {
                    _creditsIndex = _creditsParagraphs.Count - 1;
                    AnnounceCreditsBlock();
                }
                return;
            }

            // Enter: jump to the Universes Beyond section (the game's only control in the roll)
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                InputManager.ConsumeKey(KeyCode.Return);
                InputManager.ConsumeKey(KeyCode.KeypadEnter);
                JumpToCreditsSection();
                return;
            }

            // Backspace: Close credits, return to TOC
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
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

        #region Article Dump (diagnostic)

        // Diagnostic: on activation, open every visible article once through the game's own
        // OpenContent, log the article view's hierarchy plus the blocks we would announce, then
        // close it again. Runs once per session and suspends input while it works.
        // Off in release; flip on to re-check the article prefabs after a game update.
        private const bool DumpArticlesOnActivate = false;
        private const string DumpTag = "CodexDump";
        private const int DumpSettleFrames = 3;

        private static bool _dumpDone;
        private readonly List<object> _dumpQueue = new List<object>();
        private int _dumpIndex = -1;
        private int _dumpWaitFrames;
        private MethodInfo _dumpOpenContent, _dumpHideActiveContents, _dumpShowTocSection, _dumpHideTocSection;

        private void StartArticleDump()
        {
            if (!DumpArticlesOnActivate || _dumpDone || _controller == null) return;
            _dumpDone = true;

            try
            {
                var ct = _controller.GetType();
                _dumpOpenContent = ct.GetMethod("OpenContent", AllInstanceFlags);
                _dumpHideActiveContents = ct.GetMethod("HideActiveContents", AllInstanceFlags);
                _dumpShowTocSection = ct.GetMethod("ShowTableOfContentsSection", AllInstanceFlags);
                _dumpHideTocSection = ct.GetMethod("HideTableOfContentsSection", AllInstanceFlags);
                var hierarchy = ct.GetField("_hierarchy", AllInstanceFlags)?.GetValue(_controller);
                var getAll = hierarchy?.GetType().GetMethod("Get", PublicInstance);

                if (_dumpOpenContent == null || _dumpHideActiveContents == null || _dumpShowTocSection == null ||
                    _dumpHideTocSection == null || getAll == null)
                {
                    Log.Msg(DumpTag, $"Aborted: OpenContent={_dumpOpenContent != null} Hide={_dumpHideActiveContents != null} " +
                        $"ShowToc={_dumpShowTocSection != null} HideToc={_dumpHideTocSection != null} hierarchy={hierarchy != null} Get={getAll != null}");
                    return;
                }

                _dumpQueue.Clear();
                foreach (var refs in (System.Collections.IEnumerable)getAll.Invoke(hierarchy, null))
                {
                    var rt = refs.GetType();
                    string path = rt.GetProperty("Path", PublicInstance)?.GetValue(refs, null) as string;
                    int depth = (rt.GetField("Ancestors", PublicInstance)?.GetValue(refs) as Array)?.Length ?? -1;
                    bool show = rt.GetField("Show", PublicInstance)?.GetValue(refs) is bool s && s;
                    bool accessible = rt.GetField("IsSelfAccessible", PublicInstance)?.GetValue(refs) is bool a && a;
                    bool hasChildren = rt.GetProperty("HasChildren", PublicInstance)?.GetValue(refs, null) is bool hc && hc;
                    bool isNew = rt.GetProperty("ShowNewFlag", PublicInstance)?.GetValue(refs, null) is bool n && n;
                    Log.Msg(DumpTag, $"Section '{path}' depth={depth} show={show} accessible={accessible} hasChildren={hasChildren} new={isNew}");
                    if (show && !hasChildren) _dumpQueue.Add(refs);
                }

                Log.Msg(DumpTag, $"Queued {_dumpQueue.Count} articles");
                _dumpIndex = 0;
                _dumpWaitFrames = 0;
            }
            catch (Exception ex)
            {
                Log.Msg(DumpTag, $"Start failed: {ex}");
                _dumpQueue.Clear();
                _dumpIndex = -1;
            }
        }

        /// <summary>Advances the dump one frame. True while the dump owns the screen.</summary>
        private bool StepArticleDump()
        {
            if (_dumpIndex < 0) return false;
            if (_dumpIndex >= _dumpQueue.Count)
            {
                Log.Msg(DumpTag, "Complete");
                _dumpQueue.Clear();
                _dumpIndex = -1;
                return false;
            }

            var refs = _dumpQueue[_dumpIndex];
            if (_dumpWaitFrames == 0)
            {
                try
                {
                    // OpenContent dereferences the section's TOC entry; depth-2 entries are only
                    // built on demand, so make sure it exists first (as the game does on click).
                    _dumpShowTocSection.Invoke(_controller, new[] { refs });
                    _dumpOpenContent.Invoke(_controller, new[] { refs });
                }
                catch (Exception ex)
                {
                    Log.Msg(DumpTag, $"Open failed for article {_dumpIndex}: {ex.InnerException?.Message ?? ex.Message}");
                    _dumpIndex++;
                    return true;
                }
                _dumpWaitFrames = 1;
                return true;
            }

            if (_dumpWaitFrames <= DumpSettleFrames)
            {
                _dumpWaitFrames++;
                return true;
            }

            DumpArticle(refs);

            try
            {
                _dumpHideActiveContents.Invoke(_controller, null);
                _dumpHideTocSection.Invoke(_controller, new[] { refs });
            }
            catch (Exception ex)
            {
                Log.Msg(DumpTag, $"Close failed for article {_dumpIndex}: {ex.InnerException?.Message ?? ex.Message}");
            }

            _dumpIndex++;
            _dumpWaitFrames = 0;
            return true;
        }

        private void DumpArticle(object refs)
        {
            string path = refs.GetType().GetProperty("Path", PublicInstance)?.GetValue(refs, null) as string;
            Log.Msg(DumpTag, $"=== Article '{path}' ===");

            var contentViewGo = GetFieldGameObject(_codexCache.Handles.ContentView);
            if (contentViewGo == null)
            {
                Log.Msg(DumpTag, "contentView is null");
                return;
            }

            DumpTree(contentViewGo.transform, 0);

            var blocks = new List<ContentBlock>();
            var stats = new ContentStats();
            CollectContentBlocks(contentViewGo.transform, blocks, ref stats);
            Log.Msg(DumpTag, $"--- {blocks.Count} blocks, {stats.Cards} example cards, {stats.Links} links ---");
            for (int i = 0; i < blocks.Count; i++)
                Log.Msg(DumpTag, $"block {i + 1}/{blocks.Count}: {blocks[i].Text}");
        }

        private static void DumpTree(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            if (!t.gameObject.activeSelf)
            {
                if (depth <= 2) Log.Msg(DumpTag, $"{indent}{t.name} [inactive]");
                return;
            }

            var comps = new List<string>();
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform || c is CanvasRenderer) continue;
                comps.Add(c.GetType().Name);
            }

            string text = "";
            var tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
            {
                text = tmp.text.Replace("\r", "").Replace("\n", "\\n");
                if (text.Length > 160) text = text.Substring(0, 160) + "…";
                text = $" text='{text}'";
            }

            Log.Msg(DumpTag, $"{indent}{t.name} [{string.Join(", ", comps)}]{text}");

            for (int i = 0; i < t.childCount; i++)
                DumpTree(t.GetChild(i), depth + 1);
        }

        #endregion

        #region Main Input Dispatch

        private void HandleCodexInput()
        {
            // Drill-down scheduled by last frame's category click
            if (_pendingDrillDown)
            {
                _pendingDrillDown = false;
                DrillDown(_drillDownSection, _drillDownLabel);
                return;
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
