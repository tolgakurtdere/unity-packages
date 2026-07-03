namespace TK.Core.UI
{
    /// <summary>Minimal easing functions used by the built-in transitions.</summary>
    public static class UIEasing
    {
        private const float C1 = 1.70158f;
        private const float C3 = C1 + 1f;

        /// <summary>Overshoots past 1 then settles (matches PrimeTween/DOTween OutBack).</summary>
        public static float OutBack(float t)
        {
            var u = t - 1f;
            return 1f + C3 * u * u * u + C1 * u * u;
        }

        /// <summary>Pulls back below 0 then accelerates (matches InBack).</summary>
        public static float InBack(float t)
        {
            return C3 * t * t * t - C1 * t * t;
        }
    }
}
