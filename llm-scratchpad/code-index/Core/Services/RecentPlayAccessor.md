# RecentPlayAccessor.cs Code Index

## Summary
Provides reflection-based access to the game's LastPlayedBladeContentView. Used for enriching Recent tab deck labels with event names and finding play buttons. Caches FieldInfo objects for performance.

## Classes

### static class RecentPlayAccessor (line 15)
```
private static readonly BindingFlags PrivateInstance (line 17)

private static MonoBehaviour _cachedContentView (line 21)

private static FieldInfo _tilesField (line 24)
  // NOTE: _tiles (List<LastGamePlayedTile>)
private static FieldInfo _modelsField (line 25)
  // NOTE: _models (List<RecentlyPlayedInfo>)

private static bool _reflectionInitialized (line 27)

public static bool IsActive { get; } (line 32)
  // NOTE: Whether the Recent tab content view is currently active and valid
public static MonoBehaviour FindContentView() (line 54)
  // NOTE: Find and cache the LastPlayedBladeContentView component
private static void InitializeReflection(Type type) (line 94)
public static int GetTileCount() (line 118)
public static string GetEventTitle(int index) (line 138)
  // NOTE: Get event title for tile at index, reads rendered text from _eventTitleText
public static bool GetIsInProgress(int index) (line 201)
  // NOTE: Check if entry is in-progress event (Fortsetzen vs Spielen)
public static int FindTileIndexForElement(GameObject element) (line 240)
  // NOTE: Find tile index for UI element by walking up parent chain
public static List<GameObject> FindAllButtonsInTile(int index) (line 274)
  // NOTE: Find ALL non-deck CustomButtons in a tile (play button, secondary button, etc.)
public static GameObject FindPlayButtonInTile(int index) (line 316)
  // NOTE: Find play/continue button in tile for auto-press on Enter, returns _secondaryButton
private static bool IsInsideDeckView(Transform target, Transform tileRoot) (line 354)
  // NOTE: Check if transform is inside DeckView_Base, stopping at tile root
public static void ClearCache() (line 370)
  // NOTE: Clear cached component reference, call on scene changes
```
