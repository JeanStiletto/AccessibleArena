# BrowserNavigator.MultiZone.cs
Path: src/Core/Services/BrowserNavigator/BrowserNavigator.MultiZone.cs
Lines: 172

## Top-level comments
- Feature partial for SelectCardsMultiZone browsers (e.g. choose a card from your/opponent's graveyard + exile simultaneously). Adds a virtual "zone selector" element that Up/Down/Home/End cycles through zone toggle buttons, each of which filters the holder to a different zone. Clicking a zone triggers a coroutine to rediscover cards after the game rebuilds the holder.

## public partial class BrowserNavigator (line 16)
### Fields
- private bool _isMultiZone (line 19)
- private List<GameObject> _zoneButtons (line 20)
- private int _currentZoneButtonIndex (line 21)
- private bool _onZoneSelector (line 22) — true when focus is on the zone selector element
- private static readonly Dictionary<string, (ZoneType local, ZoneType opponent)> MultiZoneMap (line 25, static) — maps game zone names to mod ZoneType with local/opponent variants (Graveyard/Exile/Library/Hand/Command)

### Methods
- private void EnterZoneSelector() (line 39) — sets _onZoneSelector=true and deactivates CardInfoNavigator so it doesn't intercept arrow keys
- private string GetMultiZoneCardZoneName(GameObject card) (line 49) — returns ", Your/Opponent's zone" suffix using CardStateProvider and MultiZoneMap
- private void AnnounceMultiZoneSelector() (line 67) — announces zone name, position among zones, and card count
- private void CycleMultiZone(bool next) (line 81) — increments/decrements _currentZoneButtonIndex with boundary announcements; calls ActivateMultiZoneButton
- private void ActivateMultiZoneButton() (line 104) — SimulatePointerClick on zone button; starts RediscoverMultiZoneCards coroutine
- private IEnumerator RediscoverMultiZoneCards() (line 118) — WaitForSeconds(0.3f), clears and rediscovers cards, calls AnnounceMultiZoneSelector
- private string GetCurrentZoneButtonLabel() (line 134)
- private int FindActiveZoneButtonIndex() (line 153) — checks Toggle.isOn on zone buttons; falls back to 0
