using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Notification;

namespace TK.Notification.Tests
{
    [TestFixture]
    public class FakeNotificationBackendTests
    {
        private static NotificationRequest Req() =>
            new NotificationRequest("chan", "Title", "Body", new DateTime(2026, 7, 5, 9, 0, 0, DateTimeKind.Local));

        [Test]
        public void Schedule_RecordsAndReturnsIncrementingId()
        {
            var fake = new FakeNotificationBackend();
            var id1 = fake.Schedule(Req());
            var id2 = fake.Schedule(Req());
            Assert.That(id1, Is.EqualTo(1));
            Assert.That(id2, Is.EqualTo(2));
            Assert.That(fake.Scheduled.Count, Is.EqualTo(2));
        }

        [Test]
        public void Schedule_ThrowKnob_Throws()
        {
            var fake = new FakeNotificationBackend { ThrowOnSchedule = true };
            Assert.Throws<InvalidOperationException>(() => fake.Schedule(Req()));
        }

        [Test]
        public void CancelAndCancelAll_Recorded()
        {
            var fake = new FakeNotificationBackend();
            fake.Cancel(5);
            Assert.That(fake.Cancelled, Does.Contain(5));
            fake.CancelAll();
            Assert.That(fake.CancelAllCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Launch_And_Permission_Knobs()
        {
            var fake = new FakeNotificationBackend
            {
                Launch = new NotificationResponse("c", "d")
            };

            Assert.That(fake.TryGetLaunchNotification(out var response), Is.True);
            Assert.That(response.ChannelId, Is.EqualTo("c"));
            Assert.That(response.Data, Is.EqualTo("d"));

            fake.PermissionResult = false;
            Assert.That(await fake.RequestPermissionAsync(), Is.False);
            Assert.That(fake.PermissionRequests, Is.EqualTo(1));

            fake.PermissionStatus = NotificationPermission.Denied;
            Assert.That(fake.PermissionStatus, Is.EqualTo(NotificationPermission.Denied));
        }
    }
}
