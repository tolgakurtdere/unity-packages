using System;
using Newtonsoft.Json;
using TK.Ads;
using UnityEngine;

namespace TK.Ads.Samples.IntegrationExamples
{
    /// <summary>
    /// Reference <see cref="IAdsPacingResolver"/> that ramps the interstitial interval up as the
    /// player progresses through levels — new players see interstitials less often, veteran players
    /// see the "steady state" interval. Backed by a remote-config JSON string (e.g. from Firebase
    /// Remote Config or Unity Remote Config), parsed with Newtonsoft
    /// (<see cref="JsonConvert"/>), which <c>com.tk.ads</c> declares as a dependency. If you consume
    /// remote config through <c>com.tk.remoteconfig</c>, its <c>GetObject&lt;T&gt;</c> supersedes
    /// hand-rolling this parse — fetch the ladder as a typed object directly instead.
    ///
    /// Expected JSON shape — two parallel arrays defining level bands:
    /// <code>
    /// { "ChangingIntervalLevels": [5, 15], "IntervalsInSecond": [30, 60, 90] }
    /// </code>
    /// reads as: levels 1-5 → 30s, levels 6-15 → 60s, levels 16+ → 90s
    /// (<c>IntervalsInSecond</c> always has exactly one more entry than <c>ChangingIntervalLevels</c>
    /// — the last entry is the steady-state interval once the player is past every threshold).
    ///
    /// Only overrides <see cref="AdsPacingKeys.InterstitialInterval"/>; every other key (notably
    /// <see cref="AdsPacingKeys.CooldownAfterRewarded"/>) passes through untouched by returning
    /// <c>defaultSeconds</c>, so <see cref="InterstitialPacer"/>'s post-rewarded cooldown keeps using
    /// whatever <see cref="AdsSettings.cooldownAfterRewardedSeconds"/> (or another resolver) provides.
    /// </summary>
    public sealed class LevelLadderPacingResolverExample : IAdsPacingResolver
    {
        private class Ladder
        {
            public int[] ChangingIntervalLevels;
            public int[] IntervalsInSecond;
        }

        private readonly Func<int> _currentLevelGetter;
        private Ladder _ladder;

        /// <param name="currentLevelGetter">Returns the player's current level whenever pacing is
        /// checked (e.g. <c>() => progression.CurrentLevel</c>) — read at check time, same as the
        /// remote config JSON itself, so a level-up or a live config change both apply immediately.</param>
        public LevelLadderPacingResolverExample(Func<int> currentLevelGetter)
        {
            _currentLevelGetter = currentLevelGetter ?? throw new ArgumentNullException(nameof(currentLevelGetter));
        }

        /// <summary>
        /// Call this whenever the remote config value (re-)fetches. Safe to call every fetch — a
        /// malformed or empty payload simply clears the ladder, and <see cref="ResolveSeconds"/> then
        /// falls back to <c>defaultSeconds</c> until a valid payload arrives.
        /// </summary>
        public void SetConfigJson(string json)
        {
            _ladder = Parse(json);
        }

        public int ResolveSeconds(string key, int defaultSeconds)
        {
            if (key != AdsPacingKeys.InterstitialInterval) return defaultSeconds; // pass-through for every other key

            if (_ladder == null || _ladder.IntervalsInSecond == null || _ladder.IntervalsInSecond.Length == 0)
                return defaultSeconds;

            var level = _currentLevelGetter();
            var thresholds = _ladder.ChangingIntervalLevels ?? Array.Empty<int>();

            var band = 0;
            while (band < thresholds.Length && level > thresholds[band]) band++;

            return band < _ladder.IntervalsInSecond.Length ? _ladder.IntervalsInSecond[band] : defaultSeconds;
        }

        private static Ladder Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonConvert.DeserializeObject<Ladder>(json);
            }
            catch (Exception exception)
            {
                // Matches the reference implementation's behavior: any parse failure falls back to
                // the resolver's defaultSeconds pass-through rather than risking a bad ladder value.
                Debug.LogWarning($"[LevelLadderPacingResolverExample] Failed to parse remote config JSON: {exception.Message}. Falling back to the default interval.");
                return null;
            }
        }
    }
}
