using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides reflection-based access to the game's LastPlayedBladeContentView.
    /// Used for enriching Recent tab deck labels with event names and finding play buttons.
    /// Caches FieldInfo objects for performance.
    /// </summary>
    public static class RecentPlayAccessor
    {
        private static readonly BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        // Cached component reference (invalidated on scene change via ClearCache)
        private static MonoBehaviour _cachedContentView;

        // Cached reflection members
        private static FieldInfo _tilesField;       // _tiles (List<LastGamePlayedTile>)
        private static FieldInfo _modelsField;      // _models (List<RecentlyPlayedInfo>)

        private static bool _reflectionInitialized;

        /// <summary>
        /// Whether the Recent tab content view is currently active and valid.
        /// </summary>
        public static bool IsActive
        {
            get
            {
                if (_cachedContentView == null) return false;
                try
                {
                    return _cachedContentView.gameObject != null &&
                           _cachedContentView.gameObject.activeInHierarchy;
                }
                catch
                {
                    _cachedContentView = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// Find and cache the LastPlayedBladeContentView component in the scene.
        /// Returns the cached component if still valid, otherwise searches again.
        /// </summary>
        public static MonoBehaviour FindContentView()
        {
            // Return cached if still valid
            if (_cachedContentView != null)
            {
                try
                {
                    if (_cachedContentView.gameObject != null && _cachedContentView.gameObject.activeInHierarchy)
                        return _cachedContentView;
                }
                catch
                {
                    // Object was destroyed
                }
                _cachedContentView = null;
            }

            // Search for LastPlayedBladeContentView in the scene
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "LastPlayedBladeContentView")
                {
                    _cachedContentView = mb;

                    if (!_reflectionInitialized)
                    {
                        InitializeReflection(mb.GetType());
                    }

                    return _cachedContentView;
                }
            }

            return null;
        }

        /// <summary>
        /// Initialize reflection members from the LastPlayedBladeContentView type.
        /// </summary>
        private static void InitializeReflection(Type type)
        {
            if (_reflectionInitialized) return;

            try
            {
                _tilesField = type.GetField("_tiles", PrivateInstance);
                _modelsField = type.GetField("_models", PrivateInstance);

                _reflectionInitialized = true;

                MelonLogger.Msg($"[RecentPlayAccessor] Reflection init: " +
                    $"_tiles={_tilesField != null}, _models={_modelsField != null}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[RecentPlayAccessor] Reflection init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the number of tiles (recently played entries).
        /// </summary>
        public static int GetTileCount()
        {
            if (_cachedContentView == null || _tilesField == null)
                return 0;

            try
            {
                var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                return tiles?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the event title for a tile at the given index.
        /// Reads the rendered (localized) text from the tile's _eventTitleText component.
        /// Falls back to EventName from the model if no rendered text found.
        /// </summary>
        public static string GetEventTitle(int index)
        {
            if (_cachedContentView == null || _tilesField == null)
                return null;

            try
            {
                var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                if (tiles == null || index < 0 || index >= tiles.Count)
                    return null;

                var tile = tiles[index] as MonoBehaviour;
                if (tile == null)
                    return null;

                // Read _eventTitleText (Localize component) from the tile via reflection
                var eventTitleTextField = tile.GetType().GetField("_eventTitleText", PrivateInstance);
                if (eventTitleTextField != null)
                {
                    var localizeComp = eventTitleTextField.GetValue(tile) as MonoBehaviour;
                    if (localizeComp != null)
                    {
                        // The Localize component's TextTarget writes to a TextMeshProUGUI
                        // Find TMP_Text on the same object or children for the rendered text
                        var tmp = localizeComp.GetComponentInChildren<TMPro.TMP_Text>();
                        if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                            return tmp.text;
                    }
                }

                // Fallback: read EventName from model
                if (_modelsField != null)
                {
                    var models = _modelsField.GetValue(_cachedContentView) as IList;
                    if (models != null && index < models.Count)
                    {
                        var model = models[index];
                        if (model != null)
                        {
                            var eventInfoField = model.GetType().GetField("EventInfo");
                            var eventInfo = eventInfoField?.GetValue(model);
                            if (eventInfo != null)
                            {
                                var eventNameField = eventInfo.GetType().GetField("EventName");
                                if (eventNameField != null)
                                    return eventNameField.GetValue(eventInfo) as string;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[RecentPlayAccessor] GetEventTitle({index}) failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if the entry at the given index is an in-progress event (Fortsetzen vs Spielen).
        /// </summary>
        public static bool GetIsInProgress(int index)
        {
            if (_cachedContentView == null || _modelsField == null)
                return false;

            try
            {
                var models = _modelsField.GetValue(_cachedContentView) as IList;
                if (models == null || index < 0 || index >= models.Count)
                    return false;

                var model = models[index];
                if (model == null)
                    return false;

                var eventInfoField = model.GetType().GetField("EventInfo");
                if (eventInfoField == null)
                    return false;

                var eventInfo = eventInfoField.GetValue(model);
                if (eventInfo == null)
                    return false;

                var isInProgressField = eventInfo.GetType().GetField("IsInProgress");
                if (isInProgressField != null)
                    return (bool)isInProgressField.GetValue(eventInfo);

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Find the tile index for a given UI element by walking up the parent chain.
        /// Returns -1 if the element is not inside any tile.
        /// </summary>
        public static int FindTileIndexForElement(GameObject element)
        {
            if (_cachedContentView == null || _tilesField == null || element == null)
                return -1;

            try
            {
                var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                if (tiles == null || tiles.Count == 0)
                    return -1;

                for (int i = 0; i < tiles.Count; i++)
                {
                    var tile = tiles[i] as MonoBehaviour;
                    if (tile == null) continue;

                    if (element.transform.IsChildOf(tile.transform))
                        return i;
                }

                return -1;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[RecentPlayAccessor] FindTileIndexForElement failed: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Find ALL non-deck CustomButtons in a tile (play button, secondary button, etc.).
        /// These are buttons NOT inside a DeckView_Base parent.
        /// Used for filtering them out of the navigation list.
        /// </summary>
        public static List<GameObject> FindAllButtonsInTile(int index)
        {
            var result = new List<GameObject>();
            if (_cachedContentView == null || _tilesField == null)
                return result;

            try
            {
                var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                if (tiles == null || index < 0 || index >= tiles.Count)
                    return result;

                var tile = tiles[index] as MonoBehaviour;
                if (tile == null)
                    return result;

                foreach (var mb in tile.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (mb == null) continue;
                    if (mb.GetType().Name != "CustomButton") continue;

                    // Skip buttons inside DeckView_Base (those are deck selection buttons)
                    if (IsInsideDeckView(mb.transform, tile.transform))
                        continue;

                    result.Add(mb.gameObject);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[RecentPlayAccessor] FindAllButtonsInTile({index}) failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Find the play/continue button in a tile for auto-press on Enter.
        /// Returns the _secondaryButton (the one that actually triggers play).
        /// We identify it by reading the _secondaryButton field via reflection.
        /// Falls back to first non-deck CustomButton if reflection fails.
        /// </summary>
        public static GameObject FindPlayButtonInTile(int index)
        {
            if (_cachedContentView == null || _tilesField == null)
                return null;

            try
            {
                var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                if (tiles == null || index < 0 || index >= tiles.Count)
                    return null;

                var tile = tiles[index] as MonoBehaviour;
                if (tile == null)
                    return null;

                // Try to get _secondaryButton directly via reflection
                var secondaryField = tile.GetType().GetField("_secondaryButton", PrivateInstance);
                if (secondaryField != null)
                {
                    var secondaryBtn = secondaryField.GetValue(tile) as MonoBehaviour;
                    if (secondaryBtn != null && secondaryBtn.gameObject.activeInHierarchy)
                        return secondaryBtn.gameObject;
                }

                // Fallback: first non-deck CustomButton
                var allButtons = FindAllButtonsInTile(index);
                return allButtons.Count > 0 ? allButtons[0] : null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[RecentPlayAccessor] FindPlayButtonInTile({index}) failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a transform is inside a DeckView_Base, stopping at the tile root.
        /// </summary>
        private static bool IsInsideDeckView(Transform target, Transform tileRoot)
        {
            Transform current = target.parent;
            while (current != null && current != tileRoot)
            {
                if (current.name.Contains("DeckView_Base"))
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Clear the cached component reference. Call on scene changes.
        /// Reflection members are preserved since types don't change.
        /// </summary>
        public static void ClearCache()
        {
            _cachedContentView = null;
        }
    }
}
