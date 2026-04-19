# ExtendedInfoNavigator.cs
Path: src/Core/Services/ExtendedInfoNavigator.cs
Lines: 327

## Top-level comments
- Modal extended card-info navigator. When active, blocks all other input and walks through rules lines, linked-face data, linked tokens, and keyword descriptions with Up/Down/Home/End. Closed with I, Backspace, or Escape.

## public class ExtendedInfoNavigator (line 14)
### Fields
- private readonly IAnnouncementService _announcer (line 16)
- private readonly List<string> _items = new List<string>() (line 17)
- private int _currentIndex (line 18)
- private bool _isActive (line 19)
### Properties
- public bool IsActive (line 21)
### Methods
- public ExtendedInfoNavigator(IAnnouncementService announcer) (line 23)
- public void Open(GameObject card) (line 33) — Note: filters keyword-only rules lines to avoid duplicating keyword block below; announces NoExtendedCardInfo if nothing collected
- public void Open(uint grpId) (line 115) — Note: GrpId overload used by store details / data-only contexts; dedupes keywords against rules lines when PAPA is unavailable
- public void Close() (line 204)
- public bool HandleInput() (line 219) — Note: returns true for ALL keys while active to block other input
- private void MoveNext() (line 262)
- private void MovePrevious() (line 277)
- private void MoveFirst() (line 292)
- private void MoveLast() (line 304)
- private void AnnounceCurrentItem() (line 317)
