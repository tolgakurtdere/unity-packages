# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-05

### Added

- **Service**: `RemoteConfigService` façade — backend-agnostic lifecycle (init → fetch → activate),
  safety gates (`IsSafeToRead`/`IsReady`), events (`OnReady` latch, `OnChanged`), `RefreshAsync`, and
  raw typed reads (`GetInt/GetLong/GetDouble/GetFloat/GetBool/GetString`). All reads fall back to the
  declared default before init, on a missing key, or on a failed fetch, and never throw.
- **Params**: `ConfigParam<T>` — strongly-typed, declared-once parameters via the service factories
  (`Int/Long/Double/Float/Bool/String`), with per-platform key/default overloads, implicit conversion
  to `T`, and `Key`/`Default`/`Value`.
- **Backend seam**: `IRemoteConfigBackend` — the single interface a backend implements. The package
  ships no backend; a Firebase adapter is a sample and tests inject a fake.
- **Editor overrides**: `RemoteConfigDebug` session store + `ConfigParam<T>.SetDebugOverride`/
  `ClearDebugOverride`/`HasDebugOverride`, guarded by `#if UNITY_EDITOR || TEST_MODE` (no-op in
  release). Overrides win over backend value and default on both the typed and raw read paths.
- **Parsing**: `RemoteConfigService.GetObject<T>` (Newtonsoft — grouped-per-domain JSON, dictionaries
  / nested / optional fields) and `RemoteConfigParsing.ParseIntList`/`ParseStringList` CSV helpers,
  including fluent `ConfigParam<string>` extensions.
- **Samples**: `FirebaseBackend` (`FirebaseRemoteConfigBackend` — an `IRemoteConfigBackend` adapter
  for Firebase Remote Config) and `IntegrationExamples` (`RcAdsPacingResolver`/`RcIapAmountResolver`
  resolver bridges to the TK Ads/IAP seams, a grouped-per-domain `DomainConfigExample`, and a
  `RemoteConfigDebugMenuExample` QA-override wiring).
- Package README and this changelog.
