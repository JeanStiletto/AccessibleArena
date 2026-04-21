# DuelAnnouncer.NPE.cs
Path: src/Core/Services/DuelAnnouncer/DuelAnnouncer.NPE.cs
Lines: 444

## Top-level comments
- NPE (New Player Experience) tutorial support: handles dialog/reminder/tooltip/warning events; simulates card hover for tutorial flow; extracts localization keys for language-agnostic hint matching; manages NPE director pause/resume via hover simulation.

## public partial class DuelAnnouncer (line 15)

### Fields
- private bool _shownBlockingReminderThisStep (line 18) — suppresses duplicate BlockingReminder in same phase
- private bool _suppressNextActionReminder (line 21) — suppresses ActionReminder after custom tooltip
- private static FieldInfo _npeDialogLineField (line 24)
- private static FieldInfo _npeReminderField (line 25)
- private static FieldInfo _npeReminderTextField (line 26)
- private static FieldInfo _npeReminderSuggestedField (line 27)
- private static FieldInfo _npeTooltipTypeField (line 28)
- private static FieldInfo _npeWarningTextField (line 29)
- private static FieldInfo _localizedStringKeyField (line 30)
- private static bool _npeReflectionInitialized (line 31)
- private bool _isNPETutorial (line 38) — set by CheckNPETutorial()
- private bool _npeCheckDone (line 39)
- private static PropertyInfo _npeDirectorProp (line 41)
- private GameObject _lastHoveredNPECard (line 44) — tracks previous hovered card for change detection
- private bool _lastHoverWasStack (line 45) — fires null before moving away from Stack cards
- private static FieldInfo _hoverEventField (line 47)
- private static bool _hoverFieldSearched (line 48)

### Properties
- public bool IsNPETutorial (line 37)

### Methods
- private static void EnsureNPEReflection() (line 50) — lazy-loads NPE type field/property caches
- private string HandleNPEDialog(object uxEvent) (line 98) — extracts hint via localization key, reads aloud AlwaysReminders
- private string HandleNPEReminder(object uxEvent) (line 141) — resolves suggested card names, suppresses duplicate BlockingReminder
- private string HandleNPETooltip(object uxEvent) (line 227) — applies custom text replacements, suppresses specific tooltips
- private string HandleNPEWarning(object uxEvent) (line 265)
- public void CheckNPETutorial() (line 293) — detects NpeDirector presence on GameManager
- public void UpdateNPEHoverSimulation() (line 336) — fires OnHoveredCardUpdated for tutorial flow control; critical: fires null before leaving Stack cards to unpause NPE director
- private static bool IsInStackHolder(GameObject go) (line 371)
- private static object GetDuelSceneCDCOrParent(GameObject go) (line 388)
- private static void FireHoveredCardUpdated(object cdc) (line 400) — invokes CardHoverController.OnHoveredCardUpdated via reflection
