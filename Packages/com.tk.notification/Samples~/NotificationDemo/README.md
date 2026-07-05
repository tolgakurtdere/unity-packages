# Notification Demo

Editor-runnable demo of the full `NotificationService` flow on the real `UnityMobileNotificationBackend`,
driven entirely from `[ContextMenu]` entries. It's the fastest way to see the API shape and the
cancel-all-then-reschedule engagement pattern.

## Editor vs device

In the Editor (and on any non-mobile target) the backend reports `IsSupported == false` and **every
call is a safe no-op** — nothing schedules and nothing throws, so you can press the menu items freely
and read the `[NotificationDemo]` logs to follow the flow. To see real notifications appear in the
system tray, **build to Android or iOS**. The same code runs on both — no `#if UNITY_ANDROID` at the
call site.

## Running it

This sample ships as source only — there's no `.unity` scene file (a scene needs Editor-generated
GUIDs, which don't survive being authored outside the Editor). To try it:

1. Import this sample (Package Manager → **TK Notification** → Samples → **Notification Demo**).
2. Create a new empty scene.
3. Add an empty GameObject and attach the `NotificationDemo` component to it.
4. Press Play. Watch the Console for `[NotificationDemo]` logs.

Then right-click the component's header in the Inspector (or use its "⋮" context menu) to reach the
demo actions:

| Menu entry | What it does |
| --- | --- |
| Register Channel | `RegisterChannel(new NotificationChannel("main","General","General reminders",High))` — needed on Android before scheduling. |
| Request Permission | `await RequestPermissionAsync()` — logs the result (the real OS prompt only appears on a device). |
| Schedule Engagement Funnel | Builds a 1/3/7/14/30-day reminder set at `DateTime.Now + n days` and `ScheduleAll`s it — cancels the old set and re-declares this one. |
| Cancel All | `CancelAll()` — clears every scheduled notification. |
| Check Launch | `TryGetLaunchNotification(out var r)` — if the app was opened from a notification tap, logs its channel + data. |

## The engagement pattern

`Schedule Engagement Funnel` is the core retention move: call `ScheduleAll(...)` with your current
reminder set on every launch (and after a permission or settings change). It cancels everything
already scheduled and re-declares the set in order, so the OS always holds exactly your latest set —
no drift, no duplicates. The demo builds placeholder copy at absolute delivery times; in a real game
the content, timing, and localization are yours (see the `Integration Examples` sample's
`EngagementReminders` for a copy-and-adapt builder).

The service here is constructed with quiet hours `23:00–07:00`, so a reminder that would fire inside
that window is shifted forward to `07:00` before scheduling — see the package README's **Quiet hours**
section.
