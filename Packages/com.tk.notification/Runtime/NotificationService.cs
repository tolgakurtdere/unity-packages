using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Notification
{
    /// <summary>
    /// Local notification orchestration behind INotificationBackend. Applies quiet-hours before scheduling,
    /// supports cancel-all-then-reschedule, exposes async permission + launch routing. Every backend call is
    /// guarded (must-not-throw contract): a failure is logged and swallowed. On non-mobile targets the backend
    /// reports IsAvailable=false and all operations are safe no-ops. Main-thread usage assumed.
    /// </summary>
    public sealed class NotificationService
    {
        private readonly INotificationBackend _backend;

        public NotificationService(INotificationBackend backend, QuietHoursSettings quietHours = default)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            QuietHours = quietHours;
        }

        public QuietHoursSettings QuietHours { get; set; }

        public bool IsSupported => _backend.IsAvailable;

        public void RegisterChannel(NotificationChannel channel)
        {
            if (!_backend.IsAvailable) return;
            try { _backend.RegisterChannel(channel); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public int? Schedule(NotificationRequest request)
        {
            if (!_backend.IsAvailable) return null;
            try { return _backend.Schedule(ApplyQuietHours(request)); }
            catch (Exception exception) { Debug.LogException(exception); return null; }
        }

        public void ScheduleAll(IReadOnlyList<NotificationRequest> requests)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (!_backend.IsAvailable) return;
            try
            {
                _backend.CancelAll();
                for (var i = 0; i < requests.Count; i++)
                    _backend.Schedule(ApplyQuietHours(requests[i]));
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public void Cancel(int id)
        {
            if (!_backend.IsAvailable) return;
            try { _backend.Cancel(id); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public void CancelAll()
        {
            if (!_backend.IsAvailable) return;
            try { _backend.CancelAll(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public async Task<bool> RequestPermissionAsync()
        {
            if (!_backend.IsAvailable) return false;
            try { return await _backend.RequestPermissionAsync(); }
            catch (Exception exception) { Debug.LogException(exception); return false; }
        }

        /// <summary>
        /// The OS notification-permission state (the OS owns it — don't cache it across sessions). Branch a
        /// settings opt-in on it: <see cref="NotificationPermission.NotDetermined"/> → <see cref="RequestPermissionAsync"/>
        /// (native prompt); <see cref="NotificationPermission.Denied"/> → <see cref="OpenNotificationSettings"/>
        /// (the OS won't prompt again); <see cref="NotificationPermission.Authorized"/> → schedule. Returns
        /// <see cref="NotificationPermission.NotDetermined"/> when notifications are unavailable (non-mobile build target).
        /// </summary>
        public NotificationPermission PermissionStatus
        {
            get
            {
                if (!_backend.IsAvailable) return NotificationPermission.NotDetermined;
                try { return _backend.PermissionStatus; }
                catch (Exception exception) { Debug.LogException(exception); return NotificationPermission.NotDetermined; }
            }
        }

        /// <summary>Convenience over <see cref="PermissionStatus"/>: true only when <see cref="NotificationPermission.Authorized"/>.</summary>
        public bool IsPermissionGranted => PermissionStatus == NotificationPermission.Authorized;

        public bool TryGetLaunchNotification(out NotificationResponse response)
        {
            response = default;
            if (!_backend.IsAvailable) return false;
            try { return _backend.TryGetLaunchNotification(out response); }
            catch (Exception exception) { Debug.LogException(exception); response = default; return false; }
        }

        public void OpenNotificationSettings()
        {
            if (!_backend.IsAvailable) return;
            try { _backend.OpenSettings(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private NotificationRequest ApplyQuietHours(NotificationRequest request)
        {
            var when = QuietHours.Apply(request.DeliveryTime);
            if (when == request.DeliveryTime) return request;
            return new NotificationRequest(request.ChannelId, request.Title, request.Body, when,
                request.Data, request.SmallIcon, request.LargeIcon);
        }
    }
}
