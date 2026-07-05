namespace TK.Analytics
{
    /// <summary>User consent state. Default Unknown buffers events until a decision (GDPR-safe).</summary>
    public enum AnalyticsConsent { Unknown, Granted, Denied }
}
