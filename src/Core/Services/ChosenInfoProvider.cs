using UnityEngine;
using AccessibleArena.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Extracts the "chosen" data carried on a card model — the creature type/color/etc. picked
    /// for Cavern of Souls and similar, and the card names recorded by Pithing Needle / Meddling
    /// Mage / Cabal Therapy. The game stores both on MtgCardInstance:
    ///   <c>LinkedInfoText</c>          — list of (EnumName, Value) entries (SubType/Color/CardType/SuperType)
    ///   <c>LinkedInfoTitleLocIds</c>   — set of card-title loc IDs (named cards)
    /// MTGA's own rules-text pipeline reads these via Wotc.Mtga.Cards.Text.LinkedInfoTextParser
    /// and LinkedInfoTitleTextParser; we mirror that flow so screen-reader users hear what
    /// sighted players see baked into the card's rules text on the battlefield.
    /// </summary>
    public static class ChosenInfoProvider
    {
        private static FieldInfo _linkedInfoTextField;
        private static bool _linkedInfoTextSearched;
        private static FieldInfo _linkedInfoTitleLocIdsField;
        private static bool _linkedInfoTitleLocIdsSearched;

        // LinkedInfoText struct shape: (TypeCategory Category, string EnumName, int Value, LinkInfoData LinkInfo, bool Highlighted)
        private static FieldInfo _entryEnumNameField;
        private static FieldInfo _entryValueField;
        private static Type _cachedEntryType;

        public static void ClearCache()
        {
            _linkedInfoTextField = null;
            _linkedInfoTextSearched = false;
            _linkedInfoTitleLocIdsField = null;
            _linkedInfoTitleLocIdsSearched = false;
            _entryEnumNameField = null;
            _entryValueField = null;
            _cachedEntryType = null;
        }

        /// <summary>
        /// Pulls the chosen-info display strings off the model. Returns a 2-tuple:
        ///   <c>chosenTypes</c>  — comma-joined display strings for LinkedInfoText entries
        ///                          (e.g. "Wizard" for Cavern of Souls, "Blue" for Iona).
        ///   <c>namedCards</c>   — comma-joined card names from LinkedInfoTitleLocIds
        ///                          (e.g. "Cabal Therapy" for Pithing Needle).
        /// Either side may be null/empty if the card has no chosen info.
        /// </summary>
        public static (string chosenTypes, string namedCards) ExtractChosenInfo(object model)
        {
            if (model == null) return (null, null);
            var instance = CardModelProvider.GetModelInstance(model);
            if (instance == null) return (null, null);

            string types = ExtractLinkedInfoText(instance);
            string names = ExtractLinkedInfoTitles(instance);
            return (types, names);
        }

        private static string ExtractLinkedInfoText(object instance)
        {
            try
            {
                if (!_linkedInfoTextSearched)
                {
                    _linkedInfoTextSearched = true;
                    _linkedInfoTextField = instance.GetType().GetField("LinkedInfoText", AllInstanceFlags);
                }
                if (_linkedInfoTextField == null) return null;

                var list = _linkedInfoTextField.GetValue(instance) as IEnumerable;
                if (list == null) return null;

                List<string> parts = null;
                foreach (var entry in list)
                {
                    if (entry == null) continue;
                    if (!TryReadEntry(entry, out string enumName, out int value)) continue;
                    if (string.IsNullOrEmpty(enumName)) continue;

                    string display = CardTextProvider.GetLocalizedTextForEnumValue(enumName, value);
                    if (string.IsNullOrEmpty(display)) continue;

                    if (parts == null) parts = new List<string>();
                    parts.Add(display);
                }
                return parts == null ? null : string.Join(", ", parts);
            }
            catch (Exception ex)
            {
                Log.Warn("ChosenInfoProvider", $"LinkedInfoText read failed: {ex.Message}");
                return null;
            }
        }

        private static string ExtractLinkedInfoTitles(object instance)
        {
            try
            {
                if (!_linkedInfoTitleLocIdsSearched)
                {
                    _linkedInfoTitleLocIdsSearched = true;
                    _linkedInfoTitleLocIdsField = instance.GetType().GetField("LinkedInfoTitleLocIds", AllInstanceFlags);
                }
                if (_linkedInfoTitleLocIdsField == null) return null;

                var set = _linkedInfoTitleLocIdsField.GetValue(instance) as IEnumerable;
                if (set == null) return null;

                List<string> parts = null;
                foreach (var item in set)
                {
                    uint locId;
                    if (item is uint u) locId = u;
                    else if (item is int i && i > 0) locId = (uint)i;
                    else continue;
                    if (locId == 0) continue;

                    string name = CardTextProvider.GetLocalizedTextById(locId);
                    if (string.IsNullOrEmpty(name)) continue;

                    if (parts == null) parts = new List<string>();
                    parts.Add(name);
                }
                return parts == null ? null : string.Join(", ", parts);
            }
            catch (Exception ex)
            {
                Log.Warn("ChosenInfoProvider", $"LinkedInfoTitleLocIds read failed: {ex.Message}");
                return null;
            }
        }

        private static bool TryReadEntry(object entry, out string enumName, out int value)
        {
            enumName = null;
            value = 0;
            var entryType = entry.GetType();
            if (entryType != _cachedEntryType)
            {
                _cachedEntryType = entryType;
                _entryEnumNameField = entryType.GetField("EnumName", AllInstanceFlags);
                _entryValueField = entryType.GetField("Value", AllInstanceFlags);
            }
            if (_entryEnumNameField == null || _entryValueField == null) return false;
            enumName = _entryEnumNameField.GetValue(entry) as string;
            var raw = _entryValueField.GetValue(entry);
            if (raw is int iVal) value = iVal;
            else if (raw is uint uVal) value = (int)uVal;
            else return false;
            return true;
        }
    }
}
