# TK Analytics

Backend-agnostic analytics façade: log events, revenue, and user properties through one API that
fans out to any backend (Firebase, Adjust, …), with a consent gate and loss-free pre-init buffering.
Unifies the `com.tk.iap` / `com.tk.ads` monetization event stream through a single pipeline.

## What's inside

| Module | Location | What it gives you |
| --- | --- | --- |
| Service | `Runtime/AnalyticsService.cs` | `AnalyticsService` — the multi-backend engine. Fans every op out to all backends, gates dispatch on enabled/consent/started, buffers what can't dispatch yet (in order, params preserved), and isolates each backend call in try/catch. |
| Interface | `Runtime/IAnalytics.cs` | `IAnalytics` — the API surface: `LogEvent`, `LogPurchase`, `LogAdRevenue`, `SetUserProperty`, `SetUserId`, `SetConsent`, `Flush`, `StartAsync`, and an `IsEnabled` kill-switch. |
| Static façade | `Runtime/Analytics.cs` | `Analytics` — ambient access point (`SetInstance` + the log verbs) so you can log from anywhere. Null-safe: no-ops (one editor warning) until an instance is set. |
| Values | `Runtime/AnalyticsParam.cs`, `AnalyticsEvent.cs` | `AnalyticsParam` — allocation-free typed (String/Long/Double/Bool) parameter via static factories; `AnalyticsEvent` — a name plus its parameters. |
| Revenue | `Runtime/Revenue/` | `AnalyticsPurchase` / `AnalyticsAdRevenue` — neutral, package-owned revenue records (no vendor or IAP/Ads types leak into the API). |
| Seam + backend | `Runtime/Seams/`, `Runtime/Backends/` | `IAnalyticsBackend` — the single interface a backend implements (must not throw). Ships one built-in backend, `ConsoleAnalyticsBackend`; Firebase/Adjust are samples. |

## Install

No scoped registry and no dependencies — this package is install-by-git-URL only. Add it via Package
Manager → **Add package from git URL**:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.analytics
```

Pinned to a released version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.analytics#com.tk.analytics/0.1.0
```

The Firebase/Adjust backend adapters and the IAP/Ads bridges ship as **samples** (they reference
those SDKs/packages), so nothing here forces a dependency on you — import the `Integration Examples`
sample and adapt only what you use. To see this package's EditMode tests in your own project's Test
Runner, add `"testables": ["com.tk.analytics"]` to your project's `Packages/manifest.json`.

## Quickstart

```csharp
var analytics = new AnalyticsService(new IAnalyticsBackend[] { new ConsoleAnalyticsBackend() });
Analytics.SetInstance(analytics);          // ambient access from anywhere

await analytics.StartAsync();              // initialize backends, mark started
analytics.SetConsent(true);               // open the gate (call after your consent flow)

Analytics.LogEvent("level_start", AnalyticsParam.Long("level", 3));
```

`StartAsync` and `SetConsent` are both required before anything dispatches — call them at bootstrap
(order doesn't matter; whichever completes the gate last triggers the flush). Everything logged
before then is buffered, not lost.

## Consent & buffering

Log calls are gated on three conditions — **started** (`StartAsync` finished), **enabled**
(`IsEnabled`), and **consent** — and the consent state drives what happens to what can't dispatch:

| Consent | Behavior |
| --- | --- |
| `Unknown` (default) | Buffer every op in order (GDPR-safe: nothing is sent before a decision). |
| `Granted` | Dispatch live; on the transition, flush the whole buffer in order with parameters intact. |
| `Denied` | Clear the buffer and block dispatch — buffered ops are discarded, never sent. |

`IsEnabled` is a runtime kill-switch orthogonal to consent: setting it `false` drops new ops and
pauses the flush (the buffer is kept); setting it back `true` resumes. Use it to hard-stop analytics
without touching the user's consent choice.

## Backends

`AnalyticsService` fans every operation out to all backends and isolates each call in try/catch, so
one throwing backend never blocks the others (the exception is logged and the rest still run). Write
your own by implementing `IAnalyticsBackend`:

```csharp
public sealed class MyBackend : IAnalyticsBackend
{
    public string Name => "MyBackend";
    public Task InitializeAsync() => Task.CompletedTask;
    public void LogEvent(AnalyticsEvent evt) { /* map evt.Parameters, must not throw */ }
    public void LogPurchase(AnalyticsPurchase p) { /* map to a native purchase/revenue call */ }
    public void LogAdRevenue(AnalyticsAdRevenue a) { /* map to a native ad-revenue call */ }
    public void SetUserProperty(string key, string value) { }
    public void SetUserId(string userId) { }
    public void Flush() { }
}
```

A backend **must not throw** (the service catches and logs, but fail safe on your side too). One with
no native revenue API can map `LogPurchase`/`LogAdRevenue` onto a `LogEvent` internally; one that
doesn't care about a call simply no-ops. See the `Integration Examples` sample for real
`FirebaseAnalyticsBackend` and (selective) `AdjustAnalyticsBackend` adapters.

## Monetization bridges

`com.tk.iap`'s `IPurchaseReporter` and `com.tk.ads`'s `IAdRevenueReporter` are natural producers —
the `Integration Examples` sample ships one-line bridges that forward them into analytics:

```csharp
iapOptions.Reporter        = new AnalyticsPurchaseReporter(analytics);
adsOptions.RevenueReporter = new AnalyticsAdRevenueReporter(analytics);
```

This package has **no dependency** on iap/ads — the bridges are samples you copy into a project that
has all three. They're the "one analytics pipeline for everything" alternative to iap/ads' own
direct-to-Firebase reporter samples: use those to report straight to Firebase, or use these to fan
the same purchase/revenue events out to every configured analytics backend at once. Wire one path per
producer, not both.

## Revenue

`LogPurchase(AnalyticsPurchase)` and `LogAdRevenue(AnalyticsAdRevenue)` take neutral, package-owned
structs (no vendor or IAP/Ads types in the API) — each backend maps them onto its native revenue API:
`FirebaseAnalyticsBackend` emits a GA4 `purchase` / `ad_impression` event, `AdjustAnalyticsBackend`
calls `AdjustEvent.SetRevenue` / `Adjust.TrackAdRevenue`. `AnalyticsPurchase.Price` is a `double`
(localized price) and the bridge converts from `com.tk.iap`'s `decimal`.

## Gotchas

- **Main-thread-affine.** The service holds no locks — call it from Unity's main thread. Background
  callers (e.g. an SDK callback on an arbitrary thread) must marshal to the main thread first.
- **The static `Analytics` façade no-ops until `SetInstance`.** Calls before an instance is set are
  dropped (with a one-time editor warning) so untested/isolated scenes never `NullReference` — wire
  `Analytics.SetInstance(...)` at bootstrap.
- **Call `StartAsync` + `SetConsent` at bootstrap.** Until both complete the gate, everything buffers;
  it's loss-free, but nothing reaches a backend until dispatch is allowed.
- **At-least-once purchase redelivery.** If you use the IAP bridge, `OnPurchaseConfirmed` can fire
  again for a transaction already reported (crash-recovery) — dedupe by `TransactionId` downstream
  before counting a paid conversion.
- **`params AnalyticsParam[]` allocates.** The array-based `LogEvent(name, params …)` overload
  allocates per call — fine at normal event frequency, but avoid it on a per-frame hot path.
