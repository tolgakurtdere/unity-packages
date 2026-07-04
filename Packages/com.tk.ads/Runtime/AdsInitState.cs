namespace TK.Ads
{
    /// <summary>Lifecycle state of the ads service.</summary>
    public enum AdsInitState
    {
        NotInitialized = 0,
        Initializing,
        Initialized,
        Failed
    }
}
