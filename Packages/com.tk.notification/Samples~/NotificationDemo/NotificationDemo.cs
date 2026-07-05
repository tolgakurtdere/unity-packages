using System;
using System.Collections.Generic;
using TK.Notification;
using UnityEngine;

namespace TK.Notification.Samples.NotificationDemo
{
    /// <summary>
    /// Editor-runnable demo of the full <see cref="NotificationService"/> flow on the real
    /// <see cref="UnityMobileNotificationBackend"/>. Attach to any GameObject and press Play, then drive
    /// it from the <c>[ContextMenu]</c> entries (right-click the component header in the Inspector, or the
    /// component's "⋮" menu).
    ///
    /// In the Editor (and on any non-mobile target) the backend reports <c>IsSupported == false</c> and
    /// every call is a safe no-op — nothing schedules, nothing throws. Build to Android or iOS to see real
    /// notifications land in the system tray. This is deliberate: the same code path runs everywhere, so
    /// you never need <c>#if UNITY_ANDROID</c> at your call sites.
    ///
    /// The demo shows the standard retention pattern: <c>Schedule Engagement Funnel</c> calls
    /// <see cref="NotificationService.ScheduleAll"/>, which cancels every previously scheduled notification
    /// and re-declares the whole set in one shot (cancel-all-then-reschedule). Call it on every launch /
    /// resume with your current reminder set and the OS always holds exactly that set, no duplicates.
    /// </summary>
    public class NotificationDemo : MonoBehaviour
    {
        private NotificationService _service;

        private void Awake()
        {
            // Quiet hours 23:00–07:00: any reminder that would fire inside the window is shifted forward
            // to 07:00 before it reaches the backend. Pass default(QuietHoursSettings) to disable.
            _service = new NotificationService(new UnityMobileNotificationBackend(),
                new QuietHoursSettings(true, 23, 7));
            Debug.Log($"[NotificationDemo] Ready. IsSupported={_service.IsSupported} " +
                      "(false in the Editor — build to a device to see real notifications). " +
                      "Drive it from the ContextMenu entries.");
        }

        [ContextMenu("Register Channel")]
        private void RegisterChannel()
        {
            _service.RegisterChannel(new NotificationChannel(
                "main", "General", "General reminders", NotificationImportance.High));
            Debug.Log("[NotificationDemo] Registered channel 'main' (Android). No-op in the Editor.");
        }

        [ContextMenu("Request Permission")]
        private async void RequestPermission()
        {
            var granted = await _service.RequestPermissionAsync();
            Debug.Log($"[NotificationDemo] RequestPermissionAsync() -> granted={granted} " +
                      "(false in the Editor; the real OS prompt only appears on a device).");
        }

        [ContextMenu("Schedule Engagement Funnel")]
        private void ScheduleEngagementFunnel()
        {
            var now = DateTime.Now;
            var list = new List<NotificationRequest>
            {
                new("main", "Come back!", "We miss you", now.AddDays(1), "d1"),
                new("main", "Still here?", "Your progress is waiting", now.AddDays(3), "d3"),
                new("main", "A week already", "Jump back in", now.AddDays(7), "d7"),
                new("main", "Two weeks!", "Come see what's new", now.AddDays(14), "d14"),
                new("main", "One last nudge", "We'd love to see you again", now.AddDays(30), "d30"),
            };
            // Cancel-all-then-reschedule: the OS ends up holding exactly this set, no duplicates.
            _service.ScheduleAll(list);
            Debug.Log($"[NotificationDemo] Scheduled {list.Count} engagement reminders (1/3/7/14/30 days). " +
                      "No-op in the Editor.");
        }

        [ContextMenu("Cancel All")]
        private void CancelAll()
        {
            _service.CancelAll();
            Debug.Log("[NotificationDemo] Cancelled all scheduled notifications. No-op in the Editor.");
        }

        [ContextMenu("Check Launch")]
        private void CheckLaunch()
        {
            if (_service.TryGetLaunchNotification(out var response))
                Debug.Log($"[NotificationDemo] App was launched from a notification: " +
                          $"channel='{response.ChannelId}', data='{response.Data}'. Route on this.");
            else
                Debug.Log("[NotificationDemo] Not launched from a notification (always the case in the Editor).");
        }
    }
}
