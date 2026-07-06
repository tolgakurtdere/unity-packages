# com.tk.core v0.3.0 — Tab Bar Module + UIBase Raycast Seam Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This release is executed inline (no subagents) per the core-release precedent (0.1.1, 0.2.0).

**Goal:** Promote the play-mode-verified sliding Tab Bar system from game-shikaku into `com.tk.core`'s UI module, add a `UIBase.SetRaycastsBlocked` input-blocking seam, and expose the `CompletedAwaitable` helpers on `AppRootBase` — shipped as `com.tk.core/0.3.0` with zero breaks to existing public API.

**Architecture:** Ten classes move from `Company.Shikaku.Presentation` to `TK.Core.UI` under `Runtime/UI/TabBar/`, contracts unchanged (the game migrates later via thin subclass shims, so the three prefab/asset-bound types are unsealed). `LayoutSlideNavigator` stops resolving `CanvasGroup`s by convention and calls the new `UIBase.SetRaycastsBlocked` seam. The single-flight coalescing loop (`UiFlowBindings.ShowTabAsync`) **stays in the game** — no package-side `TabNavigationController` (same promotion discipline as ISceneFlow: the loop is glued to game-specific loading/binding and has one consumer; lifting it now would freeze speculative seams).

**Tech Stack:** Unity 6000.3.6f1 host harness, C#, NUnit EditMode. New asmdef reference: `Unity.TextMeshPro` (ships inside `com.unity.ugui` 2.0.0 — already a core dependency, verified against the game's asmdef).

**Reference implementation (read-only, play-mode verified):** `/Users/tolgahankurtdere/Documents/GitHub/game-shikaku`, branch `feature/tk2-ui-foundation` (commits `5c16352`, `170492c`), files under `game_shikaku/Assets/_Project/Scripts/Presentation/Ui/`. Coalescing-loop reference: `Scripts/Composition/UiFlowBindings.cs` (`ShowTabAsync`). NOTE: the user's prompt overrides any older session decisions (e.g. no `positionChanged` callback on the transition interface — the reference files above are the final shape).

## Global Constraints

- Repo `/Users/tolgahankurtdere/Documents/GitHub/unity-packages`, work on branch `core-v0.3.0` off `main` (`f9836ac`), ff-merge at the end, annotated tag `com.tk.core/0.3.0`. Do NOT push — the user pushes.
- **Zero API break:** UIBase/UIManager/LayoutBase/PopupBase/AppRootBase/AppFlowBase existing surface and behavior unchanged; UIBase gains one method; AppRootBase gains two protected static helpers. Every pre-existing test passes UNMODIFIED (that is the proof).
- **Contracts to preserve verbatim (all play-mode verified in the game):**
  - `IOrderedTabTransition.PlayAsync(orderedLayouts, startPosition: float, targetIndex, container, settings, shouldInterrupt) -> Awaitable<TabTransitionResult>`; `shouldInterrupt` polled once per frame BEFORE applying positions; on true stop immediately, report reached fractional position.
  - Interrupted slides KEEP layouts at their offsets (no settle) so the next slide retargets from the fractional position. The abandoner owns the settle: `SettleAsync()` re-centres the last fully shown layout and hides the rest.
  - `LayoutSlideNavigator.Current` only ever points at a fully-arrived (or `SetCurrent`-adopted) layout, never an abandoned slide target.
  - Completed slides hide ALL registered layouts except the target (load-bearing cleanup for earlier interrupts — do not optimize away).
  - `SetLayoutsInteractable` blocks content input on every registered layout; the tab bar lives outside the layouts and stays live.
  - Class names KEPT (game migrates via thin subclass shims for prefab GUID continuity) — namespace becomes `TK.Core.UI`.
- Deliberate deviations from the reference (all serve the shim/seam requirements, everything else is verbatim minus namespace):
  1. `TabBarView`, `TabBarConfig`, `DefaultTabButtonPresenter` are **unsealed** (shim subclasses need them); `TabBarView.Awake` becomes `protected virtual` (a shim declaring its own `Awake` would silently shadow a private one — Unity calls only the most-derived magic method).
  2. `LayoutSlideNavigator` drops the `_layoutGroups` dictionary + Canvas/CanvasGroup resolution; `SetLayoutsInteractable(bool)` calls `layout.SetRaycastsBlocked(!interactable)` per registered layout (user-mandated).
  3. `TabBarView.SetSelected` logs a warning on a non-empty unknown key (user-approved optional item 5).
- **NEVER run Unity CLI against the host repo project** (the user's editor may be open — also write `.cs` + `.cs.meta` together for new files; the open editor races meta generation). Harness: `<session scratchpad>/tk-verify`. The prior harness is gone; recreate: `Assets/` + `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion: 6000.3.6f1`) + `Packages/manifest.json` with `com.unity.test-framework` `1.6.0`, all EIGHT TK packages as `file:` absolute paths, `testables` listing all eight, and the AppLovin + OpenUPM scoped registries (copy shape from docs/plans/2026-07-05-tk-localization-v1.md Global Constraints).
- Gate command (from harness dir; **NEVER `-quit` with `-runTests`**; Bash timeout 600000):
  ```bash
  /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$(pwd)/results.xml" -logFile "$(pwd)/unity.log"
  ```
  Success = exit 0 AND all cases `result="Passed"` AND zero `error CS`/`warning CS` under `Packages/com.tk.core`. Baseline before changes = **287** (trust results.xml).
- Conventional commits, trailer `Co-Authored-By: Claude <noreply@anthropic.com>` — NO model name.
- EditMode test patterns that work here (from 0.2.0): manual `InvokeAwake()` public wrappers on test subclasses; `TestAwaitables` for synchronously-completed Awaitables; test files importing `System` + `TK.Core.App` need `using AppContext = TK.Core.App.AppContext;` and `using Object = UnityEngine.Object;` aliases. UI tests additionally need a `UIManager` GameObject in the scene (`LayoutBase.OnUIEnter/OnUIExit` call `UIManager.Instance.AddToStack/RemoveFromStack`; `Instance` resolves via `FindAnyObjectByType`) — create in SetUp, `DestroyImmediate` in TearDown.

---

### Task 1: Harness rebuild + baseline gate

**Files:** harness only (scratchpad — not committed).

- [ ] Recreate `tk-verify` harness per Global Constraints (manifest with 8 `file:` packages + testables + 2 scoped registries).
- [ ] Run the gate. Expected: exit 0, **287/287 Passed**. This is the zero-break baseline.

### Task 2: `UIBase.SetRaycastsBlocked` seam + `AppRootBase.CompletedAwaitable` promotion

**Files:**
- Modify: `Packages/com.tk.core/Runtime/UI/UIBase.cs` (add one method after `SetActivationStateAsync`)
- Modify: `Packages/com.tk.core/Runtime/App/AppRootBase.cs` (add two protected static helpers)
- Modify: `Packages/com.tk.core/Runtime/App/AppFlowBase.cs` (delete its two private static `CompletedAwaitable` copies; call sites now bind to the inherited protected ones)
- Test: `Packages/com.tk.core/Tests/Editor/UIBaseTests.cs` (new), extend `Tests/Editor/AppRootBaseTests.cs`

**Produces:** `public void UIBase.SetRaycastsBlocked(bool blocked)`; `protected static Awaitable AppRootBase.CompletedAwaitable()`; `protected static Awaitable<bool> AppRootBase.CompletedAwaitable(bool result)`.

- [ ] **UIBase method** (exact user-requested code; do NOT add `[RequireComponent(typeof(CanvasGroup))]` — the group lives on the possibly-child Canvas; Awake's auto-add stays the existence guarantee):

```csharp
/// <summary>Blocks or unblocks pointer input on this UI without changing visibility.</summary>
public void SetRaycastsBlocked(bool blocked)
{
    if (CanvasGroup) CanvasGroup.blocksRaycasts = !blocked;
}
```

- [ ] **AppRootBase helpers** — move AppFlowBase's two private statics up unchanged, `protected static`, XML doc: completed Awaitables for gated verbs and default hook implementations (games gate verbs like `IsTransitioning ? CompletedAwaitable() : ...`). Remove the AppFlowBase copies.
- [ ] **Tests:** `UIBaseTests` — a `LayoutBase` test subclass with `InvokeAwake()`; after Awake, `SetRaycastsBlocked(true)` → `blocksRaycasts == false`, `SetRaycastsBlocked(false)` → `true` (read via a test accessor on the subclass exposing the protected `CanvasGroup`); no-Canvas object logs the Awake error (LogAssert) and `SetRaycastsBlocked` does not throw. `AppRootBaseTests` — a subclass exposes the helpers; `await CompletedAwaitable()` completes synchronously, `await CompletedAwaitable(false)` returns `false`.
- [ ] Commit: `feat(core): UIBase raycast-blocking seam + shared CompletedAwaitable helpers`.

### Task 3: Tab Bar module — 10 files into `Runtime/UI/TabBar/`

**Files (Create, each with a `.meta`; folder gets one too):**
`TabButtonData.cs`, `ITabButtonPresenter.cs`, `DefaultTabButtonPresenter.cs`, `TabTransitionSettings.cs`, `TabTransitionResult.cs`, `IOrderedTabTransition.cs`, `DefaultOrderedTabTransition.cs`, `TabBarConfig.cs`, `TabBarView.cs`, `LayoutSlideNavigator.cs`
**Modify:** `Runtime/UI/TK.Core.UI.asmdef` (add `"Unity.TextMeshPro"` to references).

- [ ] Copy each file from the reference path, apply ONLY: namespace `TK.Core.UI` (drop the now-redundant `using TK.Core.UI;`), plus the three deliberate deviations from Global Constraints. `TabBarConfig` keeps `menuName = "TK/UI Tab Bar Config"` (matches the `"TK/UI Catalog"` convention). XML docs come along verbatim; update only the `LayoutSlideNavigator.Register`/`SetLayoutsInteractable` comments that referenced the convention-based CanvasGroup resolution.
- [ ] `LayoutSlideNavigator` changed members (rest verbatim):

```csharp
public void Register(string key, LayoutBase layout)
{
    if (string.IsNullOrEmpty(key) || !layout) return;

    _layouts[key] = layout;
    if (!Container) Container = layout.transform.parent as RectTransform;
}

public void SetLayoutsInteractable(bool interactable)
{
    foreach (var layout in _layouts.Values)
    {
        if (layout) layout.SetRaycastsBlocked(!interactable);
    }
}
```

- [ ] `TabBarView.SetSelected` warning (before the loop): `if (!string.IsNullOrEmpty(layoutKey) && !_buttons.ContainsKey(layoutKey)) Debug.LogWarning($"[TabBarView] Unknown tab key '{layoutKey}' — all tabs deselected.");`
- [ ] Commit: `feat(core): sliding tab bar module in TK.Core.UI`.

### Task 4: EditMode tests for the tab module

**Files:**
- Create: `Tests/Editor/TabTransitionSettingsTests.cs`, `Tests/Editor/DefaultOrderedTabTransitionTests.cs`, `Tests/Editor/LayoutSlideNavigatorTests.cs`, `Tests/Editor/TabBarViewTests.cs`
- Modify: `Tests/Editor/TK.Core.Tests.asmdef` (add `"UnityEngine.UI"`, `"Unity.TextMeshPro"` to references)

Coverage (all frame-free — the only frame-driven code path, `DefaultOrderedTabTransition`'s motion loop, is play-mode verified in the game; its synchronous paths ARE tested):

- [ ] **TabTransitionSettings:** `CalculateDuration(1) == MinDuration`; extra steps scale by the multiplier and clamp to `MaxDuration`; `stepCount < 1` coerces to 1; `Evaluate` clamps outside 0..1; `Default` non-null; `MaxDuration >= MinDuration` invariant.
- [ ] **DefaultOrderedTabTransition (sync paths):** null layouts / out-of-range target → `InterruptedAt(startPosition)` synchronously; destroyed target → interrupted; `shouldInterrupt` returning true immediately → interrupted at clamped start WITHOUT any motion (proves poll-before-apply), layouts sit at their start offsets (proves the single pre-loop `ApplyPositions`).
- [ ] **LayoutSlideNavigator** (fixture: `UIManager` GO + container `RectTransform` + `TestLayout : LayoutBase` children created with `typeof(RectTransform), typeof(Canvas)`, `InvokeAwake()` each; fake `IOrderedTabTransition` injected via the public `OrderedTransition` property returning a scripted `TabTransitionResult`):
  - `Register`/`TryGet`/`IsRegistered`; `Container` captured from first registration.
  - `SetCurrent(layout)` adoption → `Current` set, `HasVisualPosition == false`; `SetCurrent(layout, 2)` → `VisualPosition == 2`, `HasVisualPosition == true`.
  - `SetLayoutsInteractable(false)` → every registered layout's CanvasGroup `blocksRaycasts == false`; `(true)` restores.
  - Completed slide (fake returns `CompletedAt(target)`) → returns true, `Current == target`, `VisualPosition == targetIndex`, ALL other registered layouts (including one NOT in the slide range, pre-offset) hidden + `anchoredPosition == zero`, target re-centred.
  - Interrupted slide (fake returns `InterruptedAt(0.5f)`) → returns false, `VisualPosition == 0.5f`, `HasVisualPosition == true`, `Current` unchanged (never the abandoned target), layouts NOT re-centred (offsets kept).
  - Same-position fast path (`startPosition == targetIndex`) → true, target activated + arrived without invoking the transition (fake asserts not called).
  - Invalid target index → false, state untouched.
  - `SettleAsync` after an interrupt → `HasVisualPosition == false`, `Current` re-centred, everything else hidden + zeroed.
- [ ] **TabBarView** (fixture: `TabBarConfig` via `ScriptableObject.CreateInstance` + `SerializedObject` to fill `tabs`; view GO + child container + child `Button` template wired via `SerializedObject`; `TestTabBarView : TabBarView` exposes `InvokeAwake()`):
  - Awake builds one button per entry (template stays inactive); `TabCount`/`GetTabIndex` reflect config order; unknown key → -1; `TryGetTabKey` round-trips and rejects out-of-range.
  - Clicking a non-selected tab raises `TabSelected` with the key; clicking the selected tab does NOT raise.
  - `SetSelected(knownKey)` → no warning; `SetSelected("nope")` → LogAssert warning; `SetVisible(false/true)` toggles `activeSelf`.
- [ ] Run the gate. Expected: exit 0, 287 pre-existing + new tests ALL Passed, zero core warnings. Record exact totals.
- [ ] Commit: `test(core): tab bar module EditMode coverage`.

### Task 5: Version, docs, pins

**Files:**
- Modify: `Packages/com.tk.core/package.json` (`0.2.0` → `0.3.0`; append `tabbar` to keywords? NO — keywords stay unless asked)
- Modify: `Packages/com.tk.core/CHANGELOG.md` (new `## [0.3.0] - 2026-07-07` section: Added — tab bar module class list + one-line contracts, `UIBase.SetRaycastsBlocked`, `AppRootBase.CompletedAwaitable`; Changed — none public; note the game-shikaku provenance)
- Modify: `Packages/com.tk.core/README.md` (install pin → `0.3.0`; UI section: new "Tab bar" subsection — config-driven `TabBarView`, `LayoutSlideNavigator` slide/settle/interrupt semantics, `IOrderedTabTransition` seam, `SetRaycastsBlocked`; note the coalescing loop is composition-layer glue with a pointer to the contracts)
- Modify: root `README.md` (catalog row version, install pin, release-tags list)
- Modify: `QUICKSTART.md` (both `com.tk.core/0.2.0` pins → `0.3.0`)
- Check: `ROADMAP.md` — only touch if a listed core item is satisfied by this release.

- [ ] Apply all; verify every core pin in the repo matches `0.3.0` (`grep -rn "com.tk.core/0" --include="*.md"`).
- [ ] Commit: `chore(core): release 0.3.0 — tab bar module, raycast seam, changelog, pins`.

### Task 6: Merge, tag, report

- [ ] `git checkout main && git merge --ff-only core-v0.3.0`
- [ ] Annotated tag: `git tag -a com.tk.core/0.3.0 -m "com.tk.core 0.3.0 — sliding tab bar module (TabBarView/LayoutSlideNavigator/IOrderedTabTransition), UIBase.SetRaycastsBlocked, AppRootBase.CompletedAwaitable"`
- [ ] Do NOT push. Report to the user: tag name, full list of added public API, test totals, the deliberate deviations, and the TabNavigationController decision (not lifted + why).
