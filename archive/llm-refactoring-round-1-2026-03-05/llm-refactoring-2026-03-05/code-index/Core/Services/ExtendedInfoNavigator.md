# ExtendedInfoNavigator.cs

Modal extended card info navigator. When active, blocks all other input and allows navigation through keyword descriptions and linked face info with Up/Down arrows. Closes with I, Backspace, or Escape.

## class ExtendedInfoNavigator (line 14)

### Private Fields
- _announcer (IAnnouncementService) (line 16)
- _items (List<string>) (line 17)
- _currentIndex (int) (line 18)
- _isActive (bool) (line 19)

### Public Properties
- IsActive → bool (line 21)

### Constructor
- ExtendedInfoNavigator(IAnnouncementService) (line 23)

### Public Methods
- Open(GameObject) (line 33) - Note: builds items from keyword descriptions and linked face info
- Close() (line 75)
- HandleInput() → bool (line 90) - Note: returns true if input was handled (blocks other input)

### Private Methods
- MoveNext() (line 133)
- MovePrevious() (line 148)
- MoveFirst() (line 163)
- MoveLast() (line 175)
- AnnounceCurrentItem() (line 188)
