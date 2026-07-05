# com.tk.remoteconfig v0.1.0 — Design Spec

Status: APPROVED by Tolgahan 2026-07-05 (approach + all sections). Committed to `docs/specs/` (NOT gitignored `/.superpowers/`) so it's referenceable from the repo by any developer/agent — an improvement over the ads/iap specs, which live in gitignored scratch.

## Purpose

A backend-agnostic remote-config façade for TK games: declare strongly-typed config parameters once, read them anywhere with defaults and safety gates, override any value in-editor for QA, and refresh at runtime. Its whole reason to exist is to be pluggable — it feeds the resolver seams the shipped packages already expose (`com.tk.iap`'s `IIapAmountResolver`, `com.tk.ads`'s `IAdsPacingResolver`) from any source, tested with a fake backend. Same engineering bar as the other TK packages (seam-based, deterministic EditMode tests, per-task + whole-branch review; this package is reviewed Opus-only per user request).

## Reference system (analyzed, read-only)

`g-brain_test_5`: `Assets/02_Scripts/01_Managers/FRCController.cs` (game-specific parameter declarations) + `Assets/UnicoStudio/UnicoLibs/FirebaseManager/FirebaseRemoteConfigManager.cs` (the mechanism) + `SROptions.RemoteConfig.cs` (debug-menu override UX). Deep analysis in agent memory `remoteconfig-analysis.md`. Core idea: typed `ParameterInfo<T>(key, default)` params (per-platform key variants), self-registering defaults, live type-dispatched reads through Firebase with two readiness gates, editor debug overrides, implicit `operator T`, CSV parse helpers.

## Locked decisions

1. **Declaration model:** instance `RemoteConfigService` + typed `ConfigParam<T>` descriptors created through it (`rc.Int("key", 25)`). Params register their default with the service on creation; reads go through the service. Testable (inject fake backend), ergonomic (implicit `operator T`, co-located default), consistent with the iap/ads instance-service pattern. The game may hold params in its own static holder (`Config.HintPrice`) — its choice, not forced.
2. **Backend:** package is backend-agnostic via `IRemoteConfigBackend`; ZERO backend dependency in the package (fully testable with a fake). The Firebase adapter ships as a **Sample** with real `Firebase.RemoteConfig` code (compiles once imported into a Firebase-present project; Samples~ are never compiled in the harness). The package never references Firebase and never forces it on consumers.
3. **v1 feature scope (all approved):** editor/TEST_MODE debug overrides; per-platform keys (android/ios variant per param, with the `#else` fix so Editor/other targets are safe); JSON `GetObject<T>` helper + CSV parse helpers; mid-session `RefreshAsync` + change event. Core (always in): typed params, live read-through, two readiness gates, latch init event.
4. **Standalone:** NO dependency on `com.tk.core`, `com.tk.iap`, or `com.tk.ads`. Resolver bridges to iap/ads are trivial and game-authored (the IAP key mapping is inherently game-specific) — shipped as Samples + documented one-liners, never a hard cross-package dependency.
5. **No SDK/vendor type leak:** `Firebase.*` types never appear in any public API — only inside the Firebase sample adapter.
6. **Spec is committed** (`docs/specs/`), not gitignored — referenceable.

## Package layout

```
Packages/com.tk.remoteconfig/
  package.json                       # com.tk.remoteconfig 0.1.0, unity 6000.0, NO dependencies
  Runtime/
    TK.RemoteConfig.asmdef           # references: [] (zero — standalone)
    RemoteConfigService.cs           # facade: param factories, lifecycle, gates, events, raw reads
    ConfigParam.cs                    # ConfigParam<T> descriptor: key(s), default, .Value, implicit T, editor override hooks
    RemoteConfigOptions.cs           # optional composition (fetch-on-init toggle, etc.)
    Seams/
      IRemoteConfigBackend.cs        # backend seam + typed TryGet* + init/fetch lifecycle
    RemoteConfigDebug.cs             # editor/TEST_MODE session override store (guarded)
    RemoteConfigParsing.cs           # CSV → List<int>/<string>, GetObject<T> JSON helpers
  Tests/Editor/
    TK.RemoteConfig.Tests.asmdef
    FakeRemoteConfigBackend.cs       # scripting knobs + manual value/ready control
    FakeClock.cs                     # (if needed for any timing)
    RemoteConfigServiceTests.cs / ConfigParamTests.cs / ParsingTests.cs
  Samples~/
    FirebaseBackend/                 # FirebaseRemoteConfigBackend : IRemoteConfigBackend (real Firebase) + README
    IntegrationExamples/             # RcAdsPacingResolver, RcIapAmountResolver, debug-menu wiring + README
  README.md / CHANGELOG.md
```

## Public API surface

```csharp
public sealed class RemoteConfigService
{
    public static RemoteConfigService Instance { get; }   // set in ctor; prefer injection

    public RemoteConfigService(IRemoteConfigBackend backend, RemoteConfigOptions options = null);

    public bool IsSafeToRead { get; }   // defaults registered → reads return cached/default, never crash
    public bool IsReady { get; }        // fetch+activate completed at least once
    public event Action OnReady;        // latch: fires immediately if already ready
    public event Action OnChanged;      // fires on every activation (init + each refresh)

    public Task InitializeAsync();       // single-flight: backend.InitializeAsync(defaults) → IsSafeToRead → fetch+activate → IsReady → OnReady
    public Task<bool> RefreshAsync();    // manual re-fetch+activate; returns whether new values activated; fires OnChanged on activation

    // Typed param factories (single key)
    public ConfigParam<int>    Int(string key, int def);
    public ConfigParam<long>   Long(string key, long def);
    public ConfigParam<double> Double(string key, double def);
    public ConfigParam<float>  Float(string key, float def);
    public ConfigParam<bool>   Bool(string key, bool def);
    public ConfigParam<string> String(string key, string def);

    // Typed param factories (per-platform key/default)
    public ConfigParam<int>    Int(string androidKey, int androidDef, string iosKey, int iosDef);
    // ...same per-platform overload for Long/Double/Float/Bool/String

    // Raw reads (used by ConfigParam and directly)
    public int    GetInt(string key, int def);
    public long   GetLong(string key, long def);
    public double GetDouble(string key, double def);
    public float  GetFloat(string key, float def);
    public bool   GetBool(string key, bool def);
    public string GetString(string key, string def);

    // JSON convenience (JsonUtility; returns def on missing/parse-failure, logs warning)
    public T GetObject<T>(string key, T def);
}

public sealed class ConfigParam<T>
{
    public string Key { get; }          // resolved per-platform key
    public T Default { get; }
    public T Value { get; }             // reads through the service; editor override wins when set
    public static implicit operator T(ConfigParam<T> p);  // ergonomic reads
    public override string ToString();

#if UNITY_EDITOR || TEST_MODE
    public bool HasDebugOverride { get; }
    public void SetDebugOverride(T value);
    public void ClearDebugOverride();
#endif
}
```

`IRemoteConfigBackend`:
```csharp
public interface IRemoteConfigBackend
{
    bool IsReady { get; }   // native backend initialized enough to serve values
    Task InitializeAsync(IReadOnlyDictionary<string, object> defaults);  // register defaults + core init
    Task<bool> FetchAndActivateAsync();  // fetch + activate; true if new values activated
    bool TryGetLong(string key, out long value);
    bool TryGetDouble(string key, out double value);
    bool TryGetBool(string key, out bool value);
    bool TryGetString(string key, out string value);
}
```
The service maps `int`/`float`/enum onto `long`/`double`. `TryGet*` returning false → the service uses the param default. Backends serve last-activated values (Firebase caches natively; the fake serves an in-memory dict).

`RemoteConfigOptions`: `bool FetchOnInitialize = true` (when false, `InitializeAsync` only registers defaults + marks IsSafeToRead; the game calls `RefreshAsync` when it wants). Kept minimal.

## Behavioral contracts (reference-derived, test-pinned)

- **Read model:** `GetX`/`ConfigParam.Value` return the param default whenever `!IsSafeToRead` OR the backend has no value for the key (`TryGet*` false). After a successful activate, reads reflect activated values live. Never throws on a read.
- **Two gates:** `IsSafeToRead` flips true once `backend.InitializeAsync(defaults)` completes (defaults registered) — reads are safe from that point even if fetch never succeeds (offline → defaults/cached). `IsReady` flips true after the first successful fetch+activate. `OnReady` is a latch (subscribing after ready fires immediately, once). `OnChanged` fires on every activation, including refreshes.
- **Single-flight init:** concurrent/repeat `InitializeAsync` awaits the same task (iap/ads pattern). A failed backend init is logged; `IsSafeToRead` stays false; reads return defaults; the service does not throw.
- **Default registration:** creating any `ConfigParam<T>` records its (key → default) with the service so `InitializeAsync` hands the full default set to `backend.InitializeAsync`. Duplicate key registration with a conflicting default logs a warning (last wins, deterministic).
- **Per-platform keys:** the per-platform overload resolves `Key`/`Default` by platform via `#if UNITY_IOS … #else … #endif` (Android + Editor take `#else` — the `#else` fix from ads, so no undefined path).
- **Editor overrides:** `ConfigParam.Value` consults `RemoteConfigDebug` first, and only under `UNITY_EDITOR || TEST_MODE`; an override wins over backend + default. Overrides are session-only, never compiled into release, and never touch the backend.
- **Type mapping:** int → `TryGetLong` then convert; float → `TryGetDouble` then convert; long/double/bool/string direct. Out-of-range/convert failure → default (logged). (Enum params are NOT in v1 — a game stores an enum as int: `(MyEnum)rc.Int("key", (int)MyEnum.Foo)`. See v2 reserves.)
- **JSON GetObject<T>:** `JsonUtility.FromJson<T>` over `GetString(key, null)`; null/empty/parse-failure → `def` (warning logged). Complements, not replaces, raw string reads.
- **Refresh:** `RefreshAsync` calls `backend.FetchAndActivateAsync`; on a true result fires `OnChanged`; returns the activation result; never throws (logs on failure, returns false).

## Resolver bridges (Samples + one-liners — package stays standalone)

- `com.tk.ads`: `class RcAdsPacingResolver : IAdsPacingResolver { readonly RemoteConfigService _rc; public int ResolveSeconds(string key, int def) => _rc.GetInt(key, def); }` — wire `new AdsOptions { PacingResolver = new RcAdsPacingResolver(rc) }`.
- `com.tk.iap`: `class RcIapAmountResolver : IIapAmountResolver { … public int Resolve(string productId, string itemType, int def) => _rc.GetInt($"{productId}_{itemType}_amount", def); }` — the key convention is game-specific, so the game owns it.
Both shipped in `Samples~/IntegrationExamples` (real code referencing the respective TK package, compiled only when imported into a project that has it) and documented as one-liners in the README. No hard dependency either way.

## Firebase backend (Sample)

`Samples~/FirebaseBackend/FirebaseRemoteConfigBackend.cs` implements `IRemoteConfigBackend` with real Firebase: `SetDefaultsAsync(defaults)` in `InitializeAsync`; `FetchAsync(TimeSpan.Zero)` + `ActivateAsync()` in `FetchAndActivateAsync` (checking `LastFetchStatus`); `TryGet*` via `FirebaseRemoteConfig.DefaultInstance.GetValue(key)` with the reference's null/empty-byte check → typed `LongValue`/`DoubleValue`/`BooleanValue`/`StringValue`. README documents Firebase install (the game's dependency — via Google's UPM registry or Firebase's package; the package does not force it) and the iOS "safe to read only after SetDefaults" note.

## Testing

EditMode suite on `FakeRemoteConfigBackend` (knobs: `IsReady`, a values dict, `FailInit`, activation control) + `FakeClock` if any timing arises: default-before-init, live-after-activate, `TryGet*`-false→default, per-platform key selection (editor → android), editor override wins/clears, `GetObject<T>` success + parse-failure fallback, CSV `ParseIntList`/`ParseStringList`, `InitializeAsync` single-flight, `OnReady` latch (fires-after-ready AND fires-immediately-when-already-ready), `RefreshAsync` + `OnChanged`, `IsSafeToRead` gate, type mapping (int/float/enum), duplicate-key default warning. Target ≈25–30 tests. The Firebase adapter and the resolver bridges are review-verified (samples aren't compiled in the harness). Harness gate identical to the other packages.

## v2+ reserves (no API breaks planned)

- Additional backends as samples (Unity Remote Config, a REST/JSON backend).
- Optional schema validation / a "declared but unread" or "read but undeclared" audit for editor QA.
- A/B-test assignment surfacing (if a backend exposes it) via an added seam method.
- Enum param factory (`Enum<TEnum>(key, def)` mapping through the underlying integral type) — dropped from v1 for API simplicity; games cast an int param in the meantime.
