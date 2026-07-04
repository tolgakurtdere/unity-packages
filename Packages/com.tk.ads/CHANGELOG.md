# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
