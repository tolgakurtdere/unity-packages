namespace TK.Notification
{
    /// <summary>
    /// OS notification-permission state, read from the platform (the OS is the source of truth). Lets a
    /// settings opt-in branch prompt-vs-redirect without a persisted "hasRequested" flag that would drift.
    /// </summary>
    public enum NotificationPermission
    {
        /// <summary>Never asked — <c>RequestPermissionAsync</c> can still show the native prompt.</summary>
        NotDetermined,

        /// <summary>Asked and refused — the OS won't prompt again; route to <c>OpenNotificationSettings</c>.</summary>
        Denied,

        /// <summary>Granted — notifications can be delivered (includes iOS provisional/ephemeral).</summary>
        Authorized
    }
}
