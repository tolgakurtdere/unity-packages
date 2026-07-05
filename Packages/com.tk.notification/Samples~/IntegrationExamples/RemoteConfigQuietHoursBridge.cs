using TK.Notification;
using TK.RemoteConfig;

namespace TK.Notification.Samples.IntegrationExamples
{
    /// <summary>
    /// Builds a <see cref="QuietHoursSettings"/> from live remote config, so you can turn quiet hours on/off
    /// and move the window from your console without a client update. This is a <b>Sample</b>: it references
    /// <c>TK.RemoteConfig</c>, so it only compiles when <c>com.tk.remoteconfig</c> is present.
    /// <c>com.tk.notification</c> has <b>no</b> dependency on the remote-config package — copy this file into
    /// a project that has both to connect the two.
    ///
    /// Reads use <c>RemoteConfigService.GetBool</c>/<c>GetInt</c> (the raw accessors), which return the
    /// supplied default before init or for an unknown key — so the window is never left undefined. Assign the
    /// result to the service, ideally after a config fetch and again whenever config refreshes:
    /// <code>service.QuietHours = RemoteConfigQuietHoursBridge.From(rc);</code>
    /// </summary>
    public static class RemoteConfigQuietHoursBridge
    {
        public static QuietHoursSettings From(RemoteConfigService rc) => new(
            rc.GetBool("quiet_hours_enabled", true),
            rc.GetInt("quiet_hours_start", 23),
            rc.GetInt("quiet_hours_end", 7));
    }
}
