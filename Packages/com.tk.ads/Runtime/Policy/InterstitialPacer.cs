using System;

namespace TK.Ads
{
    /// <summary>
    /// Interstitial pacing: a minimum interval anchored to the CLOSE of the last interstitial,
    /// plus a cooldown after each COMPLETED rewarded ad (players who just watched a rewarded
    /// ad are not hit with an interstitial). Values resolve through IAdsPacingResolver at
    /// check time (late-arriving remote config is picked up automatically).
    /// The first interstitial of a session is never interval-blocked.
    /// </summary>
    public sealed class InterstitialPacer
    {
        private readonly Func<float> _clock;
        private readonly IAdsPacingResolver _resolver;
        private readonly int _defaultIntervalSeconds;
        private readonly int _defaultCooldownSeconds;

        private float _lastInterstitialClosedAt = float.NegativeInfinity;
        private float _lastRewardedCompletedAt = float.NegativeInfinity;

        public InterstitialPacer(Func<float> clock, IAdsPacingResolver resolver, int defaultIntervalSeconds, int defaultCooldownSeconds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _resolver = resolver; // null = defaults only
            _defaultIntervalSeconds = Math.Max(0, defaultIntervalSeconds);
            _defaultCooldownSeconds = Math.Max(0, defaultCooldownSeconds);
        }

        public bool CanShow()
        {
            var now = _clock();
            var interval = Resolve(AdsPacingKeys.InterstitialInterval, _defaultIntervalSeconds);
            var cooldown = Resolve(AdsPacingKeys.CooldownAfterRewarded, _defaultCooldownSeconds);

            return now - _lastInterstitialClosedAt >= interval
                   && now - _lastRewardedCompletedAt >= cooldown;
        }

        public void NotifyInterstitialClosed() => _lastInterstitialClosedAt = _clock();

        /// <summary>Call only for COMPLETED (rewarded) rewarded ads — cancelled ones don't cool down.</summary>
        public void NotifyRewardedCompleted() => _lastRewardedCompletedAt = _clock();

        private int Resolve(string key, int defaultValue)
        {
            var value = _resolver?.ResolveSeconds(key, defaultValue) ?? defaultValue;
            return value < 0 ? defaultValue : value;
        }
    }
}
