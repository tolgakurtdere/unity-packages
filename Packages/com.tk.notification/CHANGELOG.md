# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - Unreleased

Adds a tri-state permission status so a "turn notifications on" flow can branch prompt-vs-redirect, and folds in the 0.1.x doc-accuracy fix. Additive API + a small property-shape break. Tag after game-shikaku verification.

### Added

- **`NotificationPermission` (tri-state) + `NotificationService.PermissionStatus` property.** Surfaces the OS permission state — `NotDetermined` / `Denied` / `Authorized` — so a settings opt-in can branch: `NotDetermined` → `RequestPermissionAsync` (native prompt); `Denied` → `OpenNotificationSettings` (the OS won't prompt again); `Authorized` → schedule. The OS is the source of truth — no persisted "hasRequested" flag to drift across reinstalls / external settings changes. Platform mapping: iOS `AuthorizationStatus` (Authorized/Provisional/Ephemeral → Authorized); Android `UserPermissionToPost` (Allowed → Authorized; Denied / notifications-blocked-in-settings → Denied; pre-13 with no block reports Allowed); non-mobile / unavailable → `NotDetermined`.

### Changed (breaking)

- **`IsPermissionGranted` is now a property, not a method.** Migration: `svc.IsPermissionGranted()` → `svc.IsPermissionGranted`. It is now derived from `PermissionStatus == Authorized`; on iOS this means it also returns true for provisional/ephemeral authorization (previously only strict `Authorized`). Permitted in a 0.x minor bump per SemVer.
- **Seam: `INotificationBackend.IsPermissionGranted()` replaced by `NotificationPermission PermissionStatus { get; }`.** Custom-backend authors implement the property (the service derives the bool). No effect on consumers using the shipped `UnityMobileNotificationBackend`.

### Fixed

- **`IsSupported` is driven by the active build target, not the Editor.** The docs said `IsSupported == false` "in the Editor" and every op no-ops there — inaccurate on an Android/iOS build target, where `UNITY_ANDROID`/`UNITY_IOS` is defined in the Editor too, so the real `AndroidNotificationCenter` / `iOSNotificationCenter` path runs in the Editor (editor-callable; permission and scheduling execute, but no real device notification lands off-device). Reworded to be build-target-based: the README "Platform support" section (renamed from "Non-mobile targets") plus the intro and "What's inside" row, the `INotificationBackend` / `UnityMobileNotificationBackend` XML docs, and the `NotificationDemo` sample's log messages. The guard itself is unchanged — an editor-callable real path was the intended behavior (it lets a consumer exercise the opt-in flow in-editor); only the docs were wrong.

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
