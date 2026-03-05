# CardInfoNavigator.cs

Handles vertical navigation through card information blocks.
When a card is focused, Arrow Up/Down navigates through:
Name, Mana Cost, Type, Power/Toughness, Rules Text, Flavor Text, Rarity, Artist

Uses lazy loading: info blocks are only extracted when user presses arrow keys,
not on focus change. This ensures fast navigation through many cards.

## Class: CardInfoNavigator (line 17)

### Fields
- readonly IAnnouncementService _announcer (line 19)
- List<CardInfoBlock> _blocks (line 20)
- GameObject _currentCard (line 21)
- int _currentBlockIndex (line 22)
- bool _isActive (line 23)
- bool _blocksLoaded (line 24)
- ZoneType _currentZone (line 25)

### Properties
- bool IsActive (line 27)
- GameObject CurrentCard (line 28)

### Constructor
- CardInfoNavigator(IAnnouncementService announcer) (line 30)

### Preparation Methods
- void PrepareForCard(GameObject cardElement, ZoneType zone) (line 39)
  Note: Prepares navigation without extracting info yet; lazy loaded on first arrow press

- void PrepareForCardInfo(List<CardInfoBlock> blocks, string cardName) (line 68)
  Note: Used for cards that exist only as GrpId data (e.g., opponent's commander)

- bool ActivateForCard(GameObject cardElement) (line 91)

- void InvalidateBlocks() (line 120)
  Note: Invalidates cached info blocks, preserves current block index

### Lifecycle Methods
- void Deactivate() (line 129)

### Input Handling
- bool HandleInput() (line 144)
  Note: Only responds to plain Arrow Up/Down without modifiers

- bool LoadBlocks() (line 209)
  Note: Loads info blocks from the current card; called lazily on first arrow press

### Navigation Methods
- void NavigateNext() (line 229)
- void NavigatePrevious() (line 242)
- void AnnounceCurrentBlock() (line 255)
- static string FormatBlock(CardInfoBlock block) (line 263)
