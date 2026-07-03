namespace TK.Core.UI
{
    /// <summary>
    /// Modes for showing a popup.
    /// </summary>
    public enum PopupShowMode
    {
        Queue, // Default: Add to queue, show sequentially
        Immediate, // Show immediately, stacking on top of current (bypasses queue)
        Optional // Show only if no other popup is active/queued; otherwise discard
    }
}
