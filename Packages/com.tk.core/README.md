# TK Core

Reusable core systems for Unity games: a save system, a UI framework (layouts, popups, navigation stack, busy overlay), an app flow layer with level progression, and small utilities. Each module lives in its own assembly and can be used à la carte.

## What's inside

| Module | Asmdef | What it gives you |
| --- | --- | --- |
| Utilities | `TK.Core.Utilities` | Small dependency-free helpers: object pooling, ref-counted locks, a shared bool, renderer color utilities. |
| Save | `TK.Core.Save` | `ISaveSystem` abstraction + a `PlayerPrefsJsonSaveSystem` implementation, so save/load can be swapped (PlayerPrefs, file, cloud) without touching game code. |
| UI | `TK.Core.UI` | `UIManager`, `LayoutBase`/`PopupBase`, a navigation/back-button stack, an Addressables-backed `UICatalog`, a busy/task overlay, and a pluggable `IUITransition` (dependency-free default, PrimeTween/DOTween adapters available as samples). |
| App | `TK.Core.App` | `AppFlowBase` composition root, `AppContext` service registry, `LevelProgressService` (index-based level progression), `SceneLoader`, `AppBootstrapper`, and an `AppLifecycleRelay` for pause/focus/quit events. |

## Install

Add via Package Manager → "Install package from git URL":

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core
```

To pin a specific released version, add the version tag:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.core#com.tk.core/0.1.0
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

`AppFlowBase` handles the transition lock (`IsTransitioning`/`CanNavigate`, drops re-entrant navigation calls), resuming a saved session on `Start` (override `TryGetResumeState`), and wiring up `AppContext` + `LevelProgressService` in `Awake`.

**UIManager scene setup:** add a `UIManager` component to a persistent scene object, assign its layout/popup/task-overlay `RectTransform` containers, and create a `UICatalog` asset (`Create → TK → UI Catalog`) with your layout/popup Addressable references, then assign it to `UIManager.Catalog`. String-keyed APIs (`ShowLayoutAsync<T>(string key)`, `ShowPopupAsync<T>(string key)`) resolve against that catalog.

## À la carte

Every module is its own asmdef, so you can reference just what you need:

- `TK.Core.Utilities` has no dependencies.
- `TK.Core.Save` has no dependencies.
- `TK.Core.UI` depends on `TK.Core.Utilities` (plus `UnityEngine.UI`, Input System, Addressables).
- `TK.Core.App` depends only on `TK.Core.Save` — **it does not depend on `TK.Core.UI`**. You can use the app flow layer with your own UI, or the UI framework without the app flow layer.

## Extending

- **Custom popup transitions:** override `PopupBase.CreateTransition()` to swap the dependency-free `DefaultPopupTransition` for a tween-library adapter. See the `PrimeTween Popup Transition` and `DOTween Popup Transition` samples (Package Manager → TK Core → Samples) for `IUITransition` implementations you can copy into your project and adapt.
- **Custom save backend:** override `AppFlowBase.CreateSaveSystem()` to return your own `ISaveSystem` (cloud save, file-based, etc.) instead of the default `PlayerPrefsJsonSaveSystem`.
- **Game services:** override `AppFlowBase.RegisterServices(AppContext context)` to `context.Register<T>(...)` your own services (analytics, ads, IAP, ...), then resolve them elsewhere with `context.Get<T>()`/`context.TryGet<T>()`.

## Gotchas

`TK.Core.App.AppContext` collides with `System.AppContext` (a BCL type). If a file needs both `using System;` and `using TK.Core.App;`, disambiguate with:

```csharp
using AppContext = TK.Core.App.AppContext;
```
