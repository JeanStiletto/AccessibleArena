# HelpNavigator.cs

Modal help menu navigator. When active, blocks all other input and allows navigation through keybind help items with Up/Down arrows. Closes with Backspace or F1.

## class HelpNavigator (line 14)

### Private Fields
- _announcer (IAnnouncementService) (line 16)
- _helpItems (List<string>) (line 17)
- _currentIndex (int) (line 18)
- _isActive (bool) (line 19)

### Public Properties
- IsActive → bool (line 21)

### Constructor
- HelpNavigator(IAnnouncementService) (line 23)

### Public Methods
- RebuildItems() (line 33) - Note: call after LocaleManager reloads strings
- Toggle() (line 137)
- Open() (line 152)
- Close() (line 169)
- HandleInput() → bool (line 184) - Note: returns true if input was handled (blocks other input)

### Private Methods
- BuildHelpItems() → List<string> (line 43) - Note: builds from localized strings
- MoveNext() (line 227)
- MovePrevious() (line 242)
- MoveFirst() (line 257)
- MoveLast() (line 269)
- AnnounceCurrentItem() (line 282)
