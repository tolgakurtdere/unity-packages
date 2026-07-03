using PrimeTween;
using TK.Core.UI;
using UnityEngine;

namespace TK.Samples.PrimeTweenTransition
{
    /// <summary>
    /// IUITransition backed by PrimeTween. Use from a popup:
    /// protected override IUITransition CreateTransition() => new PrimeTweenPopupTransition();
    /// </summary>
    public sealed class PrimeTweenPopupTransition : IUITransition
    {
        private const float HideDurationFactor = 0.8f;
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.7f);

        public async Awaitable PlayShowAsync(UITransitionContext ctx)
        {
            if (ctx.CanvasGroup) ctx.CanvasGroup.alpha = 0f;
            if (ctx.Container) ctx.Container.localScale = Vector3.one * 0.8f;
            if (ctx.BackgroundDisabler) ctx.BackgroundDisabler.color = Color.clear;

            var seq = Sequence.Create();
            if (ctx.CanvasGroup) _ = seq.Group(Tween.Alpha(ctx.CanvasGroup, 1f, ctx.Duration));
            if (ctx.Container) _ = seq.Group(Tween.Scale(ctx.Container, Vector3.one, ctx.Duration, Ease.OutBack));
            if (ctx.BackgroundDisabler) _ = seq.Group(Tween.Color(ctx.BackgroundDisabler, DimColor, ctx.Duration));
            await seq;
        }

        public async Awaitable PlayHideAsync(UITransitionContext ctx)
        {
            var duration = ctx.Duration * HideDurationFactor;
            var seq = Sequence.Create();
            if (ctx.CanvasGroup) _ = seq.Group(Tween.Alpha(ctx.CanvasGroup, 0f, duration));
            if (ctx.Container) _ = seq.Group(Tween.Scale(ctx.Container, Vector3.one * 0.8f, duration, Ease.InBack));
            if (ctx.BackgroundDisabler) _ = seq.Group(Tween.Color(ctx.BackgroundDisabler, Color.clear, duration));
            await seq;
        }
    }
}
