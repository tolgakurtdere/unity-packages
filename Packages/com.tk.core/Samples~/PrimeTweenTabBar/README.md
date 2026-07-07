# PrimeTween Tab Bar Animations

Requires [PrimeTween](https://assetstore.unity.com/packages/tools/animation/primetween-266130) (Asset Store) in your project.

Two adapters for the tab bar module:

- `PrimeTweenTabButtonPresenter` — animated `ITabButtonPresenter`: background/label color crossfade plus a small scale pop on selection, on unscaled time so it keeps animating on pause screens. Put it on your tab button template; `TabBarView` discovers it automatically and passes `instant: true` for the initial selection (which this presenter snaps instead of tweening).
- `PrimeTweenOrderedTabTransition` — `IOrderedTabTransition` adapter. PrimeTween drives a linear 0→1 progress (`Tween.Custom`, awaited directly) while `TabTransitionSettings.Evaluate` shapes the motion, so feel stays identical to the built-in default and your `TabBarConfig` remains the single source of truth. The interrupt contract is honored: the value callback polls before applying positions, stops the tween on interrupt, and reports the reached fractional position so retargets stay seamless.

Wire the transition once, where you create your navigator:

```csharp
navigator.OrderedTransition = new PrimeTweenOrderedTabTransition();
```
