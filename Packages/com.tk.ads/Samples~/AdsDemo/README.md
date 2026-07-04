# Ads Demo

Editor-runnable demo of the full `AdsService` init → show → outcome flow, driven entirely from
`[ContextMenu]` entries. Runs against `DemoAdsGateway`, a tiny in-sample fake `IAdsGateway` — **not**
real AppLovin MAX — because the Unity Editor cannot display real MAX ads (no fill, no native views)
and this sample can't reference the package's own test assembly (`TK.Ads.Tests` ships inside the
package, outside `Samples~`'s asmdef graph).

## Why a demo gateway instead of MaxAdsGateway

If `AdsDemo` constructed `AdsService` without an `AdsOptions.Gateway` override, it would default to
the real `MaxAdsGateway` and call into AppLovin MAX — which requires a device, real ad unit ids from
the AppLovin dashboard, and network fill, none of which are available for a quick in-Editor look at
the API. `DemoAdsGateway` auto-succeeds everything a real network would otherwise gate on:

- `InitializeAsync()` raises `Initialized` immediately.
- Every `Load*` call raises its `Loaded` event immediately (banner, interstitial, and rewarded are
  all "ready" the instant the service asks for them).
- Every `Show*` call raises `Displayed`, then waits — the ad stays "up" until you tell the gateway how
  it resolved, via the methods below (wired to the component's `[ContextMenu]` entries).

## Running it

This sample ships as source only — there's no `.unity` scene file (a scene needs Editor-generated
GUIDs, which don't survive being authored outside the Editor). To try it:

1. Import this sample (Package Manager → **TK Ads** → Samples → **Ads Demo**).
2. Create a new empty scene.
3. Add an empty GameObject and attach the `AdsDemo` component to it.
4. Press Play. Watch the Console for `[AdsDemo]` logs — you should see `Calling InitializeAsync...`
   immediately followed by `Initialized.` and `Init finished with state: Initialized` (the demo
   gateway has no latency).

Then, right-click the component's header in the Inspector (or use its "⋮" context menu) to reach the
demo actions:

| Menu entry | What it does |
| --- | --- |
| Show Banner / Hide Banner / Destroy Banner | Calls the matching `AdsService` method. The demo banner "loads" instantly, so `Show Banner` shows immediately if called after init. |
| Show Interstitial | Calls `ShowInterstitialAsync("demo_button")` and logs the awaited `bool` result once the ad resolves. |
| Simulate: Close Interstitial | Call this **while** an interstitial is up (after "Show Interstitial", before the awaited call returns) to simulate the player closing it — resolves the pending `ShowInterstitialAsync` to `true`. |
| Simulate: Fail Interstitial | Call this while an interstitial is up to simulate a display failure — resolves to `false` and triggers an automatic reload. |
| Show Rewarded | Calls `ShowRewardedAsync("demo_button")` and logs the awaited `RewardedResult`. |
| Simulate: Complete Rewarded | Call this while a rewarded ad is up to simulate the player watching it fully — resolves to `RewardedResult.Rewarded`. |
| Simulate: Cancel Rewarded | Call this while a rewarded ad is up to simulate an early close — resolves to `RewardedResult.Cancelled`. |
| Show Consent Dialog | Calls `ShowConsentDialogAsync()` — the demo gateway always returns `true`. |

Because the demo gateway resolves loads synchronously, ads are ready right after `Initialized` fires
— there's no need to wait between "Show Interstitial" and the Simulate actions beyond letting the
`Displayed` log appear.

Revenue is logged too: `AdsDemo` wires a small `LoggingRevenueReporter` via
`AdsOptions.RevenueReporter`, though `DemoAdsGateway` never actually raises `RevenuePaid` (there's no
real ad network paying anything in the Editor) — see the `IntegrationExamples` sample's
`FirebaseAdRevenueReporterExample` for what a real revenue-reporting implementation looks like.

## Testing against real MAX ads

`DemoAdsGateway` is for API/flow familiarity only — it proves nothing about real ad delivery. To
verify actual AppLovin MAX behavior:

1. Follow the package README's **Install** and **MAX setup** sections (scoped registries, SDK key,
   real ad unit ids from the AppLovin dashboard).
2. Build to a real device — MAX cannot display ads in the Editor at all (this is a MAX SDK limitation,
   not something this package works around).
3. Use AppLovin's **Mediation Debugger** (`MaxSdk.ShowMediationDebugger()`, or the shake/menu gesture
   configured in the Integration Manager) on-device to test fill for each ad unit and each mediated
   network in isolation, without depending on live auction fill.
