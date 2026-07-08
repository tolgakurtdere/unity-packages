# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - Unreleased

Thin cross-platform haptic feedback. Approved design: `docs/specs/2026-07-08-tk-haptics-design.md`. Logic is unit-tested via a fake backend; the actual haptics are **device-only** (never fire in the Editor) — the tag lands after an on-device check in the consuming game.

### Added

- **`HapticService`** — `Impact(Light/Medium/Heavy)`, `Selection()`, `Notification(Success/Warning/Error)` (the iOS `UIFeedbackGenerator` taxonomy). `Enabled` (durable, optional `ISaveSystem` persistence + `Changed` event), `IsSupported`, and a per-type unscaled-time throttle (`HapticThrottleSeconds`, default 0.03; identical haptics throttle, distinct ones don't). Every call is a no-op when disabled, throttled, or unsupported.
- **`IHapticBackend`** seam with three impls: `AndroidHapticBackend` (Vibrator — predefined effects on API 29+, amplitude one-shots on API 26+, plain vibration below; JNI calls guarded), `IosHapticBackend` (`UIFeedbackGenerator` via an embedded `Plugins/iOS/TKHaptics.mm`), and `NullHapticBackend` (Editor / unsupported platforms). The real backends compile only under their platform defines.
- **`Haptics`** static façade — `Bind`/`Unbind` + mirrored calls; warn-once no-op when unbound; domain-reload-off reset.

### Notes

- Zero package dependencies (native calls only); documented prerequisite `com.tk.core` for the `ISaveSystem` seam.
