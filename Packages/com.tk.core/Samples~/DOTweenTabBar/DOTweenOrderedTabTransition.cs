using System;
using System.Collections.Generic;
using DG.Tweening;
using TK.Core.UI;
using UnityEngine;

namespace TK.Samples.DOTweenTabBar
{
    /// <summary>
    /// IOrderedTabTransition backed by DOTween. Wire it once, where you create your navigator:
    /// navigator.OrderedTransition = new DOTweenOrderedTabTransition();
    /// DOTween drives a LINEAR 0→1 progress (time, lifecycle, unscaled-time handling) while
    /// TabTransitionSettings.Evaluate shapes it — motion stays identical to the built-in
    /// DefaultOrderedTabTransition and your TabBarConfig remains the single source of truth.
    /// </summary>
    public sealed class DOTweenOrderedTabTransition : IOrderedTabTransition
    {
        public async Awaitable<TabTransitionResult> PlayAsync(IReadOnlyList<LayoutBase> orderedLayouts,
            float startPosition, int targetIndex, RectTransform container, TabTransitionSettings settings,
            Func<bool> shouldInterrupt)
        {
            if (orderedLayouts == null || targetIndex < 0 || targetIndex >= orderedLayouts.Count)
            {
                return TabTransitionResult.InterruptedAt(startPosition);
            }

            settings ??= TabTransitionSettings.Default;

            var clampedStartPosition = Mathf.Clamp(startPosition, 0f, orderedLayouts.Count - 1f);
            var firstIndex = Mathf.Min(Mathf.FloorToInt(clampedStartPosition), targetIndex);
            var lastIndex = Mathf.Max(Mathf.CeilToInt(clampedStartPosition), targetIndex);
            var stepCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(targetIndex - clampedStartPosition)));

            var target = orderedLayouts[targetIndex];
            if (!target) return TabTransitionResult.InterruptedAt(clampedStartPosition);

            var width = container ? container.rect.width : ((RectTransform)target.transform).rect.width;
            if (width <= 0f) width = Screen.width;

            if (AnyDestroyed(orderedLayouts, firstIndex, lastIndex))
                return TabTransitionResult.InterruptedAt(clampedStartPosition);

            var duration = settings.CalculateDuration(stepCount);
            var currentPosition = clampedStartPosition;
            var interrupted = false;
            ApplyPositions(orderedLayouts, firstIndex, lastIndex, currentPosition, width);

            Tweener tween = null;
            tween = DOVirtual.Float(0f, 1f, duration, progress =>
                {
                    if (interrupted) return;

                    // CONTRACT: poll once per motion tick BEFORE applying positions; on interrupt
                    // (or a layout destroyed mid-motion) stop without applying this tick's value,
                    // so the reported fractional position is the last one actually on screen.
                    if (shouldInterrupt?.Invoke() == true || AnyDestroyed(orderedLayouts, firstIndex, lastIndex))
                    {
                        interrupted = true;
                        tween.Kill();
                        return;
                    }

                    currentPosition = Mathf.Lerp(clampedStartPosition, targetIndex, settings.Evaluate(progress));
                    ApplyPositions(orderedLayouts, firstIndex, lastIndex, currentPosition, width);
                })
                .SetEase(Ease.Linear)
                .SetUpdate(settings.UseUnscaledTime);

            // Resumes on completion AND on Kill (its wait loop exits when the tween goes inactive).
            await tween.AsyncWaitForCompletion();

            if (interrupted) return TabTransitionResult.InterruptedAt(currentPosition);

            ApplyPositions(orderedLayouts, firstIndex, lastIndex, targetIndex, width);
            return TabTransitionResult.CompletedAt(targetIndex);
        }

        private static void ApplyPositions(IReadOnlyList<LayoutBase> orderedLayouts, int firstIndex, int lastIndex,
            float position, float width)
        {
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                var rect = (RectTransform)orderedLayouts[index].transform;
                rect.anchoredPosition = new Vector2((index - position) * width, 0f);
            }
        }

        private static bool AnyDestroyed(IReadOnlyList<LayoutBase> orderedLayouts, int firstIndex, int lastIndex)
        {
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                if (!orderedLayouts[index]) return true;
            }

            return false;
        }
    }
}
