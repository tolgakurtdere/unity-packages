# PrimeTween Popup Transition

Requires [PrimeTween](https://assetstore.unity.com/packages/tools/animation/primetween-266130) (Asset Store) in your project.

`PrimeTweenPopupTransition` is an `IUITransition` adapter: fade + bouncy scale + background dim on show, the reverse (faster) on hide, all driven by PrimeTween tweens/sequences instead of the package's dependency-free default.

Use it from a popup:

```csharp
protected override IUITransition CreateTransition() => new PrimeTweenPopupTransition();
```
