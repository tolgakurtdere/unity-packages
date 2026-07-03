using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// Dependency-free popup transition: fade + bouncy scale + background dim.
    /// Matches the tween-based animation the prototypes shipped with.
    /// </summary>
    public sealed class DefaultPopupTransition : IUITransition
    {
        private const float HideDurationFactor = 0.8f;
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.7f);

        public async Awaitable PlayShowAsync(UITransitionContext ctx)
        {
            await AnimateAsync(ctx, ctx.Duration,
                alphaFrom: 0f, alphaTo: 1f,
                scaleFrom: 0.8f, scaleTo: 1f,
                backgroundFrom: Color.clear, backgroundTo: DimColor,
                scaleEase: UIEasing.OutBack);
        }

        public async Awaitable PlayHideAsync(UITransitionContext ctx)
        {
            // Start from the CURRENT values so a hide requested mid-show doesn't snap to fully-shown first.
            await AnimateAsync(ctx, ctx.Duration * HideDurationFactor,
                alphaFrom: ctx.CanvasGroup ? ctx.CanvasGroup.alpha : 1f, alphaTo: 0f,
                scaleFrom: ctx.Container ? ctx.Container.localScale.x : 1f, scaleTo: 0.8f,
                backgroundFrom: ctx.BackgroundDisabler ? ctx.BackgroundDisabler.color : DimColor, backgroundTo: Color.clear,
                scaleEase: UIEasing.InBack);
        }

        private static async Awaitable AnimateAsync(
            UITransitionContext ctx, float duration,
            float alphaFrom, float alphaTo,
            float scaleFrom, float scaleTo,
            Color backgroundFrom, Color backgroundTo,
            System.Func<float, float> scaleEase)
        {
            // Zero (or negative) duration: jump straight to the end state, fully synchronously
            // (never touches Awaitable.NextFrameAsync, so it is safe outside the player loop).
            if (duration <= 0f)
            {
                Apply(ctx, 1f, alphaFrom, alphaTo, scaleFrom, scaleTo, backgroundFrom, backgroundTo, scaleEase);
                return;
            }

            Apply(ctx, 0f, alphaFrom, alphaTo, scaleFrom, scaleTo, backgroundFrom, backgroundTo, scaleEase);

            var elapsed = 0f;
            while (elapsed < duration)
            {
                await Awaitable.NextFrameAsync();

                // The popup may be destroyed mid-animation (scene unload).
                if (!ctx.Container && !ctx.CanvasGroup) return;

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                Apply(ctx, t, alphaFrom, alphaTo, scaleFrom, scaleTo, backgroundFrom, backgroundTo, scaleEase);
            }
        }

        private static void Apply(
            UITransitionContext ctx, float t,
            float alphaFrom, float alphaTo,
            float scaleFrom, float scaleTo,
            Color backgroundFrom, Color backgroundTo,
            System.Func<float, float> scaleEase)
        {
            if (ctx.CanvasGroup) ctx.CanvasGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, t);
            if (ctx.Container) ctx.Container.localScale = Vector3.one * Mathf.LerpUnclamped(scaleFrom, scaleTo, scaleEase(t));
            if (ctx.BackgroundDisabler) ctx.BackgroundDisabler.color = Color.Lerp(backgroundFrom, backgroundTo, t);
        }
    }
}
