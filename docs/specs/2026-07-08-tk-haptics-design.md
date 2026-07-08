# com.tk.haptics 0.1.0 — Design

Approved by Tolgahan 2026-07-08 (brainstorm Q&A — all four decisions locked to the recommended option). Ready first consumer: game-shikaku's `SettingsService.Vibration` flag (default true) is wired to the settings UI but has no consumer — exactly the seam audio's Sound/Music flags were. Reference: g-brain's `ButtonBase.Haptic()` hook (commented `HapticManager.Haptic(HapticType.Selection)`) — an iOS-style haptic taxonomy fired on button press.

## Locked decisions

1. **Backend: in-package native, both platforms + no-op fallback, zero dependencies.** Android via `AndroidJavaObject`; iOS via an embedded `.mm` Taptic plugin; everything else (incl. the Editor) is a no-op. The game supplies nothing.
2. **Taxonomy: the full iOS `UIFeedbackGenerator` set** — `Impact(Light/Medium/Heavy)`, `Selection()`, `Notification(Success/Warning/Error)`. Mapped to vibration effects on Android.
3. **Per-type min-interval throttle** — a haptic type replayed within its window is dropped (button spam / list scroll); types throttle independently.
4. **Game-owned enable state, no package persistence** — `Enabled` is plain runtime state; the game pushes it from its own settings service (shikaku mode) and persists the one bool itself. **Revised 2026-07-08 (post-approval):** originally an optional `ISaveSystem` (audio pattern), changed to drop the `com.tk.core` dependency so haptics is truly standalone — the thinnest-package philosophy, and shikaku used game-pushed anyway so the integration is unaffected.

## Verification reality (important)

Haptics fire ONLY on a real iOS/Android device — never in the Editor, the harness, or play mode. So the harness gate verifies **logic only** (enable gate, throttle, backend selection, taxonomy dispatch, the no-op fallback) via an injected fake backend. The actual buzz is a **manual on-device step** the user runs (build game-shikaku to a phone, toggle Vibration, press a button). This is called out in the CHANGELOG/README and the release report; the tag lands on device verification like the other packages, but that verification is a device build rather than an in-editor play-mode session.

## Components

`HapticService` (plain class, constructed at the composition root, registered in `AppContext`, optionally bound to the static `Haptics` façade):

- `HapticImpact { Light, Medium, Heavy }`, `HapticNotification { Success, Warning, Error }` (enums).
- `void Impact(HapticImpact strength)`, `void Selection()`, `void Notification(HapticNotification type)`.
- `bool Enabled { get; set; }` — game-owned runtime state (default true); the package does NOT persist it. `event Action Changed` raised when it changes (so a reactive settings UI can mirror a code-side toggle).
- `bool IsSupported` — true when the active backend actually vibrates (false in Editor / unsupported platforms), so a settings screen can hide the toggle where haptics don't exist.
- Constructor: `HapticService(IHapticBackend backend = null)` — `backend` defaults to the platform pick (`AndroidHapticBackend`/`IosHapticBackend`/`NullHapticBackend`); the parameter exists so tests inject a fake. No save-system parameter (standalone).
- Throttle: per-type `minInterval` (unscaled time; default ~0.03 s), settable via `HapticThrottleSeconds`.
- Every play method: no-op when `!Enabled`, when throttled, or when the backend is unsupported.

**`IHapticBackend`** (seam): `void Impact(HapticImpact)`, `void Selection()`, `void Notification(HapticNotification)`, `bool IsSupported`.
- `NullHapticBackend` — no-op, `IsSupported = false` (Editor + unsupported platforms; the `#else` branch).
- `AndroidHapticBackend` (`#if UNITY_ANDROID && !UNITY_EDITOR`) — cached `AndroidJavaObject` Vibrator (via `getSystemService`, or `VibratorManager` on API 31+). Predefined effects (`EFFECT_CLICK`/`EFFECT_TICK`/`EFFECT_HEAVY_CLICK`) on API 29+; amplitude one-shots (`VibrationEffect.createOneShot`) on API 26+; plain `vibrate(ms)` below. Map: Impact L/M/H → tick / click / heavy-click (or short/med/long amplitude one-shots); Selection → tick; Notification → a short pattern per type. `IsSupported` = has a vibrator.
- `IosHapticBackend` (`#if UNITY_IOS && !UNITY_EDITOR`) — `[DllImport("__Internal")]` into `Plugins/iOS/TKHaptics.mm`: `_TKHapticImpact(int)`, `_TKHapticSelection()`, `_TKHapticNotification(int)` calling `UIImpactFeedbackGenerator`/`UISelectionFeedbackGenerator`/`UINotificationFeedbackGenerator`. `IsSupported = true`.

**Static `Haptics` façade** (Analytics/Audio pattern): `Bind`/`Unbind`, mirrored `Impact`/`Selection`/`Notification` + `Enabled`/`IsSupported`, warn-once no-op when unbound, `SubsystemRegistration` reset.

## Out of scope (later / a real consumer)
Custom patterns/waveforms (Core Haptics AHAP, Android `VibrationEffect.Composition`), continuous / amplitude-envelope haptics, gamepad rumble, `prepare()` pre-warm optimization, per-type intensity config.

## Testing
EditMode (fake backend injected): disabled → no backend call; enabled → the right backend method for each taxonomy value; throttle drops a rapid repeat of the same type but not a different type; `Changed` fires on a real toggle, not a no-op; `Enabled` defaults true and is settable; `IsSupported` reflects the backend; static façade routing + unbound warn-once. The real `AndroidJavaObject`/`.mm` calls and actual haptic feedback are device-verified (harness uses `NullHapticBackend` — the real backends are compiled only under their platform defines, so the harness compiles them out).
