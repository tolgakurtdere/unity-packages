# TK Remote Config

Backend-agnostic remote-config façade: declare strongly-typed config parameters once, read them
anywhere with defaults + safety gates + editor overrides + runtime refresh — and feed the resolver
seams the shipped IAP/Ads packages already expose, from any backend.

The package ships **no** backend. A Firebase adapter and the IAP/Ads resolver bridges ship as
**Samples**; tests inject a fake. Swap in any backend by implementing one small interface.

## What's inside

| Module | Type | What it gives you |
| --- | --- | --- |
| Service | `RemoteConfigService` | The façade. Owns the lifecycle (init → fetch → activate), safety gates (`IsSafeToRead`/`IsReady`), events (`OnReady` latch, `OnChanged`), raw typed reads (`GetInt/GetLong/GetDouble/GetFloat/GetBool/GetString`), and `GetObject<T>` for JSON. |
| Params | `ConfigParam<T>` | A strongly-typed, declared-once parameter created via the service factories (`rc.Int/Long/Double/Float/Bool/String`, plus per-platform key overloads). Reads through the service; converts implicitly to `T`. |
| Backend seam | `IRemoteConfigBackend` | The one interface a backend implements (`InitializeAsync`, `FetchAndActivateAsync`, `TryGet*`). Firebase adapter is a Sample. |
| Editor overrides | `RemoteConfigDebug` + `ConfigParam<T>.SetDebugOverride` | Editor/`TEST_MODE`-only session overrides for QA; compiled out (no-op) in release. |
| Parsing | `RemoteConfigService.GetObject<T>` + `RemoteConfigParsing` | Newtonsoft `GetObject<T>` for grouped-per-domain JSON, plus CSV `ParseIntList`/`ParseStringList` helpers. |

## Install

Add this package via Package Manager → **Add package from git URL**:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.remoteconfig
```

Pinned to a released version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.remoteconfig#com.tk.remoteconfig/0.1.0
```

**No scoped registry needed** — unlike `com.tk.ads`, this package's only dependency
(`com.unity.nuget.newtonsoft-json`, which backs `GetObject<T>`) is on Unity's **default** registry,
so install stays git-URL-only. Unity resolves Newtonsoft automatically.

To see this package's EditMode tests in your own project's Test Runner, add
`"testables": ["com.tk.remoteconfig"]` to your project's `Packages/manifest.json`.

## Quickstart

Pick a backend (the Firebase sample, or your own — see below), construct the service, declare a few
params **before** `InitializeAsync` (so their defaults are registered), then read them anywhere:

```csharp
using TK.RemoteConfig;
using TK.RemoteConfig.Samples.FirebaseBackend; // the Firebase sample backend

var rc = new RemoteConfigService(new FirebaseRemoteConfigBackend());

// Declare typed params up front — the default is registered with the backend.
var startingCoins = rc.Int("starting_coins", 100);
var hardMode      = rc.Bool("hard_mode_enabled", false);
var welcomeText   = rc.String("welcome_text", "Welcome!");

await rc.InitializeAsync(); // init backend → (by default) fetch + activate

// Read via implicit conversion or .Value — safe even before init (returns the default).
int coins = startingCoins;          // implicit operator T
if (hardMode.Value) EnableHardMode();
Debug.Log(welcomeText);             // ToString() → current value
```

Reads never throw and never block: before init (or on a missing key / failed fetch) they return the
declared default.

### Per-platform keys

Every factory has an overload taking separate Android and iOS keys/defaults (Editor + other targets
use the Android side):

```csharp
var interval = rc.Int("interval_android", 30, "interval_ios", 45);
```

## Backends

The service is backend-agnostic — it talks only to `IRemoteConfigBackend`:

```csharp
public interface IRemoteConfigBackend
{
    bool IsReady { get; }
    Task InitializeAsync(IReadOnlyDictionary<string, object> defaults);
    Task<bool> FetchAndActivateAsync();
    bool TryGetLong(string key, out long value);
    bool TryGetDouble(string key, out double value);
    bool TryGetBool(string key, out bool value);
    bool TryGetString(string key, out string value);
}
```

- **Firebase** — the `FirebaseBackend` sample (`FirebaseRemoteConfigBackend`) implements this against
  Firebase Remote Config. Firebase is your project's dependency, not this package's; the sample only
  compiles once you've installed the Firebase SDK. See that sample's README.
- **Write your own** — implement the interface for Unity Remote Config, your own backend, a bundled
  JSON, etc. **Critical:** `TryGet*` must **never throw** — the service's raw reads call them without
  a try/catch and rely on a `false` return to fall back to the parameter default. Wrap anything that
  can throw and return `false` on failure (the Firebase sample does exactly this).

## Feeding IAP / Ads

This package has **no dependency** on `com.tk.iap` or `com.tk.ads`. The `IntegrationExamples` sample
ships thin bridges you copy — one line each — that back their existing resolver seams from a live
`RemoteConfigService`:

```csharp
// com.tk.ads — pacing from remote config
var ads = new AdsService(settings, new AdsOptions
{
    PacingResolver = new RcAdsPacingResolver(RemoteConfigService.Instance)
});

// com.tk.iap — catalog amounts from remote config
var iap = new IapService(catalog, new PlayerPrefsJsonSaveSystem(), new IapOptions
{
    AmountResolver = new RcIapAmountResolver(RemoteConfigService.Instance)
});
```

## Grouped-per-domain JSON (recommended)

Prefer **one JSON object per domain** over dozens of scalar keys or CSV lists. Model a feature area
as a plain class and read it with `GetObject<T>` (Newtonsoft — no `[Serializable]` needed):

```csharp
class EconomyConfig
{
    public int StartingCoins = 100;
    public Dictionary<string, int> ItemPrices = new(); // Newtonsoft handles dictionaries
}

// console value for "economy_config":
//   { "StartingCoins": 250, "ItemPrices": { "sword": 500, "shield": 300 } }
var economy = rc.GetObject("economy_config", new EconomyConfig());
```

One console entry per feature area, add a field without touching call sites (old payloads keep the C#
default), and Newtonsoft handles dictionaries / nested objects / top-level arrays / optional fields
that `JsonUtility` cannot. `GetObject<T>` returns the passed default when the key is missing/empty or
the JSON fails to parse (it logs a warning). Use the scalar factories for individual flags,
`GetObject<T>` for grouped configs. See the `DomainConfigExample` sample.

## Editor overrides (QA)

Force any value in the Editor / `TEST_MODE` without a backend round-trip — an override wins over both
the backend value and the default, on the typed path and the raw path:

```csharp
var interval = rc.Int("interstitial_interval", 30);
interval.SetDebugOverride(5);   // QA forces 5s
interval.ClearDebugOverride();  // back to backend/default
RemoteConfigDebug.ClearAll();   // clear every override
```

Wire these to your own debug/QA menu — the `RemoteConfigDebugMenuExample` sample shows the
`[ContextMenu]` pattern. `RemoteConfigDebug` and the `SetDebugOverride`/`ClearDebugOverride` members
are `#if UNITY_EDITOR || TEST_MODE`, so they compile to a no-op in release and **never ship live**.

## Refresh & events

```csharp
rc.OnReady   += () => Debug.Log("first fetch+activate done"); // latch: fires immediately if already ready
rc.OnChanged += ReloadTunables;                                // fires on every activation of new values

bool activated = await rc.RefreshAsync(); // manual re-fetch+activate; true if new values activated
```

- `IsSafeToRead` — true once the backend is initialized (reads return cached/default, never crash).
- `IsReady` — true after the first successful fetch+activate.
- `OnReady` is a **latch**: subscribing after ready invokes immediately, and it fires only once.
- `OnChanged` fires on the initial activation and each later refresh that activates new values.
- `RefreshAsync` never throws; called before `InitializeAsync` it warns and returns `false`.

## Parsing

- **`GetObject<T>`** — the domain-JSON path above (Newtonsoft).
- **CSV** — `RemoteConfigParsing.ParseIntList("4,12,20")` → `[4, 12, 20]` (trims, skips invalid,
  returns an empty list — never null — for null/empty input); `ParseStringList` likewise. For a
  `ConfigParam<string>` there are fluent extensions:

  ```csharp
  var levels = rc.String("banner_levels", "").ParseIntList(); // → List<int>
  ```

  **Gotcha — the `null` literal.** `ParseIntList`/`ParseStringList` exist both as a `static` method
  taking a `string` and as an extension on `ConfigParam<string>`. Calling `ParseIntList(null)` with a
  **bare `null` literal** binds to the *extension* overload (the compiler prefers it) and throws an
  `NullReferenceException` on the null param. To parse a null string, cast:
  `RemoteConfigParsing.ParseIntList((string)null)` — or just use the fluent form
  `param.ParseIntList()`, which is what you want in practice.

## Gotchas

- **Main-thread only.** `RemoteConfigService` assumes Unity's main thread — call every method from
  it (backends typically marshal fetch callbacks back to the main thread).
- **Reads return defaults before `InitializeAsync`.** By design — declare params early, read them
  anywhere; you get the declared default until the first activate lands, then live values.
- **One service per backend.** To re-init after a failure, construct a **fresh** `RemoteConfigService`
  (the static `Instance` points at the most recently constructed one).
- **Editor overrides never ship in release.** They live behind `#if UNITY_EDITOR || TEST_MODE` and
  compile to a no-op — safe to leave in your QA wiring.
- **Backends' `TryGet*` must never throw.** The raw reads have no try/catch; a throwing backend
  crashes the read instead of falling back to the default. Wrap and return `false` (see Backends).
- **`ParseIntList(null)` binds to the extension** and NREs — pass `(string)null` or use the fluent
  form (see Parsing).
