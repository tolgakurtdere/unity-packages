using TK.Ads;
using TK.RemoteConfig;

namespace TK.RemoteConfig.Samples.IntegrationExamples
{
    /// <summary>
    /// Bridges <c>com.tk.ads</c>'s <see cref="IAdsPacingResolver"/> to a live
    /// <see cref="RemoteConfigService"/>: pacing values (interstitial interval, cooldown, ...) are
    /// read from remote config at check time, so a live config change is picked up automatically.
    ///
    /// This is a <b>Sample</b>: it references <c>TK.Ads</c>, so it only compiles when
    /// <c>com.tk.ads</c> is present. <c>com.tk.remoteconfig</c> has <b>no</b> dependency on the ads
    /// package — copy this file into your project to connect the two.
    ///
    /// The pacing keys are supplied by the ads package (e.g. <c>AdsPacingKeys.InterstitialInterval</c>);
    /// this resolver just forwards them to <c>RemoteConfigService.GetInt</c>, which returns
    /// <paramref name="defaultSeconds"/> before init, for an unknown key, or when an editor override
    /// is not set.
    /// </summary>
    public sealed class RcAdsPacingResolver : IAdsPacingResolver
    {
        private readonly RemoteConfigService _rc;

        public RcAdsPacingResolver(RemoteConfigService rc) => _rc = rc;

        public int ResolveSeconds(string key, int defaultSeconds) => _rc.GetInt(key, defaultSeconds);
    }
}
