using UnityEngine;

namespace TK.Core.UI
{
    public readonly struct TabButtonData
    {
        public TabButtonData(string layoutKey, string label, int index) : this(layoutKey, label, index, null)
        {
        }

        public TabButtonData(string layoutKey, string label, int index, Sprite icon)
        {
            LayoutKey = layoutKey;
            Label = label;
            Index = index;
            Icon = icon;
        }

        public string LayoutKey { get; }
        public string Label { get; }
        public int Index { get; }

        /// <summary>Optional per-tab icon from the config entry; null when none was assigned.</summary>
        public Sprite Icon { get; }
    }
}
