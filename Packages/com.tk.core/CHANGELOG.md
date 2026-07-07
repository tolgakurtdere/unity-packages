# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.1] - 2026-07-07

Hardening pass over the 0.3.0 tab bar from its post-release review. No API breaks; additive surface only.

### Added

- **UI**: back-input control on `UIManager`, two independent levels — `BackInputEnabled` (default `true`) is the game's durable master switch and is never written by the package, while `PushBackInputSuppression()`/`PopBackInputSuppression()` (ref-counted, backed by `RefCountLock`) serve temporary flow windows; `IsBackInputActive` combines both and gates the Escape / gamepad East / Android back polling. `LayoutSlideNavigator.SetLayoutsInteractable` suppresses via push/pop for the slide window (raycast blocking cannot cover key input — a back press mid-slide used to route to a half-slid layout in the navigation stack), so a game that disabled back handling entirely stays disabled through tab navigation.
- **UI**: `TabBarView` errors loudly in `Start` when an `Awake` override skipped `base.Awake()` (Unity invokes only the most-derived `Awake`, so the forgotten base call otherwise means a silently empty tab bar — the exact hazard of thin subclass shims).
- **UI samples**: `PrimeTween Tab Bar Animations` + `DOTween Tab Bar Animations` — per library, an animated `ITabButtonPresenter` (background/label color crossfade + scale pop; honors the `instant` flag the color-swap default ignores) and an `IOrderedTabTransition` adapter. The adapters let the tween library drive a linear 0→1 progress while `TabTransitionSettings.Evaluate` shapes it, so motion matches the built-in default exactly and the full interrupt contract is preserved (poll-before-apply, reached-position reporting, mid-motion teardown guard). Samples are copied into `Assets/` on import and are yours to edit.

### Fixed

- **UI**: `TabBarView.Awake` now skips config entries with an empty or duplicate `layoutKey` with an error (previously a duplicate silently orphaned the first button — its presenter never received `SetSelected` updates again; Unity's grow-a-list-by-duplicating-the-previous-entry makes this an easy authoring slip).
- **UI**: `DefaultOrderedTabTransition` detects a layout destroyed while the motion runs (scene teardown, released tab) and reports `InterruptedAt(currentPosition)` instead of throwing `MissingReferenceException` mid-apply.
- **UI**: `LayoutSlideNavigator.Register` warns when a layout's parent differs from the captured `Container` — slide math assumes one shared container, and a mismatch previously misbehaved silently.
- **UI**: `TabTransitionSettings.GetDeltaTime` clamps the frame delta to 0.1 s (one frame at 10 FPS): the multi-second `unscaledDeltaTime` spike on the first frame after app pause/resume no longer teleports a running slide to completion, while genuinely slow devices are unaffected.
- **UI**: `LayoutSlideNavigator`'s arrive fast path now uses a 0.005 position epsilon (≈5 px on a 1080-wide strip) instead of `Mathf.Approximately`: an interrupt landing at e.g. 0.998 of the target no longer runs a full `MinDuration` slide (with content input locked) for invisible motion, so rapid tab-tapping feels snappier.

## [0.3.0] - 2026-07-07

The UI module gains a sliding tab bar system, promoted from its first real consumer (game-shikaku) after play-mode verification there. Purely additive: no existing public API changed.

### Added

- **UI**: `UIBase.SetRaycastsBlocked(bool)` — blocks/unblocks pointer input on the UI's CanvasGroup without changing visibility. The access seam for input-locking flows (the tab system uses it); alpha/interactable stay under package control.
- **UI**: tab bar module (`Runtime/UI/TabBar/`):
  - `TabBarView` — config-driven persistent tab bar: builds buttons from `TabBarConfig`, raises `TabSelected(layoutKey)`, `SetSelected`/`SetVisible`/`GetTabIndex`/`TryGetTabKey`, exposes the config's `TransitionSettings`. `SetSelected` with an unknown key logs a warning.
  - `TabBarConfig` (`Create → TK → UI Tab Bar Config`) — ordered `{layoutKey, label}` entries + `TabTransitionSettings`.
  - `TabTransitionSettings` — min/max duration, extra-step multiplier, easing curve, unscaled-time flag (`CalculateDuration`/`Evaluate`/`GetDeltaTime`).
  - `TabButtonData` / `ITabButtonPresenter` / `DefaultTabButtonPresenter` — button-visuals seam with a color-swap default.
  - `IOrderedTabTransition` / `DefaultOrderedTabTransition` / `TabTransitionResult` — the slide-animation seam. Contract: `shouldInterrupt` is polled once per frame BEFORE positions are applied; on interrupt the reached fractional position is reported so the caller can retarget from it.
  - `LayoutSlideNavigator` — layout registry + fractional visual position + `SlideThroughAsync`/`SettleAsync`/`SetLayoutsInteractable`. Interrupted slides keep their offsets (the next slide retargets seamlessly); whoever abandons navigation owns the `SettleAsync()`; completed slides hide every registered layout except the target; `Current` never points at an abandoned slide target.
- **App**: `AppRootBase.CompletedAwaitable()` / `CompletedAwaitable(bool)` — now `protected static` (previously private to `AppFlowBase`): completed Awaitables for gated verbs (`IsTransitioning ? CompletedAwaitable() : ...`) and hook defaults, without game-side copies.

### Changed

- `TK.Core.UI` asmdef references `Unity.TextMeshPro` (ships inside `com.unity.ugui`, already a dependency). `TabBarView`/`TabBarConfig`/`DefaultTabButtonPresenter` are non-sealed so a consuming game can migrate existing prefabs via thin subclass shims.

### Notes

- The single-flight tab-navigation coalescing loop (latest-request-wins generation counter, input lock around the whole sequence, settle-on-abandon in a `finally`) intentionally stays game-side composition glue; the README documents the contracts a game loop must uphold.

## [0.2.0] - 2026-07-06

The App module now has three adoption tiers — `AppContext` only → `AppRootBase` (level-free) → `AppFlowBase` (level preset). Pure decomposition: existing `AppFlowBase` subclasses compile and behave unchanged (every pre-existing test passes unmodified).

### Added

- **App**: `AppRootBase` — level-free composition root: `AppContext` + save seam + `RegisterServices` + `AppLifecycleRelay` + the transition lock + abstract `OnBootAsync()` + `OnGameEnded`. Subclass it for endless / one-run games and define your own verbs over `RunTransitionAsync`.
- **App**: `NavigationGate` — the drop-on-reentry transition lock as a standalone class (`RunAsync` / `IsTransitioning` / `CanNavigate`; exceptions logged and always release the gate). `AppRootBase.RunTransitionAsync` is now backed by it.
- **App**: level lifecycle hooks on `AppFlowBase` — `OnBeforeLevelStartAsync(int)` (runs inside the lock before every level entry the base initiates: the Play/Retry verbs and boot-resume; return `false` to veto — the lives/energy-gate and pre-level-interstitial seam) and `OnAfterLevelEndAsync(GameEndResult)` (runs after the sync `OnGameEnded` hook on every game end). Defaults are no-ops.
- **App**: `OnContextCreated()` hook on `AppRootBase` (runs right after `AppContext` exists, before `RegisterServices`) — `AppFlowBase` uses it to construct `LevelProgress`, preserving the 0.1.x `Awake` order.

### Changed

- **App**: `AppFlowBase` now derives from `AppRootBase`. `OnBootAsync` is abstract on the root; `AppFlowBase` overrides it with the unchanged resume-or-menu default. No public API changes — a `base.OnBootAsync()` call from a game override still means resume-or-menu.

## [0.1.1] - 2026-07-06

Additive quick wins from the first real game integration (game-shikaku). No breaking changes.

### Added

- **App**: `AppFlowBase.OnBootAsync()` — overridable boot policy; the default (resume-or-menu) is unchanged, and the public verbs are safe to call from an override.
- **App**: `LevelProgressService` multi-track support — optional `saveKey` constructor parameter (default `"level_progress"`, unchanged) so multiple instances don't collide, plus a pluggable `LevelAdvancePolicy` seam with `LevelAdvancePolicies.Wrap` (default, current behavior) and `Clamp` built-ins.
- **App**: `AppBootstrapper` serialized scene-name overrides (`mainSceneName`, `splashSceneName`; defaults unchanged) and a skip-splash-delay-in-editor toggle (default off).
- **App/UI**: `SceneLoader` / `UIManager` statics now reset at play-mode entry via `RuntimeInitializeOnLoadMethod`, so Enter Play Mode with domain reload disabled starts clean.

### Changed

- README: documented that `ShowMenuAsync` is a semantic state hook (App never references UI), that the Splash → Main → Game scene topology is opt-in at three levels, and that the App module targets level-based games (non-level games should use `AppContext` + their own root).

## [0.1.0] - 2026-07-03

### Added

- **Utilities** module: object pooling, ref-counted locks, a shared bool, renderer color utilities.
- **Save** module: `ISaveSystem` abstraction with a `PlayerPrefsJsonSaveSystem` implementation.
- **UI** module: `UIManager`, `LayoutBase`/`PopupBase`, navigation/back-button stack, Addressables-backed `UICatalog`, task/busy overlay, and a pluggable `IUITransition` with a dependency-free `DefaultPopupTransition`.
- **App** module: `AppFlowBase` composition root, `AppContext` service registry, `LevelProgressService`, `SceneLoader`, `AppBootstrapper`, `AppLifecycleRelay`.
- Samples: PrimeTween and DOTween `IUITransition` adapters for `PopupBase.CreateTransition()`.
- Package README and this changelog.
