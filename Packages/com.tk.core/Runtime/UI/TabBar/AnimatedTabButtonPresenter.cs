using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TK.Core.UI
{
    /// <summary>
    /// Full-featured tab button visuals: sprite swap + bottom-pivot scale pop + layout-aware
    /// width growth + label fade, curve-driven and dependency-free. Horizontal growth animates
    /// LayoutElement.preferredWidth so the bar's HorizontalLayoutGroup slides neighbors aside —
    /// no overlap and no off-screen by construction (edge safety comes from container padding);
    /// vertical growth is a visual scale from a bottom-centre (0.5, 0) pivot, so the button
    /// grows up + sideways only. Subclass and override ApplyProgress to add channels without
    /// rewriting the timing/reentrancy engine, or implement ITabButtonPresenter from scratch.
    /// </summary>
    public class AnimatedTabButtonPresenter : MonoBehaviour, ITabButtonPresenter
    {
        [Header("References (auto-resolved from the button when empty)")]
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform scaleTarget;
        [SerializeField] private LayoutElement layoutElement;

        [Header("Sprites (null keeps the current sprite)")]
        [SerializeField] private Sprite normalBackgroundSprite;
        [SerializeField] private Sprite selectedBackgroundSprite;

        [Header("Colors")]
        [SerializeField] private Color normalBackgroundColor = Color.white;
        [SerializeField] private Color selectedBackgroundColor = Color.white;
        [SerializeField] private Color normalTextColor = new(0.13f, 0.13f, 0.25f);
        [SerializeField] private Color selectedTextColor = Color.white;

        [Header("Size")]
        [SerializeField, Min(1f)]
        [Tooltip("Visual scale of the selected button. Grows from the pivot — set the scale target's pivot to (0.5, 0) so it grows up + sideways only.")]
        private float selectedScale = 1.1f;

        [SerializeField, Min(0f)]
        [Tooltip("Slot width of an unselected button. Width animates only when BOTH widths are above 0 and a LayoutElement is present.")]
        private float normalWidth;

        [SerializeField, Min(0f)]
        [Tooltip("Slot width of the selected button, driven via LayoutElement.preferredWidth so the layout group makes room for it.")]
        private float selectedWidth;

        [Header("Label")]
        [SerializeField]
        [Tooltip("Show the label only on the selected tab, faded via TMP alpha (never SetActive — that would churn the layout).")]
        private bool showLabelOnlyWhenSelected = true;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float duration = 0.18f;

        [SerializeField]
        [Tooltip("Eased progress over normalized time; values above 1 overshoot scale/width (soft out-back by default).")]
        private AnimationCurve easing = DefaultEasing();

        [SerializeField] private bool useUnscaledTime = true;

        private VisualState _from;
        private VisualState _target;
        private int _generation;

        private bool AnimatesWidth => normalWidth > 0f && selectedWidth > 0f && layoutElement;

        public virtual void Initialize(TabButtonData data, Button button)
        {
            if (!background && button)
                background = button.image ? button.image : button.GetComponent<Image>();
            if (!label && button)
                label = button.GetComponentInChildren<TMP_Text>(true);
            if (!scaleTarget)
                scaleTarget = (RectTransform)transform;
            if (!layoutElement)
                layoutElement = GetComponent<LayoutElement>();

            if (label)
                label.text = data.Label;
            if (icon && data.Icon)
                icon.sprite = data.Icon;

            // Warn-only: silently changing a pivot would shift the button's position.
            if (selectedScale > 1f && scaleTarget && scaleTarget.pivot != new Vector2(0.5f, 0f))
            {
                Debug.LogWarning($"[AnimatedTabButtonPresenter] '{name}': scale grows from the pivot — set the scale " +
                                 $"target's pivot to (0.5, 0) so the button grows up + sideways only (current: {scaleTarget.pivot}).");
            }

            if (selectedWidth > 0f && !layoutElement)
            {
                Debug.LogWarning($"[AnimatedTabButtonPresenter] '{name}': width animation needs a LayoutElement on the " +
                                 "button (plus a HorizontalLayoutGroup on the bar) — the width channel is skipped.");
            }

            SetSelected(false, instant: true);
        }

        public void SetSelected(bool isSelected, bool instant)
        {
            _generation++;

            // Sprite swaps at t=0 — the motion plays over the new art (reference behavior).
            var sprite = isSelected ? selectedBackgroundSprite : normalBackgroundSprite;
            if (background && sprite)
                background.sprite = sprite;

            // FROM is always the components' current live values, so a retarget mid-animation
            // continues from what's on screen instead of jumping to a cached endpoint.
            _from = CaptureCurrent();
            _target = BuildTarget(isSelected);

            if (instant)
            {
                ApplyProgress(1f);
                return;
            }

            AnimateAsync(_generation);
        }

        private async void AnimateAsync(int generation)
        {
            try
            {
                var elapsed = 0f;
                while (true)
                {
                    await Awaitable.NextFrameAsync();
                    if (!this || generation != _generation) return;

                    elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    var progress = Mathf.Clamp01(elapsed / duration);
                    ApplyProgress(easing != null ? easing.Evaluate(progress) : progress);
                    if (progress >= 1f) return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private VisualState CaptureCurrent()
        {
            var state = new VisualState
            {
                BackgroundColor = background ? background.color : Color.white,
                TextColor = label ? label.color : Color.white,
                LabelAlpha = label ? label.alpha : 1f,
                Scale = scaleTarget ? scaleTarget.localScale.x : 1f,
                Width = AnimatesWidth ? layoutElement.preferredWidth : 0f
            };

            // An untouched LayoutElement reports preferredWidth -1 (disabled) — lerping from
            // there would sweep through negative widths on the very first animation.
            if (AnimatesWidth && state.Width < 0f)
                state.Width = normalWidth;

            return state;
        }

        private VisualState BuildTarget(bool isSelected) => new()
        {
            BackgroundColor = isSelected ? selectedBackgroundColor : normalBackgroundColor,
            TextColor = isSelected ? selectedTextColor : normalTextColor,
            LabelAlpha = showLabelOnlyWhenSelected ? (isSelected ? 1f : 0f) : 1f,
            Scale = isSelected ? selectedScale : 1f,
            Width = isSelected ? selectedWidth : normalWidth
        };

        /// <summary>
        /// Single write point for one eased step. Eased progress may exceed 1 (overshoot):
        /// scale and width lerp unclamped so the pop breathes past its target; colors and
        /// label alpha use clamped progress (color overshoot reads as broken tinting).
        /// </summary>
        protected virtual void ApplyProgress(float easedProgress)
        {
            var clamped = Mathf.Clamp01(easedProgress);

            if (background)
                background.color = Color.Lerp(_from.BackgroundColor, _target.BackgroundColor, clamped);

            if (label)
            {
                label.color = Color.Lerp(_from.TextColor, _target.TextColor, clamped);
                label.alpha = Mathf.Lerp(_from.LabelAlpha, _target.LabelAlpha, clamped);
            }

            if (scaleTarget)
            {
                var scale = Mathf.LerpUnclamped(_from.Scale, _target.Scale, easedProgress);
                scaleTarget.localScale = new Vector3(scale, scale, 1f);
            }

            if (AnimatesWidth)
                layoutElement.preferredWidth = Mathf.LerpUnclamped(_from.Width, _target.Width, easedProgress);
        }

        // Fast start, ~6% overshoot around t=0.7, settle to 1 — a soft out-back.
        private static AnimationCurve DefaultEasing() => new(
            new Keyframe(0f, 0f, 0f, 2.4f),
            new Keyframe(0.7f, 1.06f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        private struct VisualState
        {
            public Color BackgroundColor;
            public Color TextColor;
            public float LabelAlpha;
            public float Scale;
            public float Width;
        }
    }
}
