# com.tk.notification v0.1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This package is executed and reviewed **Opus-only** per the user's request — dispatch every implementer and reviewer on the Opus model.

**Goal:** A reusable, testable local mobile-notification package (`com.tk.notification`): schedule/cancel local notifications, request OS permission, register channels, apply quiet-hours, and route the launching notification — behind an `INotificationBackend` seam so orchestration is unit-tested against a fake and every non-mobile target compiles to a clean no-op.

**Architecture:** Approved committed spec: `docs/specs/2026-07-05-tk-notification-design.md` (READ IT FIRST — behavioral contracts are binding). A pure instance `NotificationService` owns orchestration (quiet-hours application, cancel-all-then-reschedule, permission, launch routing) behind `INotificationBackend`; `UnityMobileNotificationBackend` is the real impl (`#if UNITY_ANDROID / #elif UNITY_IOS / #else` no-op); `FakeNotificationBackend` drives tests; `QuietHoursSettings` is a pure struct ported from the reference. Single dependency: `com.unity.mobile.notifications` (Unity default registry). Local-only — no Firebase, no push.

**Tech Stack:** Unity 6000.3.6f1 host, C#, NUnit EditMode. One dependency — `com.unity.mobile.notifications` (default registry). No Firebase, no `com.tk.core`, no push.

## Global Constraints

- Repo: `/Users/tolgahankurtdere/Documents/GitHub/unity-packages`, branch `main`. Base = the current `main` tip when execution begins (the commit that adds this plan, on top of spec commit `171e0bb`). New package root: `Packages/com.tk.notification/`.
- **NEVER run Unity CLI against the host repo project** (the user's editor may be open). Harness: `/private/tmp/claude-501/-Users-tolgahankurtdere-Documents-GitHub-unity-packages/125643b5-4b33-48e0-b763-cca5d06442d8/scratchpad/tk-verify` (already wired with com.tk.core/iap/ads/remoteconfig/analytics + the AppLovin `unity.packages.applovin.com` and OpenUPM scoped registries — **DO NOT remove them**). If the harness is missing (new session), recreate: `Assets/` + `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 6000.3.6f1`) + `Packages/manifest.json` with `com.unity.test-framework` 1.6.0, the six TK packages as `file:` absolute paths, `testables` listing all six, and the two scoped registries.
- Gate command (run from the harness dir; **NEVER combine `-quit` with `-runTests`**; Bash timeout 600000):
  ```bash
  /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$(pwd)/results.xml" -logFile "$(pwd)/unity.log"
  ```
  Success = exit 0 AND every `TK.Notification.Tests` case `result="Passed"` in results.xml AND zero `error CS` / `warning CS` under `Packages/com.tk.notification` in unity.log. The harness active build target is Standalone (macOS), so `UNITY_ANDROID`/`UNITY_IOS` are undefined → the real backend compiles its `#else` no-op branch. Baseline before Task 1 = current harness total after analytics (**~208 — trust results.xml, not arithmetic**). Report the exact `TK.Notification.Tests` count each task.
- package.json (exact): name `com.tk.notification`, version `0.1.0`, displayName `TK Notification`, description `Local mobile-notification framework: scheduling, quiet-hours, channels, permission and launch routing behind a testable seam — with a Unity Mobile Notifications backend and a no-op fallback on non-mobile targets.`, unity `6000.0`, dependencies EXACTLY `{ "com.unity.mobile.notifications": "<newest stable>" }` — verify the newest stable on Unity's **default registry** at execution and pin it (the reference pins `2.4.2`; if that is still newest, use `2.4.2`; default registry → no scoped registry, git-URL install only), author `{ "name": "Tolga Kurtdere", "url": "https://github.com/tolgakurtdere" }`, keywords `["tk", "notification", "notifications", "mobile", "retention"]`. (Samples array added in Task 4.)
- Asmdefs: `TK.Notification` (rootNamespace `TK.Notification`, `"references": ["Unity.Notifications.Android", "Unity.Notifications.iOS"]`, `"autoReferenced": true`) — both platform asmdefs include `Editor` in their `includePlatforms` and are autoReferenced, so the types resolve in the Editor harness; references to the platform-excluded assembly are silently dropped per build target, and all usage is `#if`-guarded so the `#else` path needs none of them. `TK.Notification.Tests` (`"includePlatforms": ["Editor"]`, references `["TK.Notification", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, `"overrideReferences": true`, `"precompiledReferences": ["nunit.framework.dll"]`, `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, `"autoReferenced": false`).
- **No vendor types (`Unity.Notifications.*`) in any public API** — they appear ONLY inside `UnityMobileNotificationBackend`, guarded by `#if UNITY_ANDROID / #elif UNITY_IOS / #else`.
- Namespaces: `TK.Notification` (runtime), `TK.Notification.Tests` (tests).
- `NotificationService` and the backends are **main-thread-affine** (documented; Unity notification APIs are main-thread).
- Every file/folder under `Packages/com.tk.notification` gets a committed `.meta` (the harness gate generates them). Conventional commits ending with the trailer `Co-Authored-By: Claude <noreply@anthropic.com>` — **NO model name**. Committing to `docs/` is fine; do NOT push mid-plan; do NOT commit `.superpowers/` or unrelated host churn.
- **SDK reference API** (from the reference teardown of `com.unity.mobile.notifications` 2.4.2 — RE-VERIFY every member against the installed version at execution; on drift trust the installed source and note it):
  - Android (`Unity.Notifications.Android`): `AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel { Id, Name, Importance, Description })`; `int AndroidNotificationCenter.SendNotification(new AndroidNotification { Title, Text, FireTime (local DateTime), SmallIcon, LargeIcon, IntentData }, channelId)`; `AndroidNotificationCenter.CancelNotification(int id)`; `AndroidNotificationCenter.CancelAllNotifications()`; `AndroidNotificationCenter.OpenNotificationSettings()`; `AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed`; `new PermissionRequest()` with `.Status` (`PermissionStatus.RequestPending`/`Allowed`); `AndroidNotificationCenter.GetLastNotificationIntent()` → `.Notification.IntentData` / `.Channel`. Enums: `Importance.Low/Default/High`.
  - iOS (`Unity.Notifications.iOS`): `iOSNotificationCenter.ScheduleNotification(new iOSNotification { Identifier (string), Title, Body, ShowInForeground, ThreadIdentifier, CategoryIdentifier, Data, Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval, Repeats = false } })`; `iOSNotificationCenter.RemoveScheduledNotification(string id)`; `iOSNotificationCenter.RemoveAllScheduledNotifications()` (+ `RemoveAllDeliveredNotifications()`); `iOSNotificationCenter.OpenNotificationSettings()`; `iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus == AuthorizationStatus.Authorized`; `new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge, true)` polled via `.IsFinished` then `.Granted`; `iOSNotificationCenter.GetLastRespondedNotification()` → `.Data` / `.Request.Content...`.

---

### Task 1: Skeleton + value types + QuietHoursSettings + backend seam + fake + harness wiring

**Files:**
- Create: `Packages/com.tk.notification/package.json`, `Runtime/TK.Notification.asmdef`
- Create: `Runtime/NotificationImportance.cs`, `Runtime/NotificationChannel.cs`, `Runtime/NotificationRequest.cs`, `Runtime/NotificationResponse.cs`, `Runtime/QuietHoursSettings.cs`
- Create: `Runtime/Seams/INotificationBackend.cs`
- Create: `Tests/Editor/TK.Notification.Tests.asmdef`, `Tests/Editor/FakeNotificationBackend.cs`, `Tests/Editor/QuietHoursSettingsTests.cs`, `Tests/Editor/FakeNotificationBackendTests.cs`
- Modify: harness `Packages/manifest.json`

**Interfaces produced (every later task compiles against these):** the four value types, `QuietHoursSettings`, `INotificationBackend`, and `FakeNotificationBackend`, exactly as below.

- [ ] **Step 1: package.json + both asmdefs** — exact values from Global Constraints (pin the verified newest `com.unity.mobile.notifications`; TK.Notification references the two platform asmdefs).

- [ ] **Step 2: NotificationImportance.cs + NotificationChannel.cs** (full code):

```csharp
namespace TK.Notification
{
    /// <summary>Channel importance. Maps to Android Importance; iOS ignores it.</summary>
    public enum NotificationImportance { Low, Default, High }

    /// <summary>A notification channel (Android). On iOS the id is used only for thread/category grouping.</summary>
    public readonly struct NotificationChannel
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public NotificationImportance Importance { get; }

        public NotificationChannel(string id, string name, string description = null,
            NotificationImportance importance = NotificationImportance.Default)
        {
            Id = id;
            Name = name;
            Description = description;
            Importance = importance;
        }
    }
}
```

- [ ] **Step 3: NotificationRequest.cs + NotificationResponse.cs** (full code):

```csharp
using System;

namespace TK.Notification
{
    /// <summary>A local notification the game asks to schedule. DeliveryTime is absolute (device-local wall clock);
    /// the service applies quiet-hours before scheduling. Data is an opaque payload surfaced on launch.</summary>
    public readonly struct NotificationRequest
    {
        public string ChannelId { get; }
        public string Title { get; }
        public string Body { get; }
        public DateTime DeliveryTime { get; }
        public string Data { get; }
        public string SmallIcon { get; }
        public string LargeIcon { get; }

        public NotificationRequest(string channelId, string title, string body, DateTime deliveryTime,
            string data = null, string smallIcon = null, string largeIcon = null)
        {
            ChannelId = channelId;
            Title = title;
            Body = body;
            DeliveryTime = deliveryTime;
            Data = data;
            SmallIcon = smallIcon;
            LargeIcon = largeIcon;
        }
    }

    /// <summary>The notification that launched/resumed the app (from a tap).</summary>
    public readonly struct NotificationResponse
    {
        public string ChannelId { get; }
        public string Data { get; }

        public NotificationResponse(string channelId, string data)
        {
            ChannelId = channelId;
            Data = data;
        }
    }
}
```

- [ ] **Step 4: QuietHoursSettings.cs** (full code — ported logic):

```csharp
using System;

namespace TK.Notification
{
    /// <summary>
    /// Optional quiet-hours window. Enabled + [StartHour, EndHour) in device-local wall-clock time.
    /// Apply() shifts a fire time that lands inside the window forward to EndHour:00. Supports wrapping
    /// windows (e.g. 23→7). default(QuietHoursSettings) is disabled (a no-op).
    /// </summary>
    public readonly struct QuietHoursSettings
    {
        public bool Enabled { get; }
        public int StartHour { get; }   // inclusive, 0-23
        public int EndHour { get; }     // exclusive, 0-23

        public QuietHoursSettings(bool enabled, int startHour, int endHour)
        {
            Enabled = enabled;
            StartHour = startHour;
            EndHour = endHour;
        }

        public DateTime Apply(DateTime fireTime)
        {
            if (!Enabled || StartHour == EndHour) return fireTime;

            var hour = fireTime.Hour;
            var wrapping = StartHour > EndHour;
            var inQuiet = wrapping
                ? (hour >= StartHour || hour < EndHour)
                : (hour >= StartHour && hour < EndHour);

            if (!inQuiet) return fireTime;

            var endToday = new DateTime(fireTime.Year, fireTime.Month, fireTime.Day,
                EndHour, 0, 0, fireTime.Kind);

            // Late part of a wrapping window (e.g. 23:30 in 23→7) ends the NEXT day; everything else ends today.
            return wrapping && hour >= StartHour ? endToday.AddDays(1) : endToday;
        }
    }
}
```

- [ ] **Step 5: Seams/INotificationBackend.cs** (full code):

```csharp
using System.Threading.Tasks;

namespace TK.Notification
{
    /// <summary>
    /// Native-platform notification seam. The real impl wraps com.unity.mobile.notifications; a fake drives
    /// tests; on non-mobile targets the real impl reports IsAvailable=false and no-ops. Implementations MUST
    /// NOT throw — the service wraps every call, logs, and continues.
    /// </summary>
    public interface INotificationBackend
    {
        /// <summary>True on a mobile device with the platform API available; false in Editor/desktop.</summary>
        bool IsAvailable { get; }
        void RegisterChannel(NotificationChannel channel);
        /// <summary>Schedule one notification (DeliveryTime already quiet-hours-adjusted). Returns the assigned id.</summary>
        int Schedule(NotificationRequest request);
        void Cancel(int id);
        void CancelAll();
        Task<bool> RequestPermissionAsync();
        bool IsPermissionGranted();
        bool TryGetLaunchNotification(out NotificationResponse response);
        void OpenSettings();
    }
}
```

- [ ] **Step 6: Tests/Editor/FakeNotificationBackend.cs** (full code, namespace `TK.Notification.Tests`):

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TK.Notification;

namespace TK.Notification.Tests
{
    /// <summary>Recording backend for deterministic EditMode tests.</summary>
    public sealed class FakeNotificationBackend : INotificationBackend
    {
        // Knobs
        public bool IsAvailable { get; set; } = true;
        public bool PermissionResult = true;
        public bool ThrowOnSchedule;
        public NotificationResponse? Launch;   // set to inject a launching notification

        // Recorded
        public readonly List<NotificationChannel> Channels = new();
        public readonly List<NotificationRequest> Scheduled = new();
        public readonly List<int> Cancelled = new();
        public int CancelAllCount;
        public int PermissionRequests;
        public int OpenSettingsCount;
        private int _nextId = 1;

        public void RegisterChannel(NotificationChannel channel) => Channels.Add(channel);

        public int Schedule(NotificationRequest request)
        {
            if (ThrowOnSchedule) throw new InvalidOperationException("fake: schedule threw");
            Scheduled.Add(request);
            return _nextId++;
        }

        public void Cancel(int id) => Cancelled.Add(id);
        public void CancelAll() => CancelAllCount++;

        public Task<bool> RequestPermissionAsync()
        {
            PermissionRequests++;
            return Task.FromResult(PermissionResult);
        }

        public bool IsPermissionGranted() => PermissionResult;

        public bool TryGetLaunchNotification(out NotificationResponse response)
        {
            if (Launch.HasValue) { response = Launch.Value; return true; }
            response = default;
            return false;
        }

        public void OpenSettings() => OpenSettingsCount++;
    }
}
```

- [ ] **Step 7: QuietHoursSettingsTests.cs** (complete NUnit code, namespace `TK.Notification.Tests`, 12 tests). Use a helper `D(int hour, int min = 0)` = `new DateTime(2026, 7, 5, hour, min, 0, DateTimeKind.Local)`:
  1. `Disabled_ReturnsUnchanged` — `new QuietHoursSettings(false, 23, 7).Apply(D(2))` == `D(2)`.
  2. `Default_IsDisabled_NoOp` — `default(QuietHoursSettings).Apply(D(2))` == `D(2)`.
  3. `ZeroWidthWindow_NoOp` — `new QuietHoursSettings(true, 5, 5).Apply(D(5))` == `D(5)`.
  4. `OutsideWindow_NonWrapping_Unchanged` — window (1,6), `Apply(D(8))` == `D(8)`.
  5. `InsideWindow_NonWrapping_ShiftsToEnd` — window (1,6), `Apply(D(2,30))` == `D(6)` (06:00:00).
  6. `OutsideWindow_Wrapping_Unchanged` — window (23,7), `Apply(D(12))` == `D(12)`.
  7. `InsideWindow_Wrapping_EarlyMorning_ShiftsToSameDayEnd` — window (23,7), `Apply(D(2))` == `D(7)` (same day 07:00).
  8. `InsideWindow_Wrapping_LateNight_ShiftsToNextDayEnd` — window (23,7), `Apply(D(23,30))` == `D(7).AddDays(1)` (next day 07:00).
  9. `AtStartHour_Inclusive_Shifts` — window (23,7), `Apply(D(23))` == `D(7).AddDays(1)`.
  10. `AtEndHour_Exclusive_Unchanged` — window (23,7), `Apply(D(7))` == `D(7)`.
  11. `ShiftDropsMinutesToTopOfEndHour` — window (1,6), `Apply(D(2,37))`.Minute == 0 and .Hour == 6.
  12. `PreservesKind` — window (23,7), Utc input `new DateTime(2026,7,5,2,0,0,DateTimeKind.Utc)` → result `.Kind == DateTimeKind.Utc`.

- [ ] **Step 8: FakeNotificationBackendTests.cs** (complete NUnit code, 4 tests):
  1. `Schedule_RecordsAndReturnsIncrementingId` — two `Schedule` calls → ids 1,2; `Scheduled.Count==2`.
  2. `Schedule_ThrowKnob_Throws` — `ThrowOnSchedule=true` → `Assert.Throws<InvalidOperationException>`.
  3. `CancelAndCancelAll_Recorded` — `Cancel(5)` → `Cancelled` contains 5; `CancelAll()` → `CancelAllCount==1`.
  4. `Launch_And_Permission_Knobs` — `Launch = new NotificationResponse("c","d")` → `TryGetLaunchNotification` true + response; `PermissionResult=false` → `IsPermissionGranted()` false and `await RequestPermissionAsync()` false (`PermissionRequests==1`).

- [ ] **Step 9: harness wiring** — in the harness `Packages/manifest.json`: add `"com.tk.notification": "file:/Users/tolgahankurtdere/Documents/GitHub/unity-packages/Packages/com.tk.notification"` to `dependencies` and `"com.tk.notification"` to `testables`. The embedded package's own dependency (`com.unity.mobile.notifications`) resolves from the default registry automatically — confirm it appears in the harness `Packages/packages-lock.json` after the gate. Leave the six existing packages and the two scoped registries untouched.

- [ ] **Step 10: gate** (baseline + 16). **Step 11: commit** — `feat(notification): add com.tk.notification skeleton with value types, quiet-hours, backend seam and fake`.

---

### Task 2: NotificationService — orchestration (quiet-hours, cancel-then-reschedule, permission, launch)

**Files:**
- Create: `Packages/com.tk.notification/Runtime/NotificationService.cs`
- Create: `Tests/Editor/NotificationServiceTests.cs`

**Interfaces:**
- Consumes: all value types + `QuietHoursSettings` + `INotificationBackend` (Task 1), `FakeNotificationBackend` (Task 1, tests).
- Produces (Tasks 3–4 consume): `NotificationService(INotificationBackend backend, QuietHoursSettings quietHours = default)` with the members below.

- [ ] **Step 1: NotificationService.cs** (full code — transcribe EXACTLY):

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Notification
{
    /// <summary>
    /// Local notification orchestration behind INotificationBackend. Applies quiet-hours before scheduling,
    /// supports cancel-all-then-reschedule, exposes async permission + launch routing. Every backend call is
    /// guarded (must-not-throw contract): a failure is logged and swallowed. On non-mobile targets the backend
    /// reports IsAvailable=false and all operations are safe no-ops. Main-thread usage assumed.
    /// </summary>
    public sealed class NotificationService
    {
        private readonly INotificationBackend _backend;

        public NotificationService(INotificationBackend backend, QuietHoursSettings quietHours = default)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            QuietHours = quietHours;
        }

        public QuietHoursSettings QuietHours { get; set; }

        public bool IsSupported => _backend.IsAvailable;

        public void RegisterChannel(NotificationChannel channel)
        {
            if (!_backend.IsAvailable) return;
            try { _backend.RegisterChannel(channel); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public int? Schedule(NotificationRequest request)
        {
            if (!_backend.IsAvailable) return null;
            try { return _backend.Schedule(ApplyQuietHours(request)); }
            catch (Exception exception) { Debug.LogException(exception); return null; }
        }

        public void ScheduleAll(IReadOnlyList<NotificationRequest> requests)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (!_backend.IsAvailable) return;
            try
            {
                _backend.CancelAll();
                for (var i = 0; i < requests.Count; i++)
                    _backend.Schedule(ApplyQuietHours(requests[i]));
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public void Cancel(int id)
        {
            if (!_backend.IsAvailable) return;
            try { _backend.Cancel(id); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public void CancelAll()
        {
            if (!_backend.IsAvailable) return;
            try { _backend.CancelAll(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public async Task<bool> RequestPermissionAsync()
        {
            if (!_backend.IsAvailable) return false;
            try { return await _backend.RequestPermissionAsync(); }
            catch (Exception exception) { Debug.LogException(exception); return false; }
        }

        public bool IsPermissionGranted()
        {
            if (!_backend.IsAvailable) return false;
            try { return _backend.IsPermissionGranted(); }
            catch (Exception exception) { Debug.LogException(exception); return false; }
        }

        public bool TryGetLaunchNotification(out NotificationResponse response)
        {
            response = default;
            if (!_backend.IsAvailable) return false;
            try { return _backend.TryGetLaunchNotification(out response); }
            catch (Exception exception) { Debug.LogException(exception); response = default; return false; }
        }

        public void OpenNotificationSettings()
        {
            if (!_backend.IsAvailable) return;
            try { _backend.OpenSettings(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private NotificationRequest ApplyQuietHours(NotificationRequest request)
        {
            var when = QuietHours.Apply(request.DeliveryTime);
            if (when == request.DeliveryTime) return request;
            return new NotificationRequest(request.ChannelId, request.Title, request.Body, when,
                request.Data, request.SmallIcon, request.LargeIcon);
        }
    }
}
```

- [ ] **Step 2: NotificationServiceTests.cs** (complete NUnit code; inject `FakeNotificationBackend`; `async Task` for the permission test; `using UnityEngine.TestTools;` + `using System.Text.RegularExpressions;` for `LogAssert`; helper `D(int h)` = `new DateTime(2026,7,5,h,0,0,DateTimeKind.Local)`). Tests (16):
  1. `Ctor_NullBackend_Throws` — `Assert.Throws<ArgumentNullException>(() => new NotificationService(null))`.
  2. `Schedule_AppliesQuietHours_BackendGetsShiftedTime` — service with `QuietHours = new QuietHoursSettings(true,23,7)`; `Schedule(req at D(2))`; `backend.Scheduled[0].DeliveryTime == D(7)`.
  3. `Schedule_NoQuietHours_PassesThrough` — default quiet; `Schedule(req at D(2))`; `backend.Scheduled[0].DeliveryTime == D(2)`.
  4. `Schedule_ReturnsBackendId` — first `Schedule` returns `1` (fake's first id).
  5. `Schedule_NotSupported_ReturnsNull_NoBackendCall` — `backend.IsAvailable=false`; `Schedule(req)` returns null; `backend.Scheduled.Count==0`.
  6. `ScheduleAll_CancelsAllThenSchedulesInOrder` — three requests; `ScheduleAll`; `backend.CancelAllCount==1`, `backend.Scheduled.Count==3` and titles in the given order.
  7. `ScheduleAll_AppliesQuietHoursToEach` — quiet (23,7); requests at D(2) and D(23,30)→ scheduled times D(7) and D(7).AddDays(1).
  8. `ScheduleAll_NotSupported_NoOp` — `IsAvailable=false`; `ScheduleAll(list)`; `CancelAllCount==0`, `Scheduled.Count==0`.
  9. `ScheduleAll_NullRequests_Throws` — `Assert.Throws<ArgumentNullException>(() => svc.ScheduleAll(null))`.
  10. `RegisterChannel_ReachesBackend_AndNoOpWhenUnsupported` — supported → `Channels.Count==1`; unsupported → 0.
  11. `RequestPermissionAsync_ReturnsBackendResult` — `PermissionResult=true` → true, `PermissionRequests==1`; `IsAvailable=false` → false without a request.
  12. `IsPermissionGranted_ReflectsBackend` — mirrors `PermissionResult`; false when unsupported.
  13. `TryGetLaunchNotification_SurfacesInjected` — `backend.Launch = new NotificationResponse("main","d0")` → true + response fields; no launch → false; unsupported → false.
  14. `Cancel_And_CancelAll_ReachBackend` — `Cancel(9)` → `Cancelled` contains 9; `CancelAll()` → `CancelAllCount` increments.
  15. `Schedule_BackendThrows_Swallowed_ReturnsNull` — `backend.ThrowOnSchedule=true`; `LogAssert.Expect(LogType.Exception, new Regex("fake: schedule threw"))`; `Schedule(req)` returns null; no exception escapes.
  16. `IsSupported_And_OpenSettings` — `IsSupported` mirrors `IsAvailable`; `OpenNotificationSettings()` → `OpenSettingsCount==1` when supported, 0 when not.

- [ ] **Step 3: gate** (prev + 16). **Step 4: commit** — `feat(notification): add NotificationService orchestration with quiet-hours, reschedule, permission and launch routing`.

---

### Task 3: UnityMobileNotificationBackend (real platform backend)

**Files:**
- Create: `Packages/com.tk.notification/Runtime/Backends/UnityMobileNotificationBackend.cs`

**Interfaces:**
- Consumes: `INotificationBackend` + all value types (Task 1).
- Produces: `UnityMobileNotificationBackend : INotificationBackend` (used by the demo + as the default real backend).

This is the only file that touches `Unity.Notifications.*`. It has NO new unit tests (native APIs aren't exercised in the Editor harness — the `#else` branch compiles and the gate stays green); it is **review-verified** against the installed SDK. RE-VERIFY every member against `com.unity.mobile.notifications` at execution (see Global Constraints SDK reference; on drift trust the installed source and note it).

- [ ] **Step 1: UnityMobileNotificationBackend.cs** (full code — transcribe, then RE-VERIFY the `#if`/`#elif` branches against the installed SDK):

```csharp
using System;
using System.Threading.Tasks;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace TK.Notification
{
    /// <summary>
    /// Real INotificationBackend over com.unity.mobile.notifications. Android and iOS branches call the native
    /// centers; on every other target (Editor/desktop) IsAvailable is false and all methods no-op, so the
    /// service is safe on all platforms. Must not throw (the service also guards).
    /// </summary>
    public sealed class UnityMobileNotificationBackend : INotificationBackend
    {
#if UNITY_ANDROID || UNITY_IOS
        public bool IsAvailable => true;
#else
        public bool IsAvailable => false;
#endif

        public void RegisterChannel(NotificationChannel channel)
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = channel.Id,
                Name = string.IsNullOrEmpty(channel.Name) ? channel.Id : channel.Name,
                Description = channel.Description ?? channel.Id,
                Importance = ToAndroidImportance(channel.Importance)
            });
#endif
            // iOS/other: channels are not pre-registered.
        }

        public int Schedule(NotificationRequest request)
        {
#if UNITY_ANDROID
            var n = new AndroidNotification
            {
                Title = request.Title,
                Text = request.Body,
                FireTime = request.DeliveryTime,      // local wall-clock
                IntentData = request.Data
            };
            if (!string.IsNullOrEmpty(request.SmallIcon)) n.SmallIcon = request.SmallIcon;
            if (!string.IsNullOrEmpty(request.LargeIcon)) n.LargeIcon = request.LargeIcon;
            return AndroidNotificationCenter.SendNotification(n, request.ChannelId);
#elif UNITY_IOS
            var id = Math.Abs(Guid.NewGuid().GetHashCode());
            var interval = request.DeliveryTime.ToUniversalTime() - DateTime.UtcNow;
            if (interval < TimeSpan.FromSeconds(1)) interval = TimeSpan.FromSeconds(1); // trigger must be in the future
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = id.ToString(),
                Title = request.Title,
                Body = request.Body,
                ShowInForeground = true,
                ThreadIdentifier = request.ChannelId,
                CategoryIdentifier = request.ChannelId,
                Data = request.Data ?? string.Empty,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = interval, Repeats = false }
            });
            return id;
#else
            return 0;
#endif
        }

        public void Cancel(int id)
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(id.ToString());
#endif
        }

        public void CancelAll()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif
        }

        public async Task<bool> RequestPermissionAsync()
        {
#if UNITY_ANDROID
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending) await Task.Yield();
            return request.Status == PermissionStatus.Allowed;
#elif UNITY_IOS
            using var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge, true);
            while (!request.IsFinished) await Task.Yield();
            return request.Granted;
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public bool IsPermissionGranted()
        {
#if UNITY_ANDROID
            return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;
#elif UNITY_IOS
            return iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus == AuthorizationStatus.Authorized;
#else
            return false;
#endif
        }

        public bool TryGetLaunchNotification(out NotificationResponse response)
        {
#if UNITY_ANDROID
            var intent = AndroidNotificationCenter.GetLastNotificationIntent();
            if (intent != null)
            {
                response = new NotificationResponse(intent.Channel, intent.Notification.IntentData);
                return true;
            }
#elif UNITY_IOS
            var last = iOSNotificationCenter.GetLastRespondedNotification();
            if (last != null)
            {
                response = new NotificationResponse(last.CategoryIdentifier, last.Data);
                return true;
            }
#endif
            response = default;
            return false;
        }

        public void OpenSettings()
        {
#if UNITY_ANDROID
            AndroidNotificationCenter.OpenNotificationSettings();
#elif UNITY_IOS
            iOSNotificationCenter.OpenNotificationSettings();
#endif
        }

#if UNITY_ANDROID
        private static Importance ToAndroidImportance(NotificationImportance importance) => importance switch
        {
            NotificationImportance.Low => Importance.Low,
            NotificationImportance.High => Importance.High,
            _ => Importance.Default
        };
#endif
    }
}
```

- [ ] **Step 2: gate** — the harness (Standalone target) compiles the `#else` branches; confirm zero `error CS`/`warning CS` under `Packages/com.tk.notification` and TK.Notification.Tests count unchanged (32). RE-VERIFY notes: confirm `AndroidNotification.IntentData`, `AndroidNotificationCenter.GetLastNotificationIntent().Channel/.Notification.IntentData`, `iOSNotification.Data`, `GetLastRespondedNotification().CategoryIdentifier/.Data`, and the enum names against the installed SDK; adjust + note any drift. **Step 3: commit** — `feat(notification): add UnityMobileNotificationBackend (Android/iOS real backend, no-op elsewhere)`.

---

### Task 4: Samples + docs + root README/ROADMAP + README pin sweep + host wiring + final gate

**Files:**
- Create: `Packages/com.tk.notification/Samples~/NotificationDemo/NotificationDemo.cs` + `README.md`
- Create: `Packages/com.tk.notification/Samples~/IntegrationExamples/EngagementReminders.cs`, `RemoteConfigQuietHoursBridge.cs`, `LaunchRouter.cs`, `README.md`
- Create: `Packages/com.tk.notification/README.md`, `CHANGELOG.md`
- Modify: `Packages/com.tk.notification/package.json` (samples array); root `README.md`; `ROADMAP.md`; the per-package README install pins (sweep, see Step 5); HOST `Packages/manifest.json` (testables)

- [ ] **Step 1: NotificationDemo.cs** (Sample; editor-runnable, no SDK needed at edit time). A `MonoBehaviour` building `_service = new NotificationService(new UnityMobileNotificationBackend(), new QuietHoursSettings(true, 23, 7))` in `Awake`. `[ContextMenu]` methods: `Register Channel` (`_service.RegisterChannel(new NotificationChannel("main","General", "General reminders", NotificationImportance.High))`), `Request Permission` (`async void` → `await _service.RequestPermissionAsync()`, log result), `Schedule Engagement Funnel` (build `var now = DateTime.Now; var list = new List<NotificationRequest>{ new("main","Come back!","We miss you", now.AddDays(1), "d1"), ... 3,7,14,30 days }; _service.ScheduleAll(list);`), `Cancel All` (`_service.CancelAll()`), `Check Launch` (`if (_service.TryGetLaunchNotification(out var r)) Debug.Log(...)`). Class doc: in the Editor the backend is a no-op (`IsSupported==false`); build to Android/iOS to see real notifications. Demonstrates the cancel-all-then-reschedule engagement pattern. + short README.

- [ ] **Step 2: IntegrationExamples** —
  - `EngagementReminders.cs`: a game-owned helper `public static class EngagementReminders { public static IReadOnlyList<NotificationRequest> Build(DateTime now, string channelId) { ... } }` returning the 1/3/7/14/30-day funnel with placeholder title/body (comment: replace copy with your localized strings — this is the cleaned-up, package-friendly form of the reference's `MyNotificationManager` funnel; content/timing/localization are the game's). Caller: `service.ScheduleAll(EngagementReminders.Build(DateTime.Now, "main"))`.
  - `RemoteConfigQuietHoursBridge.cs`: `using TK.RemoteConfig;` — `public static class RemoteConfigQuietHoursBridge { public static QuietHoursSettings From(RemoteConfigService rc) => new(rc.Bool("quiet_hours_enabled", true), rc.Int("quiet_hours_start", 23), rc.Int("quiet_hours_end", 7)); }` (references `TK.RemoteConfig`; compiles only when com.tk.remoteconfig is present; comment: assign to `service.QuietHours`). No hard dependency.
  - `LaunchRouter.cs`: a small startup helper that calls `service.TryGetLaunchNotification(out var response)` and `switch`es on `response.Data` to route (stub cases + comment: forward to your analytics / deep-link — replaces the reference's hardcoded analytics call).
  - Sample `README.md`: one-liners for each (engagement funnel, RC quiet-hours bridge, launch routing).

- [ ] **Step 3: package README.md** — sections: **What's inside** (table: `NotificationService`, `INotificationBackend` + `UnityMobileNotificationBackend` + fake, `QuietHoursSettings`, typed `NotificationChannel`/`Request`/`Response`); **Install** (git URL `https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.notification` + tag pin `#com.tk.notification/0.1.0`; NOTE: single dependency `com.unity.mobile.notifications` on Unity's **default registry** → NO scoped registry; add `com.tk.notification` to your project `testables` only to run its tests); **Quickstart** (`var service = new NotificationService(new UnityMobileNotificationBackend()); service.RegisterChannel(new NotificationChannel("main","General")); await service.RequestPermissionAsync(); service.ScheduleAll(mylist);`); **Quiet hours** (`service.QuietHours = new QuietHoursSettings(true, 23, 7)`; window semantics; wrapping); **Scheduling model** (absolute DeliveryTime; relative = `DateTime.Now.AddDays(n)`; cancel-all-then-reschedule each launch; ids are backend-assigned); **Permission** (async; OS is the source of truth via `IsPermissionGranted`; app-level opt-in is yours to persist); **Launch routing** (`TryGetLaunchNotification` → route `Data`); **Non-mobile targets** (`IsSupported` false in Editor/desktop; all ops no-op — safe to call unconditionally); **Feeding config from Remote Config** (the sample bridge one-liner; package has no dependency on com.tk.remoteconfig); **Gotchas** (main-thread; local-only, no push in v1; DeliveryTime is device-local wall-clock; re-declare your reminder set each launch). `CHANGELOG.md`: keep-a-changelog `## [0.1.0] - 2026-07-05`.

- [ ] **Step 4: package.json samples array** — `[{ "displayName": "Notification Demo", "description": "Editor-runnable demo with ContextMenu triggers and the engagement-funnel reschedule pattern.", "path": "Samples~/NotificationDemo" }, { "displayName": "Integration Examples", "description": "Engagement-funnel builder, Remote Config quiet-hours bridge, and a launch router.", "path": "Samples~/IntegrationExamples" }]`.

- [ ] **Step 5: root README + ROADMAP + README pin sweep + host wiring** —
  - Root `README.md`: add a `com.tk.notification` row (0.1.0, Unity 6000.0+, no scoped registries; dep `com.unity.mobile.notifications`) to the package table AND the Shipped table, its install URL, and `com.tk.notification/0.1.0` in the Versioning tag list. Match existing row/format.
  - `ROADMAP.md`: add `com.tk.notification` to the **Shipped** table (it was not previously a candidate — user added it); if a "recommended next" pointer exists, leave it at `com.tk.audio`.
  - **README install-pin sweep** (a prior background session for this was deleted — verify and fix here): for EACH package, ensure the README Install git-URL tag pin matches the package's own `package.json` version — `com.tk.iap` → `0.1.1`, `com.tk.ads` → `0.1.2`, `com.tk.core` → `0.1.0`, `com.tk.toolbar` → `0.1.0`, `com.tk.remoteconfig` → `0.1.0`, `com.tk.analytics` → `0.1.0`, `com.tk.notification` → `0.1.0`. Fix any stale pin (known: iap README pins `0.1.0` but package.json is `0.1.1`). Also align any version/tag list in the root README/ROADMAP.
  - HOST `Packages/manifest.json` (the repo's own project manifest): add `"com.tk.notification"` to `testables` (currently `["com.tk.core","com.tk.iap","com.tk.ads","com.tk.remoteconfig","com.tk.analytics"]`). Do NOT touch its scopedRegistries/dependencies.

- [ ] **Step 6: final gate** (all `TK.Notification.Tests` green — 32; zero `com.tk.notification` warnings; the samples under `Samples~` are not compiled). Verify Samples~ tracked: `git check-ignore -v Packages/com.tk.notification/Samples~/IntegrationExamples/EngagementReminders.cs` must report **NOT ignored**. `git status` clean apart from known host churn — stage `Packages/packages-lock.json` if it records the new embedded package + its `com.unity.mobile.notifications` resolution. **Step 7: commit** — `docs(notification): add samples, package docs, README pin sweep and host wiring`. (Push + the `com.tk.notification/0.1.0` tag happen AFTER the final whole-branch review, in the finishing step.)

---

## Notes for the executor

- **Opus-only** for every implementer and reviewer subagent.
- The spec (`docs/specs/2026-07-05-tk-notification-design.md`) is the binding contract; if code and spec disagree, stop and reconcile.
- Between tasks: two-stage review (per-task + whole-branch at the end). The whole-branch review must confirm: single dependency (`com.unity.mobile.notifications`, default registry) and no others; no `Unity.Notifications.*` types in any public API (only inside `UnityMobileNotificationBackend`, `#if`-guarded); quiet-hours applied service-side (shifted time reaches the backend); `ScheduleAll` = cancel-all-then-reschedule in order; non-mobile no-op path (`IsSupported` false → every op safe); async permission with no coroutine/GameObject; backend errors swallowed; the real backend's Android/iOS API verified against the installed SDK.
- Task 3 adds no unit tests (native, review-verified); its gate is "compiles clean via `#else`, existing tests still green." Do NOT invent play-mode native tests.
- Do NOT push or tag mid-plan. Report the exact `TK.Notification.Tests` count after every gate.
