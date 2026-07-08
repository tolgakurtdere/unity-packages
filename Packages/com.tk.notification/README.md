# TK Notification

Local mobile-notification framework: schedule and cancel local notifications, request OS permission,
register channels, apply quiet hours, and route the notification that launched the app — all behind a
testable seam, with a Unity Mobile Notifications backend on Android/iOS build targets and a no-op fallback everywhere else.
It powers the retention workhorse — the 1/3/7/14/30-day re-engagement funnel — without the usual game-code coupling.

## What's inside

| Module | Location | What it gives you |
| --- | --- | --- |
| Service | `Runtime/NotificationService.cs` | `NotificationService` — the orchestration engine. Applies quiet-hours before scheduling, does cancel-all-then-reschedule, exposes async permission + launch routing, and guards every backend call (a flaky OS call is logged and swallowed, never crashes the game). |
| Seam + backend | `Runtime/Seams/`, `Runtime/Backends/` | `INotificationBackend` — the single interface a backend implements (must not throw). Ships `UnityMobileNotificationBackend`, the real Android/iOS impl (active on an Android/iOS build target — the Editor included) that no-ops on every non-mobile build target. A `FakeNotificationBackend` drives the tests. |
| Quiet hours | `Runtime/QuietHoursSettings.cs` | `QuietHoursSettings` — a pure struct with an `Apply()` that shifts a fire time out of a (possibly wrapping) do-not-disturb window. Unit-tested independently of any backend. |
| Values | `Runtime/NotificationChannel.cs`, `NotificationRequest.cs`, `NotificationResponse.cs`, `NotificationImportance.cs` | Typed, package-owned records — no `Unity.Notifications.*` type leaks into the API. `NotificationChannel` (Android channel), `NotificationRequest` (what to schedule), `NotificationResponse` (what launched the app), `NotificationImportance`. |

## Install

Add it via Package Manager → **Add package from git URL**:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.notification
```

Pinned to a released version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.notification#com.tk.notification/0.2.0
```

Its single dependency, `com.unity.mobile.notifications`, is on Unity's **default registry**, so there
is **no scoped registry to add** — the git URL is all you need. To see this package's EditMode tests in
your own project's Test Runner, add `"com.tk.notification"` to your project's `Packages/manifest.json`
`testables`.

## Quickstart

```csharp
var service = new NotificationService(new UnityMobileNotificationBackend());
service.RegisterChannel(new NotificationChannel("main", "General"));   // Android channel
await service.RequestPermissionAsync();                                // OS prompt (Android 13+/iOS)
service.ScheduleAll(myList);                                           // your reminder set
```

That's the whole flow. `myList` is an `IReadOnlyList<NotificationRequest>` — build it yourself (see the
`Integration Examples` sample's `EngagementReminders` for the re-engagement funnel). On non-mobile
build targets every one of these calls is a safe no-op (see **Platform support**), so you never guard the
call site with `#if`.

## Quiet hours

Set a do-not-disturb window and any reminder that would fire inside it is shifted forward to the
window's end **before** it reaches the OS:

```csharp
service.QuietHours = new QuietHoursSettings(true, 23, 7); // no notifications 23:00–07:00
```

Semantics: the window is `[StartHour, EndHour)` in device-local wall-clock time — start inclusive, end
exclusive. A fire time inside the window is moved to `EndHour:00`. Windows **wrap** across midnight: `23
→ 7` means "23:00 through 06:59", and a fire time in its late part (say 23:30) is shifted to 07:00 the
next day. `default(QuietHoursSettings)` — and any window where `StartHour == EndHour` — is disabled (no
shift). You can also feed the window from remote config; see **Feeding config from Remote Config**.

## Scheduling model

- `NotificationRequest.DeliveryTime` is **absolute** — a `DateTime` in device-local wall-clock time. A
  relative reminder is your one-liner: `DateTime.Now.AddDays(3)`. The service stays clock-free (and so
  fully testable): quiet-hours is a pure function of the time you pass.
- **Cancel-all-then-reschedule.** `ScheduleAll(list)` cancels every currently scheduled notification,
  then schedules the given set in order. Call it on every launch/resume with your current reminder set
  and the OS always holds exactly that set — no drift, no duplicates. (`Schedule(one)` adds a single
  notification without cancelling.)
- **Ids are backend-assigned.** `Schedule` returns the `int?` id the backend assigned (null when
  unsupported); `Cancel(id)` cancels one. You rarely track ids for the funnel — you just re-declare the
  whole set.

## Permission

```csharp
bool granted = await service.RequestPermissionAsync();    // shows the OS prompt where the platform needs it
NotificationPermission status = service.PermissionStatus; // NotDetermined / Denied / Authorized
bool current = service.IsPermissionGranted;               // sugar for PermissionStatus == Authorized
```

Permission is **async** and coroutine-free (no hidden GameObject). The **OS is the source of truth** —
read `PermissionStatus` / `IsPermissionGranted` for the live state; don't cache "granted" across sessions,
since the user can revoke it in system settings. Your own app-level opt-in (a "reminders on/off" toggle)
is a separate concern to persist yourself; gate scheduling on it in your game code.
`OpenNotificationSettings()` deep-links to the OS settings page so the user can re-enable after a denial.

### Settings opt-in (prompt vs. redirect)

The standard "turn notifications on" flow branches on `PermissionStatus`: prompt if the OS still can,
otherwise send the user to system settings (once denied, the OS won't prompt again).

```csharp
switch (service.PermissionStatus)
{
    case NotificationPermission.NotDetermined:   // never asked → show the native prompt
        if (await service.RequestPermissionAsync()) service.ScheduleAll(myReminders);
        break;
    case NotificationPermission.Denied:          // asked & refused → the OS won't prompt again
        service.OpenNotificationSettings();      // deep-link to system settings
        break;
    case NotificationPermission.Authorized:      // already on → just schedule
        service.ScheduleAll(myReminders);
        break;
}
```

`PermissionStatus` reads the native state directly (iOS `AuthorizationStatus`, Android
`UserPermissionToPost` — notifications turned off in system settings report `Denied`), so you never track
a "did we ask yet?" flag that drifts across reinstalls or external settings changes.

## Launch routing

When the user taps a notification to open the app, read it once at boot and route on its payload:

```csharp
if (service.TryGetLaunchNotification(out var response))
{
    // response.ChannelId, response.Data — route to a screen / deep link / analytics tag
}
```

`response.Data` is whatever you set on the scheduled `NotificationRequest.Data`. See the `Integration
Examples` sample's `LaunchRouter` for a switch-based router you copy and adapt.

## Platform support

`IsSupported` is driven by the **active build target**, not by whether you're in the Editor. (Unity
defines `UNITY_ANDROID`/`UNITY_IOS` whenever that platform is the active build target — including in the
Editor while it's selected.)

On a **non-mobile build target** (Standalone / console / WebGL — including the Editor while one of those
is active) `UnityMobileNotificationBackend` reports `IsSupported == false` and **every operation becomes
a safe no-op**: `Schedule` returns `null`, `ScheduleAll` / `Cancel` / `CancelAll` / `RegisterChannel` /
`OpenNotificationSettings` do nothing, `PermissionStatus` is `NotDetermined`, `IsPermissionGranted` and
`RequestPermissionAsync` return `false`, and `TryGetLaunchNotification` returns `false`. Nothing throws.

With an **Android/iOS build target active — the Editor included** — `IsSupported == true` and the real
`AndroidNotificationCenter` / `iOSNotificationCenter` path runs. Unity's centers are editor-callable, so
the permission and scheduling calls actually execute in Play mode (handy for wiring up the opt-in flow),
but no real OS notification lands until you build to a device.

Either way you write the notification flow once and call it unconditionally — no `#if UNITY_ANDROID` at
your call sites.

## Feeding config from Remote Config

You can drive the quiet-hours window (and its on/off) from `com.tk.remoteconfig` so you can tune it
live from your console. The `Integration Examples` sample ships a one-line bridge:

```csharp
service.QuietHours = RemoteConfigQuietHoursBridge.From(rc);
```

This package has **no dependency** on `com.tk.remoteconfig` — the bridge is a sample you copy into a
project that has both. It reads with `RemoteConfigService.GetBool`/`GetInt`, which fall back to sensible
defaults before config is fetched.

## Gotchas

- **Main-thread-affine.** Unity's notification APIs are main-thread; the service holds no locks. Call it
  from Unity's main thread (marshal from background SDK callbacks first).
- **Local-only in v1 — no push.** This package schedules *local* notifications only. Remote push (FCM)
  is deliberately out of scope, which is what keeps it Firebase-free. Push is a reserved v2 (an
  `IPushBackend` seam + a Firebase sample), not scaffolded now.
- **`DeliveryTime` is device-local wall-clock.** It's not UTC and not affected by time zone travel after
  scheduling — pick times the way a user reads a clock.
- **Re-declare your reminder set each launch.** `ScheduleAll` is the intended pattern: cancel-all-then-
  reschedule the full set on launch/resume (and after a permission or settings change). Don't
  incrementally add on every session, or you'll pile up duplicates.
- **`RegisterChannel` before scheduling on Android.** Channels are an Android concept; register yours
  before you schedule into them. On iOS the channel id is used only for grouping.
