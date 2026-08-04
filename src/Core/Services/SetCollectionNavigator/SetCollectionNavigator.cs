using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the Set Collection screen (Profile → collection badge).
    ///
    /// The screen is a browse view: filter controls narrow a list of set badges, and the
    /// selected set expands into eleven completion meters (total, four rarities, six colors).
    /// Sighted players read the meters off a widget row; none of it carries text, so every
    /// number here is read straight out of <c>SetCollectionController</c> rather than the UI.
    ///
    /// Navigation mirrors <see cref="StoreNavigator"/>'s drill-down levels:
    ///   Filters → (Enter) → Sets → (Enter) → Actions, Backspace walks back up and out.
    /// Within the Sets level, Left/Right moves between sets and Up/Down cycles the selected
    /// set's meters — the same two-axis shape the duel Player Info Zone uses.
    /// </summary>
    public partial class SetCollectionNavigator : BaseNavigator
    {
        #region Constants

        // Above ProfileNavigator (56) so it preempts while the profile is in SetCollection mode,
        // below AchievementsNavigator (57)... note: 58 keeps it clear of both.
        private const int SetCollectionPriority = 58;

        /// <summary>ProfileScreenModeEnum.SetCollection</summary>
        private const int SetCollectionMode = 6;

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "SetCollection";
        public override string ScreenName => Strings.ScreenSetCollection;
        public override int Priority => SetCollectionPriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => true;
        protected override bool SupportsLetterNavigation => false; // handled per-level

        #endregion

        #region Navigation State

        private enum NavLevel { Filters, Sets, Actions }

        private NavLevel _navLevel = NavLevel.Filters;

        private MonoBehaviour _profileController;   // ProfileContentController
        private MonoBehaviour _screenView;          // SetCollectionScreenView
        private object _collectionController;       // SetCollectionController

        #endregion

        #region Data Structures

        /// <summary>One visible set badge, resolved to the data the announcements need.</summary>
        private struct SetEntry
        {
            public MonoBehaviour Badge;
            public object ExpansionCode;    // boxed CollationMapping
            public string Code;             // raw expansion code, e.g. "TDM"
            public string Name;             // localized set name
            public DateTime ReleaseDate;
            public bool IsStandard;
            public bool IsHistoric;
            public bool IsAlchemy;
            public bool IsUniversesBeyond;
        }

        private enum InfoKind { Metric, ReleaseDate, SetTypes }

        /// <summary>One entry in the Up/Down cycle for the selected set.</summary>
        private struct InfoEntry
        {
            public InfoKind Kind;
            public string Label;
            public object Metric;           // boxed SetCollectionController.Metrics
        }

        private readonly List<SetEntry> _sets = new List<SetEntry>();
        private int _setIndex;

        private readonly List<InfoEntry> _info = new List<InfoEntry>();
        private int _infoIndex;

        #endregion

        #region Reflection Cache

        private sealed class SetCollectionHandles
        {
            // SetCollectionScreenView
            public FieldInfo Controller;
            public FieldInfo SetBadges;
            public FieldInfo SelectedBadge;
            public FieldInfo SortDropdown;
            public FieldInfo FilterDropdown;
            public FieldInfo StandardToggle;
            public FieldInfo HistoricToggle;
            public FieldInfo AlchemyToggle;
            public MethodInfo SelectBadgeByExpansionCode;
            public MethodInfo SortBadges;
            public MethodInfo FilterBadges;
            public MethodInfo CollectionButtonClicked;
            public MethodInfo MoveToStore;
            public MethodInfo BackButtonClicked;

            // SetBadge
            public FieldInfo BadgeExpansionCode;
            public FieldInfo BadgeReleaseDate;
            public FieldInfo BadgeIsStandard;
            public FieldInfo BadgeIsHistoric;
            public FieldInfo BadgeIsAlchemy;
            public FieldInfo BadgeIsUniversesBeyond;

        }

        private static readonly ReflectionCache<SetCollectionHandles> _cache = new ReflectionCache<SetCollectionHandles>(
            builder: viewType =>
            {
                var h = new SetCollectionHandles
                {
                    Controller = viewType.GetField("_controller", PrivateInstance),
                    SetBadges = viewType.GetField("_setBadges", PrivateInstance),
                    SelectedBadge = viewType.GetField("_selectedBadge", PrivateInstance),
                    SortDropdown = viewType.GetField("_sortDropDown", PrivateInstance),
                    FilterDropdown = viewType.GetField("_filterDropDown", PrivateInstance),
                    StandardToggle = viewType.GetField("_standardToggle", PrivateInstance),
                    HistoricToggle = viewType.GetField("_historicToggle", PrivateInstance),
                    AlchemyToggle = viewType.GetField("_alchemyToggle", PrivateInstance),
                    SelectBadgeByExpansionCode = viewType.GetMethod("SelectBadgeByExpansionCode", AllInstanceFlags),
                    SortBadges = viewType.GetMethod("SortBadges", AllInstanceFlags),
                    FilterBadges = viewType.GetMethod("FilterBadges", AllInstanceFlags),
                    CollectionButtonClicked = viewType.GetMethod("CollectionButtonClicked", AllInstanceFlags),
                    MoveToStore = viewType.GetMethod("MoveToStore", AllInstanceFlags),
                    BackButtonClicked = viewType.GetMethod("BackButtonClicked", AllInstanceFlags),
                };

                var badgeType = FindType(T.SetBadge);
                if (badgeType != null)
                {
                    h.BadgeExpansionCode = badgeType.GetField("_expansionCode", AllInstanceFlags);
                    h.BadgeReleaseDate = badgeType.GetField("_releaseDate", AllInstanceFlags);
                    h.BadgeIsStandard = badgeType.GetField("_isStandard", AllInstanceFlags);
                    h.BadgeIsHistoric = badgeType.GetField("_isHistoric", AllInstanceFlags);
                    h.BadgeIsAlchemy = badgeType.GetField("_isAlchemy", AllInstanceFlags);
                    h.BadgeIsUniversesBeyond = badgeType.GetField("_isUniversesBeyond", AllInstanceFlags);
                }

                return h;
            },
            validator: h => h.Controller != null && h.SetBadges != null
                         && h.BadgeExpansionCode != null,
            logTag: "SetCollection",
            logSubject: "SetCollectionScreenView");

        private static SetCollectionHandles H => _cache.Handles;

        #endregion

        #region Constructor

        public SetCollectionNavigator(IAnnouncementService announcer) : base(announcer) { }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            var controller = FindProfileController();
            if (controller == null) return false;

            if (GetProfileScreenMode(controller) != SetCollectionMode)
            {
                _screenView = null;
                return false;
            }

            var view = GetScreenView(controller);
            if (view == null || !view.gameObject.activeInHierarchy) return false;

            _profileController = controller;
            _screenView = view;
            return true;
        }

        private MonoBehaviour FindProfileController()
        {
            if (_profileController != null && _profileController.gameObject != null
                && _profileController.gameObject.activeInHierarchy)
                return _profileController;

            _profileController = null;
            _screenView = null;
            _collectionController = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.ProfileContentController)
                    return mb;
            }

            return null;
        }

        private int GetProfileScreenMode(MonoBehaviour controller)
        {
            try
            {
                var field = controller.GetType().GetField("_profileScreenMode", PrivateInstance);
                var value = field?.GetValue(controller);
                return value == null ? -1 : Convert.ToInt32(value);
            }
            catch { return -1; }
        }

        private MonoBehaviour GetScreenView(MonoBehaviour controller)
        {
            try
            {
                var field = controller.GetType().GetField("SetCollectionPanel", AllInstanceFlags);
                return field?.GetValue(controller) as MonoBehaviour;
            }
            catch { return null; }
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            _elements.Clear();
            if (_screenView == null) return;

            _cache.EnsureInitialized(_screenView.GetType());
            ResolveCollectionController();

            // The inventory can change between visits (packs opened, cards crafted), and the
            // totals are cached in a dictionary that is only rebuilt on demand.
            RefreshControllerTotals();

            DiscoverFilters();
            BuildSetList();

            _navLevel = NavLevel.Filters;
            _filterIndex = 0;
            _setIndex = 0;
            _infoIndex = 0;

            // Every level is driven by HandleInput, but BaseNavigator deactivates a navigator
            // with an empty element list — keep one placeholder so we stay alive.
            AddElement(_screenView.gameObject, ScreenName);

            Log.Msg("{NavigatorId}", $"Discovered {_filters.Count} filter controls, {_sets.Count} visible sets");
        }

        private void ResolveCollectionController()
        {
            _collectionController = null;
            if (H?.Controller == null || _screenView == null) return;

            try { _collectionController = H.Controller.GetValue(_screenView); }
            catch (Exception ex) { Log.Warn("{NavigatorId}", $"Could not read _controller: {ex.Message}"); }
        }

        private void RefreshControllerTotals()
        {
            SetCollectionDataProvider.EnsureInitialized(_collectionController);
            SetCollectionDataProvider.RefreshTotals(_collectionController);
        }

        /// <summary>
        /// Rebuilds the set list from the badges the game currently shows. Both filtering
        /// (SetActive) and sorting (sibling index) are applied to the live badge objects, so
        /// reading them back is what keeps our order identical to the visual one.
        /// </summary>
        private void BuildSetList()
        {
            _sets.Clear();
            if (_screenView == null || H?.SetBadges == null) return;

            List<MonoBehaviour> visible = new List<MonoBehaviour>();
            try
            {
                if (!(H.SetBadges.GetValue(_screenView) is IEnumerable badges)) return;
                foreach (var badge in badges)
                {
                    var mb = badge as MonoBehaviour;
                    if (mb == null || mb.gameObject == null) continue;
                    if (!mb.gameObject.activeSelf) continue;   // hidden by the format filter / toggles
                    visible.Add(mb);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Reading badge list failed: {ex.Message}");
                return;
            }

            visible.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            foreach (var badge in visible)
            {
                var entry = BuildSetEntry(badge);
                if (entry.HasValue)
                    _sets.Add(entry.Value);
            }

            if (_setIndex >= _sets.Count)
                _setIndex = Math.Max(0, _sets.Count - 1);
        }

        private SetEntry? BuildSetEntry(MonoBehaviour badge)
        {
            try
            {
                object expansionCode = H.BadgeExpansionCode.GetValue(badge);
                string code = expansionCode?.ToString();
                if (string.IsNullOrEmpty(code)) return null;

                bool isAlchemy = ReadBadgeFlag(badge, H.BadgeIsAlchemy);

                DateTime releaseDate = default;
                if (H.BadgeReleaseDate != null)
                {
                    try { releaseDate = (DateTime)H.BadgeReleaseDate.GetValue(badge); }
                    catch { }
                }

                return new SetEntry
                {
                    Badge = badge,
                    ExpansionCode = expansionCode,
                    Code = code,
                    Name = SetCollectionDataProvider.GetSetName(code, isAlchemy),
                    ReleaseDate = releaseDate,
                    IsStandard = ReadBadgeFlag(badge, H.BadgeIsStandard),
                    IsHistoric = ReadBadgeFlag(badge, H.BadgeIsHistoric),
                    IsAlchemy = isAlchemy,
                    IsUniversesBeyond = ReadBadgeFlag(badge, H.BadgeIsUniversesBeyond),
                };
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Building set entry failed: {ex.Message}");
                return null;
            }
        }

        private static bool ReadBadgeFlag(MonoBehaviour badge, FieldInfo field)
        {
            if (field == null) return false;
            try { return field.GetValue(badge) as bool? ?? false; }
            catch { return false; }
        }

        #endregion

        #region Metric Access

        /// <summary>Owned/available for one metric of the currently selected set.</summary>
        private bool TryGetMetricTotals(SetEntry set, object metric, out int owned, out int available, out bool isPlayset)
        {
            return SetCollectionDataProvider.TryGetTotals(
                _collectionController, set.Code, metric, set.IsAlchemy,
                out owned, out available, out isPlayset);
        }

        private static int Percent(int owned, int available) => SetCollectionDataProvider.Percent(owned, available);

        #endregion

        #region Input Dispatch

        protected override void HandleInput()
        {
            // Filter dropdowns are driven by value, never opened, so a dropdown should never be
            // in edit mode here. If the game auto-opened one anyway, hand it to the shared path.
            DropdownStateManager.UpdateAndCheckExitTransition();
            if (DropdownStateManager.IsInDropdownMode)
            {
                HandleDropdownNavigation();
                return;
            }

            switch (_navLevel)
            {
                case NavLevel.Filters:
                    HandleFiltersInput();
                    break;
                case NavLevel.Sets:
                    HandleSetsInput();
                    break;
                case NavLevel.Actions:
                    HandleActionsInput();
                    break;
            }
        }

        #endregion

        #region Announcements

        protected override string GetActivationAnnouncement() => Strings.SetCollectionActivation(_sets.Count);

        public override string GetTutorialHint() => LocaleManager.Instance.Get("SetCollectionHint");

        #endregion

        #region Lifecycle

        protected override void OnActivated()
        {
            _navLevel = NavLevel.Filters;
            _filterIndex = 0;
            _setIndex = 0;
            _infoIndex = 0;
        }

        protected override void OnDeactivating()
        {
            _navLevel = NavLevel.Filters;
            _filters.Clear();
            _sets.Clear();
            _info.Clear();
            _actions.Clear();
            _screenView = null;
            _collectionController = null;
        }

        protected override bool ValidateElements()
        {
            if (_screenView == null || _screenView.gameObject == null || !_screenView.gameObject.activeInHierarchy)
            {
                Log.Msg("{NavigatorId}", "Set collection panel no longer active");
                return false;
            }
            return true;
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive) Deactivate();
            _profileController = null;
            _screenView = null;
            _collectionController = null;
            _sets.Clear();
            _filters.Clear();
            _info.Clear();
        }

        #endregion
    }
}
