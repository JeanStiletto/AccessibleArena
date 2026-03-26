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
        private string _subPanelType;            // "Avatar", "Emote", "Title", "Pet", "Sleeve"
        private readonly List<SubPanelItem> _subPanelItems = new List<SubPanelItem>();
        private int _subPanelIndex;
        private int _savedMainIndex;
        private float _subPanelPollTimer;
        private bool _pollForMonoBehaviourPanels; // Poll for Avatar/Emote panels (not PopupBase)

        // Info blocks for main screen navigation
        private readonly List<InfoBlock> _infoBlocks = new List<InfoBlock>();
        private int _infoIndex;

        #endregion

        #region Data Structures

        private struct InfoBlock
        {
            public string Label;
            public GameObject GameObject;     // Button to activate (for cosmetic categories)
            public bool IsActivatable;        // Whether Enter should activate this item
            public string CosmeticType;       // null for info, "Avatar"/"Title"/etc. for cosmetics
        }

        private struct SubPanelItem
        {
            public string Label;
            public GameObject GameObject;
            public string Status;             // "owned", "locked", "selected", etc.
        }

        #endregion

        #region Reflection Cache

        private bool _reflectionInitialized;

        // ProfileContentController fields
        private FieldInfo _usernameTextField;         // UsernameText (TMP_Text)
        private FieldInfo _avatarNameTextField;        // AvatarNameText (Localize)
        private FieldInfo _avatarBioTextField;         // AvatarBioText (Localize)
        private FieldInfo _profileDetailsPanelField;   // ProfileDetailsPanel
        private FieldInfo _profileScreenModeField;     // _profileScreenMode
        private FieldInfo _avatarButtonField;           // _avatarButton
        private FieldInfo _emoteButtonField;            // _emoteButton
        private FieldInfo _petButtonField;              // _petButton
        private FieldInfo _sleeveButtonField;           // _sleeveButton
        private FieldInfo _titleButtonField;            // _titleButton
        private FieldInfo _cosmeticSelectorField;       // _cosmeticSelectorController
        private FieldInfo _cosmeticSelectorsTransformField; // _cosmeticSelectorsTransform
        private FieldInfo _avatarDisplayItemField;     // _avatarDisplayItem
        private FieldInfo _emoteDisplayItemField;      // _emoteDisplayItem
        private FieldInfo _petDisplayItemField;        // _petDisplayItem
        private FieldInfo _sleeveDisplayItemField;     // _sleeveDisplayItem
        private FieldInfo _titleDisplayItemField;      // _titleDisplayItem

        // ProfileDetailsPanel fields
        private FieldInfo _constructedRankField;       // _constructedRankDisplay
        private FieldInfo _limitedRankField;           // _limitedRankDisplay
        private FieldInfo _seasonNameField;            // _seasonNameRankText
        private FieldInfo _battlePassBubbleField;      // _battlePassBubble

        // RankDisplay fields
        private FieldInfo _rankFormatTextField;         // _rankFormatText
        private FieldInfo _rankTierTextField;           // _rankTierText
        private FieldInfo _mythicPlacementTextField;    // _mythicPlacementText
        private FieldInfo _isLimitedField;              // _isLimited

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
            if (_reflectionInitialized) return;

            var controllerType = _controller?.GetType();
            if (controllerType == null) return;

            var flags = AllInstanceFlags;

            // ProfileContentController fields (serialized = private with [SerializeField])
            _usernameTextField = controllerType.GetField("UsernameText", flags);
            _avatarNameTextField = controllerType.GetField("AvatarNameText", flags);
            _avatarBioTextField = controllerType.GetField("AvatarBioText", flags);
            _profileDetailsPanelField = controllerType.GetField("ProfileDetailsPanel", flags);
            _profileScreenModeField = controllerType.GetField("_profileScreenMode", PrivateInstance);
            _avatarButtonField = controllerType.GetField("_avatarButton", PrivateInstance);
            _emoteButtonField = controllerType.GetField("_emoteButton", PrivateInstance);
            _petButtonField = controllerType.GetField("_petButton", PrivateInstance);
            _sleeveButtonField = controllerType.GetField("_sleeveButton", PrivateInstance);
            _titleButtonField = controllerType.GetField("_titleButton", PrivateInstance);
            _cosmeticSelectorField = controllerType.GetField("_cosmeticSelectorController", PrivateInstance);
            _cosmeticSelectorsTransformField = controllerType.GetField("_cosmeticSelectorsTransform", PrivateInstance);
            _avatarDisplayItemField = controllerType.GetField("_avatarDisplayItem", PrivateInstance);
            _emoteDisplayItemField = controllerType.GetField("_emoteDisplayItem", PrivateInstance);
            _petDisplayItemField = controllerType.GetField("_petDisplayItem", PrivateInstance);
            _sleeveDisplayItemField = controllerType.GetField("_sleeveDisplayItem", PrivateInstance);
            _titleDisplayItemField = controllerType.GetField("_titleDisplayItem", PrivateInstance);

            // ProfileDetailsPanel fields
            var detailsType = FindType("ProfileUI.ProfileDetailsPanel");
            if (detailsType != null)
            {
                _constructedRankField = detailsType.GetField("_constructedRankDisplay", PrivateInstance);
                _limitedRankField = detailsType.GetField("_limitedRankDisplay", PrivateInstance);
                _seasonNameField = detailsType.GetField("_seasonNameRankText", PrivateInstance);
                _battlePassBubbleField = detailsType.GetField("_battlePassBubble", PrivateInstance);
            }

            // RankDisplay fields
            var rankType = FindType("RankDisplay");
            if (rankType != null)
            {
                _rankFormatTextField = rankType.GetField("_rankFormatText", PrivateInstance);
                _rankTierTextField = rankType.GetField("_rankTierText", PrivateInstance);
                _mythicPlacementTextField = rankType.GetField("_mythicPlacementText", PrivateInstance);
                _isLimitedField = rankType.GetField("_isLimited", PrivateInstance);
            }

            _reflectionInitialized = true;
            MelonLogger.Msg($"[{NavigatorId}] Reflection cached: " +
                $"Controller={controllerType != null}, " +
                $"Details={detailsType != null}, " +
                $"Rank={rankType != null}, " +
                $"Username={_usernameTextField != null}");
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

            MelonLogger.Msg($"[{NavigatorId}] Discovered {_infoBlocks.Count} info blocks");
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

            // 6. Mastery progress
            string mastery = ReadMasteryInfo();
            if (!string.IsNullOrEmpty(mastery))
            {
                _infoBlocks.Add(new InfoBlock
                {
                    Label = Strings.ProfileMastery(mastery),
                    GameObject = _controller.gameObject,
                    IsActivatable = false
                });
            }

            // 7-11. Cosmetic category buttons
            AddCosmeticButton(_avatarButtonField, "Avatar");
            AddCosmeticButton(_titleButtonField, "Title");
            AddCosmeticButton(_emoteButtonField, "Emote");
            AddCosmeticButton(_petButtonField, "Pet");
            AddCosmeticButton(_sleeveButtonField, "Sleeve");
        }

        private void EnsureDetailsPanel()
        {
            if (_detailsPanel != null && _detailsPanel.gameObject != null && _detailsPanel.gameObject.activeInHierarchy)
                return;

            _detailsPanel = null;

            if (_profileDetailsPanelField != null && _controller != null)
            {
                try
                {
                    _detailsPanel = _profileDetailsPanelField.GetValue(_controller) as MonoBehaviour;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[{NavigatorId}] Failed to get ProfileDetailsPanel: {ex.Message}");
                }
            }
        }

        #endregion

        #region Data Extraction

        private string ReadUsername()
        {
            if (_usernameTextField == null || _controller == null) return null;
            try
            {
                var tmpText = _usernameTextField.GetValue(_controller) as TMP_Text;
                if (tmpText == null) return null;
                string text = tmpText.text;
                if (string.IsNullOrEmpty(text)) return null;
                // Strip rich text color tags (game adds <color=#696969>#1234</color> suffix)
                text = UITextExtractor.CleanText(text);
                return text;
            }
            catch { return null; }
        }

        private string ReadAvatarInfo()
        {
            if (_controller == null) return null;

            string name = ReadLocalizeText(_avatarNameTextField);
            string bio = ReadLocalizeText(_avatarBioTextField);

            if (string.IsNullOrEmpty(name)) return null;
            return Strings.ProfileAvatar(name, bio ?? "");
        }

        private string ReadLocalizeText(FieldInfo localizeField)
        {
            if (localizeField == null || _controller == null) return null;
            try
            {
                var localize = localizeField.GetValue(_controller) as MonoBehaviour;
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
            if (_seasonNameField == null || _detailsPanel == null) return null;
            try
            {
                var tmpText = _seasonNameField.GetValue(_detailsPanel) as TMP_Text;
                if (tmpText == null || !tmpText.gameObject.activeInHierarchy) return null;
                string text = UITextExtractor.CleanText(tmpText.text);
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch { return null; }
        }

        private string ReadRankInfo(bool limited)
        {
            if (_detailsPanel == null) return null;

            var rankField = limited ? _limitedRankField : _constructedRankField;
            if (rankField == null) return null;

            try
            {
                var rankDisplay = rankField.GetValue(_detailsPanel) as MonoBehaviour;
                if (rankDisplay == null) return null;
                if (!rankDisplay.gameObject.activeInHierarchy) return null;

                // Read _rankTierText (e.g., "Gold Tier 2")
                string tierText = null;
                if (_rankTierTextField != null)
                {
                    var tierTmp = _rankTierTextField.GetValue(rankDisplay) as TMP_Text;
                    if (tierTmp != null && tierTmp.gameObject.activeSelf)
                        tierText = UITextExtractor.CleanText(tierTmp.text);
                }

                // Read _mythicPlacementText (e.g., "#123" or "95%")
                string mythicText = null;
                if (_mythicPlacementTextField != null)
                {
                    var mythicTmp = _mythicPlacementTextField.GetValue(rankDisplay) as TMP_Text;
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
            if (_battlePassBubbleField == null || _detailsPanel == null) return null;
            try
            {
                var bubble = _battlePassBubbleField.GetValue(_detailsPanel) as MonoBehaviour;
                if (bubble == null || !bubble.gameObject.activeInHierarchy) return null;

                // Read popup data from ObjectiveBubble (same pattern as in MEMORY.md)
                var popupDataField = bubble.GetType().GetField("_popupData",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                // Also try reading the progress text directly from TMP children
                var texts = bubble.GetComponentsInChildren<TMP_Text>(true);
                string progressText = null;
                foreach (var t in texts)
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    string text = UITextExtractor.CleanText(t.text);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (progressText == null)
                            progressText = text;
                        else
                            progressText += ", " + text;
                    }
                }

                return progressText;
            }
            catch { return null; }
        }

        private void AddCosmeticButton(FieldInfo buttonField, string cosmeticType)
        {
            if (buttonField == null || _controller == null) return;
            try
            {
                var button = buttonField.GetValue(_controller) as MonoBehaviour;
                if (button == null || !button.gameObject.activeInHierarchy) return;

                // Get the button's text label
                string buttonLabel = UITextExtractor.GetText(button.gameObject);
                if (string.IsNullOrEmpty(buttonLabel))
                    buttonLabel = cosmeticType;

                string label = buttonLabel;

                _infoBlocks.Add(new InfoBlock
                {
                    Label = label,
                    GameObject = button.gameObject,
                    IsActivatable = true,
                    CosmeticType = cosmeticType
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[{NavigatorId}] Failed to add cosmetic button {cosmeticType}: {ex.Message}");
            }
        }

        private string ReadCurrentCosmeticValue(string cosmeticType)
        {
            // Read from the corresponding DisplayItem* field on the controller
            var displayItemField = GetDisplayItemField(cosmeticType);
            if (displayItemField == null || _controller == null) return null;

            try
            {
                var displayItem = displayItemField.GetValue(_controller) as MonoBehaviour;
                if (displayItem == null || !displayItem.gameObject.activeInHierarchy) return null;
                return ReadFirstText(displayItem.gameObject);
            }
            catch { return null; }
        }

        private FieldInfo GetDisplayItemField(string cosmeticType)
        {
            switch (cosmeticType)
            {
                case "Avatar": return _avatarDisplayItemField;
                case "Title": return _titleDisplayItemField;
                case "Emote": return _emoteDisplayItemField;
                case "Pet": return _petDisplayItemField;
                case "Sleeve": return _sleeveDisplayItemField;
                default: return null;
            }
        }

        #endregion

        #region Activation & Navigation

        protected override void OnActivated()
        {
            _infoIndex = 0;
            _inSubPanel = false;
            _pollForMonoBehaviourPanels = false;

            // Enable popup detection for PopupBase panels (Title, Pet, Sleeve)
            EnablePopupDetection();
        }

        protected override void OnDeactivating()
        {
            DisablePopupDetection();
            _inSubPanel = false;
            _pollForMonoBehaviourPanels = false;
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
            return $"{block.Label}, {_infoIndex + 1}/{_infoBlocks.Count}";
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
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

            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                _infoIndex = 0;
                AnnounceCurrentBlock();
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                _infoIndex = _infoBlocks.Count - 1;
                AnnounceCurrentBlock();
                return true;
            }

            // Enter: activate cosmetic button
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_infoIndex >= 0 && _infoIndex < _infoBlocks.Count)
                {
                    var block = _infoBlocks[_infoIndex];
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
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

            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                _subPanelIndex = 0;
                AnnounceCurrentSubItem();
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                _subPanelIndex = _subPanelItems.Count - 1;
                AnnounceCurrentSubItem();
                return true;
            }

            // Tab/Shift+Tab: navigate items (same as Down/Up)
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
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
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_subPanelIndex >= 0 && _subPanelIndex < _subPanelItems.Count)
                {
                    var item = _subPanelItems[_subPanelIndex];
                    if (item.GameObject != null)
                    {
                        _announcer.Announce(Strings.Activating(item.Label));
                        UIActivator.Activate(item.GameObject);
                    }
                }
                return true;
            }

            // Backspace: close sub-panel and return to main
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseSubPanel();
                return true;
            }

            return false;
        }

        #endregion

        #region Sub-Panel Management

        private void ActivateCosmeticButton(InfoBlock block)
        {
            _announcer.Announce(Strings.Activating(block.Label));
            UIActivator.Activate(block.GameObject);

            // For Avatar and Emote panels (MonoBehaviour, not PopupBase),
            // start polling for the panel to become active
            if (block.CosmeticType == "Avatar" || block.CosmeticType == "Emote")
            {
                _pollForMonoBehaviourPanels = true;
                _subPanelPollTimer = 0;
                _subPanelType = block.CosmeticType;
            }
            // Title, Pet, Sleeve are PopupBase → handled via OnPopupDetected
        }

        protected override void OnPopupDetected(PanelInfo panel)
        {
            if (panel?.GameObject == null)
            {
                base.OnPopupDetected(panel);
                return;
            }

            string panelName = panel.Name ?? "";

            // Identify which cosmetic popup this is
            string cosmeticType = null;
            if (panelName.Contains("Title")) cosmeticType = "Title";
            else if (panelName.Contains("Pet")) cosmeticType = "Pet";
            else if (panelName.Contains("CardBack") || panelName.Contains("Sleeve")) cosmeticType = "Sleeve";

            if (cosmeticType != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] Cosmetic popup detected: {cosmeticType} ({panelName})");
                _subPanelType = cosmeticType;
                EnterSubPanel(panel.GameObject);
            }
            else
            {
                // Unknown popup - use default popup mode
                base.OnPopupDetected(panel);
            }
        }

        protected override void OnPopupClosed()
        {
            if (_inSubPanel)
            {
                ExitSubPanel();
            }
        }

        private void EnterSubPanel(GameObject panelGo)
        {
            _inSubPanel = true;
            _pollForMonoBehaviourPanels = false;
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
            _pollForMonoBehaviourPanels = false;
            _subPanelItems.Clear();
            _subPanelType = null;

            // Restore main screen info
            _infoIndex = _savedMainIndex;
            _infoBlocks.Clear();
            BuildInfoBlocks();

            _announcer.AnnounceInterrupt(Strings.Back);

            if (_infoIndex >= 0 && _infoIndex < _infoBlocks.Count)
                AnnounceCurrentBlock();
        }

        private void CloseSubPanel()
        {
            // Try to go back via the game's GoBackToPreviousMode
            if (_controller != null)
            {
                try
                {
                    var goBackMethod = _controller.GetType().GetMethod("GoBackToPreviousMode",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (goBackMethod != null)
                    {
                        goBackMethod.Invoke(_controller, null);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[{NavigatorId}] GoBackToPreviousMode failed: {ex.Message}");
                }
            }

            ExitSubPanel();
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
                MelonLogger.Msg($"[{NavigatorId}] No {_subPanelType} items found under panelGo, trying scene-wide search");
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
                MelonLogger.Msg($"[{NavigatorId}] AvatarSelection type not found, falling back to generic");
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

                // Check status: _default (selected/equipped), _locked
                string status = null;
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
                        bool locked = (bool)lockedField.GetValue(mb);
                        status = locked ? Strings.ProfileItemLocked : Strings.ProfileItemOwned;
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
            // EmoteView children with emote names
            var emoteViewType = FindType("EmoteView");
            if (emoteViewType != null)
            {
                foreach (var mb in panelGo.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType() != emoteViewType) continue;
                    if (!mb.gameObject.activeInHierarchy) continue;

                    string name = ReadFirstText(mb.gameObject);
                    if (string.IsNullOrEmpty(name))
                        name = mb.gameObject.name;

                    string status = null;
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
                string petId = null;

                // Read PetId property (string like "TMT_Mastery_Companion", "Brushwagg")
                if (petIdProp != null)
                {
                    try { petId = petIdProp.GetValue(mb) as string; }
                    catch { }
                }

                // Try resolving localized name via multiple key patterns
                if (!string.IsNullOrEmpty(petId))
                {
                    string[] locPatterns =
                    {
                        $"MainNav/Cosmetics/Pet/{petId}_Details",
                        $"MainNav/Cosmetics/Pet/{petId}_Name",
                        $"MainNav/Cosmetics/Pet/{petId}",
                        $"MainNav/Cosmetics/Pets/{petId}_Details",
                        $"MainNav/Cosmetics/Pets/{petId}",
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
                }

                // Try tooltip data (TooltipTrigger.TooltipData.Text)
                if (string.IsNullOrEmpty(name))
                    name = ReadTooltipText(mb.gameObject);

                // Try any TMP_Text child
                if (string.IsNullOrEmpty(name))
                    name = ReadFirstText(mb.gameObject);

                // Humanize internal ID if nothing else worked
                if (string.IsNullOrEmpty(name))
                    name = !string.IsNullOrEmpty(petId) ? HumanizeInternalId(petId) : Strings.ProfileCosmeticNone;

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
                            MelonLogger.Msg($"[{NavigatorId}] Sleeve field {field.Name}={val}");
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

            // Poll for MonoBehaviour panels (Avatar, Emote) that PanelStateManager can't detect
            if (_pollForMonoBehaviourPanels)
            {
                _subPanelPollTimer += Time.deltaTime;
                if (_subPanelPollTimer >= SubPanelPollInterval)
                {
                    _subPanelPollTimer = 0;
                    CheckForMonoBehaviourPanel();
                }
            }

            // Check if a MonoBehaviour sub-panel was closed (game object deactivated)
            if (_inSubPanel && (_subPanelType == "Avatar" || _subPanelType == "Emote"))
            {
                if (!IsMonoBehaviourPanelActive(_subPanelType))
                {
                    MelonLogger.Msg($"[{NavigatorId}] MonoBehaviour panel closed: {_subPanelType}");
                    ExitSubPanel();
                }
            }
        }

        private void CheckForMonoBehaviourPanel()
        {
            if (_controller == null) return;

            // Check if the game's profileScreenMode matches the expected cosmetic type
            string currentMode = ReadProfileScreenMode();
            string expectedMode = _subPanelType == "Avatar" ? "AvatarSelect" : "EmoteSelect";

            if (currentMode != expectedMode) return;

            // Mode matches - find the panel content from CosmeticSelectorController's transform
            var panelGo = FindCosmeticSelectorContent();
            if (panelGo != null)
            {
                MelonLogger.Msg($"[{NavigatorId}] {_subPanelType} panel detected via mode={currentMode}");
                EnterSubPanel(panelGo);
            }
        }

        private string ReadProfileScreenMode()
        {
            if (_profileScreenModeField == null || _controller == null) return null;
            try
            {
                var mode = _profileScreenModeField.GetValue(_controller);
                return mode?.ToString();
            }
            catch { return null; }
        }

        private GameObject FindCosmeticSelectorContent()
        {
            // CosmeticSelectorController places panels under _cosmeticSelectorsTransform
            if (_cosmeticSelectorsTransformField != null && _controller != null)
            {
                try
                {
                    var transform = _cosmeticSelectorsTransformField.GetValue(_controller) as Transform;
                    if (transform != null && transform.gameObject.activeInHierarchy)
                        return transform.gameObject;
                }
                catch { }
            }

            // Fallback: use the controller's gameObject (items will be found scene-wide)
            return _controller?.gameObject;
        }

        /// <summary>
        /// Checks if a MonoBehaviour sub-panel is still active by verifying the profile screen mode.
        /// </summary>
        private bool IsMonoBehaviourPanelActive(string type)
        {
            string currentMode = ReadProfileScreenMode();
            string expectedMode = type == "Avatar" ? "AvatarSelect" : "EmoteSelect";
            return currentMode == expectedMode;
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
                        MelonLogger.Warning($"[{NavigatorId}] OnHandheldBackButton failed: {ex.Message}");
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
                MelonLogger.Msg($"[{NavigatorId}] {label} type: {type.FullName}");
                foreach (var prop in type.GetProperties(PublicInstance))
                {
                    MelonLogger.Msg($"[{NavigatorId}]   Prop: {prop.Name} ({prop.PropertyType.Name})");
                }
                foreach (var field in type.GetFields(AllInstanceFlags))
                {
                    MelonLogger.Msg($"[{NavigatorId}]   Field: {field.Name} ({field.FieldType.Name})");
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
