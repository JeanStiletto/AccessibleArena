using System;
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
    /// Takes over input when the deck builder's <c>DeckDetailsPopup</c> is open.
    /// Lets blind users edit the deck name, switch format, and pick deck-level
    /// cosmetics (avatar, sleeve, pet, emotes, title) by activating the same
    /// tiles a sighted player clicks. Modeled on <see cref="AdvancedFiltersNavigator"/>.
    ///
    /// Activates a tile by invoking <c>DisplayItemCosmeticBase.OpenSelector()</c>;
    /// the popup expands an in-place selector grid which we then re-discover and
    /// navigate through. Tile callbacks are already wired by <c>DeckDetailsPopup.Init</c>
    /// to <c>DeckBuilderModelProvider.SetSelected*</c>, so we just click and let the
    /// game's normal flow apply the choice.
    /// </summary>
    public class DeckDetailsNavigator : BaseNavigator
    {
        public override string NavigatorId => "DeckDetails";
        public override string ScreenName => Strings.ScreenDeckDetails;
        public override int Priority => 87; // Same band as AdvancedFiltersNavigator
        protected override bool SupportsCardNavigation => false;

        // PanelStateManager exposes Harmony-detected popups by type name (e.g., "DeckDetailsPopup"),
        // not by raw GameObject name. The actual prefab is "Home_DeckDetails_Desktop_16x9(Clone)".
        private const string PanelTypeName = "DeckDetailsPopup";

        // Sub-popups the deck-details popup spawns when the user activates a cosmetic tile.
        // When one of these is on top, we deactivate so GeneralMenu's generic popup mode can
        // navigate the sub-popup (avatar/pet/sleeve/emote/title pickers) instead.
        private static readonly string[] SubPopupNamePrefixes = new[]
        {
            "AvatarSelectPanel",          // ProfileUI.AvatarSelectPanel (panel name only on profile path; included for safety)
            "PetPopUpV2",                  // pet picker
            "EmoteSelectionScreen",        // emote picker
            "EmoteSelectionScreenView",
            "TitleSelection",              // title picker
            "CardBackSelectorPopup",       // sleeve picker
            "DeckSleeveSelector",
        };

        private static readonly string[] SubPopupNameContains = new[]
        {
            "Select_Pets",
            "Select_Avatars",
            "Select_Emotes",
            "Select_Titles",
            "Select_CardBacks",
            "Select_Sleeves",
        };

        private GameObject _popup;
        private bool _lastPopupState;

        public DeckDetailsNavigator(IAnnouncementService announcer) : base(announcer) { }

        #region Detection

        protected override bool DetectScreen()
        {
            // ReflectionPanelDetector reports this popup with Name = "DeckDetailsPopup:Home_DeckDetails_Desktop_16x9(Clone)"
            // (joined type and GameObject name). Walk the stack and match by StartsWith on the type prefix.
            var psm = PanelStateManager.Instance;
            GameObject foundGo = null;
            bool subPopupOpen = false;
            var stack = psm?.GetPanelStack();
            if (stack != null)
            {
                foreach (var p in stack)
                {
                    if (p == null || !p.IsValid) continue;
                    string panelName = p.Name ?? "";
                    if (panelName == PanelTypeName
                        || panelName.StartsWith(PanelTypeName + ":", StringComparison.Ordinal))
                    {
                        foundGo = p.GameObject;
                    }
                    else if (IsCosmeticSubPopup(panelName))
                    {
                        subPopupOpen = true;
                    }
                }
            }

            // Step aside when a cosmetic sub-popup (pet/avatar/etc.) is on top so GeneralMenu's
            // generic popup-mode discovery can handle it. We re-activate when the sub-popup closes.
            if (subPopupOpen)
            {
                if (_lastPopupState)
                {
                    _lastPopupState = false;
                    Log.Msg(NavigatorId, "Stepping aside: cosmetic sub-popup is on top");
                }
                _popup = null;
                return false;
            }

            bool active = foundGo != null;
            _popup = active ? foundGo : null;

            if (active != _lastPopupState)
            {
                _lastPopupState = active;
                Log.Msg(NavigatorId, $"DeckDetailsPopup open: {active} (go={_popup?.name})");
            }

            return active;
        }

        private static bool IsCosmeticSubPopup(string panelName)
        {
            if (string.IsNullOrEmpty(panelName)) return false;
            foreach (var prefix in SubPopupNamePrefixes)
            {
                if (panelName == prefix
                    || panelName.StartsWith(prefix + ":", StringComparison.Ordinal))
                    return true;
            }
            foreach (var sub in SubPopupNameContains)
            {
                if (panelName.IndexOf(sub, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        #endregion

        #region Discovery

        protected override void DiscoverElements()
        {
            _elements.Clear();

            if (_popup == null)
            {
                Log.Msg(NavigatorId, "No popup root during discovery");
                return;
            }

            // 1) Deck name input field
            var nameInput = FindFirstByName<TMP_InputField>(_popup, "_deckNameInput");
            if (nameInput == null) nameInput = _popup.GetComponentInChildren<TMP_InputField>(true);
            if (nameInput != null && nameInput.gameObject.activeInHierarchy)
                AddElement(nameInput.gameObject, $"Deck name: {nameInput.text}");

            // 2) Format dropdown (TMP_Dropdown or cTMP_Dropdown)
            var dropdownGo = FindFormatDropdown(_popup);
            if (dropdownGo != null)
            {
                string current = GetDropdownDisplayValue(dropdownGo) ?? "";
                AddElement(dropdownGo, $"Format: {current}, dropdown",
                    new CarouselInfo(), null, null, UIElementClassifier.ElementRole.Dropdown);
            }

            // 3) Cosmetic display tiles (avatar/sleeve/pet/emote/title)
            DiscoverCosmeticTiles(_popup);

            // 4) Close button (we always pick the first)
            var closeButton = FindCloseButton(_popup);
            if (closeButton != null)
                AddElement(closeButton, "Close, button", default, null, null, UIElementClassifier.ElementRole.Button);

            Log.Msg(NavigatorId, $"Discovered {_elements.Count} elements");
        }

        private void DiscoverCosmeticTiles(GameObject root)
        {
            // Find the CosmeticSelectorController, then pull display items from its private dictionary.
            MonoBehaviour controller = null;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name == T.CosmeticSelectorController)
                {
                    controller = mb;
                    break;
                }
            }
            if (controller == null) return;

            var displayItemsField = controller.GetType().GetField("_displayItems", PrivateInstance);
            var dict = displayItemsField?.GetValue(controller) as System.Collections.IDictionary;
            if (dict == null)
            {
                Log.Msg(NavigatorId, "_displayItems dictionary not accessible");
                return;
            }

            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                var displayItem = entry.Value as MonoBehaviour;
                if (displayItem == null || !displayItem.gameObject.activeInHierarchy) continue;

                string typeKey = entry.Key?.ToString() ?? "";
                string label = MapTypeToLabel(typeKey, displayItem.GetType().Name);
                string current = ResolveCurrentValueForType(typeKey);

                AddElement(displayItem.gameObject, Strings.CosmeticsTile(label, current),
                    default, null, null, UIElementClassifier.ElementRole.Button);
            }
        }

        private static string MapTypeToLabel(string displayCosmeticsType, string componentTypeName)
        {
            // DisplayCosmeticsTypes enum values: Avatar / Sleeve / Pet / Emote / Title
            string key = !string.IsNullOrEmpty(displayCosmeticsType) ? displayCosmeticsType : componentTypeName;
            if (key.Contains("Avatar")) return Strings.CosmeticsAvatar;
            if (key.Contains("Sleeve")) return Strings.CosmeticsSleeve;
            if (key.Contains("Pet")) return Strings.CosmeticsPet;
            if (key.Contains("Emote")) return Strings.CosmeticsEmote;
            if (key.Contains("Title")) return Strings.CosmeticsTitle;
            return key;
        }

        private static string ResolveCurrentValueForType(string displayCosmeticsType)
        {
            string key = displayCosmeticsType ?? "";
            if (key.Contains("Avatar")) return DeckCosmeticsReader.GetCurrentAvatarName();
            if (key.Contains("Sleeve")) return DeckCosmeticsReader.GetCurrentSleeveName();
            if (key.Contains("Pet")) return DeckCosmeticsReader.GetCurrentPetName();
            if (key.Contains("Emote")) return DeckCosmeticsReader.GetCurrentEmoteSummary();
            return Strings.ProfileItemDefault;
        }

        private static GameObject FindFormatDropdown(GameObject root)
        {
            // Inspect both TMP_Dropdown and cTMP_Dropdown (game's custom subclass).
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "cTMP_Dropdown" || mb is TMP_Dropdown)
                    return mb.gameObject;
            }
            return null;
        }

        private static GameObject FindCloseButton(GameObject root)
        {
            // Pull from the popup's CloseButtons[] array via reflection if possible
            foreach (var mb in root.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "DeckDetailsPopup") continue;
                var arrField = mb.GetType().GetField("CloseButtons", AllInstanceFlags);
                var arr = arrField?.GetValue(mb) as Array;
                if (arr != null && arr.Length > 0)
                {
                    var first = arr.GetValue(0) as MonoBehaviour;
                    if (first != null && first.gameObject.activeInHierarchy)
                        return first.gameObject;
                }
                break;
            }

            // Fallback: any CustomButton named like Close/Cancel/Back
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName != T.CustomButton && typeName != T.CustomButtonWithTooltip) continue;
                string n = mb.gameObject.name.ToLowerInvariant();
                if (n.Contains("close") || n.Contains("cancel") || n.Contains("back"))
                    return mb.gameObject;
            }
            return null;
        }

        private static TComp FindFirstByName<TComp>(GameObject root, string name) where TComp : Component
        {
            foreach (var c in root.GetComponentsInChildren<TComp>(true))
            {
                if (c != null && c.gameObject.name == name) return c;
            }
            return null;
        }

        #endregion

        #region Activation

        protected override bool OnElementActivated(int index, GameObject element)
        {
            // Default activation: UIActivator.Activate clicks the focused element. For cosmetic
            // tiles that fires the CustomButton's OnClick → OpenSelector → game opens a sub-popup
            // (Select_Pets, Select_Avatars, etc.). ValidateElements then detects the sub-popup
            // and steps aside so GeneralMenu's generic popup mode navigates it. We re-activate
            // when the user closes the sub-popup. Returning false everywhere = use base behavior.
            return false;
        }

        #endregion

        #region Input

        protected override bool HandleCustomInput()
        {
            // Backspace closes the deck-details popup via its close button.
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ClosePopup();
                return true;
            }
            return false;
        }

        private void ClosePopup()
        {
            var closeButton = FindCloseButton(_popup);
            if (closeButton != null)
            {
                UIActivator.Activate(closeButton);
                _announcer.AnnounceInterrupt(Strings.DeckDetailsClosed);
                return;
            }

            // Last resort: deactivate the popup directly
            if (_popup != null)
                _popup.SetActive(false);
            _announcer.AnnounceInterrupt(Strings.DeckDetailsClosed);
        }

        #endregion

        #region Validation / Lifecycle

        protected override bool ValidateElements()
        {
            if (_popup == null || !_popup.activeInHierarchy) return false;

            // When a cosmetic sub-popup (avatar/pet/sleeve/emote) opens on top, step aside
            // so GeneralMenu's generic popup mode can navigate the sub-popup. We re-activate
            // when it closes (DetectScreen finds DeckDetails alone again).
            var stack = PanelStateManager.Instance?.GetPanelStack();
            if (stack != null)
            {
                foreach (var p in stack)
                {
                    if (p == null || !p.IsValid) continue;
                    if (IsCosmeticSubPopup(p.Name ?? ""))
                    {
                        Log.Msg(NavigatorId, $"Deactivating: cosmetic sub-popup '{p.Name}' opened");
                        return false;
                    }
                }
            }
            return true;
        }

        public override void OnSceneChanged(string sceneName)
        {
            if (_isActive) Deactivate();
            _popup = null;
        }

        protected override string GetActivationAnnouncement()
        {
            string core = Strings.ScreenDeckDetails;
            if (_elements.Count > 0)
                core += $". {_elements.Count} items";
            return core;
        }

        #endregion
    }
}
