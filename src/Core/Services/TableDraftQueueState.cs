using System;
using System.Reflection;
using AccessibleArena.Core.Utils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Captures live state of the human ("table") draft queue from the game's own update
    /// notifications, so the LoadingScreenNavigator can read pod fill / ready counts / phase
    /// without those values being retained anywhere on the controller.
    ///
    /// The game pushes two notification streams into
    /// <c>TableDraftQueueContentController</c>:
    ///   - <c>HandlePodQueueNotification(PodQueueNotification)</c> — NumInPod / PodCapacity as
    ///     players join the pod.
    ///   - <c>HandleDraftNotification(DraftNotification)</c> — ReadyReq (table found, ready up),
    ///     ReadyUpdate (NumReady), TableInfo (starting), Close (queue rebuilt / dropped).
    /// PanelStatePatch postfixes those two methods and forwards the values here. All reads are
    /// best-effort: if the patch never applied, the navigator falls back to reflecting
    /// <c>_prevNumInPod</c> / <c>_isLocalPlayerReady</c> off the controller directly.
    /// </summary>
    public static class TableDraftQueueState
    {
        /// <summary>Mirrors Wizards.Unification.Models.Draft.EDraftNotificationType.</summary>
        public enum DraftNotificationKind
        {
            None = -1,
            ReadyReq = 0,
            ReadyUpdate = 1,
            TableInfo = 2,
            PickReq = 3,
            Close = 4,
            PackInfo = 5
        }

        // Latest pod-fill values (from PodQueueNotification).
        public static int NumInPod { get; private set; }
        public static int PodCapacity { get; private set; }

        // Latest ready-up values (from DraftNotification).
        public static int NumReady { get; private set; }
        public static int TimeoutSec { get; private set; }
        public static DraftNotificationKind LastDraftKind { get; private set; } = DraftNotificationKind.None;

        /// <summary>True once a ReadyReq was received (the queue swapped Cancel for Ready).</summary>
        public static bool ReadyRequested { get; private set; }

        /// <summary>True once a TableInfo notification arrived (draft is starting).</summary>
        public static bool TableStarting { get; private set; }

        /// <summary>
        /// Monotonic counter bumped on every captured notification. The navigator compares it
        /// against a cached value to know when something changed without diffing every field.
        /// </summary>
        public static int UpdateSeq { get; private set; }

        private static bool _subscribed;
        private static FieldInfo _numInPodField, _podCapacityField;
        private static FieldInfo _draftTypeField, _numReadyField, _timeoutSecField;

        /// <summary>
        /// Subscribe to panel open/close so we reset cleanly between successive queues.
        /// Safe to call multiple times. Invoked from PanelStatePatch once patches are applied.
        /// </summary>
        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            Patches.PanelStatePatch.OnPanelStateChanged += HandlePanelStateChanged;
        }

        private static void HandlePanelStateChanged(object instance, bool isOpen, string typeName)
        {
            if (typeName != Constants.GameTypeNames.TableDraftQueueContentController) return;
            // A fresh open (or a close) starts a clean slate; a rebuilt queue (Close while ready)
            // re-opens conceptually, so resetting on both edges is correct.
            Reset();
        }

        /// <summary>Capture a PodQueueNotification (called from the Harmony postfix).</summary>
        public static void RecordPodQueue(object podQueueNotification)
        {
            if (podQueueNotification == null) return;
            try
            {
                var type = podQueueNotification.GetType();
                if (_numInPodField == null || _numInPodField.DeclaringType != type)
                {
                    _numInPodField = type.GetField("NumInPod");
                    _podCapacityField = type.GetField("PodCapacity");
                }
                if (_numInPodField != null) NumInPod = Convert.ToInt32(_numInPodField.GetValue(podQueueNotification));
                if (_podCapacityField != null) PodCapacity = Convert.ToInt32(_podCapacityField.GetValue(podQueueNotification));
                UpdateSeq++;
            }
            catch (Exception ex)
            {
                Log.Warn("TableDraftQueueState", $"RecordPodQueue failed: {ex.Message}");
            }
        }

        /// <summary>Capture a DraftNotification (called from the Harmony postfix).</summary>
        public static void RecordDraftNotification(object draftNotification)
        {
            if (draftNotification == null) return;
            try
            {
                var type = draftNotification.GetType();
                if (_draftTypeField == null || _draftTypeField.DeclaringType != type)
                {
                    _draftTypeField = type.GetField("Type");
                    _numReadyField = type.GetField("NumReady");
                    _timeoutSecField = type.GetField("TimeoutSec");
                }

                if (_draftTypeField != null)
                    LastDraftKind = (DraftNotificationKind)Convert.ToInt32(_draftTypeField.GetValue(draftNotification));
                if (_numReadyField != null)
                    NumReady = Convert.ToInt32(_numReadyField.GetValue(draftNotification));
                if (_timeoutSecField != null)
                    TimeoutSec = Convert.ToInt32(_timeoutSecField.GetValue(draftNotification));

                switch (LastDraftKind)
                {
                    case DraftNotificationKind.ReadyReq:
                        ReadyRequested = true;
                        break;
                    case DraftNotificationKind.TableInfo:
                        TableStarting = true;
                        break;
                    case DraftNotificationKind.Close:
                        // Queue is being rebuilt (if we were ready) or we were dropped; clear the
                        // ready-up state so the UI returns to "finding a table".
                        ReadyRequested = false;
                        TableStarting = false;
                        NumReady = 0;
                        break;
                }
                UpdateSeq++;
            }
            catch (Exception ex)
            {
                Log.Warn("TableDraftQueueState", $"RecordDraftNotification failed: {ex.Message}");
            }
        }

        public static void Reset()
        {
            NumInPod = 0;
            PodCapacity = 0;
            NumReady = 0;
            TimeoutSec = 0;
            LastDraftKind = DraftNotificationKind.None;
            ReadyRequested = false;
            TableStarting = false;
            UpdateSeq++;
        }
    }
}
