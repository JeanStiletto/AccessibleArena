# InputManager.cs

## Summary
Input manager that handles keyboard input with the ability to consume/block keys from reaching the game's KeyboardManager. When the mod handles a key, it marks it as "consumed" so the KeyboardManagerPatch blocks it from the game.

## Classes

### InputManager : IInputHandler (line 17)
```
public class InputManager : IInputHandler
  // Static key consumption tracking
  private static HashSet<KeyCode> _consumedKeysThisFrame (line 20)
  private static int _lastConsumeFrame (line 21)
  public static bool ModMenuActive { get; set; } (line 27)
  public static bool BlockNextEnterKeyUp { get; set; } (line 36)
  private static bool _blockSubmitForToggle (line 44)
  public static bool BlockSubmitForToggle { get; set; } (line 45)
  private static int _enterPressedWhileBlockedFrame (line 63)
  private static int _enterPressedHandledFrame (line 64)
  public static bool EnterPressedWhileBlocked { get; set; } (line 66)

  public static void MarkEnterHandled() (line 91)
  public static void ConsumeKey(KeyCode key) (line 100)
  public static bool IsKeyConsumed(KeyCode key) (line 118)
  public static bool GetKeyDownAndConsume(KeyCode key) (line 132)
  public static bool GetEnterAndConsume() (line 146)

  private readonly IShortcutRegistry _shortcuts (line 169)
  private readonly IAnnouncementService _announcer (line 170)
  private readonly HashSet<KeyCode> _customKeys (line 173)

  public event Action<KeyCode> OnKeyPressed (line 201)
  public event Action OnNavigateNext (line 202)
  public event Action OnNavigatePrevious (line 203)
  public event Action OnAccept (line 204)
  public event Action OnCancel (line 205)

  public InputManager(IShortcutRegistry shortcuts, IAnnouncementService announcer) (line 207)
  public void OnUpdate() (line 213)
  private void ProcessCustomKey(KeyCode key) (line 226)
  public void OnGameNavigateNext() (line 248)
  public void OnGameNavigatePrevious() (line 253)
  public void OnGameAccept() (line 258)
  public void OnGameCancel() (line 263)
```
