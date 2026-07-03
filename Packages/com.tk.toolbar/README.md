# TK Toolbar

Editor main-toolbar extensions: a time scale slider with a reset button, and configurable scene-switch buttons. Built on Unity's official `MainToolbar` API.

## Requirement

Unity 6000.3+ (the `MainToolbar` API shipped in Unity 6.3).

## What's inside

- **Time Scale slider** — drag to set `Time.timeScale` live; range and default are configurable.
- **Reset button** — snaps `Time.timeScale` back to the configured default.
- **Play-mode reset** — `Time.timeScale` is restored to the default automatically when you exit Play mode.
- **Scene buttons** — one toolbar button per scene you configure, so you can jump straight to any scene without hunting through the Project window. Buttons are disabled while in Play mode.

## Install

Add via Package Manager → "Install package from git URL":

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.toolbar
```

To pin a specific released version, add the version tag:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.toolbar#com.tk.toolbar/0.1.0
```

## Setup

1. Create a settings asset: `Assets > Create > TK > Toolbar Settings` (anywhere in your project — the toolbar finds it by type, so keep at most one).
2. Configure the time scale range (`minTimeScale`/`maxTimeScale`) and `defaultTimeScale`.
3. Drag scene assets into the `scenes` list to get a toolbar button per scene. Buttons are labeled by position (🎬1, 🎬2, ...) and their tooltip shows the scene's asset path.

Without a settings asset, the time scale controls fall back to sane defaults (0–2, default 1) and no scene buttons are shown.

## Notes

- This package has zero dependencies and does not depend on `com.tk.core` — it can be installed standalone.
- Editor-only: the `TK.Toolbar.Editor` assembly is restricted to the Editor platform and contributes nothing to player builds.
