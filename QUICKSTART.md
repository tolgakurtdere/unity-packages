# Quickstart — Using the TK packages in a new project

An **incremental-adoption** guide. You do **not** install everything at once: start with
`com.tk.core` (save + UI + app flow), then add each other package the day you actually need it.
None of the packages force the others — the only hard dependency is `com.tk.iap → com.tk.core`.

Companion docs: [README.md](README.md) is the install index (every git URL + tag); [ROADMAP.md](ROADMAP.md)
is the planned per-package feature list.

## The golden rules

- **Install by git URL, pin to a tag.** `main` moves as packages evolve — always append
  `#com.tk.<pkg>/<version>` for anything past local experimentation.
- **Add packages incrementally.** Each is standalone except `com.tk.iap`, which needs `com.tk.core`.
- **`AppContext` is the spine.** Construct each package's service **once** at your composition root,
  `Register` it, and resolve it by constructor-injecting downward — not as a service locator reached
  from deep gameplay code.
- **Everything is main-thread-affine.** Call every service from Unity's main thread; marshal
  background SDK callbacks first.
- **The Editor can't exercise the real vendor surfaces.** Real ads (MAX), the real store (IAP),
  device notifications, and configured Unity Localization only run on-device / in a configured
  project. See [Reality check](#reality-check-verified-vs-not) at the end.

## Prerequisites

- **Unity 6000.0+** (only `com.tk.toolbar` needs 6000.3+).
- Install through **Package Manager → Add package from git URL**.
- **Pinning:** append `#com.tk.<pkg>/<version>` to the git URL (e.g. `…?path=Packages/com.tk.core#com.tk.core/0.2.0`).
- **Running a package's tests in your project:** add its name to `"testables"` in
  `Packages/manifest.json`, e.g. `"testables": ["com.tk.core", "com.tk.iap"]`, then open
  `Window → General → Test Runner` (EditMode).

---

## Stage 1 — Start here: `com.tk.core`

The foundation: a save system, a UI framework (layouts, popups, back-stack, busy overlay), an app-flow
layer, and utilities. Four independent asmdefs — reference only what you use
(`TK.Core.Save`, `TK.Core.UI`, `TK.Core.App`, `TK.Core.Utilities`).

**Install** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core#com.tk.core/0.2.0
```

### 1a. The composition root (your integration hub)

Everything you adopt later gets constructed and registered in one place. Create a single persistent
root object. This is the pattern the rest of the guide extends — **adopting a package later = adding a
couple of lines here**, nothing more.

```csharp
using TK.Core.App;
using TK.Core.Save;
using UnityEngine;
using AppContext = TK.Core.App.AppContext; // disambiguate from System.AppContext (see Gotchas)

public class GameRoot : MonoBehaviour
{
    public static AppContext Context { get; private set; }

    private async void Awake()
    {
        // One save system for the whole app (swap for file/cloud later — it's an ISaveSystem).
        var save = new PlayerPrefsJsonSaveSystem();

        // The service container. Construct each package's service here and Register it.
        Context = new AppContext(save);

        // Stage 1: just core — nothing else registered yet.
        // As you adopt packages, add `Context.Register(...)` lines below, then init them.

        await InitializeServicesAsync();
    }

    // Services that need async init are awaited here (construct+Register above stays synchronous
    // so they're resolvable immediately). Never block a critical path on a vendor init that can
    // hang with no network — gate UI on each service's State/readiness instead.
    private async Awaitable InitializeServicesAsync()
    {
        await Awaitable.NextFrameAsync();
    }
}
```

Resolve anywhere at your root with `GameRoot.Context.Get<T>()` (throws if unregistered) or
`TryGet<T>(out var svc)`. `Register<T>` is one-instance-per-type; a duplicate throws.

> **Level-based game?** `com.tk.core` also ships `AppFlowBase` — a batteries-included composition root
> with a transition lock, level progression, and save-resume. Subclass it and override
> `RegisterServices(AppContext context)` (the hook that replaces `GameRoot.Awake` above),
> `ShowMenuAsync()`, and `StartLevelAsync(int)`. Not level-based (endless / one-run)? Subclass
> `AppRootBase` instead — the same wiring and transition lock, no level API. See the
> [com.tk.core README](Packages/com.tk.core/README.md)'s "App adoption tiers".
> Whichever root you use, the `Register`/`Get` calls in this guide are identical.

### 1b. UI setup

1. Add a `UIManager` component to a persistent scene object.
2. Assign its layout / popup / task-overlay `RectTransform` containers in the Inspector.
3. Create a catalog: **Assets → Create → TK → UI Catalog**, add your layout/popup Addressable
   references, and assign it to `UIManager.Catalog`.
4. Show screens by key:

```csharp
await UIManager.Instance.ShowLayoutAsync<MenuLayout>("Menu");
await UIManager.Instance.ShowPopupAsync<SettingsPopup>("Settings");
```

Custom popup transitions: override `PopupBase.CreateTransition()` (the `PrimeTween` / `DOTween`
adapters ship as samples you copy).

### 1c. Save

`ISaveSystem` is injected everywhere save is needed, so the backend is swappable. The default is
`PlayerPrefsJsonSaveSystem` (synchronous, JSON-over-PlayerPrefs). Later packages reuse the **same**
`AppContext.SaveSystem` instance — don't construct a second one.

**Gotcha:** `TK.Core.App.AppContext` collides with the BCL `System.AppContext`. In any file that uses
both `using System;` and the app namespace, alias it: `using AppContext = TK.Core.App.AppContext;`.

---

## Add when needed

Each section below is self-contained: **when to add**, **install**, **prerequisites**, the **two lines**
you add to `GameRoot` (or `RegisterServices`), and the **one gotcha** most likely to bite. Skip any you
don't need.

### `com.tk.iap` — in-app purchases

**When:** you sell products (coins, remove-ads, VIP). **Requires `com.tk.core`** (installed above).

**Install** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.iap#com.tk.iap/0.1.1
```

**Setup:** create a catalog — **Assets → Create → TK → IAP Catalog** — and fill in entries
(internal id, store id, product type, item payloads).

**Wire** (in `GameRoot.Awake`, before init):

```csharp
// Register item handlers BEFORE InitializeAsync — init may re-deliver a pending order from a crash.
var iap = new IapService(catalog, save);                 // reuse the app's save system
iap.RegisterItemHandler("coins", (amount, ctx) => wallet.Add(amount));
Context.Register(iap);
```

```csharp
// in InitializeServicesAsync — but never block gameplay on this (it can hang with no network):
await iap.InitializeAsync();
```

Purchase buttons are drop-in: add `IapPurchaseButton` to a `Button`, set its `productId`; it resolves
localized pricing from `IapService.Instance` and calls `Purchase` on click. Non-consumables double as
entitlements — `IapService.Instance.Entitlements.Subscribe("remove_ads", …)`.

**Gotcha:** one service per gateway — to retry a failed init, construct a **fresh** `IapService`
(subscriptions are never removed). Put permanent grants (remove-ads) in **NonConsumable** products;
an entitlement bundled in a consumable is lost on reinstall.

### `com.tk.ads` — banner / interstitial / rewarded

**When:** you monetize with ads. Standalone (no dependency on other `com.tk.*`). **Needs two scoped
registries.**

**Install — Step 1:** merge these into `Packages/manifest.json` `scopedRegistries` (Unity reads only
one `scopedRegistries` key — merge, don't replace):

```json
"scopedRegistries": [
  {
    "name": "AppLovin MAX Unity",
    "url": "https://unity.packages.applovin.com",
    "scopes": ["com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp"]
  },
  {
    "name": "OpenUPM",
    "url": "https://package.openupm.com",
    "scopes": ["com.google.external-dependency-manager"]
  }
]
```

**Install — Step 2** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.ads#com.tk.ads/0.1.2
```

**Setup:** create **Assets → Create → TK → Ads Settings** (ad unit ids per platform/format). Configure
your SDK key + consent flow in the **AppLovin Integration Manager** (editor-only).

**Wire** (in `GameRoot`):

```csharp
var ads = new AdsService(settings, new AdsOptions
{
    AudioMuteSetter = muted => AudioListener.pause = muted,
    // Optional: gate interstitial/banner behind a purchase (rewarded is never gated, by design):
    // ShouldShowInterstitial = () => !iap.Entitlements.Has("remove_ads"),
});
Context.Register(ads);
// in InitializeServicesAsync: await ads.InitializeAsync();
```

```csharp
ads.ShowBanner();                                        // intent-based; shows once loaded
var shown  = await ads.ShowInterstitialAsync("level_complete");
var result = await ads.ShowRewardedAsync("extra_life");  // RewardedResult.Rewarded / Cancelled / …
```

**Gotcha:** **MAX shows no ads in the Editor** (no fill, no native views) — use the `AdsDemo` sample's
fake gateway in-editor, and AppLovin's Mediation Debugger on-device to verify real fill.

### `com.tk.remoteconfig` — typed remote config

**When:** you want to tune values (prices, pacing, flags) without shipping a build. Standalone; no
scoped registry. Ships **no** backend — Firebase adapter is a sample; you can implement any backend
against one small interface (`IRemoteConfigBackend`).

**Install** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.remoteconfig#com.tk.remoteconfig/0.1.0
```

**Wire** (declare params **before** init so defaults register):

```csharp
var rc = new RemoteConfigService(myBackend);             // e.g. the FirebaseBackend sample
var startingCoins = rc.Int("starting_coins", 100);       // reads return the default until first fetch
Context.Register(rc);
// in InitializeServicesAsync: await rc.InitializeAsync();
```

Reads never throw/block; before init they return the declared default. Prefer **one JSON object per
domain** via `rc.GetObject<T>("economy_config", new EconomyConfig())` (Newtonsoft) over many scalar
keys.

**Feeds IAP/Ads:** the `IntegrationExamples` sample ships one-line bridges —
`AmountResolver = new RcIapAmountResolver(rc)` and `PacingResolver = new RcAdsPacingResolver(rc)` — that
back their resolver seams from live config.

**Gotcha:** a backend's `TryGet*` must **never throw** — the reads have no try/catch and rely on a
`false` return to fall back to the default.

### `com.tk.analytics` — one analytics pipeline

**When:** you want events/revenue/user-properties fanned out to backends (Firebase, Adjust, …) through
one API with a consent gate. Standalone; **zero dependencies** (backends + IAP/Ads bridges are samples).

**Install** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.analytics#com.tk.analytics/0.1.0
```

**Wire:**

```csharp
var analytics = new AnalyticsService(new IAnalyticsBackend[] { new ConsoleAnalyticsBackend() });
Analytics.SetInstance(analytics);                        // static façade — log from anywhere
Context.Register(analytics);
// in InitializeServicesAsync:
//   await analytics.StartAsync();
//   analytics.SetConsent(true);   // after your consent flow — both gates required before dispatch
```

```csharp
Analytics.LogEvent("level_start", AnalyticsParam.Long("level", 3));
```

Everything logged before `StartAsync` + `SetConsent` is **buffered in order**, not lost (GDPR-safe).

**Feeds from IAP/Ads:** the sample bridges forward monetization events —
`iapOptions.Reporter = new AnalyticsPurchaseReporter(analytics)` and
`adsOptions.RevenueReporter = new AnalyticsAdRevenueReporter(analytics)`. Wire **one** path per producer
(these bridges **or** IAP/Ads' own direct-to-Firebase reporters, not both).

**Gotcha:** the static `Analytics` façade no-ops (one editor warning) until `SetInstance` — wire it at
bootstrap.

### `com.tk.notification` — local notifications

**When:** you want re-engagement reminders (1/3/7/14/30-day funnel), etc. Standalone; its one
dependency (`com.unity.mobile.notifications`) is on Unity's default registry — no scoped registry.
Local-only (no push).

**Install** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.notification#com.tk.notification/0.1.0
```

**Wire:**

```csharp
var notifications = new NotificationService(new UnityMobileNotificationBackend());
notifications.RegisterChannel(new NotificationChannel("main", "General")); // Android channel
Context.Register(notifications);
// later (after permission): notifications.ScheduleAll(myReminderList);
```

```csharp
bool granted = await notifications.RequestPermissionAsync();  // OS prompt (Android 13+/iOS)
```

`ScheduleAll` is **cancel-all-then-reschedule** — call it each launch with your full current set and the
OS holds exactly that (no drift). `DeliveryTime` is absolute device-local wall-clock:
`DateTime.Now.AddDays(3)`. Optional quiet hours: `notifications.QuietHours = new QuietHoursSettings(true, 23, 7)`.

**Gotcha:** on non-mobile targets (incl. Editor) **every call is a safe no-op** — write the flow once,
no `#if` at call sites. Re-declare the full set each launch; don't incrementally add (duplicates).

### `com.tk.localization` — fonts + RTL + locale

**When:** you ship multiple languages, especially with per-locale fonts or RTL (Arabic/Farsi).
Standalone; deps (`com.unity.localization`, `com.unity.ugui`) are default-registry. **You set up your
own Unity Localization project** (Settings, locales, string tables, font assets) — this package is the
mechanism, not the content.

**Install** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.localization#com.tk.localization/0.1.0
```

**Setup:** `Edit → Project Settings → Localization` (create Settings, add locales, a String Table
Collection). Create a font map: **Assets → Create → TK/Localization/Locale Font Map** and assign its
**Fallback** (mandatory — an unassigned Fallback is the one case `Resolve` returns null).

**Wire:**

```csharp
var locales = new LocaleService(new PlayerPrefsLocalePersistence("your.game.localeCode"));
Context.Register(locales);
// in InitializeServicesAsync: await locales.InitializeAsync(); // saved → device → first available
```

On a `TMP_Text`, add **`Add Component → TK Localization → Localized TMP Text`**, assign a Localized
String + a LocaleFontMap. That's it — string + RTL + font follow the selected locale, **no
`LocalizeStringEvent` wiring**. Switch at runtime: `locales.SetLocale("tr")` — every component updates
itself.

**Gotcha:** `LocalizedTmpText` self-subscribes to `LocalizedString.StringChanged` — do **not** also add
a `LocalizeStringEvent` to the same field. Use `FontLocalizer` (font-only) when the string is managed
elsewhere.

### `com.tk.toolbar` — editor quality-of-life

**When:** anytime — it's editor-only, zero-config, zero wiring, no runtime impact. Needs **Unity 6000.3+**.

**Install** (pinned):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.toolbar#com.tk.toolbar/0.1.0
```

Adds a time-scale slider (+ reset) and configurable scene-switch buttons to the main toolbar. Configure
scene buttons via its `ToolbarSettings` asset.

---

## Wiring the monetization stack together

Once you have several packages, three seams connect them (all optional, all one line). Set them where
you construct each service in `GameRoot`:

- **Remote config → IAP/Ads:** `iapOptions.AmountResolver = new RcIapAmountResolver(rc)` ·
  `adsOptions.PacingResolver = new RcAdsPacingResolver(rc)`.
- **IAP/Ads → Analytics:** `iapOptions.Reporter = new AnalyticsPurchaseReporter(analytics)` ·
  `adsOptions.RevenueReporter = new AnalyticsAdRevenueReporter(analytics)`.
- **IAP entitlement → Ads gating:** `adsOptions.ShouldShowInterstitial = () => !iap.Entitlements.Has("remove_ads")`
  (and `ShouldShowBanner` — rewarded is deliberately never gated).

The bridge classes ship as **samples** in the relevant packages (import `Integration Examples` and copy
the one you need) — there's no cross-package hard dependency.

## Reality check (verified vs not)

Be clear-eyed about what has and hasn't run:

- **Unit-tested pure logic:** 264 EditMode tests across the packages (policy, state machines, catalogs,
  parsing, RTL, selection) run green in the dev harness.
- **Never run on a device / store / configured project:** the IAP store flow, real MAX ad delivery,
  device notifications, all `Samples~`, and Unity Localization's runtime events are **review-verified
  only** — never compiled/exercised in the harness. Your **first integration is their first real run**;
  budget time to validate each on-device (store sandbox, MAX Mediation Debugger, a real notification
  fire, an actual locale switch).
- **No `LICENSE` file yet.** The repo is public, but with no license it defaults to *all rights
  reserved* — fine for your own projects; add a license before inviting outside reuse.

## Handing this to an agent

These packages are meant to be set up by an agent working **in your game project**. That agent has no
memory of how they were built — the docs carry everything. Point it at this repo
(`https://github.com/tolgakurtdere/unity-packages`) and have it read the files it needs; the
[README.md](README.md) package table plus each package's *"when to add"* note here are enough for it to
match your game's needs to the right packages.

**If you already know which package you want** — point it straight at that package:

> Read `QUICKSTART.md` and `Packages/com.tk.ads/README.md` from
> `https://github.com/tolgakurtdere/unity-packages`, then install and wire `com.tk.ads` into my
> project's composition root.

**If you want the agent to decide which packages fit** (recommended for a fresh project):

> I want to adopt reusable systems from `https://github.com/tolgakurtdere/unity-packages`.
>
> 1. First read **my project's own docs** (`<point to your design/GDD docs>`) to understand what this
>    game actually needs.
> 2. Then read that repo's `README.md` (package catalog), `QUICKSTART.md` (when to add each + wiring),
>    and `ROADMAP.md` (what's planned but **not** built yet — don't assume anything outside the catalog
>    exists).
> 3. Propose a **phased** adoption plan: which package(s) to add first (usually `com.tk.core`), which to
>    defer, and which don't apply — one line of reasoning each. **Don't install anything yet.**
> 4. After I approve, set up **Phase 1 only**, pinning to version tags, then stop so I can test on-device
>    before we do the next phase.

The phased + approve-first shape keeps you in control and matches how these packages are designed to be
adopted — one working slice at a time, not all at once.
