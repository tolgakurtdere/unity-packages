# com.tk.haptics v0.2.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (inline). Binding contracts live in `docs/specs/2026-07-23-tk-haptics-v0.2-design.md` — READ IT FIRST. The Android backend compiles out of the Editor: the harness proves only the seam/service/façade layer; the classification, the bypass flag and the advisory are proven on the MIUI device, and the plan's acceptance matrix is the release gate.

**Goal:** Classify Android vibrations by usage (`Selection`→TOUCH, `Impact`→MEDIA, `Notification`→NOTIFICATION) so the OS touch-vibration setting stops muting gameplay/notification haptics; add the opt-in `Flags=2` OS-preference bypass on top (default off); expose a separate best-effort `SystemTouchVibrationDisabled` advisory.

**Tech Stack:** Unity 6000.3.6f1 harness (`/Users/tolgahankurtdere/Documents/tk-verify`), NUnit EditMode; Android JNI via `AndroidJavaObject` (SDK ≥ 33 gate for the attributes path).

## Global Constraints

- Branch `haptics-v0.2.0` off `main` (`549a55d`). Package → `0.2.0`. **No merge, no tag until the device acceptance matrix passes.**
- Seam break is deliberate and documented (notification-0.2.0 precedent): `IHapticBackend` gains `SystemTouchVibrationDisabled { get; }` + `BypassSystemVibrationSetting { get; set; }`.
- Raw constants over JNI (frozen API, verified against android.jar 35): `USAGE_TOUCH=18`, `USAGE_MEDIA=19`, `USAGE_NOTIFICATION=49`, bypass flag `2` (non-public — label it as such at the definition site).
- Harness gate baseline **443** (trust `results.xml`); zero `error CS`/`warning CS` under `Packages/com.tk`. Harness `com.tk.haptics` already points at the main tree — this branch is checked out there, so no manifest juggling.
- Conventional commits, trailer `Co-Authored-By: Claude <noreply@anthropic.com>`.

### Task 1: Seam + service + façade + fake + tests (the harness-verifiable layer)

**Files:** modify `Runtime/IHapticBackend.cs`, `Runtime/NullHapticBackend.cs`, `Runtime/HapticService.cs`, `Runtime/Haptics.cs`, `Runtime/IosHapticBackend.cs` (inert members), `Tests/Editor/FakeHapticBackend.cs`, `Tests/Editor/HapticServiceTests.cs`, `Tests/Editor/HapticsFacadeTests.cs`.

- Seam: the two members, XML-doc'd per spec (advisory = best-effort, false when unknown; bypass = default false, Android-only effect).
- `NullHapticBackend` + `IosHapticBackend`: `SystemTouchVibrationDisabled => false`; `BypassSystemVibrationSetting` auto-property (stored, inert).
- `HapticService`: both mirrored as plain forwards to `_backend` (no guard needed beyond the seam's must-not-throw contract, consistent with `IsSupported`).
- `Haptics` façade: mirrors; unbound → advisory `false`, bypass get `false` / set no-op (existing warn-once pattern).
- `FakeHapticBackend`: settable advisory knob; bypass auto-property.
- Tests: service + façade forward the advisory; bypass set through service/façade reaches the backend and reads back; both default false; unbound façade returns false without throwing.
- [ ] Implement + gate (expect ~447, trust results.xml, 0 warnings) + commit `feat(haptics): SystemTouchVibrationDisabled advisory + BypassSystemVibrationSetting across the seam`.

### Task 2: Android backend — attributes classification + bypass + advisory (compiled-out; review + device)

**Files:** modify `Runtime/AndroidHapticBackend.cs` only.

- Constants: `UsageTouch=18`, `UsageMedia=19`, `UsageNotification=49`, `FlagBypassUserVibrationIntensityOff=2` — the last one commented as **non-public** (AOSP-internal value; device-verified, may be stripped by an OEM — in which case behavior degrades to plain classification).
- One vibrate helper replaces the three direct `_vibrator.Call("vibrate", effect)` sites (`VibratePredefined`, `VibrateWaveform`, `VibrateSimple` — each gains a `usage` parameter threaded from `Impact`/`Selection`/`Notification`):
  ```csharp
  private void Vibrate(AndroidJavaObject effect, int usage)
  {
      if (_sdk >= 33)
      {
          var attributes = GetAttributes(usage);       // lazily built, cached per usage
          if (attributes != null) { _vibrator.Call("vibrate", effect, attributes); return; }
      }
      _vibrator.Call("vibrate", effect);               // < API 33 or attribute construction failed: 0.1.1 behavior
  }
  ```
- `GetAttributes(usage)`: cache slot per usage; build via `new AndroidJavaObject("android.os.VibrationAttributes$Builder")` → `setUsage(usage)` → if bypass `setFlags(2, 2)` → `build()`; guarded try/catch → null (fall back to plain vibrate, warn once). `BypassSystemVibrationSetting` setter disposes/invalidates the cache so the next vibrate rebuilds with/without the flag.
- `SystemTouchVibrationDisabled`: live JNI read, guarded → false: `activity.getContentResolver()` + `Settings$System.getInt(resolver, "haptic_feedback_enabled", 1) == 0`. (Note: the ctor currently drops the activity after grabbing the vibrator — keep a cached `_resolver` alongside `_vibrator`.)
- Risk noted from the spec, checked on device: the two-object `Call("vibrate", effect, attrs)` must bind the `VibrationAttributes` overload, not the deprecated `AudioAttributes` one.
- [ ] Implement + gate (compiles out — 0 warnings, total unchanged) + **read the full file once end-to-end** (the Notification-catch miss in 0.1.1 got through because the gate can't see this file) + commit `feat(haptics): classify Android vibrations by usage; opt-in OS-preference bypass; settings advisory`.

### Task 3: Docs + version

**Files:** `README.md`, `CHANGELOG.md`, `package.json`.

- README: replace the MIUI paragraph's "(planned for the next minor)" with the shipped behavior; new **System settings, usage classification & bypass** section — the usage table, `BypassSystemVibrationSetting` (what it does, that the flag is non-public/OEM-dependent, default off, degrades to classification), `SystemTouchVibrationDisabled` with a settings-screen hint snippet; note `IsSupported` is *unchanged* and why the advisory is separate.
- CHANGELOG `## [0.2.0] - Unreleased`: Added (classification, bypass, advisory) + Changed-breaking (seam members; custom-backend authors only).
- `package.json` → `0.2.0`. Root doc pins untouched until the tag.
- [ ] Gate + commit `docs(haptics): usage classification, bypass and advisory (0.2.0)`.

### Task 4: Device acceptance matrix + release

- [ ] Shikaku note (file: override on this branch), device = the MIUI phone, **touch vibration OFF**:
  1. Bypass off → `Impact`/`Notification` `finished` with `Usage=MEDIA`/`NOTIFICATION`; `Selection` `ignored_for_settings`. ← headline proof
  2. `Haptics.BypassSystemVibrationSetting = true` → `Selection` `finished`, dumpsys shows `Flags=2`. If `Flags=0` → the platform stripped the non-public bit: STOP, report, decide (ship as classification-only or chase the OEM path).
  3. `SystemTouchVibrationDisabled` true ↔ setting off, false ↔ on.
  4. Touch vibration ON regression: all `finished`; editor no-op unchanged.
- [ ] On green: date CHANGELOG, bump pins (root README, QUICKSTART, ROADMAP, package README), release commit, annotated tag `com.tk.haptics/0.2.0`, user pushes, shikaku re-pins to the tag.
