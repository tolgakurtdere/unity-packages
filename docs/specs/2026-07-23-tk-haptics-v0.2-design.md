# com.tk.haptics 0.2.0 — Design: vibration usage classification + OS-preference bypass + advisory signal

Approved by Tolgahan 2026-07-23, from the 0.1.1 device verification's two feedback items. Driver: on the MIUI test device (Redmi Note 12 Pro 5G, Android 14) with the system **touch vibration** setting off, every call reached the vibrator service and came back `status: ignored_for_settings` — because the package sends no `VibrationAttributes`, so the platform files *all* its vibrations under `USAGE_TOUCH`, exactly the category that setting gates. Meanwhile the reference game (`com.cortex.shikaku`) vibrated on the same device with the same setting off, differing only by `Usage=TOUCH Flags=2`.

## Locked decisions

1. **Base (public API): classify each call type honestly.** `Selection()` → `USAGE_TOUCH` (it *is* touch feedback — a user who turned that off is respected), `Impact(...)` → `USAGE_MEDIA` (gameplay content feedback), `Notification(...)` → `USAGE_NOTIFICATION` (win/fail is an event). This alone un-gates Impact/Notification from the touch-vibration setting on every conformant device, with no bypass involved.
2. **Opt-in bypass on top (user's product call, made with my objection on record).** `BypassSystemVibrationSetting` (default **false**) additionally sets flag bit `2` on the attributes — the non-public `FLAG_BYPASS_USER_VIBRATION_INTENSITY_OFF`. Rationale: the game ships its own Vibration toggle, so that toggle — not the OS touch-vibration setting — is the player's real consent surface; the competitor demonstrably ships this. Engineering hedge for the non-public flag: it is *layered on* the classification, so if an OEM or Android version strips or ignores the bit, haptics degrade to decision 1 (OS preference respected) instead of vanishing.
3. **Separate advisory signal, NOT folded into `IsSupported`.** `SystemTouchVibrationDisabled` (best-effort, Android-only, false anywhere it cannot be read) reports the OS touch-vibration setting so a settings screen can hint ("enable vibration in system settings") instead of showing a dead toggle. `IsSupported` keeps its meaning — permission + hardware — because no public API reliably detects settings-gating: `Vibrator.areEffectsSupported`/`areAllEffectsSupported`/`hasAmplitudeControl` all report **hardware** capability only (verified against the bundled SDK), and `getCurrentIntensity` is not public.

## Verified platform facts (bundled android.jar API 35 + api-versions.xml)

- `Vibrator.vibrate(VibrationEffect, VibrationAttributes)` is public **since API 33**; the `AudioAttributes` overload is deprecated at 33. → The classification path applies on **SDK ≥ 33 only**; below 33 behavior is byte-for-byte 0.1.1 (plain `vibrate(effect)`, platform defaults to TOUCH). Consumer floor is minSdk 26; the MIUI device is API 34.
- `VibrationAttributes.Builder()` (API 30) with `setUsage(int)` / `setFlags(int flags, int mask)` / `build()`; constants `USAGE_TOUCH=18`, `USAGE_MEDIA=19`, `USAGE_NOTIFICATION=49` (frozen, passed as raw ints over JNI).
- The only public flag is `FLAG_BYPASS_INTERRUPTION_POLICY=1`. **Flag `2` is non-public**; we pass it raw via `setFlags(2, 2)`. **Open risk, only provable on device:** whether `Builder.setFlags` lets a non-public bit through — acceptance criterion is `dumpsys vibrator_manager` showing `Flags=2`. If the platform strips it, the bypass silently degrades to decision 1; that outcome is reported back and documented, not hidden.
- Advisory read: `Settings.System.getInt(contentResolver, "haptic_feedback_enabled", 1) == 0`, guarded, read live per access (the user can change it mid-session).

## API (all platforms; Android is where it has effect)

```csharp
// IHapticBackend — two new members (breaking for custom backends; shipped backends unaffected)
bool SystemTouchVibrationDisabled { get; }        // advisory; false when unknown/unsupported
bool BypassSystemVibrationSetting { get; set; }   // default false

// HapticService + static Haptics façade — mirrors, forwarded to the backend
public bool SystemTouchVibrationDisabled { get; }
public bool BypassSystemVibrationSetting { get; set; }
```

- `NullHapticBackend`: advisory `false`; bypass stored but inert. `IosHapticBackend`: same (iOS has no equivalent setting; `UIFeedbackGenerator` already respects the system's own rules).
- `AndroidHapticBackend`: builds one `VibrationAttributes` per usage lazily, caches them, invalidates the cache when `BypassSystemVibrationSetting` changes; every vibrate goes through one helper that picks `vibrate(effect, attrs)` on SDK ≥ 33 and falls back to `vibrate(effect)` below or if attribute construction ever fails.
- Overload-resolution note: `_vibrator.Call("vibrate", effect, attrs)` must bind to the `VibrationAttributes` overload, not the deprecated `AudioAttributes` one — Unity matches by the actual Java class of the arguments, but this is device-confirmed, not assumed.

## Versioning & compatibility

0.2.0 minor with a seam break (`IHapticBackend` gains two members), same precedent as `com.tk.notification` 0.2.0: consumers of the shipped backends see only additive service/façade API; only custom-backend authors implement the new members.

## Verification

- **Harness (EditMode):** forwarding through service and façade (both directions), defaults false, fake-backend knobs; the Android branch compiles out and is review-verified.
- **Device (MIUI, touch vibration OFF — the acceptance matrix):**
  1. Bypass off: `Impact`/`Notification` now `finished` with `Usage=MEDIA`/`NOTIFICATION`; `Selection` still `ignored_for_settings` (respect proven).
  2. Bypass on: `Selection` also `finished`, dumpsys shows `Flags=2` (or the flag was stripped → report, feature degrades to 1).
  3. `SystemTouchVibrationDisabled` true while the setting is off, false after turning it on.
  4. Regression with touch vibration ON: all types `finished`, editor stays a no-op.
