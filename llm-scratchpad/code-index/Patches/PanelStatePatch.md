# PanelStatePatch.cs
Path: src/Patches/PanelStatePatch.cs
Lines: 1446

## Top-level comments
- Harmony patch for intercepting panel state changes from game controllers (NavContentController and similar), allowing the mod to detect open/close events without polling.

## public static class PanelStatePatch (line 20)
### Fields
- private static bool _patchApplied (line 22)
### Events
- public static event Action<object, bool, string> OnPanelStateChanged (line 28)
- public static event Action<Guid, string, string, bool, bool> OnMailLetterSelected (line 34)
### Methods
- public static void Initialize() (line 40) — Note: applies all subpatches and runs DiscoverPanelTypes
- private static void PatchNavContentController(HarmonyLib.Harmony harmony) (line 87)
- public static void DiscoverPanelTypes() (line 182) — Note: iterates all AppDomain assemblies to log candidate panel types
- private static void PatchSettingsMenu(HarmonyLib.Harmony harmony) (line 231)
- private static void PatchDeckSelectController(HarmonyLib.Harmony harmony) (line 326)
- private static void PatchPlayBladeController(HarmonyLib.Harmony harmony) (line 404)
- private static void PatchHomePageBladeStates(HarmonyLib.Harmony harmony) (line 442)
- private static void PatchJoinMatchMaking(HarmonyLib.Harmony harmony) (line 500)
- public static void JoinMatchMakingPrefix(ref string internalEventName) (line 536) — Note: mutates internalEventName to "AIBotMatch" in Bot Match mode (side effect) and resets mode flag
- private static void PatchBladeContentView(HarmonyLib.Harmony harmony) (line 546)
- private static void PatchSocialUI(HarmonyLib.Harmony harmony) (line 653)
- private static void PatchMailboxController(HarmonyLib.Harmony harmony) (line 790)
- private static void PatchMailLetterSelected(HarmonyLib.Harmony harmony) (line 851)
- public static void MailboxOpenPostfix(object __instance) (line 886)
- public static void MailboxClosePostfix(object __instance) (line 899)
- public static void MailLetterSelectedPostfix(object __instance, object selectedLetter, bool isRead, Guid selectedLetterId) (line 916) — Note: reflects into view model to extract title/body/attachments/claimed state
- private static void LogTypeMembers(Type type) (line 988)
- public static void ShowPostfix(object __instance) (line 1013) — Note: also fires PlayBlade:Generic event when GameObject name contains "PlayBlade"
- public static void HidePostfix(object __instance) (line 1034) — Note: also fires PlayBlade:Generic event when GameObject name contains "PlayBlade"
- public static void IsOpenSetterPostfix(object __instance, bool value) (line 1055) — Note: also fires PlayBlade:Generic event when GameObject name contains "PlayBlade"
- public static void SettingsShowPostfix(object __instance) (line 1076)
- public static void SettingsHidePostfix(object __instance) (line 1089)
- public static void DeckSelectShowPostfix(object __instance) (line 1102)
- public static void DeckSelectHidePostfix(object __instance) (line 1115)
- public static void BeginOpenPostfix(object __instance) (line 1128) — Note: only fires event for PlayBlade controllers; other panels wait for FinishOpen
- public static void BeginClosePostfix(object __instance) (line 1149)
- public static void SettingsIsOpenPostfix(object __instance, bool value) (line 1171)
- public static void SettingsMainPanelPostfix(object __instance, bool value) (line 1184)
- public static void DeckSelectIsShowingPostfix(object __instance, bool value) (line 1198)
- public static void PlayBladeVisualStatePostfix(object __instance, object value) (line 1211) — Note: converts enum to int (0=Hidden means closed)
- public static void IsEventBladeActivePostfix(object __instance, bool value) (line 1230)
- public static void IsDirectChallengeBladeActivePostfix(object __instance, bool value) (line 1243)
- public static void BladeContentViewShowPostfix(object __instance) (line 1256)
- public static void BladeContentViewHidePostfix(object __instance) (line 1270)
- public static void EventBladeShowPostfix(object __instance) (line 1284)
- public static void EventBladeHidePostfix(object __instance) (line 1297)
- public static bool SocialUIShowPrefix(object __instance) (line 1314) — Note: blocks ShowSocialEntitiesList when Tab is held
- public static void SocialUIShowPostfix(object __instance) (line 1326) — Note: skips notification when Tab is held
- public static void SocialUIHidePostfix(object __instance) (line 1346) — Note: skips notification when Tab is held
- public static bool SocialUIClosePrefix(object __instance) (line 1370) — Note: blocks CloseFriendsWidget/Minimize when Tab is held
- public static bool SocialUISetVisiblePrefix(object __instance, bool visible) (line 1384) — Note: blocks SetVisible(true) when Tab is held
- public static void SocialUISetVisiblePostfix(object __instance, bool visible) (line 1395)
- public static bool SocialUIHandleKeyDownPrefix(object __instance, UnityEngine.KeyCode curr) (line 1419) — Note: swallows Tab key inside SocialUI
- public static bool SocialUIShowChatWindowPrefix() (line 1435) — Note: blocks chat window when Tab is held
