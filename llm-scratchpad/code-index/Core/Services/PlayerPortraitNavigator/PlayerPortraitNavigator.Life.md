# PlayerPortraitNavigator.Life.cs (partial)
Path: src/Core/Services/PlayerPortraitNavigator/PlayerPortraitNavigator.Life.cs
Lines: 543

## Top-level comments
- Life partial. Owns life totals (L key and Life property), counter suffix building, player effects string (designations, abilities, dungeon state), and MtgPlayer lookup via GameManager → GameState → LocalPlayer/Opponent. MtgEntity/MtgPlayer reflection cache lives here.

## public partial class PlayerPortraitNavigator (line 17)

### Entity reflection cache
- private static FieldInfo _countersField (line 20) — Note: MtgEntity.Counters (Dictionary<CounterType, int>)
- private static FieldInfo _designationsField (line 21) — Note: MtgEntity.Designations (List<DesignationData>)
- private static FieldInfo _abilitiesField (line 22) — Note: MtgEntity.Abilities (List<AbilityPrintingData>)
- private static FieldInfo _dungeonStateField (line 23) — Note: MtgPlayer.DungeonState (DungeonData)
- private static bool _entityReflectionInitialized (line 24)

### Methods
- private void AnnounceLifeTotals() (line 26) — Note: uses High priority to bypass duplicate suppression on repeated L presses
- private string BuildLifeWithCounters(int life, bool isOpponent) (line 59) — Note: "20 life" plus counter suffix if any
- private (int localLife, int opponentLife) GetLifeTotals() (line 78) — Note: walks GameManager → CurrentGameState or LatestGameState → LocalPlayer/Opponent; -1 if not found
- private int GetPlayerLife(object player) (line 165) — Note: tries LifeTotal/Life/CurrentLife/StartingLife/_life/_lifeTotal (both props and fields); falls back to dumping all player properties/fields to debug log if not found
- private object GetMtgPlayer(bool isOpponent) (line 245) — Note: same GameManager → GameState walk as GetLifeTotals
- private static void InitializeEntityReflection(object player) (line 280) — Note: walks base type hierarchy to find Counters/Designations/Abilities on MtgEntity; DungeonState on MtgPlayer directly
- private List<(string typeName, int count)> GetPlayerCounters(object player) (line 319) — Note: iterates Dictionary<CounterType, int> via IEnumerable; uses CardStateProvider.GetLocalizedCounterTypeName
- private static string FormatCountersForLife(List<(string typeName, int count)> counters) (line 365) — Note: comma-separated "N Type" fragments
- private string GetPlayerEffects(bool isOpponent) (line 380) — Note: collects designations (via FormatDesignation), ability texts (via CardTextProvider.GetAbilityText), and dungeon status; returns Strings.NoActiveEffects if nothing
- private static string FormatDesignation(string typeName, object desig, System.Type desigType) (line 516) — Note: handles Monarch/PlayerSpeed/Day/Night/CitysBlessing; returns null for card-level designations (Commander, Companion, Monstrous, etc.)
