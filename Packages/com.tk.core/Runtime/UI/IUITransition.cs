using UnityEngine;
using UnityEngine.UI;

namespace TK.Core.UI
{
    /// <summary>
    /// Everything a transition needs to animate a UI element.
    /// Fields may be null (e.g. no background disabler) — implementations must tolerate that.
    /// </summary>
    public readonly struct UITransitionContext
    {
        public CanvasGroup CanvasGroup { get; }
        public RectTransform Container { get; }
        public Image BackgroundDisabler { get; }
        public float Duration { get; }

        public UITransitionContext(CanvasGroup canvasGroup, RectTransform container, Image backgroundDisabler, float duration)
        {
            CanvasGroup = canvasGroup;
            Container = container;
            BackgroundDisabler = backgroundDisabler;
            Duration = duration;
        }
    }

    /// <summary>
    /// Pluggable show/hide animation for popups. The package ships a dependency-free
    /// default; PrimeTween/DOTween adapters are available as package samples.
    /// </summary>
    public interface IUITransition
    {
        Awaitable PlayShowAsync(UITransitionContext context);
        Awaitable PlayHideAsync(UITransitionContext context);
    }
}
