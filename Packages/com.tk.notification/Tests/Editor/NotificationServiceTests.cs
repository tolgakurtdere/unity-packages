using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Notification;
using UnityEngine;
using UnityEngine.TestTools;

namespace TK.Notification.Tests
{
    [TestFixture]
    public class NotificationServiceTests
    {
        private static DateTime D(int h) => new DateTime(2026, 7, 5, h, 0, 0, DateTimeKind.Local);

        private static NotificationRequest Req(DateTime when, string title = "Title") =>
            new NotificationRequest("chan", title, "Body", when);

        [Test]
        public void Ctor_NullBackend_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NotificationService(null));
        }

        [Test]
        public void Schedule_AppliesQuietHours_BackendGetsShiftedTime()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend) { QuietHours = new QuietHoursSettings(true, 23, 7) };

            svc.Schedule(Req(D(2)));

            Assert.That(backend.Scheduled.Count, Is.EqualTo(1));
            Assert.That(backend.Scheduled[0].DeliveryTime, Is.EqualTo(D(7)));
        }

        [Test]
        public void Schedule_NoQuietHours_PassesThrough()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);

            svc.Schedule(Req(D(2)));

            Assert.That(backend.Scheduled.Count, Is.EqualTo(1));
            Assert.That(backend.Scheduled[0].DeliveryTime, Is.EqualTo(D(2)));
        }

        [Test]
        public void Schedule_ReturnsBackendId()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);

            var id = svc.Schedule(Req(D(9)));

            Assert.That(id, Is.EqualTo(1));
        }

        [Test]
        public void Schedule_NotSupported_ReturnsNull_NoBackendCall()
        {
            var backend = new FakeNotificationBackend { IsAvailable = false };
            var svc = new NotificationService(backend);

            var id = svc.Schedule(Req(D(9)));

            Assert.That(id, Is.Null);
            Assert.That(backend.Scheduled.Count, Is.EqualTo(0));
        }

        [Test]
        public void ScheduleAll_CancelsAllThenSchedulesInOrder()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);
            var requests = new List<NotificationRequest>
            {
                Req(D(9), "A"),
                Req(D(10), "B"),
                Req(D(11), "C"),
            };

            svc.ScheduleAll(requests);

            Assert.That(backend.CancelAllCount, Is.EqualTo(1));
            Assert.That(backend.Scheduled.Count, Is.EqualTo(3));
            Assert.That(backend.Scheduled[0].Title, Is.EqualTo("A"));
            Assert.That(backend.Scheduled[1].Title, Is.EqualTo("B"));
            Assert.That(backend.Scheduled[2].Title, Is.EqualTo("C"));
        }

        [Test]
        public void ScheduleAll_AppliesQuietHoursToEach()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend) { QuietHours = new QuietHoursSettings(true, 23, 7) };
            var requests = new List<NotificationRequest>
            {
                Req(D(2), "early"),
                Req(new DateTime(2026, 7, 5, 23, 30, 0, DateTimeKind.Local), "late"),
            };

            svc.ScheduleAll(requests);

            Assert.That(backend.Scheduled.Count, Is.EqualTo(2));
            Assert.That(backend.Scheduled[0].DeliveryTime, Is.EqualTo(D(7)));
            Assert.That(backend.Scheduled[1].DeliveryTime, Is.EqualTo(D(7).AddDays(1)));
        }

        [Test]
        public void ScheduleAll_NotSupported_NoOp()
        {
            var backend = new FakeNotificationBackend { IsAvailable = false };
            var svc = new NotificationService(backend);
            var requests = new List<NotificationRequest> { Req(D(9)), Req(D(10)) };

            svc.ScheduleAll(requests);

            Assert.That(backend.CancelAllCount, Is.EqualTo(0));
            Assert.That(backend.Scheduled.Count, Is.EqualTo(0));
        }

        [Test]
        public void ScheduleAll_NullRequests_Throws()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);

            Assert.Throws<ArgumentNullException>(() => svc.ScheduleAll(null));
        }

        [Test]
        public void ScheduleAll_NullRequests_Throws_EvenWhenUnsupported()
        {
            // The null-check must precede the availability gate: throws even on an unsupported backend.
            Assert.Throws<ArgumentNullException>(() =>
                new NotificationService(new FakeNotificationBackend { IsAvailable = false }).ScheduleAll(null));
        }

        [Test]
        public void RegisterChannel_ReachesBackend_AndNoOpWhenUnsupported()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);
            svc.RegisterChannel(new NotificationChannel("id", "name"));
            Assert.That(backend.Channels.Count, Is.EqualTo(1));

            var offBackend = new FakeNotificationBackend { IsAvailable = false };
            var offSvc = new NotificationService(offBackend);
            offSvc.RegisterChannel(new NotificationChannel("id", "name"));
            Assert.That(offBackend.Channels.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task RequestPermissionAsync_ReturnsBackendResult()
        {
            var backend = new FakeNotificationBackend { PermissionResult = true };
            var svc = new NotificationService(backend);
            var granted = await svc.RequestPermissionAsync();
            Assert.That(granted, Is.True);
            Assert.That(backend.PermissionRequests, Is.EqualTo(1));

            var offBackend = new FakeNotificationBackend { IsAvailable = false, PermissionResult = true };
            var offSvc = new NotificationService(offBackend);
            var offGranted = await offSvc.RequestPermissionAsync();
            Assert.That(offGranted, Is.False);
            Assert.That(offBackend.PermissionRequests, Is.EqualTo(0));
        }

        [Test]
        public void PermissionStatus_ReflectsBackend()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);

            backend.PermissionStatus = NotificationPermission.NotDetermined;
            Assert.That(svc.PermissionStatus, Is.EqualTo(NotificationPermission.NotDetermined));

            backend.PermissionStatus = NotificationPermission.Denied;
            Assert.That(svc.PermissionStatus, Is.EqualTo(NotificationPermission.Denied));

            backend.PermissionStatus = NotificationPermission.Authorized;
            Assert.That(svc.PermissionStatus, Is.EqualTo(NotificationPermission.Authorized));
        }

        [Test]
        public void PermissionStatus_NotSupported_ReturnsNotDetermined()
        {
            var backend = new FakeNotificationBackend { IsAvailable = false, PermissionStatus = NotificationPermission.Authorized };
            var svc = new NotificationService(backend);
            Assert.That(svc.PermissionStatus, Is.EqualTo(NotificationPermission.NotDetermined));
        }

        [Test]
        public void PermissionStatus_BackendThrows_Swallowed_ReturnsNotDetermined()
        {
            var backend = new FakeNotificationBackend { ThrowOnPermissionStatus = true };
            var svc = new NotificationService(backend);

            LogAssert.Expect(LogType.Exception, new Regex("fake: status threw"));

            Assert.That(svc.PermissionStatus, Is.EqualTo(NotificationPermission.NotDetermined));
        }

        [Test]
        public void IsPermissionGranted_TrueOnlyWhenAuthorized()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);

            backend.PermissionStatus = NotificationPermission.NotDetermined;
            Assert.That(svc.IsPermissionGranted, Is.False);

            backend.PermissionStatus = NotificationPermission.Denied;
            Assert.That(svc.IsPermissionGranted, Is.False);

            backend.PermissionStatus = NotificationPermission.Authorized;
            Assert.That(svc.IsPermissionGranted, Is.True);

            var offBackend = new FakeNotificationBackend { IsAvailable = false, PermissionStatus = NotificationPermission.Authorized };
            var offSvc = new NotificationService(offBackend);
            Assert.That(offSvc.IsPermissionGranted, Is.False);
        }

        [Test]
        public void TryGetLaunchNotification_SurfacesInjected()
        {
            var backend = new FakeNotificationBackend { Launch = new NotificationResponse("main", "d0") };
            var svc = new NotificationService(backend);
            Assert.That(svc.TryGetLaunchNotification(out var response), Is.True);
            Assert.That(response.ChannelId, Is.EqualTo("main"));
            Assert.That(response.Data, Is.EqualTo("d0"));

            var noLaunchBackend = new FakeNotificationBackend();
            var noLaunchSvc = new NotificationService(noLaunchBackend);
            Assert.That(noLaunchSvc.TryGetLaunchNotification(out _), Is.False);

            var offBackend = new FakeNotificationBackend
            {
                IsAvailable = false,
                Launch = new NotificationResponse("main", "d0")
            };
            var offSvc = new NotificationService(offBackend);
            Assert.That(offSvc.TryGetLaunchNotification(out _), Is.False);
        }

        [Test]
        public void Cancel_And_CancelAll_ReachBackend()
        {
            var backend = new FakeNotificationBackend();
            var svc = new NotificationService(backend);

            svc.Cancel(9);
            Assert.That(backend.Cancelled, Does.Contain(9));

            svc.CancelAll();
            Assert.That(backend.CancelAllCount, Is.EqualTo(1));
        }

        [Test]
        public void Schedule_BackendThrows_Swallowed_ReturnsNull()
        {
            var backend = new FakeNotificationBackend { ThrowOnSchedule = true };
            var svc = new NotificationService(backend);

            LogAssert.Expect(LogType.Exception, new Regex("fake: schedule threw"));

            var id = svc.Schedule(Req(D(9)));

            Assert.That(id, Is.Null);
        }

        [Test]
        public void IsSupported_And_OpenSettings()
        {
            var backend = new FakeNotificationBackend { IsAvailable = true };
            var svc = new NotificationService(backend);
            Assert.That(svc.IsSupported, Is.True);
            svc.OpenNotificationSettings();
            Assert.That(backend.OpenSettingsCount, Is.EqualTo(1));

            var offBackend = new FakeNotificationBackend { IsAvailable = false };
            var offSvc = new NotificationService(offBackend);
            Assert.That(offSvc.IsSupported, Is.False);
            offSvc.OpenNotificationSettings();
            Assert.That(offBackend.OpenSettingsCount, Is.EqualTo(0));
        }
    }
}
