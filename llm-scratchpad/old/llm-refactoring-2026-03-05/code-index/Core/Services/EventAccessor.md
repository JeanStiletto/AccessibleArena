# EventAccessor.cs

Provides reflection-based access to event tiles, event page, and packet selection. Used for enriching accessibility labels with event status, progress, and packet info.

## static class EventAccessor (line 15)

### Constants
- PrivateInstance (BindingFlags) (line 17)
- PublicInstance (BindingFlags) (line 19)

### Private Fields - PlayBladeEventTile Reflection (line 22)
- _tileReflectionInit (bool) (line 23)
- _tileTitleTextField (FieldInfo) (line 24)
- _tileRankImageField (FieldInfo) (line 25)
- _tileBo3IndicatorField (FieldInfo) (line 26)
- _tileAttractParentField (FieldInfo) (line 27)
- _tileProgressPipsField (FieldInfo) (line 28)

### Private Fields - EventPageContentController Reflection (line 30)
- _eventPageReflectionInit (bool) (line 31)
- _currentEventContextField (FieldInfo) (line 32)
- _playerEventField (FieldInfo) (line 33) - Note: FIELD not property
- _eventInfoProp (PropertyInfo) (line 34)
- _eventUxInfoProp (PropertyInfo) (line 35)

### Private Fields - PacketSelectContentController Reflection (line 37)
- _packetReflectionInit (bool) (line 38)
- _packetOptionsField (FieldInfo) (line 39)
- _selectedPackIdField (FieldInfo) (line 40)
- _currentStateField (FieldInfo) (line 41)
- _packetToIdField (FieldInfo) (line 42)
- _headerTextField (FieldInfo) (line 43)

### Private Fields - JumpStartPacket Reflection (line 45)
- _jumpStartReflectionInit (bool) (line 46)
- _packTitleField (FieldInfo) (line 47)

### Private Fields - Cached Components (line 49)
- _cachedEventPageController (MonoBehaviour) (line 50)
- _cachedPacketController (MonoBehaviour) (line 51)

### Public Methods - Event Tile Enrichment (line 53)
- GetEventTileLabel(GameObject) → string (line 60) - Note: returns "{title}, [progress], [ranked], [bo3]"

### Private Methods - Event Tile (line 108)
- InitTileReflection(Type) (line 109)
- ReadTileTitle(MonoBehaviour) → string (line 127)
- IsRectTransformActive(MonoBehaviour, FieldInfo) → bool (line 142)
- IsImageActive(MonoBehaviour, FieldInfo) → bool (line 150)
- ReadProgressFromPips(MonoBehaviour) → string (line 161)

### Public Methods - Event Page (line 190)
- GetEventPageTitle() → string (line 196) - Note: returns localized display name
- GetEventPageSummary() → string (line 250) - Note: returns wins/losses/format
- GetEventPageInfoBlocks() → List<CardInfoBlock> (line 364) - Note: filters out buttons and objectives

### Private Methods - Event Page (line 284)
- FindEventPageController() → MonoBehaviour (line 284)
- InitEventPageReflection(Type) (line 316)
- GetPlayerEvent(MonoBehaviour) → object (line 332)
- IsInsideComponent(Transform, Transform, string) → bool (line 428)
- IsInsideNamedParent(Transform, Transform, string) → bool (line 447)
- IsRedundantTitle(string, string) → bool (line 464) - Note: fuzzy matching for event name

### Public Methods - Packet Selection (line 494)
- GetPacketLabel(GameObject) → string (line 502) - Note: returns "{name} ({colors})"
- GetPacketInfoBlocks(GameObject) → List<CardInfoBlock> (line 540) - Note: includes featured card from LandGrpId
- IsInsideJumpStartPacket(GameObject) → bool (line 692)
- ClickPacket(GameObject) → bool (line 703) - Note: invokes PacketInput.OnClick
- GetPacketScreenSummary() → string (line 752) - Note: "Packet 1 of 2"

### Private Methods - Packet Selection (line 631)
- GetPacketLandGrpId(MonoBehaviour) → uint (line 631)
- FindPacketController() → MonoBehaviour (line 802)
- InitPacketReflection(Type) (line 833)
- InitJumpStartReflection(Type) (line 851)
- ReadPacketDisplayName(MonoBehaviour) → string (line 866)
- GetPacketColorInfo(MonoBehaviour) → string (line 884)
- TranslateManaColors(string[]) → string (line 938)

### Public Methods - Utility (line 962)
- FindParentComponent(GameObject, string) → MonoBehaviour (line 967)
- ClearCache() (line 985)
