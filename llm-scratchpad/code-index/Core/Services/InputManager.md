# InputManager.cs
Path: src/Core/Services/InputManager.cs
Lines: 251

## Top-level comments
- Input manager handling keyboard input with the ability to consume/block keys from reaching the game's KeyboardManager via Harmony patch. Marks keys as consumed so unintended game actions are prevented when the mod handles them.

## public class InputManager : IInputHandler (line 16)

### Fields
- private static HashSet<KeyCode> _consumedKeysThisFrame = new HashSet<KeyCode>() (line 19)
- private static int _lastConsumeFrame = -1 (line 20)
- private static bool _blockSubmitForToggle (line 52)
- private static int _enterPressedWhileBlockedFrame = -1 (line 79)
- private static int _enterPressedHandledFrame = -1 (line 80)
- private readonly IShortcutRegistry _shortcuts (line 185)
- private readonly IAnnouncementService _announcer (line 186)
- private readonly HashSet<KeyCode> _customKeys (line 189) — Only shortcut keys the mod monitors; game navigation keys (arrows, tab, enter, escape) are left alone

### Properties
- public static bool ModMenuActive { get; set; } (line 26)
- public static bool PopupModeActive { get; set; } (line 32)
- public static bool BlockNextEnterKeyUp { get; set; } (line 41)
- public static bool BlockSubmitForToggle { get; set; } (line 53) — Persistent flag for EventSystemPatch to block Unity Submit events on toggles/dropdowns
- public static bool AllowNativeEnterOnLogin { get; set; } (line 72)
- public static bool EnterPressedWhileBlocked { get; set; } (line 82) — Frame-aware flag prevents double-activation from multiple GetKeyDown calls per frame

### Methods
- public static void MarkEnterHandled() (line 107)
- public static void ConsumeKey(KeyCode key) (line 116)
- public static bool IsKeyConsumed(KeyCode key) (line 134)
- public static bool GetKeyDownAndConsume(KeyCode key) (line 148)
- public static bool GetEnterAndConsume() (line 162) — Checks direct press AND EnterPressedWhileBlocked flag
- public InputManager(IShortcutRegistry shortcuts, IAnnouncementService announcer) (line 217)
- public void OnUpdate() (line 223)
- private void ProcessCustomKey(KeyCode key) (line 236)
