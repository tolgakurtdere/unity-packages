# Integration Examples

Reference implementations for `com.tk.iap`'s two analytics/backend seams. Both compile standalone
(no third-party SDK required) and are meant to be copied into your project and adapted, not used
as-is.

## FirebaseReporterExample (`IPurchaseReporter`)

Reports confirmed/failed purchases to Firebase Analytics. The actual `FirebaseAnalytics.LogEvent`
calls are commented out so this compiles without the Firebase SDK installed — uncomment them (and
add the Firebase Analytics package) to wire it up for real.

Register it in `IapOptions`:

```csharp
var options = new IapOptions { Reporter = new FirebaseReporterExample() };
var iap = new IapService(catalog, new PlayerPrefsJsonSaveSystem(), options);
```

**Important:** `IPurchaseReporter.OnPurchaseConfirmed` is an **at-least-once** contract — it can
fire again for a transaction already reported (crash-recovery redelivery re-confirms already-applied
purchases). Dedupe downstream using `IapPurchaseInfo.TransactionId` before writing to your backend
or sending a paid-conversion analytics event twice.

## RemoteConfigAmountResolverExample (`IIapAmountResolver`)

Overrides catalog item amounts from a JSON string (e.g. delivered by Firebase Remote Config, Unity
Remote Config, or your own backend), using `JsonUtility` — no Newtonsoft dependency.

```csharp
var resolver = new RemoteConfigAmountResolverExample();
resolver.SetConfigJson(remoteConfigJsonString); // call again whenever the config re-fetches

var options = new IapOptions { AmountResolver = resolver };
var iap = new IapService(catalog, new PlayerPrefsJsonSaveSystem(), options);
```

Expected JSON shape:

```json
{
  "overrides": [
    { "productId": "pack1", "itemType": "coins", "amount": 500 }
  ]
}
```

Behavior:

- Re-parses only when the JSON string actually changes (`SetConfigJson` no-ops on an identical
  string), so calling it on every remote-config fetch is cheap.
- A non-positive (`<= 0`) remote amount is treated as absent — `Resolve()` falls back to the
  catalog's own default amount instead of ever granting zero or negative items.
- Unknown `productId:itemType` pairs simply fall back to the catalog default (the resolver only
  needs entries for the products you actually want to override).
