# GameLogNavigator.cs
Path: src/Core/Services/GameLogNavigator.cs
Lines: 179

## Top-level comments
- Modal game-log navigator. Snapshots the announcement history and walks it newest-first with Up/Down/Home/End. Closed with O, Backspace, or Escape.

## public class GameLogNavigator (line 14)
### Fields
- private readonly IAnnouncementService _announcer (line 16)
- private readonly List<string> _items = new List<string>() (line 17)
- private int _currentIndex (line 18)
- private bool _isActive (line 19)
### Properties
- public bool IsActive (line 21)
### Methods
- public GameLogNavigator(IAnnouncementService announcer) (line 23)
- public void Open() (line 32) — Note: snapshots announcer.History in reverse order (newest first)
- public void Close() (line 58)
- public bool HandleInput() (line 73) — Note: returns true for ALL keys while active; Down moves to older, Up moves to newer
- private void MoveNext() (line 116)
- private void MovePrevious() (line 130)
- private void MoveFirst() (line 144)
- private void MoveLast() (line 156)
- private void AnnounceCurrentItem() (line 169) — Note: uses force: true on HelpItemPosition to bypass verbose filtering
