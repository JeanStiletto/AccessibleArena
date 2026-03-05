# MenuDebugHelper.cs

## Summary
Helper class for menu debugging and logging. Extracts verbose debug methods from GeneralMenuNavigator to reduce file size.

## Classes

### MenuDebugHelper (static) (line 19)
```
public static class MenuDebugHelper
  // Main debug dumps
  public static void LogAvailableUIElements(string tag, string sceneName, Func<IEnumerable<GameObject>> getActiveCustomButtons) (line 28)
  public static void DumpUIHierarchy(string tag, IAnnouncementService announcer) (line 355)
  public static void DumpChallengeBlade(string tag, IAnnouncementService announcer) (line 410)
  public static void DumpCardDetails(string tag, GameObject cardObj, IAnnouncementService announcer) (line 700)
  public static void DumpBoosterPackDetails(string tag, GameObject packObj, IAnnouncementService announcer) (line 802)
  public static void DumpWorkflowSystemDebug(string tag, GameObject workflowBrowser = null) (line 1039)

  // Component logging helpers
  private static void LogCustomButtonDetails(string tag, GameObject obj, string text, string path) (line 70)
  private static void LogTooltipTriggerDetails(string tag, MonoBehaviour tooltipTrigger) (line 136)
  public static string FormatValueForLog(object val) (line 186)
  private static void LogEventTriggers(string tag) (line 214)
  private static void LogStandardButtons(string tag) (line 227)
  private static void LogCustomToggles(string tag) (line 263)
  private static void LogScrollbars(string tag) (line 280)
  private static void LogScrollRects(string tag) (line 293)
  private static void LogUnityToggles(string tag) (line 306)
  private static void LogTmpDropdowns(string tag) (line 320)
  private static void LogCustomDropdowns(string tag) (line 334)

  // Hierarchy dump helpers
  private static void DumpDeepChildren(string tag, GameObject parent, int depth) (line 467)
  public static void DumpGameObjectChildren(string tag, GameObject parent, int currentDepth, int maxDepth) (line 517)
  public static void LogHierarchy(string tag, Transform parent, string indent, int maxDepth) (line 547)
  public static void DumpGameObjectDetails(string tag, GameObject obj, int maxDepth = 3) (line 598)
  private static void DumpGameObjectDetailsRecursive(string tag, GameObject obj, int depth, int maxDepth) (line 615)
  private static void DumpObjectProperties(string tag, object obj, string indent) (line 996)

  // Path utilities
  public static string GetGameObjectPath(GameObject obj) (line 565)
  public static string GetFullPath(Transform t) (line 584)

  // Type hierarchy
  private static string GetTypeHierarchy(Type type) (line 1462)
```
