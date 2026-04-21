# BrowserNavigator.SelectGroup.cs
Path: src/Core/Services/BrowserNavigator/BrowserNavigator.SelectGroup.cs
Lines: 208

## Top-level comments
- Feature partial for the SelectGroup browser (Fact or Fiction style pile selection). Caches the two pile CDC lists from the game's SelectGroupBrowser via GetCardGroups(), then drives discovery and per-card announcements that include pile membership (Pile 1/Pile 2) and index within pile. Handles face-down cards explicitly.

## public partial class BrowserNavigator (line 16)
### Fields
- private bool _isSelectGroup (line 19)
- private object _selectGroupBrowserRef (line 20)
- private List<object> _pile1CDCs (line 21) — top group CDCs
- private List<object> _pile2CDCs (line 22) — bottom group CDCs
- private Dictionary<GameObject, (int pile, int indexInPile, int pileTotal)> _selectGroupCardMap (line 23)

### Methods
- private void CacheSelectGroupState() (line 29) — GameManager→BrowserManager→CurrentBrowser; calls GetCardGroups() to populate _pile1CDCs/_pile2CDCs
- private void DiscoverSelectGroupCards() (line 116) — iterates pile CDCs and calls AddSelectGroupCard; populates _selectGroupCardMap
- private void AddSelectGroupCard(object cdc, int pileNumber, int indexInPile, int pileTotal) (line 132) — gets GO from CDC, adds to _browserCards and _selectGroupCardMap; includes face-down cards
- private void AnnounceSelectGroupCard(GameObject card) (line 170) — looks up pile membership from _selectGroupCardMap; uses Strings.SelectGroupFaceDown for unnamed cards; calls CardNavigator.PrepareForCard if face-up
