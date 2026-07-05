using TMPro;

namespace TK.Localization
{
    /// <summary>Applies a resolved <see cref="LocaleFontInfo"/> to a TMP_Text (font, optional material, direction).</summary>
    public static class TmpFontApplier
    {
        public static void Apply(TMP_Text text, LocaleFontInfo info)
        {
            if (text == null || info == null) return;
            if (info.Font != null) text.font = info.Font;
            if (info.Material != null) text.fontSharedMaterial = info.Material;
            text.isRightToLeftText = info.RightToLeft;
        }
    }
}
