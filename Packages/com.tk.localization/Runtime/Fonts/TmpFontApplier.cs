using TMPro;

namespace TK.Localization
{
    /// <summary>Applies a resolved <see cref="LocaleFontInfo"/> to a TMP_Text (font, optional material, direction).</summary>
    public static class TmpFontApplier
    {
        public static void Apply(TMP_Text text, LocaleFontInfo info)
        {
            if (!text || !info) return;
            if (info.Font) text.font = info.Font;
            if (info.Material) text.fontSharedMaterial = info.Material;
            text.isRightToLeftText = info.RightToLeft;
        }
    }
}
