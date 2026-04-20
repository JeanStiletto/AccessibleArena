using UnityEngine;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services.ElementGrouping;
using System.Collections.Generic;
using System.Linq;

namespace AccessibleArena.Core.Services
{
    public partial class GeneralMenuNavigator
    {
        // Booster carousel state - treated as single carousel element with left/right navigation
        private List<GameObject> _boosterPackHitboxes = new List<GameObject>();
        private int _boosterCarouselIndex = 0;
        private bool _isBoosterCarouselActive = false;

        /// <summary>
        /// Check if an element is inside a CarouselBooster parent.
        /// </summary>
        private bool IsInsideCarouselBooster(GameObject obj)
        {
            if (obj == null) return false;

            Transform current = obj.transform.parent;
            int maxLevels = 6;

            while (current != null && maxLevels > 0)
            {
                if (current.name.Contains("CarouselBooster"))
                    return true;
                current = current.parent;
                maxLevels--;
            }

            return false;
        }

        /// <summary>
        /// Add a single carousel element representing all booster packs.
        /// Uses the current index to show the selected pack name.
        /// </summary>
        private void AddBoosterCarouselElement()
        {
            if (_boosterPackHitboxes.Count == 0) return;

            // Sort packs by X position (left to right)
            _boosterPackHitboxes = _boosterPackHitboxes
                .OrderBy(p => p.transform.position.x)
                .ToList();

            // Clamp index to valid range
            if (_boosterCarouselIndex >= _boosterPackHitboxes.Count)
                _boosterCarouselIndex = 0;
            if (_boosterCarouselIndex < 0)
                _boosterCarouselIndex = _boosterPackHitboxes.Count - 1;

            // Get the current pack and its name
            var currentPack = _boosterPackHitboxes[_boosterCarouselIndex];
            string packName = UITextExtractor.GetText(currentPack);
            if (string.IsNullOrEmpty(packName))
                packName = "Pack";

            // Build the carousel label with position info
            string pos = Strings.PositionOf(_boosterCarouselIndex + 1, _boosterPackHitboxes.Count);
            string label = $"{packName}" + (pos != "" ? $", {pos}" : "") + ", use left and right arrows";

            // Add as navigable element with carousel info
            var carouselInfo = new CarouselInfo
            {
                HasArrowNavigation = true,
                PreviousControl = null, // We handle navigation ourselves
                NextControl = null
            };

            AddElement(currentPack, label, carouselInfo);
            LogDebug($"[{NavigatorId}] Added booster carousel: {label}");
        }

        /// <summary>
        /// Navigate the booster carousel (left/right).
        /// Clicks the target pack to center it in the carousel.
        /// </summary>
        /// <param name="isNext">True for right/next, false for left/previous</param>
        /// <returns>True if navigation was handled</returns>
        private bool HandleBoosterCarouselNavigation(bool isNext)
        {
            if (!_isBoosterCarouselActive || _boosterPackHitboxes.Count == 0)
                return false;

            // Calculate new index
            int newIndex = _boosterCarouselIndex + (isNext ? 1 : -1);

            // Bounds check
            if (newIndex < 0)
            {
                _announcer.AnnounceVerbose(Models.Strings.FirstPack, Models.AnnouncementPriority.Normal);
                return true;
            }
            if (newIndex >= _boosterPackHitboxes.Count)
            {
                _announcer.AnnounceVerbose(Models.Strings.LastPack, Models.AnnouncementPriority.Normal);
                return true;
            }

            // Get the old pack before updating index
            var oldPack = _boosterPackHitboxes[_boosterCarouselIndex];

            // Update index
            _boosterCarouselIndex = newIndex;

            // Get the new pack
            var targetPack = _boosterPackHitboxes[_boosterCarouselIndex];

            // Send PointerExit to old pack to stop its music/effects
            UIActivator.SimulatePointerExit(oldPack);

            // Click the new pack to center it (game's own centering behavior)
            UIActivator.Activate(targetPack);

            // Get pack name and announce
            string packName = UITextExtractor.GetText(targetPack);
            if (string.IsNullOrEmpty(packName))
                packName = "Pack";

            string pos = Strings.PositionOf(_boosterCarouselIndex + 1, _boosterPackHitboxes.Count);
            string announcement = packName + (pos != "" ? $", {pos}" : "");
            _announcer.Announce(announcement, Models.AnnouncementPriority.High);

            // Update the element label
            UpdateBoosterCarouselElement();

            // Rescan so buttons that appear/disappear based on the selected pack
            // (e.g., "Open Packs") become navigable via arrow up/down
            _suppressRescanAnnouncement = true;
            TriggerRescan();

            return true;
        }

        /// <summary>
        /// Update the booster carousel element label after navigation.
        /// </summary>
        private void UpdateBoosterCarouselElement()
        {
            if (_boosterPackHitboxes.Count == 0) return;

            var currentPack = _boosterPackHitboxes[_boosterCarouselIndex];
            string packName = UITextExtractor.GetText(currentPack);
            if (string.IsNullOrEmpty(packName))
                packName = "Pack";

            string pos = Strings.PositionOf(_boosterCarouselIndex + 1, _boosterPackHitboxes.Count);
            string label = $"{packName}" + (pos != "" ? $", {pos}" : "") + ", use left and right arrows";

            // Find and update the carousel element in our list
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_boosterPackHitboxes.Contains(_elements[i].GameObject))
                {
                    var element = _elements[i];
                    element.GameObject = currentPack;
                    element.Label = label;
                    _elements[i] = element;
                    break;
                }
            }
        }

        /// <summary>
        /// Override carousel arrow handling to support grouped navigation and booster carousel.
        /// In grouped navigation mode, _currentIndex may be out of sync with GroupedNavigator's
        /// current element. We sync it here before delegating to base for carousel/stepper handling.
        /// </summary>
        protected override bool HandleCarouselArrow(bool isNext)
        {
            // In popup mode, elements are in _elements directly (not via GroupedNavigator)
            if (IsInPopupMode)
                return base.HandleCarouselArrow(isNext);

            // When grouped navigation is active, sync _currentIndex with GroupedNavigator
            // so base.HandleCarouselArrow reads the correct element's carousel info
            if (_groupedNavigationEnabled && _groupedNavigator.IsActive)
            {
                var currentElement = _groupedNavigator.CurrentElement;
                GameObject currentObj = null;

                if (currentElement != null)
                {
                    currentObj = currentElement.Value.GameObject;
                }
                else if (_groupedNavigator.IsCurrentGroupStandalone)
                {
                    // Standalone groups are navigated at GroupList level, so CurrentElement is null.
                    // Get the element directly from the standalone group.
                    currentObj = _groupedNavigator.GetStandaloneElement();
                }

                if (currentObj == null) return false;

                // Booster carousel special handling
                if (_isBoosterCarouselActive && _boosterPackHitboxes.Count > 0 &&
                    _boosterPackHitboxes.Contains(currentObj))
                {
                    return HandleBoosterCarouselNavigation(isNext);
                }

                // Find the matching element in _elements and sync _currentIndex
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].GameObject == currentObj)
                    {
                        _currentIndex = i;
                        return base.HandleCarouselArrow(isNext);
                    }
                }

                return false; // Element not found in flat list
            }

            // Flat navigation: booster check then base
            if (_isBoosterCarouselActive && _boosterPackHitboxes.Count > 0 && IsValidIndex)
            {
                if (_boosterPackHitboxes.Contains(_elements[_currentIndex].GameObject))
                {
                    return HandleBoosterCarouselNavigation(isNext);
                }
            }

            return base.HandleCarouselArrow(isNext);
        }
    }
}
