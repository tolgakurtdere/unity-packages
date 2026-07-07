using PrimeTween;
using TK.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TK.Samples.PrimeTweenTabBar
{
    /// <summary>
    /// Animated ITabButtonPresenter backed by PrimeTween: background/label color crossfade plus a
    /// small scale pop on selection. Put it on the tab button template — TabBarView discovers
    /// presenters via GetComponentsInChildren and passes instant=true for the initial selection.
    /// </summary>
    public sealed class PrimeTweenTabButtonPresenter : MonoBehaviour, ITabButtonPresenter
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Color selectedBackground = new(0.357f, 0.357f, 0.839f);
        [SerializeField] private Color normalBackground = new(0.937f, 0.929f, 0.973f);
        [SerializeField] private Color selectedText = Color.white;
        [SerializeField] private Color normalText = new(0.13f, 0.13f, 0.25f);
        [SerializeField, Min(1f)] private float selectedScale = 1.08f;
        [SerializeField, Min(0.01f)] private float duration = 0.15f;

        private Sequence _sequence;

        public void Initialize(TabButtonData data, Button button)
        {
            if (!label && button)
                label = button.GetComponentInChildren<TMP_Text>(true);
            if (!background && button)
                background = button.image ? button.image : button.GetComponent<Image>();

            if (label)
                label.text = data.Label;

            SetSelected(false, instant: true);
        }

        public void SetSelected(bool isSelected, bool instant)
        {
            _sequence.Stop(); // dead/default handles are safe to stop

            var backgroundColor = isSelected ? selectedBackground : normalBackground;
            var textColor = isSelected ? selectedText : normalText;
            var scale = Vector3.one * (isSelected ? selectedScale : 1f);

            if (instant)
            {
                if (background) background.color = backgroundColor;
                if (label) label.color = textColor;
                transform.localScale = scale;
                return;
            }

            // Unscaled time so tab visuals keep animating while time scale is paused.
            _sequence = Sequence.Create(useUnscaledTime: true);
            if (background) _ = _sequence.Group(Tween.Color(background, backgroundColor, duration));
            if (label) _ = _sequence.Group(Tween.Color(label, textColor, duration)); // TMP_Text is a Graphic
            _ = _sequence.Group(Tween.Scale(transform, scale, duration, isSelected ? Ease.OutBack : Ease.OutQuad));
        }

        private void OnDestroy() => _sequence.Stop();
    }
}
