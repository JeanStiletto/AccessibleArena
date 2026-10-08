using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using AccessibleArena.Patches;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the inbox (nav bar mail button), shaped like <see cref="StoreNavigator"/>:
    ///
    ///   Tabs → (Enter) → Letters → (Enter) → Letter
    ///
    /// Since MTGA 2026.63.10 the inbox has two tabs, "MTG: Arena Inbox" and "Store Messages"
    /// (Wizards Play Network). Opening the inbox lands on the tabs, focused on the selected one;
    /// Enter on a tab switches to it and opens its letters, Backspace goes back up. When the game
    /// hides the tab strip (WPNFavoriteStoreStatus feature toggle off) the Tabs level is skipped.
    ///
    /// The game fills letter lists asynchronously and fires no panel event. Lists are polled and
    /// refreshed silently: nothing is announced unless the user acted, or the element they were
    /// on disappeared.
    /// </summary>
    public class MailboxNavigator : BaseNavigator
    {
        #region Constants

        // Above every screen with a nav bar the inbox can open over (Store 55, Profile 56,
        // Mastery 60), below the reward popup that claiming a letter opens (86).
        private const int MailboxPriority = 62;

        private const string MailboxPanelName = "ContentController - Mailbox_Base(Clone)";
        private const string NavBarName = "NavBar_Desktop_16x9(Clone)";
        private const string LetterListType = "PlayerInboxLetterList";
        private const string LetterItemType = "PlayerInboxBladeItemDisplay";

        private const float DetectIntervalSeconds = 0.25f;
        private const float ListPollSeconds = 0.5f;
        private const float TabLoadPollSeconds = 0.1f;
        private const float TabLoadTimeoutSeconds = 5f;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Mailbox";
        public override string ScreenName => Strings.ScreenMailbox;
        public override int Priority => MailboxPriority;
        protected override bool SupportsCardNavigation => false;

        #endregion

        #region State

        private enum NavLevel { Tabs, Letters, Letter }

        private NavLevel _navLevel = NavLevel.Letters;

        private GameObject _panel;                  // ContentController - Mailbox_Base(Clone)
        private float _detectTimer;

        private readonly List<GameObject> _tabHeaders = new List<GameObject>();

        // Enter on a tab that was not current: wait for the game to fill its list.
        private bool _waitingForTabLoad;
        private float _tabLoadElapsed;
        private float _tabLoadPollTimer;

        // PanelStatePatch reports a letter opening; its texts are built in the same frame,
        // so the level switch runs on the next Update.
        private bool _letterOpened;

        // Set when the inbox panel is found anew; it then opens on the tabs.
        private bool _freshlyOpened;

        // "Loading" is only worth saying if the list takes a moment.
        private const float LoadingAnnounceDelaySeconds = 0.5f;
        private bool _loadingAnnounced;
        private string _pendingTabName;

        private string _listSignature;
        private float _listPollTimer;

        #endregion

        #region Reflection Cache

        private sealed class MailboxHandles
        {
            public MethodInfo CloseOpenLetter;      // ContentControllerPlayerInbox.CloseOpenLetter()
            public Type TabPopulatorType;           // PlayerInboxTabPopulator (a TabController)
            public PropertyInfo Tabs;               // TabController.Tabs (IReadOnlyList<TabEntry>)
            public PropertyInfo TabEntryHeader;     // TabEntry.Header
            public Type ContentViewType;            // PlayerInboxContentView
            public FieldInfo LetterInstance;        // PlayerInboxContentView._letterInstance
        }

        private static readonly ReflectionCache<MailboxHandles> _cache = new ReflectionCache<MailboxHandles>(
            builder: inboxType =>
            {
                var h = new MailboxHandles
                {
                    CloseOpenLetter = inboxType.GetMethod("CloseOpenLetter", PublicInstance, null, Type.EmptyTypes, null),
                    TabPopulatorType = FindType("Wotc.Mtga.Wrapper.Mailbox.PlayerInboxTabPopulator"),
                    ContentViewType = FindType(T.PlayerInboxContentViewFQ),
                };
                if (h.TabPopulatorType != null)
                    h.Tabs = h.TabPopulatorType.GetProperty("Tabs", PublicInstance);
                var entryType = FindType("Wizards.GeneralUtilities.TabSystem.TabEntry");
                if (entryType != null)
                    h.TabEntryHeader = entryType.GetProperty("Header", PublicInstance);
                if (h.ContentViewType != null)
                    h.LetterInstance = h.ContentViewType.GetField("_letterInstance", PrivateInstance);
                return h;
            },
            validator: h => h.CloseOpenLetter != null && h.LetterInstance != null,
            logTag: "Mailbox",
            logSubject: "ContentControllerPlayerInbox");

        private static MailboxHandles H => _cache.Handles;

        private static Type _inboxType;

        #endregion

        #region Constructor

        public MailboxNavigator(IAnnouncementService announcer) : base(announcer)
        {
            PanelStatePatch.OnMailLetterSelected += OnMailLetterSelected;
        }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            if (_panel != null && _panel.activeInHierarchy) return EnsureHandles();
            _panel = null;

            // Polled every frame while any lower-priority navigator is active.
            _detectTimer -= Time.deltaTime;
            if (_detectTimer > 0) return false;
            _detectTimer = DetectIntervalSeconds;

            var panel = GameObject.Find(MailboxPanelName);
            if (panel == null || !panel.activeInHierarchy) return false;
            _panel = panel;
            _freshlyOpened = true;
            return EnsureHandles();
        }

        private bool EnsureHandles()
        {
            if (_inboxType == null)
                _inboxType = FindType(T.ContentControllerPlayerInboxFQ);
            return _inboxType != null && _cache.EnsureInitialized(_inboxType);
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            DiscoverTabs();

            if (IsLetterOpen())
                _navLevel = NavLevel.Letter;
            else if (_freshlyOpened)
                _navLevel = _tabHeaders.Count > 0 ? NavLevel.Tabs : NavLevel.Letters;
            else if (_navLevel == NavLevel.Letter || (_navLevel == NavLevel.Tabs && _tabHeaders.Count == 0))
                _navLevel = NavLevel.Letters;
            _freshlyOpened = false;

            PopulateLevel();
        }

        private void PopulateLevel()
        {
            _elements.Clear();
            switch (_navLevel)
            {
                case NavLevel.Tabs:
                    foreach (var header in _tabHeaders)
                        AddElement(header, GetTabName(header), default, null, null, UIElementClassifier.ElementRole.Tab);
                    break;
                case NavLevel.Letters:
                    AddLetterListElements();
                    _listSignature = GetListSignature();
                    break;
                case NavLevel.Letter:
                    AddLetterElements();
                    break;
            }

            // BaseNavigator needs one element to stay active; an inbox still loading has none.
            if (_elements.Count == 0 && _panel != null)
                AddElement(_panel, ScreenName);
        }

        /// <summary>Visible tab headers, in strip order. Empty when the game hides the strip.</summary>
        private void DiscoverTabs()
        {
            _tabHeaders.Clear();
            var populator = FindTabPopulator();
            if (populator == null || H.Tabs == null || H.TabEntryHeader == null) return;

            try
            {
                if (!(H.Tabs.GetValue(populator) is IEnumerable tabs)) return;
                foreach (var entry in tabs)
                {
                    var header = entry != null ? H.TabEntryHeader.GetValue(entry) as Component : null;
                    if (header != null && header.gameObject.activeInHierarchy)
                        _tabHeaders.Add(header.gameObject);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Reading inbox tabs failed: {ex.Message}");
            }
        }

        private Component FindTabPopulator() =>
            _panel != null && H.TabPopulatorType != null ? _panel.GetComponentInChildren(H.TabPopulatorType, true) : null;

        private int CurrentTabIndex()
        {
            for (int i = 0; i < _tabHeaders.Count; i++)
            {
                if (UIElementClassifier.TryGetTabHeaderState(_tabHeaders[i], out bool selected, out _) && selected)
                    return i;
            }
            return -1;
        }

        private string GetTabName(GameObject header) => UITextExtractor.GetText(header);

        private string CurrentListName()
        {
            int tab = CurrentTabIndex();
            return tab >= 0 ? GetTabName(_tabHeaders[tab]) : ScreenName;
        }

        /// <summary>
        /// The letter list of the current tab: each letter, or — when empty — the game's notice
        /// ("No Messages.", or why there are no store messages) followed by its button.
        /// </summary>
        private void AddLetterListElements()
        {
            foreach (var list in GetActiveLetterLists())
            {
                foreach (Transform child in list.transform)
                {
                    if (!child.gameObject.activeInHierarchy) continue;

                    if (HasComponentNamed(child.gameObject, LetterItemType))
                    {
                        var button = FindCustomButton(child);
                        if (button != null)
                            AddElement(button, UITextExtractor.GetText(button));
                        continue;
                    }
                    AddPlaceholderContent(child);
                }
            }
        }

        private void AddPlaceholderContent(Transform node)
        {
            if (!node.gameObject.activeInHierarchy) return;

            if (node.GetComponent<Selectable>() != null)
            {
                var classification = UIElementClassifier.Classify(node.gameObject);
                AddElement(node.gameObject, BuildElementLabel(classification), default, null, null, classification.Role);
                return;
            }

            var tmp = node.GetComponent<TMP_Text>();
            if (tmp != null)
                AddTextBlock(UITextExtractor.CleanText(tmp.text));

            foreach (Transform child in node)
                AddPlaceholderContent(child);
        }

        /// <summary>
        /// An open letter: title, date and body as text, then its buttons (claim, more info).
        /// The close button carries no visible text and is covered by Backspace.
        /// </summary>
        private void AddLetterElements()
        {
            var parts = UITextExtractor.GetMailContentParts();
            if (!string.IsNullOrEmpty(parts.Title)) AddTextBlock($"Title: {parts.Title}");
            if (!string.IsNullOrEmpty(parts.Date)) AddTextBlock($"Date: {parts.Date}");
            if (!string.IsNullOrEmpty(parts.Body)) AddTextBlock($"Body: {parts.Body}");

            var letter = GetLetterInstance();
            if (letter == null) return;
            foreach (var mb in letter.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb == null || mb.GetType().Name != T.CustomButton) continue;
                if (!UITextExtractor.HasActualText(mb.gameObject)) continue;
                var classification = UIElementClassifier.Classify(mb.gameObject);
                AddElement(mb.gameObject, BuildElementLabel(classification), default, null, null, classification.Role);
            }
        }

        private List<MonoBehaviour> GetActiveLetterLists()
        {
            var lists = new List<MonoBehaviour>();
            if (_panel == null) return lists;
            foreach (var mb in _panel.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb != null && mb.GetType().Name == LetterListType)
                    lists.Add(mb);
            }
            return lists;
        }

        /// <summary>Children of the visible lists; changes when letters load, tabs switch or the notice swaps.</summary>
        private string GetListSignature()
        {
            var parts = new List<string>();
            foreach (var list in GetActiveLetterLists())
            {
                parts.Add(list.transform.parent?.parent?.name ?? list.name);
                foreach (Transform child in list.transform)
                {
                    if (child.gameObject.activeSelf)
                        parts.Add(child.GetInstanceID().ToString());
                }
            }
            return string.Join("|", parts);
        }

        private int CountLetters()
        {
            int count = 0;
            foreach (var list in GetActiveLetterLists())
                foreach (Transform child in list.transform)
                    if (child.gameObject.activeInHierarchy && HasComponentNamed(child.gameObject, LetterItemType))
                        count++;
            return count;
        }

        private bool ListHasContent()
        {
            foreach (var list in GetActiveLetterLists())
                foreach (Transform child in list.transform)
                    if (child.gameObject.activeInHierarchy)
                        return true;
            return false;
        }

        private GameObject GetLetterInstance()
        {
            if (_panel == null || H?.ContentViewType == null) return null;
            var view = _panel.GetComponentInChildren(H.ContentViewType, true);
            return view != null ? H.LetterInstance.GetValue(view) as GameObject : null;
        }

        private bool IsLetterOpen()
        {
            var letter = GetLetterInstance();
            return letter != null && letter.activeInHierarchy;
        }

        private static GameObject FindCustomButton(Transform root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (mb != null && mb.GetType().Name == T.CustomButton)
                    return mb.gameObject;
            }
            return null;
        }

        private static bool HasComponentNamed(GameObject go, string typeName)
        {
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == typeName)
                    return true;
            }
            return false;
        }

        #endregion

        #region Level Transitions

        protected override bool OnElementActivated(int index, GameObject element)
        {
            switch (_navLevel)
            {
                case NavLevel.Tabs:
                    ActivateTab(index);
                    return true;
                case NavLevel.Letters:
                    // A letter opens through the game; OnMailLetterSelected announces it.
                    if (element.transform.parent != null && HasComponentNamed(element.transform.parent.gameObject, LetterItemType))
                    {
                        UIActivator.Activate(element);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private void ActivateTab(int index)
        {
            if (index < 0 || index >= _tabHeaders.Count) return;
            var header = _tabHeaders[index];

            // The tab is a real Button holding EventSystem focus, so the game's own submit may
            // already have switched it on this Enter press. Either way the previous tab's letters
            // are only destroyed at the end of the frame: never read the list in this frame.
            UIElementClassifier.TryGetTabHeaderState(header, out bool selected, out _);
            if (!selected)
                UIActivator.Activate(header);

            StartWaitingForList(GetTabName(header));
        }

        private void StartWaitingForList(string listName)
        {
            _pendingTabName = listName;
            _loadingAnnounced = false;
            _waitingForTabLoad = true;
            _tabLoadElapsed = 0f;
            _tabLoadPollTimer = TabLoadPollSeconds;
        }

        private void EnterLetters()
        {
            _waitingForTabLoad = false;
            _navLevel = NavLevel.Letters;
            PopulateLevel();
            _currentIndex = 0;
            _currentActionIndex = 0;
            _announcer.AnnounceInterrupt($"{LettersSummary()}. {GetElementAnnouncement(_currentIndex)}");
        }

        private string LettersSummary() => Strings.MailboxTabLetters(CurrentListName(), CountLetters());

        private void ReturnToTabs()
        {
            _navLevel = NavLevel.Tabs;
            DiscoverTabs();
            PopulateLevel();
            int current = CurrentTabIndex();
            _currentIndex = current >= 0 ? current : 0;
            _currentActionIndex = 0;
            // Tabs_Format is a full sentence ("Tabs. 2 tabs.") shared with the store.
            _announcer.AnnounceInterrupt($"{Strings.TabsCount(_tabHeaders.Count)} {GetElementAnnouncement(_currentIndex)}");
        }

        private void OnMailLetterSelected(Guid letterId, string title, string body, bool hasAttachments, bool isClaimed)
        {
            if (!_isActive) return;
            _letterOpened = true;
        }

        private void EnterLetter()
        {
            _letterOpened = false;
            _navLevel = NavLevel.Letter;
            PopulateLevel();
            _currentIndex = 0;
            _currentActionIndex = 0;
            AnnounceCurrentElement();
        }

        private void CloseLetter()
        {
            try
            {
                var inbox = _panel != null ? _panel.GetComponent(_inboxType) : null;
                if (inbox != null) H.CloseOpenLetter.Invoke(inbox, null);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"CloseOpenLetter failed: {ex.InnerException?.Message ?? ex.Message}");
            }

            _navLevel = NavLevel.Letters;
            PopulateLevel();
            _currentIndex = 0;
            _currentActionIndex = 0;
            _announcer.AnnounceInterrupt($"{Strings.BackToMailList}. {GetElementAnnouncement(_currentIndex)}");
        }

        /// <summary>Closes the inbox the way the nav bar does (NavBarController.HideInboxIfActive).</summary>
        private void CloseMailbox()
        {
            var navBar = GameObject.Find(NavBarName);
            if (navBar != null)
            {
                foreach (var mb in navBar.GetComponents<MonoBehaviour>())
                {
                    if (mb == null || mb.GetType().Name != "NavBarController") continue;
                    var method = mb.GetType().GetMethod("HideInboxIfActive", AllInstanceFlags);
                    if (method == null) break;
                    try
                    {
                        method.Invoke(mb, null);
                        _announcer.Announce(Strings.NavigatingBack, AnnouncementPriority.High);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("{NavigatorId}", $"HideInboxIfActive failed: {ex.InnerException?.Message ?? ex.Message}");
                    }
                    break;
                }
            }

            // Fallback: the blade's own dismiss button.
            var dismiss = _panel != null ? FindChildByName(_panel.transform, "Blade_DismissButton") : null;
            if (dismiss != null)
            {
                _announcer.Announce(Strings.NavigatingBack, AnnouncementPriority.High);
                UIActivator.Activate(dismiss);
            }
        }

        #endregion

        #region Input

        protected override bool HandleCustomInput()
        {
            if (!KeyInput.GetKeyDown(KeyCode.Backspace)) return false;

            switch (_navLevel)
            {
                case NavLevel.Letter:
                    CloseLetter();
                    break;
                case NavLevel.Letters:
                    _waitingForTabLoad = false;
                    if (_tabHeaders.Count > 0) ReturnToTabs();
                    else CloseMailbox();
                    break;
                default:
                    CloseMailbox();
                    break;
            }
            return true;
        }

        #endregion

        #region Update

        public override void Update()
        {
            base.Update();
            if (!_isActive) return;

            if (_letterOpened)
            {
                EnterLetter();
                return;
            }

            if (_waitingForTabLoad)
            {
                PollTabLoad();
                return;
            }

            if (_navLevel == NavLevel.Letters)
                PollListChanges();
        }

        private void PollTabLoad()
        {
            _tabLoadElapsed += Time.deltaTime;
            _tabLoadPollTimer -= Time.deltaTime;
            if (_tabLoadPollTimer > 0) return;
            _tabLoadPollTimer = TabLoadPollSeconds;

            if (ListHasContent() || _tabLoadElapsed > TabLoadTimeoutSeconds)
            {
                EnterLetters();
                return;
            }

            if (!_loadingAnnounced && _tabLoadElapsed > LoadingAnnounceDelaySeconds)
            {
                _loadingAnnounced = true;
                _announcer.AnnounceInterrupt(Strings.Loading(_pendingTabName ?? CurrentListName()));
            }
        }

        /// <summary>
        /// Letters arrive, are marked read or swap for a notice without any event. Refresh in
        /// place and only speak when the element the user was on is gone.
        /// </summary>
        private void PollListChanges()
        {
            _listPollTimer -= Time.deltaTime;
            if (_listPollTimer > 0) return;
            _listPollTimer = ListPollSeconds;

            string signature = GetListSignature();
            if (signature == _listSignature) return;

            var focused = IsValidIndex ? _elements[_currentIndex].GameObject : null;
            int oldIndex = _currentIndex;
            PopulateLevel();
            Log.Msg("{NavigatorId}", $"Letter list changed, {_elements.Count} elements");

            int restored = focused != null ? _elements.FindIndex(e => e.GameObject == focused) : -1;
            if (restored >= 0)
            {
                _currentIndex = restored;
                return;
            }
            _currentIndex = Math.Max(0, Math.Min(oldIndex, _elements.Count - 1));
            _currentActionIndex = 0;
            AnnounceCurrentElement();
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement()
        {
            switch (_navLevel)
            {
                case NavLevel.Tabs:
                    return Strings.WithHint($"{ScreenName}. {GetElementAnnouncement(_currentIndex)}", "MailboxTabsHint");
                case NavLevel.Letter:
                    return $"{ScreenName}. {GetElementAnnouncement(_currentIndex)}";
                default:
                    string core = ListHasContent()
                        ? $"{ScreenName}. {LettersSummary()}. {GetElementAnnouncement(_currentIndex)}"
                        : $"{ScreenName}. {Strings.Loading(CurrentListName())}";
                    return Strings.WithHint(core, "MailboxActivationHint");
            }
        }

        protected override string GetElementAnnouncement(int index)
        {
            if (_navLevel != NavLevel.Tabs || index < 0 || index >= _tabHeaders.Count)
                return base.GetElementAnnouncement(index);

            UIElementClassifier.TryGetTabHeaderState(_tabHeaders[index], out bool selected, out bool hasNew);
            string label = GetTabName(_tabHeaders[index]);
            if (selected) label += $", {Strings.Selected}";
            if (hasNew) label += $", {Strings.TabNew}";
            return Strings.TabPositionOf(index + 1, _tabHeaders.Count, label);
        }

        public override string GetTutorialHint() => LocaleManager.Instance.Get("MailboxHint");

        #endregion

        #region Lifecycle

        protected override void OnActivated()
        {
            _waitingForTabLoad = false;
            _letterOpened = false;
            _listPollTimer = ListPollSeconds;

            // The tabs open on the one the game has selected.
            if (_navLevel == NavLevel.Tabs)
                _currentIndex = Math.Max(0, CurrentTabIndex());

            // No tab strip and the list still loading: announce it once it arrives. The
            // activation announcement already said "Loading".
            if (_navLevel == NavLevel.Letters && !ListHasContent())
            {
                StartWaitingForList(CurrentListName());
                _loadingAnnounced = true;
            }
        }

        protected override void OnDeactivating()
        {
            _waitingForTabLoad = false;
            _letterOpened = false;
            _listSignature = null;
        }

        protected override bool ValidateElements()
        {
            if (_panel == null || !_panel.activeInHierarchy)
            {
                Log.Msg("{NavigatorId}", "Mailbox closed");
                _panel = null;
                _navLevel = NavLevel.Letters;
                return false;
            }
            return base.ValidateElements();
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive) Deactivate();
            _panel = null;
            _navLevel = NavLevel.Letters;
        }

        #endregion
    }
}
