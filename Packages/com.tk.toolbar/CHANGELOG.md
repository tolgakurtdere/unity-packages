# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-03

### Added

- Time Scale slider with reset button on the main editor toolbar, backed by a `ToolbarSettings` asset (`Assets > Create > TK > Toolbar Settings`).
- Automatic `Time.timeScale` reset to the configured default on exiting Play mode.
- Configurable scene-switch buttons: replaces hardcoded build-index scene buttons with a `ToolbarSettings.scenes` list, one toolbar button per configured scene.
- Package README and this changelog.
