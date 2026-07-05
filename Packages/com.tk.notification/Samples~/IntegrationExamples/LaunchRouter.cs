using TK.Notification;
using UnityEngine;

namespace TK.Notification.Samples.IntegrationExamples
{
    /// <summary>
    /// A startup helper that checks whether the app was opened by tapping a notification and routes on its
    /// payload. Call <see cref="Route"/> once during boot (after your services are up). If the app was
    /// launched normally, <see cref="NotificationService.TryGetLaunchNotification"/> returns false and this
    /// is a no-op.
    ///
    /// This replaces the reference project's hardcoded "launched-from-notification" analytics call with a
    /// simple switch you own: the <see cref="NotificationResponse.Data"/> string is whatever you set on the
    /// scheduled <see cref="NotificationRequest.Data"/>, so use it as a routing key (a screen id, a deep
    /// link, an analytics tag). The cases below are stubs — wire them to your own navigation / analytics /
    /// deep-link layer.
    /// </summary>
    public static class LaunchRouter
    {
        public static void Route(NotificationService service)
        {
            if (!service.TryGetLaunchNotification(out var response))
                return; // normal launch — nothing to route.

            // TODO: forward to your analytics ("opened from notification", response.Data) here.

            switch (response.Data)
            {
                case "d1":
                case "d3":
                case "d7":
                case "d14":
                case "d30":
                    // Re-engagement reminder tapped — e.g. open the home screen / daily-reward flow.
                    Debug.Log($"[LaunchRouter] Re-engagement notification tapped (data='{response.Data}').");
                    break;

                case null:
                case "":
                    // No payload — just a plain open. Fall through to your default landing screen.
                    Debug.Log("[LaunchRouter] Notification tapped with no payload.");
                    break;

                default:
                    // Unknown payload (e.g. a deep-link key you haven't mapped yet) — decode and route.
                    Debug.Log($"[LaunchRouter] Notification tapped with data='{response.Data}'.");
                    break;
            }
        }
    }
}
