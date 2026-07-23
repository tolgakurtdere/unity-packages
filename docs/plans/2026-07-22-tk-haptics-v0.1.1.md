# com.tk.haptics v0.1.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (inline). This is a **bugfix for a shipped package that does not work on Android at all**. The root-cause chain was confirmed against the code before planning (see below) — do not re-litigate it, but do not weaken the verification gate either: the fix is NOT done until a real Android device buzzes.

**Goal:** Make Android haptics actually fire — the package must declare `android.permission.VIBRATE` itself — and stop lying about it when it can't (`IsSupported` must go false on a permission failure instead of silently swallowing a `SecurityException`).

**Tech Stack:** Unity 6000.3.6f1 harness (EditMode/NUnit), Android IL2CPP/ARM64 + GameActivity on the consumer side (game-shikaku, minSdk 26).

## Confirmed root cause (verified against code, 2026-07-22)

1. Ctor sets `_supported` from `hasVibrator()` — a capability query needing no permission — so `IsSupported` is true even with no VIBRATE permission (`AndroidHapticBackend.cs:42`).
2. Every play path calls `_vibrator.Call("vibrate", …)` (`:134`, `:141`, `:151`, `:155`), which requires `android.permission.VIBRATE` → `SecurityException` without it.
3. Each public method catches and `Debug.LogWarning`s (`:78`, `:92`, `:124`); `com.tk.core`'s `AppBootstrapper.disableLogsInReleaseBuilds` defaults **true** and kills `logEnabled` in *every* non-Editor build — so the warning is invisible exactly where it matters.
4. The backend reaches the Vibrator through raw JNI, never `Handheld.Vibrate()`, so Unity's automatic manifest injection never triggers.
5. The package ships `Plugins/iOS` only — no `Plugins/Android`, no manifest fragment anywhere.

## Global Constraints

- Branch `haptics-v0.1.1` off `main` (`99081f2`). Package `Packages/com.tk.haptics/`, version → `0.1.1`.
- **No merge, no tag until device verification passes** — the previous release's `Device-verified.` claim is exactly what failed; do not repeat it.
- Every new file/folder gets a hand-written `.meta` (the headless harness will not generate them). The `.androidlib` folder and the `AndroidManifest.xml` inside it both need one.
- AGP 8 (Unity 6): a library manifest must **not** carry a `package=` attribute — namespace moved to Gradle. A wrong manifest fails the consumer's build.
- Android backend compiles out of the Editor (`#if UNITY_ANDROID && !UNITY_EDITOR`) → it is **never** gate-compiled. Its changes are review-verified + device-verified only. Do not claim otherwise in code comments.
- Harness gate: baseline **426** (trust `results.xml`). Zero `error CS` / `warning CS` under `Packages/com.tk`.
- Conventional commits, trailer `Co-Authored-By: Claude <noreply@anthropic.com>`.

### Task 1: Ship the VIBRATE permission from the package

**Files:** create `Plugins/Android/TKHaptics.androidlib/AndroidManifest.xml`, `Plugins/Android/TKHaptics.androidlib/project.properties`, plus `.meta` for `Plugins/Android`, the `.androidlib` folder, and each file inside.

- Manifest — permission only, no `package` attribute, no application node:
  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <manifest xmlns:android="http://schemas.android.com/apk/res/android">
      <uses-permission android:name="android.permission.VIBRATE" />
  </manifest>
  ```
- `project.properties`: `android.library=true` (classic Android-library marker Unity looks for alongside the manifest).
- The `.androidlib` folder `.meta` must import as a **DefaultImporter** folder asset; the `AndroidManifest.xml` `.meta` as a `TextScriptImporter`-style default asset. Do not mark them as `PluginImporter` — Unity treats the whole `.androidlib` directory as the library, not the individual files.
- **This cannot be verified in the harness** — manifest merging is done by Gradle/AGP during a real Android build. Gate here only proves nothing regressed.
- [ ] Implement + gate (426 green, 0 warnings) + commit `fix(haptics): ship android.permission.VIBRATE via an .androidlib manifest`.

### Task 2: Fail loudly — flip `IsSupported` on a permission failure

**Files:** modify `Runtime/AndroidHapticBackend.cs`; add `Tests/Editor/HapticServiceTests.cs` case (live-read guard).

- `_supported` loses `readonly` so it can be demoted at runtime.
- Add one shared failure handler used by all three catches:
  ```csharp
  private void OnCallFailed(Exception exception, string operation)
  {
      // Unity surfaces Java throwables as AndroidJavaException; the Java type is only in the message.
      if (exception is AndroidJavaException && exception.Message.Contains("SecurityException"))
      {
          _supported = false;   // permanent misconfiguration — stop claiming we can vibrate
          Debug.LogError($"[TK.Haptics] {operation} denied: android.permission.VIBRATE is missing from the built manifest. Haptics disabled for this session.");
          return;
      }
      Debug.LogWarning($"[TK.Haptics] {operation} failed: {exception.Message}");
  }
  ```
  Rationale: `SecurityException` cannot be caught as a typed exception through JNI, so the message check is the only available discriminator. `LogError` (not warning) because it is a build misconfiguration, not a transient.
- Replace the three `catch` bodies (`Impact`, `Selection`, `Notification`) with `OnCallFailed(exception, nameof(...))`.
- Harness test (this part IS testable — it guards the seam, not the JNI): `FakeHapticBackend.IsSupported` flipped false after construction must be reflected by `HapticService.IsSupported` and by `Haptics.IsSupported`, proving the service reads the backend live rather than caching it. Without that, a runtime demotion would never reach the game's settings UI.
- [ ] Implement + gate (427 expected — trust results.xml) + commit `fix(haptics): demote IsSupported when a vibrate call is denied`.

### Task 3: Correct the docs that made this invisible

**Files:** modify `Runtime/AndroidHapticBackend.cs` (XML doc), `README.md`, `CHANGELOG.md`, `package.json`.

- XML doc `:7-11`: delete the false **"Device-verified."** claim. Replace with what is true: the package declares `android.permission.VIBRATE` itself via `Plugins/Android/TKHaptics.androidlib`; a denied call demotes `IsSupported` for the session.
- README: add a short **Android permission** note (the package ships the permission; nothing for the consumer to hand-edit) and tighten the `IsSupported` contract — it may go **false at runtime** after a denied call, so a settings screen should re-read it rather than cache it at boot. Add the secondary MIUI note: the system setting `haptic_feedback_enabled = 0` governs `View.performHapticFeedback`, **not** direct `Vibrator.vibrate()`, so it should not block app haptics — but it is the next thing to check if a device is still silent after the permission fix.
- CHANGELOG `## [0.1.1] - Unreleased` under **Fixed**, stating the chain plainly (permission never declared → every vibrate threw `SecurityException` → swallowed into a warning that release builds suppress).
- `package.json` → `0.1.1`.
- Root README/QUICKSTART/ROADMAP pins: **not** touched until the tag.
- [ ] Gate + commit `docs(haptics): correct the Device-verified claim; document the permission + runtime IsSupported demotion`.

### Task 4: Device verification (the actual gate) + release

- [ ] Hand game-shikaku a verification note: repin `com.tk.haptics` to the branch/`file:` override, build to a real **Android 12+** device (exercises the `vibrator_manager` path, SDK ≥ 31), then:
  1. `adb shell dumpsys package <app> | grep -i vibrate` → `android.permission.VIBRATE` must now be listed. **This is the merge proof the plan cannot produce locally.**
  2. Press a button → `Selection` must be felt; sample `Impact` (Light/Medium/Heavy) and `Notification` (Success/Warning/Error).
  3. Toggle Vibration off → silence; on → buzz returns.
  4. If still silent, check MIUI's `haptic_feedback_enabled` per the README note before assuming a package bug.
  - Older-API path (< 31, ideally < 29) if a second device is available.
- [ ] If the manifest does **not** merge: fall back to an `IPostGenerateGradleAndroidProject` post-processor (would introduce the package's first Editor asmdef) and re-verify.
- [ ] On green: date the CHANGELOG, bump pins (root README + QUICKSTART + ROADMAP + package README), release commit, annotated tag `com.tk.haptics/0.1.1`, user pushes.
