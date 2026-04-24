using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Read-only view of the game's live battlefield stacks.
    ///
    /// MTGA currently ships two holder variants:
    ///   - Legacy <c>BattlefieldCardHolder</c> / <c>BattlefieldCardHolder_MP</c>:
    ///       public <c>Regions</c> (BattlefieldRegion[]) -> <c>Stacks</c> (List&lt;BattlefieldStack&gt;)
    ///   - <c>UniversalBattlefieldCardHolder</c>:
    ///       private <c>_regions</c> -> <c>AllGroups</c> -> <c>AllStacks</c>
    ///
    /// Both stack classes implement the same public API (StackParent, AllCards,
    /// HasAttachmentOrExile, IsAttackStack, IsBlockStack) so we only have to branch
    /// on how to walk from the holder down to the stack list.
    ///
    /// Prototype stage: used only to log the stack layout for comparison against
    /// BattlefieldNavigator's flat per-card list.
    /// </summary>
    public static class BattlefieldStackProvider
    {
        private const string HolderUniversal = "UniversalBattlefieldCardHolder";
        private const string HolderLegacy = "BattlefieldCardHolder";
        private const string HolderLegacyMp = "BattlefieldCardHolder_MP";

        private static MonoBehaviour _cachedHolder;
        private static bool _isUniversal;

        // Top-level holder handles
        private static FieldInfo _universalRegionsField; // List<UniversalBattlefieldRegion>
        private static PropertyInfo _legacyLayoutProp;   // ICardLayout (CardHolderBase.Layout)

        // Per-runtime-type caches for anything below the holder.
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propCache
            = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> _fieldCache
            = new Dictionary<Type, Dictionary<string, FieldInfo>>();

        // Stack index: populated by BuildStackIndex, consumed by BattlefieldNavigator.
        // StackParent InstanceId -> total cards in its stack (>=1).
        // Children (non-parent InstanceIds in multi-card stacks) are added to _childIds.
        // Stacks flagged HasAttachmentOrExile are NOT grouped here — attachment/exile
        // presentation is handled separately by CardStateProvider so we'd double-count.
        private static readonly Dictionary<uint, int> _stackSizeByParent = new Dictionary<uint, int>();
        private static readonly HashSet<uint> _childIds = new HashSet<uint>();

        public static HashSet<uint> StackChildIds => _childIds;

        public static bool TryGetStackSize(uint parentInstanceId, out int size)
            => _stackSizeByParent.TryGetValue(parentInstanceId, out size);

        public static void ClearCache()
        {
            _cachedHolder = null;
            _isUniversal = false;
            _universalRegionsField = null;
            _legacyLayoutProp = null;
            _propCache.Clear();
            _fieldCache.Clear();
            _stackSizeByParent.Clear();
            _childIds.Clear();
        }

        private static PropertyInfo GetProp(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            if (!_propCache.TryGetValue(t, out var map))
            {
                map = new Dictionary<string, PropertyInfo>();
                _propCache[t] = map;
            }
            if (!map.TryGetValue(name, out var pi))
            {
                pi = t.GetProperty(name, AllInstanceFlags | BindingFlags.FlattenHierarchy);
                map[name] = pi;
            }
            return pi;
        }

        private static FieldInfo GetFieldCached(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            if (!_fieldCache.TryGetValue(t, out var map))
            {
                map = new Dictionary<string, FieldInfo>();
                _fieldCache[t] = map;
            }
            if (!map.TryGetValue(name, out var fi))
            {
                fi = t.GetField(name, AllInstanceFlags | BindingFlags.FlattenHierarchy);
                map[name] = fi;
            }
            return fi;
        }

        private static MonoBehaviour FindHolder()
        {
            if (_cachedHolder != null)
            {
                try { if (_cachedHolder.gameObject != null) return _cachedHolder; }
                catch { }
                _cachedHolder = null;
            }

            // Preferred: the holder component lives on the "BattlefieldCardHolder"
            // GameObject we already cache for flat-list navigation.
            var go = DuelHolderCache.GetHolder("BattlefieldCardHolder");
            if (go != null)
            {
                foreach (var mb in go.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    if (IsHolderType(mb.GetType().Name))
                    {
                        _cachedHolder = mb;
                        _isUniversal = mb.GetType().Name == HolderUniversal;
                        return mb;
                    }
                }
            }

            // Fallback: scene-wide scan (older Unity: active MonoBehaviours only).
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (IsHolderType(mb.GetType().Name))
                {
                    _cachedHolder = mb;
                    _isUniversal = mb.GetType().Name == HolderUniversal;
                    return mb;
                }
            }
            return null;
        }

        private static bool IsHolderType(string typeName) =>
            typeName == HolderUniversal || typeName == HolderLegacy || typeName == HolderLegacyMp;

        /// <summary>
        /// Logs the current battlefield stack layout to the MelonLoader console.
        /// Safe no-op if the holder or reflection handles cannot be resolved.
        /// </summary>
        public static void LogStackStructure()
        {
            var holder = FindHolder();
            if (holder == null)
            {
                Log.Msg("BattlefieldStackProvider", "Battlefield card holder component not found");
                return;
            }

            try
            {
                if (_isUniversal)
                    LogUniversal(holder);
                else
                    LogLegacy(holder);
            }
            catch (Exception ex)
            {
                Log.Error("BattlefieldStackProvider", $"LogStackStructure failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Rebuilds the stack index (StackParent -> count, set of stacked-child InstanceIds).
        /// Call once per BattlefieldNavigator refresh when BattlefieldStacking is enabled.
        /// </summary>
        public static void BuildStackIndex()
        {
            _stackSizeByParent.Clear();
            _childIds.Clear();

            var holder = FindHolder();
            if (holder == null) return;

            try
            {
                if (_isUniversal)
                    WalkUniversal(holder, IndexStack);
                else
                    WalkLegacy(holder, IndexStack);
            }
            catch (Exception ex)
            {
                Log.Warn("BattlefieldStackProvider", $"BuildStackIndex failed: {ex.Message}");
            }
        }

        private static void IndexStack(object stack)
        {
            if (stack == null) return;
            try
            {
                // Stacks that also carry attachments/exile get handled by the existing
                // attachment path — don't collapse them here.
                if (GetBoolSafe(stack, "HasAttachmentOrExile")) return;

                var parent = GetProp(stack, "StackParent")?.GetValue(stack) as Component;
                if (parent == null) return;

                uint parentId = 0;
                var idVal = GetProp(parent, "InstanceId")?.GetValue(parent);
                if (idVal is uint u) parentId = u;
                if (parentId == 0) return;

                var cards = GetProp(stack, "AllCards")?.GetValue(stack) as IList;
                int count = cards?.Count ?? 1;
                _stackSizeByParent[parentId] = count;

                if (cards == null || count <= 1) return;

                foreach (var card in cards)
                {
                    var comp = card as Component;
                    if (comp == null) continue;
                    var cid = GetProp(comp, "InstanceId")?.GetValue(comp);
                    if (cid is uint uc && uc != parentId) _childIds.Add(uc);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("BattlefieldStackProvider", $"IndexStack failed: {ex.Message}");
            }
        }

        // Minimal walkers (no metadata) — shared by BuildStackIndex.
        private static void WalkUniversal(MonoBehaviour holder, Action<object> onStack)
        {
            if (_universalRegionsField == null)
            {
                _universalRegionsField = holder.GetType().GetField("_regions", PrivateInstance);
                if (_universalRegionsField == null) return;
            }
            var regions = _universalRegionsField.GetValue(holder) as IEnumerable;
            if (regions == null) return;
            foreach (var region in regions)
            {
                if (region == null) continue;
                var groups = GetProp(region, "AllGroups")?.GetValue(region) as IEnumerable;
                if (groups == null) continue;
                foreach (var group in groups)
                {
                    if (group == null) continue;
                    var stacks = GetProp(group, "AllStacks")?.GetValue(group) as IEnumerable;
                    if (stacks == null) continue;
                    foreach (var stack in stacks) onStack(stack);
                }
            }
        }

        private static void WalkLegacy(MonoBehaviour holder, Action<object> onStack)
        {
            if (_legacyLayoutProp == null)
            {
                _legacyLayoutProp = holder.GetType().GetProperty("Layout",
                    AllInstanceFlags | BindingFlags.FlattenHierarchy);
                if (_legacyLayoutProp == null) return;
            }
            var layout = _legacyLayoutProp.GetValue(holder);
            if (layout == null) return;
            var regions = GetProp(layout, "Regions")?.GetValue(layout) as IEnumerable;
            if (regions == null) return;
            foreach (var region in regions)
            {
                if (region == null) continue;
                var stacksField = GetFieldCached(region, "Stacks");
                var stacks = (stacksField?.GetValue(region) as IEnumerable)
                             ?? (GetProp(region, "Stacks")?.GetValue(region) as IEnumerable);
                if (stacks == null) continue;
                foreach (var stack in stacks) onStack(stack);
            }
        }

        // --- Universal (post-rework) holder -------------------------------------

        private static void LogUniversal(MonoBehaviour holder)
        {
            if (_universalRegionsField == null)
            {
                _universalRegionsField = holder.GetType().GetField("_regions", PrivateInstance);
                if (_universalRegionsField == null)
                {
                    Log.Warn("BattlefieldStackProvider",
                        $"_regions field missing on {holder.GetType().Name}");
                    return;
                }
                Log.Msg("BattlefieldStackProvider", "Universal reflection initialized");
            }

            var regions = _universalRegionsField.GetValue(holder) as IEnumerable;
            if (regions == null) return;

            Log.Msg("BattlefieldStackProvider", "=== Stack structure (universal) ===");
            int totalStacks = 0;
            int totalCards = 0;
            int regionIdx = 0;

            foreach (var region in regions)
            {
                if (region == null) continue;
                regionIdx++;
                string regionName = GetFieldCached(region, "_name")?.GetValue(region) as string
                                    ?? ("Region" + regionIdx);
                string controller = GetProp(region, "Controller")?.GetValue(region)?.ToString() ?? "?";

                var groups = GetProp(region, "AllGroups")?.GetValue(region) as IEnumerable;
                if (groups == null) continue;

                foreach (var group in groups)
                {
                    if (group == null) continue;
                    string groupName = "";
                    string groupType = "";
                    var config = GetProp(group, "Config")?.GetValue(group);
                    if (config != null)
                    {
                        groupName = GetProp(config, "Name")?.GetValue(config) as string ?? "";
                        groupType = GetProp(config, "GroupType")?.GetValue(config)?.ToString() ?? "";
                    }

                    var stacks = GetProp(group, "AllStacks")?.GetValue(group) as IEnumerable;
                    if (stacks == null) continue;

                    var stackList = CollectStacks(stacks, out int groupCards);
                    if (stackList.Count == 0) continue;

                    totalStacks += stackList.Count;
                    totalCards += groupCards;

                    MelonLogger.Msg(
                        $"  {regionName} ({controller}) / {groupName} ({groupType}): " +
                        $"{stackList.Count} stack(s), {groupCards} card(s)");

                    foreach (var stack in stackList)
                        LogStack(stack);
                }
            }

            Log.Msg("BattlefieldStackProvider",
                $"=== End ({totalStacks} stacks, {totalCards} cards) ===");
        }

        // --- Legacy holder (BattlefieldCardHolder / _MP) ------------------------

        private static void LogLegacy(MonoBehaviour holder)
        {
            if (_legacyLayoutProp == null)
            {
                _legacyLayoutProp = holder.GetType().GetProperty("Layout",
                    AllInstanceFlags | BindingFlags.FlattenHierarchy);
                if (_legacyLayoutProp == null)
                {
                    Log.Warn("BattlefieldStackProvider",
                        $"Layout property missing on {holder.GetType().Name}");
                    return;
                }
                Log.Msg("BattlefieldStackProvider", "Legacy reflection initialized");
            }

            var layout = _legacyLayoutProp.GetValue(holder);
            if (layout == null)
            {
                Log.Warn("BattlefieldStackProvider", "Layout was null on holder");
                return;
            }

            var regions = GetProp(layout, "Regions")?.GetValue(layout) as IEnumerable;
            if (regions == null)
            {
                Log.Warn("BattlefieldStackProvider",
                    $"Regions missing on layout type {layout.GetType().Name}");
                return;
            }

            Log.Msg("BattlefieldStackProvider", "=== Stack structure (legacy) ===");
            int totalStacks = 0;
            int totalCards = 0;
            int regionIdx = 0;

            foreach (var region in regions)
            {
                if (region == null) continue;
                regionIdx++;
                bool opponent = GetProp(region, "Opponent")?.GetValue(region) as bool? ?? false;
                string regionName = TryGetLegacyRegionName(region) ?? ("Region" + regionIdx);

                var stacksField = GetFieldCached(region, "Stacks");
                var stacksEnum = (stacksField?.GetValue(region) as IEnumerable)
                                 ?? (GetProp(region, "Stacks")?.GetValue(region) as IEnumerable);
                if (stacksEnum == null) continue;

                var stackList = CollectStacks(stacksEnum, out int regionCards);
                if (stackList.Count == 0) continue;

                totalStacks += stackList.Count;
                totalCards += regionCards;

                MelonLogger.Msg(
                    $"  {regionName} ({(opponent ? "Opponent" : "LocalPlayer")}): " +
                    $"{stackList.Count} stack(s), {regionCards} card(s)");

                foreach (var stack in stackList)
                    LogStack(stack);
            }

            Log.Msg("BattlefieldStackProvider",
                $"=== End ({totalStacks} stacks, {totalCards} cards) ===");
        }

        private static string TryGetLegacyRegionName(object region)
        {
            try
            {
                // RegionLocator is a Transform whose name is set to LayoutVariants[0].Name.
                var locator = GetProp(region, "RegionLocator")?.GetValue(region) as Component;
                if (locator != null && !string.IsNullOrEmpty(locator.name))
                    return locator.name;
            }
            catch { }
            return null;
        }

        // --- Shared stack walking ----------------------------------------------

        private static List<object> CollectStacks(IEnumerable stacks, out int totalCards)
        {
            totalCards = 0;
            var list = new List<object>();
            foreach (var s in stacks)
            {
                if (s == null) continue;
                list.Add(s);
                var cards = GetProp(s, "AllCards")?.GetValue(s) as IList;
                if (cards != null) totalCards += cards.Count;
            }
            return list;
        }

        private static void LogStack(object stack)
        {
            try
            {
                var cards = GetProp(stack, "AllCards")?.GetValue(stack) as IList;
                int count = cards?.Count ?? 0;

                var parent = GetProp(stack, "StackParent")?.GetValue(stack) as Component;
                string parentName = "(no parent)";
                uint parentInstanceId = 0;
                if (parent != null)
                {
                    parentName = CardDetector.GetCardName(parent.gameObject);
                    var idVal = GetProp(parent, "InstanceId")?.GetValue(parent);
                    if (idVal is uint u) parentInstanceId = u;
                }

                bool hasAttach = GetBoolSafe(stack, "HasAttachmentOrExile");
                bool isAttack = GetBoolSafe(stack, "IsAttackStack");
                bool isBlock = GetBoolSafe(stack, "IsBlockStack");

                var flags = new List<string>();
                if (hasAttach) flags.Add("attach/exile");
                if (isAttack) flags.Add("attack");
                if (isBlock) flags.Add("block");
                string flagText = flags.Count > 0 ? " [" + string.Join(",", flags) + "]" : "";

                MelonLogger.Msg(
                    $"    - {parentName} x{count} (parentInstanceId={parentInstanceId}){flagText}");
            }
            catch (Exception ex)
            {
                Log.Warn("BattlefieldStackProvider", $"LogStack failed: {ex.Message}");
            }
        }

        // IsAttackStack/IsBlockStack getters dereference StackParentModel.Instance,
        // which can throw on partially-initialized stacks. Swallow.
        private static bool GetBoolSafe(object o, string name)
        {
            try
            {
                var v = GetProp(o, name)?.GetValue(o);
                return v is bool b && b;
            }
            catch { return false; }
        }
    }
}
