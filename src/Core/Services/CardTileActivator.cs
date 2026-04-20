using UnityEngine;
using System.Linq;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;

using AccessibleArena.Core.Utils;
namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Specialized activation utilities for collection cards, commander/companion/partner
    /// slot tiles, and deck entries in the deck builder.
    ///
    /// Scope: card TILES only (screens that show card art + name + quantity, not
    /// playable duel cards). For generic buttons/toggles/inputs, use
    /// <see cref="UIActivator"/>. For playing cards out of the duel hand, that
    /// lives on UIActivator (see UIActivator.PlayCard*) because it's keyed on
    /// hand-zone CDCs rather than collection tiles.
    /// </summary>
    public static class CardTileActivator
    {
        #region Constants

        private const int MaxDeckSearchDepth = 5;
        private const int MaxDeckViewSearchDepth = 6;
        private const string DeckViewTypeName = "DeckView";

        #endregion

        #region Reflection Cache

        // Cached reflection for reading Commanders filter state via Pantry
        private static System.Reflection.MethodInfo _pantryGetFilterProviderMethod;
        private static System.Reflection.PropertyInfo _filterProperty;
        private static System.Reflection.MethodInfo _isSetMethod;
        private static object _commandersEnumValue;
        private static bool _filterReflectionInit;

        #endregion

        #region Collection Card Activation

        /// <summary>
        /// Checks if an element is a collection card (PagesMetaCardView/MetaCardView in deck builder's PoolHolder).
        /// These cards need special handling because the display view is navigated but the
        /// actual clickable element is a child.
        /// </summary>
        public static bool IsCollectionCard(GameObject element)
        {
            if (element == null) return false;

            // Check if element has PagesMetaCardView or MetaCardView component
            foreach (var mb in element.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == T.PagesMetaCardView || typeName == T.MetaCardView)
                {
                    // Verify it's in PoolHolder (collection area) not deck area
                    Transform current = element.transform;
                    while (current != null)
                    {
                        if (current.name == "PoolHolder")
                            return true;
                        current = current.parent;
                    }
                }
            }

            // Also check by element name for cases where we navigate the view directly
            if (element.name.Contains(T.PagesMetaCardView) || element.name.Contains(T.MetaCardView))
            {
                Transform current = element.transform;
                while (current != null)
                {
                    if (current.name == "PoolHolder")
                        return true;
                    current = current.parent;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an element is inside a commander/partner/companion container.
        /// Used for empty slot detection (CustomButton - EmptySlot).
        /// </summary>
        public static bool IsInCommanderContainer(GameObject element)
        {
            if (element == null) return false;
            Transform current = element.transform.parent;
            while (current != null)
            {
                string name = current.name;
                if (name.Contains("CardTileCommander_CONTAINER") ||
                    name.Contains("CardTilePartner_CONTAINER") ||
                    name.Contains("CardTileCompanion_CONTAINER"))
                    return true;
                if (name.Contains("MainDeckContentCONTAINER"))
                    return false;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Reads the current state of the Commanders card filter via Pantry reflection.
        /// Returns true if ON, false if OFF, null if reflection fails.
        /// </summary>
        public static bool? IsCommandersFilterActive()
        {
            try
            {
                if (!_filterReflectionInit)
                {
                    _filterReflectionInit = true;
                    var pantryType = FindType("Wizards.Mtga.Pantry");
                    var filterProviderType = FindType("Core.Code.Decks.DeckBuilderCardFilterProvider");
                    var cardFilterTypeEnum = FindType("Core.Shared.Code.CardFilters.CardFilterType");
                    if (pantryType == null || filterProviderType == null || cardFilterTypeEnum == null)
                    {
                        Log.Activation("CardTileActivator", $"Filter reflection init failed: pantry={pantryType != null}, provider={filterProviderType != null}, enum={cardFilterTypeEnum != null}");
                        return null;
                    }
                    var getMethod = pantryType.GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (getMethod != null && getMethod.IsGenericMethod)
                        _pantryGetFilterProviderMethod = getMethod.MakeGenericMethod(filterProviderType);
                    _filterProperty = filterProviderType.GetProperty("Filter", PublicInstance);
                    // Find IsSet method on IReadOnlyCardFilter
                    if (_filterProperty != null)
                    {
                        var filterType = _filterProperty.PropertyType;
                        _isSetMethod = filterType.GetMethod("IsSet", PublicInstance);
                    }
                    // Get the Commanders enum value
                    _commandersEnumValue = System.Enum.Parse(cardFilterTypeEnum, "Commanders");
                    Log.Activation("CardTileActivator", $"Filter reflection init: getMethod={_pantryGetFilterProviderMethod != null}, filter={_filterProperty != null}, isSet={_isSetMethod != null}, commanders={_commandersEnumValue}");
                }
                if (_pantryGetFilterProviderMethod == null || _filterProperty == null || _isSetMethod == null || _commandersEnumValue == null)
                    return null;

                var provider = _pantryGetFilterProviderMethod.Invoke(null, null);
                if (provider == null) return null;
                var filter = _filterProperty.GetValue(provider, null);
                if (filter == null) return null;
                return (bool)_isSetMethod.Invoke(filter, new object[] { _commandersEnumValue });
            }
            catch (System.Exception ex)
            {
                Log.Activation("CardTileActivator", $"IsCommandersFilterActive failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if an element is a commander/companion/partner card tile in the Brawl deck builder.
        /// These are "CustomButton - Tile" elements inside CardTileCommander/Partner/Companion containers.
        /// </summary>
        public static bool IsCommanderSlotCard(GameObject element)
        {
            if (element == null) return false;
            if (element.name != "CustomButton - Tile") return false;

            Transform current = element.transform.parent;
            while (current != null)
            {
                string name = current.name;
                if (name.Contains("CardTileCommander_CONTAINER") ||
                    name.Contains("CardTilePartner_CONTAINER") ||
                    name.Contains("CardTileCompanion_CONTAINER"))
                    return true;
                if (name.Contains("MainDeckContentCONTAINER"))
                    return false;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Checks if an element is a deck list card (CustomButton - Tile in MainDeckContentCONTAINER).
        /// These cards need special handling to avoid double activation.
        /// </summary>
        public static bool IsDeckListCard(GameObject element)
        {
            if (element == null) return false;

            // Deck list cards are CustomButton - Tile elements inside MainDeckContentCONTAINER
            if (!element.name.Contains("CustomButton") && !element.name.Contains("Tile"))
                return false;

            // Check parent hierarchy for deck list container
            Transform current = element.transform;
            while (current != null)
            {
                string name = current.name;
                if (name.Contains("MainDeckContentCONTAINER") || name.Contains("MainDeck_MetaCardHolder"))
                    return true;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Tries to activate a collection card by simulating a left click.
        /// Left click adds to deck (if owned) or opens craft popup (if unowned).
        /// </summary>
        public static ActivationResult TryActivateCollectionCard(GameObject cardElement)
        {
            if (cardElement == null)
                return new ActivationResult(false, "Card element is null");

            Log.Activation("CardTileActivator", $"Attempting collection card activation for: {cardElement.name}");

            // Block the Enter KeyUp from reaching PopupManager, which would call
            // CardViewerController.OnEnter() and auto-trigger OnCraftClicked()
            InputManager.BlockNextEnterKeyUp = true;

            return UIActivator.SimulatePointerClick(cardElement);
        }

        #endregion

        #region Deck Selection

        /// <summary>
        /// Attempts to select a deck entry by invoking its OnDeckClick method.
        /// This is needed because MTGA's deck CustomButtons don't reliably respond
        /// to standard pointer events - the DeckView.OnDeckClick() method must be called directly.
        /// </summary>
        /// <param name="deckElement">The deck UI element (CustomButton on DeckView_Base/UI)</param>
        /// <returns>True if deck was selected successfully</returns>
        public static bool TrySelectDeck(GameObject deckElement)
        {
            if (deckElement == null) return false;

            Log.Activation("CardTileActivator", $"Attempting deck selection for: {deckElement.name}");

            // Find the DeckView component in parent hierarchy
            // Structure: DeckView_Base(Clone)/UI <- we click this
            // The DeckView component is on DeckView_Base(Clone)
            var deckView = FindDeckViewInParents(deckElement);
            if (deckView == null)
            {
                Log.Activation("CardTileActivator", "No DeckView component found in parents");
                return false;
            }

            Log.Activation("CardTileActivator", $"Found DeckView on: {deckView.gameObject.name}");

            // Invoke OnDeckClick() on the DeckView component
            var deckViewType = deckView.GetType();
            var onDeckClickMethod = deckViewType.GetMethod("OnDeckClick",
                AllInstanceFlags);

            if (onDeckClickMethod != null && onDeckClickMethod.GetParameters().Length == 0)
            {
                try
                {
                    Log.Activation("CardTileActivator", $"Invoking {deckViewType.Name}.OnDeckClick()");
                    onDeckClickMethod.Invoke(deckView, null);
                    return true;
                }
                catch (System.Exception ex)
                {
                    Log.Activation("CardTileActivator", $"Error invoking OnDeckClick: {ex.Message}");
                }
            }
            else
            {
                Log.Activation("CardTileActivator", "OnDeckClick method not found on DeckView");
            }

            return false;
        }

        /// <summary>
        /// Checks if an element is a deck entry (for specialized handling).
        /// TextBox elements are excluded - they're for renaming, not selecting.
        /// </summary>
        public static bool IsDeckEntry(GameObject element)
        {
            if (element == null) return false;

            // TextBox is for renaming the deck, not selecting it
            if (element.name == "TextBox")
                return false;

            // Check parent hierarchy for DeckView_Base
            Transform current = element.transform;
            int depth = 0;
            while (current != null && depth < MaxDeckSearchDepth)
            {
                if (current.name.Contains("DeckView_Base"))
                    return true;
                current = current.parent;
                depth++;
            }

            return false;
        }

        private static MonoBehaviour FindDeckViewInParents(GameObject element)
        {
            Transform current = element.transform;
            int depth = 0;

            while (current != null && depth < MaxDeckViewSearchDepth)
            {
                // Look for DeckView component
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == DeckViewTypeName)
                        return mb;
                }

                current = current.parent;
                depth++;
            }

            return null;
        }

        /// <summary>
        /// Check if a deck is currently selected.
        /// Compares the deck's DeckView with DeckViewSelector._selectedDeckView.
        /// </summary>
        /// <param name="deckElement">The deck UI element (CustomButton on DeckView_Base/UI)</param>
        /// <returns>True if the deck is selected, false otherwise or if not a deck</returns>
        public static bool IsDeckSelected(GameObject deckElement)
        {
            if (deckElement == null) return false;

            var deckView = FindDeckViewInParents(deckElement);
            if (deckView == null) return false;

            // Find DeckViewSelector and get _selectedDeckView
            var selectedDeckView = GetSelectedDeckView();
            if (selectedDeckView == null) return false;

            // Compare if this deck's DeckView matches the selected one
            return deckView == selectedDeckView;
        }

        /// <summary>
        /// Returns a short invalid-deck status label for a deck entry.
        /// Returns null for valid decks.
        /// </summary>
        public static string GetDeckInvalidStatus(GameObject deckElement)
        {
            if (deckElement == null) return null;

            var deckView = FindDeckViewInParents(deckElement);
            if (deckView == null) return null;

            var type = deckView.GetType();
            var flags = PrivateInstance;

            bool animateInvalid = GetFieldValue<bool>(type, deckView, "_animateInvalid", flags);
            int invalidCardCount = GetFieldValue<int>(type, deckView, "_invalidCardCount", flags);
            bool craftable = GetFieldValue<bool>(type, deckView, "_animateCraftable", flags);
            bool uncraftable = GetFieldValue<bool>(type, deckView, "_animateUncraftable", flags);
            bool invalidCompanion = GetFieldValue<bool>(type, deckView, "_animateInvalidCompanion", flags);
            bool unavailable = GetFieldValue<bool>(type, deckView, "_animateUnavailable", flags);

            if (unavailable) return Models.Strings.DeckStatusUnavailable;
            if (animateInvalid) return Models.Strings.DeckStatusInvalid;
            if (invalidCardCount > 0) return Models.Strings.DeckStatusInvalidCards(invalidCardCount);
            if (uncraftable) return Models.Strings.DeckStatusMissingCards;
            if (craftable) return Models.Strings.DeckStatusMissingCardsCraftable;
            if (invalidCompanion) return Models.Strings.DeckStatusInvalidCompanion;

            return null;
        }

        /// <summary>
        /// Returns the detailed tooltip text for an invalid deck.
        /// Returns null for valid decks or when no detail is available.
        /// </summary>
        public static string GetDeckInvalidTooltip(GameObject deckElement)
        {
            if (deckElement == null) return null;

            // Only return tooltip if deck actually has an issue
            if (string.IsNullOrEmpty(GetDeckInvalidStatus(deckElement)))
                return null;

            var deckView = FindDeckViewInParents(deckElement);
            if (deckView == null) return null;

            var flags = PrivateInstance;
            var pubFlags = PublicInstance;

            // Read _tooltipTrigger field from DeckView
            var triggerField = deckView.GetType().GetField("_tooltipTrigger", flags);
            if (triggerField == null) return null;

            var trigger = triggerField.GetValue(deckView);
            if (trigger == null) return null;

            // Read TooltipData from trigger (public field)
            var triggerType = trigger.GetType();
            var dataField = triggerType.GetField("TooltipData", pubFlags);
            if (dataField == null) return null;

            var data = dataField.GetValue(trigger);
            if (data == null) return null;

            // Read Text from TooltipData (public property)
            var textProp = data.GetType().GetProperty("Text", pubFlags);
            if (textProp == null) return null;

            var text = textProp.GetValue(data) as string;
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Clean up: trim and collapse whitespace
            text = text.Trim();
            if (string.IsNullOrEmpty(text)) return null;

            return text;
        }

        private static TValue GetFieldValue<TValue>(System.Type type, object instance, string fieldName, System.Reflection.BindingFlags flags)
        {
            var field = type.GetField(fieldName, flags);
            if (field != null)
            {
                try { return (TValue)field.GetValue(instance); }
                catch { /* Field value may be incompatible type or inaccessible */ }
            }
            return default;
        }

        /// <summary>
        /// Get the currently selected DeckView from DeckViewSelector.
        /// </summary>
        private static MonoBehaviour GetSelectedDeckView()
        {
            // Find DeckViewSelector_Base
            var selectorTransform = GameObject.FindObjectsOfType<Transform>()
                .FirstOrDefault(t => t.name.Contains("DeckViewSelector_Base") && t.gameObject.activeInHierarchy);

            if (selectorTransform == null) return null;

            // Find DeckViewSelector component
            MonoBehaviour selector = null;
            foreach (var mb in selectorTransform.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "DeckViewSelector")
                {
                    selector = mb;
                    break;
                }
            }

            if (selector == null) return null;

            // Read _selectedDeckView field
            try
            {
                var field = selector.GetType().GetField("_selectedDeckView",
                    PrivateInstance);

                if (field != null)
                {
                    return field.GetValue(selector) as MonoBehaviour;
                }
            }
            catch (System.Exception ex)
            {
                Log.Activation("CardTileActivator", $"Error getting selected deck: {ex.Message}");
            }

            return null;
        }

        #endregion
    }
}
