namespace TK.Localization
{
    /// <summary>
    /// Public RTL text utilities. IsRtl detects RTL content; Fix shapes Arabic/Farsi (glyphs, tashkeel,
    /// ligatures), preserves rich-text tags, and reverses for display. Main-thread-affine (reuses a static
    /// buffer, matching the reference). Pure — no Unity Localization dependency.
    /// </summary>
    public static class RtlText
    {
        // Reuse a static buffer like the reference (main-thread only).
        // RtlStringBuilder has no char[] ctor; the int-capacity ctor pre-sizes the backing array
        // (it grows automatically when needed).
        private static readonly RtlStringBuilder s_buffer = new RtlStringBuilder(512);

        /// <summary>
        /// Returns true when <paramref name="text"/> contains RTL (Arabic/Hebrew) content and should be shaped.
        /// </summary>
        public static bool IsRtl(string text)
        {
            return !string.IsNullOrEmpty(text) && RtlTextUtils.IsRTLInput(text);
        }

        /// <summary>
        /// Shapes Arabic/Farsi text for display: fixes glyph forms, restores tashkeel, resolves ligatures,
        /// preserves rich-text tags, and reverses the flow for a left-to-right text box.
        /// Non-RTL input (including null/empty) is returned unchanged — mirroring the reference's
        /// <c>FontLocalizer</c>, which only shapes when <see cref="IsRtl"/> is true and passes LTR text through.
        /// </summary>
        public static string Fix(string text)
        {
            if (!IsRtl(text)) return text;
            s_buffer.Clear();
            RtlSupport.FixRTL(text, s_buffer, farsi: true, fixTextTags: true, preserveNumbers: true);
            s_buffer.Reverse();
            return s_buffer.ToString();
        }
    }
}
