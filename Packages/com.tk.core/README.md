# TK Core

Reusable core systems for Unity games: a save system, a UI framework (layouts, popups, navigation stack, sliding tab bar, busy overlay), an app flow layer with level progression, and small utilities. Each module lives in its own assembly and can be used à la carte.

## What's inside

| Module | Asmdef | What it gives you |
| --- | --- | --- |
| Utilities | `TK.Core.Utilities` | Small dependency-free helpers: object pooling, ref-counted locks, a shared bool, renderer color utilities. |
| Save | `TK.Core.Save` | `ISaveSystem` abstraction + a `PlayerPrefsJsonSaveSystem` implementation, so save/load can be swapped (PlayerPrefs, file, cloud) without touching game code. |
| UI | `TK.Core.UI` | `UIManager`, `LayoutBase`/`PopupBase`, a navigation/back-button stack, an Addressables-backed `UICatalog`, a busy/task overlay, a pluggable `IUITransition` (dependency-free default, PrimeTween/DOTween adapters available as samples), and a config-driven sliding tab bar (`TabBarView` + `LayoutSlideNavigator`, see below). |
| App | `TK.Core.App` | `AppRootBase`/`AppFlowBase` composition roots (level-free / level-based), `AppContext` service registry, `NavigationGate` transition lock, `LevelProgressService` (index-based level progression), `SceneLoader`, `AppBootstrapper`, and an `AppLifecycleRelay` for pause/focus/quit events. |

## Install

Add via Package Manager → "Install package from git URL":

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core
```

To pin a specific released version, add the version tag:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core#com.tk.core/0.4.0
```

To see this package's EditMode tests in your own project's Test Runner, add `"testables": ["com.tk.core"]` to your project's `Packages/manifest.json`.

## Quickstart

Subclass `AppFlowBase` to define what "show the menu" and "start level N" mean for your game:

```csharp
using TK.Core.App;
using UnityEngine;

public class MyGameFlow : AppFlowBase
{
    [SerializeField] private int levelCount = 10;

    protected override int LevelCount => levelCount;

    protected override async Awaitable ShowMenuAsync()
    {
        // e.g. UIManager.Instance.ShowLayoutAsync<MenuLayout>("Menu");
        await Awaitable.NextFrameAsync();
    }

    protected override async Awaitable StartLevelAsync(int levelIndex)
    {
        await SceneLoader.LoadGameAsync($"Level_{levelIndex}");
        // e.g. UIManager.Instance.ShowLayoutAsync<GameplayLayout>("Gameplay");
    }

    // Called from a Win popup, a level-select button, etc.
    public void OnWinButtonPressed() => _ = PlayNextLevelAsync();
    public void OnRetryButtonPressed() => _ = RetryLevelAsync();
    public void OnMenuButtonPressed() => _ = ReturnToMenuAsync();
}
```

`AppFlowBase` handles the transition lock (`IsTransitioning`/`CanNavigate`, drops re-entrant navigation calls), the boot policy on `Start` (override `TryGetResumeState` to resume a session, or `OnBootAsync` to replace resume-or-menu entirely), and wiring up `AppContext` + `LevelProgressService` in `Awake`.

Two things `AppFlowBase` deliberately does **not** own:

- **UI.** `ShowMenuAsync` is a semantic state hook, not a UI call — `TK.Core.App` never references
  `TK.Core.UI`; implement it with whatever UI you use (or none). `AppFlowBase` itself targets
  **level-based games**: if yours isn't (endless, one-run arcade), subclass `AppRootBase` instead —
  see the adoption tiers below.
- **Scene topology.** The Splash → Main → Game additive layout is opt-in at three levels: the flow
  base never calls `SceneLoader`; `AppBootstrapper` is optional (and its scene names are serialized
  fields); every `SceneLoader` method takes scene-name parameters.

## App adoption tiers

The App module is opt-in at three tiers — each is a superset of the previous, and none forces the
next:

| Tier | You use | When |
| --- | --- | --- |
| **`AppContext` only** | `new AppContext(save)` + `Register`/`Get`, plus `AppLifecycleRelay` added manually | You want the service container + lifecycle events and nothing else — any game shape, your own root. |
| **`AppRootBase`** | Subclass; override `OnBootAsync()` (and optionally `CreateSaveSystem`/`RegisterServices`) | You also want the save seam, service registration, relay wiring, and the `RunTransitionAsync` transition lock — but **no level concepts** (endless / one-run games). Define your own verbs (e.g. `StartNewRunAsync`) over the inherited lock. |
| **`AppFlowBase`** | Subclass; override `ShowMenuAsync`/`StartLevelAsync`/`LevelCount` | Level-based games: adds `LevelProgress`, the Play/Retry/Return verbs, resume, and the level lifecycle hooks. |

`NavigationGate` — the drop-on-reentry transition lock behind `RunTransitionAsync` — is also a
standalone class: construct one directly in composition-first setups that skip the bases entirely.

**UIManager scene setup:** add a `UIManager` component to a persistent scene object, assign its layout/popup/task-overlay `RectTransform` containers, and create a `UICatalog` asset (`Create → TK → UI Catalog`) with your layout/popup Addressable references, then assign it to `UIManager.Catalog`. String-keyed APIs (`ShowLayoutAsync<T>(string key)`, `ShowPopupAsync<T>(string key)`) resolve against that catalog.

**Back input:** Escape / gamepad East / the Android back button pop the top of the navigation stack. `UIManager.BackInputEnabled` is the game's durable master switch (the package never writes it — turn it off and it stays off); flows that must not be interrupted suspend back input temporarily via the ref-counted `PushBackInputSuppression()`/`PopBackInputSuppression()` pair instead. The tab bar does this automatically around slides.

## Tab bar

A config-driven sliding tab bar for main-menu-style screens (Home / Shop / Daily …). Layout instances are loaded once and slid horizontally instead of re-instantiated; a tap mid-slide retargets the motion from wherever it is.

- **`TabBarConfig`** (`Create → TK → UI Tab Bar Config`): ordered `{layoutKey, label, icon}` entries — keys usually match your `UICatalog` layout keys, the icon is optional and per-tab — plus `TabTransitionSettings` (min/max duration, extra-step multiplier, easing, unscaled time).
- **`TabBarView`**: a persistent shell that lives *outside* every layout. Builds one button per entry from a serialized template and raises `TabSelected(layoutKey)`; switching layouts is your composition layer's job. Button visuals go through `ITabButtonPresenter` — put your own presenter on the template to replace them without touching `TabBarView`. Two are included:
  - `DefaultTabButtonPresenter` — minimal color swap (plus the config icon when an icon `Image` is assigned).
  - `AnimatedTabButtonPresenter` — the full selected-state treatment: background sprite swap, bottom-pivot scale pop with a soft out-back curve, layout-aware width growth, and a label that fades in only on the selected tab. Curve, durations, sprites, colors, and widths are all serialized; subclass and override `ApplyProgress` to add channels on top of its timing/reentrancy engine. **Bar setup it expects:** a `HorizontalLayoutGroup` on the button container (child alignment Lower Center, padding for edge safety) and, when width animation is wanted, a `LayoutElement` on the button template — width animates via `preferredWidth`, so the layout group makes room and the growing button can never overlap neighbors or leave the screen. Template pivot must be `(0.5, 0)` so scale grows up + sideways only (the presenter warns when it isn't).
- **`LayoutSlideNavigator`**: registry of loaded layouts + the slide engine. `SlideThroughAsync(orderedLayouts, startPosition, targetIndex, settings, shouldInterrupt)` slides through every layout between the (possibly fractional) start position and the target in one continuous motion. `SetLayoutsInteractable(false)` blocks content input on every registered layout via `UIBase.SetRaycastsBlocked` — the tab bar itself stays live, so rapid taps retarget instead of hitting buttons on a half-slid screen.
- **`IOrderedTabTransition`**: the animation seam (`DefaultOrderedTabTransition` moves the strip with the configured easing). `shouldInterrupt` is polled once per frame *before* positions are applied; on interrupt the reached fractional position is reported via `TabTransitionResult.InterruptedAt` so the caller can retarget from it.
- **Tween-library adapters**: the `PrimeTween Tab Bar Animations` and `DOTween Tab Bar Animations` samples (Package Manager → TK Core → Samples) ship an animated button presenter and an `IOrderedTabTransition` adapter per library — copied into your project on import, yours to restyle.

Behavioral contracts (play-mode verified in a shipping consumer; the EditMode tests pin them):

- **Interrupted slides do not settle.** Layouts keep their offsets so the next slide retargets seamlessly from the fractional position. Whoever *abandons* navigation (cancellation, or a failed load with no follow-up request) owns the cleanup: `SettleAsync()` re-centres the last fully shown layout and hides the rest.
- **Completed slides hide every registered layout except the target** — not just the slide range. That is the load-bearing cleanup for layouts left offset by earlier interrupts; do not "optimize" it away.
- **`Current` only ever points at a layout that fully arrived** (or was adopted via `SetCurrent`), never at an abandoned slide target.
- **Entries from boot or a non-tab screen** go through `UIManager.ShowLayout(...)` as usual, then `SetCurrent(target, index)` adopts the result into the navigator.

One piece intentionally stays in your game: the single-flight request coalescing (latest-tap-wins with a generation counter bumped by every request *and* cancellation, content input locked around the whole sequence including chained retargets, settle-on-abandon in a `finally`). It glues these seams to your layout loading/binding, so its shape is game-specific — implement it in your composition layer against the contracts above.

## À la carte

Every module is its own asmdef, so you can reference just what you need:

- `TK.Core.Utilities` has no dependencies.
- `TK.Core.Save` has no dependencies.
- `TK.Core.UI` depends on `TK.Core.Utilities` (plus `UnityEngine.UI`, Input System, Addressables).
- `TK.Core.App` depends only on `TK.Core.Save` — **it does not depend on `TK.Core.UI`**. You can use the app flow layer with your own UI, or the UI framework without the app flow layer.

## Extending

- **Custom popup transitions:** override `PopupBase.CreateTransition()` to swap the dependency-free `DefaultPopupTransition` for a tween-library adapter. See the `PrimeTween Popup Transition` and `DOTween Popup Transition` samples (Package Manager → TK Core → Samples) for `IUITransition` implementations you can copy into your project and adapt.
- **Custom save backend:** override `AppFlowBase.CreateSaveSystem()` to return your own `ISaveSystem` (cloud save, file-based, etc.) instead of the default `PlayerPrefsJsonSaveSystem`.
- **Custom boot policy:** override `AppFlowBase.OnBootAsync()` to boot into something other than resume-or-menu — straight into a level, a consent/tutorial flow first, etc. The public verbs (`PlayCurrentLevelAsync`, ...) are safe to call from it.
- **Multiple progression tracks / advance policy:** construct extra `LevelProgressService` instances with distinct `saveKey`s (e.g. Main + Master tracks; register them in `RegisterServices`), and/or pass `LevelAdvancePolicies.Clamp` — or your own `LevelAdvancePolicy` delegate — instead of the default wrap-after-last.
- **Lives/energy gates and pre-level interstitials:** override `AppFlowBase.OnBeforeLevelStartAsync(levelIndex)` — it runs inside the transition lock before every level entry the base initiates (the Play/Retry verbs and boot-resume); return `false` to veto the start (e.g. out of lives → show the refill popup and return `false`). No verb overriding needed.
- **Post-level flows (interstitial, reward grant, autosave):** override `AppFlowBase.OnAfterLevelEndAsync(result)` — it runs right after the synchronous `OnGameEnded` hook on every game end.
- **Game services:** override `AppFlowBase.RegisterServices(AppContext context)` to `context.Register<T>(...)` your own services (analytics, ads, IAP, ...), then resolve them elsewhere with `context.Get<T>()`/`context.TryGet<T>()`.

## Gotchas

`TK.Core.App.AppContext` collides with `System.AppContext` (a BCL type). If a file needs both `using System;` and `using TK.Core.App;`, disambiguate with:

```csharp
using AppContext = TK.Core.App.AppContext;
```
