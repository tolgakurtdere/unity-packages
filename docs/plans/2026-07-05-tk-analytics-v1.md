# com.tk.analytics v0.1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This package is executed and reviewed **Opus-only** per the user's request — dispatch every implementer and reviewer on the Opus model.

**Goal:** A backend-agnostic analytics façade (`com.tk.analytics`): log events, revenue, and user properties through one API that fans out to any number of backend adapters, with a consent gate and loss-free pre-init buffering — unifying the monetization event stream the shipped IAP/Ads packages already produce.

**Architecture:** Approved committed spec: `docs/specs/2026-07-05-tk-analytics-design.md` (READ IT FIRST — the behavioral contracts are binding). An instance `AnalyticsService : IAnalytics` owns fan-out + a gating state machine (enabled / consent / started) + an ordered, param-preserving buffer, behind an `IAnalyticsBackend` seam. A thin static `Analytics` façade delegates to one `IAnalytics` set at bootstrap. A built-in `ConsoleAnalyticsBackend` ships in Runtime; Firebase/Adjust backends and the IAP/Ads monetization bridges ship as Samples. **Zero runtime dependencies.**

**Tech Stack:** Unity 6000.3.6f1 host, C#, NUnit EditMode. No package dependencies (typed structs, no JSON; consent is game-owned; backends are samples). Firebase/Adjust live only in Samples.

## Global Constraints

- Repo: `/Users/tolgahankurtdere/Documents/GitHub/unity-packages`, branch `main`. Base = the current `main` tip when execution begins (the commit that adds this plan, on top of spec commit `02daa1f`). New package root: `Packages/com.tk.analytics/`.
- **NEVER run Unity CLI against the host project** (the user's editor may be open — it holds a lockfile). Harness: `/private/tmp/claude-501/-Users-tolgahankurtdere-Documents-GitHub-unity-packages/125643b5-4b33-48e0-b763-cca5d06442d8/scratchpad/tk-verify`. It is already wired with `com.tk.core`, `com.tk.iap`, `com.tk.ads`, `com.tk.remoteconfig` (+ the AppLovin `unity.packages.applovin.com` and OpenUPM scoped registries that ads needs — **DO NOT remove them**). If the harness is missing (new session), recreate: `Assets/` (empty) + `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 6000.3.6f1`) + `Packages/manifest.json` with `com.unity.test-framework` 1.6.0, the five TK packages as `file:` absolute paths, `testables` listing all five, and the two scoped registries.
- Gate command (run from the harness dir; **NEVER combine `-quit` with `-runTests`**; Bash timeout 600000):
  ```bash
  /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$(pwd)/results.xml" -logFile "$(pwd)/unity.log"
  ```
  Success = exit 0 AND every `TK.Analytics.Tests` case `result="Passed"` in results.xml AND zero `error CS` / `warning CS` under `Packages/com.tk.analytics` in unity.log. Baseline before Task 1 = the current harness total after remoteconfig (**~174 — trust results.xml, not arithmetic**). Report the exact `TK.Analytics.Tests` count each task.
- package.json (exact): name `com.tk.analytics`, version `0.1.0`, displayName `TK Analytics`, description `Backend-agnostic analytics façade: log events, revenue and user properties through one API that fans out to any backend (Firebase, Adjust, …), with a consent gate and loss-free pre-init buffering — unifies the IAP/Ads monetization event stream.`, unity `6000.0`, `"dependencies": {}` (NONE), author `{ "name": "Tolga Kurtdere", "url": "https://github.com/tolgakurtdere" }`, keywords `["tk", "analytics", "firebase", "adjust", "monetization"]`. (Samples array added in Task 4.)
- Asmdefs: `TK.Analytics` (rootNamespace `TK.Analytics`, `"references": []`, `"autoReferenced": true`); `TK.Analytics.Tests` (Editor-only: `"includePlatforms": ["Editor"]`, references `["TK.Analytics", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, `"overrideReferences": true` + `"precompiledReferences": ["nunit.framework.dll"]`, `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, `"autoReferenced": false`).
- **No vendor types (`Firebase.*`, `AdjustSdk.*`) in any public API** — only inside the respective Sample adapters.
- Namespaces: `TK.Analytics` for all runtime; `TK.Analytics.Tests` for tests.
- `IAnalytics` is **main-thread-affine** (documented contract; no locking). Background-thread callers marshal first.
- Every file/folder under `Packages/com.tk.analytics` gets a committed `.meta` (the harness gate generates them). Conventional commits ending with the trailer `Co-Authored-By: Claude <noreply@anthropic.com>` — **NO model name**. Committing to `docs/` is fine; do NOT push mid-plan; do NOT commit `.superpowers/` or unrelated host churn.
- Firebase/Adjust Samples (Task 4) target the **latest stable** vendor SDKs (Firebase Unity 13.x, Adjust 5.x as of 2026-07); the version is the game's choice, not a package pin. RE-VERIFY every SDK member the sample calls against whatever SDK source is available at execution; on drift, trust the installed source and note it. Reference API shapes (from `g-brain_test_5`): `FirebaseAnalytics.LogEvent(string, Parameter[])`, `FirebaseAnalytics.EventPurchase`, `FirebaseAnalytics.ParameterTransactionID/ParameterCurrency/ParameterValue/ParameterItems/ParameterItemID/ParameterItemName/ParameterQuantity`, `FirebaseAnalytics.SetUserProperty(string,string)`, `FirebaseAnalytics.SetUserId(string)`; `AdjustSdk.Adjust.TrackEvent(new AdjustEvent(token))`, `AdjustEvent.SetRevenue(double, string)`, `Adjust.TrackAdRevenue(new AdjustAdRevenue(source){...})`.

---

### Task 1: Package skeleton + value types + backend seam + fake + harness wiring

**Files:**
- Create: `Packages/com.tk.analytics/package.json`, `Runtime/TK.Analytics.asmdef`
- Create: `Runtime/AnalyticsParam.cs`, `Runtime/AnalyticsEvent.cs`, `Runtime/AnalyticsConsent.cs`, `Runtime/Revenue/AnalyticsPurchase.cs`, `Runtime/Revenue/AnalyticsAdRevenue.cs`
- Create: `Runtime/Seams/IAnalyticsBackend.cs`
- Create: `Tests/Editor/TK.Analytics.Tests.asmdef`, `Tests/Editor/FakeAnalyticsBackend.cs`, `Tests/Editor/AnalyticsParamTests.cs`, `Tests/Editor/FakeAnalyticsBackendTests.cs`
- Modify: harness `Packages/manifest.json`

**Interfaces produced (every later task compiles against these):** the five value types, `IAnalyticsBackend`, and `FakeAnalyticsBackend`, exactly as below.

- [ ] **Step 1: package.json + both asmdefs** — exact values from Global Constraints.

- [ ] **Step 2: AnalyticsParam.cs** (full code):

```csharp
namespace TK.Analytics
{
    public enum AnalyticsParamType { String, Long, Double, Bool }

    /// <summary>
    /// SDK-agnostic analytics parameter. Allocation-free (no boxing): the value lives in one of
    /// four typed fields selected by <see cref="Type"/>. Create via the static factories.
    /// </summary>
    public readonly struct AnalyticsParam
    {
        public string Key { get; }
        public AnalyticsParamType Type { get; }
        public string StringValue { get; }   // valid when Type == String
        public long   LongValue { get; }      // valid when Type == Long
        public double DoubleValue { get; }    // valid when Type == Double
        public bool   BoolValue { get; }      // valid when Type == Bool

        private AnalyticsParam(string key, AnalyticsParamType type, string s, long l, double d, bool b)
        {
            Key = key; Type = type; StringValue = s; LongValue = l; DoubleValue = d; BoolValue = b;
        }

        public static AnalyticsParam String(string key, string value) => new(key, AnalyticsParamType.String, value, 0, 0, false);
        public static AnalyticsParam Long(string key, long value)     => new(key, AnalyticsParamType.Long, null, value, 0, false);
        public static AnalyticsParam Double(string key, double value) => new(key, AnalyticsParamType.Double, null, 0, value, false);
        public static AnalyticsParam Bool(string key, bool value)     => new(key, AnalyticsParamType.Bool, null, 0, 0, value);

        public override string ToString()
        {
            object v = Type switch
            {
                AnalyticsParamType.String => StringValue,
                AnalyticsParamType.Long   => LongValue,
                AnalyticsParamType.Double => DoubleValue,
                AnalyticsParamType.Bool   => BoolValue,
                _ => null
            };
            return $"{Key}={v}";
        }
    }
}
```

- [ ] **Step 3: AnalyticsEvent.cs** (full code):

```csharp
using System.Collections.Generic;

namespace TK.Analytics
{
    /// <summary>An event name plus its parameters — the unit that is buffered and dispatched.</summary>
    public readonly struct AnalyticsEvent
    {
        public string Name { get; }
        public IReadOnlyList<AnalyticsParam> Parameters { get; }   // never null; empty when none

        public AnalyticsEvent(string name, IReadOnlyList<AnalyticsParam> parameters = null)
        {
            Name = name;
            Parameters = parameters ?? System.Array.Empty<AnalyticsParam>();
        }
    }
}
```

- [ ] **Step 4: AnalyticsConsent.cs** (full code):

```csharp
namespace TK.Analytics
{
    /// <summary>User consent state. Default Unknown buffers events until a decision (GDPR-safe).</summary>
    public enum AnalyticsConsent { Unknown, Granted, Denied }
}
```

- [ ] **Step 5: Revenue/AnalyticsPurchase.cs** (full code):

```csharp
namespace TK.Analytics
{
    /// <summary>Neutral, package-owned purchase record. Bridges map com.tk.iap's IapPurchaseInfo into this.</summary>
    public readonly struct AnalyticsPurchase
    {
        public string ProductId { get; }
        public double Price { get; }          // localized price as double
        public string Currency { get; }       // ISO 4217; may be null
        public string TransactionId { get; }
        public int    Quantity { get; }       // defaults to 1
        public bool   IsRestore { get; }

        public AnalyticsPurchase(string productId, double price, string currency,
            string transactionId, int quantity = 1, bool isRestore = false)
        {
            ProductId = productId;
            Price = price;
            Currency = currency;
            TransactionId = transactionId;
            Quantity = quantity;
            IsRestore = isRestore;
        }
    }
}
```

- [ ] **Step 6: Revenue/AnalyticsAdRevenue.cs** (full code):

```csharp
namespace TK.Analytics
{
    /// <summary>Neutral, package-owned ad-revenue record. Bridges map com.tk.ads's AdRevenueInfo into this.</summary>
    public readonly struct AnalyticsAdRevenue
    {
        public string Format { get; }         // "banner"/"interstitial"/"rewarded"
        public string AdNetwork { get; }      // winning mediated network
        public string AdUnitId { get; }
        public double Revenue { get; }
        public string Currency { get; }       // ISO 4217 (usually "USD")
        public string Placement { get; }      // may be null

        public AnalyticsAdRevenue(string format, string adNetwork, string adUnitId,
            double revenue, string currency, string placement)
        {
            Format = format;
            AdNetwork = adNetwork;
            AdUnitId = adUnitId;
            Revenue = revenue;
            Currency = currency;
            Placement = placement;
        }
    }
}
```

- [ ] **Step 7: Seams/IAnalyticsBackend.cs** (full code):

```csharp
using System.Threading.Tasks;

namespace TK.Analytics
{
    /// <summary>
    /// Analytics backend adapter seam. The package ships no vendor backend (Firebase/Adjust are Samples)
    /// beyond the built-in ConsoleAnalyticsBackend. Implementations MUST NOT throw — the service wraps
    /// every call in try/catch, logs, and continues, so one bad backend never blocks the others.
    /// A backend with no native revenue API maps LogPurchase/LogAdRevenue onto a LogEvent internally;
    /// a backend that does not care about a call simply no-ops.
    /// </summary>
    public interface IAnalyticsBackend
    {
        string Name { get; }
        Task InitializeAsync();
        void LogEvent(AnalyticsEvent evt);
        void LogPurchase(AnalyticsPurchase purchase);
        void LogAdRevenue(AnalyticsAdRevenue adRevenue);
        void SetUserProperty(string key, string value);
        void SetUserId(string userId);
        void Flush();
    }
}
```

- [ ] **Step 8: Tests/Editor/FakeAnalyticsBackend.cs** (full code, namespace `TK.Analytics.Tests`):

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TK.Analytics;

namespace TK.Analytics.Tests
{
    /// <summary>Recording backend for deterministic EditMode tests.</summary>
    public sealed class FakeAnalyticsBackend : IAnalyticsBackend
    {
        public string Name { get; }
        public FakeAnalyticsBackend(string name = "fake") => Name = name;

        // Knobs
        public bool ThrowOnInit;
        public bool ThrowOnLogEvent;

        // Recorded
        public int InitializeCalls;
        public int FlushCalls;
        public readonly List<AnalyticsEvent> Events = new();
        public readonly List<AnalyticsPurchase> Purchases = new();
        public readonly List<AnalyticsAdRevenue> AdRevenues = new();
        public readonly Dictionary<string, string> UserProperties = new();
        public string UserId;
        /// <summary>Ordered trace of every op, for order-sensitive assertions.</summary>
        public readonly List<string> Trace = new();

        public Task InitializeAsync()
        {
            InitializeCalls++;
            if (ThrowOnInit) throw new InvalidOperationException("fake: init threw");
            return Task.CompletedTask;
        }

        public void LogEvent(AnalyticsEvent evt)
        {
            if (ThrowOnLogEvent) throw new InvalidOperationException("fake: logevent threw");
            Events.Add(evt);
            Trace.Add($"event:{evt.Name}");
        }

        public void LogPurchase(AnalyticsPurchase purchase)
        {
            Purchases.Add(purchase);
            Trace.Add($"purchase:{purchase.ProductId}");
        }

        public void LogAdRevenue(AnalyticsAdRevenue adRevenue)
        {
            AdRevenues.Add(adRevenue);
            Trace.Add($"adrevenue:{adRevenue.AdUnitId}");
        }

        public void SetUserProperty(string key, string value)
        {
            UserProperties[key] = value;
            Trace.Add($"userprop:{key}={value}");
        }

        public void SetUserId(string userId)
        {
            UserId = userId;
            Trace.Add($"userid:{userId}");
        }

        public void Flush()
        {
            FlushCalls++;
            Trace.Add("flush");
        }
    }
}
```

- [ ] **Step 9: AnalyticsParamTests.cs** (complete NUnit code, namespace `TK.Analytics.Tests`, 5 tests):
  1. `String_Factory_RoundTrips` — `AnalyticsParam.String("k","v")` → `Type==String`, `Key=="k"`, `StringValue=="v"`.
  2. `Long_Factory_RoundTrips` — `Long("n",5)` → `Type==Long`, `LongValue==5`.
  3. `Double_Factory_RoundTrips` — `Double("d",1.5)` → `Type==Double`, `DoubleValue==1.5` (use `Assert.AreEqual(1.5, p.DoubleValue, 1e-9)`).
  4. `Bool_Factory_RoundTrips` — `Bool("b",true)` → `Type==Bool`, `BoolValue==true`.
  5. `ToString_FormatsKeyEqualsValue` — `String("k","v").ToString()=="k=v"` and `Long("n",3).ToString()=="n=3"`.

- [ ] **Step 10: FakeAnalyticsBackendTests.cs** (complete NUnit code, 4 tests):
  1. `Initialize_CountsAndReadyTask` — `await new FakeAnalyticsBackend().InitializeAsync()` completes; `InitializeCalls==1`.
  2. `Initialize_ThrowKnob_Throws` — `ThrowOnInit=true` → `Assert.ThrowsAsync<InvalidOperationException>(async () => await fake.InitializeAsync())`.
  3. `LogEvent_RecordsAndTraces` — `LogEvent(new AnalyticsEvent("e"))` → `Events.Count==1`, `Trace[0]=="event:e"`; `ThrowOnLogEvent=true` → next `LogEvent` throws.
  4. `RevenueAndUser_Recorded` — `LogPurchase`/`LogAdRevenue`/`SetUserProperty("k","v")`/`SetUserId("u")`/`Flush()` → `Purchases.Count==1`, `AdRevenues.Count==1`, `UserProperties["k"]=="v"`, `UserId=="u"`, `FlushCalls==1`.

- [ ] **Step 11: harness wiring** — in the harness `Packages/manifest.json`: add `"com.tk.analytics": "file:/Users/tolgahankurtdere/Documents/GitHub/unity-packages/Packages/com.tk.analytics"` to `dependencies` and `"com.tk.analytics"` to `testables`. Leave the existing four packages and the two scoped registries untouched (analytics has zero external deps → no new registry).

- [ ] **Step 12: gate** (baseline + 9). **Step 13: commit** — `feat(analytics): add com.tk.analytics skeleton with value types, backend seam and fake`.

---

### Task 2: IAnalytics + AnalyticsService (fan-out, gating state machine, ordered buffer)

**Files:**
- Create: `Packages/com.tk.analytics/Runtime/IAnalytics.cs`, `Runtime/AnalyticsService.cs`
- Create: `Tests/Editor/AnalyticsServiceTests.cs`

**Interfaces:**
- Consumes: all value types + `IAnalyticsBackend` (Task 1), `FakeAnalyticsBackend` (Task 1, tests only).
- Produces (Task 3 consumes): `IAnalytics` (below) and `AnalyticsService(IEnumerable<IAnalyticsBackend> backends)` with read-only `AnalyticsConsent Consent` + `bool IsStarted`.

- [ ] **Step 1: IAnalytics.cs** (full code):

```csharp
using System.Threading.Tasks;

namespace TK.Analytics
{
    /// <summary>
    /// Analytics façade. Main-thread-affine (background callers marshal first). Log calls are gated by
    /// enabled/consent/started state and buffered (in order, with parameters preserved) until dispatch
    /// is allowed. Lifecycle/config (StartAsync, SetConsent, IsEnabled, Flush) is driven by the instance;
    /// the static Analytics façade exposes only the log verbs.
    /// </summary>
    public interface IAnalytics
    {
        /// <summary>Runtime kill-switch. False = inert: new ops dropped, flush paused (buffer kept).</summary>
        bool IsEnabled { get; set; }

        void LogEvent(string name);
        void LogEvent(string name, params AnalyticsParam[] parameters);
        void LogPurchase(AnalyticsPurchase purchase);
        void LogAdRevenue(AnalyticsAdRevenue adRevenue);
        void SetUserProperty(string key, string value);
        void SetUserId(string userId);

        /// <summary>true → flush the buffer and allow dispatch; false → clear the buffer and block.</summary>
        void SetConsent(bool granted);

        /// <summary>Ask each backend to flush its own native buffer (no-op if dispatch not yet allowed).</summary>
        void Flush();

        /// <summary>Initialize all backends, mark started, then flush if the gate is open. Single-flight.</summary>
        Task StartAsync();
    }
}
```

- [ ] **Step 2: AnalyticsService.cs** (full code — transcribe EXACTLY):

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Analytics
{
    /// <summary>
    /// Multi-backend analytics service. Fans every operation out to all backends, gates dispatch on
    /// enabled/consent/started, and buffers (in order, params preserved) whatever cannot dispatch yet.
    /// Each backend call is isolated in try/catch so one throwing backend never blocks the others.
    /// Main-thread-affine: no locking; background-thread callers marshal to the main thread first.
    /// </summary>
    public sealed class AnalyticsService : IAnalytics
    {
        private readonly List<IAnalyticsBackend> _backends;
        private readonly Queue<Action<IAnalyticsBackend>> _queue = new();
        private Task _startTask;
        private bool _enabled = true;

        public AnalyticsService(IEnumerable<IAnalyticsBackend> backends)
        {
            if (backends == null) throw new ArgumentNullException(nameof(backends));
            _backends = new List<IAnalyticsBackend>(backends);
        }

        public AnalyticsConsent Consent { get; private set; } = AnalyticsConsent.Unknown;
        public bool IsStarted { get; private set; }

        public bool IsEnabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (value) TryFlushQueue();   // re-enabling resumes a paused flush
            }
        }

        private bool IsDispatchAllowed => IsStarted && _enabled && Consent == AnalyticsConsent.Granted;

        // ── Ingestion ──

        public void LogEvent(string name) => Ingest(b => b.LogEvent(new AnalyticsEvent(name)));

        public void LogEvent(string name, params AnalyticsParam[] parameters)
            => Ingest(b => b.LogEvent(new AnalyticsEvent(name, parameters)));

        public void LogPurchase(AnalyticsPurchase purchase) => Ingest(b => b.LogPurchase(purchase));

        public void LogAdRevenue(AnalyticsAdRevenue adRevenue) => Ingest(b => b.LogAdRevenue(adRevenue));

        public void SetUserProperty(string key, string value) => Ingest(b => b.SetUserProperty(key, value));

        public void SetUserId(string userId) => Ingest(b => b.SetUserId(userId));

        private void Ingest(Action<IAnalyticsBackend> op)
        {
            if (!_enabled) return;                            // kill-switch: drop
            if (Consent == AnalyticsConsent.Denied) return;   // GDPR: never send
            if (IsDispatchAllowed) Dispatch(op);              // live
            else _queue.Enqueue(op);                          // buffer (consent Unknown or not started)
        }

        private void Dispatch(Action<IAnalyticsBackend> op)
        {
            for (var i = 0; i < _backends.Count; i++)
            {
                try { op(_backends[i]); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private void TryFlushQueue()
        {
            if (!IsDispatchAllowed) return;
            while (_queue.Count > 0)
                Dispatch(_queue.Dequeue());
        }

        // ── Consent ──

        public void SetConsent(bool granted)
        {
            Consent = granted ? AnalyticsConsent.Granted : AnalyticsConsent.Denied;
            if (granted) TryFlushQueue();
            else _queue.Clear();   // denied: discard buffered ops, never send
        }

        // ── Lifecycle ──

        public Task StartAsync()
        {
            if (_startTask != null) return _startTask;   // single-flight
            _startTask = StartInternalAsync();
            return _startTask;
        }

        private async Task StartInternalAsync()
        {
            for (var i = 0; i < _backends.Count; i++)
            {
                try { await _backends[i].InitializeAsync(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }

            IsStarted = true;
            TryFlushQueue();
        }

        public void Flush()
        {
            if (!IsDispatchAllowed) return;
            Dispatch(b => b.Flush());
        }
    }
}
```

- [ ] **Step 3: AnalyticsServiceTests.cs** (complete NUnit code, inject `FakeAnalyticsBackend`; `async Task` tests where `StartAsync` is awaited; `using System.Text.RegularExpressions;` + `using UnityEngine.TestTools;` for `LogAssert`). Tests (17):
  1. `Event_BeforeStart_Buffered_NotDispatched` — `SetConsent(true)`; `LogEvent("e")`; backend `Events.Count==0` (not started → buffered).
  2. `Event_AfterStartAndConsent_Dispatched` — `SetConsent(true)`; `await StartAsync()`; `LogEvent("e")`; `Events.Count==1`, `Events[0].Name=="e"`.
  3. `BufferedEvent_FlushedOnStart_WithParamsIntact` — `SetConsent(true)`; `LogEvent("e", AnalyticsParam.Long("n",5))` (buffered); `await StartAsync()`; `Events.Count==1`, `Events[0].Parameters.Count==1`, `Events[0].Parameters[0].LongValue==5`. **(reference param-loss regression.)**
  4. `Event_ConsentUnknown_BufferedEvenAfterStart_ThenFlushedOnGrant` — `await StartAsync()` (consent Unknown); `LogEvent("e")`; `Events.Count==0`; `SetConsent(true)`; `Events.Count==1`.
  5. `SetConsentFalse_ClearsBuffer_AndDropsFuture` — `LogEvent("a")` (consent Unknown → buffered); `SetConsent(false)`; `await StartAsync()`; `LogEvent("b")`; `Events.Count==0` (buffer cleared + "b" dropped as Denied).
  6. `Disabled_DropsEvent_NotBuffered` — `SetConsent(true)`; `await StartAsync()`; `IsEnabled=false`; `LogEvent("e")`; `Events.Count==0`; `IsEnabled=true`; `Events.Count==0` still (dropped, not buffered — re-enabling does not recover a dropped op).
  7. `StartAsync_InitializesAllBackends_Once_SingleFlight` — two backends; `await StartAsync()`; each `InitializeCalls==1`; `await StartAsync()` again; still `==1` each.
  8. `Purchase_BufferedThenDispatched_FieldsIntact` — `SetConsent(true)`; `LogPurchase(new AnalyticsPurchase("p",1.99,"USD","tx1"))` (buffered); `await StartAsync()`; `Purchases.Count==1`, ProductId "p", `Price==1.99` (delta 1e-9), Currency "USD", TransactionId "tx1", Quantity 1.
  9. `AdRevenue_Dispatched_FieldsIntact` — ready; `LogAdRevenue(new AnalyticsAdRevenue("banner","admob","unit1",0.01,"USD","main"))`; `AdRevenues.Count==1` + fields (Revenue delta 1e-9).
  10. `UserPropertyAndUserId_Dispatched` — ready; `SetUserProperty("k","v")`; `SetUserId("u")`; `UserProperties["k"]=="v"`, `UserId=="u"`.
  11. `Buffer_ReplaysInFifoOrder` — `SetConsent(true)`; `SetUserId("u")`; `LogEvent("e1")`; `LogEvent("e2")` (all buffered); `await StartAsync()`; `CollectionAssert.AreEqual(new[]{"userid:u","event:e1","event:e2"}, backend.Trace)`.
  12. `FanOut_AllBackendsReceive` — two backends; `SetConsent(true)`; `await StartAsync()`; `LogEvent("e")`; both `Events.Count==1`.
  13. `BackendThrowsInLogEvent_OthersStillReceive` — backend A `ThrowOnLogEvent=true`, backend B normal; `SetConsent(true)`; `await StartAsync()`; `LogAssert.Expect(LogType.Exception, new Regex("fake: logevent threw"))`; `LogEvent("e")`; B `Events.Count==1`; no exception escapes.
  14. `BackendThrowsInInit_StartCompletes_ServiceUsable` — backend A `ThrowOnInit=true`, backend B normal; `SetConsent(true)`; `LogAssert.Expect(LogType.Exception, new Regex("fake: init threw"))`; `await StartAsync()`; `IsStarted` true, B `InitializeCalls==1`; `LogEvent("e")`; B `Events.Count==1`.
  15. `Flush_CallsBackendFlush_WhenReady` — `SetConsent(true)`; `await StartAsync()`; `Flush()`; `FlushCalls==1`.
  16. `Flush_BeforeReady_NoOp` — new service; `Flush()`; `FlushCalls==0`.
  17. `Consent_Property_Reflects_Transitions` — new service `Consent==Unknown`; `SetConsent(true)` → `Granted`; `SetConsent(false)` → `Denied`.

- [ ] **Step 4: gate** (prev + 17). **Step 5: commit** — `feat(analytics): add AnalyticsService with fan-out, consent gating and loss-free buffer`.

---

### Task 3: Static Analytics façade + built-in ConsoleAnalyticsBackend

**Files:**
- Create: `Packages/com.tk.analytics/Runtime/Analytics.cs`, `Runtime/Backends/ConsoleAnalyticsBackend.cs`
- Create: `Tests/Editor/AnalyticsStaticFacadeTests.cs`, `Tests/Editor/ConsoleAnalyticsBackendTests.cs`

**Interfaces:**
- Consumes: `IAnalytics` (Task 2), all value types + `IAnalyticsBackend` (Task 1). Tests use `AnalyticsService` + `FakeAnalyticsBackend`.
- Produces: static `Analytics` (`Instance`, `HasInstance`, `SetInstance`, `ClearInstance` + delegating log verbs); `ConsoleAnalyticsBackend`.

- [ ] **Step 1: Analytics.cs** (full code):

```csharp
using UnityEngine;

namespace TK.Analytics
{
    /// <summary>
    /// Ambient static access point for analytics logging from anywhere. Set one IAnalytics at bootstrap
    /// via SetInstance; every log verb forwards to it. When no instance is set, calls are no-ops (with a
    /// one-time editor warning) so untested/isolated scenes never NullReference. Lifecycle/config lives on
    /// the instance, not here. Tests drive the instance directly and Clear between cases.
    /// </summary>
    public static class Analytics
    {
        public static IAnalytics Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        public static void SetInstance(IAnalytics instance) => Instance = instance;
        public static void ClearInstance() => Instance = null;

        public static void LogEvent(string name)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogEvent(name);
        }

        public static void LogEvent(string name, params AnalyticsParam[] parameters)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogEvent(name, parameters);
        }

        public static void LogPurchase(AnalyticsPurchase purchase)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogPurchase(purchase);
        }

        public static void LogAdRevenue(AnalyticsAdRevenue adRevenue)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.LogAdRevenue(adRevenue);
        }

        public static void SetUserProperty(string key, string value)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.SetUserProperty(key, value);
        }

        public static void SetUserId(string userId)
        {
            if (Instance == null) { WarnUnset(); return; }
            Instance.SetUserId(userId);
        }

        private static bool s_warnedUnset;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void WarnUnset()
        {
            if (s_warnedUnset) return;
            s_warnedUnset = true;
            Debug.LogWarning("[Analytics] No instance set (call Analytics.SetInstance). Calls are no-ops until one is set.");
        }
    }
}
```
NOTE: `WarnUnset` is `[Conditional("UNITY_EDITOR")]` so its call sites vanish in player builds; the method body still compiles, and `s_warnedUnset` is referenced by that always-compiled body, so no CS0169 unused-field warning. The one-time warning is a `LogWarning` (not error), so it never fails a test run even when it fires.

- [ ] **Step 2: Backends/ConsoleAnalyticsBackend.cs** (full code):

```csharp
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TK.Analytics
{
    /// <summary>
    /// Built-in, dependency-free backend that writes every call to the Unity console. Useful in-editor
    /// and for QA; add it alongside real backends or use it alone. Requires no SDK.
    /// </summary>
    public sealed class ConsoleAnalyticsBackend : IAnalyticsBackend
    {
        public string Name => "Console";

        public Task InitializeAsync() => Task.CompletedTask;

        public void LogEvent(AnalyticsEvent evt)
        {
            var sb = new StringBuilder();
            sb.Append("[Analytics] event '").Append(evt.Name).Append('\'');
            if (evt.Parameters.Count > 0)
            {
                sb.Append(" {");
                for (var i = 0; i < evt.Parameters.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(evt.Parameters[i]);
                }
                sb.Append('}');
            }
            Debug.Log(sb.ToString());
        }

        public void LogPurchase(AnalyticsPurchase p) =>
            Debug.Log($"[Analytics] purchase '{p.ProductId}' {p.Price} {p.Currency} (tx {p.TransactionId}, qty {p.Quantity}, restore {p.IsRestore})");

        public void LogAdRevenue(AnalyticsAdRevenue a) =>
            Debug.Log($"[Analytics] adRevenue {a.Format} {a.AdNetwork} {a.AdUnitId} {a.Revenue} {a.Currency} placement={a.Placement}");

        public void SetUserProperty(string key, string value) =>
            Debug.Log($"[Analytics] userProperty {key}={value}");

        public void SetUserId(string userId) =>
            Debug.Log($"[Analytics] userId={userId}");

        public void Flush() => Debug.Log("[Analytics] flush");
    }
}
```

- [ ] **Step 3: AnalyticsStaticFacadeTests.cs** (complete NUnit code; `[SetUp]` and `[TearDown]` both call `Analytics.ClearInstance()`; helper builds a ready service: `var backend = new FakeAnalyticsBackend(); var svc = new AnalyticsService(new[]{backend}); svc.SetConsent(true); await svc.StartAsync();`). Tests (5):
  1. `NoInstance_LogEvent_NoOp_NoThrow` — `Analytics.ClearInstance()`; `Assert.DoesNotThrow(() => Analytics.LogEvent("e"))`; `Analytics.HasInstance` false.
  2. `SetInstance_DelegatesLogEvent` — build ready svc; `Analytics.SetInstance(svc)`; `Analytics.LogEvent("e", AnalyticsParam.Long("n",1))`; `backend.Events.Count==1`, param preserved.
  3. `SetInstance_DelegatesRevenueAndUser` — ready svc set as instance; `Analytics.LogPurchase(...)`, `Analytics.LogAdRevenue(...)`, `Analytics.SetUserProperty("k","v")`, `Analytics.SetUserId("u")`; backend recorded each.
  4. `HasInstance_Reflects_SetAndClear` — `ClearInstance()` → `HasInstance` false; `SetInstance(svc)` → true; `ClearInstance()` → false.
  5. `ClearInstance_StopsDelegation` — set instance, log one event (recorded); `ClearInstance()`; `Analytics.LogEvent("e2")`; `backend.Events.Count==1` (no new dispatch after clear).

- [ ] **Step 4: ConsoleAnalyticsBackendTests.cs** (complete NUnit code, using `LogAssert`, 3 tests):
  1. `InitializeAsync_Completes` — `var t = new ConsoleAnalyticsBackend().InitializeAsync(); Assert.IsTrue(t.IsCompleted);`.
  2. `LogEvent_WritesToConsole` — `LogAssert.Expect(LogType.Log, new Regex("event 'e'"))`; `new ConsoleAnalyticsBackend().LogEvent(new AnalyticsEvent("e", new[]{AnalyticsParam.Long("n",3)}))`; (optionally also `Expect` the `n=3` fragment via a second regex `new Regex("n=3")`).
  3. `Flush_WritesToConsole` — `LogAssert.Expect(LogType.Log, new Regex("flush"))`; `new ConsoleAnalyticsBackend().Flush()`.

- [ ] **Step 5: gate** (prev + 8). **Step 6: commit** — `feat(analytics): add static Analytics façade and built-in ConsoleAnalyticsBackend`.

---

### Task 4: Samples (Firebase/Adjust backends + IAP/Ads bridges + demo) + docs + host wiring + final gate

**Files:**
- Create: `Packages/com.tk.analytics/Samples~/IntegrationExamples/FirebaseAnalyticsBackend.cs`, `AdjustAnalyticsBackend.cs`, `AnalyticsPurchaseReporter.cs`, `AnalyticsAdRevenueReporter.cs`, `README.md`
- Create: `Packages/com.tk.analytics/Samples~/AnalyticsDemo/AnalyticsDemo.cs`, `README.md`
- Create: `Packages/com.tk.analytics/README.md`, `CHANGELOG.md`
- Modify: `Packages/com.tk.analytics/package.json` (samples array); root `README.md` (package row + install + versioning tag); `ROADMAP.md` (mark shipped, advance the recommended-next pointer); HOST `Packages/manifest.json` (`testables` gains `com.tk.analytics`)

- [ ] **Step 1: FirebaseAnalyticsBackend.cs** (Sample, real `Firebase.Analytics` code; RE-VERIFY each member against the installed Firebase SDK — see Global Constraints). Structure: `Name => "Firebase"`. `InitializeAsync()` → `await FirebaseApp.CheckAndFixDependenciesAsync()` and mark ready (doc-comment: if the game already initializes Firebase centrally, this may just return `Task.CompletedTask`). `LogEvent(evt)` → build `Firebase.Analytics.Parameter[]` from `evt.Parameters` (switch on `AnalyticsParamType`: String→`new Parameter(key, StringValue)`, Long→`(long)`, Double→`(double)`, Bool→`BoolValue ? "true":"false"` as string, mirroring the reference) then `FirebaseAnalytics.LogEvent(evt.Name, parameters)`. `LogPurchase(p)` → **cross-platform** GA4 purchase: build an items array `new IDictionary<string,object>[]{ new Dictionary<string,object>{ {FirebaseAnalytics.ParameterItemID, p.ProductId}, {FirebaseAnalytics.ParameterQuantity, (long)p.Quantity} } }` and `FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventPurchase, new[]{ new Parameter(FirebaseAnalytics.ParameterTransactionID, p.TransactionId), new Parameter(FirebaseAnalytics.ParameterCurrency, p.Currency ?? ""), new Parameter(FirebaseAnalytics.ParameterValue, p.Price), new Parameter(FirebaseAnalytics.ParameterItems, items) })` (doc-comment: fixes the reference's iOS-only purchase omission). `LogAdRevenue(a)` → `FirebaseAnalytics.LogEvent("ad_impression", new[]{ new Parameter("ad_platform", "AppLovin"), new Parameter("ad_source", a.AdNetwork ?? ""), new Parameter("ad_unit_name", a.AdUnitId ?? ""), new Parameter("ad_format", a.Format ?? ""), new Parameter("value", a.Revenue), new Parameter("currency", a.Currency ?? "") })` (doc-comment: `ad_platform` is the mediation platform — constant "AppLovin" since com.tk.ads sources revenue from MAX; change it if you feed non-MAX ad revenue through analytics). `SetUserProperty` → `FirebaseAnalytics.SetUserProperty(key, value)`; `SetUserId` → `FirebaseAnalytics.SetUserId(userId)`; `Flush` → no-op (Firebase auto-flushes; doc-comment). Class doc: Sample; references `Firebase.Analytics`, compiles only once imported into a project that has the Firebase Analytics SDK.

- [ ] **Step 2: AdjustAnalyticsBackend.cs** (Sample, real `AdjustSdk` code; a **selective** backend). `Name => "Adjust"`. ctor takes `IReadOnlyDictionary<string,string> eventTokens = null` (event name → Adjust token) and optional `string purchaseEventToken = null`. `InitializeAsync()` → `Task.CompletedTask` (doc: the game initializes Adjust centrally via its config). `LogEvent(evt)` → if `eventTokens != null && eventTokens.TryGetValue(evt.Name, out var token)` then `Adjust.TrackEvent(new AdjustEvent(token))`, else no-op (doc: Adjust tracks milestone-token events, not every event). `LogPurchase(p)` → if `purchaseEventToken != null`: `var e = new AdjustEvent(purchaseEventToken); e.SetRevenue(p.Price, p.Currency); Adjust.TrackEvent(e);`. `LogAdRevenue(a)` → `var r = new AdjustAdRevenue("applovin_max_sdk"); r.SetRevenue(a.Revenue, a.Currency); r.AdRevenueNetwork = a.AdNetwork; r.AdRevenueUnit = a.AdUnitId; r.AdRevenuePlacement = a.Placement; Adjust.TrackAdRevenue(r);` (RE-VERIFY the exact AdjustAdRevenue setter/property names against the installed Adjust SDK; on drift, trust the source and note it). `SetUserId`/`SetUserProperty` → Adjust global callback parameters (`Adjust.AddGlobalCallbackParameter(key, value)`; for user id use a `"user_id"` key), doc-comment that Adjust has no first-class user properties. Class doc: Sample; references `AdjustSdk`, compiles only when the Adjust SDK is present.

- [ ] **Step 3: AnalyticsPurchaseReporter.cs + AnalyticsAdRevenueReporter.cs** (Samples — the monetization bridges; near-full code):

```csharp
// AnalyticsPurchaseReporter.cs
using TK.Analytics;
using TK.IAP;

namespace TK.Analytics.Samples.IntegrationExamples
{
    /// <summary>
    /// Bridges com.tk.iap's IPurchaseReporter into com.tk.analytics: a confirmed purchase flows through
    /// IAnalytics to every configured backend. Wire: iapOptions.Reporter = new AnalyticsPurchaseReporter(analytics).
    /// IPurchaseReporter.OnPurchaseConfirmed is at-least-once (crash-recovery redelivery) — dedupe downstream
    /// by TransactionId. References TK.IAP + TK.Analytics; compiles only when both packages are present.
    /// </summary>
    public sealed class AnalyticsPurchaseReporter : IPurchaseReporter
    {
        private readonly IAnalytics _analytics;
        public AnalyticsPurchaseReporter(IAnalytics analytics) => _analytics = analytics;

        public void OnPurchaseConfirmed(IapPurchaseInfo info) =>
            _analytics.LogPurchase(new AnalyticsPurchase(
                info.ProductId, (double)info.LocalizedPrice, info.IsoCurrencyCode,
                info.TransactionId, 1, info.IsRestore));

        public void OnPurchaseFailed(string productId, string reason) =>
            _analytics.LogEvent("purchase_failed",
                AnalyticsParam.String("product_id", productId),
                AnalyticsParam.String("reason", reason));
    }
}
```

```csharp
// AnalyticsAdRevenueReporter.cs
using TK.Ads;
using TK.Analytics;

namespace TK.Analytics.Samples.IntegrationExamples
{
    /// <summary>
    /// Bridges com.tk.ads's IAdRevenueReporter into com.tk.analytics: each paid impression flows through
    /// IAnalytics to every configured backend. Wire: adsOptions.RevenueReporter = new AnalyticsAdRevenueReporter(analytics).
    /// References TK.Ads + TK.Analytics; compiles only when both packages are present.
    /// </summary>
    public sealed class AnalyticsAdRevenueReporter : IAdRevenueReporter
    {
        private readonly IAnalytics _analytics;
        public AnalyticsAdRevenueReporter(IAnalytics analytics) => _analytics = analytics;

        public void OnAdRevenue(AdRevenueInfo info) =>
            _analytics.LogAdRevenue(new AnalyticsAdRevenue(
                info.Format, info.NetworkName, info.AdUnitId, info.Revenue, info.Currency, info.Placement));
    }
}
```

- [ ] **Step 4: AnalyticsDemo.cs** (Sample; editor-runnable, no SDK). A MonoBehaviour that builds `_analytics = new AnalyticsService(new IAnalyticsBackend[]{ new ConsoleAnalyticsBackend() })` in `Awake` and calls `Analytics.SetInstance(_analytics)`. `[ContextMenu]` methods: `GrantConsent` (`_analytics.SetConsent(true)`), `DenyConsent` (`SetConsent(false)`), `Start Backends` (`_ = _analytics.StartAsync()`), `Log Test Event` (`Analytics.LogEvent("demo_event", AnalyticsParam.Long("score", 42), AnalyticsParam.String("mode","hard"))`), `Log Test Purchase` (`Analytics.LogPurchase(new AnalyticsPurchase("com.demo.coins", 4.99, "USD", "demo-tx-1"))`), `Log Test Ad Revenue` (`Analytics.LogAdRevenue(new AnalyticsAdRevenue("rewarded","admob","demo_unit",0.02,"USD","level_end"))`), `Set User` (`Analytics.SetUserId("demo-user"); Analytics.SetUserProperty("tier","gold")`), `Flush` (`_analytics.Flush()`). Class doc: log an event BEFORE `Start Backends`/`GrantConsent` and watch it buffer, then flush to the console once both are done — demonstrates the consent gate + loss-free buffer.

- [ ] **Step 5: package README.md** — sections: **What's inside** (table: `IAnalytics`/`AnalyticsService`, static `Analytics`, `AnalyticsParam`/`AnalyticsEvent`, neutral `AnalyticsPurchase`/`AnalyticsAdRevenue`, `IAnalyticsBackend` + built-in `ConsoleAnalyticsBackend`); **Install** (git URL `https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.analytics` + tag pin `#com.tk.analytics/0.1.0`; NOTE: NO scoped registry and NO dependencies — install is git-URL-only; add `com.tk.analytics` to your project `testables` only if you want to run its tests); **Quickstart** (`var analytics = new AnalyticsService(new IAnalyticsBackend[]{ new ConsoleAnalyticsBackend() }); Analytics.SetInstance(analytics); await analytics.StartAsync(); analytics.SetConsent(true);` then `Analytics.LogEvent("level_start", AnalyticsParam.Long("level", 3));`); **Consent & buffering** (Unknown buffers, Granted flushes in order with params intact, Denied clears + blocks; `IsEnabled` kill-switch); **Backends** (fan-out; write your own by implementing `IAnalyticsBackend`, must-not-throw; Firebase/Adjust samples); **Monetization bridges** (the two one-liners wiring `iapOptions.Reporter` / `adsOptions.RevenueReporter`; note this package has NO dependency on iap/ads — the bridges are samples you copy; contrast with iap/ads' own direct-to-Firebase reporter samples); **Revenue** (`LogPurchase`/`LogAdRevenue` + neutral structs → native backend revenue APIs); **Gotchas** (main-thread-affine; static `Analytics` no-ops until `SetInstance`; call `StartAsync` + `SetConsent` at bootstrap; at-least-once purchase redelivery → dedupe by TransactionId; `params` array allocates — fine at event frequency). `CHANGELOG.md`: keep-a-changelog `## [0.1.0] - 2026-07-05` with the feature list.

- [ ] **Step 6: package.json samples array** — `[{ "displayName": "Analytics Demo", "description": "Editor-runnable demo on the built-in Console backend with ContextMenu triggers and the consent/buffer flow.", "path": "Samples~/AnalyticsDemo" }, { "displayName": "Integration Examples", "description": "Firebase and Adjust backend adapters plus IAP/Ads monetization bridge reporters.", "path": "Samples~/IntegrationExamples" }]`.

- [ ] **Step 7: root README + ROADMAP + host wiring** — root `README.md`: add a `com.tk.analytics` row (0.1.0, Unity 6000.0+, no registries/deps) to the package table + Shipped table, its install URL, and `com.tk.analytics/0.1.0` in the Versioning tag list. `ROADMAP.md`: move `com.tk.analytics` from "Candidate new packages" (#1) into the **Shipped** table; renumber the remaining candidates and update the "recommended next" pointer to `com.tk.audio`. HOST `Packages/manifest.json`: add `"com.tk.analytics"` to `testables` (the embedded package resolves automatically; no registry needed).

- [ ] **Step 8: final gate** (all `TK.Analytics.Tests` green, zero `com.tk.analytics` warnings; `git status` clean apart from known host churn — commit `Packages/packages-lock.json` if it records the new embedded package). Verify `Samples~` files are git-tracked (the `!*~/` .gitignore rule): `git check-ignore -v Packages/com.tk.analytics/Samples~/IntegrationExamples/FirebaseAnalyticsBackend.cs` must report **NOT ignored**. **Step 9: commit** — `docs(analytics): add samples, package docs and host wiring`. (Push + the `com.tk.analytics/0.1.0` tag happen AFTER the final whole-branch review, in the finishing step.)

---

## Notes for the executor

- **Opus-only** for every implementer and reviewer subagent.
- The spec (`docs/specs/2026-07-05-tk-analytics-design.md`) is the binding contract; if code and spec disagree, stop and reconcile before proceeding.
- Between tasks: two-stage review (per-task reviewer + a whole-branch review at the end) per subagent-driven-development. The whole-branch review must confirm: zero runtime deps in package.json; no vendor types in public API; the gating precedence (disabled→drop / denied→drop-and-clear / ready→dispatch / else→buffer) matches the spec; the buffer preserves params AND order; backend isolation holds; the static façade is null-safe.
- Do NOT push or tag mid-plan. Report the exact `TK.Analytics.Tests` count after every gate.
