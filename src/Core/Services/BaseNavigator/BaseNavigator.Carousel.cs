using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MelonLoader;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    public partial class BaseNavigator
    {
        // Delayed stepper value announcement (game needs a frame to update value after button click)
        private float _stepperAnnounceDelay;
        private const float StepperAnnounceDelaySeconds = 0.1f;

        // Delayed re-scan after spinner value change (game needs time to update UI visibility)
        private float _spinnerRescanDelay;
        private const float SpinnerRescanDelaySeconds = 0.5f;

        /// <summary>
        /// Handle left/right arrow keys for carousel/stepper/slider elements or attached actions.
        /// Returns true if the current element supports arrow navigation and the key was handled.
        /// </summary>
        protected virtual bool HandleCarouselArrow(bool isNext)
        {
            if (!IsValidIndex)
                return false;

            var element = _elements[_currentIndex];

            // Check for attached actions first (e.g., deck actions: Delete, Edit, Export)
            if (element.AttachedActions != null && element.AttachedActions.Count > 0)
            {
                return HandleAttachedActionArrow(element, isNext);
            }

            var info = element.Carousel;
            if (!info.HasArrowNavigation)
                return false;

            // Handle slider elements directly
            if (info.SliderComponent != null)
            {
                return HandleSliderArrow(info.SliderComponent, isNext);
            }

            // Handle action-based steppers (e.g., popup craft count via reflection)
            if (info.OnIncrement != null || info.OnDecrement != null)
            {
                var action = isNext ? info.OnIncrement : info.OnDecrement;
                if (action != null)
                {
                    action();
                    _stepperAnnounceDelay = StepperAnnounceDelaySeconds;
                }
                return true;
            }

            // Handle carousel/stepper elements via control buttons
            GameObject control = isNext ? info.NextControl : info.PreviousControl;
            if (control == null || !control.activeInHierarchy)
            {
                _announcer.Announce(isNext ? Strings.NoNextItem : Strings.NoPreviousItem, AnnouncementPriority.Normal);
                return true;
            }

            // Activate the nav control (carousel nav button or stepper increment/decrement)
            Log.Msg("{NavigatorId}", $"Arrow nav {(isNext ? "next/increment" : "previous/decrement")}: {control.name}");
            if (info.UseHoverActivation)
            {
                UIActivator.SimulateHover(control, isNext);
                // Schedule delayed re-scan - spinner value change may show/hide UI elements
                _spinnerRescanDelay = SpinnerRescanDelaySeconds;
            }
            else
            {
                UIActivator.Activate(control);
            }

            // Schedule delayed announcement - game needs a frame to update the value
            _stepperAnnounceDelay = StepperAnnounceDelaySeconds;

            return true;
        }

        /// <summary>
        /// Handle slider arrow keys by letting Unity's built-in Slider.OnMove do the work (10% steps).
        /// We just ensure the slider is selected and schedule a delayed announcement.
        /// </summary>
        private bool HandleSliderArrow(Slider slider, bool isNext)
        {
            if (slider == null || !slider.interactable)
                return false;

            // Ensure slider is selected in Unity's EventSystem so its built-in
            // OnMove handles the value change (10% steps per arrow key press)
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != slider.gameObject)
            {
                eventSystem.SetSelectedGameObject(slider.gameObject);
            }

            // Schedule delayed announcement — Unity needs a frame to process the value change
            _stepperAnnounceDelay = StepperAnnounceDelaySeconds;

            return true;
        }

        /// <summary>
        /// Handle left/right arrow keys for cycling through attached actions.
        /// Action index 0 = the element itself, 1+ = attached actions.
        /// </summary>
        private bool HandleAttachedActionArrow(NavigableElement element, bool isNext)
        {
            int actionCount = element.AttachedActions.Count;
            int totalOptions = 1 + actionCount; // Element itself + attached actions

            int newActionIndex = _currentActionIndex + (isNext ? 1 : -1);

            // Clamp to valid range (no wrapping)
            if (newActionIndex < 0)
            {
                _announcer.AnnounceVerbose(Strings.BeginningOfList, AnnouncementPriority.Normal);
                return true;
            }
            if (newActionIndex >= totalOptions)
            {
                _announcer.AnnounceVerbose(Strings.EndOfList, AnnouncementPriority.Normal);
                return true;
            }

            _currentActionIndex = newActionIndex;

            // Announce current action
            string announcement;
            if (_currentActionIndex == 0)
            {
                // Back to the element itself
                announcement = element.Label;
            }
            else
            {
                // Attached action
                var action = element.AttachedActions[_currentActionIndex - 1];
                announcement = action.Label;
            }

            Log.Msg("{NavigatorId}", $"Action cycle: index {_currentActionIndex}, announcing: {announcement}");
            _announcer.AnnounceInterrupt(announcement);
            return true;
        }

        /// <summary>
        /// Quiet re-scan after a spinner value change. The game may show/hide UI elements
        /// depending on the selected option (e.g. tournament vs challenge match types).
        /// Preserves focus on the current stepper element if it still exists.
        /// </summary>
        protected virtual void RescanAfterSpinnerChange()
        {
            if (!_isActive || !IsValidIndex) return;

            // Remember what we're focused on
            var currentObj = _elements[_currentIndex].GameObject;
            int oldCount = _elements.Count;

            // Re-discover elements
            _elements.Clear();
            _currentIndex = -1;
            DiscoverElements();

            if (_elements.Count == 0) return;

            // Try to restore focus to the same element
            if (currentObj != null)
            {
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == currentObj)
                    {
                        _currentIndex = i;
                        break;
                    }
                }
            }

            if (_currentIndex < 0)
                _currentIndex = 0;

            // Only announce if element count changed
            if (_elements.Count != oldCount)
            {
                Log.Msg("{NavigatorId}", $"Spinner rescan: {oldCount} -> {_elements.Count} elements");
                string posAnnouncement = Strings.ItemPositionOf(_currentIndex + 1, _elements.Count, _elements[_currentIndex].Label);
                _announcer.Announce(posAnnouncement, AnnouncementPriority.Normal);
            }
        }

        /// <summary>
        /// Announce the current stepper/carousel value after a delay.
        /// Called from Update() when the delay expires.
        /// </summary>
        private void AnnounceStepperValue()
        {
            if (!IsValidIndex)
                return;

            // For elements with ReadLabel (e.g., popup craft count), re-read directly
            var carousel = _elements[_currentIndex].Carousel;
            if (carousel.ReadLabel != null)
            {
                string newLabel = carousel.ReadLabel();
                if (!string.IsNullOrEmpty(newLabel))
                {
                    var updated = _elements[_currentIndex];
                    updated.Label = newLabel;
                    _elements[_currentIndex] = updated;
                    Log.Msg("{NavigatorId}", $"Stepper value (ReadLabel): {newLabel}");
                    _announcer.AnnounceInterrupt(newLabel);
                }
                return;
            }

            var currentElement = _elements[_currentIndex].GameObject;
            if (currentElement != null)
            {
                // Re-classify to get the updated label with new value
                var classification = UIElementClassifier.Classify(currentElement);

                // Update cached label and role in our element list
                var updatedElement = _elements[_currentIndex];
                updatedElement.Label = BuildElementLabel(classification);
                updatedElement.Role = classification.Role;
                _elements[_currentIndex] = updatedElement;

                // For sliders, announce just the percent value
                if (classification.Role == UIElementClassifier.ElementRole.Slider && classification.SliderComponent != null)
                {
                    var slider = classification.SliderComponent;
                    float range = slider.maxValue - slider.minValue;
                    int percent = range > 0 ? Mathf.RoundToInt((slider.value - slider.minValue) / range * 100) : 0;
                    Log.Msg("{NavigatorId}", $"Slider value: {percent}%");
                    _announcer.AnnounceInterrupt(Strings.Percent(percent));
                }
                else
                {
                    Log.Msg("{NavigatorId}", $"Stepper value updated: {classification.Label}");
                    _announcer.Announce(classification.Label, AnnouncementPriority.High);
                }
            }
        }
    }
}
