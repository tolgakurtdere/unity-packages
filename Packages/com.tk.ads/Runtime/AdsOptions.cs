using System;

namespace TK.Ads
{
    /// <summary>
    /// Code-side composition for AdsService. All members optional.
    /// Policy delegates: null means "always allowed". Rewarded is deliberately NEVER
    /// policy-gated (remove-ads must not remove rewarded ads).
    /// </summary>
    public sealed class AdsOptions
    {
        /// <summary>Gateway override. Null = MaxAdsGateway (the real SDK). Tests/demos inject fakes.</summary>
        public IAdsGateway Gateway;
        public IAdRevenueReporter RevenueReporter;
        public IAdsPacingResolver PacingResolver;

        public Func<bool> ShouldLoadBanner;
        public Func<bool> ShouldShowBanner;
        public Func<bool> ShouldLoadInterstitial;
        public Func<bool> ShouldShowInterstitial;

        /// <summary>Mute/unmute game audio around fullscreen ads (true = mute). Always balanced.</summary>
        public Action<bool> AudioMuteSetter;

        /// <summary>Test seam: monotonic seconds. Null = Time.realtimeSinceStartup.</summary>
        public Func<float> Clock;

        /// <summary>Test seam: scales load-retry delays (0 = immediate retries in tests). Default 1.</summary>
        public float RetryDelayScale = 1f;
    }
}
