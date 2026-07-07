# com.tk.core 0.4.0 — AnimatedTabButtonPresenter Design

Approved by Tolgahan 2026-07-07 (brainstorm decisions locked via Q&A). Reference look: Magic Sort's bottom bar — selected tab gets a bigger pedestal sprite, grows up + sideways with a soft overshoot, shows its label; neighbors make room (no overlap), nothing leaves the screen.

## Decisions (locked)

1. **Package-side, dependency-free presenter.** `AnimatedTabButtonPresenter` joins `Runtime/UI/TabBar/`; `DefaultTabButtonPresenter` stays as the minimal placeholder. Games needing a different look write their own `ITabButtonPresenter` (the seam is the extensibility story) or subclass this one.
2. **No-overlap via layout reflow.** Horizontal growth animates `LayoutElement.preferredWidth`; a `HorizontalLayoutGroup` on the bar re-solves every frame, so neighbors slide aside and edge buttons stay inside container padding — overlap and off-screen are impossible *by construction*, and the z-order/`SetAsLastSibling` trap (layout groups derive positions from sibling order) never arises. Vertical pop is a visual scale from a **bottom-centre pivot** `(0.5, 0)` so the button grows up + sideways only.
3. **Rollout:** built on `core-v0.4.0`, version `0.4.0` (minor — new feature + additive config field). **Tag only after play-mode verification in game-shikaku** (which consumes 0.3.1 today; verification happens via a temporary `file:` override or a pre-release pin). Pins stay 0.3.1 until then.
4. **Per-tab icons:** `TabBarConfig.Entry` gains an optional `Sprite icon`; `TabButtonData` gains an `Icon` property + a new constructor overload (the existing ctor stays — zero break). `TabBarView` passes it through; both presenters apply it when they have an icon `Image` assigned.

## Component behavior

`public class AnimatedTabButtonPresenter : MonoBehaviour, ITabButtonPresenter` (unsealed).

**Serialized surface**

- Refs: `background` (Image; auto-resolve from `button.image`/`GetComponent`), `icon` (Image, optional), `label` (TMP_Text; auto-resolve from children), `scaleTarget` (RectTransform; default own transform), `layoutElement` (LayoutElement; auto-resolve via `GetComponent`).
- Sprites: `normalBackgroundSprite` / `selectedBackgroundSprite` — **null = sprite untouched** (color-only mode keeps working).
- Colors: `normalBackgroundColor` / `selectedBackgroundColor` (default white — sprites carry the art), `normalTextColor` / `selectedTextColor`.
- Size: `selectedScale` (default 1.1; 1 = no scale), `normalWidth` / `selectedWidth` (default 0 = width animation disabled).
- Label: `showLabelOnlyWhenSelected` (default true — Magic Sort behavior). Visibility via **TMP's `alpha`** (never `SetActive` — that would churn layout).
- Timing: `duration` (default 0.18), `easing` (serialized `AnimationCurve`, default built in code as a soft out-back: fast start, ~6% overshoot near t≈0.7, settle to 1), `useUnscaledTime` (default true).

**Contracts**

- `Initialize`: resolves refs, applies `data.Icon`/`data.Label`, then `SetSelected(false, instant: true)`. Warns (does **not** auto-fix — changing a pivot shifts position) when `selectedScale > 1` and `scaleTarget.pivot != (0.5, 0)`; warns when `selectedWidth > 0` but no `LayoutElement` is present (width axis is then skipped).
- `SetSelected(isSelected, instant)`: sprite swaps at t=0 (motion plays over the new art, like the reference); `instant` snaps everything. Otherwise a frame-loop drives eased progress:
  - **Retarget-safe:** the FROM state is captured from the components' *current live values* at every call (the `DefaultPopupTransition` lesson) — rapid tab spam never jumps. A width capture of an unset `preferredWidth` (< 0) falls back to `normalWidth`.
  - **Reentrancy:** a generation counter; every new call abandons the previous loop. Simultaneous shrink/grow across two buttons needs nothing special — presenter instances are independent and the layout group solves both widths in the same frame.
  - **Overshoot semantics:** scale + width lerp **unclamped** (the curve may exceed 1); colors and label alpha lerp with clamped progress (color overshoot looks broken).
  - Loop is `async void` guarded by try/`LogException` and a destroyed-component check after each frame await.
- `protected virtual void ApplyProgress(float easedProgress)`: the single write point (colors, alpha, scale, width) — the seam both for EditMode tests (frame-free) and for subclasses that want to add channels without rewriting timing/reentrancy.

## Out of scope (deliberate)

Icon crossfade, bar-background morphing (the Magic Sort "notch"), per-tab colors in config — game-specific; extend the presenter or write your own. `TabBarView`/`LayoutSlideNavigator` are untouched (no API need). Delta clamping is intentionally omitted here: after a pause/resume spike a button snapping to its (correct) end state is fine.

## Recommended bar setup (documented in README)

Bar container: `HorizontalLayoutGroup` (child alignment Lower Center, Use Child Scale off, padding for edge safety) — shikaku's bar already has one (verified). Button template: root pivot `(0.5, 0)`, `LayoutElement` when width animation is wanted.

## Testing

EditMode (frame-free): instant-path endpoints for every channel; retarget-from-current via the `ApplyProgress` seam; unclamped-scale / clamped-color at eased > 1; sprite-null leaves sprite untouched; pivot + missing-LayoutElement warnings; icon/label application; `TabButtonData` old-ctor compatibility; config icon round-trip; `TabBarView` pass-through. Motion itself is play-mode verified in shikaku before tagging. Existing 336 tests must pass unmodified.
