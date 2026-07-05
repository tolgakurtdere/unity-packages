# Integration Examples

Reference implementations that show the three things a game supplies on top of `com.tk.notification`'s
mechanism: the **reminder set**, a **remote-config bridge** for quiet hours, and **launch routing**.
Copy what you need into your project and adapt it — these are examples, not drop-in production code.

The `RemoteConfigQuietHoursBridge` references `com.tk.remoteconfig`, so it compiles only once that
package is present in your project. The other two only use `com.tk.notification` itself.

## EngagementReminders

The classic re-engagement funnel as a game-owned builder: local reminders at 1 / 3 / 7 / 14 / 30 days
out. This is the cleaned-up form of the reference project's `MyNotificationManager` funnel — the
package owns scheduling/quiet-hours/launch; the reminder set (content, timing, localization) is yours,
so it lives here. Feed it to the service, which cancels the old set and re-declares this one:

```csharp
service.ScheduleAll(EngagementReminders.Build(DateTime.Now, "main"));
```

Call it on every launch/resume. Replace the placeholder copy with your localized strings and tune the
day set to your retention curve.

## RemoteConfigQuietHoursBridge

Builds a `QuietHoursSettings` from live remote config, so you can toggle quiet hours and move the
window from your console without a client update. References `TK.RemoteConfig`.

```csharp
service.QuietHours = RemoteConfigQuietHoursBridge.From(rc);
```

Reads use `RemoteConfigService.GetBool`/`GetInt`, which fall back to the supplied default before init
or for an unknown key, so the window is never undefined. Assign after a fetch and again on refresh.

> `com.tk.notification` has **no dependency** on `com.tk.remoteconfig` — this bridge is a sample you
> copy into a project that has both.

## LaunchRouter

A one-call boot helper that checks whether the app was opened from a notification tap and `switch`es on
the payload to route. Replaces the reference's hardcoded analytics call with a switch you own.

```csharp
LaunchRouter.Route(service); // call once at boot, after your services are up
```

`response.Data` is whatever you set on the scheduled `NotificationRequest.Data` — use it as a routing
key (screen id, deep link, analytics tag). The cases in the file are stubs; wire them to your own
navigation / analytics / deep-link layer.
