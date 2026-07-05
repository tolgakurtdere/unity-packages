using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TK.Notification;

namespace TK.Notification.Tests
{
    /// <summary>Recording backend for deterministic EditMode tests.</summary>
    public sealed class FakeNotificationBackend : INotificationBackend
    {
        // Knobs
        public bool IsAvailable { get; set; } = true;
        public bool PermissionResult = true;
        public bool ThrowOnSchedule;
        public NotificationResponse? Launch;   // set to inject a launching notification

        // Recorded
        public readonly List<NotificationChannel> Channels = new();
        public readonly List<NotificationRequest> Scheduled = new();
        public readonly List<int> Cancelled = new();
        public int CancelAllCount;
        public int PermissionRequests;
        public int OpenSettingsCount;
        private int _nextId = 1;

        public void RegisterChannel(NotificationChannel channel) => Channels.Add(channel);

        public int Schedule(NotificationRequest request)
        {
            if (ThrowOnSchedule) throw new InvalidOperationException("fake: schedule threw");
            Scheduled.Add(request);
            return _nextId++;
        }

        public void Cancel(int id) => Cancelled.Add(id);
        public void CancelAll() => CancelAllCount++;

        public Task<bool> RequestPermissionAsync()
        {
            PermissionRequests++;
            return Task.FromResult(PermissionResult);
        }

        public bool IsPermissionGranted() => PermissionResult;

        public bool TryGetLaunchNotification(out NotificationResponse response)
        {
            if (Launch.HasValue) { response = Launch.Value; return true; }
            response = default;
            return false;
        }

        public void OpenSettings() => OpenSettingsCount++;
    }
}
