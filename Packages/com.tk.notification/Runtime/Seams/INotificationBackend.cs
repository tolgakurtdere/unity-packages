using System.Threading.Tasks;

namespace TK.Notification
{
    /// <summary>
    /// Native-platform notification seam. The real impl wraps com.unity.mobile.notifications; a fake drives
    /// tests; on non-mobile targets the real impl reports IsAvailable=false and no-ops. Implementations MUST
    /// NOT throw — the service wraps every call, logs, and continues.
    /// </summary>
    public interface INotificationBackend
    {
        /// <summary>True on a mobile device with the platform API available; false in Editor/desktop.</summary>
        bool IsAvailable { get; }
        void RegisterChannel(NotificationChannel channel);
        /// <summary>Schedule one notification (DeliveryTime already quiet-hours-adjusted). Returns the assigned id.</summary>
        int Schedule(NotificationRequest request);
        void Cancel(int id);
        void CancelAll();
        Task<bool> RequestPermissionAsync();
        bool IsPermissionGranted();
        bool TryGetLaunchNotification(out NotificationResponse response);
        void OpenSettings();
    }
}
