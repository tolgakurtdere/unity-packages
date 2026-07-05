# Integration Examples

Reference implementations that connect `com.tk.remoteconfig` to the other TK packages and show the
recommended config-modeling patterns. Copy the ones you need into your project and adapt them.

`com.tk.remoteconfig` has **no dependency** on `com.tk.ads` or `com.tk.iap` — the resolver bridges
below are samples you copy, and they only compile when the referenced package is present.

## RcAdsPacingResolver (`IAdsPacingResolver`)

Feeds `com.tk.ads`'s pacing seam from a live `RemoteConfigService`, so ad pacing values read from
remote config at check time.

```csharp
var ads = new AdsService(settings, new AdsOptions
{
    PacingResolver = new RcAdsPacingResolver(RemoteConfigService.Instance)
});
```

The pacing keys come from the ads package (e.g. `AdsPacingKeys.InterstitialInterval`); the resolver
just forwards them to `rc.GetInt`, which returns the built-in default before init or for an unknown
key.

## RcIapAmountResolver (`IIapAmountResolver`)

Feeds `com.tk.iap`'s amount seam from a live `RemoteConfigService`, so catalog item amounts read from
remote config both when a purchase is applied and when the UI shows a wallet amount.

```csharp
var iap = new IapService(catalog, new PlayerPrefsJsonSaveSystem(), new IapOptions
{
    AmountResolver = new RcIapAmountResolver(RemoteConfigService.Instance)
});
```

The key convention (`"{productId}_{itemType}_amount"`) is a **game choice** — adapt it to whatever
you name keys in your console. A missing remote key falls back to the catalog default, so it can
never zero out an amount.

## DomainConfigExample — grouped-per-domain JSON

The **recommended** way to model config: one remote-config key per domain (ads / iap / economy),
each holding a single JSON object deserialized to a typed class via `GetObject<T>` (Newtonsoft).

```csharp
class EconomyConfig { public int StartingCoins = 100; public Dictionary<string,int> ItemPrices = new(); }

var economy = rc.GetObject("economy_config", new EconomyConfig());
// console value: { "StartingCoins": 250, "ItemPrices": { "sword": 500, "shield": 300 } }
```

Prefer this over dozens of scalar keys or CSV lists: one console entry per feature area, add a field
without touching call sites, and Newtonsoft handles dictionaries / nested objects / optional fields
that `JsonUtility` cannot. Use the scalar `rc.Int`/`rc.Bool`/... factories for individual flags and
`GetObject<T>` for a grouped feature config.

## RemoteConfigDebugMenuExample — QA overrides

A `MonoBehaviour` with `[ContextMenu]` items that call `param.SetDebugOverride(...)` /
`RemoteConfigDebug.ClearAll()` — the pattern for wiring remote-config overrides into your own debug /
QA menu (SROptions, a cheat panel, etc.).

```csharp
var interval = rc.Int("interstitial_interval", 30);
interval.SetDebugOverride(5); // QA forces 5s — wins over backend AND default
interval.ClearDebugOverride(); // back to backend/default
```

Overrides are **editor / TEST_MODE only** (`RemoteConfigDebug` is a no-op in release), so this wiring
never ships live. An override wins on both the typed `param.Value` path and the raw `rc.GetInt` path.
