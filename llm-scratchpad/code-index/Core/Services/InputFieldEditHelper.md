# InputFieldEditHelper.cs
Path: src/Core/Services/InputFieldEditHelper.cs
Lines: 582

## Top-level comments
- Shared input field editing logic used by BaseNavigator popup mode. Handles edit mode state, key navigation, character announcements, and field reactivation. Supports both TMP_InputField and legacy InputField.

## public class InputFieldEditHelper (line 17)

### Fields
- private readonly IAnnouncementService _announcer (line 19)
- private GameObject _editingField (line 22)
- private string _prevText = "" (line 23)
- private int _prevCaretPos (line 24)

### Properties
- public bool IsEditing => _editingField != null (line 26)
- public GameObject EditingField => _editingField (line 27)

### Nested Types
- public struct FieldInfo (line 31)
  - public bool IsValid (line 33)
  - public string Text (line 34)
  - public int CaretPosition (line 35)
  - public bool IsPassword (line 36)
  - public GameObject GameObject (line 37)

### Methods
- public InputFieldEditHelper(IAnnouncementService announcer) (line 42)
- public void EnterEditMode(GameObject field) (line 52)
- public void ExitEditMode() (line 68) — Passes editing field reference to DeactivateFocusedInputField; critical for Tab so onEndEdit fires on correct field
- public void SetEditingFieldSilently(GameObject field) (line 80)
- public void ClearEditingFieldSilently() (line 90)
- public void Clear() (line 98)
- public void TrackState() (line 113)
- public void TrackState(FieldInfo info) (line 132)
- public bool HandleEditing(Action<int> onTabNavigate) (line 155)
- public FieldInfo GetEditingFieldInfo() (line 208)
- public FieldInfo GetEditingFieldInfo(GameObject fallback) (line 217)
- public FieldInfo GetFieldInfoFrom(GameObject fieldObj, bool allowUnfocused = false) (line 229)
- public FieldInfo ScanForAnyFocusedField() (line 267)
- public FieldInfo ScanForAnyFocusedField(GameObject fallback) (line 275)
- public void AnnounceDeletedCharacter() (line 333)
- public void AnnounceDeletedCharacter(FieldInfo info) (line 344)
- public void AnnounceCharacterAtCursor() (line 369)
- private bool IsEditingFieldFocused() (line 391)
- public void AnnounceCharacterAtCursor(FieldInfo info) (line 406) — Left arrow announces text[caretPos], Right announces text[caretPos-1] (TMP caret is post-move when read)
- public void AnnounceFieldContent() (line 452)
- public void AnnounceFieldContent(FieldInfo info) (line 463)
- public void PreserveTextOnEscape() (line 496) — Restores _prevText before ExitEditMode so TMP's OnUpdateSelected Escape handling doesn't revert to m_OriginalText
- public void ReactivateField() (line 523)
- public static char FindDeletedCharacter(string prevText, string currentText, int prevCaretPos) (line 554)
