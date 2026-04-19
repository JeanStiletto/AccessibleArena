# RecentPlayAccessor.cs
Path: src/Core/Services/RecentPlayAccessor.cs
Lines: 497

## Top-level comments
- Reflection-based access to LastPlayedBladeContentView and LastPlayedBladeView. The game splits recent entries: the most recent goes into BladeView._lastPlayedTile, the rest go into ContentView._tiles. Used for enriching Recent tab deck labels and finding play buttons.

## public static class RecentPlayAccessor (line 18)
### Fields
- public const int BLADE_VIEW_INDEX = 1000 (line 24) — Note: sentinel index returned by FindTileIndexForElement when the element is inside the BladeView's most-recent tile.
- private static MonoBehaviour _cachedContentView (line 27)
- private static MonoBehaviour _cachedBladeView (line 28)
- private static FieldInfo _tilesField (line 31)
- private static FieldInfo _modelsField (line 32)
- private static FieldInfo _bladeViewTileField (line 35)
- private static bool _reflectionInitialized (line 37)

### Properties
- public static bool IsActive (line 42)

### Methods
- public static MonoBehaviour FindContentView() (line 64) — Note: also refreshes the BladeView cache and initializes reflection on first discovery.
- private static void FindBladeView() (line 112)
- private static MonoBehaviour GetBladeViewTile() (line 141)
- private static string ReadEventTitleFromTile(MonoBehaviour tile) (line 160) — Note: prefers rendered localized _eventTitleText TMP text; falls back to model.EventInfo.LocTitle then EventName.
- private static void InitializeReflection(Type type) (line 217)
- public static int GetTileCount() (line 240)
- public static string GetEventTitle(int index) (line 262) — Note: BLADE_VIEW_INDEX routes to the BladeView tile.
- public static bool GetIsInProgress(int index) (line 289)
- public static int FindTileIndexForElement(GameObject element) (line 329) — Note: returns BLADE_VIEW_INDEX for BladeView tile, -1 if not inside any tile.
- public static List<GameObject> FindAllButtonsInTile(int index) (line 382) — Note: skips buttons inside DeckView_Base (those are deck selection buttons).
- public static GameObject FindPlayButtonInTile(int index) (line 419) — Note: BladeView uses _playButton; ContentView uses _secondaryButton; falls back to first non-deck CustomButton.
- private static MonoBehaviour GetTileByIndex(int index) (line 451)
- private static bool IsInsideDeckView(Transform target, Transform tileRoot) (line 475)
- public static void ClearCache() (line 491) — Note: reflection members are preserved since game types don't change.
