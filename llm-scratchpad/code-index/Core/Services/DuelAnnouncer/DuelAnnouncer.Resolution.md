# DuelAnnouncer.Resolution.cs
Path: src/Core/Services/DuelAnnouncer/DuelAnnouncer.Resolution.cs
Lines: 478

## Top-level comments
- Spell/ability resolution tracking and library browser events; correlates damage with resolving cards; handles scry/surveil/mill effects; announces spell/ability casts and resolutions via delayed coroutine.

## public partial class DuelAnnouncer (line 15)

### Fields
- private Dictionary<uint, string> _instanceIdToName (line 18) — card name cache
- private string _lastResolvingCardName (line 21) — set on ResolutionStarted, cleared on ResolutionEnded, used for damage correlation
- private uint _lastResolvingInstanceId (line 22)
- private bool _lastResolvingIsAbility (line 23)

### Properties
- public bool IsLibraryBrowserActive { get; private set; } (line 29)
- public string CurrentEffectType { get; private set; } (line 34)
- public int CurrentEffectCount { get; private set; } (line 35)

### Methods
- public bool DidSpellResolveRecently(int withinMs = 500) (line 41)
- private string HandleMultistepEffect(object uxEvent) (line 46) — detects scry/surveil/mill, sets IsLibraryBrowserActive
- public void OnLibraryBrowserClosed() (line 164)
- private string HandleResolutionStarted(object uxEvent) (line 171) — tracks instigator for damage correlation
- private string HandleResolutionEnded(object uxEvent) (line 228) — clears tracking, announces resolve via delayed coroutine
- private IEnumerator AnnounceResolvedDelayed(string message) (line 264) — 4-frame delay for ordering with stack announcement
- private string BuildCounteredAnnouncement(string ownerPrefix, string cardName, object transfer, bool exiled) (line 279) — extracts counterspell source
- private IEnumerator AnnounceStackCardDelayed() (line 338) — delays stack card announcement up to 3 frames for holder population
- private string BuildCastAnnouncement(GameObject cardObj) (line 364) — handles abilities vs spells, respects brief-mode settings
- private string GetCastPrefix(GameObject cardObj) (line 407) — distinguishes Adventure/MDFC/Split/Omen card types via ObjectType enum
- private GameObject GetTopStackCard() (line 461)
