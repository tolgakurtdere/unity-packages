using TK.IAP;
using TK.RemoteConfig;

namespace TK.RemoteConfig.Samples.IntegrationExamples
{
    /// <summary>
    /// Bridges <c>com.tk.iap</c>'s <see cref="IIapAmountResolver"/> to a live
    /// <see cref="RemoteConfigService"/>: catalog item amounts are read from remote config at read
    /// time (both when applying a purchase and when the UI shows a wallet amount), so a live config
    /// change is picked up automatically.
    ///
    /// This is a <b>Sample</b>: it references <c>TK.IAP</c>, so it only compiles when
    /// <c>com.tk.iap</c> is present. <c>com.tk.remoteconfig</c> has <b>no</b> dependency on the IAP
    /// package — copy this file into your project to connect the two.
    ///
    /// The key convention below (<c>"{productId}_{itemType}_amount"</c>, e.g.
    /// <c>"pack1_coins_amount"</c>) is a <b>game choice</b> — adapt it to whatever naming you use in
    /// your remote-config console. <c>GetInt</c> returns <paramref name="defaultAmount"/> (the
    /// catalog value) before init, for an unknown key, or when no editor override is set — so a
    /// missing remote key can never zero out an amount.
    /// </summary>
    public sealed class RcIapAmountResolver : IIapAmountResolver
    {
        private readonly RemoteConfigService _rc;

        public RcIapAmountResolver(RemoteConfigService rc) => _rc = rc;

        public int Resolve(string productId, string itemType, int defaultAmount)
            => _rc.GetInt($"{productId}_{itemType}_amount", defaultAmount);
    }
}
