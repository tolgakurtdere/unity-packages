# unity-packages

Personal, reusable Unity packages — game-agnostic systems extracted into installable UPM packages, developed and tested inside a host Unity project in this repo.

## Packages

| Package | Version | Min Unity | Description | Docs |
| --- | --- | --- | --- | --- |
| `com.tk.core` | 0.1.0 | 6000.0+ | Reusable core systems: save system, UI framework (layouts, popups, navigation stack, busy overlay), app flow with level progression, and utilities. Modules are usable à la carte via separate asmdefs. | [README](Packages/com.tk.core/README.md) |
| `com.tk.iap` | 0.1.0 | 6000.0+ | In-app purchasing framework wrapping Unity IAP v5: string-key catalog, item-handler composition, entitlements, idempotent purchase application, drop-in UI. Requires `com.tk.core`. | [README](Packages/com.tk.iap/README.md) |
| `com.tk.ads` | 0.1.0 | 6000.0+ | Ads framework wrapping AppLovin MAX mediation: banner/interstitial/rewarded with testable policy layer (pacing, intent-based banner, reward latching) and analytics/remote-config seams. Standalone (no dependency on other `com.tk.*` packages); needs the AppLovin MAX + OpenUPM scoped registries. | [README](Packages/com.tk.ads/README.md) |
| `com.tk.remoteconfig` | 0.1.0 | 6000.0+ | Backend-agnostic remote-config façade: typed parameters with defaults, safety gates, editor overrides, and runtime refresh — feeds the IAP/Ads resolver seams from any backend. Standalone (no dependency on other `com.tk.*` packages); no scoped registries needed. | [README](Packages/com.tk.remoteconfig/README.md) |
| `com.tk.toolbar` | 0.1.0 | 6000.3+ | Editor main-toolbar extensions: a time scale slider with reset, and configurable scene-switch buttons, built on Unity's official `MainToolbar` API. | [README](Packages/com.tk.toolbar/README.md) |

## Installing

Install via Unity's Package Manager → **Add package from git URL**.

**com.tk.core**

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core#com.tk.core/0.1.0
```

**com.tk.iap** — requires `com.tk.core` installed first (it depends on `TK.Core.Save`'s
`ISaveSystem`).

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.iap
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.iap#com.tk.iap/0.1.0
```

**com.tk.ads** — requires **two scoped registries** added to your project's `Packages/manifest.json`
first (AppLovin MAX's own registry for the MAX SDK, plus OpenUPM for a transitive dependency MAX
pulls in that AppLovin's registry doesn't serve — see the package README's Install section for the
exact JSON block):

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.ads
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.ads#com.tk.ads/0.1.1
```

**com.tk.remoteconfig** — no scoped registries needed (its only dependency,
`com.unity.nuget.newtonsoft-json`, is on Unity's default registry).

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.remoteconfig
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.remoteconfig#com.tk.remoteconfig/0.1.0
```

**com.tk.toolbar**

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.toolbar
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.toolbar#com.tk.toolbar/0.1.0
```

Pinning to a tag is recommended for anything beyond local experimentation, since `main` moves as packages evolve.

## Versioning

Each package is versioned and tagged independently. Tags follow the format `<package-name>/<version>`:

- `com.tk.core/0.1.0`
- `com.tk.iap/0.1.0`
- `com.tk.ads/0.1.1`
- `com.tk.remoteconfig/0.1.0`
- `com.tk.toolbar/0.1.0`

Check each package's `package.json` for its current version, and the repo's tags for the full release history.

## Development

The repo root is a Unity **6000.3** host project used to develop and test the packages. The packages themselves are embedded under `Packages/` (`Packages/com.tk.core`, `Packages/com.tk.iap`, `Packages/com.tk.ads`, `Packages/com.tk.remoteconfig`, `Packages/com.tk.toolbar`), so changes are edited in place and picked up immediately by the host project.

Tests live inside `com.tk.core`, `com.tk.iap`, `com.tk.ads`, and `com.tk.remoteconfig`, and run via the Unity **Test Runner** (EditMode). Open `Window → General → Test Runner` in the host project to run them.
