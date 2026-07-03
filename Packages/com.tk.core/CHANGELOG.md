# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-03

### Added

- **Utilities** module: object pooling, ref-counted locks, a shared bool, renderer color utilities.
- **Save** module: `ISaveSystem` abstraction with a `PlayerPrefsJsonSaveSystem` implementation.
- **UI** module: `UIManager`, `LayoutBase`/`PopupBase`, navigation/back-button stack, Addressables-backed `UICatalog`, task/busy overlay, and a pluggable `IUITransition` with a dependency-free `DefaultPopupTransition`.
- **App** module: `AppFlowBase` composition root, `AppContext` service registry, `LevelProgressService`, `SceneLoader`, `AppBootstrapper`, `AppLifecycleRelay`.
- Samples: PrimeTween and DOTween `IUITransition` adapters for `PopupBase.CreateTransition()`.
- Package README and this changelog.
