# BrowserDetector.cs
Path: src/Core/Services/BrowserDetector.cs
Lines: 806

## public class BrowserInfo (line 14)

Information about a detected browser.

### Properties
- public bool IsActive { get; set; } (line 16)
- public string BrowserType { get; set; } (line 17)
- public GameObject BrowserGameObject { get; set; } (line 18)
- public bool IsZoneBased => IsScryLike || IsLondon (line 19)
- public bool IsScryLike { get; set; } (line 20)
- public bool IsLondon { get; set; } (line 21)
- public bool IsMulligan { get; set; } (line 22)
- public bool IsWorkflow { get; set; } (line 23)
- public bool IsOptionalAction { get; set; } (line 24)
- public List<GameObject> WorkflowButtons { get; set; } (line 27) — workflow action buttons
- public static BrowserInfo None => new BrowserInfo { IsActive = false } (line 29)

## public static class BrowserDetector (line 36)

Stateless detection with caching. Pattern mirrors CardDetector.

### Constants
- public const string ButtonKeep = "KeepButton" (line 41)
- public const string ButtonMulligan = "MulliganButton" (line 42)
- public const string ButtonSubmit = "SubmitButton" (line 43)
- public const string ButtonSingle = "SingleButton" (line 44)
- public const string PromptButtonPrimaryPrefix = "PromptButton_Primary" (line 45)
- public const string PromptButtonSecondaryPrefix = "PromptButton_Secondary" (line 46)
- public const string HolderDefault = "BrowserCardHolder_Default" (line 49)
- public const string HolderViewDismiss = "BrowserCardHolder_ViewDismiss" (line 50)
- private const string ScaffoldPrefix = "BrowserScaffold_" (line 53)
- private const string WorkflowBrowserName = "WorkflowBrowser" (line 56)
- public const string BrowserTypeMulligan = "Mulligan" (line 59)
- public const string BrowserTypeOpeningHand = "OpeningHand" (line 60)
- public const string BrowserTypeLondon = "London" (line 61)
- public const string BrowserTypeWorkflow = "Workflow" (line 62)
- public const string BrowserTypeViewDismiss = "ViewDismiss" (line 63)
- public static readonly string[] ButtonPatterns (line 66)
- public static readonly string[] ConfirmPatterns (line 67)
- public static readonly string[] CancelPatterns (line 68)

### Fields
- private static BrowserInfo _cachedBrowserInfo (line 76)
- private static float _lastScanTime (line 77)
- private const float ScanInterval = 0.1f (line 78) — scan every 100ms
- private static readonly HashSet<string> _loggedBrowserTypes (line 81)
- private static readonly HashSet<string> _debugEnabledBrowsers (line 84)

### Methods
- public static void EnableDebugForBrowser(string browserType) (line 95)
- public static void DisableDebugForBrowser(string browserType) (line 104)
- public static bool IsDebugEnabled(string browserType) (line 113)
- public static void DisableAllDebug() (line 121)
- public static BrowserInfo FindActiveBrowser() (line 135) — cached scene scan
- private static bool IsBrowserStillValid() (line 177)
- public static void InvalidateCache() (line 204)
- public static bool IsMulliganBrowser(string browserType) (line 213) — OpeningHand or Mulligan
- public static bool IsLondonBrowser(string browserType) (line 221)
- public static bool IsScryLikeBrowser(string browserType) (line 230) — Scry/Surveil/ReadAhead/SplitGroup
- public static bool IsSplitBrowser(string browserType) (line 242) — SplitGroup
- public static bool IsZoneBasedBrowser(string browserType) (line 250)
- public static bool IsWorkflowBrowser(string browserType) (line 258)
- public static bool IsOptionalActionBrowser(string browserType) (line 263) — also matches "Riot"
- public static string GetFriendlyBrowserName(string typeName) (line 273)
- public static bool IsValidCardName(string cardName) (line 281)
- private static BrowserInfo ScanForBrowser() (line 296) — single-pass scene scan
- private static string ExtractBrowserTypeFromScaffold(string scaffoldName) (line 450)
- private static bool IsMulliganBrowserVisible() (line 472)
- private static int CountCardsInContainer(GameObject container) (line 488)
- private static void LogBrowserDiscovery(string scaffoldName, string scaffoldType) (line 504)
- public static GameObject FindActiveGameObject(string exactName) (line 520)
- public static List<GameObject> FindActiveGameObjects(Func<GameObject, bool> predicate) (line 535)
- public static bool HasClickableComponent(GameObject go) (line 551)
- public static bool MatchesButtonPattern(string buttonName, string[] patterns) (line 572)
- public static bool IsDuplicateCard(GameObject card, List<GameObject> existingCards) (line 588)
- private static void DumpWorkflowBrowserDebug(GameObject wb, string actionText) (line 598)
- private static GameObject FindConfirmWidgetButton(GameObject workflowBrowser) (line 763)
