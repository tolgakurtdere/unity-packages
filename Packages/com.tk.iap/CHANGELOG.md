# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-07-05

### Changed

- Bumped Unity IAP (`com.unity.purchasing`) to 5.4.0 (v5 gateway API re-verified against the new SDK
  source — no drift; `StoreController.Connect()` still resolves on both success and exhausted-retry
  failure, and `ProcessPendingOrdersOnPurchasesFetched(true)` still re-raises `OnPurchasePending` for
  fetched pending orders with per-session dedupe for Google crash recovery).
- Declared `com.unity.nuget.newtonsoft-json` 3.2.2 explicitly (previously only transitive via
  `com.tk.core`), now that the `IntegrationExamples` sample uses it.
- `IntegrationExamples` `RemoteConfigAmountResolverExample` now parses with Newtonsoft
  (`JsonConvert.DeserializeObject`) instead of `JsonUtility`.

## [0.1.0] - 2026-07-04

### Added

- **Service**: `IapService` facade — store lifecycle (connect → fetch products with retry → fetch
  purchase history), the pending → apply → confirm contract, restore flows (Apple explicit-request /
  Google auto-restore), and idempotency via `AppliedPurchaseLedger`.
- **Catalog**: `IapCatalog` `ScriptableObject` — string-keyed entries (internal id, store id,
  product type, per-platform gating, item list), with editor validation for empty/duplicate ids,
  unsupported subscriptions, and entitlements bundled inside consumables.
- **Entitlements**: persistent boolean grants with a latch-style `Subscribe`/`Unsubscribe` API.
- **Gateway**: `IStoreGateway` seam + `UnityIapGateway`, the default Unity IAP v5-backed
  implementation.
- **Seams**: `IPurchaseReporter` (post-confirmation analytics/backend reporting) and
  `IIapAmountResolver` (read-time item amount overrides, e.g. remote config), both optional via
  `IapOptions`.
- **UI** (`TK.IAP.UI`): `IapPurchaseButton`, `IapButtonContentSetter`, `IapRestoreButton`,
  `HideWhenEntitled` — drop-in purchase/restore buttons and entitlement-aware visibility.
- **Samples**: `IapDemo` (runtime demo catalog + logging item handlers, editor fake-store
  purchases) and `IntegrationExamples` (`IPurchaseReporter`/`IIapAmountResolver` reference
  implementations).
- Package README and this changelog.
