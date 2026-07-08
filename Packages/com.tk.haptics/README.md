# TK Haptics

Cross-platform haptic feedback for Unity games: `Impact` / `Selection` / `Notification` with an enable toggle, per-type throttling, and native iOS (Taptic) + Android (Vibrator) backends behind a testable seam with a no-op fallback. Thin — the game supplies nothing beyond an on/off preference.

## Install

Standalone — no dependencies, no other `com.tk.*` package required.

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.haptics
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.haptics#com.tk.haptics/0.1.0
```

## Quickstart

Construct once at your composition root, register/bind:

```csharp
using TK.Haptics;

var haptics = new HapticService();
Context.Register(haptics);
Haptics.Bind(haptics);          // optional static sugar: Haptics.Selection() anywhere
```

Fire feedback:

```csharp
Haptics.Selection();                       // light tick — selection / button press
Haptics.Impact(HapticImpact.Medium);       // Light / Medium / Heavy
Haptics.Notification(HapticNotification.Success);  // Success / Warning / Error
```

**Settings wiring (the game owns the toggle + its persistence):**

```csharp
haptics.Enabled = settings.VibrationEnabled;                 // push the saved value on init
settings.Changed += () => haptics.Enabled = settings.VibrationEnabled;
```

`Enabled` is plain runtime state — the package never persists it (that's why it needs no save system and no `com.tk.core`). Persist the one bool in your own settings store. `haptics.Changed` fires if code elsewhere toggles it, so a reactive settings UI can mirror it.

## Verification is device-only

Haptics fire **only on a real iOS/Android device** — never in the Editor, play mode, or CI. In the Editor `IsSupported` is `false` and every call is a silent no-op. So: unit tests cover the logic (enable gate, throttle, taxonomy dispatch) via a fake backend, but the actual buzz must be checked on a phone build.

## Gotchas

- **Check `IsSupported` before showing a Vibration toggle** — it's `false` in the Editor and on platforms without a vibrator, so you can hide the setting where haptics don't exist.
- **The Editor is always a no-op** (`NullHapticBackend`). Don't expect feedback in play mode; build to a device.
- **Throttle:** an identical haptic repeated within `HapticThrottleSeconds` (default 0.03 s) is dropped; distinct haptics don't throttle each other. Set `HapticThrottleSeconds = 0` to disable.
- **Android** maps to predefined effects (API 29+) / amplitude one-shots (API 26+) / plain vibration below; a device without a vibrator reports `IsSupported = false`. **iOS** uses `UIFeedbackGenerator` via an embedded native plugin.
- The service holds no `IDisposable` resources; no teardown call needed.
- **Standalone** — `Enabled` is game-owned runtime state, so the package has zero dependencies. Persist the toggle in your own settings store (one bool).
