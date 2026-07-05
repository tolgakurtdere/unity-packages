using System;
using NUnit.Framework;
using TK.Notification;

namespace TK.Notification.Tests
{
    [TestFixture]
    public class QuietHoursSettingsTests
    {
        private static DateTime D(int hour, int min = 0) =>
            new DateTime(2026, 7, 5, hour, min, 0, DateTimeKind.Local);

        [Test]
        public void Disabled_ReturnsUnchanged()
        {
            var qh = new QuietHoursSettings(false, 23, 7);
            Assert.That(qh.Apply(D(2)), Is.EqualTo(D(2)));
        }

        [Test]
        public void Default_IsDisabled_NoOp()
        {
            Assert.That(default(QuietHoursSettings).Apply(D(2)), Is.EqualTo(D(2)));
        }

        [Test]
        public void ZeroWidthWindow_NoOp()
        {
            var qh = new QuietHoursSettings(true, 5, 5);
            Assert.That(qh.Apply(D(5)), Is.EqualTo(D(5)));
        }

        [Test]
        public void OutsideWindow_NonWrapping_Unchanged()
        {
            var qh = new QuietHoursSettings(true, 1, 6);
            Assert.That(qh.Apply(D(8)), Is.EqualTo(D(8)));
        }

        [Test]
        public void InsideWindow_NonWrapping_ShiftsToEnd()
        {
            var qh = new QuietHoursSettings(true, 1, 6);
            Assert.That(qh.Apply(D(2, 30)), Is.EqualTo(D(6)));
        }

        [Test]
        public void OutsideWindow_Wrapping_Unchanged()
        {
            var qh = new QuietHoursSettings(true, 23, 7);
            Assert.That(qh.Apply(D(12)), Is.EqualTo(D(12)));
        }

        [Test]
        public void InsideWindow_Wrapping_EarlyMorning_ShiftsToSameDayEnd()
        {
            var qh = new QuietHoursSettings(true, 23, 7);
            Assert.That(qh.Apply(D(2)), Is.EqualTo(D(7)));
        }

        [Test]
        public void InsideWindow_Wrapping_LateNight_ShiftsToNextDayEnd()
        {
            var qh = new QuietHoursSettings(true, 23, 7);
            Assert.That(qh.Apply(D(23, 30)), Is.EqualTo(D(7).AddDays(1)));
        }

        [Test]
        public void AtStartHour_Inclusive_Shifts()
        {
            var qh = new QuietHoursSettings(true, 23, 7);
            Assert.That(qh.Apply(D(23)), Is.EqualTo(D(7).AddDays(1)));
        }

        [Test]
        public void AtEndHour_Exclusive_Unchanged()
        {
            var qh = new QuietHoursSettings(true, 23, 7);
            Assert.That(qh.Apply(D(7)), Is.EqualTo(D(7)));
        }

        [Test]
        public void ShiftDropsMinutesToTopOfEndHour()
        {
            var qh = new QuietHoursSettings(true, 1, 6);
            var result = qh.Apply(D(2, 37));
            Assert.That(result.Minute, Is.EqualTo(0));
            Assert.That(result.Hour, Is.EqualTo(6));
        }

        [Test]
        public void PreservesKind()
        {
            var qh = new QuietHoursSettings(true, 23, 7);
            var input = new DateTime(2026, 7, 5, 2, 0, 0, DateTimeKind.Utc);
            var result = qh.Apply(input);
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(result, Is.EqualTo(new DateTime(2026, 7, 5, 7, 0, 0, DateTimeKind.Utc)));
        }
    }
}
