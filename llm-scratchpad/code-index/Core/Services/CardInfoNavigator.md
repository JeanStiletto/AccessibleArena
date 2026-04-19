# CardInfoNavigator.cs
Path: src/Core/Services/CardInfoNavigator.cs
Lines: 287

## public class CardInfoNavigator (line 17)

Handles vertical navigation through card information blocks (Arrow Up/Down cycles Name, Mana Cost, Type, P/T, Rules, Flavor, Rarity, Artist). Uses lazy loading so blocks are only extracted on first arrow press.

### Fields
- private readonly IAnnouncementService _announcer (line 19)
- private List<CardInfoBlock> _blocks = new List<CardInfoBlock>() (line 20)
- private GameObject _currentCard (line 21)
- private int _currentBlockIndex = -1 (line 22)
- private bool _isActive (line 23)
- private bool _blocksLoaded (line 24)
- private bool _isHidden (line 25)
- private ZoneType _currentZone = ZoneType.Hand (line 26)

### Properties
- public bool IsActive => _isActive (line 28)
- public GameObject CurrentCard => _currentCard (line 29)

### Methods
- public CardInfoNavigator(IAnnouncementService announcer) (line 31)
- public void PrepareForCard(GameObject cardElement, ZoneType zone = ZoneType.Hand, bool isHidden = false) (line 40) — does not load blocks
- public void PrepareForCardInfo(List<CardInfoBlock> blocks, string cardName) (line 70) — for GrpId-only cards (opponent commander)
- public bool ActivateForCard(GameObject cardElement) (line 93)
- public void InvalidateBlocks() (line 122) — preserves block index for re-extraction
- public void Deactivate() (line 131)
- public bool HandleInput() (line 147) — only Arrow Up/Down without modifiers
- private bool LoadBlocks() (line 212)
- private void NavigateNext() (line 244)
- private void NavigatePrevious() (line 257)
- private void AnnounceCurrentBlock() (line 270)
- private static string FormatBlock(CardInfoBlock block) (line 278) — hides label when IsVerbose and VerboseAnnouncements disabled
