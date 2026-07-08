# com.tk.haptics v0.1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (inline). Binding contracts live in `docs/specs/2026-07-08-tk-haptics-design.md` — READ IT FIRST. Haptics fire on-device only; the harness verifies logic via a fake backend (the real Android/iOS backends compile out of the Editor).

**Goal:** Cross-platform haptic feedback — `Impact`/`Selection`/`Notification`, enable toggle (optional persistence) + `Changed`, per-type throttle, `IHapticBackend` seam with Android (`AndroidJavaObject`) + iOS (embedded `.mm`) reals and a no-op fallback, plus a static `Haptics` façade.

**Tech Stack:** Unity 6000.3.6f1 harness, NUnit EditMode. Standalone — no package dependency, no `com.tk.*` prerequisite (revised 2026-07-08 post-approval: dropped the `com.tk.core`/`ISaveSystem` persistence to make haptics truly standalone; `Enabled` is game-owned runtime state).

## Global Constraints

- Repo branch `haptics-v0.1.0` off main (`a2d4e14` = tag `com.tk.audio/0.3.0`). **No merge, no tag** — wait for device verification. New package root `Packages/com.tk.haptics/`.
- package.json: name `com.tk.haptics`, version `0.1.0`, displayName `TK Haptics`, unity `6000.0`, dependencies `{}`, author Tolga Kurtdere, keywords `["tk","haptics","vibration","feedback","mobile"]`.
- Asmdef `TK.Haptics` (rootNamespace `TK.Haptics`, references `[]`, autoReferenced true). Tests asmdef `TK.Haptics.Tests` (Editor-only, refs `["TK.Haptics","UnityEngine.TestRunner","UnityEditor.TestRunner"]`, overrideReferences + nunit, `UNITY_INCLUDE_TESTS`, autoReferenced false).
- Every file/folder gets a hand-written `.meta` written with the `.cs`. The iOS `.mm` gets a `.meta` with `PluginImporter` set to iOS-only (write it explicitly — the harness won't generate a correct plugin meta headless).
- Harness gate: add `com.tk.haptics` as the 10th `file:` package + testable. Baseline before Task 1 = current total after audio (**~410 — trust results.xml**). Zero `error CS`/`warning CS` under `Packages/com.tk`.
- Conventional commits, trailer `Co-Authored-By: Claude <noreply@anthropic.com>`.

### Task 1: Skeleton + enums + IHapticBackend + NullHapticBackend + HapticService core + fake-backed tests

**Files:** `package.json`, `Runtime/TK.Haptics.asmdef`, `Runtime/HapticImpact.cs`, `Runtime/HapticNotification.cs`, `Runtime/IHapticBackend.cs`, `Runtime/NullHapticBackend.cs`, `Runtime/HapticService.cs`; `Tests/Editor/TK.Haptics.Tests.asmdef`, `Tests/Editor/FakeSaveSystem.cs`, `Tests/Editor/FakeHapticBackend.cs`, `Tests/Editor/HapticServiceTests.cs`; harness `manifest.json`.

- Enums per spec. `IHapticBackend` per spec (Impact/Selection/Notification + `IsSupported`). `NullHapticBackend`: no-op, `IsSupported=false`.
- `HapticService(IHapticBackend backend = null)`: `backend ??= CreatePlatformBackend()` (Task 1 returns `NullHapticBackend`; Task 2/3 add the reals behind platform defines). `Enabled` = game-owned runtime bool (default true) + `Changed`, `IsSupported => _backend.IsSupported`, `HapticThrottleSeconds` (default 0.03), per-type `Time.unscaledTime` throttle map. `Impact/Selection/Notification`: return when `!Enabled` or `!_backend.IsSupported` or throttled; else dispatch to `_backend`.
- `FakeHapticBackend`: records calls (list of strings / counters) + settable `IsSupported`.
- Tests: disabled → no backend call; enabled → correct backend method per taxonomy value; throttle drops a rapid same-type repeat, allows a different type; unsupported backend → no call; `Changed` fires on real toggle not no-op; `Enabled` defaults true + settable; `IsSupported` reflects the backend.
- [ ] Implement + harness-wire + gate + commit `feat(haptics): service core + null backend + taxonomy/throttle/persistence`.

### Task 2: Android backend

**Files:** `Runtime/AndroidHapticBackend.cs` (whole file under `#if UNITY_ANDROID && !UNITY_EDITOR`).

- Cache `AndroidJavaObject` vibrator: API 31+ `VibratorManager.getDefaultVibrator()`, else `activity.getSystemService("vibrator")`. `IsSupported = vibrator != null && vibrator.Call<bool>("hasVibrator")`.
- SDK-level dispatch (read `AndroidVersion` from `android.os.Build$VERSION.SDK_INT`): API 29+ predefined `VibrationEffect.createPredefined(EFFECT_TICK/CLICK/HEAVY_CLICK)`; API 26+ `VibrationEffect.createOneShot(ms, amplitude)`; below → `vibrator.Call("vibrate", (long)ms)`. Map Impact L/M/H, Selection (tick), Notification (per-type pattern via `createWaveform` on 26+, else a short vibrate).
- Guard every JNI call in try/catch → `Debug.LogWarning` and treat as unsupported on failure (never throw into game code).
- `HapticService.CreatePlatformBackend()` returns `new AndroidHapticBackend()` under the Android define.
- Not harness-testable (compiled out of Editor) — review + device verified. No test file.
- [ ] Implement + gate (compiles out, still green) + commit `feat(haptics): Android Vibrator/VibrationEffect backend`.

### Task 3: iOS backend + native plugin

**Files:** `Runtime/IosHapticBackend.cs` (under `#if UNITY_IOS && !UNITY_EDITOR`), `Plugins/iOS/TKHaptics.mm` (+ its iOS-only `.meta`).

- `TKHaptics.mm`: C functions `_TKHapticImpact(int style)` (0/1/2 → light/medium/heavy `UIImpactFeedbackGenerator`), `_TKHapticSelection()` (`UISelectionFeedbackGenerator`), `_TKHapticNotification(int type)` (0/1/2 → success/warning/error `UINotificationFeedbackGenerator`). Create generator, call, done (no prepare in v1).
- `IosHapticBackend`: `[DllImport("__Internal")]` externs + the interface mapping; `IsSupported = true`.
- `.mm.meta`: `PluginImporter` with only iOS enabled (write the meta by hand — platformData for iOS true, others false).
- `HapticService.CreatePlatformBackend()` returns `new IosHapticBackend()` under the iOS define.
- Not harness-testable — review + device verified.
- [ ] Implement + gate (compiles out, still green) + commit `feat(haptics): iOS UIFeedbackGenerator backend (embedded .mm)`.

### Task 4: Static façade + docs + version

**Files:** `Runtime/Haptics.cs`; `Tests/Editor/HapticsFacadeTests.cs`; `Packages/com.tk.haptics/README.md`, `CHANGELOG.md`.

- `Haptics` static: `Bind`/`Unbind`, mirrors (`Impact`/`Selection`/`Notification`/`Enabled`/`IsSupported`), warn-once no-op unbound, `SubsystemRegistration` reset.
- Tests: bind routing to a fake-backed service; unbound warn-once + default `IsSupported=false`.
- README: install (pin `#com.tk.haptics/0.1.0`), quickstart (construct + register + bind + push `Enabled` from a settings service + a `Haptics.Selection()` on button press), the **device-only verification** note, gotchas (Editor is always a no-op / `IsSupported=false`; check `IsSupported` before showing a Vibration toggle). CHANGELOG `[0.1.0] - Unreleased`.
- Root README/QUICKSTART/ROADMAP: **not** touched until the tag (device verification gate).
- [ ] Gate + commit `feat(haptics): static Haptics facade + docs`.

### Task 5: Report
- [ ] Report: branch, test totals, and the device verification checklist (build game-shikaku to a phone; Vibration toggle in settings; a `Selection` on button press; `Impact`/`Notification` sampling; confirm Editor is a silent no-op and `IsSupported` is false there). Tag `com.tk.haptics/0.1.0` after the user's device check.
