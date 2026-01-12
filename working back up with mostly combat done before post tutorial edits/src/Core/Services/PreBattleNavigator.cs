using UnityEngine;
using UnityEngine.UI;
using MelonLoader;
using MTGAAccessibility.Core.Interfaces;
using MTGAAccessibility.Core.Models;

namespace MTGAAccessibility.Core.Services
{
    /// <summary>
    /// Navigator for the pre-game VS screen shown before a duel starts (PreGameScene).
    /// Handles PromptButton_Primary/Secondary which use StyledButton (pointer events).
    /// This handles the "Continue to battle" / "Cancel" prompt before the actual duel.
    /// </summary>
    public class PreBattleNavigator : BaseNavigator
    {
        private bool _isWatching;
        private bool _waitingForTransition;
        private float _activationTime;
        private const float TRANSITION_WAIT = 2.0f;

        public override string NavigatorId => "PreBattle";
        public override string ScreenName => "Pre-game screen";
        public override int Priority => 80;

        // Don't accept space - only Enter for battle confirmation
        protected override bool AcceptSpaceKey => false;

        public PreBattleNavigator(IAnnouncementService announcer) : base(announcer) { }

        /// <summary>
        /// Called by MTGAAccessibilityMod when DuelScene loads.
        /// Starts watching for prompt buttons to appear.
        /// </summary>
        public void OnDuelSceneLoaded()
        {
            MelonLogger.Msg($"[{NavigatorId}] DuelScene loaded - starting to watch for buttons");
            _isWatching = true;
            _waitingForTransition = false;
            _activationTime = 0;
        }

        public override void OnSceneChanged(string sceneName)
        {
            // Stop watching when we leave DuelScene
            if (sceneName != "DuelScene")
            {
                _isWatching = false;
            }

            base.OnSceneChanged(sceneName);
            _waitingForTransition = false;
        }

        protected override bool DetectScreen()
        {
            // Only detect if we're watching (DuelScene loaded)
            if (!_isWatching) return false;

            // Detect by presence of prompt buttons (Continue/Cancel)
            return HasPromptButtons();
        }

        protected override void DiscoverElements()
        {
            // Find prompt buttons from all Selectables
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;

                string name = selectable.gameObject.name;

                if (name.Contains("PromptButton_Primary"))
                {
                    string label = GetButtonText(selectable.gameObject, "Continue to battle");
                    // Insert at beginning so primary is first
                    _elements.Insert(0, selectable.gameObject);
                    _labels.Insert(0, $"{label}, button");
                    MelonLogger.Msg($"[{NavigatorId}] Found primary button: {label}");
                }
                else if (name.Contains("PromptButton_Secondary"))
                {
                    string label = GetButtonText(selectable.gameObject, "Cancel");
                    AddElement(selectable.gameObject, $"{label}, button");
                    MelonLogger.Msg($"[{NavigatorId}] Found secondary button: {label}");
                }
                else if (name.Contains("PromptButton"))
                {
                    // Handle other prompt buttons (Tertiary, Options, etc.)
                    string label = GetButtonText(selectable.gameObject, "Option");
                    AddElement(selectable.gameObject, $"{label}, button");
                    MelonLogger.Msg($"[{NavigatorId}] Found other prompt button '{name}': {label}");
                }
            }

            // Find settings button
            var settingsButton = GameObject.Find("Nav_Settings");
            if (settingsButton != null && settingsButton.activeInHierarchy)
            {
                AddElement(settingsButton, "Settings, button");
            }
        }

        public override void Update()
        {
            // Handle transition waiting state
            if (_waitingForTransition)
            {
                HandleTransitionWait();
                return;
            }

            // Check if prompt buttons disappeared while active
            if (_isActive && !HasPromptButtons())
            {
                MelonLogger.Msg($"[{NavigatorId}] Prompt buttons gone - deactivating");
                FullDeactivate();
                return;
            }

            // Normal update
            base.Update();
        }

        private void HandleTransitionWait()
        {
            float elapsed = Time.time - _activationTime;

            // Still waiting for scene change
            if (elapsed < TRANSITION_WAIT) return;

            // Timeout - no scene change, button didn't work
            // Re-enable navigation silently by re-discovering elements without announcing
            MelonLogger.Msg($"[{NavigatorId}] Transition timeout - re-enabling navigation silently");
            _waitingForTransition = false;

            // Re-discover elements to avoid stale references
            _elements.Clear();
            _labels.Clear();
            DiscoverElements();

            if (_elements.Count > 0)
            {
                _isActive = true;
                _currentIndex = 0;
                MelonLogger.Msg($"[{NavigatorId}] Re-discovered {_elements.Count} elements");
            }
        }

        protected override bool OnElementActivated(int index, GameObject element)
        {
            // StyledButton responds to pointer events, not onClick
            MelonLogger.Msg($"[{NavigatorId}] Using pointer click for: {element.name}");
            UIActivator.SimulatePointerClick(element);
            _announcer.Announce("Activated", AnnouncementPriority.Normal);

            // Start waiting for transition
            _activationTime = Time.time;
            _waitingForTransition = true;
            _isActive = false;

            return true; // We handled it
        }

        private void FullDeactivate()
        {
            Deactivate();
            _isWatching = false;
            _waitingForTransition = false;
            _activationTime = 0;
        }

        private bool HasPromptButtons()
        {
            foreach (var selectable in GameObject.FindObjectsOfType<Selectable>())
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy)
                    continue;
                string name = selectable.gameObject.name;
                if (name.Contains("PromptButton_Primary") || name.Contains("PromptButton_Secondary"))
                    return true;
            }
            return false;
        }
    }
}
