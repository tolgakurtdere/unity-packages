namespace TK.Core.UI
{
    public readonly struct TabButtonData
    {
        public TabButtonData(string layoutKey, string label, int index)
        {
            LayoutKey = layoutKey;
            Label = label;
            Index = index;
        }

        public string LayoutKey { get; }
        public string Label { get; }
        public int Index { get; }
    }
}
