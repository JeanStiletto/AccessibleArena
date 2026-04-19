# MenuDebugHelper.cs
Path: src/Core/Services/MenuDebugHelper.cs
Lines: 1474

## Top-level comments
- Helper class for menu debugging and logging. Extracts verbose debug methods from GeneralMenuNavigator to reduce file size.

## public static class MenuDebugHelper (line 20)

### Methods
- public static void LogAvailableUIElements(string tag, string sceneName, Func<IEnumerable<GameObject>> getActiveCustomButtons) (line 29)
- private static void LogCustomButtonDetails(string tag, GameObject obj, string text, string path) (line 71)
- private static void LogTooltipTriggerDetails(string tag, MonoBehaviour tooltipTrigger) (line 137)
- public static string FormatValueForLog(object val) (line 187)
- private static void LogEventTriggers(string tag) (line 215)
- private static void LogStandardButtons(string tag) (line 228)
- private static void LogCustomToggles(string tag) (line 264)
- private static void LogScrollbars(string tag) (line 281)
- private static void LogScrollRects(string tag) (line 294)
- private static void LogUnityToggles(string tag) (line 307)
- private static void LogTmpDropdowns(string tag) (line 321)
- private static void LogCustomDropdowns(string tag) (line 335)
- public static void DumpUIHierarchy(string tag, IAnnouncementService announcer) (line 356)
- public static void DumpChallengeBlade(string tag, IAnnouncementService announcer) (line 411)
- private static void DumpDeepChildren(string tag, GameObject parent, int depth) (line 468)
- public static void DumpGameObjectChildren(string tag, GameObject parent, int currentDepth, int maxDepth) (line 518)
- public static void LogHierarchy(string tag, Transform parent, string indent, int maxDepth) (line 548)
- public static string GetGameObjectPath(GameObject obj) (line 566)
- public static string GetFullPath(Transform t) (line 585)
- public static void DumpGameObjectDetails(string tag, GameObject obj, int maxDepth = 3) (line 599)
- private static void DumpGameObjectDetailsRecursive(string tag, GameObject obj, int depth, int maxDepth) (line 616)
- public static void DumpCardDetails(string tag, GameObject cardObj, IAnnouncementService announcer) (line 701)
- public static void DumpBoosterPackDetails(string tag, GameObject packObj, IAnnouncementService announcer) (line 803)
- private static void DumpObjectProperties(string tag, object obj, string indent) (line 997)
- public static void DumpWorkflowSystemDebug(string tag, GameObject workflowBrowser = null) (line 1040)
- private static string GetTypeHierarchy(Type type) (line 1462)
