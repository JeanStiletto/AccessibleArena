# DuelHolderCache.cs
Path: src/Core/Services/DuelHolderCache.cs
Lines: 47

## Top-level comments
- Shared static cache for duel card holder GameObjects keyed by substring match. Holders (e.g., "BattlefieldCardHolder", "LocalHand") persist for the entire duel; only their children change. Avoids repeated FindObjectsOfType scans in ZoneNavigator, BattlefieldNavigator, and DuelAnnouncer.

## public static class DuelHolderCache (line 12)
### Fields
- private static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>() (line 14)
### Methods
- public static GameObject GetHolder(string nameContains) (line 20) — Note: returns cached holder if still live, otherwise does FindObjectsOfType scan for any active GO whose name contains the pattern
- public static void Clear() (line 41) — Note: called on duel end (DuelAnnouncer.Deactivate)
