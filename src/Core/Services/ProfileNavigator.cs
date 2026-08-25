using UnityEngine;
using TMPro;
using MelonLoader;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.PanelDetection;
using System;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Navigator for the MTGA Profile screen.
    /// Provides info block navigation (username, avatar, rank, mastery, cosmetic categories)
    /// and custom handling for cosmetic sub-panels (avatar, title, emote, pet, sleeve).
    /// </summary>
    public class ProfileNavigator : BaseNavigator
    {
        #region Constants

        private const int ProfilePriority = 56;
        private const float SubPanelPollInterval = 0.3f;
        // Selectors are instantiated from a prefab on first open, so give the load real headroom
        // before reporting that the panel never came up.
        private const float SubPanelPollTimeoutSeconds = 6f;

        /// <summary>
        /// The five cosmetic categories, in the order they appear on the screen. All of them are
        /// opened, detected and closed the same way — only their item contents differ.
        /// </summary>
        private static readonly string[] CosmeticTypes = { "Avatar", "Title", "Emote", "Pet", "Sleeve" };

        #endregion

        #region Navigator Identity

        public override string NavigatorId => "Profile";
        public override string ScreenName => Strings.ScreenProfile;
        public override int Priority => ProfilePriority;
        protected override bool SupportsCardNavigation => false;
        protected override bool AcceptSpaceKey => true;

        #endregion

        #region State

        private MonoBehaviour _controller;       // ProfileContentController
        private MonoBehaviour _detailsPanel;     // ProfileDetailsPanel
        private bool _inSubPanel;
        // Set when the game refused an emote save (equip cap). The next Backspace discards instead
        // of retrying, so a rejected save can never leave the user stuck in the panel.
        private bool _emoteDiscardArmed;
        private string _subPanelType;            // "Avatar", "Emote", "Title", "Pet", "Sleeve"
        private readonly List<SubPanelItem> _subPanelItems = new List<SubPanelItem>();
        private int _subPanelIndex;
        private int _savedMainIndex;
        private float _subPanelPollTimer;
        private float _subPanelPollElapsed;        // Total time spent waiting for a selector to appear
        private bool _pollForSelectorPanel;       // Waiting for a just-opened cosmetic selector to appear
        // The opener behind the sub-panel currently open. Every cosmetic selector is created and
        // shown by one of these, so it is also the one handle that can close any of them again.
        private MonoBehaviour _activeOpener;
        private float _rescanDelay;               // Countdown for silent info-block rescan after sub-panel exit
        private bool _persistedDuringSubPanel;    // Marks "user actually applied a cosmetic change" so ExitSubPanel can schedule a rescan

        // Info blocks for main screen navigation
        private readonly List<InfoBlock> _infoBlocks = new List<InfoBlock>();
        private int _infoIndex;

        private const float RescanDelaySeconds = 0.4f;

        #endregion

        #region Data Structures

        private struct InfoBlock
        {
            public string Label;
            public GameObject GameObject;     // Button to activate (for cosmetic categories)
            public bool IsActivatable;        // Whether Enter should activate this item
            public string CosmeticType;       // null for info, "Avatar"/"Title"/etc. for cosmetics
            public bool OpensSetCollection;   // Enter opens the full set collection screen
            public MonoBehaviour CosmeticOpener; // CosmeticSelectorOpener driving this category
        }

        private struct SubPanelItem
        {
            public string Label;
            public GameObject GameObject;
            public string Status;             // "owned", "locked", "selected", etc.
            public bool Toggleable;           // emotes: false for the always-equipped Classic set
        }

        #endregion

        #region Reflection Cache

        private sealed class ProfileHandles
        {
            // ProfileContentController
            public FieldInfo UsernameDisplay;        // _usernameDisplay (ProfileUsernameDisplay)
            public FieldInfo AvatarDisplay;          // _avatarDisplay (ProfileAvatarDisplay)
            public FieldInfo ProfileDetailsPanel;
            public FieldInfo UseNewNavigationBar;    // _useNewNavigationBar — picks which host is live
            public FieldInfo CustomizationPanelNew;  // _customizationPanel (new nav bar layout)
            public FieldInfo CustomizationPanelOld;  // _oldCustomizationHost (legacy layout)
            public MethodInfo GoToProfileDetails;    // replaces the removed GoBackToPreviousMode

            // ProfileUsernameDisplay / ProfileAvatarDisplay — the username and avatar blurb moved
            // off the controller into these two components in the 2026-08-10 profile rework.
            public PropertyInfo UsernameText;        // ProfileUsernameDisplay.Text
            public FieldInfo AvatarNameText;         // ProfileAvatarDisplay._avatarNameText (Localize)
            public FieldInfo AvatarBioText;          // ProfileAvatarDisplay._avatarBioText (Localize)

            // CustomizationPanel — holds the five opener components that replaced the old buttons
            public FieldInfo AvatarOpener;
            public FieldInfo TitleOpener;
            public FieldInfo EmoteOpener;
            public FieldInfo PetOpener;
            public FieldInfo SleeveOpener;

            // CosmeticSelectorOpener — the shared base of all five openers.
            // Open() instantiates the selector on first use and shows it; HideSelector() hides it;
            // HandleSelectorClosed() is what each selector's own back button is wired to, so it is
            // the one close path that also re-shows the opener buttons (via the Closed event).
            public MethodInfo OpenerOpen;
            public MethodInfo OpenerHideSelector;
            public MethodInfo OpenerHandleSelectorClosed;

            // Per-opener selector instance, used to tell whether that cosmetic's panel is showing.
            // Every opener lazily instantiates its selector into a private field: Avatar/Pet/Sleeve/
            // Title call it _selector, Emote calls it _view.
            public FieldInfo AvatarOpenerSelector;
            public FieldInfo TitleOpenerSelector;
            public FieldInfo PetOpenerSelector;
            public FieldInfo SleeveOpenerSelector;
            public FieldInfo EmoteOpenerView;
            public FieldInfo EmoteOpenerController;  // EmoteSelectorOpener._controller

            // ProfileDetailsPanel
            public FieldInfo ConstructedRank;
            public FieldInfo LimitedRank;
            public FieldInfo SeasonName;
            public FieldInfo BattlePassBubble;
            public FieldInfo CollectionController;   // SetCollectionControllerLogic behind the badge
            public FieldInfo SetMetadataProvider;
            public MethodInfo SetCollectionClicked;  // opens the full set collection screen

            // RankDisplay
            public FieldInfo RankFormatText;
            public FieldInfo RankTierText;
            public FieldInfo MythicPlacementText;
            public FieldInfo IsLimited;

            // AvatarSelection
            public PropertyInfo BioString;
            public PropertyInfo StoreSection;

            // Avatar persistence
            public Type AvatarSelectPanelType;
            public MethodInfo DoneButtonOnClick;
            public MethodInfo AvatarIsLocked;

            // Pet persistence
            public Type PetPopUpV2Type;
            public MethodInfo PetOnConfirm;
            public Type SelectPetsListItemViewType;
            public PropertyInfo SelectPetsItemIsOwned;

            // Emote state + persistence.
            // Unlike avatars and pets, the emote panel is a deferred-commit selector: clicking an
            // emote only toggles it in EmoteSelectionController._equippedEmotesOnHide, and nothing
            // is written until the panel's confirm button runs SaveAndHide(). Closing the selector
            // (Close()) just deactivates the GameObject and silently drops every change, so the
            // navigator has to drive the save itself on the way out.
            public Type EmoteViewType;
            public FieldInfo EmoteIsEquipped;
            public PropertyInfo EmoteViewId;
            public FieldInfo EmoteDataProvider;         // EmoteSelectionController._emoteDataProvider
            public MethodInfo EmoteGetDefaultData;      // IEmoteDataProvider.GetDefaultEmoteData()
            public MethodInfo EmoteSaveAndHide;         // EmoteSelectionController.SaveAndHide()
            public MethodInfo EmoteSelectorClose;       // EmoteSelectionController.Close()
            public PropertyInfo EmoteHasUnsavedChanges; // EmoteSelectionController.HasUnsavedChanges
            public FieldInfo EmotePopupView;            // EmoteSelectionController._emoteSelectionPopupView
            public FieldInfo EmotePhrasesText;          // EmoteSelectionScreenView._phrasesText
            public FieldInfo EmoteStickersText;         // EmoteSelectionScreenView._stickersText
        }

        private static readonly ReflectionCache<ProfileHandles> _profileCache = new ReflectionCache<ProfileHandles>(
            builder: t =>
            {
                var h = new ProfileHandles
                {
                    // Controller
                    UsernameDisplay = t.GetField("_usernameDisplay", PrivateInstance),
                    AvatarDisplay = t.GetField("_avatarDisplay", PrivateInstance),
                    ProfileDetailsPanel = t.GetField("ProfileDetailsPanel", AllInstanceFlags),
                    UseNewNavigationBar = t.GetField("_useNewNavigationBar", PrivateInstance),
                    CustomizationPanelNew = t.GetField("_customizationPanel", PrivateInstance),
                    CustomizationPanelOld = t.GetField("_oldCustomizationHost", PrivateInstance),
                    GoToProfileDetails = t.GetMethod("GoToProfileDetails", PublicInstance, null, Type.EmptyTypes, null),
                };

                var usernameDisplayType = FindType("Core.Meta.MainNavigation.Profile.ProfileUsernameDisplay");
                if (usernameDisplayType != null)
                    h.UsernameText = usernameDisplayType.GetProperty("Text", PublicInstance);

                var avatarDisplayType = FindType("Core.Meta.MainNavigation.Profile.ProfileAvatarDisplay");
                if (avatarDisplayType != null)
                {
                    h.AvatarNameText = avatarDisplayType.GetField("_avatarNameText", PrivateInstance);
                    h.AvatarBioText = avatarDisplayType.GetField("_avatarBioText", PrivateInstance);
                }

                var customizationPanelType = FindType("Core.Meta.MainNavigation.Profile.CustomizationPanel");
                if (customizationPanelType != null)
                {
                    h.AvatarOpener = customizationPanelType.GetField("_avatarOpener", PrivateInstance);
                    h.TitleOpener = customizationPanelType.GetField("_titleOpener", PrivateInstance);
                    h.EmoteOpener = customizationPanelType.GetField("_emoteOpener", PrivateInstance);
                    h.PetOpener = customizationPanelType.GetField("_petOpener", PrivateInstance);
                    h.SleeveOpener = customizationPanelType.GetField("_sleeveOpener", PrivateInstance);
                }

                var openerBaseType = FindType("Core.Meta.MainNavigation.Profile.CosmeticSelectorOpener");
                if (openerBaseType != null)
                {
                    h.OpenerOpen = openerBaseType.GetMethod("Open", PublicInstance, null, Type.EmptyTypes, null);
                    h.OpenerHideSelector = openerBaseType.GetMethod("HideSelector", PublicInstance, null, Type.EmptyTypes, null);
                    h.OpenerHandleSelectorClosed = openerBaseType.GetMethod("HandleSelectorClosed",
                        BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                }

                // The selector instance lives on the concrete opener, not the base, so each one has
                // to be bound against its own type — GetField with private flags never sees a base
                // class's privates.
                h.AvatarOpenerSelector = GetOpenerSelectorField("AvatarSelectorOpener", "_selector");
                h.TitleOpenerSelector = GetOpenerSelectorField("TitleSelectorOpener", "_selector");
                h.PetOpenerSelector = GetOpenerSelectorField("PetSelectorOpener", "_selector");
                h.SleeveOpenerSelector = GetOpenerSelectorField("SleeveSelectorOpener", "_selector");
                h.EmoteOpenerView = GetOpenerSelectorField("EmoteSelectorOpener", "_view");
                h.EmoteOpenerController = GetOpenerSelectorField("EmoteSelectorOpener", "_controller");

                var detailsType = FindType("ProfileUI.ProfileDetailsPanel");
                if (detailsType != null)
                {
                    h.ConstructedRank = detailsType.GetField("_constructedRankDisplay", PrivateInstance);
                    h.LimitedRank = detailsType.GetField("_limitedRankDisplay", PrivateInstance);
                    h.SeasonName = detailsType.GetField("_seasonNameRankText", PrivateInstance);
                    h.BattlePassBubble = detailsType.GetField("_battlePassBubble", PrivateInstance);
                    h.CollectionController = detailsType.GetField("_collectionControllerLogic", PrivateInstance);
                    h.SetMetadataProvider = detailsType.GetField("_setMetadataProvider", PrivateInstance);
                    h.SetCollectionClicked = detailsType.GetMethod("SetCollectionClicked", PublicInstance);
                }

                var rankType = FindType("RankDisplay");
                if (rankType != null)
                {
                    h.RankFormatText = rankType.GetField("_rankFormatText", PrivateInstance);
                    h.RankTierText = rankType.GetField("_rankTierText", PrivateInstance);
                    h.MythicPlacementText = rankType.GetField("_mythicPlacementText", PrivateInstance);
                    h.IsLimited = rankType.GetField("_isLimited", PrivateInstance);
                }

                h.AvatarSelectPanelType = FindType("ProfileUI.AvatarSelectPanel");
                if (h.AvatarSelectPanelType != null)
                    h.DoneButtonOnClick = h.AvatarSelectPanelType.GetMethod("DoneButton_OnClick", PublicInstance);

                var avatarSelType = FindType("AvatarSelection");
                if (avatarSelType != null)
                {
                    h.AvatarIsLocked = avatarSelType.GetMethod("IsLocked", PublicInstance);
                    h.BioString = avatarSelType.GetProperty("BioString", PublicInstance);
                    h.StoreSection = avatarSelType.GetProperty("StoreSection", PublicInstance);
                }

                h.PetPopUpV2Type = FindType("Core.Meta.MainNavigation.Profile.PetPopUpV2");
                if (h.PetPopUpV2Type != null)
                    h.PetOnConfirm = h.PetPopUpV2Type.GetMethod("OnConfirm", PublicInstance);

                h.SelectPetsListItemViewType = FindType("SelectPetsListItemView");
                if (h.SelectPetsListItemViewType != null)
                    h.SelectPetsItemIsOwned = h.SelectPetsListItemViewType.GetProperty("IsOwned", PublicInstance);

                h.EmoteViewType = FindType("EmoteView");
                if (h.EmoteViewType != null)
                {
                    h.EmoteIsEquipped = h.EmoteViewType.GetField("_isEquipped", PrivateInstance);
                    h.EmoteViewId = h.EmoteViewType.GetProperty("Id", PublicInstance);
                }

                var emoteProviderType = FindType("IEmoteDataProvider");
                if (emoteProviderType != null)
                    h.EmoteGetDefaultData = emoteProviderType.GetMethod("GetDefaultEmoteData", PublicInstance);

                var emoteSelectorType = FindType("EmoteSelectionController");
                if (emoteSelectorType != null)
                {
                    h.EmoteDataProvider = emoteSelectorType.GetField("_emoteDataProvider", PrivateInstance);
                    h.EmoteSaveAndHide = emoteSelectorType.GetMethod("SaveAndHide", PublicInstance, null, Type.EmptyTypes, null);
                    h.EmoteSelectorClose = emoteSelectorType.GetMethod("Close", PublicInstance, null, Type.EmptyTypes, null);
                    h.EmoteHasUnsavedChanges = emoteSelectorType.GetProperty("HasUnsavedChanges", PublicInstance);
                    h.EmotePopupView = emoteSelectorType.GetField("_emoteSelectionPopupView", PrivateInstance);
                }

                var emoteScreenViewType = FindType("EmoteSelectionScreenView");
                if (emoteScreenViewType != null)
                {
                    h.EmotePhrasesText = emoteScreenViewType.GetField("_phrasesText", PrivateInstance);
                    h.EmoteStickersText = emoteScreenViewType.GetField("_stickersText", PrivateInstance);
                }

                WarnIfCosmeticsUnavailable(h);
                return h;
            },
            // Only the main-screen reading path is required. The cosmetic openers are reported
            // separately by WarnIfCosmeticsUnavailable so that a rework of the customization host
            // costs the user the five cosmetic entries, not the whole Profile screen.
            validator: h => h.UsernameDisplay != null && h.UsernameText != null
                         && h.ProfileDetailsPanel != null && h.ConstructedRank != null
                         && h.RankTierText != null,
            logTag: "Profile",
            logSubject: "ProfileContentController");

        /// <summary>
        /// Binds a concrete opener's private selector field. Each of the five openers declares its
        /// own selector instance, so the lookup has to go against the derived type — a private
        /// field on a base class is invisible to GetField on the subclass.
        /// </summary>
        private static FieldInfo GetOpenerSelectorField(string openerTypeName, string fieldName)
        {
            var type = FindType("Core.Meta.MainNavigation.Profile." + openerTypeName);
            return type?.GetField(fieldName, PrivateInstance);
        }

        /// <summary>
        /// The cosmetic categories are optional as far as the cache validator is concerned, so an
        /// unresolved opener would otherwise vanish from the screen with no trace in the log.
        /// </summary>
        private static void WarnIfCosmeticsUnavailable(ProfileHandles h)
        {
            var missing = new List<string>();
            if (h.CustomizationPanelNew == null && h.CustomizationPanelOld == null) missing.Add("CustomizationPanel host");
            if (h.AvatarOpener == null) missing.Add("_avatarOpener");
            if (h.TitleOpener == null) missing.Add("_titleOpener");
            if (h.EmoteOpener == null) missing.Add("_emoteOpener");
            if (h.PetOpener == null) missing.Add("_petOpener");
            if (h.SleeveOpener == null) missing.Add("_sleeveOpener");
            if (h.OpenerOpen == null) missing.Add("CosmeticSelectorOpener.Open");
            if (h.OpenerHandleSelectorClosed == null) missing.Add("CosmeticSelectorOpener.HandleSelectorClosed");

            if (missing.Count > 0)
                Log.Warn("Profile", $"Cosmetic categories unavailable, missing: {string.Join(", ", missing)}");
        }

        #endregion

        #region Constructor

        public ProfileNavigator(IAnnouncementService announcer) : base(announcer)
        {
        }

        #endregion

        #region Screen Detection

        protected override bool DetectScreen()
        {
            if (_inSubPanel) return true; // Stay active while in sub-panel

            var controller = FindProfileController();
            if (controller == null) return false;

            // Check if the controller is open (IsOpen property on NavContentController base)
            if (!IsControllerOpen(controller)) return false;

            _controller = controller;
            return true;
        }

        private bool IsControllerOpen(MonoBehaviour controller)
        {
            try
            {
                var isOpenProp = controller.GetType().GetProperty("IsOpen",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (isOpenProp != null)
                    return (bool)isOpenProp.GetValue(controller);

                // Fallback: check if game object is active
                return controller.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        private MonoBehaviour FindProfileController()
        {
            if (_controller != null && _controller.gameObject != null && _controller.gameObject.activeInHierarchy)
                return _controller;

            _controller = null;
            _detailsPanel = null;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == T.ProfileContentController)
                    return mb;
            }

            return null;
        }

        #endregion

        #region Reflection Caching

        private void EnsureReflectionCached()
        {
            var controllerType = _controller?.GetType();
            if (controllerType == null) return;
            _profileCache.EnsureInitialized(controllerType);
        }

        #endregion

        #region Element Discovery

        protected override void DiscoverElements()
        {
            EnsureReflectionCached();
            _infoBlocks.Clear();
            _inSubPanel = false;

            BuildInfoBlocks();

            // Add a placeholder element for BaseNavigator (needs at least 1)
            if (_infoBlocks.Count > 0)
                AddElement(_controller.gameObject, "Profile");

            Log.Msg("{NavigatorId}", $"Discovered {_infoBlocks.Count} info blocks");
        }

        private void BuildInfoBlocks()
        {
            if (_controller == null) return;

            // Ensure the details panel reference
            EnsureDetailsPanel();

            // 1. Player name + title
            string username = ReadUsername();
            if (!string.IsNullOrEmpty(username))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = username,
                    GameObject = _controller.gameObject,
                    IsActivatable = false
                });
            }

            // 2. Avatar name + bio
            string avatarInfo = ReadAvatarInfo();
            if (!string.IsNullOrEmpty(avatarInfo))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = avatarInfo,
                    GameObject = _controller.gameObject,
                    IsActivatable = false
                });
            }

            // 3. Season name
            string season = ReadSeasonName();
            if (!string.IsNullOrEmpty(season))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = Strings.ProfileSeason(season),
                    GameObject = _controller.gameObject,
                    IsActivatable = false
                });
            }

            // 4. Constructed rank
            string constructedRank = ReadRankInfo(false);
            if (!string.IsNullOrEmpty(constructedRank))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = Strings.ProfileRankConstructed(constructedRank),
                    GameObject = _controller.gameObject,
                    IsActivatable = false
                });
            }

            // 5. Limited rank (only if visible)
            string limitedRank = ReadRankInfo(true);
            if (!string.IsNullOrEmpty(limitedRank))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = Strings.ProfileRankLimited(limitedRank),
                    GameObject = _controller.gameObject,
                    IsActivatable = false
                });
            }

            // 6. Mastery progress (ReadMasteryInfo returns fully formatted string)
            string mastery = ReadMasteryInfo();
            if (!string.IsNullOrEmpty(mastery))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = mastery,
                    GameObject = _controller.gameObject,
                    IsActivatable = false
                });
            }

            // 7. Set collection progress
            string collection = ReadSetCollectionInfo();
            if (!string.IsNullOrEmpty(collection))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = collection,
                    GameObject = _controller.gameObject,
                    IsActivatable = true,
                    OpensSetCollection = true
                });
            }

            // 8-12. Cosmetic categories, read off the live CustomizationPanel's five openers.
            var host = GetActiveCustomizationHost();
            if (host != null)
            {
                foreach (string type in CosmeticTypes)
                    AddCosmeticOpener(host, GetOpenerField(_profileCache.Handles, type), type);
            }
        }

        /// <summary>
        /// Mirrors ProfileContentController.ActiveCustomizationHost. The screen ships both layouts
        /// side by side and picks between them with _useNewNavigationBar: the new navigation bar
        /// routes the cosmetic buttons through _customizationPanel, the legacy layout through
        /// _oldCustomizationHost. Reading the wrong one yields openers that are never shown.
        /// </summary>
        private MonoBehaviour GetActiveCustomizationHost()
        {
            var h = _profileCache.Handles;
            if (h == null || _controller == null) return null;

            try
            {
                bool useNew = h.UseNewNavigationBar != null
                    && h.UseNewNavigationBar.GetValue(_controller) as bool? == true;
                var field = useNew ? h.CustomizationPanelNew : h.CustomizationPanelOld;
                return field?.GetValue(_controller) as MonoBehaviour
                    // Either layout may be left unassigned in the prefab; fall back to the other.
                    ?? (useNew ? h.CustomizationPanelOld : h.CustomizationPanelNew)?.GetValue(_controller) as MonoBehaviour;
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Reading the customization host failed: {ex.Message}");
                return null;
            }
        }

        private void EnsureDetailsPanel()
        {
            if (_detailsPanel != null && _detailsPanel.gameObject != null && _detailsPanel.gameObject.activeInHierarchy)
                return;

            _detailsPanel = null;

            if (_profileCache.Handles?.ProfileDetailsPanel != null && _controller != null)
            {
                try
                {
                    _detailsPanel = _profileCache.Handles.ProfileDetailsPanel.GetValue(_controller) as MonoBehaviour;
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"Failed to get ProfileDetailsPanel: {ex.Message}");
                }
            }
        }

        #endregion

        #region Data Extraction

        private string ReadUsername()
        {
            var h = _profileCache.Handles;
            if (h?.UsernameDisplay == null || h.UsernameText == null || _controller == null) return null;
            try
            {
                // ProfileUsernameDisplay.Text is a passthrough to its own TMP_Text.
                var display = h.UsernameDisplay.GetValue(_controller);
                if (display == null) return null;
                string text = h.UsernameText.GetValue(display) as string;
                if (string.IsNullOrEmpty(text)) return null;
                // Strip rich text color tags (game adds <color=#696969>#1234</color> suffix)
                text = UITextExtractor.CleanText(text);
                return text;
            }
            catch { return null; }
        }

        private string ReadAvatarInfo()
        {
            var h = _profileCache.Handles;
            if (h?.AvatarDisplay == null || _controller == null) return null;

            object display;
            try { display = h.AvatarDisplay.GetValue(_controller); }
            catch { return null; }
            if (display == null) return null;

            string name = ReadLocalizeText(display, h.AvatarNameText);
            string bio = ReadLocalizeText(display, h.AvatarBioText);

            if (string.IsNullOrEmpty(name)) return null;
            return Strings.ProfileAvatar(name, bio ?? "");
        }

        private string ReadLocalizeText(object source, FieldInfo localizeField)
        {
            if (localizeField == null || source == null) return null;
            try
            {
                var localize = localizeField.GetValue(source) as MonoBehaviour;
                if (localize == null) return null;

                // Read TMP_Text from the Localize component's game object
                var tmpText = localize.GetComponentInChildren<TMP_Text>(true);
                if (tmpText != null)
                {
                    string text = UITextExtractor.CleanText(tmpText.text);
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }

                return null;
            }
            catch { return null; }
        }

        private string ReadSeasonName()
        {
            if (_profileCache.Handles.SeasonName == null || _detailsPanel == null) return null;
            try
            {
                var tmpText = _profileCache.Handles.SeasonName.GetValue(_detailsPanel) as TMP_Text;
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy) return null;
                string text = UITextExtractor.CleanText(tmpText.text);
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch { return null; }
        }

        private string ReadRankInfo(bool limited)
        {
            if (_detailsPanel == null) return null;

            var rankField = limited ? _profileCache.Handles.LimitedRank : _profileCache.Handles.ConstructedRank;
            if (rankField == null) return null;

            try
            {
                var rankDisplay = rankField.GetValue(_detailsPanel) as MonoBehaviour;
                if (rankDisplay == null) return null;
                if (!rankDisplay.gameObject.activeInHierarchy) return null;

                // Read _rankTierText (e.g., "Gold Tier 2")
                string tierText = null;
                if (_profileCache.Handles.RankTierText != null)
                {
                    var tierTmp = _profileCache.Handles.RankTierText.GetValue(rankDisplay) as TMP_Text;
                    if (tierTmp != null && tierTmp.gameObject.activeSelf)
                        tierText = UITextExtractor.CleanText(tierTmp.text);
                }

                // Read _mythicPlacementText (e.g., "#123" or "95%")
                string mythicText = null;
                if (_profileCache.Handles.MythicPlacementText != null)
                {
                    var mythicTmp = _profileCache.Handles.MythicPlacementText.GetValue(rankDisplay) as TMP_Text;
                    if (mythicTmp != null && mythicTmp.gameObject.activeInHierarchy)
                    {
                        mythicText = UITextExtractor.CleanText(mythicTmp.text);
                        if (!string.IsNullOrEmpty(mythicText))
                            return Strings.ProfileRankMythic(tierText ?? "", mythicText);
                    }
                }

                return tierText;
            }
            catch { return null; }
        }

        private string ReadMasteryInfo()
        {
            if (_profileCache.Handles.BattlePassBubble == null || _detailsPanel == null) return null;
            try
            {
                var bubble = _profileCache.Handles.BattlePassBubble.GetValue(_detailsPanel) as MonoBehaviour;
                if (bubble == null || !bubble.gameObject.activeInHierarchy) return null;

                // Read level text from visible TMP children
                var texts = bubble.GetComponentsInChildren<TMP_Text>(true);
                string levelText = null;
                foreach (var t in texts)
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    string text = UITextExtractor.CleanText(t.text);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (levelText == null)
                            levelText = text;
                        else
                            levelText += ", " + text;
                    }
                }

                // Try to read enhanced details from _popupData (protected field)
                string xpProgress = null;
                string rewardText = null;
                var popupDataField = bubble.GetType().GetField("_popupData",
                    PrivateInstance | BindingFlags.FlattenHierarchy);
                if (popupDataField != null)
                {
                    var popupData = popupDataField.GetValue(bubble);
                    if (popupData != null)
                    {
                        // ProgressString → XP progress (e.g., "200/1000 XP")
                        var progressField = popupData.GetType().GetField("ProgressString", AllInstanceFlags);
                        if (progressField != null)
                        {
                            try { xpProgress = progressField.GetValue(popupData)?.ToString(); }
                            catch { }
                        }

                        // HeaderString2 → reward description (e.g., "500 Gold")
                        var headerString2Field = popupData.GetType().GetField("HeaderString2", AllInstanceFlags);
                        if (headerString2Field != null)
                        {
                            try { rewardText = headerString2Field.GetValue(popupData)?.ToString(); }
                            catch { }
                        }
                    }
                }

                // Use enhanced format if we have XP or reward details
                if (!string.IsNullOrEmpty(xpProgress) || !string.IsNullOrEmpty(rewardText))
                {
                    string level = levelText ?? "";
                    string xp = !string.IsNullOrEmpty(xpProgress) ? xpProgress : "";
                    string reward = !string.IsNullOrEmpty(rewardText) ? rewardText : "";
                    return Strings.ProfileMasteryDetail(level, xp, reward);
                }

                // Fallback: basic mastery format with just level text
                return !string.IsNullOrEmpty(levelText) ? Strings.ProfileMastery(levelText) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// The profile's collection line, summarizing the newest set.
        ///
        /// Deliberately does not read the SetBadge widget. ProfileDetailsPanel builds that badge
        /// through SetBadge.Init() — the parameterless overload, which only grabs the Animator —
        /// so the badge's tooltip and _expansionCode are never populated and there is no set name
        /// to scrape. The percentage label is also ambiguous: the game reuses it for playset
        /// completion once the set is complete at one-of. Both problems go away by reading the
        /// same SetCollectionControllerLogic the badge itself is fed from.
        /// </summary>
        private string ReadSetCollectionInfo()
        {
            if (_detailsPanel == null) return null;

            try
            {
                var collectionController = _profileCache.Handles.CollectionController?.GetValue(_detailsPanel);
                if (collectionController == null) return null;

                SetCollectionDataProvider.RefreshTotals(collectionController);

                object expansion = SetCollectionDataProvider.GetMostRecentExpansion(collectionController);
                string code = expansion?.ToString();
                if (string.IsNullOrEmpty(code)) return null;

                var metadataProvider = _profileCache.Handles.SetMetadataProvider?.GetValue(_detailsPanel);
                bool isAlchemy = SetCollectionDataProvider.IsAlchemy(metadataProvider, expansion);

                object total = SetCollectionDataProvider.TotalMetric;
                if (total == null) return null;

                if (!SetCollectionDataProvider.TryGetTotals(collectionController, code, total, isAlchemy,
                        out int owned, out int available, out bool isPlayset))
                    return null;

                string setName = SetCollectionDataProvider.GetSetName(code, isAlchemy);
                int percent = SetCollectionDataProvider.Percent(owned, available);

                return isPlayset
                    ? Strings.ProfileCollectionPlayset(setName, owned, available, percent)
                    : Strings.ProfileCollection(setName, owned, available, percent);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Reading set collection info failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Opens the full Set Collection screen, the same way clicking the badge does.</summary>
        private void OpenSetCollectionScreen()
        {
            if (_detailsPanel == null || _profileCache.Handles.SetCollectionClicked == null)
            {
                _announcer.Announce(Strings.SetCollectionActionFailed);
                return;
            }

            try
            {
                _profileCache.Handles.SetCollectionClicked.Invoke(_detailsPanel, null);
                Log.Msg("{NavigatorId}", "Opened set collection screen");
                // SetCollectionNavigator (priority 58) preempts us once the mode flips.
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"SetCollectionClicked failed: {ex.Message}");
                _announcer.Announce(Strings.SetCollectionActionFailed);
            }
        }

        /// <summary>
        /// Adds one cosmetic category, backed by its CosmeticSelectorOpener rather than a button.
        /// The opener's own GameObject is hidden while any selector is open (CustomizationPanel
        /// hides the whole button row), so activeInHierarchy is deliberately not required here —
        /// Open() works either way, and the category name stands in when the label can't be read.
        /// </summary>
        private void AddCosmeticOpener(MonoBehaviour host, FieldInfo openerField, string cosmeticType)
        {
            if (openerField == null || host == null) return;
            try
            {
                var opener = openerField.GetValue(host) as MonoBehaviour;
                if (opener == null) return;

                string label = UITextExtractor.GetText(opener.gameObject);
                if (string.IsNullOrEmpty(label))
                    label = cosmeticType;

                _infoBlocks.Add(new InfoBlock
                {
                    Label = label,
                    GameObject = opener.gameObject,
                    CosmeticOpener = opener,
                    IsActivatable = true,
                    CosmeticType = cosmeticType
                });
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Failed to add cosmetic category {cosmeticType}: {ex.Message}");
            }
        }

        /// <summary>The opener field for a cosmetic type, or null for an unknown type.</summary>
        private static FieldInfo GetOpenerField(ProfileHandles h, string cosmeticType)
        {
            if (h == null) return null;
            switch (cosmeticType)
            {
                case "Avatar": return h.AvatarOpener;
                case "Title": return h.TitleOpener;
                case "Emote": return h.EmoteOpener;
                case "Pet": return h.PetOpener;
                case "Sleeve": return h.SleeveOpener;
                default: return null;
            }
        }

        /// <summary>
        /// The field holding a cosmetic's live selector instance. Avatar, Title, Pet and Sleeve
        /// each keep theirs in _selector; the emote opener keeps its view in _view.
        /// </summary>
        private static FieldInfo GetOpenerSelectorField(ProfileHandles h, string cosmeticType)
        {
            if (h == null) return null;
            switch (cosmeticType)
            {
                case "Avatar": return h.AvatarOpenerSelector;
                case "Title": return h.TitleOpenerSelector;
                case "Pet": return h.PetOpenerSelector;
                case "Sleeve": return h.SleeveOpenerSelector;
                case "Emote": return h.EmoteOpenerView;
                default: return null;
            }
        }

        #endregion

        #region Activation & Navigation

        protected override void OnActivated()
        {
            _infoIndex = 0;
            _inSubPanel = false;
            _pollForSelectorPanel = false;

            // Cosmetic selectors are handled by their openers and excluded in IsPopupExcluded;
            // this is for everything else that can pop up over the profile (store redirects,
            // the emote equip-cap modal).
            EnablePopupDetection();
        }

        protected override void OnDeactivating()
        {
            DisablePopupDetection();
            _inSubPanel = false;
            _pollForSelectorPanel = false;
            _subPanelPollElapsed = 0f;
            _activeOpener = null;
            _rescanDelay = 0f;
            _persistedDuringSubPanel = false;
            _subPanelItems.Clear();
            _infoBlocks.Clear();
            _controller = null;
            _detailsPanel = null;
        }

        protected override string GetActivationAnnouncement()
        {
            string playerInfo = "";
            if (_infoBlocks.Count > 0)
                playerInfo = _infoBlocks[0].Label;

            return Strings.ProfileActivation(playerInfo, _infoBlocks.Count);
        }

        protected override string GetElementAnnouncement(int index)
        {
            if (_inSubPanel)
            {
                if (_subPanelIndex < 0 || _subPanelIndex >= _subPanelItems.Count) return "";
                var item = _subPanelItems[_subPanelIndex];
                string label = item.Label;
                if (!string.IsNullOrEmpty(item.Status))
                    label += ", " + item.Status;
                return Strings.ProfileSubPanelItem(label, _subPanelIndex + 1, _subPanelItems.Count);
            }

            if (_infoIndex < 0 || _infoIndex >= _infoBlocks.Count) return "";
            var block = _infoBlocks[_infoIndex];
            return Strings.ItemPositionOf(_infoIndex + 1, _infoBlocks.Count, block.Label);
        }

        #endregion

        #region Input Handling

        protected override bool HandleCustomInput()
        {
            if (_inSubPanel)
                return HandleSubPanelInput();

            return HandleMainScreenInput();
        }

        private bool HandleMainScreenInput()
        {
            if (_infoBlocks.Count == 0) return false;

            // Up/Down: navigate info blocks
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                if (_infoIndex > 0)
                {
                    _infoIndex--;
                    AnnounceCurrentBlock();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.BeginningOfList);
                }
                return true;
            }

            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                if (_infoIndex < _infoBlocks.Count - 1)
                {
                    _infoIndex++;
                    AnnounceCurrentBlock();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.EndOfList);
                }
                return true;
            }

            // Home/End: jump to first/last
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                _infoIndex = 0;
                AnnounceCurrentBlock();
                return true;
            }

            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                _infoIndex = _infoBlocks.Count - 1;
                AnnounceCurrentBlock();
                return true;
            }

            // Enter: activate cosmetic button
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_infoIndex >= 0 && _infoIndex < _infoBlocks.Count)
                {
                    var block = _infoBlocks[_infoIndex];
                    if (block.OpensSetCollection)
                    {
                        OpenSetCollectionScreen();
                        return true;
                    }
                    if (block.IsActivatable && block.GameObject != null)
                    {
                        _savedMainIndex = _infoIndex;
                        ActivateCosmeticButton(block);
                        return true;
                    }
                }
                return true;
            }

            // Tab/Shift+Tab: navigate info blocks (same as Down/Up)
            if (KeyInput.GetKeyDown(KeyCode.Tab))
            {
                bool shift = KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift);
                if (shift)
                {
                    if (_infoIndex > 0)
                    {
                        _infoIndex--;
                        AnnounceCurrentBlock();
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(Strings.BeginningOfList);
                    }
                }
                else
                {
                    if (_infoIndex < _infoBlocks.Count - 1)
                    {
                        _infoIndex++;
                        AnnounceCurrentBlock();
                    }
                    else
                    {
                        _announcer.AnnounceInterrupt(Strings.EndOfList);
                    }
                }
                return true;
            }

            // Backspace: navigate back to home screen
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                NavigateBackToHome();
                return true;
            }

            return false;
        }

        private bool HandleSubPanelInput()
        {
            if (_subPanelItems.Count == 0) return false;

            // Up/Down: navigate items
            if (KeyInput.GetKeyDown(KeyCode.UpArrow))
            {
                if (_subPanelIndex > 0)
                {
                    _subPanelIndex--;
                    AnnounceCurrentSubItem();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.BeginningOfList);
                }
                return true;
            }

            if (KeyInput.GetKeyDown(KeyCode.DownArrow))
            {
                if (_subPanelIndex < _subPanelItems.Count - 1)
                {
                    _subPanelIndex++;
                    AnnounceCurrentSubItem();
                }
                else
                {
                    _announcer.AnnounceInterrupt(Strings.EndOfList);
                }
                return true;
            }

            // Home/End
            if (KeyInput.GetKeyDown(KeyCode.Home))
            {
                _subPanelIndex = 0;
                AnnounceCurrentSubItem();
                return true;
            }

            if (KeyInput.GetKeyDown(KeyCode.End))
            {
                _subPanelIndex = _subPanelItems.Count - 1;
                AnnounceCurrentSubItem();
                return true;
            }

            // Tab/Shift+Tab: navigate items (same as Down/Up)
            if (KeyInput.GetKeyDown(KeyCode.Tab))
            {
                bool shift = KeyInput.GetKey(KeyCode.LeftShift) || KeyInput.GetKey(KeyCode.RightShift);
                if (shift)
                {
                    if (_subPanelIndex > 0)
                    {
                        _subPanelIndex--;
                        AnnounceCurrentSubItem();
                    }
                    else
                        _announcer.AnnounceInterrupt(Strings.BeginningOfList);
                }
                else
                {
                    if (_subPanelIndex < _subPanelItems.Count - 1)
                    {
                        _subPanelIndex++;
                        AnnounceCurrentSubItem();
                    }
                    else
                        _announcer.AnnounceInterrupt(Strings.EndOfList);
                }
                return true;
            }

            // Enter: select item
            if (KeyInput.GetKeyDown(KeyCode.Return) || KeyInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_subPanelIndex >= 0 && _subPanelIndex < _subPanelItems.Count)
                {
                    var item = _subPanelItems[_subPanelIndex];
                    if (item.GameObject != null)
                    {
                        if (_subPanelType == "Emote")
                        {
                            ToggleEmote(item);
                        }
                        else
                        {
                            _announcer.Announce(Strings.Activating(item.Label));
                            UIActivator.Activate(item.GameObject);
                            TryPersistAvatarSelection(item);
                            TryPersistPetSelection(item);
                        }
                    }
                }
                return true;
            }

            // Backspace: close sub-panel and return to main
            if (KeyInput.GetKeyDown(KeyCode.Backspace))
            {
                CloseSubPanel();
                return true;
            }

            return false;
        }

        /// <summary>
        /// After previewing an avatar via UIActivator, invoke DoneButton_OnClick to persist
        /// the selection and close the panel. Skips locked avatars.
        /// </summary>
        private void TryPersistAvatarSelection(SubPanelItem item)
        {
            if (_subPanelType != "Avatar") return;
            if (item.GameObject == null || _profileCache.Handles.AvatarSelectPanelType == null || _profileCache.Handles.DoneButtonOnClick == null) return;

            // Check if avatar is locked via IsLocked() on the AvatarSelection component
            if (_profileCache.Handles.AvatarIsLocked != null)
            {
                try
                {
                    var avatarSelType = FindType("AvatarSelection");
                    if (avatarSelType != null)
                    {
                        var avatarComp = item.GameObject.GetComponent(avatarSelType);
                        if (avatarComp != null && (bool)_profileCache.Handles.AvatarIsLocked.Invoke(avatarComp, null))
                        {
                            Log.Msg("{NavigatorId}", $"Avatar is locked, skipping persistence");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"IsLocked check failed: {ex.Message}");
                }
            }

            // Find AvatarSelectPanel in parent hierarchy and invoke DoneButton_OnClick
            try
            {
                var panel = item.GameObject.GetComponentInParent(_profileCache.Handles.AvatarSelectPanelType);
                if (panel == null)
                {
                    Log.Msg("{NavigatorId}", $"AvatarSelectPanel not found in parent hierarchy");
                    return;
                }

                _profileCache.Handles.DoneButtonOnClick.Invoke(panel, null);
                Log.Msg("{NavigatorId}", $"Avatar selection persisted via DoneButton_OnClick");
                _persistedDuringSubPanel = true;
                // Panel closes automatically via _backButton_OnClicked callback;
                // polling will detect mode change and call ExitSubPanel(), which schedules the rescan.
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"DoneButton_OnClick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// PetPopUpV2 uses a two-step flow: clicking a pet item only previews it
        /// (sets _selectPetListIcon, shows 3D preview); the selection is only persisted
        /// when the Confirm button is clicked, which calls OnConfirm() → _onPetSelected.Invoke().
        /// After the preview click, invoke OnConfirm directly so Enter persists in one keypress.
        /// Skips locked pets (the game itself hides the confirm buttons when !IsOwned).
        /// </summary>
        private void TryPersistPetSelection(SubPanelItem item)
        {
            if (_subPanelType != "Pet") return;
            if (item.GameObject == null) return;
            if (_profileCache.Handles.PetPopUpV2Type == null || _profileCache.Handles.PetOnConfirm == null) return;

            // Skip persistence for locked pets — the game's confirm buttons aren't shown
            if (_profileCache.Handles.SelectPetsListItemViewType != null && _profileCache.Handles.SelectPetsItemIsOwned != null)
            {
                try
                {
                    var petItem = item.GameObject.GetComponent(_profileCache.Handles.SelectPetsListItemViewType);
                    if (petItem != null && !(bool)_profileCache.Handles.SelectPetsItemIsOwned.GetValue(petItem))
                    {
                        Log.Msg("{NavigatorId}", "Pet is locked, skipping persistence");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"IsOwned check failed: {ex.Message}");
                }
            }

            try
            {
                var popup = item.GameObject.GetComponentInParent(_profileCache.Handles.PetPopUpV2Type);
                if (popup == null)
                {
                    Log.Msg("{NavigatorId}", "PetPopUpV2 not found in parent hierarchy");
                    return;
                }

                _profileCache.Handles.PetOnConfirm.Invoke(popup, null);
                Log.Msg("{NavigatorId}", "Pet selection persisted via OnConfirm");
                _persistedDuringSubPanel = true;
                // Popup closes itself via Hide() in OnConfirm; OnPopupClosed → ExitSubPanel schedules the rescan.
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"OnConfirm failed: {ex.Message}");
            }
        }

        #endregion

        #region Emote Selection

        /// <summary>
        /// Resolves the live EmoteSelectionController. DisplayItemEmote is gone; the controller is
        /// now built lazily by EmoteSelectorOpener the first time its selector is opened and kept
        /// in EmoteSelectorOpener._controller, so it is only reachable through that opener.
        /// </summary>
        private object GetEmoteSelector()
        {
            var h = _profileCache.Handles;
            if (h?.EmoteOpenerController == null) return null;

            // Prefer the opener the user actually went through, but fall back to the one on the
            // live customization host so a save can still be driven if state was lost. Only trust
            // _activeOpener while the emote panel is the one open — it holds a different opener
            // type for every other cosmetic.
            var opener = (_subPanelType == "Emote" ? _activeOpener : null) ?? ResolveOpener("Emote");
            if (opener == null) return null;

            try
            {
                return h.EmoteOpenerController.GetValue(opener);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"GetEmoteSelector failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ids of the fixed "Classic" emotes. The game grants these permanently and treats them as
        /// always equipped (EmoteUtils sets isClickable = !isDefault), so they are status-only —
        /// pressing Enter on one can never change anything.
        /// </summary>
        private HashSet<string> GetDefaultEmoteIds()
        {
            var result = new HashSet<string>();
            var h = _profileCache.Handles;
            if (h?.EmoteDataProvider == null || h.EmoteGetDefaultData == null)
                return result;

            try
            {
                // The provider moved onto the controller itself when DisplayItemEmote was removed.
                var selector = GetEmoteSelector();
                var provider = selector == null ? null : h.EmoteDataProvider.GetValue(selector);
                if (provider == null) return result;

                if (!(h.EmoteGetDefaultData.Invoke(provider, null) is System.Collections.IEnumerable defaults)) return result;

                var idField = FindType("EmoteData")?.GetField("Id", PublicInstance);
                if (idField == null) return result;

                foreach (var data in defaults)
                {
                    if (data == null) continue;
                    if (idField.GetValue(data) is string id && !string.IsNullOrEmpty(id))
                        result.Add(id);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"GetDefaultEmoteIds failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Reads EmoteView._isEquipped. This is the field EmoteUtils.UpdateEquippedEmote writes
        /// through SetEquipped() during the click, so it is already current on read-back.
        /// </summary>
        private bool? ReadEmoteEquipped(GameObject emoteObject)
        {
            var h = _profileCache.Handles;
            if (emoteObject == null || h?.EmoteViewType == null || h.EmoteIsEquipped == null) return null;

            try
            {
                var view = emoteObject.GetComponent(h.EmoteViewType);
                if (view == null) return null;
                return h.EmoteIsEquipped.GetValue(view) as bool?;
            }
            catch
            {
                return null;
            }
        }

        private string ReadEmoteId(GameObject emoteObject)
        {
            var h = _profileCache.Handles;
            if (emoteObject == null || h?.EmoteViewType == null || h.EmoteViewId == null) return null;

            try
            {
                var view = emoteObject.GetComponent(h.EmoteViewType);
                return view == null ? null : h.EmoteViewId.GetValue(view) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The game's own "Phrases 3/15" / "Stickers 2/10" header text, already localized.
        /// Sighted players read these off the panel; announcing them after each toggle is what
        /// keeps the equip caps discoverable instead of only surfacing as a failure on save.
        /// </summary>
        private string ReadEmoteCounts()
        {
            var h = _profileCache.Handles;
            if (h?.EmotePopupView == null) return null;

            try
            {
                var selector = GetEmoteSelector();
                if (selector == null) return null;

                var view = h.EmotePopupView.GetValue(selector);
                if (view == null) return null;

                string phrases = (h.EmotePhrasesText?.GetValue(view) as TMP_Text)?.text;
                string stickers = (h.EmoteStickersText?.GetValue(view) as TMP_Text)?.text;

                var parts = new List<string>(2);
                if (!string.IsNullOrWhiteSpace(phrases)) parts.Add(phrases.Trim());
                if (!string.IsNullOrWhiteSpace(stickers)) parts.Add(stickers.Trim());
                return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : null;
            }
            catch
            {
                return null;
            }
        }

        private bool EmoteHasUnsavedChanges()
        {
            var h = _profileCache.Handles;
            if (h?.EmoteHasUnsavedChanges == null) return false;

            try
            {
                var selector = GetEmoteSelector();
                if (selector == null) return false;
                return h.EmoteHasUnsavedChanges.GetValue(selector) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Commits the pending emote selection through the game's own confirm path
        /// (EmoteSelectionController.SaveAndHide, the confirm button's callback).
        /// Returns false only when the game refused the save because an equip cap was exceeded —
        /// in that case it has put its own modal on screen and nothing was written.
        /// </summary>
        private bool TrySaveEmoteSelection()
        {
            var h = _profileCache.Handles;
            if (h?.EmoteSaveAndHide == null) return true;   // nothing we can drive; let the close proceed

            var selector = GetEmoteSelector();
            if (selector == null) return true;

            if (!EmoteHasUnsavedChanges())
                return true;

            try
            {
                h.EmoteSaveAndHide.Invoke(selector, null);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"SaveAndHide failed: {ex.Message}");
                return true;    // never trap the user behind a reflection failure
            }

            // Save() clears HasUnsavedChanges on success. Still set means the equip cap was
            // exceeded and the game raised ShowExceededEquippedEmotesSystemMessage instead.
            if (EmoteHasUnsavedChanges())
            {
                Log.Msg("{NavigatorId}", "Emote save refused (equip cap exceeded)");
                return false;
            }

            Log.Msg("{NavigatorId}", "Emote selection saved via SaveAndHide");
            _persistedDuringSubPanel = true;
            return true;
        }

        /// <summary>
        /// Enter on an emote. Activation still goes through UIActivator like every other cosmetic —
        /// only the read-back is emote-specific, because equipped state lives on EmoteView._isEquipped
        /// rather than the "Selected" animator flag the other selectors use.
        /// </summary>
        private void ToggleEmote(SubPanelItem item)
        {
            if (!item.Toggleable)
            {
                // Classic emotes are permanently granted; the game never wires them for clicking.
                _announcer.Announce(Strings.ProfileEmoteAlwaysEquipped(item.Label));
                return;
            }

            bool? before = ReadEmoteEquipped(item.GameObject);
            var activation = UIActivator.Activate(item.GameObject);

            if (!activation.Success)
            {
                _announcer.Announce(activation.Message);
                return;
            }

            bool? after = ReadEmoteEquipped(item.GameObject);

            // No state handle, or the click did not take — do not claim a toggle that did not happen.
            if (!after.HasValue)
            {
                _announcer.Announce(Strings.Activating(item.Label));
                return;
            }
            if (before.HasValue && before.Value == after.Value)
            {
                _announcer.Announce(Strings.ProfileEmoteToggleFailed(item.Label));
                return;
            }

            item.Status = after.Value ? Strings.ProfileItemEquipped : Strings.ProfileItemNotEquipped;
            _subPanelItems[_subPanelIndex] = item;

            string counts = ReadEmoteCounts();
            string announcement = $"{item.Label}, {item.Status}";
            if (!string.IsNullOrEmpty(counts))
                announcement = $"{announcement}. {counts}";

            _announcer.AnnounceInterrupt(announcement);
        }

        /// <summary>
        /// Drops the pending emote changes and closes the selector, mirroring what the game does
        /// today when the panel is dismissed without confirming.
        /// </summary>
        private void DiscardEmoteSelection()
        {
            var h = _profileCache.Handles;
            if (h?.EmoteSelectorClose == null) return;

            try
            {
                var selector = GetEmoteSelector();
                if (selector != null)
                    h.EmoteSelectorClose.Invoke(selector, null);
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"Emote selector Close failed: {ex.Message}");
            }
        }

        #endregion

        #region Sub-Panel Management

        private void ActivateCosmeticButton(InfoBlock block)
        {
            _announcer.Announce(Strings.Activating(block.Label));

            // Remember the opener for every category, not just the polled ones: it is what closes
            // the selector again on Backspace, whichever way the panel was detected.
            _activeOpener = block.CosmeticOpener ?? ResolveOpener(block.CosmeticType);

            if (!OpenCosmeticSelector(block))
            {
                _activeOpener = null;
                _announcer.Announce(Strings.CosmeticOpenFailed(block.Label), AnnouncementPriority.High);
                return;
            }

            // Selectors are instantiated from a prefab on first open, so none of them exist yet at
            // this point. Poll the opener until its selector shows up — the same way for all five.
            _pollForSelectorPanel = true;
            _subPanelPollTimer = 0;
            _subPanelPollElapsed = 0;
            _subPanelType = block.CosmeticType;
        }

        /// <summary>
        /// Opens a cosmetic selector through CosmeticSelectorOpener.Open(). The opener's button is
        /// a plain click target, but by the time the user reaches this the button row may already
        /// be hidden, so driving Open() directly is both simpler and more reliable than a synthetic
        /// click. Falls back to activating the button if the method could not be resolved.
        /// </summary>
        private bool OpenCosmeticSelector(InfoBlock block)
        {
            var h = _profileCache.Handles;
            if (_activeOpener != null && h?.OpenerOpen != null)
            {
                try
                {
                    h.OpenerOpen.Invoke(_activeOpener, null);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"Opener.Open() failed for {block.CosmeticType}: {ex.Message}");
                }
            }

            if (block.GameObject == null) return false;
            UIActivator.Activate(block.GameObject);
            return true;
        }

        /// <summary>Re-reads a cosmetic type's opener from the live customization host.</summary>
        private MonoBehaviour ResolveOpener(string cosmeticType)
        {
            var host = GetActiveCustomizationHost();
            var field = GetOpenerField(_profileCache.Handles, cosmeticType);
            if (host == null || field == null) return null;

            try { return field.GetValue(host) as MonoBehaviour; }
            catch { return null; }
        }

        /// <summary>
        /// Keeps the cosmetic selectors out of generic popup mode.
        ///
        /// Title, Pet and Sleeve are PopupBase types, so PanelStateManager reports them — but all
        /// five cosmetics are now entered and left through their opener, and letting the base class
        /// also claim three of them would put the navigator in two modes at once. The match is by
        /// GameObject identity against the live openers rather than by panel name, so it cannot
        /// drift the way the old Contains("Title") / Contains("CardBack") test could.
        ///
        /// Anything else on the screen — a store redirect, the emote equip-cap modal — still gets
        /// the base popup treatment.
        /// </summary>
        protected override bool IsPopupExcluded(PanelInfo panel)
        {
            if (base.IsPopupExcluded(panel)) return true;
            if (panel?.GameObject == null) return false;
            return IsCosmeticSelectorGo(panel.GameObject);
        }

        /// <summary>Whether a GameObject is one of the five openers' selector instances.</summary>
        private bool IsCosmeticSelectorGo(GameObject go)
        {
            if (go == null) return false;

            var host = GetActiveCustomizationHost();
            if (host == null) return false;

            foreach (string type in CosmeticTypes)
            {
                var openerField = GetOpenerField(_profileCache.Handles, type);
                var selectorField = GetOpenerSelectorField(_profileCache.Handles, type);
                if (openerField == null || selectorField == null) continue;

                try
                {
                    var opener = openerField.GetValue(host) as MonoBehaviour;
                    if (opener == null) continue;
                    if (selectorField.GetValue(opener) is MonoBehaviour selector && selector.gameObject == go)
                        return true;
                }
                catch { }
            }

            return false;
        }

        private void EnterSubPanel(GameObject panelGo)
        {
            _inSubPanel = true;
            _pollForSelectorPanel = false;
            _subPanelPollElapsed = 0f;
            _subPanelItems.Clear();
            _subPanelIndex = 0;

            DiscoverSubPanelItems(panelGo);

            string panelLabel = _subPanelType ?? "Items";
            _announcer.AnnounceInterrupt(Strings.ProfileSubPanelOpened(panelLabel, _subPanelItems.Count));

            if (_subPanelItems.Count > 0)
                AnnounceCurrentSubItem();
        }

        private void ExitSubPanel()
        {
            _inSubPanel = false;
            _pollForSelectorPanel = false;
            _subPanelPollElapsed = 0f;
            _activeOpener = null;
            _subPanelItems.Clear();
            _subPanelType = null;
            _emoteDiscardArmed = false;

            // Restore main screen info
            _infoIndex = _savedMainIndex;
            _infoBlocks.Clear();
            BuildInfoBlocks();

            _announcer.AnnounceInterrupt(Strings.Back);

            if (_infoIndex >= 0 && _infoIndex < _infoBlocks.Count)
                AnnounceCurrentBlock();

            // If the user actually applied a cosmetic change, schedule a deferred silent
            // rebuild — game-side reactive bindings often haven't propagated by this frame.
            if (_persistedDuringSubPanel)
            {
                _persistedDuringSubPanel = false;
                TriggerRescan();
            }
        }

        private void CloseSubPanel()
        {
            // Emotes are the one deferred-commit selector: the panel batches toggles and only
            // writes them when its confirm button runs SaveAndHide(). Backspace has to stand in
            // for that button, otherwise every change is silently dropped on the way out.
            if (_subPanelType == "Emote" && !_emoteDiscardArmed)
            {
                if (!TrySaveEmoteSelection())
                {
                    // Equip cap exceeded: the game is showing its own modal and nothing was saved.
                    // Arm the discard so a second Backspace always gets the user out.
                    _emoteDiscardArmed = true;
                    _announcer.Announce(Strings.ProfileEmoteSaveBlocked, AnnouncementPriority.High);
                    return;
                }
            }
            else if (_subPanelType == "Emote")
            {
                DiscardEmoteSelection();
                _announcer.Announce(Strings.ProfileEmoteChangesDiscarded, AnnouncementPriority.High);
            }

            DismissCosmeticSelector();
            ExitSubPanel();
        }

        /// <summary>
        /// Closes whichever cosmetic selector is showing, for all five categories.
        ///
        /// The old GoBackToPreviousMode() is gone: cosmetic selectors are no longer screen modes at
        /// all. Each one is a prefab that its CosmeticSelectorOpener instantiates on first use and
        /// then just shows and hides, and every selector's own back button is wired to the opener's
        /// HandleSelectorClosed(). Calling that is therefore exactly what the game does when the
        /// user clicks back: it hides the selector *and* raises Closed, which is what makes
        /// CustomizationPanel show the cosmetic button row again. Hiding the selector without that
        /// event would leave the profile screen with no visible cosmetic buttons.
        ///
        /// HideSelector() is the public half of the same pair and is kept as a fallback; the
        /// screen-level GoToProfileDetails() is the last resort if neither resolved.
        /// </summary>
        private void DismissCosmeticSelector()
        {
            var h = _profileCache.Handles;

            if (_activeOpener != null && h?.OpenerHandleSelectorClosed != null)
            {
                try
                {
                    h.OpenerHandleSelectorClosed.Invoke(_activeOpener, null);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"HandleSelectorClosed failed for {_subPanelType}: {ex.Message}");
                }
            }

            if (_activeOpener != null && h?.OpenerHideSelector != null)
            {
                try
                {
                    h.OpenerHideSelector.Invoke(_activeOpener, null);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"HideSelector failed for {_subPanelType}: {ex.Message}");
                }
            }

            if (_controller != null && h?.GoToProfileDetails != null)
            {
                try
                {
                    h.GoToProfileDetails.Invoke(_controller, null);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warn("{NavigatorId}", $"GoToProfileDetails failed: {ex.Message}");
                }
            }

            Log.Warn("{NavigatorId}", $"No way to close the {_subPanelType} selector — it may stay on screen");
        }

        #endregion

        #region Sub-Panel Item Discovery

        private void DiscoverSubPanelItems(GameObject panelGo)
        {
            switch (_subPanelType)
            {
                case "Avatar":
                    DiscoverAvatarItems(panelGo);
                    break;
                case "Title":
                    DiscoverTitleItems(panelGo);
                    break;
                case "Emote":
                    DiscoverEmoteItems(panelGo);
                    break;
                case "Pet":
                    DiscoverPetItems(panelGo);
                    break;
                case "Sleeve":
                    DiscoverSleeveItems(panelGo);
                    break;
                default:
                    DiscoverGenericItems(panelGo);
                    break;
            }

            // If no items found under panelGo for Avatar/Emote, try scene-wide search
            if (_subPanelItems.Count == 0 && (_subPanelType == "Avatar" || _subPanelType == "Emote"))
            {
                Log.Msg("{NavigatorId}", $"No {_subPanelType} items found under panelGo, trying scene-wide search");
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    switch (_subPanelType)
                    {
                        case "Avatar": DiscoverAvatarItems(root); break;
                        case "Emote": DiscoverEmoteItems(root); break;
                    }
                    if (_subPanelItems.Count > 0) break;
                }
            }
        }

        private void DiscoverAvatarItems(GameObject panelGo)
        {
            // AvatarSelectPanel contains AvatarSelection children
            // AvatarSelection has NameString (localized name), IsLocked(), animator Toggle state
            var avatarSelType = FindType("AvatarSelection");
            if (avatarSelType == null)
            {
                Log.Msg("{NavigatorId}", $"AvatarSelection type not found, falling back to generic");
                DiscoverGenericItems(panelGo);
                return;
            }

            var nameField = avatarSelType.GetProperty("NameString", PublicInstance);
            var idProp = avatarSelType.GetProperty("Id", PublicInstance);
            var lockedField = avatarSelType.GetField("_locked", PrivateInstance);
            var defaultField = avatarSelType.GetField("_default", PrivateInstance);
            bool loggedType = false;

            foreach (var mb in panelGo.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType() != avatarSelType) continue;
                if (!mb.gameObject.activeInHierarchy) continue;

                if (!loggedType)
                {
                    loggedType = true;
                    LogTypeMembers(avatarSelType, "AvatarItem");
                }

                string name = null;

                // Try NameString property (returns LocalizedString, ToString() gives localized text)
                if (nameField != null)
                {
                    try { name = nameField.GetValue(mb)?.ToString(); }
                    catch { }
                }

                // Fallback: read via ProfileUtilities loc key pattern
                if (string.IsNullOrEmpty(name) && idProp != null)
                {
                    try
                    {
                        string id = idProp.GetValue(mb)?.ToString();
                        if (!string.IsNullOrEmpty(id))
                            name = UITextExtractor.ResolveLocKey($"MainNav/Profile/Avatars/{id}_Name");
                    }
                    catch { }
                }

                // Fallback: read TMP_Text children
                if (string.IsNullOrEmpty(name))
                    name = ReadFirstText(mb.gameObject);
                if (string.IsNullOrEmpty(name))
                    name = mb.gameObject.name;

                // Read bio text from BioString property (appended after status below)
                string bio = null;
                if (_profileCache.Handles.BioString != null)
                {
                    try { bio = _profileCache.Handles.BioString.GetValue(mb)?.ToString(); }
                    catch { }
                }

                // Check status: _default (selected/equipped), _locked
                string status = null;
                bool isLocked = false;
                if (defaultField != null)
                {
                    try
                    {
                        if ((bool)defaultField.GetValue(mb))
                            status = Strings.ProfileItemSelected;
                    }
                    catch { }
                }
                if (status == null && lockedField != null)
                {
                    try
                    {
                        isLocked = (bool)lockedField.GetValue(mb);
                        status = isLocked ? Strings.ProfileItemLocked : Strings.ProfileItemOwned;
                    }
                    catch { }
                }

                // For locked avatars, check if purchasable in store
                if (isLocked && _profileCache.Handles.StoreSection != null)
                {
                    try
                    {
                        var storeSection = _profileCache.Handles.StoreSection.GetValue(mb);
                        // EStoreSection.None = 0; non-zero means purchasable
                        if (storeSection != null && (int)storeSection != 0)
                            status = $"{Strings.ProfileItemLocked}, {Strings.ProfileItemStore}";
                    }
                    catch { }
                }

                // Append bio after status so name+status is heard first, then the long bio
                if (!string.IsNullOrEmpty(bio))
                {
                    if (!string.IsNullOrEmpty(status))
                        status = $"{status}. {bio}";
                    else
                        status = bio;
                }

                _subPanelItems.Add(new SubPanelItem
                {
                    Label = name,
                    GameObject = mb.gameObject,
                    Status = status
                });
            }
        }

        private void DiscoverTitleItems(GameObject panelGo)
        {
            // TitleListViewItem with LocalizedText, IsOwned properties
            var titleItemType = FindType("TitleListViewItem");
            if (titleItemType == null)
            {
                DiscoverGenericItems(panelGo);
                return;
            }

            var localizedTextProp = titleItemType.GetProperty("LocalizedText", PublicInstance);
            var isOwnedProp = titleItemType.GetProperty("IsOwned", PublicInstance);

            foreach (var mb in panelGo.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType() != titleItemType) continue;
                if (!mb.gameObject.activeInHierarchy) continue;

                string name = null;
                if (localizedTextProp != null)
                {
                    try { name = localizedTextProp.GetValue(mb)?.ToString(); }
                    catch { }
                }

                if (string.IsNullOrEmpty(name))
                    name = ReadFirstText(mb.gameObject);
                if (string.IsNullOrEmpty(name))
                    name = mb.gameObject.name;

                string status = null;
                if (isOwnedProp != null)
                {
                    try
                    {
                        bool owned = (bool)isOwnedProp.GetValue(mb);
                        status = owned ? Strings.ProfileItemOwned : Strings.ProfileItemLocked;
                    }
                    catch { }
                }

                // Check for selected state via animator
                if (CheckAnimatorBool(mb.gameObject, "Selected"))
                    status = Strings.ProfileItemSelected;

                _subPanelItems.Add(new SubPanelItem
                {
                    Label = name,
                    GameObject = mb.gameObject,
                    Status = status
                });
            }
        }

        private void DiscoverEmoteItems(GameObject panelGo)
        {
            // EmoteView children with emote names.
            // Equipped state is EmoteView._isEquipped (animator parameter "Equipped"). The old
            // CheckAnimatorBool(go, "Selected") probe never matched — EmoteView has no such
            // parameter — so no emote ever announced a state.
            var emoteViewType = _profileCache.Handles?.EmoteViewType ?? FindType("EmoteView");
            if (emoteViewType != null)
            {
                var defaultEmoteIds = GetDefaultEmoteIds();

                foreach (var mb in panelGo.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType() != emoteViewType) continue;
                    if (!mb.gameObject.activeInHierarchy) continue;

                    string name = ReadFirstText(mb.gameObject);
                    if (string.IsNullOrEmpty(name))
                        name = mb.gameObject.name;

                    string emoteId = ReadEmoteId(mb.gameObject);
                    bool isDefault = !string.IsNullOrEmpty(emoteId) && defaultEmoteIds.Contains(emoteId);
                    bool? equipped = ReadEmoteEquipped(mb.gameObject);

                    string status;
                    if (!equipped.HasValue)
                        status = CheckAnimatorBool(mb.gameObject, "Equipped") ? Strings.ProfileItemEquipped : null;
                    else if (isDefault)
                        status = Strings.ProfileItemEquipped;   // Classic emotes are always on
                    else
                        status = equipped.Value ? Strings.ProfileItemEquipped : Strings.ProfileItemNotEquipped;

                    _subPanelItems.Add(new SubPanelItem
                    {
                        Label = name,
                        GameObject = mb.gameObject,
                        Status = status,
                        Toggleable = !isDefault && equipped.HasValue
                    });
                }
            }

            if (_subPanelItems.Count == 0)
                DiscoverGenericItems(panelGo);
        }

        private void DiscoverPetItems(GameObject panelGo)
        {
            // SelectPetsListItemView with PetEntry, IsOwned, IsDefault
            var petItemType = FindType("SelectPetsListItemView");
            PropertyInfo petIdProp = null;
            PropertyInfo isOwnedProp = null;
            FieldInfo isDefaultField = null;
            FieldInfo isSelectedField = null;
            bool loggedFields = false;

            if (petItemType != null)
            {
                petIdProp = petItemType.GetProperty("PetId", PublicInstance);
                isOwnedProp = petItemType.GetProperty("IsOwned", PublicInstance);
                isDefaultField = petItemType.GetField("_isDefault", PrivateInstance);
                isSelectedField = petItemType.GetField("_isSelected", PrivateInstance);
            }

            if (petItemType == null)
            {
                DiscoverGenericItems(panelGo);
                return;
            }

            foreach (var mb in panelGo.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType() != petItemType) continue;
                if (!mb.gameObject.activeInHierarchy) continue;

                // Log fields/properties of the first item for debugging
                if (!loggedFields)
                {
                    loggedFields = true;
                    LogTypeMembers(mb.GetType(), "PetItem");
                }

                string name = null;
                string acquisitionText = ReadTooltipText(mb.gameObject);
                name = CosmeticItemResolver.ResolvePetDescription(
                    mb,
                    acquisitionText,
                    UITextExtractor.ResolveLocKey);

                // Try any TMP_Text child
                if (string.IsNullOrEmpty(name))
                    name = ReadFirstText(mb.gameObject);

                // Humanize internal ID if nothing else worked
                if (string.IsNullOrEmpty(name))
                {
                    string petId = null;
                    try
                    {
                        petId = petIdProp?.GetValue(mb) as string;
                    }
                    catch { }
                    name = !string.IsNullOrEmpty(petId) ? HumanizeInternalId(petId) : Strings.ProfileCosmeticNone;
                }

                // Check selected/default status via private fields
                string status = null;
                if (isSelectedField != null)
                {
                    try
                    {
                        if ((bool)isSelectedField.GetValue(mb))
                            status = Strings.ProfileItemSelected;
                    }
                    catch { }
                }
                if (status == null && isDefaultField != null)
                {
                    try
                    {
                        if ((bool)isDefaultField.GetValue(mb))
                            status = Strings.ProfileItemDefault;
                    }
                    catch { }
                }
                if (status == null && isOwnedProp != null)
                {
                    try
                    {
                        bool owned = (bool)isOwnedProp.GetValue(mb);
                        status = owned ? Strings.ProfileItemOwned : Strings.ProfileItemLocked;
                    }
                    catch { }
                }

                _subPanelItems.Add(new SubPanelItem
                {
                    Label = name,
                    GameObject = mb.gameObject,
                    Status = status
                });
            }
        }

        private void DiscoverSleeveItems(GameObject panelGo)
        {
            // CardBackSelector with CardBack string, Collected bool
            var selectorType = FindType("CardBackSelector");
            bool loggedFields = false;

            if (selectorType == null)
            {
                DiscoverGenericItems(panelGo);
                return;
            }

            foreach (var mb in panelGo.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType() != selectorType) continue;
                if (!mb.gameObject.activeInHierarchy) continue;

                // Log fields/properties of the first item for debugging
                if (!loggedFields)
                {
                    loggedFields = true;
                    LogTypeMembers(mb.GetType(), "SleeveItem");
                }

                string name = null;
                string cardBack = null;

                // Try to read any string field/property that looks like a name or identifier
                foreach (var field in mb.GetType().GetFields(AllInstanceFlags))
                {
                    if (field.FieldType != typeof(string)) continue;
                    try
                    {
                        string val = field.GetValue(mb) as string;
                        if (!string.IsNullOrEmpty(val))
                        {
                            Log.Msg("{NavigatorId}", $"Sleeve field {field.Name}={val}");
                            if (cardBack == null)
                                cardBack = val;
                        }
                    }
                    catch { }
                }

                // Try to resolve localized name from card back ID
                if (!string.IsNullOrEmpty(cardBack))
                {
                    // Try direct loc key patterns first
                    string[] locPatterns =
                    {
                        $"MainNav/Cosmetics/CardBack/{cardBack}",
                        $"MainNav/Cosmetics/Sleeve/{cardBack}",
                    };
                    foreach (var pattern in locPatterns)
                    {
                        string locName = UITextExtractor.ResolveLocKey(pattern);
                        if (!string.IsNullOrEmpty(locName))
                        {
                            name = locName;
                            break;
                        }
                    }

                    // Try extracting set code from ID (e.g., "CardBack_DMU_StainedGlassBasics" → "DMU")
                    if (string.IsNullOrEmpty(name))
                    {
                        string stripped = cardBack.StartsWith("CardBack_") ? cardBack.Substring(9) : cardBack;
                        // Try set code as first segment (e.g., "DMU_StainedGlassBasics" → set "DMU")
                        int underscoreIdx = stripped.IndexOf('_');
                        if (underscoreIdx > 0)
                        {
                            string possibleSetCode = stripped.Substring(0, underscoreIdx);
                            string setName = UITextExtractor.ResolveLocKey($"General/Sets/{possibleSetCode}");
                            if (!string.IsNullOrEmpty(setName))
                            {
                                // Use set name + rest as descriptor
                                string descriptor = stripped.Substring(underscoreIdx + 1);
                                name = $"{setName} - {HumanizeInternalId(descriptor)}";
                            }
                        }
                    }

                    // Humanize the internal ID as fallback
                    if (string.IsNullOrEmpty(name))
                        name = HumanizeInternalId(cardBack);
                }

                // Try tooltip data
                if (string.IsNullOrEmpty(name))
                    name = ReadTooltipText(mb.gameObject);

                if (string.IsNullOrEmpty(name))
                    name = mb.gameObject.name;

                // Check collected/owned status via bool properties
                string status = null;
                foreach (var prop in mb.GetType().GetProperties(PublicInstance))
                {
                    if (prop.PropertyType != typeof(bool)) continue;
                    try
                    {
                        bool val = (bool)prop.GetValue(mb);
                        string pName = prop.Name.ToLower();
                        if (pName.Contains("collected") || pName.Contains("owned"))
                            status = val ? Strings.ProfileItemOwned : Strings.ProfileItemLocked;
                        else if (pName.Contains("default") || pName.Contains("selected"))
                        {
                            if (val) status = Strings.ProfileItemSelected;
                        }
                    }
                    catch { }
                }

                _subPanelItems.Add(new SubPanelItem
                {
                    Label = name,
                    GameObject = mb.gameObject,
                    Status = status
                });
            }
        }

        private void DiscoverGenericItems(GameObject panelGo)
        {
            // Fallback: find all CustomButton children with text
            foreach (var mb in panelGo.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name != T.CustomButton) continue;
                if (!mb.gameObject.activeInHierarchy) continue;

                string name = UITextExtractor.GetText(mb.gameObject);
                if (string.IsNullOrEmpty(name) || name == "button")
                    name = mb.gameObject.name;

                _subPanelItems.Add(new SubPanelItem
                {
                    Label = name,
                    GameObject = mb.gameObject,
                    Status = null
                });
            }
        }

        #endregion

        #region Update Loop

        public override void Update()
        {
            base.Update();

            if (!_isActive) return;

            // Wait for the selector the user just opened to appear.
            if (_pollForSelectorPanel)
            {
                _subPanelPollTimer += Time.deltaTime;
                if (_subPanelPollTimer >= SubPanelPollInterval)
                {
                    _subPanelPollTimer = 0;
                    CheckForSelectorPanel();
                }
            }

            // ...and notice when it goes away again, however it was closed: our own Backspace, the
            // selector's back button, or the game hiding it after a confirm.
            if (_inSubPanel && !IsSelectorPanelActive(_subPanelType))
            {
                Log.Msg("{NavigatorId}", $"Selector closed: {_subPanelType}");
                ExitSubPanel();
            }

            // Delayed silent rescan after cosmetic persistence + sub-panel exit.
            // Game-side reactive bindings (cosmetic provider → display item) settle one or two
            // frames after _onPetSelected / DoneButton_OnClick, so the immediate BuildInfoBlocks
            // in ExitSubPanel may read stale data. This deferred pass catches the settled state.
            if (_rescanDelay > 0f)
            {
                _rescanDelay -= Time.deltaTime;
                if (_rescanDelay <= 0f)
                {
                    _rescanDelay = 0f;
                    PerformRescan();
                }
            }
        }

        /// <summary>
        /// Schedule a silent rescan after the standard delay. Mirrors the SettingsMenuNavigator
        /// pattern (SettingsMenuNavigator.cs:1112). Only triggered from ExitSubPanel so the rescan
        /// runs once we are definitively back on the main screen — never mid-transition.
        /// </summary>
        private void TriggerRescan()
        {
            _rescanDelay = RescanDelaySeconds;
        }

        /// <summary>
        /// Silently rebuild main info blocks, preserving selection by GameObject reference.
        /// Skips while still in a sub-panel — sub-panel state transitions own their discovery and
        /// touching _subPanelItems mid-flight strands the user in an empty list (frozen state).
        /// </summary>
        private void PerformRescan()
        {
            if (!_isActive) return;
            if (_inSubPanel) return; // Defensive: never disturb sub-panel item list

            GameObject prevGo = (_infoIndex >= 0 && _infoIndex < _infoBlocks.Count)
                ? _infoBlocks[_infoIndex].GameObject : null;
            string prevLabel = (_infoIndex >= 0 && _infoIndex < _infoBlocks.Count)
                ? _infoBlocks[_infoIndex].Label : null;

            _infoBlocks.Clear();
            BuildInfoBlocks();

            // Restore by reference first; fall back to label match (cosmetic buttons share controller GO)
            int restored = -1;
            if (prevGo != null)
            {
                for (int i = 0; i < _infoBlocks.Count; i++)
                {
                    if (_infoBlocks[i].GameObject == prevGo) { restored = i; break; }
                }
            }
            if (restored < 0 && !string.IsNullOrEmpty(prevLabel))
            {
                for (int i = 0; i < _infoBlocks.Count; i++)
                {
                    if (_infoBlocks[i].Label == prevLabel) { restored = i; break; }
                }
            }

            if (restored >= 0)
                _infoIndex = restored;
            else if (_infoBlocks.Count > 0)
                _infoIndex = Math.Min(_infoIndex, _infoBlocks.Count - 1);
        }

        private void CheckForSelectorPanel()
        {
            _subPanelPollElapsed += SubPanelPollInterval;

            var panelGo = FindSelectorPanelGo(_subPanelType);
            if (panelGo != null)
            {
                Log.Msg("{NavigatorId}", $"{_subPanelType} panel detected");
                _subPanelPollElapsed = 0f;
                EnterSubPanel(panelGo);
                return;
            }

            // The selector is instantiated from a prefab the first time it is opened, so a little
            // waiting is normal — but polling forever would leave the user on a screen that never
            // announces anything, with nothing in the log to explain it.
            if (_subPanelPollElapsed >= SubPanelPollTimeoutSeconds)
            {
                Log.Warn("{NavigatorId}", $"{_subPanelType} selector never appeared after {SubPanelPollTimeoutSeconds:0.#}s");
                _announcer.Announce(Strings.CosmeticOpenFailed(_subPanelType), AnnouncementPriority.High);
                _pollForSelectorPanel = false;
                _subPanelPollElapsed = 0f;
                _subPanelType = null;
                _activeOpener = null;
            }
        }

        /// <summary>
        /// The live selector GameObject for a cosmetic type, or null when it is not showing.
        /// This is the single detection path for all five cosmetics.
        ///
        /// _profileScreenMode is no longer usable for this. The controller only sets it when its own
        /// SetMode() runs, and SetMode() is private — the mod opens selectors through the opener
        /// instead, so the mode never changes and would report the wrong panel forever. The opener's
        /// selector instance is the direct signal: it is created on first open, activated while the
        /// selector shows, and deactivated by HideSelector(). For the three PopupBase selectors that
        /// is the same GameObject PopupBase.Hide() deactivates, so it tracks their own back and
        /// confirm buttons as well.
        /// </summary>
        private GameObject FindSelectorPanelGo(string cosmeticType)
        {
            if (_activeOpener == null) return null;

            var field = GetOpenerSelectorField(_profileCache.Handles, cosmeticType);
            if (field == null) return null;

            try
            {
                var selector = field.GetValue(_activeOpener) as MonoBehaviour;
                if (selector == null || !selector.gameObject.activeInHierarchy) return null;
                return selector.gameObject;
            }
            catch { return null; }
        }

        /// <summary>Whether the given cosmetic's selector is still showing.</summary>
        private bool IsSelectorPanelActive(string type)
        {
            return FindSelectorPanelGo(type) != null;
        }

        #endregion

        #region Announcement Helpers

        private void AnnounceCurrentBlock()
        {
            if (_infoIndex < 0 || _infoIndex >= _infoBlocks.Count) return;
            _announcer.AnnounceInterrupt(GetElementAnnouncement(0));
        }

        private void AnnounceCurrentSubItem()
        {
            if (_subPanelIndex < 0 || _subPanelIndex >= _subPanelItems.Count) return;
            _announcer.AnnounceInterrupt(GetElementAnnouncement(0));
        }

        #endregion

        #region Utility

        private string ReadFirstText(GameObject go)
        {
            if (go == null) return null;
            var texts = go.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                if (t == null) continue;
                string text = UITextExtractor.CleanText(t.text);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
            return null;
        }

        /// <summary>
        /// Navigate back to the home screen by clicking Nav_Home in the nav bar.
        /// </summary>
        private void NavigateBackToHome()
        {
            // Find the Nav_Home button in the nav bar
            var homeButton = GameObject.Find("Nav_Home");
            if (homeButton != null)
            {
                _announcer.Announce(Strings.Back);
                UIActivator.Activate(homeButton);
            }
            else
            {
                // Fallback: try the game's OnHandheldBackButton
                if (_controller != null)
                {
                    try
                    {
                        var backMethod = _controller.GetType().GetMethod("OnHandheldBackButton",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (backMethod != null)
                        {
                            _announcer.Announce(Strings.Back);
                            backMethod.Invoke(_controller, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("{NavigatorId}", $"OnHandheldBackButton failed: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Read tooltip text from a TooltipTrigger component if present.
        /// TooltipTrigger.TooltipData is a public FIELD, TooltipData.Text is a property.
        /// </summary>
        private string ReadTooltipText(GameObject go)
        {
            if (go == null) return null;
            try
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    if (comp.GetType().Name != "TooltipTrigger") continue;

                    var tooltipDataField = comp.GetType().GetField("TooltipData", PublicInstance);
                    if (tooltipDataField == null) continue;

                    var tooltipData = tooltipDataField.GetValue(comp);
                    if (tooltipData == null) continue;

                    var textProp = tooltipData.GetType().GetProperty("Text", PublicInstance);
                    if (textProp != null)
                    {
                        string text = textProp.GetValue(tooltipData)?.ToString();
                        if (!string.IsNullOrEmpty(text))
                            return UITextExtractor.CleanText(text);
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Converts internal IDs like "CardBack_DMU_StainedGlassBasics" or "TMT_Mastery_Companion"
        /// into human-readable text by stripping prefixes and splitting on underscores/camelCase.
        /// </summary>
        private string HumanizeInternalId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;

            // Strip common prefixes
            if (id.StartsWith("CardBack_"))
                id = id.Substring(9);

            // Replace underscores with spaces
            id = id.Replace('_', ' ');

            // Insert spaces before capitals in camelCase (e.g., "StainedGlassBasics" → "Stained Glass Basics")
            var sb = new System.Text.StringBuilder(id.Length + 8);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (i > 0 && char.IsUpper(c) && id[i - 1] != ' ' && !char.IsUpper(id[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Log fields and properties of a type for debugging (first item only).
        /// </summary>
        private void LogTypeMembers(System.Type type, string label)
        {
            try
            {
                Log.Msg("{NavigatorId}", $"{label} type: {type.FullName}");
                foreach (var prop in type.GetProperties(PublicInstance))
                {
                    Log.Msg("{NavigatorId}", $"  Prop: {prop.Name} ({prop.PropertyType.Name})");
                }
                foreach (var field in type.GetFields(AllInstanceFlags))
                {
                    Log.Msg("{NavigatorId}", $"  Field: {field.Name} ({field.FieldType.Name})");
                }
            }
            catch { }
        }

        /// <summary>
        /// Check if an Animator component has a bool parameter set to true.
        /// Uses reflection to avoid referencing UnityEngine.AnimationModule.
        /// </summary>
        private bool CheckAnimatorBool(GameObject go, params string[] boolNames)
        {
            if (go == null) return false;
            try
            {
                // Find Animator component via reflection
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var type = comp.GetType();
                    if (type.Name != "Animator") continue;

                    var getBoolMethod = type.GetMethod("GetBool", new[] { typeof(string) });
                    if (getBoolMethod == null) continue;

                    foreach (var boolName in boolNames)
                    {
                        try
                        {
                            if ((bool)getBoolMethod.Invoke(comp, new object[] { boolName }))
                                return true;
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return false;
        }

        #endregion
    }
}
