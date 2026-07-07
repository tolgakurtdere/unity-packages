using System;
using System.Collections.Generic;
using UnityEngine;

namespace TK.Core.UI
{
    public sealed class DefaultOrderedTabTransition : IOrderedTabTransition
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
            var elapsed = 0f;
            var currentPosition = clampedStartPosition;
            ApplyPositions(orderedLayouts, firstIndex, lastIndex, currentPosition, width);

            while (elapsed < duration)
            {
                if (shouldInterrupt?.Invoke() == true)
                    return TabTransitionResult.InterruptedAt(currentPosition);

                // A layout can be destroyed while the motion runs (scene teardown, a released
                // tab): report the reached position instead of throwing mid-apply.
                if (AnyDestroyed(orderedLayouts, firstIndex, lastIndex))
                    return TabTransitionResult.InterruptedAt(currentPosition);

                elapsed += settings.GetDeltaTime();
                var k = settings.Evaluate(elapsed / duration);
                currentPosition = Mathf.Lerp(clampedStartPosition, targetIndex, k);
                ApplyPositions(orderedLayouts, firstIndex, lastIndex, currentPosition, width);

                await Awaitable.NextFrameAsync();
            }

            if (AnyDestroyed(orderedLayouts, firstIndex, lastIndex))
                return TabTransitionResult.InterruptedAt(currentPosition);

            ApplyPositions(orderedLayouts, firstIndex, lastIndex, targetIndex, width);
            return TabTransitionResult.CompletedAt(targetIndex);
        }

        private static bool AnyDestroyed(IReadOnlyList<LayoutBase> orderedLayouts, int firstIndex, int lastIndex)
        {
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                if (!orderedLayouts[index]) return true;
            }

            return false;
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
    }
}
