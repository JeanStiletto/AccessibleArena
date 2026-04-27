using UnityEngine;
using System;
using System.Collections;
using System.Reflection;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;
using T = AccessibleArena.Core.Constants.GameTypeNames;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides reflection-based access to event tiles, event page, and packet selection.
    /// Used for enriching accessibility labels with event status, progress, and packet info.
    /// Follows the same pattern as RecentPlayAccessor.
    /// </summary>
    public static class EventAccessor
    {

        private sealed class TileHandles
        {
            public FieldInfo TitleText;       // _titleText (Localize)
            public FieldInfo RankImage;       // _rankImage (Image, optional)
            public FieldInfo Bo3Indicator;    // _bestOf3Indicator (RectTransform, optional)
            public FieldInfo AttractParent;   // _attractParent (RectTransform, optional)
            public FieldInfo ProgressPips;    // _eventProgressPips (RectTransform, optional)
        }

        private sealed class EventPageHandles
        {
            public FieldInfo CurrentEventContext; // _currentEventContext (EventContext)
        }

        private sealed class FactionalizedEventHandles
        {
            public FieldInfo EventContexts;       // _eventContexts (Dictionary<string, FactionEventContext>)
            public FieldInfo CurrentKey;          // _currentEventContextKey (string)
        }

        private sealed class FactionEventContextHandles
        {
            public PropertyInfo EventContext;     // FactionEventContext.EventContext (auto-property)
        }

        private sealed class EventContextHandles
        {
            public FieldInfo PlayerEvent;         // EventContext.PlayerEvent (field)
        }

        private sealed class PlayerEventHandles
        {
            public PropertyInfo EventInfo;        // optional
            public PropertyInfo EventUxInfo;      // optional
        }

        private sealed class PacketHandles
        {
            public FieldInfo CurrentState;        // _currentState (ServiceState)
            public FieldInfo PacketToId;          // _packetToId (Dictionary)
        }

        private sealed class JumpStartHandles
        {
            public FieldInfo PackTitle;           // _packTitle (Localize)
        }

        private sealed class CampaignGraphHandles
        {
            public FieldInfo Strategy;            // _strategy (IColorChallengeStrategy)
        }

        private sealed class MainButtonHandles
        {
            // All payment button fields on MainButtonComponent. Currency is derived from each
            // field's name via CurrencyLabels.FromFieldName; non-currency fields (Play/Start,
            // EventToken which carries its own localized text) return null and are skipped.
            public FieldInfo[] ButtonFields;
        }

        private static readonly ReflectionCache<TileHandles> _tileCache = new ReflectionCache<TileHandles>(
            builder: t => new TileHandles
            {
                TitleText = t.GetField("_titleText", PrivateInstance),
                RankImage = t.GetField("_rankImage", PrivateInstance),
                Bo3Indicator = t.GetField("_bestOf3Indicator", PrivateInstance),
                AttractParent = t.GetField("_attractParent", PrivateInstance),
                ProgressPips = t.GetField("_eventProgressPips", PrivateInstance),
            },
            validator: h => h.TitleText != null,
            logTag: "EventAccessor",
            logSubject: "PlayBladeEventTile");

        private static readonly ReflectionCache<EventPageHandles> _eventPageCache = new ReflectionCache<EventPageHandles>(
            builder: t => new EventPageHandles
            {
                CurrentEventContext = t.GetField("_currentEventContext", PrivateInstance),
            },
            validator: h => h.CurrentEventContext != null,
            logTag: "EventAccessor",
            logSubject: "EventPageContentController");

        private static readonly ReflectionCache<FactionalizedEventHandles> _factionalizedCache = new ReflectionCache<FactionalizedEventHandles>(
            builder: t => new FactionalizedEventHandles
            {
                EventContexts = t.GetField("_eventContexts", PrivateInstance),
                CurrentKey = t.GetField("_currentEventContextKey", PrivateInstance),
            },
            validator: h => h.EventContexts != null && h.CurrentKey != null,
            logTag: "EventAccessor",
            logSubject: "FactionalizedEventTemplate");

        private static readonly ReflectionCache<FactionEventContextHandles> _factionEventContextCache = new ReflectionCache<FactionEventContextHandles>(
            builder: t => new FactionEventContextHandles
            {
                EventContext = t.GetProperty("EventContext", PublicInstance),
            },
            validator: h => h.EventContext != null,
            logTag: "EventAccessor",
            logSubject: "FactionEventContext");

        private static readonly ReflectionCache<EventContextHandles> _eventContextCache = new ReflectionCache<EventContextHandles>(
            builder: t => new EventContextHandles
            {
                PlayerEvent = t.GetField("PlayerEvent", PublicInstance),
            },
            validator: h => h.PlayerEvent != null,
            logTag: "EventAccessor",
            logSubject: "EventContext");

        private static readonly ReflectionCache<PlayerEventHandles> _playerEventCache = new ReflectionCache<PlayerEventHandles>(
            builder: t => new PlayerEventHandles
            {
                EventInfo = t.GetProperty("EventInfo", PublicInstance),
                EventUxInfo = t.GetProperty("EventUXInfo", PublicInstance),
            },
            validator: _ => true,
            logTag: "EventAccessor",
            logSubject: "IPlayerEvent");

        private static readonly ReflectionCache<PacketHandles> _packetCache = new ReflectionCache<PacketHandles>(
            builder: t => new PacketHandles
            {
                CurrentState = t.GetField("_currentState", PrivateInstance),
                PacketToId = t.GetField("_packetToId", PrivateInstance),
            },
            validator: h => h.CurrentState != null && h.PacketToId != null,
            logTag: "EventAccessor",
            logSubject: "PacketSelectContentController");

        private static readonly ReflectionCache<JumpStartHandles> _jumpStartCache = new ReflectionCache<JumpStartHandles>(
            builder: t => new JumpStartHandles
            {
                PackTitle = t.GetField("_packTitle", PrivateInstance),
            },
            validator: h => h.PackTitle != null,
            logTag: "EventAccessor",
            logSubject: "JumpStartPacket");

        private static readonly ReflectionCache<CampaignGraphHandles> _campaignGraphCache = new ReflectionCache<CampaignGraphHandles>(
            builder: t => new CampaignGraphHandles
            {
                Strategy = t.GetField("_strategy", PrivateInstance),
            },
            validator: h => h.Strategy != null,
            logTag: "EventAccessor",
            logSubject: "CampaignGraphContentController");

        private static readonly ReflectionCache<MainButtonHandles> _mainButtonCache = new ReflectionCache<MainButtonHandles>(
            builder: t => new MainButtonHandles
            {
                ButtonFields = new[]
                {
                    t.GetField("_payWithGemsButton", PrivateInstance),
                    t.GetField("_payWithGoldButton", PrivateInstance),
                    t.GetField("_payWithEventTokenButton", PrivateInstance),
                    t.GetField("_playButton", PrivateInstance),
                    t.GetField("_startButton", PrivateInstance),
                },
            },
            validator: h => h.ButtonFields != null && System.Array.Exists(h.ButtonFields, f => f != null),
            logTag: "EventAccessor",
            logSubject: "MainButtonComponent");

        // Cached component references (invalidated on scene change)
        private static MonoBehaviour _cachedEventPageController;
        private static MonoBehaviour _cachedFactionalizedController;
        private static MonoBehaviour _cachedPacketController;
        private static MonoBehaviour _cachedCampaignGraphController;

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

                if (!_tileCache.EnsureInitialized(tile.GetType()))
                    return null;

                var th = _tileCache.Handles;

                // Read title text from _titleText (Localize -> TMP_Text)
                string title = ReadLocalizeText(th.TitleText, tile);
                if (string.IsNullOrEmpty(title)) return null;

                // Build enriched label
                var parts = new System.Collections.Generic.List<string>();
                parts.Add(title);

                // Check if in progress (_attractParent active)
                if (IsRectTransformActive(tile, th.AttractParent))
                {
                    // Check progress pips
                    string progress = ReadProgressFromPips(tile, th.ProgressPips);
                    if (!string.IsNullOrEmpty(progress))
                        parts.Add(progress);
                    else
                        parts.Add(Strings.EventTileInProgress);
                }

                // Check ranked
                if (IsImageActive(tile, th.RankImage))
                    parts.Add(Strings.EventTileRanked);

                // Check Bo3
                if (IsRectTransformActive(tile, th.Bo3Indicator))
                    parts.Add(Strings.EventTileBo3);

                return string.Join(", ", parts);
            }
            catch (Exception ex)
            {
                Log.Error("EventAccessor", $"GetEventTileLabel failed: {ex.Message}");
                return null;
            }
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
        private static string ReadProgressFromPips(MonoBehaviour tile, FieldInfo pipsField)
        {
            if (pipsField == null) return null;

            var pipsParent = pipsField.GetValue(tile) as RectTransform;
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
                var controller = FindActiveEventController();
                if (controller == null) return null;

                var playerEvent = GetPlayerEvent(controller);
                if (playerEvent == null) return null;

                var peh = _playerEventCache.Handles;

                // Try EventUXInfo.PublicEventName first (localized display name)
                if (peh?.EventUxInfo != null)
                {
                    var uxInfo = peh.EventUxInfo.GetValue(playerEvent);
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
                if (peh?.EventInfo != null)
                {
                    var eventInfo = peh.EventInfo.GetValue(playerEvent);
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
                Log.Error("EventAccessor", $"GetEventPageTitle failed: {ex.Message}");
                return null;
            }
        }

        private static MonoBehaviour FindEventPageController()
            => FindCachedController(ref _cachedEventPageController, T.EventPageContentController, _eventPageCache);

        private static MonoBehaviour FindFactionalizedEventTemplate()
            => FindCachedController(ref _cachedFactionalizedController, T.FactionalizedEventTemplate, _factionalizedCache);

        /// <summary>
        /// Find whichever event-page controller is currently active. Supports both the
        /// classic <c>EventPageContentController</c> and the V2 <c>FactionalizedEventTemplate</c>
        /// (used by Sealed/Faction events).
        /// </summary>
        private static MonoBehaviour FindActiveEventController()
            => FindEventPageController() ?? FindFactionalizedEventTemplate();

        /// <summary>
        /// Get the IPlayerEvent from the active event page controller.
        /// Also initializes EventContext/PlayerEvent caches against their runtime types.
        /// Handles both V1 (single _currentEventContext field) and V2 (Dictionary keyed by _currentEventContextKey).
        /// </summary>
        private static object GetPlayerEvent(MonoBehaviour controller)
        {
            object eventContext = ExtractEventContext(controller);
            if (eventContext == null) return null;

            if (!_eventContextCache.EnsureInitialized(eventContext.GetType()))
                return null;

            var playerEvent = _eventContextCache.Handles.PlayerEvent.GetValue(eventContext);
            if (playerEvent == null) return null;

            _playerEventCache.EnsureInitialized(playerEvent.GetType());

            return playerEvent;
        }

        /// <summary>
        /// Extract the EventContext object from either the V1 (_currentEventContext) or
        /// V2 (_eventContexts[_currentEventContextKey].EventContext) controller.
        /// </summary>
        private static object ExtractEventContext(MonoBehaviour controller)
        {
            if (controller == null) return null;

            // V1: EventPageContentController._currentEventContext
            if (_eventPageCache.IsInitialized && controller.GetType().Name == T.EventPageContentController)
                return _eventPageCache.Handles.CurrentEventContext.GetValue(controller);

            // V2: FactionalizedEventTemplate._eventContexts[_currentEventContextKey].EventContext
            if (_factionalizedCache.IsInitialized && controller.GetType().Name == T.FactionalizedEventTemplate)
            {
                var fh = _factionalizedCache.Handles;
                string key = fh.CurrentKey.GetValue(controller) as string;
                if (string.IsNullOrEmpty(key)) return null;

                var dict = fh.EventContexts.GetValue(controller) as IDictionary;
                if (dict == null || !dict.Contains(key)) return null;

                var factionContext = dict[key];
                if (factionContext == null) return null;

                if (!_factionEventContextCache.EnsureInitialized(factionContext.GetType()))
                    return null;

                return _factionEventContextCache.Handles.EventContext.GetValue(factionContext);
            }

            return null;
        }

        /// <summary>
        /// Get navigable info blocks from the event page's text content.
        /// Scans all active TMP_Text in the EventPageContentController hierarchy,
        /// filters out button text and objective/progress milestones, and splits
        /// long texts on newlines for screen reader readability.
        /// </summary>
        public static System.Collections.Generic.List<CardInfoBlock> GetEventPageInfoBlocks()
        {
            var blocks = new System.Collections.Generic.List<CardInfoBlock>();

            try
            {
                var controller = FindActiveEventController();
                if (controller == null) return blocks;

                var seenTexts = new System.Collections.Generic.HashSet<string>();
                string label = Strings.EventInfoLabel;

                // Get event title to filter out redundant name-only blocks
                string eventTitle = GetEventPageTitle();

                foreach (var tmp in controller.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (tmp == null) continue;

                    string text = UITextExtractor.CleanText(tmp.text);
                    if (string.IsNullOrWhiteSpace(text) || text.Length < 5) continue;

                    // Skip text inside CustomButton parent chain (buttons)
                    if (IsInsideComponent(tmp.transform, controller.transform, "CustomButton"))
                        continue;
                    if (IsInsideComponent(tmp.transform, controller.transform, "CustomButtonWithTooltip"))
                        continue;

                    // V2 faction tiles: the Localize text TMP is a sibling of the CustomButton
                    // (not a descendant), so the CustomButton check above doesn't catch it.
                    // Skip anything inside a FactionalizedEventBladeItem — those names are
                    // already announced via the navigable button itself.
                    if (IsInsideComponent(tmp.transform, controller.transform, "FactionalizedEventBladeItem"))
                        continue;

                    // Skip text inside GameObjects with "Objective" in name (progress milestones)
                    if (IsInsideNamedParent(tmp.transform, controller.transform, "Objective"))
                        continue;

                    // Split long texts on newlines for readability
                    var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length < 5) continue;
                        if (seenTexts.Contains(trimmed)) continue;

                        // Skip short blocks that look like the event name (already in screen title)
                        // Uses fuzzy matching: if <=4 words and shares 1/3 of words with title, skip
                        if (!string.IsNullOrEmpty(eventTitle) && IsRedundantTitle(trimmed, eventTitle))
                            continue;

                        seenTexts.Add(trimmed);
                        blocks.Add(new CardInfoBlock(label, trimmed, isVerbose: false));
                    }
                }

                Log.Msg("EventAccessor", $"GetEventPageInfoBlocks: {blocks.Count} blocks");
            }
            catch (Exception ex)
            {
                Log.Error("EventAccessor", $"GetEventPageInfoBlocks failed: {ex.Message}");
            }

            return blocks;
        }

        /// <summary>
        /// Check if a transform is inside a parent with a component of the given type name.
        /// Walks up from child to stopAt (exclusive).
        /// </summary>
        private static bool IsInsideComponent(Transform child, Transform stopAt, string typeName)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == typeName)
                        return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Check if a transform is inside a parent whose GameObject name contains the given substring.
        /// Walks up from child to stopAt (exclusive).
        /// </summary>
        private static bool IsInsideNamedParent(Transform child, Transform stopAt, string nameSubstring)
        {
            Transform current = child.parent;
            while (current != null && current != stopAt)
            {
                if (current.gameObject.name.Contains(nameSubstring))
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Check if a text block is a redundant event title that should be filtered.
        /// True if the block is short (max 4 words) and shares at least 1/3 of its
        /// words with the event title. Handles abbreviated expansion names.
        /// </summary>
        private static bool IsRedundantTitle(string blockText, string eventTitle)
        {
            // Split into words, stripping punctuation like colons and hyphens
            char[] separators = { ' ', ':', '-', '_', '\u2013', '\u2014' };
            var blockWords = blockText.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (blockWords.Length == 0 || blockWords.Length > 4) return false;

            var titleWords = eventTitle.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (titleWords.Length == 0) return false;

            // Count how many block words appear in the title
            int matches = 0;
            foreach (string bw in blockWords)
            {
                foreach (string tw in titleWords)
                {
                    if (string.Equals(bw, tw, StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                        break;
                    }
                }
            }

            // At least 1/3 of the block words must match
            return matches > 0 && matches >= (blockWords.Length + 2) / 3;
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

                if (!_jumpStartCache.EnsureInitialized(packet.GetType()))
                    return null;

                // Read localized display name from _packTitle (Localize -> TMP_Text)
                string displayName = ReadLocalizeText(_jumpStartCache.Handles.PackTitle, packet);

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
                Log.Error("EventAccessor", $"GetPacketLabel failed: {ex.Message}");
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

                if (!_jumpStartCache.EnsureInitialized(packet.GetType()))
                    return blocks;

                // Block 1: Packet name
                string displayName = ReadLocalizeText(_jumpStartCache.Handles.PackTitle, packet);
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
                Log.Error("EventAccessor", $"GetPacketInfoBlocks failed: {ex.Message}");
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
                if (controller == null || !_packetCache.IsInitialized)
                    return 0;

                var ph = _packetCache.Handles;

                // Get packet ID from _packetToId dictionary
                var dict = ph.PacketToId.GetValue(controller);
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
                var state = ph.CurrentState.GetValue(controller);
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
                Log.Error("EventAccessor", $"GetPacketLandGrpId failed: {ex.Message}");
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
        /// Get the JumpStartPacket tile root for a given element.
        /// Used to sort packet elements by their tile's position rather than the child
        /// element's offset position, which may not reflect the visual grid order.
        /// Returns null if not inside a packet.
        /// </summary>
        public static GameObject GetJumpStartPacketRoot(GameObject element)
        {
            if (element == null) return null;
            var packet = FindParentComponent(element, "JumpStartPacket");
            return packet?.gameObject;
        }

        /// <summary>
        /// Click a packet element by finding the PacketInput on the parent JumpStartPacket
        /// and invoking its OnClick method. UIActivator's pointer simulation doesn't reach
        /// CustomTouchButton on the JumpStartPacket GO because the navigable element is MainButton (child).
        /// </summary>
        public static bool ClickPacket(GameObject element)
        {
            if (element == null) return false;

            try
            {
                var packet = FindParentComponent(element, "JumpStartPacket");
                if (packet == null) return false;

                // Find PacketInput component on the same GO
                MonoBehaviour packetInput = null;
                foreach (var mb in packet.GetComponents<MonoBehaviour>())
                {
                    if (mb != null && mb.GetType().Name == "PacketInput")
                    {
                        packetInput = mb;
                        break;
                    }
                }
                if (packetInput == null)
                {
                    Log.Warn("EventAccessor", "PacketInput not found on JumpStartPacket");
                    return false;
                }

                // Invoke OnClick() which fires Clicked?.Invoke(_pack)
                var onClickMethod = packetInput.GetType().GetMethod("OnClick",
                    PrivateInstance);
                if (onClickMethod != null)
                {
                    onClickMethod.Invoke(packetInput, null);
                    Log.Msg("EventAccessor", "Packet click invoked via PacketInput.OnClick");
                    return true;
                }
                else
                {
                    Log.Warn("EventAccessor", "PacketInput.OnClick method not found");
                }
            }
            catch (Exception ex)
            {
                Log.Error("EventAccessor", $"ClickPacket failed: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Get screen-level packet summary: "Packet 1 of 2" etc.
        /// </summary>
        public static string GetPacketScreenSummary()
        {
            try
            {
                var controller = FindPacketController();
                if (controller == null || !_packetCache.IsInitialized) return null;

                var state = _packetCache.Handles.CurrentState.GetValue(controller);
                if (state == null) return null;

                // SubmissionCount() returns uint
                var submissionCountMethod = state.GetType().GetMethod("SubmissionCount",
                    PublicInstance);
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
                Log.Error("EventAccessor", $"GetPacketScreenSummary failed: {ex.Message}");
                return null;
            }
        }

        private static MonoBehaviour FindPacketController()
            => FindCachedController(ref _cachedPacketController, T.PacketSelectContentController, _packetCache);

        /// <summary>
        /// Get color info for a JumpStartPacket by looking up its PacketDetails
        /// via the controller's _packetToId dictionary and _currentState.
        /// </summary>
        private static string GetPacketColorInfo(MonoBehaviour packet)
        {
            var controller = FindPacketController();
            if (controller == null || !_packetCache.IsInitialized)
                return null;

            var ph = _packetCache.Handles;

            try
            {
                // Get the packet ID from _packetToId dictionary
                var packetToId = ph.PacketToId.GetValue(controller);
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
                var state = ph.CurrentState.GetValue(controller);
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
                Log.Error("EventAccessor", $"GetPacketColorInfo failed: {ex.Message}");
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

        #region Color Challenge

        /// <summary>
        /// Get progress summaries for all Color Challenge tracks, keyed by localized color name.
        /// E.g. {"Weiß" → "3 von 5 Knoten freigeschaltet", "Blau" → "Abschluss abgeschlossen"}.
        /// Used by GeneralMenuNavigator to enrich color button labels after element discovery.
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, string> GetAllTrackSummaries()
        {
            var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var controller = FindCampaignGraphController();
                if (controller == null || !_campaignGraphCache.IsInitialized) return result;

                var strategy = _campaignGraphCache.Handles.Strategy.GetValue(controller);
                if (strategy == null) return result;

                var tracksDict = strategy.GetType()
                    .GetProperty("Tracks", PublicInstance)?.GetValue(strategy) as IDictionary;
                if (tracksDict == null || tracksDict.Count == 0) return result;

                foreach (DictionaryEntry entry in tracksDict)
                {
                    var track = entry.Value;
                    if (track == null) continue;

                    var trackType = track.GetType();
                    string trackKey = entry.Key as string;

                    bool completed = (bool)(trackType.GetProperty("Completed", PublicInstance)?.GetValue(track) ?? false);
                    int unlocked = (int)(trackType.GetProperty("UnlockedMatchNodeCount", PublicInstance)?.GetValue(track) ?? 0);

                    int total = 0;
                    int aiCount = 0, pvpCount = 0;
                    var nodesProp = trackType.GetProperty("Nodes", PublicInstance);
                    if (nodesProp != null)
                    {
                        var nodes = nodesProp.GetValue(track) as IList;
                        if (nodes != null)
                        {
                            total = nodes.Count;
                            FieldInfo pvpField = null;
                            foreach (var node in nodes)
                            {
                                if (node == null) continue;
                                if (pvpField == null)
                                    pvpField = node.GetType().GetField("IsPvpMatch", PublicInstance);
                                if (pvpField != null)
                                {
                                    bool isPvp = (bool)pvpField.GetValue(node);
                                    if (isPvp) pvpCount++;
                                    else aiCount++;
                                }
                            }
                        }
                    }

                    string summary = Strings.ColorChallengeProgress(null, unlocked, total, completed, aiCount, pvpCount);
                    if (string.IsNullOrEmpty(summary)) continue;

                    // Map track key (e.g. "white") to localized color name (e.g. "Weiß")
                    string localizedColor = MapToLocalizedColor(trackKey);
                    if (!string.IsNullOrEmpty(localizedColor))
                        result[localizedColor] = summary;

                    // Also add under raw key/name for English or direct matches
                    if (!string.IsNullOrEmpty(trackKey))
                        result[trackKey] = summary;
                }

                Log.Msg("EventAccessor", $"GetAllTrackSummaries: {result.Count} entries");
            }
            catch (Exception ex)
            {
                Log.Error("EventAccessor", $"GetAllTrackSummaries failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Map English color name to the localized mana color string.
        /// Returns null if the key is not a recognized color.
        /// </summary>
        private static string MapToLocalizedColor(string colorKey)
        {
            if (string.IsNullOrEmpty(colorKey)) return null;
            switch (colorKey.ToLower())
            {
                case "white": return Strings.ManaWhite;
                case "blue": return Strings.ManaBlue;
                case "black": return Strings.ManaBlack;
                case "red": return Strings.ManaRed;
                case "green": return Strings.ManaGreen;
                default: return null;
            }
        }

        /// <summary>
        /// Read text from a Localize component field (gets its TMP_Text child).
        /// </summary>
        private static string ReadLocalizeText(FieldInfo field, MonoBehaviour owner)
        {
            if (field == null) return null;
            var localizeComp = field.GetValue(owner) as MonoBehaviour;
            if (localizeComp == null) return null;
            var tmp = localizeComp.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                return UITextExtractor.CleanText(tmp.text);
            return null;
        }

        private static MonoBehaviour FindCampaignGraphController()
            => FindCachedController(ref _cachedCampaignGraphController, T.CampaignGraphContentController, _campaignGraphCache);

        #endregion

        #region Utility

        /// <summary>
        /// Find an active scene MonoBehaviour by its type name, caching the result.
        /// Validates the cache (destroyed objects are cleared). On first discovery,
        /// initializes the associated <see cref="ReflectionCache{THandles}"/> against
        /// the concrete type.
        /// </summary>
        private static MonoBehaviour FindCachedController<THandles>(
            ref MonoBehaviour cache,
            string typeName,
            ReflectionCache<THandles> reflectionCache) where THandles : class, new()
        {
            if (cache != null)
            {
                try
                {
                    if (cache.gameObject != null && cache.gameObject.activeInHierarchy)
                        return cache;
                }
                catch { /* Cached object may have been destroyed */ }
                cache = null;
            }

            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetType().Name == typeName)
                {
                    cache = mb;
                    reflectionCache.EnsureInitialized(mb.GetType());
                    return mb;
                }
            }

            return null;
        }

        #region Event Payment Button Enrichment

        /// <summary>
        /// Get an enriched label for an event-page payment button (Pay with Gems / Gold / Event Token).
        /// The game writes only the numeric quantity to the button text and conveys the currency
        /// via an icon, which is invisible to screen readers. This method walks parent chain to a
        /// MainButtonComponent, identifies which button field the element belongs to, and prefixes
        /// the price with the localized currency name.
        /// Returns null if not a payment button (Play / Start states already have proper labels).
        /// </summary>
        public static string GetEventPaymentButtonLabel(GameObject element)
        {
            if (element == null) return null;

            try
            {
                var component = FindParentComponent(element, T.MainButtonComponent);
                if (component == null) return null;

                if (!_mainButtonCache.EnsureInitialized(component.GetType()))
                    return null;

                // Find which of the component's button fields the element belongs to,
                // then ask CurrencyLabels for the matching localized currency name.
                // Returns null for Play/Start/EventToken — those carry their own localized text.
                foreach (var field in _mainButtonCache.Handles.ButtonFields)
                {
                    if (field == null) continue;
                    if (!IsElementInButtonField(element, component, field)) continue;

                    string currencyName = CurrencyLabels.FromFieldName(field.Name);
                    if (currencyName == null) return null;

                    var tmp = element.GetComponentInChildren<TMPro.TMP_Text>(true);
                    string priceText = tmp != null ? tmp.text?.Trim() : null;
                    return CurrencyLabels.FormatPrice(priceText, currencyName);
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error("EventAccessor", $"GetEventPaymentButtonLabel failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check whether the element is the same GameObject as the field's button or a descendant of it.
        /// </summary>
        private static bool IsElementInButtonField(GameObject element, MonoBehaviour component, FieldInfo field)
        {
            var button = field.GetValue(component) as Component;
            if (button == null) return false;

            Transform target = button.transform;
            Transform current = element.transform;
            while (current != null)
            {
                if (current == target) return true;
                current = current.parent;
            }
            return false;
        }

        #endregion

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
            _cachedFactionalizedController = null;
            _cachedPacketController = null;
            _cachedCampaignGraphController = null;
        }

        #endregion
    }
}
