# com.tk.remoteconfig v0.1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This package is executed and reviewed **Opus-only** per the user's request — dispatch every implementer and reviewer on the Opus model.

**Goal:** A backend-agnostic remote-config façade (`com.tk.remoteconfig`): declare strongly-typed config parameters once, read them anywhere with defaults + safety gates + editor overrides + runtime refresh, backing the resolver seams the shipped IAP/Ads packages already expose — all testable against a fake backend.

**Architecture:** Approved committed spec: `docs/specs/2026-07-05-tk-remoteconfig-design.md` (READ IT FIRST — behavioral contracts are binding). An instance `RemoteConfigService` owns lifecycle/gates/reads behind an `IRemoteConfigBackend` seam; `ConfigParam<T>` descriptors give typed ergonomic reads; the Firebase backend and the IAP/Ads resolver bridges ship as Samples. Zero package dependencies.

**Tech Stack:** Unity 6000.3.6f1 host, C#, NUnit EditMode. No runtime third-party dependency (Firebase lives only in a Sample).

## Global Constraints

- Repo: `/Users/tolgahankurtdere/Documents/GitHub/unity-packages`, branch `main`. Base = the current `main` tip when execution begins (the commit that added this plan, on top of spec commit `1ad92e5`). New package root: `Packages/com.tk.remoteconfig/`.
- **NEVER run Unity CLI against the host project** (user's editor may be open). Harness: `/private/tmp/claude-501/-Users-tolgahankurtdere-Documents-GitHub-unity-packages/125643b5-4b33-48e0-b763-cca5d06442d8/scratchpad/tk-verify`. It is already wired with `com.tk.core`, `com.tk.iap`, `com.tk.ads` (+ the AppLovin & OpenUPM scoped registries that ads needs — DO NOT remove them). If the harness is missing (new session), recreate: `Assets/` + `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 6000.3.6f1`) + `Packages/manifest.json` with test-framework 1.6.0, the four TK packages as `file:` absolute paths, `testables` for all four, and the two scoped registries.
- Gate command (from harness dir; NEVER `-quit` with `-runTests`; Bash timeout 600000):
  ```bash
  /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$(pwd)/results.xml" -logFile "$(pwd)/unity.log"
  ```
  Success = exit 0 AND results.xml `result="Passed"` AND zero `error CS`/`warning CS` under `Packages/com.tk`. Baseline before Task 1 = the current harness total (core+iap+ads, ~134 — trust results.xml, not arithmetic). Report the exact `TK.RemoteConfig.Tests` count each task.
- package.json (exact): name `com.tk.remoteconfig`, version `0.1.0`, displayName `TK Remote Config`, description `Backend-agnostic remote-config façade: typed parameters with defaults, safety gates, editor overrides, and runtime refresh — feeds the IAP/Ads resolver seams from any backend.`, unity `6000.0`, **NO `dependencies` key** (zero dependencies — standalone), author `{ "name": "Tolga Kurtdere", "url": "https://github.com/tolgakurtdere" }`, keywords `["tk", "remote-config", "config", "firebase"]`.
- Asmdefs: `TK.RemoteConfig` (rootNamespace `TK.RemoteConfig`, `"references": []`, autoReferenced true); `TK.RemoteConfig.Tests` (Editor-only, references `["TK.RemoteConfig", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, overrideReferences true + `nunit.framework.dll`, defineConstraints `UNITY_INCLUDE_TESTS`, autoReferenced false).
- **No `Firebase.*` / vendor types in any public API** — only inside the Firebase Sample.
- Namespace `TK.RemoteConfig` for all runtime; `TK.RemoteConfig.Tests` for tests.
- Editor-override code is guarded by `#if UNITY_EDITOR || TEST_MODE` and compiles cleanly (and to a no-op) in release.
- Every file/folder under `Packages/com.tk.remoteconfig` gets a committed `.meta` (harness gate generates). Conventional commits + trailer `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` (kept uniform with the rest of the repo history). Do NOT push mid-plan; do NOT commit `docs/` churn is fine to commit (it's the plan/spec home) but do NOT commit `.superpowers/` or unrelated host churn.
- Firebase API facts for the Sample (Task 5), verified against installed `com.google.firebase.remote-config@12.10.1` + the working reference `g-brain_test_5/Assets/UnicoStudio/UnicoLibs/FirebaseManager/FirebaseRemoteConfigManager.cs` (implementer RE-VERIFIES before writing): `FirebaseRemoteConfig.DefaultInstance`; `SetDefaultsAsync(IDictionary<string,object>)`; `FetchAsync(TimeSpan)`; `remoteConfig.Info.LastFetchStatus == LastFetchStatus.Success`; `ActivateAsync()`; `GetValue(key)` → `ConfigValue` with `.LongValue/.DoubleValue/.BooleanValue/.StringValue/.ByteArrayValue` (null/empty byte array ⇒ no value).

---

### Task 1: Package skeleton + backend seam + fake + harness wiring

**Files:**
- Create: `Packages/com.tk.remoteconfig/package.json`, `Runtime/TK.RemoteConfig.asmdef`
- Create: `Runtime/Seams/IRemoteConfigBackend.cs`
- Create: `Tests/Editor/TK.RemoteConfig.Tests.asmdef`, `Tests/Editor/FakeRemoteConfigBackend.cs`, `Tests/Editor/FakeRemoteConfigBackendTests.cs`
- Modify: harness `Packages/manifest.json`

**Interfaces produced:** `IRemoteConfigBackend` and `FakeRemoteConfigBackend` exactly as below — every later task compiles against these.

- [ ] **Step 1: package.json + asmdefs** (exact values from Global Constraints).

- [ ] **Step 2: IRemoteConfigBackend.cs** (full code):

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TK.RemoteConfig
{
    /// <summary>
    /// Remote-config backend seam. The package ships no backend (a Firebase adapter is a Sample);
    /// tests inject a fake. Implementations serve last-activated values; a missing key returns false
    /// from TryGet* so the service falls back to the parameter default.
    /// </summary>
    public interface IRemoteConfigBackend
    {
        /// <summary>True once the backend core is initialized enough to answer TryGet* safely.</summary>
        bool IsReady { get; }

        /// <summary>Register defaults and initialize the backend core. Completes when values are safe to read.</summary>
        Task InitializeAsync(IReadOnlyDictionary<string, object> defaults);

        /// <summary>Fetch + activate remote values. Returns true if new values were activated.</summary>
        Task<bool> FetchAndActivateAsync();

        bool TryGetLong(string key, out long value);
        bool TryGetDouble(string key, out double value);
        bool TryGetBool(string key, out bool value);
        bool TryGetString(string key, out string value);
    }
}
```

- [ ] **Step 3: FakeRemoteConfigBackend.cs** (full code, namespace `TK.RemoteConfig.Tests`):

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TK.RemoteConfig;

namespace TK.RemoteConfig.Tests
{
    /// <summary>In-memory backend for deterministic EditMode tests.</summary>
    public sealed class FakeRemoteConfigBackend : IRemoteConfigBackend
    {
        public bool IsReady { get; private set; }

        // Knobs
        public bool ThrowOnInit;
        public bool FailFetch;
        public bool NextFetchActivates = true;

        // Recorded
        public int InitializeCalls;
        public int FetchCalls;
        public IReadOnlyDictionary<string, object> ReceivedDefaults;

        // Values the backend serves (set by tests to simulate activated remote values)
        public readonly Dictionary<string, object> Values = new();

        public void SetLong(string key, long v) => Values[key] = v;
        public void SetDouble(string key, double v) => Values[key] = v;
        public void SetBool(string key, bool v) => Values[key] = v;
        public void SetString(string key, string v) => Values[key] = v;

        public Task InitializeAsync(IReadOnlyDictionary<string, object> defaults)
        {
            InitializeCalls++;
            ReceivedDefaults = defaults;
            if (ThrowOnInit) throw new InvalidOperationException("fake: init threw");
            IsReady = true;
            return Task.CompletedTask;
        }

        public Task<bool> FetchAndActivateAsync()
        {
            FetchCalls++;
            if (FailFetch) return Task.FromException<bool>(new Exception("fake: fetch failed"));
            return Task.FromResult(NextFetchActivates);
        }

        public bool TryGetLong(string key, out long value)
        {
            if (Values.TryGetValue(key, out var o) && o is long l) { value = l; return true; }
            value = 0; return false;
        }

        public bool TryGetDouble(string key, out double value)
        {
            if (Values.TryGetValue(key, out var o) && o is double d) { value = d; return true; }
            value = 0; return false;
        }

        public bool TryGetBool(string key, out bool value)
        {
            if (Values.TryGetValue(key, out var o) && o is bool b) { value = b; return true; }
            value = false; return false;
        }

        public bool TryGetString(string key, out string value)
        {
            if (Values.TryGetValue(key, out var o) && o is string s) { value = s; return true; }
            value = null; return false;
        }
    }
}
```

- [ ] **Step 4: smoke tests** (`FakeRemoteConfigBackendTests.cs`, complete NUnit code, namespace `TK.RemoteConfig.Tests`, 4 tests): `InitializeAsync_RecordsDefaults_AndReady` (pass a dict → ReceivedDefaults same, IsReady true, InitializeCalls 1); `InitializeAsync_ThrowKnob_Throws` (`Assert.ThrowsAsync<InvalidOperationException>`); `TryGet_TypedHitAndMiss` (SetLong("a",5) → TryGetLong true/5, TryGetLong("b") false/0, TryGetString("a") false — wrong type); `FetchAndActivate_ReturnsKnob_AndCounts` (NextFetchActivates=false → false; FetchCalls increments).

- [ ] **Step 5: harness wiring** — in the harness `Packages/manifest.json`: add `"com.tk.remoteconfig": "file:/Users/tolgahankurtdere/Documents/GitHub/unity-packages/Packages/com.tk.remoteconfig"` to dependencies and `"com.tk.remoteconfig"` to `testables`. Leave the existing packages and the two scoped registries untouched. (No new registry — this package has zero external deps.)

- [ ] **Step 6: gate** (baseline + 4). **Step 7: commit** — `feat(remoteconfig): add com.tk.remoteconfig skeleton with backend seam and fake`.

---

### Task 2: RemoteConfigService — lifecycle, gates, events, raw reads

**Files:**
- Create: `Packages/com.tk.remoteconfig/Runtime/RemoteConfigOptions.cs`, `Runtime/RemoteConfigService.cs`
- Create: `Tests/Editor/RemoteConfigServiceTests.cs`

**Interfaces:**
- Consumes: `IRemoteConfigBackend` (Task 1).
- Produces (Task 3 consumes): `RemoteConfigService` with `IsSafeToRead`/`IsReady`, `OnReady` (latch)/`OnChanged`, `InitializeAsync()`/`RefreshAsync()`, raw `GetInt/GetLong/GetDouble/GetFloat/GetBool/GetString`, `internal void RegisterDefault(string, object)`, `internal static bool TryGetOverride<T>(string, out T)`, `static Instance`. `RemoteConfigOptions { bool FetchOnInitialize = true; }`.

- [ ] **Step 1: RemoteConfigOptions.cs**:

```csharp
namespace TK.RemoteConfig
{
    /// <summary>Optional composition for RemoteConfigService.</summary>
    public sealed class RemoteConfigOptions
    {
        /// <summary>When true, InitializeAsync also fetches+activates. When false, call RefreshAsync yourself.</summary>
        public bool FetchOnInitialize = true;
    }
}
```

- [ ] **Step 2: RemoteConfigService.cs** (full code — transcribe EXACTLY):

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.RemoteConfig
{
    /// <summary>
    /// Backend-agnostic remote-config façade. Declare typed params via the factory methods (Task 3),
    /// read them live with default fallback, override in-editor for QA, refresh at runtime.
    /// Main-thread usage assumed. One service per backend; construct fresh to re-init after a failure.
    /// </summary>
    public sealed partial class RemoteConfigService
    {
        public static RemoteConfigService Instance { get; private set; }

        /// <summary>True once defaults are registered (backend init completed) — reads are safe (return cached/default), never crash.</summary>
        public bool IsSafeToRead { get; private set; }

        /// <summary>True after the first successful fetch+activate.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Latch: fires once when ready; subscribing after ready invokes immediately.</summary>
        public event Action OnReady
        {
            add { if (IsReady) value?.Invoke(); else _onReady += value; }
            remove => _onReady -= value;
        }

        /// <summary>Fires on every activation (initial fetch + each refresh that activates new values).</summary>
        public event Action OnChanged;

        private readonly IRemoteConfigBackend _backend;
        private readonly RemoteConfigOptions _options;
        private readonly Dictionary<string, object> _defaults = new();
        private Action _onReady;
        private Task _initTask;

        public RemoteConfigService(IRemoteConfigBackend backend, RemoteConfigOptions options = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _options = options ?? new RemoteConfigOptions();
            Instance = this;
        }

        /// <summary>Records a parameter's default for backend registration. Called by the typed factories.</summary>
        internal void RegisterDefault(string key, object firebaseDefault)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[RemoteConfig] Parameter registered with an empty key.");
                return;
            }

            if (_defaults.TryGetValue(key, out var existing) && !Equals(existing, firebaseDefault))
                Debug.LogWarning($"[RemoteConfig] Key '{key}' registered with a different default; last wins.");

            _defaults[key] = firebaseDefault;
        }

        public async Task InitializeAsync()
        {
            if (_initTask != null)
            {
                await _initTask;
                return;
            }

            _initTask = InitializeInternalAsync();
            await _initTask;
        }

        private async Task InitializeInternalAsync()
        {
            try
            {
                await _backend.InitializeAsync(_defaults);
                IsSafeToRead = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return; // IsSafeToRead stays false → reads return defaults; no throw out of Initialize
            }

            if (_options.FetchOnInitialize)
                await FetchActivateAsync();
        }

        /// <summary>Manual re-fetch+activate. Returns whether new values activated. Never throws.</summary>
        public async Task<bool> RefreshAsync()
        {
            if (!IsSafeToRead)
            {
                Debug.LogWarning("[RemoteConfig] RefreshAsync called before InitializeAsync; ignored.");
                return false;
            }

            return await FetchActivateAsync();
        }

        private async Task<bool> FetchActivateAsync()
        {
            try
            {
                var activated = await _backend.FetchAndActivateAsync();

                if (!IsReady)
                {
                    IsReady = true;
                    var latch = _onReady;
                    _onReady = null;
                    latch?.Invoke();
                }

                if (activated) OnChanged?.Invoke();
                return activated;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        // ── Raw reads (default fallback + safety gate + editor override) ──

        public long GetLong(string key, long def)
        {
            if (TryGetOverride<long>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetLong(key, out var v) ? v : def;
        }

        public int GetInt(string key, int def)
        {
            if (TryGetOverride<int>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetLong(key, out var v) ? unchecked((int)v) : def;
        }

        public double GetDouble(string key, double def)
        {
            if (TryGetOverride<double>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetDouble(key, out var v) ? v : def;
        }

        public float GetFloat(string key, float def)
        {
            if (TryGetOverride<float>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetDouble(key, out var v) ? (float)v : def;
        }

        public bool GetBool(string key, bool def)
        {
            if (TryGetOverride<bool>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetBool(key, out var v) ? v : def;
        }

        public string GetString(string key, string def)
        {
            if (TryGetOverride<string>(key, out var ov)) return ov;
            if (!IsSafeToRead) return def;
            return _backend.TryGetString(key, out var v) ? v : def;
        }
    }
}
```
NOTE: the class is `partial` because Task 3 adds the typed factories + `TryGetOverride` in a second file (`RemoteConfigService.Params.cs`). For THIS task to compile, `TryGetOverride<T>` must exist — add a minimal stub in a small partial now, OR (cleaner) include `TryGetOverride` here. To keep Task 2 self-contained and green, ADD this private method to RemoteConfigService.cs in this task (Task 3 does NOT redefine it):

```csharp
        internal static bool TryGetOverride<T>(string key, out T value)
        {
#if UNITY_EDITOR || TEST_MODE
            if (RemoteConfigDebug.TryGet(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
#endif
            value = default;
            return false;
        }
```
`RemoteConfigDebug` is referenced only inside the `#if` — so it must exist for editor/test compilation. Task 2 therefore ALSO creates the minimal `RemoteConfigDebug` (below); Task 3 uses it further. Add `Runtime/RemoteConfigDebug.cs`:

```csharp
using System.Collections.Generic;

namespace TK.RemoteConfig
{
    /// <summary>
    /// Editor/TEST_MODE-only session override store for QA. Never compiled into release builds.
    /// A game wires this to its own debug menu (see the Integration Examples sample).
    /// </summary>
    public static class RemoteConfigDebug
    {
#if UNITY_EDITOR || TEST_MODE
        private static readonly Dictionary<string, object> s_overrides = new();

        public static bool HasAny => s_overrides.Count > 0;
        public static void Set(string key, object value) => s_overrides[key] = value;
        public static void Clear(string key) => s_overrides.Remove(key);
        public static void ClearAll() => s_overrides.Clear();
        public static bool TryGet(string key, out object value) => s_overrides.TryGetValue(key, out value);
#else
        public static bool HasAny => false;
        public static void Set(string key, object value) { }
        public static void Clear(string key) { }
        public static void ClearAll() { }
        public static bool TryGet(string key, out object value) { value = null; return false; }
#endif
    }
}
```
(Both branches defined so callers compile in every configuration; release branch is a no-op.)

- [ ] **Step 3: RemoteConfigServiceTests.cs** (complete code, inject `FakeRemoteConfigBackend`; `async Task` tests; `TearDown` calls `RemoteConfigDebug.ClearAll()` to isolate override state). Tests (14):
  1. `Reads_BeforeInitialize_ReturnDefaults` — no init → GetInt/GetString/GetBool/GetDouble/GetFloat/GetLong all return their defaults; IsSafeToRead false.
  2. `Initialize_SetsSafeToRead` — after InitializeAsync, IsSafeToRead true, backend.InitializeCalls 1, ReceivedDefaults non-null (empty dict is fine — no factories exist yet in this task; default-registration is covered by Task 3's `Factory_RegistersDefault`). Note: `RegisterDefault` is `internal` and factories arrive in Task 3, so Task 2 tests must NOT try to register defaults directly.
  3. `Reads_AfterActivate_ReflectBackendValues` — backend.SetLong("a",7)+SetString("s","x") before init; init → GetInt("a",0)==7, GetString("s","d")=="x".
  4. `Reads_MissingKey_ReturnDefault_AfterReady` — init with empty Values → GetInt("nope",42)==42.
  5. `Initialize_SingleFlight` — two awaited InitializeAsync → InitializeCalls 1, FetchCalls 1.
  6. `Initialize_BackendThrows_StaysUnsafe_NoThrow` — ThrowOnInit → InitializeAsync does not throw; IsSafeToRead false; reads return defaults (LogAssert.Expect the exception).
  7. `Initialize_FetchFails_SafeToReadStillTrue_NotReady` — FailFetch → after init IsSafeToRead true (defaults registered) but IsReady false; reads return defaults (LogAssert.Expect exception).
  8. `OnReady_FiresAfterInit` — subscribe before init, init → invoked once.
  9. `OnReady_Latch_FiresImmediatelyWhenAlreadyReady` — init first, THEN subscribe → invoked immediately; and only once total across a later RefreshAsync.
  10. `OnChanged_FiresOnActivation` — init (NextFetchActivates true) → OnChanged fired once; RefreshAsync (activates) → fired again (count 2).
  11. `OnChanged_NotFired_WhenNoNewActivation` — NextFetchActivates=false → after init OnChanged count 0, but IsReady true and OnReady fired.
  12. `Refresh_BeforeInit_ReturnsFalse_Warns` — RefreshAsync without init → false (LogAssert.Expect warning), FetchCalls 0.
  13. `Refresh_ReturnsActivationResult` — init; set NextFetchActivates=false → RefreshAsync false; set true → RefreshAsync true.
  14. `Int_OverflowFromLong_UncheckedCast` — SetLong("big", (long)int.MaxValue + 1) → GetInt("big",0) wraps via unchecked cast (documents the contract; assert the unchecked value).

- [ ] **Step 4: gate** (baseline + 4 + 14). **Step 5: commit** — `feat(remoteconfig): add RemoteConfigService with lifecycle, gates, events and raw reads`.

---

### Task 3: ConfigParam<T> + typed factories + per-platform keys + editor overrides

**Files:**
- Create: `Packages/com.tk.remoteconfig/Runtime/ConfigParam.cs`, `Runtime/RemoteConfigService.Params.cs`
- Create: `Tests/Editor/ConfigParamTests.cs`

**Interfaces:**
- Consumes: `RemoteConfigService` raw reads + `RegisterDefault` + `TryGetOverride` (Task 2), `RemoteConfigDebug` (Task 2).
- Produces: `ConfigParam<T>` (`Key`, `Default`, `Value`, implicit `operator T`, editor `SetDebugOverride/ClearDebugOverride/HasDebugOverride`); service factories `Int/Long/Double/Float/Bool/String` (single-key) and their per-platform overloads.

- [ ] **Step 1: ConfigParam.cs** (full code):

```csharp
using System;

namespace TK.RemoteConfig
{
    /// <summary>
    /// A strongly-typed, declared-once config parameter. Reads through its RemoteConfigService
    /// (default fallback + safety gate + editor override), converts implicitly to T.
    /// Create via the service factories (rc.Int/Bool/String/...).
    /// </summary>
    public sealed class ConfigParam<T>
    {
        public string Key { get; }
        public T Default { get; }

        private readonly Func<T> _getter;

        internal ConfigParam(string key, T def, Func<T> getter)
        {
            Key = key;
            Default = def;
            _getter = getter;
        }

        /// <summary>Current value: editor override (if any) → backend value → default.</summary>
        public T Value => _getter();

        public static implicit operator T(ConfigParam<T> param) => param.Value;

        public override string ToString() => Value?.ToString() ?? string.Empty;

#if UNITY_EDITOR || TEST_MODE
        public bool HasDebugOverride => RemoteConfigDebug.TryGet(Key, out _);
        public void SetDebugOverride(T value) => RemoteConfigDebug.Set(Key, value);
        public void ClearDebugOverride() => RemoteConfigDebug.Clear(Key);
#endif
    }
}
```

- [ ] **Step 2: RemoteConfigService.Params.cs** (full code — the second partial):

```csharp
namespace TK.RemoteConfig
{
    public sealed partial class RemoteConfigService
    {
        // ── Single-key typed factories (register default, wire a getter through the raw reads) ──

        public ConfigParam<int> Int(string key, int def)
        {
            RegisterDefault(key, (long)def);
            return new ConfigParam<int>(key, def, () => GetInt(key, def));
        }

        public ConfigParam<long> Long(string key, long def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<long>(key, def, () => GetLong(key, def));
        }

        public ConfigParam<double> Double(string key, double def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<double>(key, def, () => GetDouble(key, def));
        }

        public ConfigParam<float> Float(string key, float def)
        {
            RegisterDefault(key, (double)def);
            return new ConfigParam<float>(key, def, () => GetFloat(key, def));
        }

        public ConfigParam<bool> Bool(string key, bool def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<bool>(key, def, () => GetBool(key, def));
        }

        public ConfigParam<string> String(string key, string def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<string>(key, def, () => GetString(key, def));
        }

        // ── Per-platform overloads (Android + Editor use android; iOS uses ios) ──

        public ConfigParam<int> Int(string androidKey, int androidDef, string iosKey, int iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Int);

        public ConfigParam<long> Long(string androidKey, long androidDef, string iosKey, long iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Long);

        public ConfigParam<double> Double(string androidKey, double androidDef, string iosKey, double iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Double);

        public ConfigParam<float> Float(string androidKey, float androidDef, string iosKey, float iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Float);

        public ConfigParam<bool> Bool(string androidKey, bool androidDef, string iosKey, bool iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Bool);

        public ConfigParam<string> String(string androidKey, string androidDef, string iosKey, string iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, String);

        private static ConfigParam<T> SelectPlatform<T>(
            string androidKey, T androidDef, string iosKey, T iosDef, System.Func<string, T, ConfigParam<T>> factory)
        {
#if UNITY_IOS
            return factory(iosKey, iosDef);
#else
            return factory(androidKey, androidDef); // Android + Editor + other targets
#endif
        }
    }
}
```

- [ ] **Step 3: ConfigParamTests.cs** (complete code, inject fake; `TearDown` → `RemoteConfigDebug.ClearAll()`). Tests (12):
  1. `Int_Value_ReadsThroughService` — backend.SetLong("k",9); init; `ConfigParam<int> p = rc.Int("k",0)` → p.Value==9, implicit `int x = p` ==9.
  2. `Param_BeforeInit_ReturnsDefault` — `rc.Bool("b",true)`; no init → p.Value true; backend has false but not safe-to-read.
  3. `Param_MissingKey_ReturnsOwnDefault` — init empty; `rc.Int("miss",5)` → 5.
  4. `Factory_RegistersDefault` — `rc.Int("k",3)`; init → backend.ReceivedDefaults["k"] equals 3L (long-boxed).
  5. `Float_RegistersDoubleDefault_AndReads` — SetDouble("f",1.5); init; `rc.Float("f",0f)` → 1.5f; ReceivedDefaults["f"] is double.
  6. `String_ReadsAndDefaults` — both paths.
  7. `Long/Double/Bool_ReadThrough` — one combined test asserting each type round-trips a backend value.
  8. `PerPlatform_Editor_SelectsAndroid` — `rc.Int("k_a",1,"k_i",2)` → Key=="k_a", Default==1 (editor runs #else); backend.SetLong("k_a",11); init → Value 11.
  9. `EditorOverride_WinsOverBackendAndDefault` — SetLong("k",9); init; `var p=rc.Int("k",0); p.SetDebugOverride(123)` → p.Value==123; and `rc.GetInt("k",0)`==123 (override honored on the raw path too).
  10. `EditorOverride_Clear_RestoresValue` — after ClearDebugOverride → p.Value back to 9; HasDebugOverride false.
  11. `EditorOverride_WrongTypeStored_FallsThrough` — manually `RemoteConfigDebug.Set("k","str")` then `rc.GetInt("k",0)` after init with SetLong("k",9) → returns 9 (override type mismatch falls through, no throw).
  12. `DuplicateKey_DifferentDefault_Warns_LastWins` — `rc.Int("k",1); rc.Int("k",2)`; LogAssert.Expect warning; init → ReceivedDefaults["k"]==2L.

- [ ] **Step 4: gate** (prev + 12). **Step 5: commit** — `feat(remoteconfig): add typed ConfigParam factories with per-platform keys and editor overrides`.

---

### Task 4: Parsing helpers (GetObject<T> + CSV)

**Files:**
- Create: `Packages/com.tk.remoteconfig/Runtime/RemoteConfigParsing.cs`
- Modify: `Runtime/RemoteConfigService.cs` — add `GetObject<T>` (or put it in the parsing file as an extension; see below)
- Create: `Tests/Editor/RemoteConfigParsingTests.cs`

**Interfaces:**
- Consumes: `RemoteConfigService.GetString`, `ConfigParam<string>`.
- Produces: `RemoteConfigService.GetObject<T>(string key, T def)`; extensions `ConfigParam<string>.ParseIntList()/ParseStringList()` and `RemoteConfigParsing.ParseIntList(string)/ParseStringList(string)`.

- [ ] **Step 1: GetObject<T>** — add to `RemoteConfigService.cs` (it needs `GetString` + is core API):

```csharp
        /// <summary>
        /// Reads a JSON string value and deserializes it via JsonUtility. Returns def when the key
        /// is missing/empty or parsing fails (logs a warning). Complements raw GetString.
        /// </summary>
        public T GetObject<T>(string key, T def)
        {
            var json = GetString(key, null);
            if (string.IsNullOrEmpty(json)) return def;

            try
            {
                var parsed = JsonUtility.FromJson<T>(json);
                return parsed != null ? parsed : def;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RemoteConfig] GetObject<{typeof(T).Name}> failed for key '{key}': {exception.Message}");
                return def;
            }
        }
```
(`JsonUtility`/`Debug` are already available via `using UnityEngine;` in that file.)

- [ ] **Step 2: RemoteConfigParsing.cs** (full code):

```csharp
using System.Collections.Generic;

namespace TK.RemoteConfig
{
    /// <summary>CSV parse helpers for string config values (e.g. "4,12,20" → list).</summary>
    public static class RemoteConfigParsing
    {
        public static List<int> ParseIntList(string csv)
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(csv)) return list;

            foreach (var part in csv.Split(','))
            {
                if (int.TryParse(part.Trim(), out var n)) list.Add(n);
            }

            return list;
        }

        public static List<string> ParseStringList(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(csv)) return list;

            foreach (var part in csv.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }

            return list;
        }

        public static List<int> ParseIntList(this ConfigParam<string> param) => ParseIntList(param.Value);
        public static List<string> ParseStringList(this ConfigParam<string> param) => ParseStringList(param.Value);
    }
}
```

- [ ] **Step 3: RemoteConfigParsingTests.cs** (complete code). Tests (8):
  1. `ParseIntList_Basic` — "4,12,20" → [4,12,20].
  2. `ParseIntList_TrimsAndSkipsInvalid` — " 4 , x, 12 " → [4,12].
  3. `ParseIntList_EmptyOrNull` — "" and null → empty list (not null).
  4. `ParseStringList_Basic` — "a,b,c" → [a,b,c].
  5. `ParseStringList_TrimsAndDropsEmpty` — "a, ,b," → [a,b].
  6. `ParseIntList_FromParam` — SetString("k","1,2"); init; `rc.String("k","").ParseIntList()` → [1,2].
  7. `GetObject_ParsesJson` — a `[Serializable] class Payload { public int n; public string s; }`; SetString("k","{\"n\":5,\"s\":\"x\"}"); init; `rc.GetObject<Payload>("k",null)` → n5/s"x".
  8. `GetObject_MissingOrInvalid_ReturnsDefault` — missing key → def; SetString("k","{bad json"); init → def (LogAssert.Expect warning).

- [ ] **Step 4: gate** (prev + 8). **Step 5: commit** — `feat(remoteconfig): add GetObject<T> and CSV parse helpers`.

---

### Task 5: Samples (Firebase backend + resolver bridges) + docs + host wiring + final gate

**Files:**
- Create: `Packages/com.tk.remoteconfig/Samples~/FirebaseBackend/FirebaseRemoteConfigBackend.cs` + `README.md`
- Create: `Packages/com.tk.remoteconfig/Samples~/IntegrationExamples/RcAdsPacingResolver.cs`, `RcIapAmountResolver.cs`, `RemoteConfigDebugMenuExample.cs`, `README.md`
- Create: `Packages/com.tk.remoteconfig/README.md`, `CHANGELOG.md`
- Modify: `Packages/com.tk.remoteconfig/package.json` (samples array); root `README.md` (package row + install); `ROADMAP.md` (mark shipped); HOST `Packages/manifest.json` (testables gains com.tk.remoteconfig)

- [ ] **Step 1: FirebaseRemoteConfigBackend.cs** — implements `IRemoteConfigBackend` with real Firebase (RE-VERIFY every member against installed `com.google.firebase.remote-config@12.10.1` + the reference `FirebaseRemoteConfigManager.cs`; on drift trust the installed source and note it). Structure: `InitializeAsync(defaults)` → `FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(new Dictionary<string,object>(defaults))` then set an internal ready flag; `FetchAndActivateAsync()` → `FetchAsync(TimeSpan.Zero)`, check `Info.LastFetchStatus == LastFetchStatus.Success`, `await ActivateAsync()`, return whether activated; `TryGetLong/Double/Bool/String` → `DefaultInstance.GetValue(key)`, treat empty `ByteArrayValue` (null/zero-length) as "no value" (return false), else return `.LongValue/.DoubleValue/.BooleanValue/.StringValue`. `IsReady` → true after SetDefaults completes. Class doc: this is a Sample; it references `Firebase.RemoteConfig`, so it only compiles once imported into a project that has the Firebase Remote Config SDK installed (the game's own dependency — via Google's registry, the Firebase tarballs, or the Firebase unitypackage; this package does not force Firebase).

- [ ] **Step 2: resolver bridges + debug menu sample** —
  - `RcAdsPacingResolver.cs`: `using TK.Ads; using TK.RemoteConfig; sealed class RcAdsPacingResolver : IAdsPacingResolver { readonly RemoteConfigService _rc; public RcAdsPacingResolver(RemoteConfigService rc){_rc=rc;} public int ResolveSeconds(string key, int defaultSeconds) => _rc.GetInt(key, defaultSeconds); }` (references TK.Ads — compiles when com.tk.ads is present).
  - `RcIapAmountResolver.cs`: `using TK.IAP; using TK.RemoteConfig; sealed class RcIapAmountResolver : IIapAmountResolver { readonly RemoteConfigService _rc; public RcIapAmountResolver(RemoteConfigService rc){_rc=rc;} public int Resolve(string productId, string itemType, int defaultAmount) => _rc.GetInt($"{productId}_{itemType}_amount", defaultAmount); }` (comment: the key convention is a game choice — adapt it).
  - `RemoteConfigDebugMenuExample.cs`: a small MonoBehaviour with `[ContextMenu]` methods that call `param.SetDebugOverride(...)` / `RemoteConfigDebug.ClearAll()` on a couple of example params, showing how a game wires RC overrides to its own QA menu (mirrors the reference's SROptions integration).
  - Sample `README.md`: how to use each (one-liners for wiring the resolvers into `AdsOptions.PacingResolver` / `IapOptions.AmountResolver`).

- [ ] **Step 3: package README.md** — sections: What's inside (service/param/seam/debug/parsing table); Install (git URL `https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.remoteconfig` + tag pin `#com.tk.remoteconfig/0.1.0`; NOTE: zero registries needed — unlike com.tk.ads — because this package has no external dependencies; consumer `testables` note); Quickstart (`new RemoteConfigService(backend)` with the Firebase sample backend, declare a few params, `await InitializeAsync()`, read via implicit conversion); Backends (backend-agnostic; Firebase sample; write your own by implementing `IRemoteConfigBackend`); Feeding IAP/Ads (the two resolver one-liners; note this package has NO dependency on iap/ads — the bridges are samples you copy); Editor overrides (QA: SetDebugOverride, wire to a debug menu — sample); Refresh & events (OnReady latch, OnChanged, RefreshAsync); Parsing (GetObject<T>, CSV); Gotchas (main-thread; reads return defaults before InitializeAsync; one service per backend; editor overrides never ship in release). CHANGELOG.md keep-a-changelog `## [0.1.0] - 2026-07-05`.

- [ ] **Step 4: package.json samples array** — `[{ "displayName": "Firebase Backend", "description": "IRemoteConfigBackend adapter backed by Firebase Remote Config.", "path": "Samples~/FirebaseBackend" }, { "displayName": "Integration Examples", "description": "Resolver bridges to TK IAP/Ads and a debug-menu override example.", "path": "Samples~/IntegrationExamples" }]`.

- [ ] **Step 5: root README + ROADMAP + host wiring** — root `README.md`: add `com.tk.remoteconfig` row (0.1.0, Unity 6000.0+, no registries) + install URL + tag `com.tk.remoteconfig/0.1.0` in the Versioning list. `ROADMAP.md`: move remote-config from "Candidate new packages" to "Shipped", and update the "recommended next" pointer to analytics. HOST `Packages/manifest.json`: add `"com.tk.remoteconfig"` to `testables` (embedded package resolves automatically; no registry needed).

- [ ] **Step 6: final gate** (all tests green, zero com.tk warnings; `git status` clean apart from known host churn — commit `Packages/packages-lock.json` if it records the new embedded package). Verify `Samples~` files are git-tracked (`!*~/` rule): `git check-ignore -v Packages/com.tk.remoteconfig/Samples~/FirebaseBackend/FirebaseRemoteConfigBackend.cs` must report NOT ignored. **Step 7: commit** — `docs(remoteconfig): add samples, package docs and host wiring`. (Push + `com.tk.remoteconfig/0.1.0` tag happen AFTER the final whole-branch review, in the finishing step.)
