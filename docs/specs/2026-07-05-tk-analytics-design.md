# com.tk.analytics v0.1.0 — Design Spec

Status: APPROVED by Tolgahan 2026-07-05 (approach + all sections + the 3 judgment calls). Committed to `docs/specs/` (referenceable from the repo by any developer/agent), following the standard set with `com.tk.remoteconfig`.

## Purpose

A backend-agnostic analytics façade for TK games: log events, revenue, and user properties through one API that fans out to any number of backend adapters (Firebase, Adjust, …), tested with a fake backend. Its reason to exist is to **unify the monetization event stream** — the two producers the shipped packages already expose (`com.tk.iap`'s `IPurchaseReporter`, `com.tk.ads`'s `IAdRevenueReporter`) forward into it through thin, game-authored bridges, so purchase and ad-revenue events reach every configured analytics backend from a single point. It also owns two mechanisms the reference system was missing: a **consent gate** (GDPR-safe buffering) and a **loss-free pre-init queue**. Same engineering bar as the other TK packages (seam-based, deterministic EditMode tests, per-task + whole-branch review; Opus-only per user request for monetization-adjacent packages).

## Reference system (analyzed, read-only)

`g-brain_test_5`, deep read-only teardown performed 2026-07-05; key findings summarized here. Mechanism files: `Assets/UnicoStudio/UnicoLibs/FirebaseManager/FirebaseEventManager.cs` (event dispatch + `ConcurrentQueue` pre-init buffer + `EventParameter`→`Firebase.Analytics.Parameter[]` conversion), `EventParameter.cs` (SDK-agnostic typed param mirroring Firebase's `Parameter`), `AdjustService.cs`/`AdjustEventController.cs` (attribution + token-based event/revenue dispatch, fans out to Firebase), `MyIAPManager.cs` (purchase → `c_purchase_verified` + iOS-only GA4 `Purchase`), `NeftaAdapterEvents.cs` (ad-revenue captured by Nefta only, never reaching Firebase). Game-specific taxonomy (`BrainQuestAnalytics`, `FailFlowAnalytics`, `FirebaseConstants`) stays in the game — the package provides mechanism, the game supplies the event names.

Five gaps the teardown revealed, which become this package's v1 value:
1. No generic `SetUserProperty` (only Adjust attribution set Firebase user properties, via a delegate).
2. No generic revenue-logging interface.
3. Ad revenue never reached the analytics backend (isolated inside Nefta).
4. No consent / GDPR abstraction (Facebook only toggled IDFA).
5. **Bug:** `FirebaseEventManager.SendEvent(name, Parameter[])` enqueued a param-less event when fired before init — parameters were silently lost.

## Locked decisions

1. **API shape:** instance `AnalyticsService : IAnalytics` (the testable core) **plus** a thin static `Analytics` façade that delegates to a single `IAnalytics` set once at bootstrap. Call sites use `Analytics.LogEvent(...)` (ergonomic, null-safe no-op when unset — analytics is called from everywhere and must never NRE an untested scene); tests always drive the instance directly. Same spirit as the suite's `Service.Instance` convenience, but null-safe and decoupled from the concrete type.
2. **Backend-agnostic:** package fans out to `IAnalyticsBackend` adapters; **ZERO backend dependency** in the package, fully testable with a fake. Firebase and Adjust adapters ship as **Samples** with real SDK code (compile once imported into a project that has the SDK; `Samples~` are never compiled in the harness). Package never references any vendor SDK and never forces one on consumers.
3. **Revenue path:** neutral, package-owned `AnalyticsPurchase` / `AnalyticsAdRevenue` structs + typed `LogPurchase` / `LogAdRevenue` methods on the façade **and** the backend seam, so each backend maps to its native revenue API (Firebase `EventPurchase`, Adjust `TrackAdRevenue`). The iap/ads info types never appear in this package — game-authored bridges translate `IapPurchaseInfo`/`AdRevenueInfo` into the neutral structs.
4. **Consent + loss-free queue (v1):** a consent gate with an ordered pre-dispatch buffer that preserves parameters (structurally fixes reference gap #5). Consent state is **game-owned** (passed in via `SetConsent`), so the package stays standalone — no `com.tk.core`/`ISaveSystem` dependency for persistence; the game persists the user's choice and replays it on launch.
5. **Zero runtime dependencies:** params are typed structs (no JSON → no Newtonsoft), consent is game-owned (no core), backends are samples (no vendor SDK). `com.tk.analytics` declares **no** dependencies (like `com.tk.toolbar`).
6. **Three judgment calls (approved):**
   - **Main-thread affinity** — the service is main-thread-affine (no locks); callers marshal from background threads. Both producers (iap/ads reporters) already deliver on the main thread; consistent with the suite and Unity norms. Thread-safe ingestion is a non-breaking v2 reserve.
   - **`ConsoleAnalyticsBackend` is built-in** (Runtime, dependency-free `Debug.Log` backend) rather than a sample — universally useful in-editor/QA with no SDK.
   - **`params AnalyticsParam[]`** ingestion — the per-call array allocation is negligible at analytics event frequency; a zero-alloc builder is a non-breaking v2 reserve.
7. **No SDK/vendor type leak:** `Firebase.*` / `AdjustSdk.*` never appear in any public API — only inside the respective sample adapters.

## Package layout

```
Packages/com.tk.analytics/
  package.json                       # com.tk.analytics 0.1.0, unity 6000.0, NO dependencies
  Runtime/
    TK.Analytics.asmdef              # name TK.Analytics, references: [] (zero — standalone), autoReferenced true
    IAnalytics.cs                    # façade interface
    Analytics.cs                     # static façade: SetInstance/ClearInstance + delegating logging members (null-safe)
    AnalyticsService.cs              # multi-backend fan-out + gating state machine + ordered buffer
    AnalyticsParam.cs                # AnalyticsParam struct + AnalyticsParamType enum (allocation-free)
    AnalyticsEvent.cs                # AnalyticsEvent struct (name + params) — the buffered unit
    AnalyticsConsent.cs              # enum Unknown/Granted/Denied
    Revenue/
      AnalyticsPurchase.cs           # neutral purchase struct
      AnalyticsAdRevenue.cs          # neutral ad-revenue struct
    Backends/
      ConsoleAnalyticsBackend.cs     # built-in Debug.Log backend (dependency-free)
    Seams/
      IAnalyticsBackend.cs           # backend adapter seam (must-not-throw contract)
  Tests/Editor/
    TK.Analytics.Tests.asmdef        # references TK.Analytics
    FakeAnalyticsBackend.cs          # recording double: Events/Purchases/AdRevenues/UserProperties/UserId/Flush/Init counts + optional throw knobs
    AnalyticsServiceTests.cs
    AnalyticsParamTests.cs
    AnalyticsStaticFacadeTests.cs
  Samples~/
    AnalyticsDemo/                   # AnalyticsDemo.cs — ContextMenu demo on ConsoleAnalyticsBackend (event/purchase/ad-revenue/consent flow) + README
    IntegrationExamples/             # FirebaseAnalyticsBackend, AdjustAnalyticsBackend, AnalyticsPurchaseReporter, AnalyticsAdRevenueReporter + README
  README.md / CHANGELOG.md
```

Fakes live in `Tests/Editor/` (suite convention: FakeAdsGateway, FakeStoreGateway, FakeRemoteConfigBackend). The demo uses the built-in `ConsoleAnalyticsBackend`; fan-out and gating are exercised in EditMode tests with two `FakeAnalyticsBackend`s.

## Public API surface

```csharp
public enum AnalyticsParamType { String, Long, Double, Bool }

public readonly struct AnalyticsParam
{
    public string Key { get; }
    public AnalyticsParamType Type { get; }
    public string StringValue { get; }   // valid when Type == String
    public long   LongValue { get; }     // valid when Type == Long
    public double DoubleValue { get; }   // valid when Type == Double
    public bool   BoolValue { get; }     // valid when Type == Bool

    public static AnalyticsParam String(string key, string value);
    public static AnalyticsParam Long(string key, long value);
    public static AnalyticsParam Double(string key, double value);
    public static AnalyticsParam Bool(string key, bool value);
    public override string ToString();   // "key=value" for debugging
}

public readonly struct AnalyticsEvent
{
    public string Name { get; }
    public IReadOnlyList<AnalyticsParam> Parameters { get; }   // never null; empty when none
    public AnalyticsEvent(string name, IReadOnlyList<AnalyticsParam> parameters = null);
}

public readonly struct AnalyticsPurchase
{
    public string ProductId { get; }
    public double Price { get; }          // localized price as double (backends want double)
    public string Currency { get; }       // ISO 4217; may be null
    public string TransactionId { get; }
    public int    Quantity { get; }       // defaults to 1
    public bool   IsRestore { get; }
    public AnalyticsPurchase(string productId, double price, string currency,
        string transactionId, int quantity = 1, bool isRestore = false);
}

public readonly struct AnalyticsAdRevenue
{
    public string Format { get; }         // "banner"/"interstitial"/"rewarded"
    public string AdNetwork { get; }      // winning mediated network
    public string AdUnitId { get; }
    public double Revenue { get; }
    public string Currency { get; }       // ISO 4217 (usually "USD")
    public string Placement { get; }      // may be null
    public AnalyticsAdRevenue(string format, string adNetwork, string adUnitId,
        double revenue, string currency, string placement);
}

public interface IAnalytics
{
    bool IsEnabled { get; set; }          // runtime kill-switch; false = inert (drops new, pauses flush)
    void LogEvent(string name);
    void LogEvent(string name, params AnalyticsParam[] parameters);
    void LogPurchase(AnalyticsPurchase purchase);
    void LogAdRevenue(AnalyticsAdRevenue adRevenue);
    void SetUserProperty(string key, string value);
    void SetUserId(string userId);
    void SetConsent(bool granted);        // true → flush buffer + allow; false → clear buffer + block
    void Flush();                         // ask backends to flush their own buffers
    Task StartAsync();                    // initialize all backends, then flush if gate open
}

public sealed class AnalyticsService : IAnalytics
{
    public AnalyticsService(IEnumerable<IAnalyticsBackend> backends);
    public AnalyticsConsent Consent { get; }   // read-only current state (QA/debug)
    public bool IsStarted { get; }
    // ... IAnalytics members ...
}

public static class Analytics
{
    public static IAnalytics Instance { get; }
    public static bool HasInstance { get; }
    public static void SetInstance(IAnalytics instance);
    public static void ClearInstance();

    // Delegating logging members (no-op + one-time editor warning when Instance == null):
    public static void LogEvent(string name);
    public static void LogEvent(string name, params AnalyticsParam[] parameters);
    public static void LogPurchase(AnalyticsPurchase purchase);
    public static void LogAdRevenue(AnalyticsAdRevenue adRevenue);
    public static void SetUserProperty(string key, string value);
    public static void SetUserId(string userId);
}
```

The static façade intentionally exposes only the "fire from anywhere" verbs. Lifecycle/config (`StartAsync`, `SetConsent`, `IsEnabled`, `Flush`) live on the instance the game holds at its wiring points (bootstrap, consent dialog, settings screen).

`IAnalyticsBackend`:
```csharp
public interface IAnalyticsBackend
{
    string Name { get; }                  // for logging/diagnostics
    Task InitializeAsync();               // native SDK setup; return CompletedTask if none
    void LogEvent(AnalyticsEvent evt);
    void LogPurchase(AnalyticsPurchase purchase);
    void LogAdRevenue(AnalyticsAdRevenue adRevenue);
    void SetUserProperty(string key, string value);
    void SetUserId(string userId);
    void Flush();
}
```
Contract: implementations **must not throw** — the service wraps every call in try/catch, logs, and continues (one bad backend never blocks the others). A backend with no native revenue API maps `LogPurchase`/`LogAdRevenue` onto a `LogEvent` internally; a backend that ignores a call (e.g. Adjust for an unmapped event name) simply no-ops.

## Behavioral contracts (reference-derived, test-pinned)

- **One ingestion path, one ordered buffer.** Every operation (`LogEvent`, `LogPurchase`, `LogAdRevenue`, `SetUserProperty`, `SetUserId`) is an ordered command. Dispatch precedence:
  1. `!IsEnabled` → **drop** (kill-switch; nothing recorded).
  2. `Consent == Denied` → **drop** (GDPR: never send).
  3. `IsStarted && Consent == Granted` → **dispatch** to all backends now.
  4. otherwise (enabled, not denied, but not yet started or consent still Unknown) → **buffer** in order.
- **Buffering preserves parameters and order.** The buffer stores whole `AnalyticsEvent`/typed-revenue/user-op commands and replays them in FIFO order on flush — a `SetUserId` fired before `session_start` reaches the backend first. This structurally prevents reference gap #5 (param loss).
- **`StartAsync` is single-flight.** Concurrent/repeat calls await the same task (iap/ads/RC pattern). It awaits every `backend.InitializeAsync()` (each wrapped so one failure neither throws nor blocks the rest), sets `IsStarted`, then flushes if the gate is open. A backend whose init throws is logged and still receives subsequent calls (its own concern whether they succeed).
- **Consent transitions.** `SetConsent(true)` → `Consent = Granted`, then flush (if started + enabled). `SetConsent(false)` → `Consent = Denied`, **clear the buffer** (pending events are discarded, never sent). Default `Consent = Unknown` buffers indefinitely until a decision — GDPR-safe without losing startup events.
- **`IsEnabled`** gates both ingestion and flush (`false` = inert). It does not clear the buffer; re-enabling resumes. Toggling it is the user-settings kill-switch.
- **Backend isolation.** Each backend call is individually try/caught; an exception is logged (`Debug.LogException`) and swallowed; remaining backends still receive the call.
- **`Flush()`** asks each backend to flush its own native buffer (`backend.Flush()`); it is distinct from the internal consent-queue drain and does not bypass consent.
- **Static façade.** `Analytics.X` forwards to `Instance`; when `Instance == null` every member is a no-op (with a single editor-only warning), so unwired/isolated scenes never NRE. `SetInstance`/`ClearInstance` manage the ambient reference (tests clear between cases).
- **Main-thread contract (documented, not enforced).** `IAnalytics` is main-thread-affine; background-thread callers marshal first. The iap/ads bridges are already main-thread per those reporters' contracts.

## Monetization bridges (Samples + one-liners — package stays standalone)

Both ship in `Samples~/IntegrationExamples`, each referencing the respective TK package **and** `TK.Analytics` (compiled only when imported into a project that has both). They are the "unified" path: monetization events flow through `IAnalytics` to every configured backend, rather than each reporter talking to one backend directly.

- `com.tk.iap` → `class AnalyticsPurchaseReporter : IPurchaseReporter`:
  - `OnPurchaseConfirmed(IapPurchaseInfo i)` → `_analytics.LogPurchase(new AnalyticsPurchase(i.ProductId, (double)i.LocalizedPrice, i.IsoCurrencyCode, i.TransactionId, 1, i.IsRestore))`.
  - `OnPurchaseFailed(id, reason)` → `_analytics.LogEvent("purchase_failed", AnalyticsParam.String("product_id", id), AnalyticsParam.String("reason", reason))`.
  - Inherits `IPurchaseReporter`'s at-least-once redelivery caveat: downstream/backends dedupe by `TransactionId` (documented).
- `com.tk.ads` → `class AnalyticsAdRevenueReporter : IAdRevenueReporter`:
  - `OnAdRevenue(AdRevenueInfo i)` → `_analytics.LogAdRevenue(new AnalyticsAdRevenue(i.Format, i.NetworkName, i.AdUnitId, i.Revenue, i.Currency, i.Placement))`.

Wire-up is a one-liner: `iapOptions.Reporter = new AnalyticsPurchaseReporter(analytics)` / `adsOptions.RevenueReporter = new AnalyticsAdRevenueReporter(analytics)`. The existing `FirebaseReporterExample` (iap) and `FirebaseAdRevenueReporterExample` (ads) remain as "direct-to-one-backend" examples; the README cross-links both approaches so the choice is explicit.

## Backend adapters (Samples, real SDK code)

- `Samples~/IntegrationExamples/FirebaseAnalyticsBackend.cs` — `IAnalyticsBackend` over `Firebase.Analytics`:
  - `InitializeAsync` → `FirebaseApp.CheckAndFixDependenciesAsync()` (or assumes a game-level Firebase init; documented), marks ready.
  - `LogEvent` → convert `AnalyticsParam[]` → `Firebase.Analytics.Parameter[]` (switch on `Type`) → `FirebaseAnalytics.LogEvent(name, params)`.
  - `LogPurchase` → `FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase, [ParameterTransactionID, ParameterCurrency, ParameterValue, ParameterItems])` — **cross-platform**, fixing the reference's iOS-only GA4 purchase omission.
  - `LogAdRevenue` → `FirebaseAnalytics.LogEvent("ad_impression", [ad_platform, ad_source=network, ad_unit_name, ad_format, value, currency])`.
  - `SetUserProperty` → `FirebaseAnalytics.SetUserProperty`; `SetUserId` → `FirebaseAnalytics.SetUserId`; `Flush` → no-op (Firebase auto-flushes).
  - README: import only into a Firebase-present project (Firebase is the game's dependency; the package never forces it).
- `Samples~/IntegrationExamples/AdjustAnalyticsBackend.cs` — `IAnalyticsBackend` over `AdjustSdk` (a **selective** backend, demonstrating that fan-out backends need not consume every call):
  - ctor takes an optional `IReadOnlyDictionary<string,string> eventTokens` (event name → Adjust token); `LogEvent` tracks only mapped names (`Adjust.TrackEvent(new AdjustEvent(token))`), else no-ops — matching Adjust's milestone-token model.
  - `LogAdRevenue` → `Adjust.TrackAdRevenue(AdjustAdRevenue)` (Adjust's dedicated ad-revenue API — a clean fit); `LogPurchase` → a revenue `AdjustEvent` when a purchase token is provided.
  - `SetUserId`/`SetUserProperty` → global callback parameters (Adjust has no first-class user properties; documented).

## Testing

EditMode suite on `FakeAnalyticsBackend` (records Events/Purchases/AdRevenues/UserProperties/UserId, Flush/Init counts, optional per-method throw knobs). Target ≈22–26 tests:
- event before `StartAsync` → buffered, backend receives nothing;
- after `StartAsync` + `SetConsent(true)` → buffered event flushed **with params intact** (reference gap #5 regression);
- `StartAsync` but consent still Unknown → still buffered;
- `SetConsent(false)` → buffer cleared, backend receives nothing, later events dropped;
- `IsEnabled = false` → operation dropped (not buffered, not dispatched);
- `StartAsync` calls `InitializeAsync` once per backend; repeat call is single-flight (still once);
- `LogPurchase` / `LogAdRevenue` buffered pre-ready, dispatched post-ready with all fields intact;
- `SetUserProperty` / `SetUserId` buffered and replayed in FIFO order (user id before a later event);
- fan-out: two backends both receive a dispatched event;
- backend throwing in `LogEvent` → the other backend still receives; no exception escapes;
- backend throwing in `InitializeAsync` → `StartAsync` completes, other backends init, service usable;
- `Flush()` → each backend's `Flush()` called;
- `AnalyticsParam` factories: correct `Type` + value round-trip + `ToString`;
- static `Analytics`: null-safe no-op when unset, delegation after `SetInstance`, `ClearInstance`.

The Firebase/Adjust adapters and the two bridges are review-verified (samples aren't compiled in the harness). Harness gate identical to the other packages: scratch project consuming the package via `file:`, `-batchmode -runTests -testPlatform EditMode` (never `-quit` with `-runTests`), zero warnings.

## v2+ reserves (no API breaks planned)

- **Thread-safe ingestion** (`ConcurrentQueue` + lock) if background-thread logging becomes a real need.
- **Zero-alloc param builder** / pooled params for any high-frequency event path.
- **Nested/array params** (GA4 `items` beyond the purchase struct; the reference's `IDictionary`/`IEnumerable` param cases).
- **More backend samples** (GameAnalytics, AppsFlyer, Amplitude, Unity Analytics).
- **AppContext lifecycle auto-flush** (flush on pause/quit) as an optional `com.tk.core`-integrated sample — kept out of the standalone package.
- **Consent-region / DMA policy helpers** if a target market needs finer-grained consent than a single boolean.
