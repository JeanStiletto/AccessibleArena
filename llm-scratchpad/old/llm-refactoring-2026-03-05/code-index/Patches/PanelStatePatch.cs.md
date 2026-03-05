# PanelStatePatch.cs Code Index

## File Overview
Harmony patch for intercepting panel state changes from game controllers. Patches NavContentController, SettingsMenu, DeckSelectBlade, PlayBladeController, HomePageContentController, SocialUI, and mailbox controllers.

## Static Class: PanelStatePatch (line 18)

### Private Fields
- private static bool _patchApplied (line 20)

### Public Events
- public static event Action<object, bool, string> OnPanelStateChanged (line 26)
  // Parameters: (controller instance, isOpen, controllerTypeName)

- public static event Action<Guid, string, string, bool, bool> OnMailLetterSelected (line 32)
  // Parameters: (letterId, title, body, hasAttachments, isClaimed)

### Public Methods
- public static void Initialize() (line 38)
  // Manually applies the Harmony patch after game assemblies are loaded

- public static void DiscoverPanelTypes() (line 180)
  // Discovers and logs all types that might be panel controllers

- public static void MailboxOpenPostfix(object __instance) (line 865)
- public static void MailboxClosePostfix(object __instance) (line 878)
- public static void MailLetterSelectedPostfix(object __instance, object selectedLetter, bool isRead, Guid selectedLetterId) (line 895)
  // Postfix for ContentControllerPlayerInbox.OnLetterSelected

### Harmony Postfix Methods
- public static void ShowPostfix(object __instance) (line 1018)
- public static void HidePostfix(object __instance) (line 1039)
- public static void IsOpenSetterPostfix(object __instance, bool value) (line 1060)
- public static void SettingsShowPostfix(object __instance) (line 1081)
- public static void SettingsHidePostfix(object __instance) (line 1094)
- public static void DeckSelectShowPostfix(object __instance) (line 1107)
- public static void DeckSelectHidePostfix(object __instance) (line 1120)
- public static void BeginOpenPostfix(object __instance) (line 1133)
- public static void BeginClosePostfix(object __instance) (line 1154)
- public static void SettingsIsOpenPostfix(object __instance, bool value) (line 1176)
- public static void SettingsMainPanelPostfix(object __instance, bool value) (line 1189)
- public static void DeckSelectIsShowingPostfix(object __instance, bool value) (line 1203)
- public static void PlayBladeVisualStatePostfix(object __instance, object value) (line 1216)
- public static void IsEventBladeActivePostfix(object __instance, bool value) (line 1235)
- public static void IsDirectChallengeBladeActivePostfix(object __instance, bool value) (line 1248)
- public static void BladeContentViewShowPostfix(object __instance) (line 1261)
- public static void BladeContentViewHidePostfix(object __instance) (line 1275)
- public static void EventBladeShowPostfix(object __instance) (line 1289)
- public static void EventBladeHidePostfix(object __instance) (line 1302)

### Social UI Patches (with Tab blocking)
- public static bool SocialUIShowPrefix(object __instance) (line 1319)
  // Blocks opening if Tab key is pressed

- public static void SocialUIShowPostfix(object __instance) (line 1331)
- public static void SocialUIHidePostfix(object __instance) (line 1351)
- public static bool SocialUIClosePrefix(object __instance) (line 1375)
  // Blocks closing if Tab key is pressed

- public static bool SocialUISetVisiblePrefix(object __instance, bool visible) (line 1389)
  // Blocks showing if Tab key is pressed

- public static void SocialUISetVisiblePostfix(object __instance, bool visible) (line 1400)
- public static bool SocialUIHandleKeyDownPrefix(object __instance, UnityEngine.KeyCode curr) (line 1424)
  // Blocks Tab from toggling the social panel

### Matchmaking Patch
- public static void JoinMatchMakingPrefix(ref string internalEventName) (line 534)
  // When Bot Match mode is active, replaces event name with "AIBotMatch"

### Private Methods
- private static void PatchNavContentController(HarmonyLib.Harmony harmony) (line 85)
- private static void PatchSettingsMenu(HarmonyLib.Harmony harmony) (line 229)
- private static void PatchDeckSelectController(HarmonyLib.Harmony harmony) (line 324)
- private static void PatchPlayBladeController(HarmonyLib.Harmony harmony) (line 402)
- private static void PatchHomePageBladeStates(HarmonyLib.Harmony harmony) (line 440)
- private static void PatchJoinMatchMaking(HarmonyLib.Harmony harmony) (line 498)
- private static void PatchBladeContentView(HarmonyLib.Harmony harmony) (line 544)
- private static void PatchSocialUI(HarmonyLib.Harmony harmony) (line 651)
- private static void PatchMailboxController(HarmonyLib.Harmony harmony) (line 769)
- private static void PatchMailLetterSelected(HarmonyLib.Harmony harmony) (line 830)
- private static void LogTypeMembers(Type type) (line 967)
  // Logs methods and properties for debugging

- private static Type FindType(string fullName) (line 991)
  // Finds a type by full name across all loaded assemblies
