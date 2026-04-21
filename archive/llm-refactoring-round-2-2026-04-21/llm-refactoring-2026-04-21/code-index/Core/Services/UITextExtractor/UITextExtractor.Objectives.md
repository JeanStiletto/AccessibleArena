# UITextExtractor.Objectives.cs
Path: src/Core/Services/UITextExtractor/UITextExtractor.Objectives.cs
Lines: 546

## Top-level comments
- Feature partial for objective/quest/NPE/wildcard-progress label extraction: parses ObjectiveGraphics children (TextLine, Text_GoalProgress, Circle), reads ObjectiveBubble._popupData for localized reward/timer text, and classifies NPE stage state via Animator reflection.

## public static partial class UITextExtractor (line 12)

### Fields
- private static Type _npeObjectiveType (line 341, static)
- private static FieldInfo _npeCircleTextField (line 342, static)
- private static FieldInfo _npeAnimatorField (line 343, static)
- private static bool _npeFieldsCached (line 344, static)
- private static MethodInfo _animGetStateInfo (line 422, static) — Animator.GetCurrentAnimatorStateInfo(int)
- private static MethodInfo _stateInfoIsName (line 423, static) — AnimatorStateInfo.IsName(string)

### Methods
- private static string TryGetObjectiveText(GameObject gameObject) (line 20) — only processes "ObjectiveGraphics"; handles Wildcard progress, Achievement, QuestNormal, Daily, Weekly, BattlePass - Level, SparkRankTier1, and Timer (empty quest slot)
- private static string TryGetWildcardProgressText(GameObject gameObject, string parentName) (line 265) — reads Text_GoalProgress child; falls back to Image.fillAmount percentage
- private static string TryGetNPEObjectiveText(GameObject gameObject) (line 346) — reads NPEObjective._circleText roman numeral; combines with localized stage status from GetNPEObjectiveStatus
- private static string GetNPEObjectiveStatus(object animator) (line 425) — checks animator state names (Complete/Lock/Normal/etc.), falls back to GetBool("Completed"/"Locked")
- private static string TryGetObjectiveBubblePopupText(GameObject gameObject, string fieldName) (line 491) — reads ObjectiveBubble._popupData.{HeaderString1|HeaderString2|FooterString}.Key and resolves via MTGALocalizedString.ToString()
