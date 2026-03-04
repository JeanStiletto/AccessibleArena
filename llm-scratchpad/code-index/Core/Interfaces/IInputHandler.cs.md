# IInputHandler.cs Code Index

## File Overview
Interface for handling input and raising navigation events.

## Interface: IInputHandler (line 6)

### Methods
- void OnUpdate() (line 8)
  // Called every frame to process input

### Events
- event Action<KeyCode> OnKeyPressed (line 10)
  // Raised when any key is pressed

- event Action OnNavigateNext (line 11)
  // Raised when Tab/Down is pressed

- event Action OnNavigatePrevious (line 12)
  // Raised when Shift+Tab/Up is pressed

- event Action OnAccept (line 13)
  // Raised when Enter/Space is pressed

- event Action OnCancel (line 14)
  // Raised when Escape is pressed
