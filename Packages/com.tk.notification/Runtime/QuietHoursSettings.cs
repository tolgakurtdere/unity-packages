using System;

namespace TK.Notification
{
    /// <summary>
    /// Optional quiet-hours window. Enabled + [StartHour, EndHour) in device-local wall-clock time.
    /// Apply() shifts a fire time that lands inside the window forward to EndHour:00. Supports wrapping
    /// windows (e.g. 23→7). default(QuietHoursSettings) is disabled (a no-op).
    /// </summary>
    public readonly struct QuietHoursSettings
    {
        public bool Enabled { get; }
        public int StartHour { get; }   // inclusive, 0-23
        public int EndHour { get; }     // exclusive, 0-23

        public QuietHoursSettings(bool enabled, int startHour, int endHour)
        {
            Enabled = enabled;
            StartHour = startHour;
            EndHour = endHour;
        }

        public DateTime Apply(DateTime fireTime)
        {
            if (!Enabled || StartHour == EndHour) return fireTime;

            var hour = fireTime.Hour;
            var wrapping = StartHour > EndHour;
            var inQuiet = wrapping
                ? (hour >= StartHour || hour < EndHour)
                : (hour >= StartHour && hour < EndHour);

            if (!inQuiet) return fireTime;

            var endToday = new DateTime(fireTime.Year, fireTime.Month, fireTime.Day,
                EndHour, 0, 0, fireTime.Kind);

            // Late part of a wrapping window (e.g. 23:30 in 23→7) ends the NEXT day; everything else ends today.
            return wrapping && hour >= StartHour ? endToday.AddDays(1) : endToday;
        }
    }
}
