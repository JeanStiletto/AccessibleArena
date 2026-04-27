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

        public static void ClearCache()
        {
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

        private static MonoBehaviour FindGameManager()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "GameManager")
                    return mb;
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

            var childrenProp = type.GetProperty("ChildWorkflows", AllInstanceFlags);
            if (childrenProp != null)
            {
                if (childrenProp.GetValue(workflow) is IEnumerable children)
                {
                    foreach (var child in children)
                    {
                        if (WorkflowAcceptsStackClick(child)) return true;
                    }
                }
            }
            return false;
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
