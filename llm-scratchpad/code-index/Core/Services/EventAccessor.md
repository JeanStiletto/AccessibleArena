# EventAccessor.cs
Path: src/Core/Services/EventAccessor.cs
Lines: 1516

## Top-level comments
- Provides reflection-based access to event tiles, event page, packet selection, and Color Challenge campaign graph. Enriches accessibility labels with event status, progress, and packet info. Follows the same pattern as RecentPlayAccessor.

## public static class EventAccessor (line 17)
### Fields
- private static bool _tileReflectionInit (line 21)
- private static FieldInfo _tileTitleTextField (line 22)
- private static FieldInfo _tileRankImageField (line 23)
- private static FieldInfo _tileBo3IndicatorField (line 24)
- private static FieldInfo _tileAttractParentField (line 25)
- private static FieldInfo _tileProgressPipsField (line 26)
- private static bool _eventPageReflectionInit (line 29)
- private static FieldInfo _currentEventContextField (line 30)
- private static FieldInfo _playerEventField (line 31) — Note: EventContext.PlayerEvent is a FIELD not a property
- private static PropertyInfo _eventInfoProp (line 32)
- private static PropertyInfo _eventUxInfoProp (line 33)
- private static bool _packetReflectionInit (line 36)
- private static FieldInfo _packetOptionsField (line 37)
- private static FieldInfo _selectedPackIdField (line 38)
- private static FieldInfo _currentStateField (line 39)
- private static FieldInfo _packetToIdField (line 40)
- private static FieldInfo _headerTextField (line 41)
- private static bool _jumpStartReflectionInit (line 44)
- private static FieldInfo _packTitleField (line 45)
- private static bool _campaignGraphReflectionInit (line 48)
- private static FieldInfo _campaignGraphStrategyField (line 49)
- private static MonoBehaviour _cachedEventPageController (line 52)
- private static MonoBehaviour _cachedPacketController (line 53)
- private static MonoBehaviour _cachedCampaignGraphController (line 54)
### Methods
- public static string GetEventTileLabel(GameObject element) (line 63) — Note: walks parent chain to PlayBladeEventTile, reads title + ranked/bo3/progress pips
- private static void InitTileReflection(Type type) (line 112)
- private static string ReadTileTitle(MonoBehaviour tile) (line 130)
- private static bool IsRectTransformActive(MonoBehaviour tile, FieldInfo field) (line 145)
- private static bool IsImageActive(MonoBehaviour tile, FieldInfo field) (line 153)
- private static string ReadProgressFromPips(MonoBehaviour tile) (line 164) — Note: counts pips with active "Fill" child to compute filled/total progress
- public static string GetEventPageTitle() (line 199) — Note: tries EventUXInfo.PublicEventName first, falls back to EventInfo.InternalEventName
- public static string GetEventPageSummary() (line 253) — Note: reads CurrentWins/MaxWins for wins/losses summary
- private static MonoBehaviour FindEventPageController() (line 287)
- private static void InitEventPageReflection(Type type) (line 319)
- private static object GetPlayerEvent(MonoBehaviour controller) (line 335) — Note: lazily initializes PlayerEvent field and EventInfo/EventUXInfo properties
- public static List<CardInfoBlock> GetEventPageInfoBlocks() (line 367) — Note: scans TMP_Text children, filters out buttons/objectives, splits on newlines
- private static bool IsInsideComponent(Transform child, Transform stopAt, string typeName) (line 431)
- private static bool IsInsideNamedParent(Transform child, Transform stopAt, string nameSubstring) (line 450)
- private static bool IsRedundantTitle(string blockText, string eventTitle) (line 467) — Note: filters short blocks that look like the event name (<=4 words and share 1/3 words with title)
- public static string GetPacketLabel(GameObject element) (line 505) — Note: walks parent chain to JumpStartPacket, returns "{name} ({colors})"
- public static List<CardInfoBlock> GetPacketInfoBlocks(GameObject element) (line 543) — Note: includes name, colors, featured card info from LandGrpId, and description text
- private static uint GetPacketLandGrpId(MonoBehaviour packet) (line 634)
- public static bool IsInsideJumpStartPacket(GameObject element) (line 695)
- public static GameObject GetJumpStartPacketRoot(GameObject element) (line 707) — Note: used to sort packet elements by tile position rather than child offset
- public static bool ClickPacket(GameObject element) (line 719) — Note: invokes PacketInput.OnClick since UIActivator's pointer simulation doesn't reach CustomTouchButton on JumpStartPacket GO
- public static string GetPacketScreenSummary() (line 768) — Note: "Packet 1 of 2" via SubmissionCount()
- private static MonoBehaviour FindPacketController() (line 818)
- private static void InitPacketReflection(Type type) (line 849)
- private static void InitJumpStartReflection(Type type) (line 867)
- private static string ReadPacketDisplayName(MonoBehaviour packet) (line 882)
- private static string GetPacketColorInfo(MonoBehaviour packet) (line 900) — Note: looks up via controller's _packetToId dictionary and _currentState.GetDetailsById
- private static string TranslateManaColors(string[] rawColors) (line 954) — Note: translates W/U/B/R/G/C to localized color names
- public static Dictionary<string, string> GetAllTrackSummaries() (line 986) — Note: progress summaries for all Color Challenge tracks, keyed by localized color name
- private static string MapToLocalizedColor(string colorKey) (line 1065)
- public static List<CardInfoBlock> GetCampaignGraphInfoBlocks() (line 1085) — Note: reads ObjectiveBubbles when track module visible, falls back to strategy data otherwise
- private static Dictionary<string, object> BuildNodeMap(MonoBehaviour controller) (line 1146)
- private static string ReadBubbleInfo(MonoBehaviour bubble, object matchNode = null) (line 1184) — Note: reads roman numeral, Animator bools (Locked/Completed/Selected), reward popup, and enriches with match node data
- private static string ReadRewardDisplayText(object reward) (line 1318)
- private static string ReadLocalizeText(FieldInfo field, MonoBehaviour owner) (line 1360)
- private static bool IsPlaceholderText(string text) (line 1374) — Note: filters developer template text like "character max)", "short sentences go here"
- private static List<CardInfoBlock> GetCampaignGraphInfoFromStrategy() (line 1388) — Note: fallback when track module not visible; reads track-level summary
- private static MonoBehaviour FindCampaignGraphController() (line 1438)
- private static void InitCampaignGraphReflection(Type type) (line 1469)
- private static MonoBehaviour FindParentComponent(GameObject element, string typeName) (line 1488)
- public static void ClearCache() (line 1506) — Note: call on scene changes
