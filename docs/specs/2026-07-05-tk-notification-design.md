# com.tk.notification v0.1.0 — Design Spec

Status: APPROVED by Tolgahan 2026-07-05 (direction + all 4 decisions + the 3 judgment calls). Committed to `docs/specs/` (referenceable), following the standard set with the other TK packages. Reviewed/executed Opus-only (retention-critical).

## Purpose

A reusable, testable **local mobile-notification** package for TK games: schedule/cancel local notifications, request OS permission, register channels, apply quiet-hours, and route the notification that launched the app — all behind a seam so the orchestration is unit-testable and every non-mobile target compiles to a clean no-op. It extracts the genuinely reusable *mechanism* from the reference project's comprehensive-but-not-UPM-grade notification system, and deliberately fixes that system's architectural blockers (native calls inline → untestable; no `#else` → broken non-mobile targets; coroutine-with-hidden-GameObject permission flow; hard Firebase dependency). The game supplies *composition*: channel definitions, the reminder catalog (content + timing + localization), and launch-routing decisions. Same engineering bar as the other TK packages (seam-based, deterministic EditMode tests, per-task + whole-branch review).

## Reference system (analyzed, read-only)

`g-brain_test_5`, deep read-only teardown performed 2026-07-05 (~1490 LOC). Mechanism files: `Assets/UnicoStudio/UnicoLibs/Notifications/NotificationManager.cs` (552L, static; platform-agnostic scheduling/cancel/permission/quiet-hours/launch-detail with native Android/iOS calls inline via `#if`), `QuietHoursSettings.cs` (54L, pure readonly struct + `Apply()` forward-shift, 16 EditMode tests — excellent, reused), `MyNotificationManager.cs` (182L, game wrapper: 1/2/3/4/5/7/10/15/20/30-day engagement funnel, PlayerPrefs status toggle, `OnApplicationPause` resync, `LocalizedString` content), `NotificationConfig(s).cs` (Firebase Remote Config binding for quiet-hours/permission-levels/reward), `FirebaseMessagingManager.cs` (33L, minimal FCM — subscribes `"all_users"` topic, fire-and-forget; no token/on-message/deep-link). Local backend: `com.unity.mobile.notifications` 2.4.2 (`Unity.Notifications.Android`/`Unity.Notifications.iOS`, Unity default registry). Standard pattern used: cancel-all-then-reschedule on init/permission-toggle/resume; notification ids = random positive int (not persisted); launch handled via `TryGetNotificationLaunchDetails` → game routes.

UPM-hostility blockers this package fixes:
1. Native calls (`AndroidNotificationCenter`/`iOSNotificationCenter`) inline in the core → orchestration untestable. **Fix: `INotificationBackend` seam.**
2. `#if UNITY_ANDROID / #elif UNITY_IOS` with **no `#else`** → Editor/desktop warnings/breakage. **Fix: `#else` no-op backend.**
3. Permission flow via a coroutine that spawns a hidden `CoroutineManager` GameObject → test-hostile. **Fix: `Task<bool> RequestPermissionAsync()`.**
4. Hard `Firebase.Messaging` dependency baked in → won't compile without FCM. **Fix: push is out of v1 scope; no Firebase dependency.**
5. Static singleton + `PlayerPrefs` + `FRCController` coupling (all in the game wrapper) → excluded; the game owns opt-in persistence and config.
6. Raw string channel ids; non-persistent random ids. **Fix: typed `NotificationChannel`; backend-assigned ids.**

## Locked decisions

1. **Local-only (v1).** No FCM / remote push. The reference's push is minimal and Firebase-coupling is the #1 UPM blocker; keeping the package local-only makes it Firebase-free and focused on the retention workhorse. Push is a v2 reserve (`IPushBackend` + Firebase sample), not scaffolded now.
2. **Standalone; opt-in is game-owned.** OS permission (`IsPermissionGranted`, from the backend) is the single source of truth. Any app-level "user wants notifications" preference is the game's to persist (its own `ISaveSystem`/PlayerPrefs) — the package holds no persistence, so it has **no `com.tk.core` dependency**.
3. **Pure instance `NotificationService` + `INotificationBackend` seam** (like RC/iap/ads — not the analytics static-façade shape). Notifications are scheduled from a few deliberate call sites (bootstrap, settings toggle, domain events), so an ambient static accessor isn't warranted; a plain injectable service is simplest and maximally testable.
4. **Single dependency:** `com.unity.mobile.notifications` (newest stable — reference pins 2.4.2; verify the newest at execution and pin it; Unity **default registry**, so no scoped registry, git-URL install only).
5. **Three judgment calls (approved):**
   - **`NotificationRequest.DeliveryTime` is absolute** (`DateTime`). Relative reminders are the game's one-liner (`DateTime.Now.AddDays(n)`); the service stays clock-free and fully testable (quiet-hours is a pure function of the given time). A relative-offset helper is a v2 reserve.
   - **Single runtime asmdef** `TK.Notification` referencing the mobile-notifications asmdef; the real backend is the only file touching native types, guarded `#if UNITY_ANDROID / #elif UNITY_IOS / #else`(no-op). Mirrors how `TK.Ads` references `MaxSdk.Scripts`.
   - **`int` notification ids**, backend-assigned and returned (iOS identifier = `id.ToString()`), matching the reference and working cross-platform.
6. **No vendor type leak:** `Unity.Notifications.*` types never appear in the package's public API — only inside `UnityMobileNotificationBackend`.

## Package layout

```
Packages/com.tk.notification/
  package.json                       # com.tk.notification 0.1.0, unity 6000.0, dep: com.unity.mobile.notifications (newest stable, default registry)
  Runtime/
    TK.Notification.asmdef           # rootNamespace TK.Notification; references the com.unity.mobile.notifications asmdef; autoReferenced true
    NotificationService.cs           # orchestration: schedule/scheduleAll (cancel-then-reschedule)/cancel/permission/quiet-hours/launch — all through the seam
    QuietHoursSettings.cs            # pure readonly struct + Apply() (ported from reference, with its logic preserved)
    NotificationChannel.cs           # typed channel descriptor + NotificationImportance enum
    NotificationRequest.cs           # what the game declares: channel + title/body + absolute DeliveryTime + data + icons
    NotificationResponse.cs          # the notification that launched/resumed the app
    Seams/
      INotificationBackend.cs        # native-platform seam (must-not-throw contract)
    Backends/
      UnityMobileNotificationBackend.cs  # real impl (#if ANDROID/IOS + #else no-op); the ONLY file using Unity.Notifications.*
  Tests/Editor/
    TK.Notification.Tests.asmdef     # references TK.Notification + TestRunner + nunit
    FakeNotificationBackend.cs       # records scheduled/cancelled/channels; permission + launch + availability knobs
    QuietHoursSettingsTests.cs       # ported reference quiet-hours cases
    NotificationServiceTests.cs      # orchestration against the fake
    FakeNotificationBackendTests.cs  # fake smoke
  Samples~/
    NotificationDemo/                # NotificationDemo.cs — ContextMenu demo on the real backend (no-op in editor); engagement-funnel pattern + README
    IntegrationExamples/             # EngagementReminders, RemoteConfigQuietHoursBridge, LaunchRouter + README
  README.md / CHANGELOG.md
```

## Public API surface

```csharp
public enum NotificationImportance { Low, Default, High }   // maps to Android Importance; iOS ignores

public readonly struct NotificationChannel
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public NotificationImportance Importance { get; }
    public NotificationChannel(string id, string name, string description = null,
        NotificationImportance importance = NotificationImportance.Default);
}

public readonly struct NotificationRequest
{
    public string ChannelId { get; }
    public string Title { get; }
    public string Body { get; }
    public DateTime DeliveryTime { get; }   // absolute; the service applies quiet-hours before scheduling
    public string Data { get; }             // opaque payload surfaced on launch; may be null
    public string SmallIcon { get; }        // Android; may be null
    public string LargeIcon { get; }        // Android; may be null
    public NotificationRequest(string channelId, string title, string body, DateTime deliveryTime,
        string data = null, string smallIcon = null, string largeIcon = null);
}

public readonly struct NotificationResponse
{
    public string ChannelId { get; }
    public string Data { get; }
    public NotificationResponse(string channelId, string data);
}

public interface INotificationBackend
{
    /// <summary>True on a mobile device with the platform API available; false in Editor/desktop (no-op backend).</summary>
    bool IsAvailable { get; }
    void RegisterChannel(NotificationChannel channel);
    /// <summary>Schedule one notification (DeliveryTime already quiet-hours-adjusted). Returns the platform id.</summary>
    int Schedule(NotificationRequest request);
    void Cancel(int id);
    void CancelAll();
    Task<bool> RequestPermissionAsync();
    bool IsPermissionGranted();
    /// <summary>True if the app was launched/resumed by tapping a notification; outputs its channel + data.</summary>
    bool TryGetLaunchNotification(out NotificationResponse response);
    void OpenSettings();
}

public sealed class NotificationService
{
    public NotificationService(INotificationBackend backend, QuietHoursSettings quietHours = default);

    public QuietHoursSettings QuietHours { get; set; }
    public bool IsSupported { get; }                     // backend.IsAvailable

    public void RegisterChannel(NotificationChannel channel);

    /// <summary>Applies quiet-hours to DeliveryTime, schedules via the backend, returns the id. Returns null when !IsSupported.</summary>
    public int? Schedule(NotificationRequest request);

    /// <summary>Cancel-all-then-reschedule: cancels every scheduled notification, then schedules the given set (each quiet-hours-adjusted).</summary>
    public void ScheduleAll(IReadOnlyList<NotificationRequest> requests);

    public void Cancel(int id);
    public void CancelAll();

    public Task<bool> RequestPermissionAsync();
    public bool IsPermissionGranted();

    public bool TryGetLaunchNotification(out NotificationResponse response);
    public void OpenNotificationSettings();
}

public readonly struct QuietHoursSettings
{
    public bool Enabled { get; }
    public int StartHour { get; }   // inclusive, 0-23
    public int EndHour { get; }     // exclusive, 0-23
    public QuietHoursSettings(bool enabled, int startHour, int endHour);

    /// <summary>
    /// Returns fireTime unchanged when disabled or outside the window; otherwise shifts forward to EndHour:00
    /// (same or next day), preserving DateTime.Kind. Supports wrapping windows (e.g. 23→7). default() is disabled.
    /// </summary>
    public DateTime Apply(DateTime fireTime);
}
```

`INotificationBackend` contract: implementations **must not throw** — the service wraps native calls and logs (a flaky OS call never crashes the game). The real `UnityMobileNotificationBackend` returns `IsAvailable == false` and no-ops on Editor/desktop (`#else`), so `NotificationService` is safe to use on every target.

## Behavioral contracts (reference-derived, test-pinned)

- **Quiet-hours applied service-side.** `Schedule`/`ScheduleAll` pass each request's `DeliveryTime` through `QuietHours.Apply(...)` before the backend call. `QuietHours` defaults to `default` (disabled → no shift). Pure and unit-tested independently of any backend.
- **`ScheduleAll` = cancel-all-then-reschedule.** It calls `backend.CancelAll()` then schedules each request in order. This is the reference's robust pattern: the game re-declares its full reminder set (with fresh absolute times) each launch/resume, so there is no stale-schedule or duplicate-notification risk and no on-disk persistence is needed.
- **`IsSupported` / no-op on non-mobile.** When `backend.IsAvailable` is false (Editor/desktop), `Schedule` returns `null`, `ScheduleAll`/`Cancel`/`RegisterChannel`/`OpenNotificationSettings` are safe no-ops, `IsPermissionGranted` returns false, `RequestPermissionAsync` returns false, `TryGetLaunchNotification` returns false. Nothing throws; nothing requires `#if` at the call site.
- **Async permission.** `RequestPermissionAsync()` awaits the backend's platform request (iOS `AuthorizationRequest`, Android `POST_NOTIFICATIONS` `PermissionRequest`) and returns whether it was granted. No coroutine, no spawned GameObject.
- **Ids are backend-assigned.** `Schedule` returns the id the backend assigned (`int`); the fake returns a deterministic counter so tests can assert. iOS uses `id.ToString()` as the identifier internally.
- **Launch routing is the game's job.** `TryGetLaunchNotification` surfaces the launching notification's `ChannelId`+`Data`; the package does not interpret `Data` (the game routes it — e.g. to `com.tk.analytics` or a deep-link). Returns false when the app wasn't launched from a notification (and always in Editor).
- **Backend errors are swallowed.** Each backend call in the service is guarded; an exception is logged (`Debug.LogException`) and the service continues (matches the must-not-throw seam contract and the other TK packages).
- **Main-thread usage assumed** (Unity notification APIs are main-thread; documented).

## Samples

- `Samples~/NotificationDemo/NotificationDemo.cs` — editor-runnable `MonoBehaviour` building `new NotificationService(new UnityMobileNotificationBackend())` (no-op in editor, real on device). `[ContextMenu]` triggers: Register Channel, Request Permission, Schedule Engagement Funnel (builds a `List<NotificationRequest>` at `DateTime.Now` + 1/3/7/14/30-day offsets and calls `ScheduleAll`), Cancel All, Check Launch. Demonstrates the cancel-all-then-reschedule engagement pattern. + README.
- `Samples~/IntegrationExamples/`:
  - `EngagementReminders.cs` — the reference's engagement funnel cleaned up: a game-owned helper that builds the reminder `List<NotificationRequest>` (offsets + channel + game-supplied localized copy) to hand to `ScheduleAll`. Shows that content/timing/localization live in the game.
  - `RemoteConfigQuietHoursBridge.cs` — one-liner mapping `com.tk.remoteconfig` → `QuietHoursSettings` (e.g. `service.QuietHours = new QuietHoursSettings(rc.Bool("quiet_enabled", true), rc.Int("quiet_start", 23), rc.Int("quiet_end", 7))`). References `TK.RemoteConfig`; compiles only when that package is present. No hard dependency.
  - `LaunchRouter.cs` — reads `TryGetLaunchNotification` at startup and routes `Data` (a switch/dispatch stub), the clean replacement for the reference's hardcoded analytics call.
  - README.

## Testing

EditMode suite on `FakeNotificationBackend` (records channels/scheduled/cancelled; knobs: `IsAvailable`, `PermissionResult`, an injectable launch `NotificationResponse`; deterministic incrementing ids). Target ≈25–30 tests:
- `QuietHoursSettings.Apply`: ported reference cases — outside-window unchanged; inside-window shifts to EndHour; wrapping window (23→7); non-wrapping (1→6); `default`/disabled is a no-op; `DateTime.Kind` preserved.
- `NotificationService`: quiet-hours applied before scheduling (backend receives the shifted time); no shift when disabled; `Schedule` returns the backend id; `Schedule` returns `null` and does not call the backend when `!IsSupported`; `ScheduleAll` calls `CancelAll` once then schedules each request in order; `RegisterChannel` reaches the backend; `RequestPermissionAsync` returns the backend result; `IsPermissionGranted` reflects the backend; `TryGetLaunchNotification` surfaces the injected response (and false when none); `Cancel`/`CancelAll`/`OpenNotificationSettings` reach the backend; backend throwing is swallowed (logged, no escape).
- `FakeNotificationBackend` smoke.

The real `UnityMobileNotificationBackend` is review-verified (its Android/iOS branches aren't compiled in the Editor harness; the `#else` no-op compiles and is what tests exercise indirectly). RE-VERIFY every `com.unity.mobile.notifications` member the backend calls against the installed package version at execution. Harness gate identical to the other packages: scratch project consuming the package via `file:`, `-batchmode -runTests -testPlatform EditMode` (never `-quit` with `-runTests`), zero warnings under `Packages/com.tk.notification`.

## v2+ reserves (no API breaks planned)

- **Remote push (FCM)** via an `IPushBackend` seam + a Firebase Cloud Messaging sample (token, topic subscription, foreground message handling) — deliberately out of v1.
- **Relative-offset request helper** (`NotificationRequest.After(TimeSpan, ...)` resolved against a reference time) if the absolute-time call sites prove verbose.
- **Persistent scheduled-catalog** (serialize + hydrate) if a game needs to cancel individual reminders across launches rather than the cancel-all-then-reschedule model.
- **iOS notification categories / action buttons**, **badge-count control**, and a **launch-routing helper** (typed dispatch table) if games want more than raw `Data`.
- **`com.tk.core` opt-in-persistence sample** (wiring the app-level toggle through `ISaveSystem`) — kept out of the standalone package.
