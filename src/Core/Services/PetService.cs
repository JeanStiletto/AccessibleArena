using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Reflection wrapper around the local player's pet (a.k.a. "accessory") — locates the
    /// active <c>AccessoryController</c>, decides which interactions the pet supports
    /// (whole-body tap vs the chest/arm/leg/head parts on richer pets, plus the hover-style
    /// "stroke" gesture), and invokes them through the same handlers the mouse would.
    ///
    /// Local pets are never affected by <c>MDNPlayerPrefs.DisableEmotes</c>, so we don't gate
    /// any of these calls on the global-mute flag — the controller's internal mute state for
    /// the local player stays false by construction.
    /// </summary>
    public static class PetService
    {
        public enum PetInteractionKind
        {
            Stroke, // hover enter + delayed hover exit
            Tap,    // whole-body click on a basic pet (HandleClick → clickEvent)
            Chest,  // InteractiveParts chest (HandleClick → chestClickEvent)
            Arm,    // InteractiveParts arm
            Leg,    // InteractiveParts right leg
            Head,   // InteractiveParts head
        }

        public readonly struct PetInteraction
        {
            public readonly PetInteractionKind Kind;
            public readonly string Label;
            public PetInteraction(PetInteractionKind kind, string label) { Kind = kind; Label = label; }
        }

        // Cached reflection handles on AccessoryController (and InteractiveParts subclass).
        // _ownerPlayerNum + _cosmetics live on the base class. Click parts only exist on the
        // _InteractiveParts subclass; we resolve them lazily and may legitimately end up null.
        private sealed class AccessoryHandles
        {
            public FieldInfo OwnerPlayerNum;     // public GREPlayerNum (base)
            public FieldInfo Cosmetics;          // protected CosmeticsProvider _cosmetics (base)
            public MethodInfo HandleClick;       // virtual, public
            public MethodInfo HandleHoverEnter;
            public MethodInfo HandleHoverExit;
            public MethodInfo HandleClickPart;   // only on InteractiveParts subclass
            public FieldInfo ClickEvent;         // protected UnityEvent (base) — used to gate Tap/Chest
            public FieldInfo HoverEnterEvent;    // protected UnityEvent (base) — used to gate Stroke
            public FieldInfo HoverExitEvent;     // protected UnityEvent (base)
            public FieldInfo ArmClickEvent;      // public UnityEvent — InteractiveParts only
            public FieldInfo LegClickEvent;
            public FieldInfo HeadClickEvent;
            public bool IsInteractiveParts;
        }

        // CosmeticsProvider.PlayerPetSelection (returns ClientPetSelection { name, variant }).
        private sealed class CosmeticsHandles
        {
            public PropertyInfo PlayerPetSelection;
        }

        private sealed class PetSelectionHandles
        {
            public FieldInfo Name;
            public FieldInfo Variant;
        }

        private static readonly ReflectionCache<CosmeticsHandles> _cosmeticsCache = new ReflectionCache<CosmeticsHandles>(
            builder: t => new CosmeticsHandles
            {
                PlayerPetSelection = t.GetProperty("PlayerPetSelection", PublicInstance),
            },
            validator: h => h.PlayerPetSelection != null,
            logTag: "PetService",
            logSubject: "CosmeticsProvider");

        private static readonly ReflectionCache<PetSelectionHandles> _selectionCache = new ReflectionCache<PetSelectionHandles>(
            builder: t => new PetSelectionHandles
            {
                Name = t.GetField("name", PublicInstance),
                Variant = t.GetField("variant", PublicInstance),
            },
            validator: h => h.Name != null && h.Variant != null,
            logTag: "PetService",
            logSubject: "ClientPetSelection");

        // Cached enum value GREPlayerNum.LocalPlayer (resolved once).
        private static object _localPlayerEnum;
        private static bool _localPlayerEnumResolved;

        // Cached local pet — invalidated on scene change.
        private static MonoBehaviour _localController;
        private static int _localControllerSearchFrame = -1;
        private static AccessoryHandles _localHandles;

        public static void ClearCache()
        {
            _localController = null;
            _localControllerSearchFrame = -1;
            _localHandles = null;
            _localPlayerEnum = null;
            _localPlayerEnumResolved = false;
            _eventDumpLogged = false;
            _mutedStateLogged = false;
        }

        /// <summary>
        /// Locates the local player's <c>AccessoryController</c>. Walks the type hierarchy so
        /// any subclass (including <c>AccessoryController_InteractiveParts</c>) is recognized.
        /// Returns null when no local pet is in the scene.
        /// </summary>
        public static MonoBehaviour FindLocalAccessoryController()
        {
            // Drop a stale reference (Unity-destroyed objects compare false to null).
            if (_localController != null && !_localController) { _localController = null; _localHandles = null; }
            if (_localController != null) return _localController;

            int frame = Time.frameCount;
            if (frame == _localControllerSearchFrame) return null;
            _localControllerSearchFrame = frame;

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                // Walk inheritance to AccessoryController.
                Type baseType = null;
                for (var cur = mb.GetType(); cur != null; cur = cur.BaseType)
                {
                    if (cur.Name == "AccessoryController") { baseType = cur; break; }
                }
                if (baseType == null) continue;

                var ownerField = baseType.GetField("_ownerPlayerNum", PublicInstance);
                if (ownerField == null) continue;

                // Resolve the GREPlayerNum.LocalPlayer enum value from the field type itself,
                // rather than by FindType("GREPlayerNum") — name-only lookup picks up unrelated
                // wrapper classes that share the simple name and aren't enums.
                if (!_localPlayerEnumResolved)
                {
                    _localPlayerEnumResolved = true;
                    if (!ownerField.FieldType.IsEnum)
                    {
                        Log.Warn("PetService", $"_ownerPlayerNum field type is not an enum: {ownerField.FieldType.FullName}");
                    }
                    else
                    {
                        try { _localPlayerEnum = Enum.Parse(ownerField.FieldType, "LocalPlayer"); }
                        catch (Exception ex) { Log.Warn("PetService", $"Enum.Parse({ownerField.FieldType.FullName},'LocalPlayer') threw", ex); }
                    }
                }
                if (_localPlayerEnum == null) return null;

                var owner = ownerField.GetValue(mb);
                if (owner == null || !owner.Equals(_localPlayerEnum)) continue;

                _localController = mb;
                _localHandles = BuildHandles(mb.GetType(), baseType);
                return _localController;
            }
            return null;
        }

        private static AccessoryHandles BuildHandles(Type concreteType, Type baseType)
        {
            var h = new AccessoryHandles
            {
                IsInteractiveParts = concreteType.Name == "AccessoryController_InteractiveParts",
                OwnerPlayerNum = baseType.GetField("_ownerPlayerNum", PublicInstance),
                Cosmetics = baseType.GetField("_cosmetics", PrivateInstance),
                HandleClick = concreteType.GetMethod("HandleClick", PublicInstance, null, Type.EmptyTypes, null),
                HandleHoverEnter = concreteType.GetMethod("HandleHoverEnter", PublicInstance, null, Type.EmptyTypes, null),
                HandleHoverExit = concreteType.GetMethod("HandleHoverExit", PublicInstance, null, Type.EmptyTypes, null),
                // UnityEvent fields are protected on the base class — needed so we can suppress
                // menu entries for actions whose prefab event has zero persistent listeners.
                ClickEvent = baseType.GetField("clickEvent", PrivateInstance),
                HoverEnterEvent = baseType.GetField("hoverEnterEvent", PrivateInstance),
                HoverExitEvent = baseType.GetField("hoverExitEvent", PrivateInstance),
            };
            if (h.IsInteractiveParts)
            {
                h.HandleClickPart = concreteType.GetMethod("HandleClickPart", PublicInstance, null, new[] { typeof(UnityEvent) }, null);
                h.ArmClickEvent = concreteType.GetField("armClickEvent", PublicInstance);
                h.LegClickEvent = concreteType.GetField("legClickEvent", PublicInstance);
                h.HeadClickEvent = concreteType.GetField("headClickEvent", PublicInstance);
            }
            return h;
        }

        // Returns true if the named UnityEvent on the controller has at least one persistent
        // listener wired by the prefab. Used to hide pet actions that would do nothing.
        // Tolerates a null FieldInfo (filter fails open — better to surface a no-op action
        // than to hide a real one because reflection couldn't see the field).
        private static bool HasListeners(MonoBehaviour ctrl, FieldInfo evtField)
        {
            if (evtField == null) return true;
            try
            {
                var evt = evtField.GetValue(ctrl) as UnityEvent;
                return evt != null && evt.GetPersistentEventCount() > 0;
            }
            catch { return true; }
        }

        /// <summary>True iff a local pet is in the duel scene.</summary>
        public static bool HasLocalPet() => FindLocalAccessoryController() != null;

        /// <summary>
        /// Returns the menu of interactions supported by the active local pet.
        /// Empty list when no pet is present yet.
        /// </summary>
        public static List<PetInteraction> GetAvailableInteractions()
        {
            var ctrl = FindLocalAccessoryController();
            if (ctrl == null || _localHandles == null) return new List<PetInteraction>();

            var list = new List<PetInteraction>(5);
            // Each option also gates on the prefab's UnityEvent having at least one persistent
            // listener — pets like ONE_Skitterling have hover events with zero listeners, so
            // surfacing "Stroke" would just announce "ausgelöst" with no in-game effect.
            if (_localHandles.HandleHoverEnter != null && _localHandles.HandleHoverExit != null
                && (HasListeners(ctrl, _localHandles.HoverEnterEvent) || HasListeners(ctrl, _localHandles.HoverExitEvent)))
                list.Add(new PetInteraction(PetInteractionKind.Stroke, Strings.PetActionStroke));

            if (_localHandles.IsInteractiveParts)
            {
                if (_localHandles.HandleClick != null && HasListeners(ctrl, _localHandles.ClickEvent))
                    list.Add(new PetInteraction(PetInteractionKind.Chest, Strings.PetActionChest));
                if (_localHandles.HandleClickPart != null && HasListeners(ctrl, _localHandles.ArmClickEvent))
                    list.Add(new PetInteraction(PetInteractionKind.Arm, Strings.PetActionArm));
                if (_localHandles.HandleClickPart != null && HasListeners(ctrl, _localHandles.LegClickEvent))
                    list.Add(new PetInteraction(PetInteractionKind.Leg, Strings.PetActionLeg));
                if (_localHandles.HandleClickPart != null && HasListeners(ctrl, _localHandles.HeadClickEvent))
                    list.Add(new PetInteraction(PetInteractionKind.Head, Strings.PetActionHead));
            }
            else
            {
                if (_localHandles.HandleClick != null && HasListeners(ctrl, _localHandles.ClickEvent))
                    list.Add(new PetInteraction(PetInteractionKind.Tap, Strings.PetActionTap));
            }
            return list;
        }

        /// <summary>
        /// Triggers the interaction on the local pet. Stroke fires HandleHoverEnter
        /// immediately and schedules HandleHoverExit 1 second later via a Unity coroutine.
        /// Returns false if the pet vanished between the menu opening and the press.
        /// </summary>
        public static bool TriggerInteraction(PetInteractionKind kind)
        {
            var ctrl = FindLocalAccessoryController();
            if (ctrl == null || _localHandles == null) return false;

            DumpPetEventListenersOnce(ctrl);
            LogMutedStateIfRelevant(ctrl);

            try
            {
                switch (kind)
                {
                    case PetInteractionKind.Stroke:
                        _localHandles.HandleHoverEnter?.Invoke(ctrl, null);
                        MelonCoroutines.Start(DelayedHoverExit(ctrl, _localHandles));
                        return true;

                    case PetInteractionKind.Tap:
                    case PetInteractionKind.Chest:
                        _localHandles.HandleClick?.Invoke(ctrl, null);
                        return true;

                    case PetInteractionKind.Arm:
                        return InvokePart(ctrl, _localHandles.ArmClickEvent);
                    case PetInteractionKind.Leg:
                        return InvokePart(ctrl, _localHandles.LegClickEvent);
                    case PetInteractionKind.Head:
                        return InvokePart(ctrl, _localHandles.HeadClickEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PetService", $"TriggerInteraction({kind}) threw", ex);
                return false;
            }
            return false;
        }

        // Diagnostic latch: dumps the pet's UnityEvent persistent listeners once per cache
        // lifetime so we can tell whether a "silent" interaction is failing to fire or simply
        // wired to visual-only callbacks (no audio). Reset on scene change with the rest.
        private static bool _eventDumpLogged;

        private static void DumpPetEventListenersOnce(MonoBehaviour ctrl)
        {
            if (_eventDumpLogged) return;
            _eventDumpLogged = true;

            // Walk the AccessoryController hierarchy collecting every UnityEvent field. Each
            // pet prefab wires these in the Inspector — listeners reveal animator/audio split.
            var fields = new List<FieldInfo>();
            for (var t = ctrl.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var f in t.GetFields(AllInstanceFlags))
                {
                    if (typeof(UnityEvent).IsAssignableFrom(f.FieldType) && !fields.Exists(x => x.Name == f.Name))
                        fields.Add(f);
                }
            }

            Log.Msg("PetService", $"Dumping pet '{ctrl.gameObject.name}' UnityEvent listeners ({fields.Count} fields):");
            foreach (var f in fields)
            {
                UnityEvent evt;
                try { evt = f.GetValue(ctrl) as UnityEvent; }
                catch { continue; }
                Log.Msg("PetService", $"  {f.Name}: {DescribeUnityEvent(evt)}");
            }
        }

        private static string DescribeUnityEvent(UnityEvent evt)
        {
            if (evt == null) return "null";
            int count;
            try { count = evt.GetPersistentEventCount(); }
            catch { return "(GetPersistentEventCount threw)"; }
            if (count == 0) return "no persistent listeners";

            var parts = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                string targetName, methodName;
                try
                {
                    var target = evt.GetPersistentTarget(i);
                    targetName = target == null ? "(null)" : target.GetType().Name;
                    methodName = evt.GetPersistentMethodName(i) ?? "(no-method)";
                }
                catch { targetName = "(threw)"; methodName = "(threw)"; }
                parts.Add($"{targetName}.{methodName}");
            }
            return $"[{string.Join(", ", parts)}]";
        }

        private static bool _mutedStateLogged;
        private static void LogMutedStateIfRelevant(MonoBehaviour ctrl)
        {
            if (_mutedStateLogged) return;
            _mutedStateLogged = true;

            // Pet's _muted property gates every action — log it once so we know whether a
            // silent press is being rejected at the controller's first guard.
            try
            {
                Type cur = ctrl.GetType();
                FieldInfo gm = null, pm = null;
                while (cur != null && (gm == null || pm == null))
                {
                    gm = gm ?? cur.GetField("_globalMuted", PrivateInstance | BindingFlags.FlattenHierarchy);
                    pm = pm ?? cur.GetField("_playerMuted", PrivateInstance | BindingFlags.FlattenHierarchy);
                    cur = cur.BaseType;
                }
                bool? gmv = gm == null ? (bool?)null : (bool)gm.GetValue(ctrl);
                bool? pmv = pm == null ? (bool?)null : (bool)pm.GetValue(ctrl);
                Log.Msg("PetService", $"Pet mute state: _globalMuted={gmv?.ToString() ?? "?"}, _playerMuted={pmv?.ToString() ?? "?"} (either=true blocks all actions)");
            }
            catch (Exception ex)
            {
                Log.Warn("PetService", "Could not read pet mute fields", ex);
            }
        }

        private static bool InvokePart(MonoBehaviour ctrl, FieldInfo eventField)
        {
            if (eventField == null || _localHandles?.HandleClickPart == null) return false;
            var evt = eventField.GetValue(ctrl) as UnityEvent;
            if (evt == null) return false;
            _localHandles.HandleClickPart.Invoke(ctrl, new object[] { evt });
            return true;
        }

        // The pet may be destroyed between the hover-enter and our scheduled hover-exit
        // (scene change, GameManager teardown). Re-check liveness before calling.
        private static IEnumerator DelayedHoverExit(MonoBehaviour ctrl, AccessoryHandles handles)
        {
            yield return new WaitForSeconds(1f);
            if (ctrl == null || !ctrl) yield break;
            try { handles.HandleHoverExit?.Invoke(ctrl, null); }
            catch { /* best-effort — pet likely torn down */ }
        }

        /// <summary>
        /// Resolves the localized name of the local player's equipped pet, or null when the
        /// cosmetics provider isn't reachable yet. Tries the game's loc-key precedence
        /// (variant-specific → name-only) and falls back to the raw name on miss.
        /// </summary>
        public static string GetLocalPetName()
        {
            var ctrl = FindLocalAccessoryController();
            if (ctrl == null || _localHandles == null || _localHandles.Cosmetics == null) return null;

            object cosmetics;
            try { cosmetics = _localHandles.Cosmetics.GetValue(ctrl); }
            catch { return null; }
            if (cosmetics == null) return null;

            if (!_cosmeticsCache.EnsureInitialized(cosmetics.GetType())) return null;

            object selection;
            try { selection = _cosmeticsCache.Handles.PlayerPetSelection.GetValue(cosmetics); }
            catch { return null; }
            if (selection == null) return null;

            if (!_selectionCache.EnsureInitialized(selection.GetType())) return null;
            var name = _selectionCache.Handles.Name.GetValue(selection) as string;
            var variant = _selectionCache.Handles.Variant.GetValue(selection) as string;
            if (string.IsNullOrEmpty(name)) return null;

            // PetUtils.KeyForPetDetails precedence: variant-specific, then name-only.
            // We don't have a Level here (ClientPetSelection is name+variant only).
            if (!string.IsNullOrEmpty(variant))
            {
                var keyVariant = $"MainNav/PetNames/{name}_{variant}";
                var resolved = UITextExtractor.ResolveLocKey(keyVariant);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            var keyName = $"MainNav/PetNames/{name}";
            return UITextExtractor.ResolveLocKey(keyName) ?? name;
        }
    }
}
