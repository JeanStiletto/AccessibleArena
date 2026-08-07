using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AccessibleArena.Core.Utils;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    internal static class OpponentRevealAnnouncementBuilder
    {
        private const string LogTag = "OpponentRevealAnnouncementBuilder";

        private sealed class EventHandles
        {
            public FieldInfo RevealEvents;
        }

        private sealed class RecordHandles
        {
            public FieldInfo EventType;
            public FieldInfo OwnerId;
            public FieldInfo RevealedInstance;
        }

        private sealed class CardInstanceHandles
        {
            public PropertyInfo GrpId;
        }

        private static readonly ReflectionCache<EventHandles> EventCache =
            new ReflectionCache<EventHandles>(
                type => new EventHandles
                {
                    RevealEvents = type.GetField("_revealEvents", AllInstanceFlags)
                },
                handles => handles.RevealEvents != null,
                LogTag,
                "reveal event");

        private static readonly ReflectionCache<RecordHandles> RecordCache =
            new ReflectionCache<RecordHandles>(
                type => new RecordHandles
                {
                    EventType = type.GetField("EventType", AllInstanceFlags),
                    OwnerId = type.GetField("OwnerId", AllInstanceFlags),
                    RevealedInstance = type.GetField("RevealedInstance", AllInstanceFlags)
                },
                handles => handles.EventType != null
                    && handles.OwnerId != null
                    && handles.RevealedInstance != null,
                LogTag,
                "reveal record");

        private static readonly ReflectionCache<CardInstanceHandles> CardInstanceCache =
            new ReflectionCache<CardInstanceHandles>(
                type => new CardInstanceHandles
                {
                    GrpId = type.GetProperty("GrpId", AllInstanceFlags)
                },
                handles => handles.GrpId != null,
                LogTag,
                "revealed card instance");

        internal static string Build(
            object uxEvent,
            uint localPlayerId,
            bool includeRulesText,
            Func<uint, CardInfo?> resolveCard,
            Func<string, string> formatReveal)
        {
            if (uxEvent == null || localPlayerId == 0 || resolveCard == null || formatReveal == null)
                return null;

            try
            {
                if (!EventCache.EnsureInitialized(uxEvent.GetType()))
                    return null;

                var records = EventCache.Handles.RevealEvents.GetValue(uxEvent) as IEnumerable;
                if (records == null)
                    return null;

                var announcements = new List<string>();
                foreach (var record in records)
                {
                    string announcement = BuildRecordAnnouncement(
                        record,
                        localPlayerId,
                        includeRulesText,
                        resolveCard,
                        formatReveal);
                    if (!string.IsNullOrEmpty(announcement))
                        announcements.Add(announcement);
                }

                return announcements.Count == 0 ? null : string.Join(". ", announcements);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildRecordAnnouncement(
            object record,
            uint localPlayerId,
            bool includeRulesText,
            Func<uint, CardInfo?> resolveCard,
            Func<string, string> formatReveal)
        {
            if (record == null)
                return null;

            try
            {
                if (!RecordCache.EnsureInitialized(record.GetType()))
                    return null;

                var handles = RecordCache.Handles;
                if (!string.Equals(handles.EventType.GetValue(record)?.ToString(), "Reveal", StringComparison.Ordinal))
                    return null;

                uint ownerId = handles.OwnerId.GetValue(record) is uint id ? id : 0;
                if (ownerId == 0 || ownerId == localPlayerId)
                    return null;

                object cardInstance = handles.RevealedInstance.GetValue(record);
                if (cardInstance == null || !CardInstanceCache.EnsureInitialized(cardInstance.GetType()))
                    return null;

                uint grpId = CardInstanceCache.Handles.GrpId.GetValue(cardInstance) is uint groupId
                    ? groupId
                    : 0;
                if (grpId == 0)
                    return null;

                CardInfo? resolved = resolveCard(grpId);
                if (!resolved.HasValue || !resolved.Value.IsValid || string.IsNullOrWhiteSpace(resolved.Value.Name))
                    return null;

                string detail = resolved.Value.Name;
                if (includeRulesText && !string.IsNullOrWhiteSpace(resolved.Value.RulesText))
                    detail += ", " + resolved.Value.RulesText;

                return formatReveal(detail);
            }
            catch
            {
                return null;
            }
        }
    }
}
