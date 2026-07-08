using System;
using System.Threading.Tasks;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace TK.Notification
{
    /// <summary>
    /// Real INotificationBackend over com.unity.mobile.notifications. IsAvailable follows the active build
    /// target (UNITY_ANDROID/UNITY_IOS), not the Editor: with a mobile target active the native centers run
    /// in the Editor too (editor-callable, but no real notification lands until you're on a device); on every
    /// non-mobile build target IsAvailable is false and all methods no-op. Must not throw (the service also guards).
    /// </summary>
    public sealed class UnityMobileNotificationBackend : INotificationBackend
    {
#if UNITY_ANDROID || UNITY_IOS
        public bool IsAvailable => true;
#else
        public bool IsAvailable => false;
#endif

        public void RegisterChannel(NotificationChannel channel)
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = channel.Id,
                Name = string.IsNullOrEmpty(channel.Name) ? channel.Id : channel.Name,
                Description = string.IsNullOrEmpty(channel.Description) ? channel.Id : channel.Description,
                Importance = ToAndroidImportance(channel.Importance)
            });
#endif
            // iOS/other: channels are not pre-registered.
        }

        public int Schedule(NotificationRequest request)
        {
#if UNITY_ANDROID
            var n = new AndroidNotification
            {
                Title = request.Title,
                Text = request.Body,
                FireTime = request.DeliveryTime,      // local wall-clock
                IntentData = request.Data
            };
            if (!string.IsNullOrEmpty(request.SmallIcon)) n.SmallIcon = request.SmallIcon;
            if (!string.IsNullOrEmpty(request.LargeIcon)) n.LargeIcon = request.LargeIcon;
            return AndroidNotificationCenter.SendNotification(n, request.ChannelId);
#elif UNITY_IOS
            var id = Math.Abs(Guid.NewGuid().GetHashCode());
            var interval = request.DeliveryTime.ToUniversalTime() - DateTime.UtcNow;
            // The iOS TimeInterval trigger throws for a non-positive interval and truncates to whole seconds,
            // so clamp past/near-now delivery times up to a minimum of one second in the future.
            if (interval < TimeSpan.FromSeconds(1)) interval = TimeSpan.FromSeconds(1);
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = id.ToString(),
                Title = request.Title,
                Body = request.Body,
                ShowInForeground = true,
                ThreadIdentifier = request.ChannelId,
                CategoryIdentifier = request.ChannelId,
                Data = request.Data ?? string.Empty,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = interval, Repeats = false }
            });
            return id;
#else
            return 0;
#endif
        }

        public void Cancel(int id)
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(id.ToString());
#endif
        }

        public void CancelAll()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif
        }

        public async Task<bool> RequestPermissionAsync()
        {
#if UNITY_ANDROID
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending) await Task.Yield();
            return request.Status == PermissionStatus.Allowed;
#elif UNITY_IOS
            using var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge, true);
            while (!request.IsFinished) await Task.Yield();
            return request.Granted;
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public bool IsPermissionGranted()
        {
#if UNITY_ANDROID
            return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;
#elif UNITY_IOS
            return iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus == AuthorizationStatus.Authorized;
#else
            return false;
#endif
        }

        public bool TryGetLaunchNotification(out NotificationResponse response)
        {
#if UNITY_ANDROID
            var intent = AndroidNotificationCenter.GetLastNotificationIntent();
            if (intent != null)
            {
                response = new NotificationResponse(intent.Channel, intent.Notification.IntentData);
                return true;
            }
#elif UNITY_IOS
            // Deliberate: the recommended QueryLastRespondedNotification is an async op that does not fit
            // the synchronous TryGetLaunchNotification(out ...) contract. The synchronous accessor is obsolete
            // but functional; suppress CS0618 rather than change the seam shape.
#pragma warning disable CS0618
            var last = iOSNotificationCenter.GetLastRespondedNotification();
#pragma warning restore CS0618
            if (last != null)
            {
                response = new NotificationResponse(last.CategoryIdentifier, last.Data);
                return true;
            }
#endif
            response = default;
            return false;
        }

        public void OpenSettings()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.OpenNotificationSettings();
#elif UNITY_IOS
            iOSNotificationCenter.OpenNotificationSettings();
#endif
        }

#if UNITY_ANDROID
        private static Importance ToAndroidImportance(NotificationImportance importance) => importance switch
        {
            NotificationImportance.Low => Importance.Low,
            NotificationImportance.High => Importance.High,
            _ => Importance.Default
        };
#endif
    }
}
