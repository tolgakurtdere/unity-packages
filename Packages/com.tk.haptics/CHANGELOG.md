# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - Unreleased

Android haptics never fired: the package shipped an Android backend without declaring the permission that backend needs, and then hid the failure. Tag after on-device verification.

### Fixed

- **`android.permission.VIBRATE` is now declared by the package** — `Plugins/Android/TKHaptics.androidlib`, whose manifest Unity merges into the app's. The backend reaches the `Vibrator` through raw JNI, and Unity only auto-injects that permission when it detects `Handheld.Vibrate()` usage, so nothing ever declared it and every `vibrate()` call threw `SecurityException` on device. Consumers need no manifest edit. The library ships its own `build.gradle`: AGP 8 requires every library module to declare a namespace, and Unity's `libTemplate.gradle` fills that token from the very `package` attribute AGP 8 forbids — so the module has to name its namespace in Gradle instead. That build file also re-declares `sourceSets { main { manifest.srcFile 'AndroidManifest.xml' } }`, which the replaced template used to provide; without it AGP would look for `src/main/AndroidManifest.xml`, find nothing, and merge no permission while still reporting a successful build.
- **A denied vibrate call now demotes `IsSupported` to `false` for the session and logs an error.** Previously `IsSupported` came from `hasVibrator()` — a capability query needing no permission — so it stayed `true` while the `SecurityException` was swallowed into a `LogWarning` that release builds suppress by default (`com.tk.core`'s `disableLogsInReleaseBuilds` is on by default). The result was a Vibration toggle sitting over a dead backend with no signal at all. **Read `IsSupported` live rather than caching it at boot.**
- **Docs corrected.** The `AndroidHapticBackend` XML doc claimed "Device-verified." for a path that had never vibrated on a device. Replaced with what is true, and the README now documents the shipped permission, the runtime demotion, and the MIUI `haptic_feedback_enabled` red herring (it governs `View.performHapticFeedback`, not direct `Vibrator.vibrate()`).

## [0.1.0] - 2026-07-08

Thin cross-platform haptic feedback. Approved design: `docs/specs/2026-07-08-tk-haptics-design.md`. Logic is unit-tested via a fake backend; the actual haptics are **device-only** (never fire in the Editor). Editor-verified in game-shikaku (wired to the settings Vibration toggle; `IsSupported` false and every call a safe no-op in-editor without throwing); real on-device buzz is confirmed on the game's device build.

### Added

- **`HapticService`** — `Impact(Light/Medium/Heavy)`, `Selection()`, `Notification(Success/Warning/Error)` (the iOS `UIFeedbackGenerator` taxonomy). `Enabled` (game-owned runtime state + `Changed` event — the game persists the one bool), `IsSupported`, and a per-type unscaled-time throttle (`HapticThrottleSeconds`, default 0.03; identical haptics throttle, distinct ones don't). Every call is a no-op when disabled, throttled, or unsupported.
- **`IHapticBackend`** seam with three impls: `AndroidHapticBackend` (Vibrator — predefined effects on API 29+, amplitude one-shots on API 26+, plain vibration below; JNI calls guarded), `IosHapticBackend` (`UIFeedbackGenerator` via an embedded `Plugins/iOS/TKHaptics.mm`), and `NullHapticBackend` (Editor / unsupported platforms). The real backends compile only under their platform defines.
- **`Haptics`** static façade — `Bind`/`Unbind` + mirrored calls; warn-once no-op when unbound; domain-reload-off reset.

### Notes

- Standalone: zero dependencies and no `com.tk.*` prerequisite. `Enabled` is game-owned runtime state (the game persists it), which is why no save-system seam / `com.tk.core` reference is needed.
