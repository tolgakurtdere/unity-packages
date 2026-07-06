using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TK.Core.UI
{
    /// <summary>
    /// Placeholder tab button visuals: swaps background/label colors on selection. Colors are
    /// serialized so a designer can retint without code; a real design replaces this presenter
    /// (or its colors) without touching TabBarView, which only talks to ITabButtonPresenter.
    /// </summary>
    public class DefaultTabButtonPresenter : MonoBehaviour, ITabButtonPresenter
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Color selectedBackground = new(0.357f, 0.357f, 0.839f);
        [SerializeField] private Color normalBackground = new(0.937f, 0.929f, 0.973f);
        [SerializeField] private Color selectedText = Color.white;
        [SerializeField] private Color normalText = new(0.13f, 0.13f, 0.25f);

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
            if (background)
                background.color = isSelected ? selectedBackground : normalBackground;
            if (label)
                label.color = isSelected ? selectedText : normalText;
        }
    }
}
