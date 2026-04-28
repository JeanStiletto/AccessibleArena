using System;
using System.Reflection;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Resolves human-readable labels for cosmetic-selector items (avatar busts,
    /// pet list rows, sleeve cards). The selector popups (PetPopUpV2,
    /// CardBackSelectorPopup, AvatarSelectPanel) instantiate these items with no
    /// visible text — only sprites — so generic popup-mode discovery falls back
    /// to "button" / "avatar bust select" / "card back selector" and dedup
    /// collapses them to one entry. We walk up from the discovered button GO,
    /// find the item-level MonoBehaviour, and pull its localized name + status.
    /// </summary>
    public static class CosmeticItemResolver
    {
        private const int MaxAncestorWalk = 6;

        /// <summary>
        /// Attempt to resolve a label for a cosmetic item that owns (or is the
        /// parent of) the given button GameObject. Returns false if the GO is
        /// not a recognised cosmetic item — caller falls back to the default label.
        /// Output label includes status when relevant ("Selected", "Default",
        /// "Locked", "None") so the screen reader announces it in one breath.
        /// </summary>
        public static bool TryResolve(GameObject buttonObj, out string label)
        {
            label = null;
            if (buttonObj == null) return false;

            // AvatarSelection items: avatar bust prefab, MonoBehaviour on the same GO
            // (or an ancestor) as the discovered CustomButton.
            var avatarSel = FindAncestorComponent(buttonObj, "AvatarSelection");
            if (avatarSel != null)
            {
                label = ResolveAvatarLabel(avatarSel);
                return label != null;
            }

            // SelectPetsListItemView: the CustomButton is a child of a GO carrying
            // the list-item MonoBehaviour.
            var petItem = FindAncestorComponent(buttonObj, "SelectPetsListItemView");
            if (petItem != null)
            {
                label = ResolvePetLabel(petItem);
                return label != null;
            }

            // CardBackSelector: same shape as pet items.
            var sleeveItem = FindAncestorComponent(buttonObj, "CardBackSelector");
            if (sleeveItem != null)
            {
                label = ResolveSleeveLabel(sleeveItem);
                return label != null;
            }

            return false;
        }

        #region Avatar

        private static string ResolveAvatarLabel(MonoBehaviour avatarSelection)
        {
            var type = avatarSelection.GetType();
            string name = ReadStringProp(type, avatarSelection, "NameString");

            if (string.IsNullOrEmpty(name))
            {
                string id = ReadStringProp(type, avatarSelection, "Id");
                if (!string.IsNullOrEmpty(id))
                    name = UITextExtractor.ResolveLocKey($"MainNav/Profile/Avatars/{id}_Name");
                if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                    name = id;
            }

            if (string.IsNullOrEmpty(name)) return null;

            string status = null;
            try
            {
                var defaultField = type.GetField("_default", PrivateInstance);
                if (defaultField != null && (bool)defaultField.GetValue(avatarSelection))
                    status = Strings.ProfileItemSelected;
            }
            catch { }

            if (status == null)
            {
                try
                {
                    var lockedField = type.GetField("_locked", PrivateInstance);
                    if (lockedField != null && (bool)lockedField.GetValue(avatarSelection))
                        status = Strings.ProfileItemLocked;
                }
                catch { }
            }

            return string.IsNullOrEmpty(status) ? name : $"{name}, {status}";
        }

        #endregion

        #region Pet

        private static string ResolvePetLabel(MonoBehaviour petItem)
        {
            var type = petItem.GetType();
            string petId = ReadStringProp(type, petItem, "PetId");

            string name = null;
            if (!string.IsNullOrEmpty(petId))
            {
                // PetId is "Name.Variant" — the localization uses the prefix.
                string baseId = petId.Contains(".") ? petId.Substring(0, petId.IndexOf('.')) : petId;
                string[] patterns =
                {
                    $"MainNav/Cosmetics/Pet/{petId}_Details",
                    $"MainNav/Cosmetics/Pet/{petId}_Name",
                    $"MainNav/Cosmetics/Pet/{petId}",
                    $"MainNav/Cosmetics/Pet/{baseId}_Details",
                    $"MainNav/Cosmetics/Pet/{baseId}_Name",
                    $"MainNav/Cosmetics/Pet/{baseId}",
                    $"MainNav/Cosmetics/Pets/{baseId}",
                };
                foreach (var pattern in patterns)
                {
                    string loc = UITextExtractor.ResolveLocKey(pattern);
                    if (!string.IsNullOrEmpty(loc))
                    {
                        name = loc;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(name))
                    name = HumanizeId(baseId);
            }

            if (string.IsNullOrEmpty(name)) return null;

            string status = ReadCosmeticStatus(type, petItem);
            return string.IsNullOrEmpty(status) ? name : $"{name}, {status}";
        }

        #endregion

        #region Sleeve

        private static string ResolveSleeveLabel(MonoBehaviour sleeveItem)
        {
            var type = sleeveItem.GetType();
            string cardBack = ReadStringProp(type, sleeveItem, "CardBack");

            string name = null;
            if (!string.IsNullOrEmpty(cardBack))
            {
                // Sleeves are derived from card art; the loc key is the card name itself.
                // Fall back to humanizing the ID if no localization is available.
                string[] patterns =
                {
                    $"MainNav/Cosmetics/Sleeve/{cardBack}_Name",
                    $"MainNav/Cosmetics/Sleeve/{cardBack}",
                    $"MainNav/Cosmetics/CardBack/{cardBack}",
                };
                foreach (var pattern in patterns)
                {
                    string loc = UITextExtractor.ResolveLocKey(pattern);
                    if (!string.IsNullOrEmpty(loc))
                    {
                        name = loc;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(name))
                    name = HumanizeId(cardBack);
            }

            if (string.IsNullOrEmpty(name)) return null;

            // CardBackSelector exposes Collected (owned) but no _selected/_default field
            // — the selected state is animator-driven and not easily readable. Owners
            // and locked items are still distinguished, which is the most useful split.
            string status = null;
            try
            {
                var collectedProp = type.GetProperty("Collected", PublicInstance);
                if (collectedProp != null)
                {
                    bool collected = (bool)collectedProp.GetValue(sleeveItem);
                    status = collected ? Strings.ProfileItemOwned : Strings.ProfileItemLocked;
                }
            }
            catch { }

            return string.IsNullOrEmpty(status) ? name : $"{name}, {status}";
        }

        #endregion

        #region Helpers

        private static MonoBehaviour FindAncestorComponent(GameObject start, string typeName)
        {
            Transform t = start.transform;
            int safety = MaxAncestorWalk;
            while (t != null && safety-- > 0)
            {
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == typeName)
                        return mb;
                }
                t = t.parent;
            }
            return null;
        }

        private static string ReadStringProp(Type type, object instance, string propName)
        {
            try
            {
                var prop = type.GetProperty(propName, PublicInstance);
                if (prop == null) return null;
                var val = prop.GetValue(instance);
                return val?.ToString();
            }
            catch { return null; }
        }

        private static string ReadCosmeticStatus(Type type, MonoBehaviour item)
        {
            // Order: selected > default > owned/locked
            try
            {
                var selectedField = type.GetField("_isSelected", PrivateInstance);
                if (selectedField != null && (bool)selectedField.GetValue(item))
                    return Strings.ProfileItemSelected;
            }
            catch { }

            try
            {
                var defaultField = type.GetField("_isDefault", PrivateInstance);
                if (defaultField != null && (bool)defaultField.GetValue(item))
                    return Strings.ProfileItemDefault;
            }
            catch { }

            try
            {
                var ownedProp = type.GetProperty("IsOwned", PublicInstance);
                if (ownedProp != null)
                {
                    bool owned = (bool)ownedProp.GetValue(item);
                    return owned ? Strings.ProfileItemOwned : Strings.ProfileItemLocked;
                }
            }
            catch { }

            return null;
        }

        private static string HumanizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            // "TMT_Mastery_Companion" -> "TMT Mastery Companion"
            return id.Replace('_', ' ');
        }

        #endregion
    }
}
