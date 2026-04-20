using MelonLoader;
using AccessibleArena.Core.Utils;
using AccessibleArena.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    public partial class DuelAnnouncer
    {
        // Suppress duplicate NPE BlockingReminder in same blockers phase
        private bool _shownBlockingReminderThisStep;

        // Suppress the generic ActionReminder that fires right after a tooltip with custom text
        private bool _suppressNextActionReminder;

        // Reflection cache for NPE types
        private static FieldInfo _npeDialogLineField;
        private static FieldInfo _npeReminderField;
        private static FieldInfo _npeReminderTextField;
        private static FieldInfo _npeReminderSuggestedField;
        private static FieldInfo _npeTooltipTypeField;
        private static FieldInfo _npeWarningTextField;
        private static FieldInfo _localizedStringKeyField;
        private static bool _npeReflectionInitialized;

        /// <summary>
        /// Returns true if the current duel is an NPE tutorial game.
        /// Caches the result for the duration of the duel.
        /// </summary>
        public bool IsNPETutorial => _isNPETutorial;
        private bool _isNPETutorial;
        private bool _npeCheckDone;

        private static PropertyInfo _npeDirectorProp;

        // Hover simulation for NPE - fire CardHoverController.OnHoveredCardUpdated when user navigates to a CDC
        private GameObject _lastHoveredNPECard;
        private bool _lastHoverWasStack;

        private static FieldInfo _hoverEventField;
        private static bool _hoverFieldSearched;

        private static void EnsureNPEReflection()
        {
            if (_npeReflectionInitialized) return;
            _npeReflectionInitialized = true;

            try
            {
                // NPEDialogUXEvent.Line (public readonly field, MTGALocalizedString)
                var dialogType = FindType("Wotc.Mtga.DuelScene.UXEvents.NPEDialogUXEvent");
                if (dialogType != null)
                    _npeDialogLineField = dialogType.GetField("Line", PublicInstance);

                // NPEReminderUXEvent._reminder (private field, NPEReminder)
                var reminderEventType = FindType("Wotc.Mtga.DuelScene.UXEvents.NPEReminderUXEvent");
                if (reminderEventType != null)
                    _npeReminderField = reminderEventType.GetField("_reminder", PrivateInstance);

                // NPEReminder.Text (public field, MTGALocalizedString)
                // NPEReminder.SparkySuggestedInstances (public field, List<uint>)
                var reminderType = FindType("NPEReminder");
                if (reminderType != null)
                {
                    _npeReminderTextField = reminderType.GetField("Text", PublicInstance);
                    _npeReminderSuggestedField = reminderType.GetField("SparkySuggestedInstances", PublicInstance);
                }

                // MTGALocalizedString.Key (public field, string) - for NPE key extraction
                // MTGALocalizedString is in the root namespace, not Wotc.Mtga.Loc
                var localizedStringType = FindType("MTGALocalizedString");
                if (localizedStringType != null)
                    _localizedStringKeyField = localizedStringType.GetField("Key", PublicInstance);

                // NPEWarningUXEvent._displayText (protected field, MTGALocalizedString)
                var warningType = FindType("Wotc.Mtga.DuelScene.UXEvents.NPEWarningUXEvent");
                if (warningType != null)
                    _npeWarningTextField = warningType.GetField("_displayText", PrivateInstance);

                // NPEDismissableDeluxeTooltipUXEvent._type (private field, DeluxeTooltipType enum)
                var tooltipType = FindType("Wotc.Mtga.DuelScene.UXEvents.NPEDismissableDeluxeTooltipUXEvent");
                if (tooltipType != null)
                    _npeTooltipTypeField = tooltipType.GetField("_type", PrivateInstance);
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Failed to initialize NPE reflection: {ex.Message}");
            }
        }

        private string HandleNPEDialog(object uxEvent)
        {
            try
            {
                EnsureNPEReflection();

                if (_npeDialogLineField == null) return null;

                var lineObj = _npeDialogLineField.GetValue(uxEvent);
                if (lineObj == null) return null;

                string text = lineObj.ToString();
                if (string.IsNullOrEmpty(text)) return null;

                // Extract localization key for language-agnostic hint matching
                string locKey = null;
                if (_localizedStringKeyField != null)
                    locKey = _localizedStringKeyField.GetValue(lineObj) as string;

                Log.Announce("DuelAnnouncer", $"NPE Dialog: {text} (key: {locKey})");

                // Check if we have a supplementary keyboard hint for this dialog line
                string hint = NPETutorialTextProvider.GetDialogHint(locKey);
                if (hint != null) return hint;

                // AlwaysReminder interceptions are error messages (wrong target, can't afford spell, etc.)
                // These must be read aloud so blind players understand why their action was rejected
                if (NPETutorialTextProvider.ShouldReadAloud(locKey))
                {
                    Log.Announce("DuelAnnouncer", $"Reading AlwaysReminder aloud: {text}");
                    return text;
                }

                // Other dialog lines are voice-acted NPC subtitles - don't read aloud
                return null;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error handling NPE dialog: {ex.Message}");
                return null;
            }
        }

        private string HandleNPEReminder(object uxEvent)
        {
            try
            {
                EnsureNPEReflection();

                if (_npeReminderField == null) return null;

                var reminderObj = _npeReminderField.GetValue(uxEvent);
                if (reminderObj == null) return null;

                // Get reminder text and localization key
                string text = null;
                string locKey = null;
                if (_npeReminderTextField != null)
                {
                    var textObj = _npeReminderTextField.GetValue(reminderObj);
                    if (textObj != null)
                    {
                        text = textObj.ToString();
                        if (_localizedStringKeyField != null)
                            locKey = _localizedStringKeyField.GetValue(textObj) as string;
                    }
                }

                if (string.IsNullOrEmpty(text)) return null;

                // Try to resolve suggested card names
                string cardHint = null;
                if (_npeReminderSuggestedField != null)
                {
                    var suggestedObj = _npeReminderSuggestedField.GetValue(reminderObj);
                    if (suggestedObj is IList suggestedList && suggestedList.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var item in suggestedList)
                        {
                            if (item is uint instanceId)
                            {
                                string name = GetCardNameByInstanceId(instanceId);
                                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                                    names.Add(name);
                            }
                        }
                        if (names.Count > 0)
                            cardHint = string.Join(", ", names);
                    }
                }

                Log.Announce("DuelAnnouncer", $"NPE Reminder: {text} (key: {locKey})" + (cardHint != null ? $" (cards: {cardHint})" : ""));

                // Suppress duplicate BlockingReminder in same blockers phase
                if (locKey != null && locKey.Contains("/BlockingReminder"))
                {
                    if (_shownBlockingReminderThisStep)
                    {
                        Log.Announce("DuelAnnouncer", "Suppressing duplicate BlockingReminder");
                        return null;
                    }
                    _shownBlockingReminderThisStep = true;
                }

                // Suppress the generic ActionReminder that fires right after a tooltip with custom text
                if (_suppressNextActionReminder && locKey != null && locKey.Contains("/ActionReminder"))
                {
                    _suppressNextActionReminder = false;
                    Log.Announce("DuelAnnouncer", "Suppressing ActionReminder after tooltip hint");
                    return null;
                }
                _suppressNextActionReminder = false;

                // Replace mouse/drag instructions with keyboard-focused text
                string replacement = NPETutorialTextProvider.GetReplacementText(locKey);
                string announcement = replacement ?? text;

                if (!string.IsNullOrEmpty(cardHint))
                    return $"{announcement} {Strings.NPE_SuggestedCard(cardHint)}";
                return announcement;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error handling NPE reminder: {ex.Message}");
                return null;
            }
        }

        private string HandleNPETooltip(object uxEvent)
        {
            try
            {
                EnsureNPEReflection();

                if (_npeTooltipTypeField == null) return null;

                var typeObj = _npeTooltipTypeField.GetValue(uxEvent);
                if (typeObj == null) return null;

                string tooltipType = typeObj.ToString();
                Log.Announce("DuelAnnouncer", $"NPE Tooltip: {tooltipType}");

                // DominariaFall fires at the very start of the first tutorial duel,
                // overlapping with NPC intro dialogs and matchup info — suppress it.
                // Combat fires right after "Du wurdest geblockt!" dialog which has its own hint.
                // Mana fires after playing first land — mana hint already delivered via Sparky_05 dialog.
                if (tooltipType == "DominariaFall" || tooltipType == "Combat" || tooltipType == "Mana") return null;

                // Check for custom tooltip text (visual-only popups replaced with keyboard hints)
                string custom = NPETutorialTextProvider.GetTooltipText(tooltipType);
                if (custom != null)
                {
                    // Suppress the generic ActionReminder that immediately follows this tooltip
                    _suppressNextActionReminder = true;
                    return custom;
                }

                return Strings.NPE_Tooltip(tooltipType);
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error handling NPE tooltip: {ex.Message}");
                return null;
            }
        }

        private string HandleNPEWarning(object uxEvent)
        {
            try
            {
                EnsureNPEReflection();

                if (_npeWarningTextField == null) return null;

                var textObj = _npeWarningTextField.GetValue(uxEvent);
                if (textObj == null) return null;

                string text = textObj.ToString();
                if (string.IsNullOrEmpty(text)) return null;

                Log.Announce("DuelAnnouncer", $"NPE Warning: {text}");
                return text;
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error handling NPE warning: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check once per duel activation whether this is an NPE tutorial.
        /// Call from Activate() after the duel is set up.
        /// </summary>
        public void CheckNPETutorial()
        {
            if (_npeCheckDone) return;
            _npeCheckDone = true;

            try
            {
                if (_npeDirectorProp == null)
                {
                    var gmType = FindType("GameManager");
                    if (gmType != null)
                        _npeDirectorProp = gmType.GetProperty("NpeDirector", PublicInstance);
                }

                if (_npeDirectorProp == null) return;

                // GameManager is a singleton - find it
                var gmObj = UnityEngine.Object.FindObjectOfType(FindType("GameManager")) as Component;
                if (gmObj == null) return;

                var director = _npeDirectorProp.GetValue(gmObj);
                _isNPETutorial = director != null;

                if (_isNPETutorial)
                    Log.Msg("DuelAnnouncer", "NPE tutorial detected");
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error checking NPE state: {ex.Message}");
            }
        }

        /// <summary>
        /// During NPE tutorials, simulate a mouse hover on the currently focused CDC.
        /// The tutorial expects CardHoverController.OnHoveredCardUpdated to fire when
        /// the player looks at a card - without this, Sparky loops pointing at cards forever.
        /// Call from DuelNavigator.Update() each frame.
        ///
        /// CRITICAL: NPEController pauses the NPE director when the player hovers a Stack card,
        /// and only resumes when OnHoveredCardUpdated fires with null. With a mouse, moving
        /// away from a card fires null automatically. We must replicate this: when focus leaves
        /// a Stack card, fire null first so the NPE director resumes.
        /// </summary>
        public void UpdateNPEHoverSimulation()
        {
            if (!_isNPETutorial || !_isActive) return;

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return;

            var focused = eventSystem.currentSelectedGameObject;

            // Only fire when focus actually changes to a different card
            if (focused == _lastHoveredNPECard) return;
            _lastHoveredNPECard = focused;

            // When focus leaves a Stack card (to null or another card), fire null first
            // to unpause the NPE director. NPEController.OnHoveredCardUpdated pauses on
            // Stack hover and only resumes on null - without this the NPE freezes permanently.
            if (_lastHoverWasStack)
            {
                _lastHoverWasStack = false;
                FireHoveredCardUpdated(null);
            }

            if (focused == null) return;

            // Check if focused object is a DuelScene_CDC
            var cdcComponent = GetDuelSceneCDCOrParent(focused);
            if (cdcComponent == null) return;

            // Track whether this CDC is on the Stack
            _lastHoverWasStack = IsInStackHolder(focused);

            // Fire CardHoverController.OnHoveredCardUpdated(cdc)
            FireHoveredCardUpdated(cdcComponent);
        }

        private static bool IsInStackHolder(GameObject go)
        {
            var current = go.transform;
            while (current != null)
            {
                if (current.name.Contains("StackCardHolder"))
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Finds a DuelScene_CDC on the given object or its parent.
        /// Reuses CardModelProvider for self-lookup; parent fallback is needed because
        /// NPE hover simulation may focus a child element within the CDC hierarchy.
        /// </summary>
        private static object GetDuelSceneCDCOrParent(GameObject go)
        {
            var cdc = CardModelProvider.GetDuelSceneCDC(go);
            if (cdc != null) return cdc;

            // Focused element may be a child of the CDC (e.g. card art, text)
            if (go.transform.parent != null)
                return CardModelProvider.GetDuelSceneCDC(go.transform.parent.gameObject);

            return null;
        }

        private static void FireHoveredCardUpdated(object cdc)
        {
            try
            {
                if (!_hoverFieldSearched)
                {
                    _hoverFieldSearched = true;
                    var hoverType = FindType("CardHoverController");
                    if (hoverType != null)
                    {
                        // OnHoveredCardUpdated is a static event (Action<DuelScene_CDC>)
                        // Access the backing field directly
                        _hoverEventField = hoverType.GetField("OnHoveredCardUpdated",
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                }

                if (_hoverEventField == null) return;

                // Re-read the delegate each time since subscribers may change
                var currentDelegate = _hoverEventField.GetValue(null) as Delegate;
                if (currentDelegate != null)
                {
                    // DynamicInvoke(null) is ambiguous - must wrap in array to pass null as first arg
                    if (cdc != null)
                    {
                        currentDelegate.DynamicInvoke(cdc);
                        Log.Announce("DuelAnnouncer",
                            $"NPE hover simulated on: {((MonoBehaviour)cdc).gameObject.name}");
                    }
                    else
                    {
                        currentDelegate.DynamicInvoke(new object[] { null });
                        Log.Announce("DuelAnnouncer",
                            "NPE hover cleared (null) to unpause NPE director");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("DuelAnnouncer", $"Error simulating NPE hover: {ex.Message}");
            }
        }
    }
}
