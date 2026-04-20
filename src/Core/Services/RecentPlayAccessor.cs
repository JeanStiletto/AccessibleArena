using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides reflection-based access to the game's LastPlayedBladeContentView and
    /// LastPlayedBladeView. The game splits recent entries: the most recent goes into
    /// BladeView._lastPlayedTile, the rest go into ContentView._tiles.
    /// Used for enriching Recent tab deck labels with event names and finding play buttons.
    /// Caches FieldInfo objects for performance.
    /// </summary>
    public static class RecentPlayAccessor
    {
        /// <summary>
        /// Sentinel tile index returned by FindTileIndexForElement when the element
        /// is inside the BladeView's most-recent tile (not in the ContentView tiles).
        /// </summary>
        public const int BLADE_VIEW_INDEX = 1000;

        // Cached component references (invalidated on scene change via ClearCache)
        private static MonoBehaviour _cachedContentView;
        private static MonoBehaviour _cachedBladeView;

        // Cached reflection members (ContentView)
        private static FieldInfo _tilesField;       // _tiles (List<LastGamePlayedTile>)
        private static FieldInfo _modelsField;      // _models (List<RecentlyPlayedInfo>)

        // Cached reflection members (BladeView)
        private static FieldInfo _bladeViewTileField; // _lastPlayedTile (LastGamePlayedTile)

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
        /// Find and cache the LastPlayedBladeContentView and LastPlayedBladeView components.
        /// Returns the ContentView if still valid, otherwise searches again.
        /// </summary>
        public static MonoBehaviour FindContentView()
        {
            // Return cached if still valid
            if (_cachedContentView != null)
            {
                try
                {
                    if (_cachedContentView.gameObject != null && _cachedContentView.gameObject.activeInHierarchy)
                    {
                        // Also refresh blade view if needed
                        FindBladeView();
                        return _cachedContentView;
                    }
                }
                catch
                {
                    // Object was destroyed
                }
                _cachedContentView = null;
                _cachedBladeView = null;
            }

            // Search for both views in one pass
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var typeName = mb.GetType().Name;

                if (typeName == "LastPlayedBladeContentView" && _cachedContentView == null)
                {
                    _cachedContentView = mb;
                    if (!_reflectionInitialized)
                        InitializeReflection(mb.GetType());
                }
                else if (typeName == "LastPlayedBladeView" && _cachedBladeView == null)
                {
                    _cachedBladeView = mb;
                    if (_bladeViewTileField == null)
                        _bladeViewTileField = mb.GetType().GetField("_lastPlayedTile", PrivateInstance);
                }
            }

            return _cachedContentView;
        }

        /// <summary>
        /// Refresh the blade view cache if it's stale.
        /// </summary>
        private static void FindBladeView()
        {
            if (_cachedBladeView != null)
            {
                try
                {
                    if (_cachedBladeView.gameObject != null && _cachedBladeView.gameObject.activeInHierarchy)
                        return;
                }
                catch { }
                _cachedBladeView = null;
            }

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "LastPlayedBladeView")
                {
                    _cachedBladeView = mb;
                    if (_bladeViewTileField == null)
                        _bladeViewTileField = mb.GetType().GetField("_lastPlayedTile", PrivateInstance);
                    return;
                }
            }
        }

        /// <summary>
        /// Get the BladeView's most-recent tile (LastGamePlayedTile), or null.
        /// </summary>
        private static MonoBehaviour GetBladeViewTile()
        {
            if (_cachedBladeView == null || _bladeViewTileField == null)
                return null;

            try
            {
                return _bladeViewTileField.GetValue(_cachedBladeView) as MonoBehaviour;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read the event title from any LastGamePlayedTile by reading its _eventTitleText
        /// Localize component, falling back to _model.EventInfo.EventName.
        /// </summary>
        private static string ReadEventTitleFromTile(MonoBehaviour tile)
        {
            if (tile == null) return null;

            try
            {
                // Read _eventTitleText (Localize component) from the tile
                var eventTitleTextField = tile.GetType().GetField("_eventTitleText", PrivateInstance);
                if (eventTitleTextField != null)
                {
                    var localizeComp = eventTitleTextField.GetValue(tile) as MonoBehaviour;
                    if (localizeComp != null)
                    {
                        var tmp = localizeComp.GetComponentInChildren<TMPro.TMP_Text>();
                        if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                            return tmp.text;
                    }
                }

                // Fallback: read _model.EventInfo.EventName from the tile itself
                var modelField = tile.GetType().GetField("_model", PrivateInstance);
                if (modelField != null)
                {
                    var model = modelField.GetValue(tile);
                    if (model != null)
                    {
                        var eventInfoField = model.GetType().GetField("EventInfo");
                        var eventInfo = eventInfoField?.GetValue(model);
                        if (eventInfo != null)
                        {
                            // Try LocTitle first (localized), then EventName
                            var locTitleField = eventInfo.GetType().GetField("LocTitle");
                            if (locTitleField != null)
                            {
                                var locTitle = locTitleField.GetValue(eventInfo) as string;
                                if (!string.IsNullOrEmpty(locTitle))
                                    return locTitle;
                            }

                            var eventNameField = eventInfo.GetType().GetField("EventName");
                            if (eventNameField != null)
                                return eventNameField.GetValue(eventInfo) as string;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("RecentPlayAccessor", $"ReadEventTitleFromTile failed: {ex.Message}");
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

                Log.Msg("RecentPlayAccessor", $"Reflection init: " +
                    $"_tiles={_tilesField != null}, _models={_modelsField != null}");
            }
            catch (Exception ex)
            {
                Log.Error("RecentPlayAccessor", $"Reflection init failed: {ex.Message}");
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
        /// Use BLADE_VIEW_INDEX for the most-recent entry in the BladeView.
        /// Reads the rendered (localized) text from the tile's _eventTitleText component.
        /// Falls back to EventName from the model if no rendered text found.
        /// </summary>
        public static string GetEventTitle(int index)
        {
            // BladeView's most-recent tile
            if (index == BLADE_VIEW_INDEX)
                return ReadEventTitleFromTile(GetBladeViewTile());

            if (_cachedContentView == null || _tilesField == null)
                return null;

            try
            {
                var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                if (tiles == null || index < 0 || index >= tiles.Count)
                    return null;

                return ReadEventTitleFromTile(tiles[index] as MonoBehaviour);
            }
            catch (Exception ex)
            {
                Log.Error("RecentPlayAccessor", $"GetEventTitle({index}) failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Find the tile index for a given UI element by walking up the parent chain.
        /// Returns BLADE_VIEW_INDEX if inside the BladeView's most-recent tile.
        /// Returns -1 if the element is not inside any tile.
        /// </summary>
        public static int FindTileIndexForElement(GameObject element)
        {
            if (element == null)
                return -1;

            // Check ContentView tiles first
            if (_cachedContentView != null && _tilesField != null)
            {
                try
                {
                    var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                    if (tiles != null)
                    {
                        for (int i = 0; i < tiles.Count; i++)
                        {
                            var tile = tiles[i] as MonoBehaviour;
                            if (tile == null) continue;

                            if (element.transform.IsChildOf(tile.transform))
                                return i;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("RecentPlayAccessor", $"FindTileIndexForElement (content) failed: {ex.Message}");
                }
            }

            // Check BladeView's most-recent tile
            var bladeViewTile = GetBladeViewTile();
            if (bladeViewTile != null)
            {
                try
                {
                    if (element.transform.IsChildOf(bladeViewTile.transform))
                        return BLADE_VIEW_INDEX;
                }
                catch (Exception ex)
                {
                    Log.Error("RecentPlayAccessor", $"FindTileIndexForElement (blade) failed: {ex.Message}");
                }
            }

            return -1;
        }

        /// <summary>
        /// Find ALL non-deck CustomButtons in a tile (play button, secondary button, etc.).
        /// These are buttons NOT inside a DeckView_Base parent.
        /// Used for filtering them out of the navigation list.
        /// Supports BLADE_VIEW_INDEX for the BladeView's most-recent tile.
        /// </summary>
        public static List<GameObject> FindAllButtonsInTile(int index)
        {
            var result = new List<GameObject>();

            MonoBehaviour tile = GetTileByIndex(index);
            if (tile == null)
                return result;

            try
            {
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
                Log.Error("RecentPlayAccessor", $"FindAllButtonsInTile({index}) failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Find the play/continue button in a tile for auto-press on Enter.
        /// For ContentView tiles: returns _secondaryButton.
        /// For BladeView tile: returns _playButton.
        /// Falls back to first non-deck CustomButton if reflection fails.
        /// Supports BLADE_VIEW_INDEX for the BladeView's most-recent tile.
        /// </summary>
        public static GameObject FindPlayButtonInTile(int index)
        {
            MonoBehaviour tile = GetTileByIndex(index);
            if (tile == null)
                return null;

            try
            {
                // BladeView uses _playButton; ContentView uses _secondaryButton
                string buttonFieldName = (index == BLADE_VIEW_INDEX) ? "_playButton" : "_secondaryButton";
                var buttonField = tile.GetType().GetField(buttonFieldName, PrivateInstance);
                if (buttonField != null)
                {
                    var btn = buttonField.GetValue(tile) as MonoBehaviour;
                    if (btn != null && btn.gameObject.activeInHierarchy)
                        return btn.gameObject;
                }

                // Fallback: first non-deck CustomButton
                var allButtons = FindAllButtonsInTile(index);
                return allButtons.Count > 0 ? allButtons[0] : null;
            }
            catch (Exception ex)
            {
                Log.Error("RecentPlayAccessor", $"FindPlayButtonInTile({index}) failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get a tile MonoBehaviour by index. Supports both ContentView indices and BLADE_VIEW_INDEX.
        /// </summary>
        private static MonoBehaviour GetTileByIndex(int index)
        {
            if (index == BLADE_VIEW_INDEX)
                return GetBladeViewTile();

            if (_cachedContentView == null || _tilesField == null)
                return null;

            try
            {
                var tiles = _tilesField.GetValue(_cachedContentView) as IList;
                if (tiles == null || index < 0 || index >= tiles.Count)
                    return null;
                return tiles[index] as MonoBehaviour;
            }
            catch
            {
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
        /// Clear the cached component references. Call on scene changes.
        /// Reflection members are preserved since types don't change.
        /// </summary>
        public static void ClearCache()
        {
            _cachedContentView = null;
            _cachedBladeView = null;
        }
    }
}
