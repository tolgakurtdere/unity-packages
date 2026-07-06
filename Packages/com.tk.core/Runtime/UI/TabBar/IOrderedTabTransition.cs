using System;
using System.Collections.Generic;
using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// Animates ordered tab layouts from a (possibly fractional) start position to the target
    /// index. Contract: <paramref name="shouldInterrupt"/> must be polled once per frame of
    /// motion; on a true result the implementation stops moving layouts immediately and reports
    /// the reached position via <see cref="TabTransitionResult.InterruptedAt"/> so the caller
    /// can retarget from it.
    /// </summary>
    public interface IOrderedTabTransition
    {
        Awaitable<TabTransitionResult> PlayAsync(IReadOnlyList<LayoutBase> orderedLayouts, float startPosition,
            int targetIndex, RectTransform container, TabTransitionSettings settings, Func<bool> shouldInterrupt);
    }
}
