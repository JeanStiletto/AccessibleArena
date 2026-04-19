# HelpNavigator.cs
Path: src/Core/Services/HelpNavigator.cs
Lines: 306

## Top-level comments
- Modal help menu navigator. When active, blocks all other input and allows navigation through keybind help items with Up/Down arrows. Closes with Backspace, Escape, or F1.

## public class HelpNavigator (line 14)

### Fields
- private readonly IAnnouncementService _announcer (line 16)
- private readonly List<string> _helpItems (line 17)
- private int _currentIndex (line 18)
- private bool _isActive (line 19)

### Properties
- public bool IsActive => _isActive (line 21)

### Methods
- public HelpNavigator(IAnnouncementService announcer) (line 23)
- public void RebuildItems() (line 33) — Rebuild help items when language changes
- private List<string> BuildHelpItems() (line 43)
- public void Toggle() (line 152)
- public void Open() (line 167)
- public void Close() (line 184)
- public bool HandleInput() (line 199)
- private void MoveNext() (line 242)
- private void MovePrevious() (line 257)
- private void MoveFirst() (line 272)
- private void MoveLast() (line 284)
- private void AnnounceCurrentItem() (line 297)
