# BrowserNavigator.cs

Navigator for browser UIs in the duel scene.
Orchestrates browser detection and navigation:
- Uses BrowserDetector for finding active browsers
- Delegates zone-based navigation (Scry/London) to BrowserZoneNavigator
- Handles generic browsers (YesNo, Dungeon, etc.) directly

## Class: BrowserNavigator (line 20)

Note: File is very large (>115KB). Preview shows first 2KB only.
Full content includes generic browser navigation, ViewDismiss auto-dismiss,
AssignDamage browser state management, and extensive reflection-based API calls.

### Fields (from preview)
- readonly IAnnouncementService _announcer (line 22)
- readonly BrowserZoneNavigator _zoneNavigator (line 23)
- bool _isActive (line 26)
- bool _hasAnnouncedEntry (line 27)
- BrowserInfo _browserInfo (line 28)
- List<GameObject> _browserCards (line 31)
- List<GameObject> _browserButtons (line 32)
- int _currentCardIndex (line 33)
- int _currentButtonIndex (line 34)
- bool _viewDismissDismissed (line 37)
- bool _isAssignDamage (line 40)
- object _assignDamageBrowserRef (line 41)
- System.Collections.IDictionary _spinnerMap (line 42)
- uint _totalDamage (line 43)
- bool _totalDamageCached (line 44)

Note: Complete file structure available in actual source at C:\Users\fabia\arena\src\Core\Services\BrowserNavigator.cs
Full file includes methods for browser detection, navigation, card/button management, and submit handling.
