using UnityEngine;

namespace TK.Core.UI
{
    /// <summary>
    /// Dependency-free default curtain: a CanvasGroup alpha fade over UNSCALED time, so the
    /// curtain keeps animating when the game pauses gameplay with timeScale = 0 around a swap.
    /// Durations are prefab configuration; the visual (color, logo, layout) is the prefab's own
    /// hierarchy — this component only drives the root CanvasGroup. Zero duration jumps
    /// synchronously (never touches the player loop).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeCurtainView : TransitionCurtainView
    {
        [SerializeField, Min(0f)] private float showDuration = 0.25f;
        [SerializeField, Min(0f)] private float hideDuration = 0.25f;

        private CanvasGroup _canvasGroup;

        private CanvasGroup Group => _canvasGroup ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        /// <summary>Seconds to fade to full cover. Settable in code for the generated fallback curtain.</summary>
        public float ShowDuration { get => showDuration; set => showDuration = Mathf.Max(0f, value); }

        /// <summary>Seconds to fade back open.</summary>
        public float HideDuration { get => hideDuration; set => hideDuration = Mathf.Max(0f, value); }

        protected virtual void Awake()
        {
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
        }

        public override async Awaitable ShowAsync()
        {
            // Block from the first fade frame, not only at full cover.
            Group.blocksRaycasts = true;
            await FadeAsync(1f, showDuration);
        }

        public override async Awaitable HideAsync()
        {
            await FadeAsync(0f, hideDuration);
            if (this) Group.blocksRaycasts = false;
        }

        private async Awaitable FadeAsync(float target, float duration)
        {
            // Start from the CURRENT alpha so a reversal mid-animation doesn't snap.
            var from = Group.alpha;

            if (duration <= 0f)
            {
                Group.alpha = target;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                await Awaitable.NextFrameAsync();
                if (!this) return;
                elapsed += Time.unscaledDeltaTime;
                Group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
            }
        }
    }
}
