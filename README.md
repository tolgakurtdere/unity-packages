# unity-packages

Personal, reusable Unity packages — game-agnostic systems extracted into installable UPM packages, developed and tested inside a host Unity project in this repo.

## Packages

| Package | Version | Min Unity | Description | Docs |
| --- | --- | --- | --- | --- |
| `com.tk.core` | 0.4.0 | 6000.0+ | Reusable core systems: save system, UI framework (layouts, popups, navigation stack, sliding tab bar, busy overlay), app flow with level progression, and utilities. Modules are usable à la carte via separate asmdefs. | [README](Packages/com.tk.core/README.md) |
| `com.tk.iap` | 0.1.1 | 6000.0+ | In-app purchasing framework wrapping Unity IAP v5: string-key catalog, item-handler composition, entitlements, idempotent purchase application, drop-in UI. Requires `com.tk.core`. | [README](Packages/com.tk.iap/README.md) |
| `com.tk.ads` | 0.1.2 | 6000.0+ | Ads framework wrapping AppLovin MAX mediation: banner/interstitial/rewarded with testable policy layer (pacing, intent-based banner, reward latching) and analytics/remote-config seams. Standalone (no dependency on other `com.tk.*` packages); needs the AppLovin MAX + OpenUPM scoped registries. | [README](Packages/com.tk.ads/README.md) |
| `com.tk.remoteconfig` | 0.1.0 | 6000.0+ | Backend-agnostic remote-config façade: typed parameters with defaults, safety gates, editor overrides, and runtime refresh — feeds the IAP/Ads resolver seams from any backend. Standalone (no dependency on other `com.tk.*` packages); no scoped registries needed. | [README](Packages/com.tk.remoteconfig/README.md) |
| `com.tk.analytics` | 0.1.0 | 6000.0+ | Backend-agnostic analytics façade: log events, revenue, and user properties through one API that fans out to any backend (Firebase, Adjust, …), with a consent gate and loss-free pre-init buffering — unifies the IAP/Ads monetization event stream. Standalone (no dependency on other `com.tk.*` packages); no scoped registries needed. | [README](Packages/com.tk.analytics/README.md) |
| `com.tk.notification` | 0.1.0 | 6000.0+ | Local mobile-notification framework: scheduling, quiet-hours, channels, permission and launch routing behind a testable seam, with a Unity Mobile Notifications backend and a no-op fallback on non-mobile targets. Standalone (no dependency on other `com.tk.*` packages); its only dependency `com.unity.mobile.notifications` is on Unity's default registry, so no scoped registries needed. | [README](Packages/com.tk.notification/README.md) |
| `com.tk.localization` | 0.1.0 | 6000.0+ | Localization framework over Unity Localization: per-locale TMP font swapping, an RTL text-shaping pipeline (Arabic/Farsi), and an injectable locale selection/persistence service. Standalone (no dependency on other `com.tk.*` packages); its dependencies `com.unity.localization` + `com.unity.ugui` are on Unity's default registry, so no scoped registries needed. | [README](Packages/com.tk.localization/README.md) |
| `com.tk.audio` | 0.2.0 | 6000.0+ | Audio framework: Music/Sfx channels with optional save-backed settings, ref-counted temporary mute (ads-ready), pooled one-shot SFX with variations and retrigger throttling, and crossfading music with playlists and per-entry Addressables. Requires `com.tk.core`. | [README](Packages/com.tk.audio/README.md) |
| `com.tk.toolbar` | 0.1.0 | 6000.3+ | Editor main-toolbar extensions: a time scale slider with reset, and configurable scene-switch buttons, built on Unity's official `MainToolbar` API. | [README](Packages/com.tk.toolbar/README.md) |

## Installing

New to these packages? See **[QUICKSTART.md](QUICKSTART.md)** for an incremental-adoption walkthrough — start with `com.tk.core`, add the rest (with wiring code) when you need them.

Install via Unity's Package Manager → **Add package from git URL**.

**com.tk.core**

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core#com.tk.core/0.4.0
```

**com.tk.iap** — requires `com.tk.core` installed first (it depends on `TK.Core.Save`'s
`ISaveSystem`).

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.iap
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.iap#com.tk.iap/0.1.1
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
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.ads#com.tk.ads/0.1.2
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

**com.tk.analytics** — no scoped registries and no dependencies (install by git URL only; the
Firebase/Adjust backends and IAP/Ads bridges ship as samples, so no SDK is forced on you).

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.analytics
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.analytics#com.tk.analytics/0.1.0
```

**com.tk.notification** — no scoped registries needed (its only dependency,
`com.unity.mobile.notifications`, is on Unity's default registry).

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.notification
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.notification#com.tk.notification/0.1.0
```

**com.tk.localization** — no scoped registries needed (its dependencies, `com.unity.localization` and
`com.unity.ugui`, are on Unity's default registry). You set up your own Unity Localization project (settings,
locales, string tables, fonts).

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.localization
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.localization#com.tk.localization/0.1.0
```

**com.tk.audio** — requires `com.tk.core` installed first (uses its `TK.Core.Utilities` +
`TK.Core.Save` assemblies). No scoped registries needed.

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.audio
```

Pinned to a version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.audio#com.tk.audio/0.2.0
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

- `com.tk.core/0.4.0`
- `com.tk.iap/0.1.1`
- `com.tk.ads/0.1.2`
- `com.tk.remoteconfig/0.1.0`
- `com.tk.analytics/0.1.0`
- `com.tk.notification/0.1.0`
- `com.tk.localization/0.1.0`
- `com.tk.audio/0.2.0`
- `com.tk.toolbar/0.1.0`

Check each package's `package.json` for its current version, and the repo's tags for the full release history.

## Development

The repo root is a Unity **6000.3** host project used to develop and test the packages. The packages themselves are embedded under `Packages/` (`Packages/com.tk.core`, `Packages/com.tk.iap`, `Packages/com.tk.ads`, `Packages/com.tk.remoteconfig`, `Packages/com.tk.analytics`, `Packages/com.tk.notification`, `Packages/com.tk.localization`, `Packages/com.tk.audio`, `Packages/com.tk.toolbar`), so changes are edited in place and picked up immediately by the host project.

Tests live inside `com.tk.core`, `com.tk.iap`, `com.tk.ads`, `com.tk.remoteconfig`, `com.tk.analytics`, `com.tk.notification`, `com.tk.localization`, and `com.tk.audio`, and run via the Unity **Test Runner** (EditMode). Open `Window → General → Test Runner` in the host project to run them.
