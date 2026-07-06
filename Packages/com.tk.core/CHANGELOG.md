# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
