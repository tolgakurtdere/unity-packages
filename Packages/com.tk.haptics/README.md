# TK Haptics

Cross-platform haptic feedback for Unity games: `Impact` / `Selection` / `Notification` with an enable toggle, per-type throttling, and native iOS (Taptic) + Android (Vibrator) backends behind a testable seam with a no-op fallback. Thin — the game supplies nothing beyond an on/off preference.

## Install

Requires `com.tk.core` installed first (uses its `TK.Core.Save`'s `ISaveSystem` for optional persistence).

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

// saveSystem is optional: null = runtime-only (your settings service owns the toggle);
// supply one and the package persists Enabled itself (key "tk_haptics_settings").
var haptics = new HapticService(saveSystem: null);
Context.Register(haptics);
Haptics.Bind(haptics);          // optional static sugar: Haptics.Selection() anywhere
```

Fire feedback:

```csharp
Haptics.Selection();                       // light tick — selection / button press
Haptics.Impact(HapticImpact.Medium);       // Light / Medium / Heavy
Haptics.Notification(HapticNotification.Success);  // Success / Warning / Error
```

**Settings wiring (game-owned toggle):**

```csharp
settings.Changed += () => haptics.Enabled = settings.VibrationEnabled;
```

Or pass an `ISaveSystem` and let the package persist it — pick ONE owner. Subscribe to `haptics.Changed` to reflect code-side changes in a bound toggle.

## Verification is device-only

Haptics fire **only on a real iOS/Android device** — never in the Editor, play mode, or CI. In the Editor `IsSupported` is `false` and every call is a silent no-op. So: unit tests cover the logic (enable gate, throttle, taxonomy dispatch) via a fake backend, but the actual buzz must be checked on a phone build.

## Gotchas

- **Check `IsSupported` before showing a Vibration toggle** — it's `false` in the Editor and on platforms without a vibrator, so you can hide the setting where haptics don't exist.
- **The Editor is always a no-op** (`NullHapticBackend`). Don't expect feedback in play mode; build to a device.
- **Throttle:** an identical haptic repeated within `HapticThrottleSeconds` (default 0.03 s) is dropped; distinct haptics don't throttle each other. Set `HapticThrottleSeconds = 0` to disable.
- **Android** maps to predefined effects (API 29+) / amplitude one-shots (API 26+) / plain vibration below; a device without a vibrator reports `IsSupported = false`. **iOS** uses `UIFeedbackGenerator` via an embedded native plugin.
- The service holds no `IDisposable` resources; no teardown call needed.
