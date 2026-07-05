# com.tk.ads v0.1.0 — Design Spec

Status: APPROVED by Tolgahan 2026-07-04 (approach + all sections). Location note: specs/plans live in gitignored `.superpowers/` per repo convention (public repo stays clean); durable knowledge goes to project memory.

## Purpose

Reusable ads package for TK games: AppLovin MAX mediation with banner / interstitial / rewarded, designed so every new prototype integrates in minutes without touching package code. As critical as com.tk.iap; same engineering bar (seam-based architecture, deterministic EditMode tests, per-task review pipeline).

## Reference system (analyzed, read-only)

`g-brain_test_5/Assets/UnicoStudio/UnicoLibs/AdsManager/` (9 files, ~4350L) + consumers. Deep analysis: `.superpowers/sdd/ads-backend-analysis.md` (backends, defects, preserve-verbatim list). Controller-read: `AdManager.cs` (facade — backfill pattern, delegate composition), `MyAdManager.cs` (game-side wiring: GDPR-gated init, pacing ladder, RemoveAds gating, revenue events).

## Locked decisions

1. v1 scope = banner + interstitial + rewarded via AppLovin MAX only. App Open reserved for v1.1 (MAX app-open format). AdBlock detection: never. AdMob (non-mediation) backfill: v2, via CompositeAdsGateway.
2. Standard MAX only — no W3 manual waterfall, no Nefta (gateway seam keeps the door open).
3. Consent: MAX built-in CMP flow (wraps Google UMP + iOS ATT) as OPTIONAL config (privacy/terms URLs); external-CMP games disable it and call InitializeAsync after their own consent. Package never implements consent UI itself.
4. Architecture = Approach A: policy/state machine in package behind `IAdsGateway` seam (IAP twin); MAX behind thin `MaxAdsGateway`; `FakeAdsGateway` + injectable clock for tests.
5. NO dependency on com.tk.core or com.tk.iap — package is fully standalone (no persistence need in v1; RemoveAds via policy delegates). Sole dependency: `com.applovin.mediation.ads` (8.6.2 pin, verify latest 8.x at plan time) via AppLovin scoped registry (documented consumer step).
6. `MaxSdkBase`/AppLovin types NEVER leak into the public API (DTOs on the seam).
7. All gateway events marshaled to the main thread by the package (defensive; Unity 6 `Awaitable.MainThreadAsync()`), regardless of what MAX promises.

## Package layout

```
Packages/com.tk.ads/
  package.json                    # com.tk.ads 0.1.0, unity 6000.0, dep: com.applovin.mediation.ads
  Runtime/
    TK.Ads.asmdef                 # refs: [MAX asmdef — name VERIFIED AT PLAN TIME from installed package]
    AdsService.cs                 # facade + policy state machine (the flagship)
    AdsInitState.cs               # NotInitialized/Initializing/Initialized/Failed
    RewardedResult.cs             # Rewarded/Cancelled/NotReady/FailedToShow/NotInitialized
    AdsSettings.cs                # ScriptableObject (CreateAssetMenu "TK/Ads Settings")
    AdsOptions.cs                 # code-side options (delegates, seams, overrides)
    Policy/
      InterstitialPacer.cs        # min-interval + post-rewarded cooldown, injectable clock
      BannerIntent.cs             # intent-based banner visibility state (single source of truth)
      LoadRetryPolicy.cs          # per-format exponential backoff (cap 64s), cancelable
    Seams/
      IAdsGateway.cs              # store-agnostic gateway contract + DTOs
      IAdRevenueReporter.cs       # AdRevenueInfo → analytics seam
      IAdsPacingResolver.cs       # int ResolveSeconds(string key, int defaultSeconds)
    Gateway/
      MaxAdsGateway.cs            # MAX adapter (MaxSdkCallbacks → seam events)
      MainThreadDispatcher.cs     # marshaling helper (internal)
  Tests/Editor/
    TK.Ads.Tests.asmdef
    FakeAdsGateway.cs             # scripting knobs + manual event delivery
    FakeClock.cs
    AdsServiceTests.cs / InterstitialPacerTests.cs / BannerIntentTests.cs / LoadRetryPolicyTests.cs
  Samples~/
    AdsDemo/                      # editor-runnable demo on FakeAdsGateway (MAX can't show ads in editor)
    IntegrationExamples/          # Firebase ad_impression reporter; level-ladder pacing resolver (JsonUtility)
  README.md / CHANGELOG.md
```

## Public API surface

```csharp
public sealed class AdsService
{
    public static AdsService Instance { get; }          // set in ctor; prefer injection
    public AdsInitState State { get; }
    public event Action Initialized; public event Action InitFailed;

    public AdsService(AdsSettings settings, AdsOptions options = null);
    public Task InitializeAsync();                       // consent contract: call after external CMP if consentFlowEnabled=false

    // Banner — intent-based
    public void ShowBanner(); public void HideBanner(); public void DestroyBanner();
    public bool IsBannerVisible { get; }
    public event Action BannerClicked;                   // v1.1 app-open safe-exit will need this

    // Interstitial
    public bool IsInterstitialReady { get; }             // pure readiness — policy NOT mixed in (fixes reference smell)
    public Task<bool> ShowInterstitialAsync(string placement = null);
    public event Action InterstitialClosed;

    // Rewarded
    public bool IsRewardedReady { get; }
    public event Action RewardedReadyChanged;            // for button states
    public Task<RewardedResult> ShowRewardedAsync(string placement = null);
}
```

`AdsSettings` (SO): per-platform ad unit ids (banner/interstitial/rewarded × Android/iOS), banner position enum + background color, `interstitialMinIntervalSeconds` (default 60), `cooldownAfterRewardedSeconds` (default 60). NO consent fields (see Consent — MAX owns that config in its Integration Manager). OnValidate warns on empty ad units.

`AdsOptions`: `IAdsGateway Gateway` (test/composite override), `IAdRevenueReporter RevenueReporter`, `IAdsPacingResolver PacingResolver`, policy delegates `Func<bool> ShouldLoadBanner / ShouldShowBanner / ShouldLoadInterstitial / ShouldShowInterstitial` (null = allowed), `Action<bool> AudioMuteSetter`, `Func<float> Clock` (tests). RemoveAds wiring is the game's one-liner: `ShouldShowInterstitial = () => !iap.Entitlements.Has("remove_ads")` — rewarded is deliberately NEVER policy-gated (reference asymmetry preserved).

## Behavioral contracts (reference-verbatim knowledge, test-pinned)

- **Rewarded grant semantics:** reward latches on the gateway's `RewardReceived` (fires mid-display); on `Hidden`, resolve `Rewarded` if latched else `Cancelled`. Early close = no reward; reward-then-slow-close still rewards. `NotReady` when no loaded ad; `FailedToShow` on display failure (triggers reload).
- **Interstitial pacing:** window measured from the CLOSE of the last interstitial (not show start); post-rewarded cooldown suppresses interstitials for `cooldownAfterRewardedSeconds` after a REWARDED completion. Both values resolved through PacingResolver at check time (remote-config-ready, late-arriving values picked up) under the well-known keys `AdsService.InterstitialIntervalKey = "interstitial_interval"` and `AdsService.CooldownAfterRewardedKey = "cooldown_after_rewarded"` (public consts). Pacing applies to interstitial only.
- **Banner intent model:** `ShowBanner()` records intent; if the banner loads later, it auto-shows on load; `HideBanner()`/`DestroyBanner()` clear intent. Visibility state lives ONLY in BannerIntent (reference scattered it across facade+backends).
- **Fullscreen audio mute:** AudioMuteSetter(true) before interstitial/rewarded display, (false) after hidden/failed — always balanced (try/finally semantics).
- **Double-show guard:** a second Show*Async while a fullscreen ad is displaying returns false/NotReady immediately (no queuing).
- **Load/retry:** each format preloads at init (subject to ShouldLoad* delegates) and reloads after hide/consume/fail with exponential backoff `min(2^attempt, 64)s` on the injected clock; retries canceled on DestroyBanner/service teardown (fixes reference leak). Attempt counter resets on success.
- **Init:** single-flight InitializeAsync (IAP pattern); gateway init failure → Failed + InitFailed; late gateway events ignored unless Initializing/Initialized as appropriate (IAP hardening lessons applied symmetrically from day one).
- **Revenue:** gateway `RevenuePaid(AdRevenueInfo { Format, NetworkName, AdUnitId, Revenue(double), Currency("USD"), Placement })` → reporter (exceptions swallowed+logged, never break ad flow). Placement = last show call's placement for that format.

## Threading

MaxAdsGateway forwards every MAX callback through MainThreadDispatcher (no-op if already on main). AdsService assumes main-thread delivery from ANY gateway; FakeAdsGateway delivers synchronously on main (tests stay deterministic).

## Consent (VERIFIED against installed 8.6.2 — corrected from the draft)

MAX's Terms & Privacy Policy Flow (UMP + iOS ATT) is configured in AppLovin's OWN Integration Manager editor settings (AppLovinInternalSettings) — there is NO runtime enable/URL API in 8.6.2. Therefore: `AdsSettings` carries NO consent fields (they would be dead config). Package surface: (a) README documents enabling the flow + URLs in AppLovin Integration Manager (AppLovin's standard place — MAX then runs UMP/ATT automatically during SDK init); (b) `AdsService.ShowConsentDialogAsync()` wraps the verified runtime API `MaxSdk.CmpService.ShowCmpForExistingUser` (+ `HasSupportedCmp` guard) for settings-screen "Privacy Options" re-prompts, returns false when unsupported; (c) external-CMP contract unchanged — call `InitializeAsync` after your own consent flow. Decision unchanged (MAX built-in CMP, optional); only the configuration location moved to where AppLovin actually put it.

## Testing

EditMode suite on FakeAdsGateway + FakeClock: init flows (happy/fail/late events), pacing windows incl. cooldown interplay, banner intent lifecycle (show-before-load, load-after-hide, destroy cancels retry), rewarded latch matrix (reward→close, close-only, fail-to-show), double-show guards, backoff timing/cancelation, reporter exception tolerance, policy delegate gating. Target ≈30+ tests. Editor demo (Samples~) runs the full loop on FakeAdsGateway; device verification via MAX Mediation Debugger (README). Harness gate identical to IAP (scoped registry must be added to harness manifest — plan task 1 proves headless registry resolution).

## v2+ reserves (no API breaks)

- **AdMob backfill (v2):** `CompositeAdsGateway(primary, fallback)` implementing IAdsGateway — per-show fallback chain from the reference (MAX show-fail → AdMob; banner contention MAX-wins) with the reference's dropped-AdMob-revenue defect fixed by construction (both gateways feed RevenuePaid).
- **App Open (v1.1):** MAX app-open format; safe-exit/fast-return/watching-ads heuristics documented in ads-backend-analysis.md.
- W3/Nefta-style optimizations as gateway variants if ever wanted.

## Plan-time verification items (NOT open design questions)

1. MAX UPM asmdef name (from installed package in harness).
2. Exact MaxSdk consent-flow API names on 8.6.2; latest 8.x pin decision.
3. Headless scoped-registry resolution in the harness (first plan task proves it).
4. MAX banner "load" event semantics for the intent model (OnAdLoadedEvent on banners — confirm callback names).
