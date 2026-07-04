# TK Ads

Ads framework wrapping AppLovin MAX mediation (`com.applovin.mediation.ads`): banner, interstitial,
and rewarded ads behind a testable policy layer — pacing, intent-based banner visibility, reward
latching, retry backoff — plus analytics and remote-config seams.

## What's inside

| Module | Location | What it gives you |
| --- | --- | --- |
| Service | `Runtime/AdsService.cs` | The main facade. Owns init lifecycle, banner visibility intent, interstitial pacing, rewarded latch semantics, per-format retry with exponential backoff, and balanced audio muting around fullscreen ads. |
| Settings | `Runtime/AdsSettings.cs` | `AdsSettings` (a `ScriptableObject`) — per-platform ad unit ids (Android/iOS × banner/interstitial/rewarded), banner position/background color, default pacing seconds. |
| Options | `Runtime/AdsOptions.cs` | `AdsOptions` — code-side composition: gateway override, revenue reporter, pacing resolver, policy delegates, audio mute setter, test clock/retry-scale seams. |
| Policy | `Runtime/Policy/` | `InterstitialPacer` (min-interval + post-rewarded cooldown), `BannerIntent` (single source of truth for banner visibility), `LoadRetryPolicy` (per-format exponential backoff, cap 64s) — all pure, all unit-tested in isolation. |
| Seams | `Runtime/Seams/` | `IAdsGateway` (ad-network seam — the package ships `MaxAdsGateway`; tests/demos inject fakes), `IAdRevenueReporter` (analytics), `IAdsPacingResolver` (remote-config overrides for pacing). |
| Gateway | `Runtime/Gateway/` | `MaxAdsGateway` — the real AppLovin MAX adapter, marshaling every SDK callback to the Unity main thread via `MainThreadDispatcher`. `MaxSdk`/`MaxSdkBase` types never leak outside this folder. |

## Install

This package needs **two** scoped registries — AppLovin MAX's own registry for the MAX SDK itself,
plus OpenUPM for a transitive dependency MAX pulls in (`com.google.external-dependency-manager`,
a.k.a. EDM4U) that AppLovin's registry does not serve.

**Step 1 — add both scoped registries** to your project's `Packages/manifest.json`:

```json
"scopedRegistries": [
  {
    "name": "AppLovin MAX Unity",
    "url": "https://unity.packages.applovin.com",
    "scopes": [
      "com.applovin.mediation.ads",
      "com.applovin.mediation.adapters",
      "com.applovin.mediation.dsp"
    ]
  },
  {
    "name": "OpenUPM",
    "url": "https://package.openupm.com",
    "scopes": [
      "com.google.external-dependency-manager"
    ]
  }
]
```

If your manifest already has a `scopedRegistries` array (e.g. from another package), **merge** these
two entries into it rather than replacing the array — Unity only reads one `scopedRegistries` key per
manifest.

**Step 2 — add this package** via Package Manager → **Add package from git URL**:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.ads
```

Pinned to a released version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.ads#com.tk.ads/0.1.0
```

Unity resolves `com.applovin.mediation.ads` (and its transitive EDM4U dependency) automatically from
the registries added in Step 1. To see this package's EditMode tests in your own project's Test
Runner, add `"testables": ["com.tk.ads"]` to your project's `Packages/manifest.json`.

## MAX setup

1. Create an app in the [AppLovin dashboard](https://dash.applovin.com/) and grab its **SDK key**.
2. Open **AppLovin Integration Manager** (Unity menu: `AppLovin` → `Integration Manager`, installed
   alongside the SDK) and paste the SDK key in — this is also where you enable/configure the **Terms
   & Privacy Policy Flow** (MAX's built-in consent flow wrapping Google UMP + iOS ATT). There is no
   runtime API for this in MAX 8.6.2 — it's editor-only configuration, not something this package's
   `AdsSettings` exposes fields for.
3. Create ad units for banner/interstitial/rewarded in the dashboard and paste their ids into an
   `AdsSettings` asset (see Quickstart below).
4. The Integration Manager also runs mediation network adapter installs (if you enable additional
   mediated networks beyond MAX's own bidding) and Android/iOS platform setup checks — follow its
   in-editor guidance for anything it flags as missing.
5. Real ads only display on-device — the Unity Editor cannot render MAX ads (no fill, no native
   views). Use AppLovin's **Mediation Debugger** (`MaxSdk.ShowMediationDebugger()`, or the
   shake/menu gesture configured in the Integration Manager) on a real build to verify fill per ad
   unit and per mediated network.

## Quickstart

1. Create a settings asset: **Assets → Create → TK → Ads Settings**, then fill in your ad unit ids
   (per platform, per format) in the Inspector.
2. Construct the service and subscribe to events before calling `InitializeAsync`:

   ```csharp
   var ads = new AdsService(settings, new AdsOptions
   {
       RevenueReporter = new FirebaseAdRevenueReporterExample(),   // see IntegrationExamples sample
       AudioMuteSetter = muted => AudioListener.pause = muted,

       // Optional: gate interstitials/banner behind a "remove ads" entitlement. Rewarded is
       // deliberately NEVER policy-gated — remove-ads must not remove the player's ability to
       // opt into a rewarded ad. If you use com.tk.iap (a separate, optional package — this
       // package has no dependency on it), the one-liner looks like:
       //   ShouldShowInterstitial = () => !iap.Entitlements.Has("remove_ads"),
       //   ShouldShowBanner       = () => !iap.Entitlements.Has("remove_ads"),
   });

   ads.Initialized += () => Debug.Log("Ads ready.");
   await ads.InitializeAsync();
   ```

3. Show ads:

   ```csharp
   ads.ShowBanner();                                    // intent-based — auto-shows once loaded
   var shown = await ads.ShowInterstitialAsync("level_complete");
   var result = await ads.ShowRewardedAsync("extra_life");  // RewardedResult.Rewarded/Cancelled/...
   ```

See the `AdsDemo` sample for a runnable end-to-end version of this flow (on a fake gateway, since MAX
can't show ads in the Editor).

## Pacing & remote config

Interstitial pacing is anchored to the **close** of the last interstitial (not show start), plus a
cooldown after each **completed** rewarded ad so a player who just watched a rewarded ad isn't
immediately hit with an interstitial. Both values resolve through `IAdsPacingResolver` **at check
time** — a live remote-config change applies to the very next pacing check, no restart needed — under
the well-known keys in `AdsPacingKeys` (also exposed as `AdsService.InterstitialIntervalKey` /
`CooldownAfterRewardedKey` public consts):

| Key | Default source | Meaning |
| --- | --- | --- |
| `AdsPacingKeys.InterstitialInterval` | `AdsSettings.interstitialMinIntervalSeconds` (60) | Minimum seconds between interstitial closes |
| `AdsPacingKeys.CooldownAfterRewarded` | `AdsSettings.cooldownAfterRewardedSeconds` (60) | Seconds an interstitial stays blocked after a rewarded completion |

Wire a resolver via `AdsOptions.PacingResolver`. See the `IntegrationExamples` sample's
`LevelLadderPacingResolverExample` for a level-tiered ladder resolver backed by remote-config JSON
(new players see interstitials less often; the interval ramps down toward a steady state as they
level up). Pacing applies to **interstitial only** — rewarded ads are never rate-limited by design
(they're opt-in by the player).

## Consent

MAX's Terms & Privacy Policy Flow (wrapping Google UMP + iOS ATT) is configured entirely in the
**AppLovin Integration Manager** (see MAX setup above) — it runs automatically during
`MaxSdk.InitializeSdk()` for a user who hasn't consented yet, with no runtime API to enable/configure
it from code in MAX 8.6.2.

Two supported consent patterns:

- **MAX's built-in flow (default):** just configure it in the Integration Manager and call
  `ads.InitializeAsync()` as normal — MAX handles first-time consent collection itself before
  finishing init. For a settings-screen "Privacy Options" re-prompt (letting a user revisit their
  choice later), call `await ads.ShowConsentDialogAsync()` — this wraps MAX's CMP re-prompt API and
  returns `false` when no supported CMP is present or the flow fails/is unavailable.
- **External CMP:** if your project already runs its own consent management (a different CMP SDK, or
  a fully custom flow), disable MAX's Terms & Privacy Policy Flow in the Integration Manager and call
  `ads.InitializeAsync()` only **after** your own consent flow completes. This package places no
  consent UI of its own and never delays or gates `InitializeAsync` on consent internally — the
  calling order is the entire contract.

## Testing

The package ships 48 EditMode tests (policy units in isolation, plus the full `AdsService` state
machine against a fake gateway) — add `"testables": ["com.tk.ads"]` to your project's manifest (see
Install above) to run them from your own Test Runner.

For manual/interactive testing, import the `AdsDemo` sample: it runs the complete init → show →
outcome loop against an in-sample fake gateway, since **MAX cannot display ads in the Unity Editor**
at all (no fill, no native views — this is a MAX SDK limitation the package can't route around). To
verify real ad delivery, build to a device and use AppLovin's **Mediation Debugger** — see MAX setup
above.

## Gotchas

- **Main-thread only.** `AdsService` and `MaxAdsGateway` are not thread-safe — call every method from
  Unity's main thread. `MaxAdsGateway` marshals every AppLovin SDK callback to the main thread
  internally (native callbacks can arrive on arbitrary threads), but that's defensive on the gateway
  side, not a general threading guarantee for the rest of the API.
- **One service per gateway.** To retry a `Failed` init, construct a **fresh** `AdsService` (and, if
  you passed a custom `AdsOptions.Gateway`, a fresh gateway too) — event subscriptions are never
  removed, so reusing either leaks handlers onto a dead instance.
- **Rewarded is never policy-gated, by design.** `ShouldLoadInterstitial`/`ShouldShowInterstitial`/
  `ShouldLoadBanner`/`ShouldShowBanner` can block those formats (e.g. for a "remove ads" purchase),
  but there is no `ShouldShowRewarded` delegate — a rewarded ad is something the player actively opts
  into for a benefit, and removing that option isn't what "remove ads" purchases are for.
- **Init may sit in `Initializing` indefinitely with no network.** Like most mediation SDKs, MAX's
  init can hang or take a long time to resolve without connectivity, and there's no error callback
  guaranteeing it eventually reports failure (see `MaxAdsGateway`'s class doc: MAX 8.6.2 has no
  init-failure callback at all — only a synchronous throw out of `InitializeSdk()` itself surfaces as
  `InitFailed`). Never block the rest of the game on ads init — gate ad-showing UI on
  `AdsService.State` instead of awaiting `InitializeAsync` on a critical path.
- **Double-show guard, no queuing.** Calling `ShowInterstitialAsync`/`ShowRewardedAsync` while a
  fullscreen ad is already displaying returns `false`/`RewardedResult.NotReady` immediately — it does
  not queue the request for after the current ad closes.

## v2 reserves (no API breaks planned)

- **AdMob backfill** — a `CompositeAdsGateway(primary, fallback)` implementing `IAdsGateway` for a
  per-show fallback chain (MAX show-fail → AdMob) as a non-mediation backfill layer, with both
  gateways feeding `RevenuePaid` so backfill revenue isn't dropped from reporting.
- **App Open ads (v1.1)** — MAX's app-open ad format, with safe-exit/fast-return heuristics so
  returning from your own fullscreen ad doesn't immediately trigger an app-open ad.
