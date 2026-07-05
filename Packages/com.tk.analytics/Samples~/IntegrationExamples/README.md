# Integration Examples

Reference implementations wiring `com.tk.analytics` to real backends and to the `com.tk.iap` /
`com.tk.ads` reporter seams. Copy what you need into your project and adapt it — these are examples,
not drop-in production code.

Unlike the `AnalyticsDemo` sample (which runs on the built-in Console backend with no SDK), the two
**backend adapters** here reference third-party SDKs and the two **bridges** reference the IAP/Ads
packages, so each compiles only once its dependency is present in your project.

## FirebaseAnalyticsBackend (`IAnalyticsBackend`)

Forwards every call to Firebase Analytics. References `Firebase.Analytics` — compiles only with the
Firebase Analytics SDK installed. Add it to the service:

```csharp
var analytics = new AnalyticsService(new IAnalyticsBackend[] { new FirebaseAnalyticsBackend() });
```

Notes:

- `InitializeAsync` calls `FirebaseApp.CheckAndFixDependenciesAsync()`. If your game already
  initializes Firebase centrally on boot, drop that and just `return Task.CompletedTask`.
- `LogPurchase` logs a GA4 `purchase` event **cross-platform** — the common reference guards it with
  `#if UNITY_IOS` and so drops Android purchases; this one doesn't.
- `LogAdRevenue` logs `ad_impression` with `ad_platform = "AppLovin"` (MAX is where com.tk.ads sources
  revenue — change it if you feed non-MAX revenue through).
- `Flush` is a no-op (Firebase batches/flushes itself).

## AdjustAnalyticsBackend (`IAnalyticsBackend`)

Forwards to Adjust. References `AdjustSdk` — compiles only with the Adjust SDK installed. This is a
**selective** backend: Adjust tracks a curated set of milestone events keyed by their Adjust event
token, not every analytics event, so you pass the name→token map at construction. Events without a
mapped token are ignored; only ad revenue is always forwarded.

```csharp
var tokens = new Dictionary<string, string>
{
    { "level_start", "abc123" },
    { "tutorial_complete", "def456" },
};
var adjust = new AdjustAnalyticsBackend(tokens, purchaseEventToken: "ghi789");
var analytics = new AnalyticsService(new IAnalyticsBackend[] { adjust });
```

- `InitializeAsync` is a no-op — initialize Adjust centrally via its own config.
- `SetUserProperty`/`SetUserId` map to Adjust global callback parameters (`user_id` for the id) —
  Adjust has no first-class user properties.

## AnalyticsPurchaseReporter (`IPurchaseReporter`)

Bridges `com.tk.iap`'s `IPurchaseReporter` into analytics — a confirmed purchase becomes a
`LogPurchase` that fans out to every backend. References `TK.IAP` + `TK.Analytics`.

```csharp
var iapOptions = new IapOptions { Reporter = new AnalyticsPurchaseReporter(analytics) };
```

**Important:** `OnPurchaseConfirmed` is **at-least-once** (crash-recovery redelivery re-confirms
already-applied purchases) — dedupe downstream by `TransactionId` before counting a paid conversion.

## AnalyticsAdRevenueReporter (`IAdRevenueReporter`)

Bridges `com.tk.ads`'s `IAdRevenueReporter` into analytics — each paid impression becomes a
`LogAdRevenue` that fans out to every backend. References `TK.Ads` + `TK.Analytics`.

```csharp
var adsOptions = new AdsOptions { RevenueReporter = new AnalyticsAdRevenueReporter(analytics) };
```

> This package has **no dependency** on `com.tk.iap` or `com.tk.ads` — these bridges are samples you
> copy into a project that has all three. They're the "route monetization through one analytics
> pipeline" alternative to iap/ads' own direct-to-Firebase reporter samples (`FirebaseReporterExample`
> / `FirebaseAdRevenueReporterExample`): use those to report straight to Firebase, or use these to fan
> the same events out to every analytics backend at once. Pick one path per producer, not both.
