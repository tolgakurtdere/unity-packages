# TK IAP

In-app purchasing framework wrapping Unity IAP v5 (`com.unity.purchasing`): a string-keyed product
catalog, item-handler composition, entitlements, idempotent purchase application, and drop-in UI.

## What's inside

| Module | Asmdef | What it gives you |
| --- | --- | --- |
| Service | `TK.IAP` | `IapService` — the main facade. Owns the store lifecycle (connect → fetch products with retry → fetch purchase history), the pending → apply → confirm contract, restore flows, and idempotency via `AppliedPurchaseLedger`. |
| Catalog | `TK.IAP` | `IapCatalog` (a `ScriptableObject`) — internal ids ↔ store ids, product type, per-item payloads (`type`/`amount`/`value`). Editor validation warns about empty/duplicate ids, subscriptions (unsupported in v1), and entitlements bundled inside consumables. |
| Entitlements | `TK.IAP` | `Entitlements` — persistent boolean grants (`"remove_ads"`, `"vip"`, ...) with a latch-style `Subscribe` API so other systems (e.g. an Ads package) can react without polling. |
| Gateway | `TK.IAP` | `IStoreGateway` seam + `UnityIapGateway`, the default v5-backed implementation. Swappable for tests or special builds via `IapOptions.Gateway`. |
| UI | `TK.IAP.UI` | `IapPurchaseButton`, `IapButtonContentSetter`, `IapRestoreButton`, `HideWhenEntitled` — drop-in `MonoBehaviour`s for a purchase button with live pricing/discount display, wallet-amount labels, a restore button, and entitlement-gated visibility. |

## Install

**Requires `com.tk.core`** (used for `ISaveSystem`) — install it first if it isn't already in your
project:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core
```

Then add this package via Package Manager → **Add package from git URL**:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.iap
```

Pinned to a released version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.iap#com.tk.iap/0.1.1
```

To see this package's EditMode tests in your own project's Test Runner, add
`"testables": ["com.tk.iap"]` to your project's `Packages/manifest.json` (alongside `com.tk.core`'s
entry, if you added that too).

## Quickstart

1. Create a catalog asset: **Assets → Create → TK → IAP Catalog**, then fill in its entries (id,
   store id, product type, items) in the Inspector — or build one at runtime via `SetEntries` (see
   the `IapDemo` sample).
2. Construct the service and register item handlers **before** calling `InitializeAsync` (init may
   re-deliver pending orders from a previous crashed session):

   ```csharp
   var iap = new IapService(catalog, new PlayerPrefsJsonSaveSystem());
   iap.RegisterItemHandler("coins", (amount, ctx) => wallet.Add(amount));
   await iap.InitializeAsync();
   ```

3. Wire up a purchase button: add a `Button` + `IapPurchaseButton` to a UI GameObject, assign its
   `productId` and price `TextMeshProUGUI` in the Inspector — it resolves the localized price once
   `IapService.Instance` finishes initializing, and calls `Purchase(productId)` on click.

## Core contract

Every purchase goes through **pending → apply → confirm**: the store hands `IapService` a
`PendingPurchase`, the service applies its items to the game (via registered handlers and/or
entitlement grants), and **only then** confirms the purchase back to the store. If applying fails
(e.g. no handler registered for an item type, or a handler throws), the purchase is left
unconfirmed — the store re-delivers it next session instead of silently losing it.

The `AppliedPurchaseLedger` tracks which transactions/products were already applied, so
re-delivered orders never double-grant. **Item handlers do not need to be idempotent** — the
package guarantees a given transaction is only ever applied once.

## Entitlements

Non-consumable products double as entitlements keyed by their **catalog id**: purchasing (or
restoring) `remove_ads` calls `Entitlements.Grant("remove_ads")` automatically — no `entitlement`
item required. Other packages (e.g. an Ads package) react via the latch-style `Subscribe` API,
which fires immediately if already granted or once when it becomes granted:

```csharp
IapService.Instance.Entitlements.Subscribe("remove_ads", () => adsController.Disable());
```

This package never calls into other packages — it only exposes `Entitlements` for them to consume.

## Seams

Three constructor-injected seams (all optional, via `IapOptions`) let you extend behavior without
touching package code:

- **`IStoreGateway`** — swap the store backend. Tests inject a fake; `UnityIapGateway` (Unity IAP
  v5) is the default when `IapOptions.Gateway` is left `null`.
- **`IPurchaseReporter`** — analytics/backend reporting, called after a purchase is applied and
  confirmed. See the `IntegrationExamples` sample's `FirebaseReporterExample`.
- **`IIapAmountResolver`** — override item amounts at read time (e.g. from remote config), consulted
  both when applying a purchase and when the UI displays a wallet amount. See
  `RemoteConfigAmountResolverExample`.

```csharp
var options = new IapOptions
{
    Reporter = new FirebaseReporterExample(),
    AmountResolver = new RemoteConfigAmountResolverExample()
};
var iap = new IapService(catalog, new PlayerPrefsJsonSaveSystem(), options);
```

## Testing

The package ships 47 EditMode tests (service state machine, catalog, entitlements, ledger, fake
gateway) — add `"testables": ["com.tk.iap"]` to your project's manifest (see Install above) to run
them from your own Test Runner. For manual/interactive testing, import the `IapDemo` sample: in the
Editor, Unity IAP runs against Unity's built-in fake store, so purchases auto-succeed with no store
configuration needed.

## v1 scope

No subscriptions yet — `IapCatalog`'s editor validation warns if you set an entry's product type to
`Subscription`. Planned for a later version.

## Store setup checklist

Before shipping, define matching products in each store console:

- **Google Play Console**: create the product with the exact `storeId` used in your catalog entry.
- **App Store Connect**: same — the `storeId` must match your catalog exactly on both stores (this
  package assumes a shared store id per catalog entry, gated by `platforms` when they diverge).
- Double-check `productType` matches what you configured server-side (Consumable vs. NonConsumable)
  — a mismatch causes store-side purchase/restore failures that are hard to diagnose from logs alone.

## Gotchas

- **Register item handlers before `InitializeAsync`.** Init may re-deliver pending orders from a
  crashed previous session; a handler registered after init started can miss them.
- **Main-thread only.** `IapService` and `UnityIapGateway` are not thread-safe — call every method
  from Unity's main thread.
- **One service per gateway.** To retry a `Failed` init, construct a **fresh** `IapService` AND a
  **fresh** gateway — event subscriptions are never removed, so reusing either leaks handlers onto
  a dead instance.
- **Permanent entitlements belong in NonConsumable products.** An `entitlement` item bundled inside
  a *consumable* does NOT survive reinstall — store purchase history only restores NonConsumables,
  so the entitlement grant is lost on a fresh install even if the coins/consumable side gets
  re-granted on repurchase. `IapCatalog`'s editor validation warns about this case.
- **Android init can hang if the device has no network/Play services.** Unity IAP v5 retries the
  store connection forever in that situation, so `InitializeAsync` may sit in `Initializing`
  indefinitely. Never block the rest of the game on IAP init — gate purchase UI on
  `IapService.Instance.State` instead of awaiting init on a critical path.
- **Give restore flows a UI-side timeout.** A network failure completes `RestorePurchases` via
  `RestoreCompleted(false)`, but a dead store connection can leave the request hanging with no event
  at all. Mirror any loading indicator around `RestorePurchases` with your own timeout (~15s is a
  reasonable cap) so the UI can recover instead of spinning forever.
- **The idempotency ledger assumes synchronous saves.** `AppliedPurchaseLedger` and `Entitlements`
  call `ISaveSystem.Save` expecting the write to be durable by the time the call returns (the shipped
  `PlayerPrefsJsonSaveSystem` is synchronous); a batched or async `ISaveSystem` implementation widens
  the window in which a crash between apply and persist can cause a re-delivered purchase to be
  applied twice.
