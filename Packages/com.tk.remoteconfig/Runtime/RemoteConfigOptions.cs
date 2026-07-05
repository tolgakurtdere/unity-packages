namespace TK.RemoteConfig
{
    /// <summary>Optional composition for RemoteConfigService.</summary>
    public sealed class RemoteConfigOptions
    {
        /// <summary>When true, InitializeAsync also fetches+activates. When false, call RefreshAsync yourself.</summary>
        public bool FetchOnInitialize = true;
    }
}
