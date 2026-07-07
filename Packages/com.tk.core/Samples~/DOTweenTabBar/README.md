# DOTween Tab Bar Animations

Requires [DOTween](http://dotween.demigiant.com/) (Asset Store) in your project. DOTween has no official UPM package, which is why this ships as a sample instead of a `package.json` dependency.

Two adapters for the tab bar module:

- `DOTweenTabButtonPresenter` — animated `ITabButtonPresenter`: background/label color crossfade plus a small scale pop on selection, on unscaled time so it keeps animating on pause screens. Put it on your tab button template; `TabBarView` discovers it automatically and passes `instant: true` for the initial selection (which this presenter snaps instead of tweening).
- `DOTweenOrderedTabTransition` — `IOrderedTabTransition` adapter. DOTween drives a linear 0→1 progress (`DOVirtual.Float`, awaited via `AsyncWaitForCompletion()`) while `TabTransitionSettings.Evaluate` shapes the motion, so feel stays identical to the built-in default and your `TabBarConfig` remains the single source of truth. The interrupt contract is honored: the value callback polls before applying positions, kills the tween on interrupt, and reports the reached fractional position so retargets stay seamless.

`AsyncWaitForCompletion()` requires DOTween's async/Tasks support to be enabled (DOTween Utility Panel → enable Tasks), same as the popup transition sample.

Wire the transition once, where you create your navigator:

```csharp
navigator.OrderedTransition = new DOTweenOrderedTabTransition();
```
