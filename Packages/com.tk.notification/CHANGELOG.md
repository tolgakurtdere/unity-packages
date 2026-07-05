# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-05

### Added

- `NotificationService` — local-notification orchestration behind `INotificationBackend`: applies
  quiet-hours before scheduling, cancel-all-then-reschedule (`ScheduleAll`), single `Schedule`/`Cancel`,
  async permission (`RequestPermissionAsync` / `IsPermissionGranted`), launch routing
  (`TryGetLaunchNotification`), channel registration, and `OpenNotificationSettings`. Every backend call
  is guarded (must-not-throw contract).
- `QuietHoursSettings` — pure struct with `Apply()` that shifts a fire time out of a `[StartHour, EndHour)`
  do-not-disturb window, including wrapping windows across midnight; `default` is disabled.
- Typed value types — `NotificationChannel`, `NotificationRequest`, `NotificationResponse`,
  `NotificationImportance` — so no `Unity.Notifications.*` type leaks into the public API.
- `INotificationBackend` seam plus `UnityMobileNotificationBackend`, the real Android/iOS implementation
  (backed by `com.unity.mobile.notifications`) that reports `IsSupported == false` and no-ops on every
  non-mobile target.
- Samples: **Notification Demo** (editor-runnable `[ContextMenu]` demo with the engagement-funnel
  reschedule pattern) and **Integration Examples** (`EngagementReminders` funnel builder,
  `RemoteConfigQuietHoursBridge`, and `LaunchRouter`).

[0.1.0]: https://github.com/tolgakurtdere/unity-packages/releases/tag/com.tk.notification/0.1.0
