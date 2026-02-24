using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Reflection;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides reflection-based access to event tiles, event page, and packet selection.
    /// Used for enriching accessibility labels with event status, progress, and packet info.
    /// Follows the same pattern as RecentPlayAccessor.
    /// </summary>
    public static class EventAccessor
    {
        private static readonly BindingFlags PrivateInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags PublicInstance =
            BindingFlags.Public | BindingFlags.Instance;

        // --- PlayBladeEventTile reflection cache ---
        private static bool _tileReflectionInit;
        private static FieldInfo _tileTitleTextField;       // _titleText (Localize component)
        private static FieldInfo _tileRankImageField;       // _rankImage (Image)
        private static FieldInfo _tileBo3IndicatorField;    // _bestOf3Indicator (RectTransform)
        private static FieldInfo _tileAttractParentField;   // _attractParent (RectTransform)
        private static FieldInfo _tileProgressPipsField;    // _eventProgressPips (RectTransform)

        // --- EventPageContentController reflection cache ---
        private static bool _eventPageReflectionInit;
        private static FieldInfo _currentEventContextField; // _currentEventContext (EventContext)
        private static PropertyInfo _playerEventProp;       // EventContext.PlayerEvent
        private static PropertyInfo _eventInfoProp;         // IPlayerEvent.EventInfo
        private static PropertyInfo _eventUxInfoProp;       // IPlayerEvent.EventUXInfo

        // --- PacketSelectContentController reflection cache ---
        private static bool _packetReflectionInit;
        private static FieldInfo _packetOptionsField;       // _packetOptions (List<JumpStartPacket>)
        private static FieldInfo _selectedPackIdField;      // _selectedPackId (string)
        private static FieldInfo _currentStateField;        // _currentState (ServiceState)
        private static FieldInfo _packetToIdField;          // _packetToId (Dictionary<JumpStartPacket, string>)
        private static FieldInfo _headerTextField;          // _headerText (Localize)

        // --- JumpStartPacket reflection cache ---
        private static bool _jumpStartReflectionInit;
        private static FieldInfo _packTitleField;           // _packTitle (Localize)

        // Cached component references (invalidated on scene change)
        private static MonoBehaviour _cachedEventPageController;
        private static MonoBehaviour _cachedPacketController;

        #region Event Tile Enrichment

        /// <summary>
        /// Get an enriched label for an event tile element.
        /// Walks the parent chain to find PlayBladeEventTile, then reads its UI components.
        /// Returns: "{title}" + optional status info (ranked, bo3, in progress, progress).
        /// </summary>
        public static string GetEventTileLabel(GameObject element)
        {
            if (element == null) return null;

            try
            {
                // Walk parent chain to find PlayBladeEventTile component
                var tile = FindParentComponent(element, "PlayBladeEventTile");
                if (tile == null) return null;

                if (!_tileReflectionInit)
                    InitTileReflection(tile.GetType());

                // Read title text from _titleText (Localize -> TMP_Text)
                string title = ReadTileTitle(tile);
                if (string.IsNullOrEmpty(title)) return null;

                // Build enriched label
                var parts = new System.Collections.Generic.List<string>();
                parts.Add(title);

                // Check if in progress (_attractParent active)
                if (IsRectTransformActive(tile, _tileAttractParentField))
                {
                    // Check progress pips
                    string progress = ReadProgressFromPips(tile);
                    if (!string.IsNullOrEmpty(progress))
                        parts.Add(progress);
                    else
                        parts.Add(Strings.EventTileInProgress);
                }

                // Check ranked
                if (IsImageActive(tile, _tileRankImageField))
                    parts.Add(Strings.EventTileRanked);

                // Check Bo3
                if (IsRectTransformActive(tile, _tileBo3IndicatorField))
                    parts.Add(Strings.EventTileBo3);

                return string.Join(", ", parts);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetEventTileLabel failed: {ex.Message}");
                return null;
            }
        }

        private static void InitTileReflection(Type type)
        {
            if (_tileReflectionInit) return;

            _tileTitleTextField = type.GetField("_titleText", PrivateInstance);
            _tileRankImageField = type.GetField("_rankImage", PrivateInstance);
            _tileBo3IndicatorField = type.GetField("_bestOf3Indicator", PrivateInstance);
            _tileAttractParentField = type.GetField("_attractParent", PrivateInstance);
            _tileProgressPipsField = type.GetField("_eventProgressPips", PrivateInstance);

            _tileReflectionInit = true;

            MelonLogger.Msg($"[EventAccessor] Tile reflection init: " +
                $"title={_tileTitleTextField != null}, rank={_tileRankImageField != null}, " +
                $"bo3={_tileBo3IndicatorField != null}, attract={_tileAttractParentField != null}, " +
                $"pips={_tileProgressPipsField != null}");
        }

        private static string ReadTileTitle(MonoBehaviour tile)
        {
            if (_tileTitleTextField == null) return null;

            var localizeComp = _tileTitleTextField.GetValue(tile) as MonoBehaviour;
            if (localizeComp == null) return null;

            // Localize component writes to a TMP_Text child
            var tmp = localizeComp.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                return UITextExtractor.CleanText(tmp.text);

            return null;
        }

        private static bool IsRectTransformActive(MonoBehaviour tile, FieldInfo field)
        {
            if (field == null) return false;

            var rt = field.GetValue(tile) as RectTransform;
            return rt != null && rt.gameObject.activeInHierarchy;
        }

        private static bool IsImageActive(MonoBehaviour tile, FieldInfo field)
        {
            if (field == null) return false;

            var component = field.GetValue(tile) as Component;
            return component != null && component.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Read progress from event progress pips. Counts active/filled pips.
        /// </summary>
        private static string ReadProgressFromPips(MonoBehaviour tile)
        {
            if (_tileProgressPipsField == null) return null;

            var pipsParent = _tileProgressPipsField.GetValue(tile) as RectTransform;
            if (pipsParent == null || !pipsParent.gameObject.activeInHierarchy) return null;

            int total = 0;
            int filled = 0;

            foreach (Transform pip in pipsParent)
            {
                if (!pip.gameObject.activeInHierarchy) continue;
                total++;

                // Filled pips typically have a "Fill" child active or an Image with higher alpha
                var fillChild = pip.Find("Fill");
                if (fillChild != null && fillChild.gameObject.activeInHierarchy)
                    filled++;
            }

            if (total > 0)
                return Strings.EventTileProgress(filled, total);

            return null;
        }

        #endregion

        #region Event Page

        /// <summary>
        /// Get the event page title from the active EventPageContentController.
        /// Returns the event's public display name or null.
        /// </summary>
        public static string GetEventPageTitle()
        {
            try
            {
                var controller = FindEventPageController();
                if (controller == null) return null;

                var playerEvent = GetPlayerEvent(controller);
                if (playerEvent == null) return null;

                // Try EventUXInfo.PublicEventName first (localized display name)
                if (_eventUxInfoProp != null)
                {
                    var uxInfo = _eventUxInfoProp.GetValue(playerEvent);
                    if (uxInfo != null)
                    {
                        var publicNameProp = uxInfo.GetType().GetProperty("PublicEventName", PublicInstance);
                        if (publicNameProp != null)
                        {
                            string publicName = publicNameProp.GetValue(uxInfo) as string;
                            if (!string.IsNullOrEmpty(publicName))
                                return publicName;
                        }
                    }
                }

                // Fallback: EventInfo.InternalEventName
                if (_eventInfoProp != null)
                {
                    var eventInfo = _eventInfoProp.GetValue(playerEvent);
                    if (eventInfo != null)
                    {
                        var internalNameProp = eventInfo.GetType().GetProperty("InternalEventName", PublicInstance);
                        if (internalNameProp != null)
                        {
                            string name = internalNameProp.GetValue(eventInfo) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name.Replace("_", " ");
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetEventPageTitle failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get a summary of the event page (wins/losses/format) for screen context.
        /// </summary>
        public static string GetEventPageSummary()
        {
            try
            {
                var controller = FindEventPageController();
                if (controller == null) return null;

                var playerEvent = GetPlayerEvent(controller);
                if (playerEvent == null) return null;

                var peType = playerEvent.GetType();

                // Read CurrentWins and MaxWins
                var currentWinsProp = peType.GetProperty("CurrentWins", PublicInstance);
                var maxWinsProp = peType.GetProperty("MaxWins", PublicInstance);

                if (currentWinsProp != null && maxWinsProp != null)
                {
                    int wins = (int)currentWinsProp.GetValue(playerEvent);
                    int maxWins = (int)maxWinsProp.GetValue(playerEvent);

                    if (maxWins > 0)
                        return Strings.EventPageSummary(wins, maxWins);
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetEventPageSummary failed: {ex.Message}");
                return null;
            }
        }

        private static MonoBehaviour FindEventPageController()
        {
            // Return cached if still valid
            if (_cachedEventPageController != null)
            {
                try
                {
                    if (_cachedEventPageController.gameObject != null &&
                        _cachedEventPageController.gameObject.activeInHierarchy)
                        return _cachedEventPageController;
                }
                catch { }
                _cachedEventPageController = null;
            }

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "EventPageContentController")
                {
                    _cachedEventPageController = mb;

                    if (!_eventPageReflectionInit)
                        InitEventPageReflection(mb.GetType());

                    return mb;
                }
            }

            return null;
        }

        private static void InitEventPageReflection(Type type)
        {
            if (_eventPageReflectionInit) return;

            _currentEventContextField = type.GetField("_currentEventContext", PrivateInstance);

            _eventPageReflectionInit = true;

            MelonLogger.Msg($"[EventAccessor] EventPage reflection init: " +
                $"eventContext={_currentEventContextField != null}");
        }

        /// <summary>
        /// Get the IPlayerEvent from the active event page controller.
        /// Also lazily initializes PlayerEvent/EventInfo/EventUxInfo property info.
        /// </summary>
        private static object GetPlayerEvent(MonoBehaviour controller)
        {
            if (_currentEventContextField == null) return null;

            var eventContext = _currentEventContextField.GetValue(controller);
            if (eventContext == null) return null;

            // Lazy init PlayerEvent property
            if (_playerEventProp == null)
            {
                _playerEventProp = eventContext.GetType().GetProperty("PlayerEvent", PublicInstance);
                if (_playerEventProp == null) return null;
            }

            var playerEvent = _playerEventProp.GetValue(eventContext);
            if (playerEvent == null) return null;

            // Lazy init EventInfo and EventUXInfo props
            if (_eventInfoProp == null)
                _eventInfoProp = playerEvent.GetType().GetProperty("EventInfo", PublicInstance);
            if (_eventUxInfoProp == null)
                _eventUxInfoProp = playerEvent.GetType().GetProperty("EventUXInfo", PublicInstance);

            return playerEvent;
        }

        #endregion

        #region Packet Selection

        /// <summary>
        /// Get an enriched label for a packet option element.
        /// Walks parent chain to find JumpStartPacket component, reads localized name
        /// and color info from the controller's state data.
        /// Returns: "{name} ({colors})" or null.
        /// </summary>
        public static string GetPacketLabel(GameObject element)
        {
            if (element == null) return null;

            try
            {
                // Find the JumpStartPacket MonoBehaviour by walking up
                var packet = FindParentComponent(element, "JumpStartPacket");
                if (packet == null) return null;

                // Initialize JumpStartPacket reflection if needed
                if (!_jumpStartReflectionInit)
                    InitJumpStartReflection(packet.GetType());

                // Read localized display name from _packTitle (Localize -> TMP_Text)
                string displayName = ReadPacketDisplayName(packet);

                // Try to get color info from the controller's state
                string colorInfo = GetPacketColorInfo(packet);

                if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(colorInfo))
                    return $"{displayName} ({colorInfo})";
                if (!string.IsNullOrEmpty(displayName))
                    return displayName;

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetPacketLabel failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Build info blocks for a packet element, readable via Left/Right arrow navigation.
        /// Includes: packet name, colors, featured card info (from LandGrpId), and description text.
        /// </summary>
        public static System.Collections.Generic.List<CardInfoBlock> GetPacketInfoBlocks(GameObject element)
        {
            var blocks = new System.Collections.Generic.List<CardInfoBlock>();
            if (element == null) return blocks;

            try
            {
                // Find the JumpStartPacket
                var packet = FindParentComponent(element, "JumpStartPacket");
                if (packet == null) return blocks;

                if (!_jumpStartReflectionInit)
                    InitJumpStartReflection(packet.GetType());

                // Block 1: Packet name
                string displayName = ReadPacketDisplayName(packet);
                if (!string.IsNullOrEmpty(displayName))
                    blocks.Add(new CardInfoBlock(Strings.CardInfoName, displayName, isVerbose: false));

                // Block 2: Colors
                string colorInfo = GetPacketColorInfo(packet);
                if (!string.IsNullOrEmpty(colorInfo))
                    blocks.Add(new CardInfoBlock(Strings.ManaColorless.Contains("Farblos") ? "Farben" : "Colors", colorInfo));

                // Block 3+: Featured card from LandGrpId via CardModelProvider
                uint landGrpId = GetPacketLandGrpId(packet);
                if (landGrpId > 0)
                {
                    var cardInfo = CardModelProvider.GetCardInfoFromGrpId(landGrpId);
                    if (cardInfo.HasValue && cardInfo.Value.IsValid)
                    {
                        var cardBlocks = CardDetector.BuildInfoBlocks(cardInfo.Value);
                        // Prefix each card block label with "Card" context
                        foreach (var cb in cardBlocks)
                            blocks.Add(cb);
                    }
                    else
                    {
                        // Fallback: at least show card name
                        string cardName = CardModelProvider.GetNameFromGrpId(landGrpId);
                        if (!string.IsNullOrEmpty(cardName))
                            blocks.Add(new CardInfoBlock(Strings.CardInfoName, cardName));
                    }
                }

                // Remaining blocks: description text from controller
                var controller = FindPacketController();
                if (controller != null)
                {
                    var seenTexts = new System.Collections.Generic.HashSet<string>();
                    foreach (var block in blocks)
                        seenTexts.Add(block.Content);

                    foreach (var tmp in controller.GetComponentsInChildren<TMPro.TMP_Text>(false))
                    {
                        if (tmp == null) continue;
                        string text = UITextExtractor.CleanText(tmp.text);
                        if (string.IsNullOrWhiteSpace(text) || text.Length < 20) continue;
                        if (seenTexts.Contains(text)) continue;

                        // Skip if this text is inside a JumpStartPacket
                        bool insidePacket = false;
                        Transform current = tmp.transform;
                        while (current != null && current != controller.transform)
                        {
                            foreach (var mb in current.GetComponents<MonoBehaviour>())
                            {
                                if (mb != null && mb.GetType().Name == "JumpStartPacket")
                                { insidePacket = true; break; }
                            }
                            if (insidePacket) break;
                            current = current.parent;
                        }
                        if (insidePacket) continue;

                        seenTexts.Add(text);
                        blocks.Add(new CardInfoBlock("Description", text));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetPacketInfoBlocks failed: {ex.Message}");
            }

            return blocks;
        }

        /// <summary>
        /// Get the LandGrpId for a JumpStartPacket by looking up its PacketDetails.
        /// </summary>
        private static uint GetPacketLandGrpId(MonoBehaviour packet)
        {
            try
            {
                var controller = FindPacketController();
                if (controller == null || _packetToIdField == null || _currentStateField == null)
                    return 0;

                // Get packet ID from _packetToId dictionary
                var dict = _packetToIdField.GetValue(controller);
                if (dict == null) return 0;

                // Use IDictionary to find the packet's ID
                string packetId = null;
                foreach (System.Collections.DictionaryEntry entry in (System.Collections.IDictionary)dict)
                {
                    if (entry.Key == (object)packet)
                    {
                        packetId = entry.Value as string;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(packetId)) return 0;

                // Get current state and look up PacketDetails
                var state = _currentStateField.GetValue(controller);
                if (state == null) return 0;

                // Access PacketOptions field on ServiceState struct
                var stateType = state.GetType();
                var optionsField = stateType.GetField("PacketOptions");
                if (optionsField == null) return 0;

                var options = optionsField.GetValue(state) as System.Array;
                if (options == null) return 0;

                foreach (var option in options)
                {
                    var pidField = option.GetType().GetField("PacketId");
                    var grpIdField = option.GetType().GetField("LandGrpId");
                    if (pidField == null || grpIdField == null) continue;

                    string pid = pidField.GetValue(option) as string;
                    if (pid == packetId)
                    {
                        var val = grpIdField.GetValue(option);
                        if (val is uint grpId) return grpId;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetPacketLandGrpId failed: {ex.Message}");
            }
            return 0;
        }

        /// <summary>
        /// Check if a GameObject is inside a JumpStartPacket.
        /// Used by GeneralMenuNavigator to detect packet elements for info block navigation.
        /// </summary>
        public static bool IsInsideJumpStartPacket(GameObject element)
        {
            if (element == null) return false;
            return FindParentComponent(element, "JumpStartPacket") != null;
        }

        /// <summary>
        /// Get screen-level packet summary: "Packet 1 of 2" etc.
        /// </summary>
        public static string GetPacketScreenSummary()
        {
            try
            {
                var controller = FindPacketController();
                if (controller == null) return null;

                if (_currentStateField == null) return null;

                var state = _currentStateField.GetValue(controller);
                if (state == null) return null;

                // SubmissionCount() returns uint
                var submissionCountMethod = state.GetType().GetMethod("SubmissionCount",
                    BindingFlags.Public | BindingFlags.Instance);
                if (submissionCountMethod != null)
                {
                    object result = submissionCountMethod.Invoke(state, null);
                    int submitted = Convert.ToInt32(result);
                    int current = submitted + 1;
                    return Strings.PacketOf(current, 2);
                }

                // Fallback: check SubmittedPackets array length
                var submittedField = state.GetType().GetField("SubmittedPackets", PublicInstance);
                if (submittedField != null)
                {
                    var submitted = submittedField.GetValue(state) as Array;
                    if (submitted != null)
                    {
                        // Count non-default entries
                        int count = 0;
                        foreach (var entry in submitted)
                        {
                            if (entry != null && !entry.Equals(Activator.CreateInstance(entry.GetType())))
                                count++;
                        }
                        return Strings.PacketOf(count + 1, 2);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetPacketScreenSummary failed: {ex.Message}");
                return null;
            }
        }

        private static MonoBehaviour FindPacketController()
        {
            if (_cachedPacketController != null)
            {
                try
                {
                    if (_cachedPacketController.gameObject != null &&
                        _cachedPacketController.gameObject.activeInHierarchy)
                        return _cachedPacketController;
                }
                catch { }
                _cachedPacketController = null;
            }

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == "PacketSelectContentController")
                {
                    _cachedPacketController = mb;

                    if (!_packetReflectionInit)
                        InitPacketReflection(mb.GetType());

                    return mb;
                }
            }

            return null;
        }

        private static void InitPacketReflection(Type type)
        {
            if (_packetReflectionInit) return;

            _packetOptionsField = type.GetField("_packetOptions", PrivateInstance);
            _selectedPackIdField = type.GetField("_selectedPackId", PrivateInstance);
            _currentStateField = type.GetField("_currentState", PrivateInstance);
            _packetToIdField = type.GetField("_packetToId", PrivateInstance);
            _headerTextField = type.GetField("_headerText", PrivateInstance);

            _packetReflectionInit = true;

            MelonLogger.Msg($"[EventAccessor] Packet reflection init: " +
                $"options={_packetOptionsField != null}, selected={_selectedPackIdField != null}, " +
                $"state={_currentStateField != null}, toId={_packetToIdField != null}, " +
                $"header={_headerTextField != null}");
        }

        private static void InitJumpStartReflection(Type type)
        {
            if (_jumpStartReflectionInit) return;

            _packTitleField = type.GetField("_packTitle", PrivateInstance);

            _jumpStartReflectionInit = true;

            MelonLogger.Msg($"[EventAccessor] JumpStartPacket reflection init: " +
                $"packTitle={_packTitleField != null}");
        }

        /// <summary>
        /// Read the localized display name from JumpStartPacket's _packTitle (Localize -> TMP_Text).
        /// </summary>
        private static string ReadPacketDisplayName(MonoBehaviour packet)
        {
            if (_packTitleField == null) return null;

            var localizeComp = _packTitleField.GetValue(packet) as MonoBehaviour;
            if (localizeComp == null) return null;

            var tmp = localizeComp.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                return UITextExtractor.CleanText(tmp.text);

            return null;
        }

        /// <summary>
        /// Get color info for a JumpStartPacket by looking up its PacketDetails
        /// via the controller's _packetToId dictionary and _currentState.
        /// </summary>
        private static string GetPacketColorInfo(MonoBehaviour packet)
        {
            var controller = FindPacketController();
            if (controller == null || _packetToIdField == null || _currentStateField == null)
                return null;

            try
            {
                // Get the packet ID from _packetToId dictionary
                var packetToId = _packetToIdField.GetValue(controller);
                if (packetToId == null) return null;

                // Use IDictionary to access the dictionary generically
                // _packetToId is Dictionary<JumpStartPacket, string>
                // We need to check if our packet is a key
                string packetId = null;
                var tryGetMethod = packetToId.GetType().GetMethod("TryGetValue");
                if (tryGetMethod != null)
                {
                    var args = new object[] { packet, null };
                    bool found = (bool)tryGetMethod.Invoke(packetToId, args);
                    if (found)
                        packetId = args[1] as string;
                }

                if (string.IsNullOrEmpty(packetId)) return null;

                // Get PacketDetails from _currentState
                var state = _currentStateField.GetValue(controller);
                if (state == null) return null;

                var getDetailsMethod = state.GetType().GetMethod("GetDetailsById", PublicInstance);
                if (getDetailsMethod == null) return null;

                var details = getDetailsMethod.Invoke(state, new object[] { packetId });
                if (details == null) return null;

                // Read RawColors (string[] field on PacketDetails struct)
                var rawColorsField = details.GetType().GetField("RawColors", PublicInstance);
                if (rawColorsField == null) return null;

                var rawColors = rawColorsField.GetValue(details) as string[];
                return TranslateManaColors(rawColors);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[EventAccessor] GetPacketColorInfo failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Translate raw mana color codes (e.g., ["W", "U"]) to readable color names.
        /// </summary>
        private static string TranslateManaColors(string[] rawColors)
        {
            if (rawColors == null || rawColors.Length == 0) return null;

            var names = new System.Collections.Generic.List<string>();
            foreach (string color in rawColors)
            {
                if (string.IsNullOrEmpty(color)) continue;
                switch (color.ToUpper())
                {
                    case "W": names.Add(Strings.ManaWhite); break;
                    case "U": names.Add(Strings.ManaBlue); break;
                    case "B": names.Add(Strings.ManaBlack); break;
                    case "R": names.Add(Strings.ManaRed); break;
                    case "G": names.Add(Strings.ManaGreen); break;
                    case "C": names.Add(Strings.ManaColorless); break;
                }
            }

            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Walk parent chain to find a MonoBehaviour of the given type name.
        /// </summary>
        private static MonoBehaviour FindParentComponent(GameObject element, string typeName)
        {
            Transform current = element.transform;
            while (current != null)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == typeName)
                        return mb;
                }
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Clear cached component references. Call on scene changes.
        /// </summary>
        public static void ClearCache()
        {
            _cachedEventPageController = null;
            _cachedPacketController = null;
        }

        #endregion
    }
}
