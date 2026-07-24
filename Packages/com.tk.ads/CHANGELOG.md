# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-07-24

Consumer-feedback minor from the first real game integration (game-shikaku): the integration itself
was clean (pacing, reward latching, mute push/pop all behaved), but the game had to write its own
editor/test-build fake gateway — a thing every consumer would otherwise rewrite.

### Added

- **`FakeAdsGateway` (runtime)** — the official no-network `IAdsGateway` for editor runs, test
  builds, and demos; wire it as the service's gateway (e.g. behind
  `#if UNITY_EDITOR || TK_TEST_BUILD`) and the full ads flow runs without the MAX SDK or any
  scoped-registry setup. Auto mode (default): loads fill immediately and every show resolves
  itself synchronously (Displayed → RewardReceived → Hidden) for unattended end-to-end runs.
  Manual mode (`AutoResolveShows = false`): shows stop at Displayed and wait for
  `CompleteRewarded` / `CancelRewarded` / `CloseInterstitial` / `FailInterstitial` /
  `FailRewarded`. Knobs: `FailInit`, per-format `*Fill` toggles (no-fill paths),
  `ConsentDialogResult`, `ClickBanner()`, and `RaiseRevenue(AdRevenueInfo)` for exercising
  revenue reporters. All events fire synchronously on the caller's thread.

### Changed

- The AdsDemo sample now runs on `FakeAdsGateway` in manual mode and its in-sample
  `DemoAdsGateway` was deleted — the sample demonstrates exactly the class consumers ship with.

### Internal

- The package tests' own recording double was renamed `FakeAdsGateway` → `RecordingAdsGateway`
  (test assembly only, no public impact) so the official runtime name is unambiguous.

## [0.1.2] - 2026-07-05

### Changed

- Bumped AppLovin MAX to 8.6.4 (API re-verified against the new SDK source — no drift).
- Declared `com.unity.nuget.newtonsoft-json` 3.2.2 as a direct dependency (the IntegrationExamples
  sample now uses it).
- **Samples**: `IntegrationExamples`' `LevelLadderPacingResolverExample` now parses its remote-config
  JSON with Newtonsoft (`JsonConvert`) instead of `JsonUtility`.

## [0.1.1] - 2026-07-04

### Fixed

- **Banner**: `DestroyBanner()` is now reversible — a later `ShowBanner()` re-creates the banner and
  auto-shows it once it loads, instead of silently staying gone.
- **Fullscreen ads**: hardened interstitial/rewarded task completion against re-entrancy — the
  completion source is now cleared before the task is completed, so a continuation that immediately
  requests another ad of the same format can no longer hang.
- **Consent**: `ShowConsentDialogAsync` now treats MAX's "form not required" (consent already
  resolved) outcome as success rather than failure.

## [0.1.0] - 2026-07-04

### Added

- **Service**: `AdsService` facade — single-flight init lifecycle, intent-based banner visibility,
  close-anchored interstitial pacing with post-rewarded cooldown, reward-latch semantics, per-format
  exponential retry backoff (cap 64s, canceled on destroy/teardown), and balanced audio muting around
  fullscreen ads.
- **Settings**: `AdsSettings` `ScriptableObject` — per-platform (Android/iOS) ad unit ids for
  banner/interstitial/rewarded, banner position/background color, default pacing seconds, with editor
  validation warning on empty ad unit ids.
- **Options**: `AdsOptions` — code-side composition (gateway override, revenue reporter, pacing
  resolver, policy delegates, audio mute setter, test clock/retry-scale seams).
- **Policy** (all independently unit-tested): `InterstitialPacer` (min-interval + post-rewarded
  cooldown, values resolved at check time), `BannerIntent` (single source of truth for banner
  visibility, surviving load latency), `LoadRetryPolicy` (per-format exponential backoff).
- **Seams**: `IAdsGateway` (ad-network seam), `IAdRevenueReporter` (analytics/backend revenue
  reporting), `IAdsPacingResolver` (remote-config overrides for pacing), both optional via
  `AdsOptions`.
- **Gateway**: `MaxAdsGateway`, the AppLovin MAX 8.6.2-backed implementation — every SDK callback
  marshaled to the Unity main thread via `MainThreadDispatcher`; `ShowConsentDialogAsync` wraps MAX's
  CMP re-prompt API for existing users.
- **Samples**: `AdsDemo` (editor-runnable demo on an in-sample fake gateway with `[ContextMenu]`
  triggers for every ad format and outcome) and `IntegrationExamples`
  (`IAdRevenueReporter`/`IAdsPacingResolver` reference implementations: a Firebase `ad_impression`
  reporter and a level-ladder pacing resolver).
- Package README and this changelog.
