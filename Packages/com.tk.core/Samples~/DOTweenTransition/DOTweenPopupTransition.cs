using DG.Tweening;
using TK.Core.UI;
using UnityEngine;

namespace TK.Samples.DOTweenTransition
{
    /// <summary>
    /// IUITransition backed by DOTween. Use from a popup:
    /// protected override IUITransition CreateTransition() => new DOTweenPopupTransition();
    /// </summary>
    public sealed class DOTweenPopupTransition : IUITransition
    {
        private const float HideDurationFactor = 0.8f;
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.7f);

        public async Awaitable PlayShowAsync(UITransitionContext ctx)
        {
            if (ctx.CanvasGroup) ctx.CanvasGroup.alpha = 0f;
            if (ctx.Container) ctx.Container.localScale = Vector3.one * 0.8f;
            if (ctx.BackgroundDisabler) ctx.BackgroundDisabler.color = Color.clear;

            var sequence = DOTween.Sequence();
            if (ctx.CanvasGroup) sequence.Join(ctx.CanvasGroup.DOFade(1f, ctx.Duration));
            if (ctx.Container) sequence.Join(ctx.Container.DOScale(Vector3.one, ctx.Duration).SetEase(Ease.OutBack));
            if (ctx.BackgroundDisabler) sequence.Join(ctx.BackgroundDisabler.DOColor(DimColor, ctx.Duration));
            await sequence.AsyncWaitForCompletion();
        }

        public async Awaitable PlayHideAsync(UITransitionContext ctx)
        {
            var duration = ctx.Duration * HideDurationFactor;
            var sequence = DOTween.Sequence();
            // DOFade/DOScale/DOColor tween from each target's CURRENT value, so no explicit
            // "from" state is needed here (unlike the dependency-free default, which has to
            // read+store the current values itself before it can lerp).
            if (ctx.CanvasGroup) sequence.Join(ctx.CanvasGroup.DOFade(0f, duration));
            if (ctx.Container) sequence.Join(ctx.Container.DOScale(Vector3.one * 0.8f, duration).SetEase(Ease.InBack));
            if (ctx.BackgroundDisabler) sequence.Join(ctx.BackgroundDisabler.DOColor(Color.clear, duration));
            await sequence.AsyncWaitForCompletion();
        }
    }
}
