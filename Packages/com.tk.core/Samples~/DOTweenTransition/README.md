# DOTween Popup Transition

Requires [DOTween](http://dotween.demigiant.com/) (Asset Store) in your project. DOTween has no official UPM package, which is why this ships as a sample instead of a `package.json` dependency.

`DOTweenPopupTransition` is an `IUITransition` adapter: fade + bouncy scale + background dim on show, the reverse (faster) on hide, driven by a DOTween `Sequence` and awaited via `AsyncWaitForCompletion()` (DOTween has no native `Awaitable` support).

`AsyncWaitForCompletion()` requires DOTween's async/Tasks support to be enabled (DOTween Utility Panel → Create ASMDEF / enable Tasks — the standard DOTween async-module setup), otherwise the awaited Task never completes.

Use it from a popup:

```csharp
protected override IUITransition CreateTransition() => new DOTweenPopupTransition();
```
