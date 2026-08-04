using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using AccessibleArena.Core.Constants;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Answers "did that card actually get played?" from the game's own state instead of
    /// assuming the simulated clicks worked.
    ///
    /// Two questions, both read straight off the engine:
    ///
    /// 1. <see cref="GetHolderType"/> — which card holder the game currently has the card in.
    ///    This is the same value the game's own click handling switches on
    ///    (<c>DuelScene_CDC.CurrentCardHolder.CardHolderType</c>). A card that was cast has
    ///    left Hand for Stack (or Battlefield, for a land), so a changed holder is proof the
    ///    play went through.
    ///
    /// 2. <see cref="IsAwaitingPlayerInput"/> — whether the game opened something that is now
    ///    waiting on the player. A play can legitimately succeed without the card moving yet:
    ///    <list type="bullet">
    ///      <item>a targeting / X-cost / action-source variant opened on the current workflow</item>
    ///      <item>a mana color picker opened (<c>UIManager.ManaColorSelector.IsOpen</c>)</item>
    ///      <item>a browser opened — this covers BOTH the "are you sure?" play-warning
    ///            confirmation and the modal "which action?" chooser, because
    ///            <c>ClientSideInteraction</c> implements both as browsers</item>
    ///    </list>
    ///    Treating these as success is what keeps the mod from announcing "could not play"
    ///    over the top of a prompt the user is about to answer.
    /// </summary>
    public static class CardPlayVerifier
    {
        #region Reflection Handles

        private sealed class GameManagerHandles
        {
            public PropertyInfo BrowserManager;
            public PropertyInfo UIManager;
            public PropertyInfo CurrentInteraction;
        }

        private static readonly ReflectionCache<GameManagerHandles> _gameManagerCache =
            new ReflectionCache<GameManagerHandles>(
                builder: t => new GameManagerHandles
                {
                    BrowserManager = t.GetProperty("BrowserManager", PublicInstance),
                    UIManager = t.GetProperty("UIManager", PublicInstance),
                    CurrentInteraction = t.GetProperty("CurrentInteraction", PublicInstance),
                },
                validator: h => h.BrowserManager != null && h.UIManager != null && h.CurrentInteraction != null,
                logTag: "CardPlayVerifier",
                logSubject: "GameManager");

        private static MonoBehaviour _gameManager;

        // Resolved lazily from the runtime instances, since their declaring types are game types.
        private static PropertyInfo _currentCardHolderProp;
        private static PropertyInfo _cardHolderTypeProp;
        private static PropertyInfo _isAnyBrowserOpenProp;
        private static PropertyInfo _manaColorSelectorProp;
        private static PropertyInfo _selectorIsOpenProp;

        // Workflow type varies (ActionsAvailableWorkflow, SelectCardsWorkflow, ...), so the
        // _currentVariant handle is cached per concrete type.
        private static readonly Dictionary<Type, FieldInfo> _variantFieldByWorkflow = new Dictionary<Type, FieldInfo>();

        /// <summary>Clears all cached reflection data and the GameManager reference. Call on scene change.</summary>
        public static void ClearCache()
        {
            _gameManagerCache.Clear();
            _gameManager = null;
            _currentCardHolderProp = null;
            _cardHolderTypeProp = null;
            _isAnyBrowserOpenProp = null;
            _manaColorSelectorProp = null;
            _selectorIsOpenProp = null;
            _variantFieldByWorkflow.Clear();
        }

        #endregion

        #region Holder Type

        /// <summary>
        /// Gets the <c>CardHolderType</c> the game currently has this card in, as an int
        /// (compare against <see cref="CardHolderTypes"/>). Returns null when the card has no
        /// holder — a destroyed/recycled CDC or a non-duel card view.
        ///
        /// Reads <c>CurrentCardHolder.CardHolderType</c> rather than the CDC's own
        /// <c>HolderType</c> property, because <c>HolderType</c> can be masked by
        /// <c>HolderTypeOverride</c> while the game's click handling always looks at the
        /// holder itself.
        /// </summary>
        public static int? GetHolderType(GameObject card)
        {
            if (card == null) return null;

            try
            {
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc == null) return null;

                if (_currentCardHolderProp == null)
                {
                    _currentCardHolderProp = ReflectionWalk.FindProperty(
                        cdc.GetType(), "CurrentCardHolder", PublicInstance);
                    if (_currentCardHolderProp == null)
                    {
                        Log.Warn("CardPlayVerifier", "DuelScene_CDC.CurrentCardHolder not found");
                        return null;
                    }
                }

                var holder = _currentCardHolderProp.GetValue(cdc);
                if (holder == null) return null;

                if (_cardHolderTypeProp == null)
                {
                    _cardHolderTypeProp = ReflectionWalk.FindProperty(
                        holder.GetType(), "CardHolderType", PublicInstance);
                    if (_cardHolderTypeProp == null)
                    {
                        Log.Warn("CardPlayVerifier", "ICardHolder.CardHolderType not found");
                        return null;
                    }
                }

                var value = _cardHolderTypeProp.GetValue(holder);
                return value == null ? (int?)null : Convert.ToInt32(value);
            }
            catch (Exception ex)
            {
                Log.Warn("CardPlayVerifier", $"GetHolderType failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>Human-readable holder name for logs.</summary>
        public static string DescribeHolder(int? holderType)
        {
            switch (holderType)
            {
                case null: return "none";
                case CardHolderTypes.Library: return "Library";
                case CardHolderTypes.Hand: return "Hand";
                case CardHolderTypes.Battlefield: return "Battlefield";
                case CardHolderTypes.Graveyard: return "Graveyard";
                case CardHolderTypes.Exile: return "Exile";
                case CardHolderTypes.Stack: return "Stack";
                case CardHolderTypes.Command: return "Command";
                default: return holderType.Value.ToString();
            }
        }

        #endregion

        #region Pending Interaction

        /// <summary>
        /// True when the game is currently waiting on the player because of something a card
        /// click opened: a workflow variant (targeting, X cost, action source), the mana color
        /// picker, or a browser (play-warning confirmation or modal action chooser).
        /// </summary>
        public static bool IsAwaitingPlayerInput()
        {
            var gm = GetGameManager();
            if (gm == null) return false;

            try
            {
                var handles = _gameManagerCache.Handles;

                if (IsBrowserOpen(handles.BrowserManager.GetValue(gm))) return true;
                if (IsColorPickerOpen(handles.UIManager.GetValue(gm))) return true;
                if (HasOpenWorkflowVariant(handles.CurrentInteraction.GetValue(gm))) return true;
            }
            catch (Exception ex)
            {
                Log.Warn("CardPlayVerifier", $"IsAwaitingPlayerInput failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Covers both the play-warning confirmation ("are you sure?") and the modal action
        /// chooser — <c>ClientSideInteraction</c> opens both through the BrowserManager.
        /// </summary>
        private static bool IsBrowserOpen(object browserManager)
        {
            if (browserManager == null) return false;

            if (_isAnyBrowserOpenProp == null)
            {
                _isAnyBrowserOpenProp = ReflectionWalk.FindProperty(
                    browserManager.GetType(), "IsAnyBrowserOpen", PublicInstance);
                if (_isAnyBrowserOpenProp == null) return false;
            }

            return _isAnyBrowserOpenProp.GetValue(browserManager) is bool open && open;
        }

        private static bool IsColorPickerOpen(object uiManager)
        {
            if (uiManager == null) return false;

            if (_manaColorSelectorProp == null)
            {
                _manaColorSelectorProp = ReflectionWalk.FindProperty(
                    uiManager.GetType(), "ManaColorSelector", PublicInstance);
                if (_manaColorSelectorProp == null) return false;
            }

            var selector = _manaColorSelectorProp.GetValue(uiManager);
            if (selector == null) return false;

            if (_selectorIsOpenProp == null)
            {
                _selectorIsOpenProp = ReflectionWalk.FindProperty(
                    selector.GetType(), "IsOpen", PublicInstance);
                if (_selectorIsOpenProp == null) return false;
            }

            return _selectorIsOpenProp.GetValue(selector) is bool open && open;
        }

        /// <summary>
        /// A non-null <c>_currentVariant</c> means the workflow opened a sub-interaction
        /// (target selection, X value, which-source-to-use) and is waiting for input.
        /// </summary>
        private static bool HasOpenWorkflowVariant(object workflow)
        {
            if (workflow == null) return false;

            var type = workflow.GetType();
            if (!_variantFieldByWorkflow.TryGetValue(type, out var field))
            {
                field = ReflectionWalk.FindField(type, "_currentVariant", PrivateInstance);
                _variantFieldByWorkflow[type] = field;
            }

            return field != null && field.GetValue(workflow) != null;
        }

        #endregion

        #region GameManager

        private static MonoBehaviour GetGameManager()
        {
            if (_gameManager != null) return _gameManager;

            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == GameTypeNames.GameManager)
                {
                    _gameManager = mb;
                    break;
                }
            }

            if (_gameManager == null) return null;

            return _gameManagerCache.EnsureInitialized(_gameManager.GetType()) ? _gameManager : null;
        }

        #endregion
    }
}
