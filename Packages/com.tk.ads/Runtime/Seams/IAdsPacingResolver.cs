namespace TK.Ads
{
    /// <summary>
    /// Optional seam for overriding pacing values at read time (e.g. from remote config).
    /// Called wherever pacing is checked, so late-arriving config is picked up automatically.
    /// Return defaultSeconds to keep the built-in value. Well-known keys live in AdsPacingKeys.
    /// </summary>
    public interface IAdsPacingResolver
    {
        int ResolveSeconds(string key, int defaultSeconds);
    }
}
