using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Bridges our Ctrl+Enter shortcut to the game's stack-count badge click.
    ///
    /// In MTGA, sighted players can click the little "xN" badge hovering over a
    /// multi-card stack to select/toggle the whole stack at once. We ask the
    /// current workflow's CanClickStack with our prepared proxy view, and only
    /// dispatch when it returns true. See GameInteractionSystem.OnStackClicked
    /// and the IClickableWorkflow.CanClickStack extension at WorkflowBase.
    ///
    /// Scan of all IClickableWorkflow implementers (Core.dll) shows stack-badge
    /// clicks are only honored by DeclareAttackersWorkflow and DeclareBlockersWorkflow.
    /// Every other workflow — ActionsAvailableWorkflow, SelectTargetsWorkflow,
    /// SelectionWorkflow, GroupWorkflow_BattlefieldPermanents, SelectFromGroupsWorkflow,
    /// DistributionWorkflow, all SelectN variants, etc. — hardcodes CanClickStack=false.
    ///
    /// Because of that, a native badge click is only available in combat. For every other
    /// workflow BattlefieldNavigator falls back to clicking each card of the stack in turn
    /// (see PumpStackClickSequence there) — the same clicks a sighted player makes on a fanned
    /// stack, each one validated by the game's own workflow.
    ///
    /// That fallback has to pace itself: workflows implementing IRoundTripWorkflow (which
    /// includes SelectTargetsWorkflow) send each click to the GRE and refuse every further
    /// one until the answer comes back. IsWaitingForRoundTrip and WorkflowAcceptsClick below
    /// expose the game's own two gates so the sequence waits instead of firing into the void.
    ///
    /// Note: tapping multiple lands at once during spell payment is handled separately
    /// by the game itself via ActionsAvailableWorkflow.BatchManaSubmission.OnClick,
    /// which iterates stack.AllCards on a single per-card click. That batches mana
    /// without going through OnStackClicked, so a plain Enter on a stacked land while
    /// paying for a spell already taps as many copies as the cost requires.
    /// </summary>
    public static class StackInteractionBridge
    {
        public enum Result { Success, Unavailable, ReflectionFailure }

        private static Type _cdcViewType;
        private static FieldInfo _parentIdField;
        private static MethodInfo _onStackClickedMethod;
        private static Type _simpleInteractionEnum;
        private static object _primaryValue;

        // Dummy GameObject we reuse across calls — holds a real CdcStackCounterView
        // component so OnStackClicked's `view.gameObject` reference is always valid.
        private static GameObject _proxyGo;
        private static MonoBehaviour _proxyView;

        private static MonoBehaviour _cachedGameManager;

        public static void ClearCache()
        {
            _cachedGameManager = null;
            _cdcViewType = null;
            _parentIdField = null;
            _onStackClickedMethod = null;
            _simpleInteractionEnum = null;
            _primaryValue = null;
            if (_proxyGo != null)
            {
                try { UnityEngine.Object.Destroy(_proxyGo); } catch { }
            }
            _proxyGo = null;
            _proxyView = null;
        }

        /// <summary>
        /// Attempts to select/toggle the entire stack whose parent has the given InstanceId.
        /// Returns Success when the current workflow consumed the click, Unavailable when
        /// the workflow doesn't support stack clicks for this stack, or ReflectionFailure
        /// on reflection/setup failure.
        /// </summary>
        public static Result TrySelectStack(uint parentInstanceId)
        {
            if (parentInstanceId == 0) return Result.ReflectionFailure;

            try
            {
                if (!EnsureReflection()) return Result.ReflectionFailure;

                MonoBehaviour gm = FindGameManager();
                if (gm == null) return Result.ReflectionFailure;

                var interactionSystem = GetInteractionSystem(gm);
                if (interactionSystem == null) return Result.ReflectionFailure;

                var workflow = GetCurrentWorkflow(gm);
                if (workflow == null)
                {
                    Log.Msg("StackInteractionBridge", "no current workflow");
                    return Result.Unavailable;
                }

                if (!EnsureProxyView()) return Result.ReflectionFailure;
                _parentIdField.SetValue(_proxyView, parentInstanceId);

                if (!WorkflowAcceptsStackClick(workflow))
                {
                    Log.Msg("StackInteractionBridge",
                        $"workflow '{workflow.GetType().Name}' rejected stack click for id {parentInstanceId}");
                    return Result.Unavailable;
                }

                _onStackClickedMethod.Invoke(interactionSystem,
                    new object[] { _proxyView, _primaryValue });
                Log.Msg("StackInteractionBridge",
                    $"dispatched stack click via '{workflow.GetType().Name}' for id {parentInstanceId}");
                return Result.Success;
            }
            catch (Exception ex)
            {
                Log.Warn("StackInteractionBridge", $"TrySelectStack failed: {ex.Message}");
                return Result.ReflectionFailure;
            }
        }

        /// <summary>
        /// True while the current workflow is mid round trip to the GRE: it has sent the
        /// player's last click and refuses every further one until the answer arrives
        /// (IRoundTripWorkflow.IsWaitingForRoundTrip).
        ///
        /// SelectTargetsWorkflow sets that flag on the *first* selection click
        /// (UpdateTarget -> _submitted = true) and its CanClick returns false for as long
        /// as it is set. A burst of stack clicks on a fixed timer therefore only ever
        /// landed the first one; the rest were refused and fell through to the invalid sfx.
        /// </summary>
        public static bool IsWaitingForRoundTrip()
        {
            try
            {
                var workflow = GetCurrentWorkflow();
                return workflow != null && WorkflowIsWaiting(workflow);
            }
            catch { return false; }
        }

        private static bool WorkflowIsWaiting(object workflow)
        {
            if (workflow == null) return false;

            var method = workflow.GetType().GetMethod("IsWaitingForRoundTrip",
                AllInstanceFlags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool))
            {
                try { if ((bool)method.Invoke(workflow, null)) return true; }
                catch { }
            }

            return AnyChildWorkflow(workflow, WorkflowIsWaiting);
        }

        /// <summary>
        /// Asks the current workflow's own IClickableWorkflow.CanClick whether a plain
        /// primary click on this card would be honored right now — the exact gate
        /// GameInteractionSystem.HandleCardViewClick applies before forwarding a click.
        ///
        /// Permissive on failure: when the workflow, the card component or the method
        /// can't be resolved we return true and let the click go out, rather than
        /// silently dropping clicks the game would have accepted. Callers that use the
        /// answer to *discover* clickable cards rather than to validate a click the user
        /// already asked for pass permissiveOnFailure: false — for them an unresolvable
        /// workflow must mean "no", or every card would look clickable.
        /// </summary>
        public static bool WorkflowAcceptsClick(GameObject card)
            => WorkflowAcceptsClick(card, permissiveOnFailure: true);

        /// <inheritdoc cref="WorkflowAcceptsClick(GameObject)"/>
        public static bool WorkflowAcceptsClick(GameObject card, bool permissiveOnFailure)
        {
            try
            {
                if (card == null) return false;
                if (!EnsureReflection()) return permissiveOnFailure;

                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc == null) return permissiveOnFailure;

                var workflow = GetCurrentWorkflow();
                if (workflow == null) return false;

                return WorkflowAcceptsClick(workflow, cdc);
            }
            catch (Exception ex)
            {
                Log.Warn("StackInteractionBridge", $"WorkflowAcceptsClick failed: {ex.Message}");
                return permissiveOnFailure;
            }
        }

        private static bool WorkflowAcceptsClick(object workflow, Component cdc)
        {
            if (workflow == null) return false;

            // CanClick's first parameter is IEntityView, which DuelScene_CDC implements —
            // match on the enum parameter and let the runtime type check the rest.
            foreach (var method in workflow.GetType().GetMethods(AllInstanceFlags))
            {
                if (method.Name != "CanClick" || method.ReturnType != typeof(bool)) continue;
                var ps = method.GetParameters();
                if (ps.Length != 2
                    || ps[1].ParameterType != _simpleInteractionEnum
                    || !ps[0].ParameterType.IsInstanceOfType(cdc)) continue;

                try
                {
                    if ((bool)method.Invoke(workflow, new object[] { cdc, _primaryValue }))
                        return true;
                }
                catch (Exception ex)
                {
                    Log.Warn("StackInteractionBridge",
                        $"CanClick threw on '{workflow.GetType().Name}': {ex.Message}");
                }
                break;
            }

            return AnyChildWorkflow(workflow, child => WorkflowAcceptsClick(child, cdc));
        }

        /// <summary>
        /// Runs the predicate over a parent workflow's ChildWorkflows, mirroring how the
        /// game's own workflow extensions fall through to a group workflow's children.
        /// </summary>
        private static bool AnyChildWorkflow(object workflow, Func<object, bool> predicate)
        {
            var childrenProp = workflow.GetType().GetProperty("ChildWorkflows", AllInstanceFlags);
            if (childrenProp?.GetValue(workflow) is IEnumerable children)
            {
                foreach (var child in children)
                {
                    if (predicate(child)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Identity token for the interaction the game is currently running
        /// (GameManager.CurrentInteraction, or null when idle). Callers that dispatch a
        /// sequence of clicks compare this between clicks and stop as soon as it changes —
        /// once the workflow that was accepting the clicks is gone, the next click would
        /// land in whatever replaced it.
        /// </summary>
        public static object GetCurrentWorkflow()
        {
            try
            {
                var gm = FindGameManager();
                return gm == null ? null : GetCurrentWorkflow(gm);
            }
            catch { return null; }
        }

        /// <summary>
        /// Type name of the interaction the game is currently running (e.g.
        /// "ActionsAvailableWorkflow", "SelectTargetsWorkflow", "PayCostWorkflow"), or null
        /// when idle. Lets callers tell "the player has priority" apart from "the game is
        /// waiting for something specific" without reflecting into the workflow itself.
        /// </summary>
        public static string GetCurrentWorkflowName() => GetCurrentWorkflow()?.GetType().Name;

        private static MonoBehaviour FindGameManager()
        {
            if (_cachedGameManager != null)
            {
                try { if (_cachedGameManager.gameObject != null) return _cachedGameManager; }
                catch { }
                _cachedGameManager = null;
            }

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                {
                    _cachedGameManager = mb;
                    return mb;
                }
            }
            return null;
        }

        private static object GetInteractionSystem(MonoBehaviour gm)
        {
            var prop = gm.GetType().GetProperty("InteractionSystem", AllInstanceFlags);
            return prop?.GetValue(gm);
        }

        private static object GetCurrentWorkflow(MonoBehaviour gm)
        {
            // GameManager.CurrentInteraction => WorkflowController?.CurrentWorkflow
            var ciProp = gm.GetType().GetProperty("CurrentInteraction", AllInstanceFlags);
            return ciProp?.GetValue(gm);
        }

        /// <summary>
        /// Mirrors WorkflowBase.CanClickStack extension: true if the workflow itself
        /// implements IClickableWorkflow.CanClickStack and accepts, OR any child workflow
        /// does (for parent/group workflows).
        /// </summary>
        private static bool WorkflowAcceptsStackClick(object workflow)
        {
            if (workflow == null) return false;
            Type type = workflow.GetType();

            var canMethod = type.GetMethod("CanClickStack",
                AllInstanceFlags, null,
                new[] { _cdcViewType, _simpleInteractionEnum }, null);
            if (canMethod != null && canMethod.ReturnType == typeof(bool))
            {
                try
                {
                    bool ok = (bool)canMethod.Invoke(workflow,
                        new object[] { _proxyView, _primaryValue });
                    if (ok) return true;
                }
                catch (Exception ex)
                {
                    Log.Warn("StackInteractionBridge",
                        $"CanClickStack threw on '{type.Name}': {ex.Message}");
                }
            }

            return AnyChildWorkflow(workflow, WorkflowAcceptsStackClick);
        }

        private static bool EnsureReflection()
        {
            if (_cdcViewType == null)
            {
                _cdcViewType = FindType("CdcStackCounterView");
                if (_cdcViewType == null) return false;
            }
            if (_parentIdField == null)
            {
                _parentIdField = _cdcViewType.GetField("parentInstanceId",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_parentIdField == null) return false;
            }
            if (_simpleInteractionEnum == null)
            {
                _simpleInteractionEnum = FindType("SimpleInteractionType");
                if (_simpleInteractionEnum == null) return false;
                _primaryValue = Enum.Parse(_simpleInteractionEnum, "Primary");
            }
            if (_onStackClickedMethod == null)
            {
                var gisType = FindType("GameInteractionSystem");
                if (gisType == null) return false;
                _onStackClickedMethod = gisType.GetMethod("OnStackClicked",
                    new[] { _cdcViewType, _simpleInteractionEnum });
                if (_onStackClickedMethod == null) return false;
            }
            return true;
        }

        private static bool EnsureProxyView()
        {
            if (_proxyView != null && _proxyGo != null) return true;
            _proxyGo = new GameObject("AA_StackClickProxy");
            _proxyGo.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_proxyGo);
            _proxyView = _proxyGo.AddComponent(_cdcViewType) as MonoBehaviour;
            return _proxyView != null;
        }
    }
}
