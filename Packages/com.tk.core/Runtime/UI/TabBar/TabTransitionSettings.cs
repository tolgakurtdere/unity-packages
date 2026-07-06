using System;
using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// Dependency-free transition tuning for ordered tab page movement.
    /// </summary>
    [Serializable]
    public sealed class TabTransitionSettings
    {
        [SerializeField, Min(0.01f)]
        [Tooltip("Duration for a one-step tab transition.")]
        private float minDuration = 0.2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Upper limit after the extra step multiplier is applied.")]
        private float maxDuration = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Extra duration added per additional tab step. " +
                 "0 keeps total duration constant, 1 behaves like per-step duration.")]
        private float extraStepDurationMultiplier = 0.5f;

        [SerializeField]
        [Tooltip("Easing curve evaluated from 0 to 1 during the transition.")]
        private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField]
        [Tooltip("Use unscaled time so tab transitions keep running while time scale is paused.")]
        private bool useUnscaledTime = true;

        public static TabTransitionSettings Default { get; } = new();

        public float MinDuration => Mathf.Max(0.01f, minDuration);
        public float MaxDuration => Mathf.Max(MinDuration, maxDuration);
        public float ExtraStepDurationMultiplier => Mathf.Clamp01(extraStepDurationMultiplier);
        public AnimationCurve Easing => easing;
        public bool UseUnscaledTime => useUnscaledTime;

        public float CalculateDuration(int stepCount)
        {
            var steps = Mathf.Max(1, stepCount);
            var duration = MinDuration + (steps - 1) * MinDuration * ExtraStepDurationMultiplier;
            return Mathf.Clamp(duration, MinDuration, MaxDuration);
        }

        public float Evaluate(float normalizedTime)
        {
            var t = Mathf.Clamp01(normalizedTime);
            return Easing?.Evaluate(t) ?? Mathf.SmoothStep(0f, 1f, t);
        }

        public float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
