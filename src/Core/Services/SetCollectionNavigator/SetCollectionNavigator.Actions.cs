using System;
using System.Collections.Generic;
using UnityEngine;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class SetCollectionNavigator
    {
        #region Action State

        private enum ActionKind { DeckBuilder, BuyPacks }

        private struct SetAction
        {
            public string Label;
            public ActionKind Kind;
        }

        private readonly List<SetAction> _actions = new List<SetAction>();
        private int _actionIndex;

        #endregion

        #region Level Entry

        private void EnterActionsLevel()
        {
            if (_setIndex < 0 || _setIndex >= _sets.Count) return;

            _actions.Clear();
            if (H?.CollectionButtonClicked != null)
                _actions.Add(new SetAction { Label = Strings.SetCollectionActionDeckBuilder, Kind = ActionKind.DeckBuilder });
            if (H?.MoveToStore != null)
                _actions.Add(new SetAction { Label = Strings.SetCollectionActionBuyPacks, Kind = ActionKind.BuyPacks });

            if (_actions.Count == 0)
            {
                _announcer.AnnounceInterrupt(Strings.NoItemsFound);
                return;
            }

            _navLevel = NavLevel.Actions;
            _actionIndex = 0;

            _announcer.AnnounceInterrupt(Strings.SetCollectionActionsOpened(_sets[_setIndex].Name, _actions.Count));
            AnnounceCurrentAction();
        }

        private void ReturnToSetsLevel()
        {
            _navLevel = NavLevel.Sets;
            _actions.Clear();
            AnnounceCurrentSet();
        }

        #endregion

        #region Action Input

        private void HandleActionsInput()
        {
            if (_actions.Count == 0)
            {
                ReturnToSetsLevel();
                return;
            }

            if (_holdRepeater.Check(KeyCode.UpArrow, () => MoveAction(-1))) return;
            if (_holdRepeater.Check(KeyCode.DownArrow, () => MoveAction(1))) return;

            if (Input.GetKeyDown(KeyCode.Home))
            {
                _actionIndex = 0;
                AnnounceCurrentAction();
                return;
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                _actionIndex = _actions.Count - 1;
                AnnounceCurrentAction();
                return;
            }

            if (InputManager.GetEnterAndConsume() || InputManager.GetKeyDownAndConsume(KeyCode.Space))
            {
                ActivateCurrentAction();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputManager.ConsumeKey(KeyCode.Backspace);
                ReturnToSetsLevel();
                return;
            }
        }

        private bool MoveAction(int direction)
        {
            int newIndex = _actionIndex + direction;
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return false;
            }
            if (newIndex >= _actions.Count)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return false;
            }

            _actionIndex = newIndex;
            AnnounceCurrentAction();
            return true;
        }

        private void AnnounceCurrentAction()
        {
            if (_actionIndex < 0 || _actionIndex >= _actions.Count) return;
            _announcer.AnnounceInterrupt(Strings.ItemPositionOf(
                _actionIndex + 1, _actions.Count, _actions[_actionIndex].Label));
        }

        private void ActivateCurrentAction()
        {
            if (_actionIndex < 0 || _actionIndex >= _actions.Count) return;
            if (_setIndex < 0 || _setIndex >= _sets.Count) return;

            var action = _actions[_actionIndex];
            var set = _sets[_setIndex];

            // Both actions read the view's own _selectedBadge. We only sync it here rather than
            // on every arrow press, because selecting fires the game's meter rebuild and a
            // telemetry event each time.
            if (!SyncSelectedBadge(set))
            {
                _announcer.AnnounceInterrupt(Strings.SetCollectionActionFailed);
                return;
            }

            _announcer.Announce(Strings.Activating(action.Label));

            try
            {
                if (action.Kind == ActionKind.DeckBuilder)
                    H.CollectionButtonClicked.Invoke(_screenView, null);
                else
                    H.MoveToStore.Invoke(_screenView, null);

                Log.Msg("{NavigatorId}", $"Invoked {action.Kind} for {set.Code}");
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"{action.Kind} failed: {ex.Message}");
                _announcer.AnnounceInterrupt(Strings.SetCollectionActionFailed);
            }
        }

        /// <summary>
        /// Points the view's selection at our set. Returns false when the game refused to
        /// select it, so an action can never silently run against the wrong (or no) set.
        /// </summary>
        private bool SyncSelectedBadge(SetEntry set)
        {
            if (H?.SelectBadgeByExpansionCode == null || set.ExpansionCode == null) return false;

            try
            {
                H.SelectBadgeByExpansionCode.Invoke(_screenView, new[] { set.ExpansionCode });
            }
            catch (Exception ex)
            {
                Log.Warn("{NavigatorId}", $"SelectBadgeByExpansionCode failed: {ex.Message}");
                return false;
            }

            if (H.SelectedBadge == null) return true;   // cannot verify; assume the call took

            try
            {
                var selected = H.SelectedBadge.GetValue(_screenView) as MonoBehaviour;
                return selected != null && selected == set.Badge;
            }
            catch
            {
                return true;
            }
        }

        #endregion
    }
}
