using System;
using System.Collections.Generic;
using TK.Notification;

namespace TK.Notification.Samples.IntegrationExamples
{
    /// <summary>
    /// A game-owned builder for the classic re-engagement funnel: local reminders at 1 / 3 / 7 / 14 / 30
    /// days out. This is the cleaned-up, package-friendly form of the reference project's
    /// <c>MyNotificationManager</c> funnel — the <b>package owns the mechanism</b> (scheduling, quiet-hours,
    /// launch routing); the <b>reminder set (content, timing, localization) is the game's</b>, so it lives
    /// here as a sample you copy and adapt, not in the package.
    ///
    /// Feed the result straight to the service — <see cref="NotificationService.ScheduleAll"/> cancels the
    /// previous set and re-declares this one, so call it on every launch/resume:
    /// <code>service.ScheduleAll(EngagementReminders.Build(DateTime.Now, "main"));</code>
    /// </summary>
    public static class EngagementReminders
    {
        /// <summary>
        /// Builds the 1/3/7/14/30-day reminder set relative to <paramref name="now"/> on
        /// <paramref name="channelId"/>. Delivery times are absolute (device-local wall clock); the service
        /// applies quiet-hours before scheduling. Replace the placeholder title/body with your own localized
        /// strings, and tune the offsets/day set to your game's retention curve.
        /// </summary>
        public static IReadOnlyList<NotificationRequest> Build(DateTime now, string channelId)
        {
            return new List<NotificationRequest>
            {
                // TODO: replace placeholder copy with your localized strings (e.g. from com.tk.core's
                // localization, or your own table). Content, timing, and the day set are the game's call.
                new(channelId, "Come back!", "We miss you", now.AddDays(1), "d1"),
                new(channelId, "Still here?", "Your progress is waiting", now.AddDays(3), "d3"),
                new(channelId, "A week already", "Jump back in", now.AddDays(7), "d7"),
                new(channelId, "Two weeks!", "Come see what's new", now.AddDays(14), "d14"),
                new(channelId, "One last nudge", "We'd love to see you again", now.AddDays(30), "d30"),
            };
        }
    }
}
