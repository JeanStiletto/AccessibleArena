# InputFieldEditHelper.cs

## Summary
Shared input field editing logic used by BaseNavigator popup mode. Handles edit mode state, key navigation, character announcements, and field reactivation. Supports both TMP_InputField and legacy InputField.

## Classes

### InputFieldEditHelper (line 17)
```
public class InputFieldEditHelper
  private readonly IAnnouncementService _announcer (line 19)
  private GameObject _editingField (line 22)
  private string _prevText (line 23)
  private int _prevCaretPos (line 24)
  public bool IsEditing => _editingField != null (line 26)
  public GameObject EditingField => _editingField (line 27)

  public struct FieldInfo (line 31)
    public bool IsValid (line 33)
    public string Text (line 34)
    public int CaretPosition (line 35)
    public bool IsPassword (line 36)
    public GameObject GameObject (line 37)

  public InputFieldEditHelper(IAnnouncementService announcer) (line 42)

  // Edit Mode Management
  public void EnterEditMode(GameObject field) (line 52)
  public void ExitEditMode() (line 64)
  public void SetEditingFieldSilently(GameObject field) (line 75)
  public void ClearEditingFieldSilently() (line 85)
  public void Clear() (line 93)

  // State Tracking
  public void TrackState() (line 108)
  public void TrackState(FieldInfo info) (line 127)

  // Key Handling
  public bool HandleEditing(Action<int> onTabNavigate) (line 150)

  // Field Info Retrieval
  public FieldInfo GetEditingFieldInfo() (line 202)
  public FieldInfo GetEditingFieldInfo(GameObject fallback) (line 211)
  public FieldInfo GetFieldInfoFrom(GameObject fieldObj, bool allowUnfocused = false) (line 223)
  public FieldInfo ScanForAnyFocusedField() (line 261)
  public FieldInfo ScanForAnyFocusedField(GameObject fallback) (line 269)

  // Announcements
  public void AnnounceDeletedCharacter() (line 327)
  public void AnnounceDeletedCharacter(FieldInfo info) (line 338)
  public void AnnounceCharacterAtCursor() (line 361)
  public void AnnounceCharacterAtCursor(FieldInfo info) (line 372)
  public void AnnounceFieldContent() (line 413)
  public void AnnounceFieldContent(FieldInfo info) (line 424)

  // Field Reactivation
  public void ReactivateField() (line 456)

  // Character Detection
  public static char FindDeletedCharacter(string prevText, string currentText, int prevCaretPos) (line 487)
```
