# DuelHolderCache.cs

Shared static cache for duel card holder GameObjects. Holders persist for the entire duel; only their children change. Avoids repeated FindObjectsOfType scene scans.

## static class DuelHolderCache (line 12)

### Private Fields
- _cache (Dictionary<string, GameObject>) (line 14)

### Public Methods
- GetHolder(string) → GameObject (line 20) - Note: finds and caches on first call
- Clear() (line 41) - Called on duel end
