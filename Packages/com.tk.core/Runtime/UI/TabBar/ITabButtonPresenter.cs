using UnityEngine.UI;

namespace TK.Core.UI
{
    public interface ITabButtonPresenter
    {
        void Initialize(TabButtonData data, Button button);
        void SetSelected(bool isSelected, bool instant);
    }
}
