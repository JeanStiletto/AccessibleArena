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

        // Reflection caches for NPE UX events. Each type is discovered via FindType
        // and cached independently — they share no parent type.
        private sealed class NpeDialogHandles { public FieldInfo Line; }
        private sealed class NpeReminderEventHandles { public FieldInfo Reminder; }
        private sealed class NpeReminderHandles { public FieldInfo Text; public FieldInfo SparkySuggested; }
        private sealed class LocalizedStringHandles { public FieldInfo Key; }
        private sealed class NpeWarningHandles { public FieldInfo DisplayText; }
        private sealed class NpeTooltipHandles { public FieldInfo Type; }

        private static readonly ReflectionCache<NpeDialogHandles> _npeDialogCache = new ReflectionCache<NpeDialogHandles>(
            builder: t => new NpeDialogHandles { Line = t.GetField("Line", PublicInstance) },
            validator: h => h.Line != null,
            logTag: "DuelAnnouncer",
            logSubject: "NPEDialogUXEvent");

        private static readonly ReflectionCache<NpeReminderEventHandles> _npeReminderEventCache = new ReflectionCache<NpeReminderEventHandles>(
            builder: t => new NpeReminderEventHandles { Reminder = t.GetField("_reminder", PrivateInstance) },
            validator: h => h.Reminder != null,
            logTag: "DuelAnnouncer",
            logSubject: "NPEReminderUXEvent");

        private static readonly ReflectionCache<NpeReminderHandles> _npeReminderCache = new ReflectionCache<NpeReminderHandles>(
            builder: t => new NpeReminderHandles
            {
                Text = t.GetField("Text", PublicInstance),
                SparkySuggested = t.GetField("SparkySuggestedInstances", PublicInstance),
            },
            validator: h => h.Text != null,
            logTag: "DuelAnnouncer",
            logSubject: "NPEReminder");

        private static readonly ReflectionCache<LocalizedStringHandles> _locStringCache = new ReflectionCache<LocalizedStringHandles>(
            builder: t => new LocalizedStringHandles { Key = t.GetField("Key", PublicInstance) },
            validator: h => h.Key != null,
            logTag: "DuelAnnouncer",
            logSubject: "MTGALocalizedString");

        private static readonly ReflectionCache<NpeWarningHandles> _npeWarningCache = new ReflectionCache<NpeWarningHandles>(
            builder: t => new NpeWarningHandles { DisplayText = t.GetField("_displayText", PrivateInstance) },
            validator: h => h.DisplayText != null,
            logTag: "DuelAnnouncer",
            logSubject: "NPEWarningUXEvent");

        private static readonly ReflectionCache<NpeTooltipHandles> _npeTooltipCache = new ReflectionCache<NpeTooltipHandles>(
            builder: t => new NpeTooltipHandles { Type = t.GetField("_type", PrivateInstance) },
            validator: h => h.Type != null,
            logTag: "DuelAnnouncer",
            logSubject: "NPEDismissableDeluxeTooltipUXEvent");

        /// <summary>
        /// Returns true if the current duel is an NPE tutorial game.
        /// Caches the result for the duration of the duel.
        /// </summary>
        public bool IsNPETutorial => _isNPETutorial;
        private bool _isNPETutorial;
        private bool _npeCheckDone;

        private sealed class GameManagerNpeHandles { public PropertyInfo NpeDirector; }
        private static readonly ReflectionCache<GameManagerNpeHandles> _gmNpeCache = new ReflectionCache<GameManagerNpeHandles>(
            builder: t => new GameManagerNpeHandles { NpeDirector = t.GetProperty("NpeDirector", PublicInstance) },
            validator: h => h.NpeDirector != null,
            logTag: "DuelAnnouncer",
            logSubject: "GameManager");

        // Hover simulation for NPE - fire CardHoverController.OnHoveredCardUpdated when user navigates to a CDC
        private GameObject _lastHoveredNPECard;
        private bool _lastHoverWasStack;

        private sealed class HoverHandles
        {
            public FieldInfo OnHoveredCardUpdated;   // static event backing field
        }
        private static readonly ReflectionCache<HoverHandles> _hoverCache = new ReflectionCache<HoverHandles>(
            builder: t => new HoverHandles
            {
                OnHoveredCardUpdated = t.GetField("OnHoveredCardUpdated",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            },
            validator: h => h.OnHoveredCardUpdated != null,
            logTag: "DuelAnnouncer",
            logSubject: "CardHoverController");

        private string HandleNPEDialog(object uxEvent)
        {
            try
            {
                if (!_npeDialogCache.EnsureInitialized(uxEvent.GetType())) return null;

                var lineObj = _npeDialogCache.Handles.Line.GetValue(uxEvent);
                if (lineObj == null) return null;

                string text = lineObj.ToString();
                if (string.IsNullOrEmpty(text)) return null;

                // Extract localization key for language-agnostic hint matching
                string locKey = null;
                if (_locStringCache.EnsureInitialized(lineObj.GetType()))
                    locKey = _locStringCache.Handles.Key.GetValue(lineObj) as string;

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
                if (!_npeReminderEventCache.EnsureInitialized(uxEvent.GetType())) return null;

                var reminderObj = _npeReminderEventCache.Handles.Reminder.GetValue(uxEvent);
                if (reminderObj == null) return null;

                if (!_npeReminderCache.EnsureInitialized(reminderObj.GetType())) return null;
                var r = _npeReminderCache.Handles;

                // Get reminder text and localization key
                string text = null;
                string locKey = null;
                var textObj = r.Text.GetValue(reminderObj);
                if (textObj != null)
                {
                    text = textObj.ToString();
                    if (_locStringCache.EnsureInitialized(textObj.GetType()))
                        locKey = _locStringCache.Handles.Key.GetValue(textObj) as string;
                }

                if (string.IsNullOrEmpty(text)) return null;

                // Try to resolve suggested card names
                string cardHint = null;
                if (r.SparkySuggested != null)
                {
                    var suggestedObj = r.SparkySuggested.GetValue(reminderObj);
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
                if (!_npeTooltipCache.EnsureInitialized(uxEvent.GetType())) return null;

                var typeObj = _npeTooltipCache.Handles.Type.GetValue(uxEvent);
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
                if (!_npeWarningCache.EnsureInitialized(uxEvent.GetType())) return null;

                var textObj = _npeWarningCache.Handles.DisplayText.GetValue(uxEvent);
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
                var gmType = FindType("GameManager");
                if (gmType == null) return;

                if (!_gmNpeCache.EnsureInitialized(gmType)) return;

                // GameManager is a singleton - find it
                var gmObj = UnityEngine.Object.FindObjectOfType(gmType) as Component;
                if (gmObj == null) return;

                var director = _gmNpeCache.Handles.NpeDirector.GetValue(gmObj);
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
                var hoverType = FindType("CardHoverController");
                if (hoverType == null) return;
                if (!_hoverCache.EnsureInitialized(hoverType)) return;

                // Re-read the delegate each time since subscribers may change
                var currentDelegate = _hoverCache.Handles.OnHoveredCardUpdated.GetValue(null) as Delegate;
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
