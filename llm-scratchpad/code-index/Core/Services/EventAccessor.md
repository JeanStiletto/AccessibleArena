# EventAccessor.cs
Path: src/Core/Services/EventAccessor.cs
Lines: 1052

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
- private static bool IsRectTransformActive(MonoBehaviour tile, FieldInfo field) (line 130)
- private static bool IsImageActive(MonoBehaviour tile, FieldInfo field) (line 138)
- private static string ReadProgressFromPips(MonoBehaviour tile) (line 149) — Note: counts pips with active "Fill" child to compute filled/total progress
- public static string GetEventPageTitle() (line 184) — Note: tries EventUXInfo.PublicEventName first, falls back to EventInfo.InternalEventName
- private static MonoBehaviour FindEventPageController() (line 235) — Note: thin wrapper over FindCachedController
- private static void InitEventPageReflection(Type type) (line 238)
- private static object GetPlayerEvent(MonoBehaviour controller) (line 254) — Note: lazily initializes PlayerEvent field and EventInfo/EventUXInfo properties
- public static List<CardInfoBlock> GetEventPageInfoBlocks() (line 286) — Note: scans TMP_Text children, filters out buttons/objectives, splits on newlines
- private static bool IsInsideComponent(Transform child, Transform stopAt, string typeName) — in the EventPage region
- private static bool IsInsideNamedParent(Transform child, Transform stopAt, string nameSubstring) — in the EventPage region
- private static bool IsRedundantTitle(string blockText, string eventTitle) — Note: filters short blocks that look like the event name
- public static string GetPacketLabel(GameObject element) — Note: walks parent chain to JumpStartPacket, returns "{name} ({colors})"
- public static List<CardInfoBlock> GetPacketInfoBlocks(GameObject element) — Note: includes name, colors, featured card info from LandGrpId, and description text
- private static uint GetPacketLandGrpId(MonoBehaviour packet)
- public static bool IsInsideJumpStartPacket(GameObject element)
- public static GameObject GetJumpStartPacketRoot(GameObject element) — Note: used to sort packet elements by tile position rather than child offset
- public static bool ClickPacket(GameObject element) (line 638) — Note: invokes PacketInput.OnClick since UIActivator's pointer simulation doesn't reach CustomTouchButton on JumpStartPacket GO
- public static string GetPacketScreenSummary() (line 687) — Note: "Packet 1 of 2" via SubmissionCount()
- private static MonoBehaviour FindPacketController() (line 737) — Note: thin wrapper over FindCachedController
- private static void InitPacketReflection(Type type) (line 740)
- private static void InitJumpStartReflection(Type type) (line 758)
- private static string GetPacketColorInfo(MonoBehaviour packet) (line 774) — Note: looks up via controller's _packetToId dictionary and _currentState.GetDetailsById
- private static string TranslateManaColors(string[] rawColors) (line 828) — Note: translates W/U/B/R/G/C to localized color names
- public static Dictionary<string, string> GetAllTrackSummaries() (line 859) — Note: progress summaries for all Color Challenge tracks, keyed by localized color name
- private static string MapToLocalizedColor(string colorKey) (line 938)
- private static string ReadLocalizeText(FieldInfo field, MonoBehaviour owner) (line 955) — Note: shared helper used by tile title, packet display name, and popup title/desc readers
- private static MonoBehaviour FindCampaignGraphController() (line 966) — Note: thin wrapper over FindCachedController
- private static void InitCampaignGraphReflection(Type type) (line 969)
- private static MonoBehaviour FindCachedController(ref MonoBehaviour cache, string typeName, Action<Type> initReflection) (line 992) — Note: shared caching scene-scan helper; dedups the three FindXxxController methods
- private static MonoBehaviour FindParentComponent(GameObject element, string typeName) (line 1025)
- public static void ClearCache() (line 1043) — Note: call on scene changes
