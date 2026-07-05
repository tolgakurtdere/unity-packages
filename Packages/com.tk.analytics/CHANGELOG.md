# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-05

### Added

- **Service**: `AnalyticsService` — multi-backend façade that fans every operation out to all backends,
  gates dispatch on enabled/consent/started, and buffers whatever cannot dispatch yet (in order, with
  parameters preserved). Each backend call is isolated in try/catch so one throwing backend never
  blocks the others.
- **Interface**: `IAnalytics` — `LogEvent`, `LogPurchase`, `LogAdRevenue`, `SetUserProperty`,
  `SetUserId`, `SetConsent`, `Flush`, `StartAsync`, and an `IsEnabled` runtime kill-switch.
- **Static façade**: `Analytics` — ambient access point (`SetInstance`/`ClearInstance` + the log verbs)
  for logging from anywhere; null-safe (no-ops with a one-time editor warning) until an instance is set.
- **Values**: `AnalyticsParam` (allocation-free typed String/Long/Double/Bool parameter),
  `AnalyticsEvent`, and neutral, package-owned `AnalyticsPurchase` / `AnalyticsAdRevenue` revenue
  records (no vendor or IAP/Ads types in the public API).
- **Backend seam**: `IAnalyticsBackend` — the single interface a backend implements (must not throw).
  The package ships one built-in backend, `ConsoleAnalyticsBackend`, which writes every call to the
  Unity console; Firebase and Adjust are samples.
- **Samples**: `AnalyticsDemo` (editor-runnable demo on the Console backend with ContextMenu triggers
  and the consent/buffer flow) and `IntegrationExamples` (`FirebaseAnalyticsBackend` /
  `AdjustAnalyticsBackend` adapters plus `AnalyticsPurchaseReporter` / `AnalyticsAdRevenueReporter`
  bridges wiring the com.tk.iap / com.tk.ads reporter seams into analytics).
- Package README and this changelog.
