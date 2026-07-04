using System.Collections.Generic;
using NUnit.Framework;

namespace TK.Ads.Tests
{
    [TestFixture]
    public sealed class InterstitialPacerTests
    {
        private sealed class StubResolver : IAdsPacingResolver
        {
            public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
            public readonly List<string> RequestedKeys = new List<string>();

            public int ResolveSeconds(string key, int defaultSeconds)
            {
                RequestedKeys.Add(key);
                return Values.TryGetValue(key, out var value) ? value : defaultSeconds;
            }
        }

        [Test]
        public void FirstShow_AllowedImmediately()
        {
            var clock = new FakeClock { Now = 0f };
            var pacer = new InterstitialPacer(clock.Read, null, 60, 60);

            Assert.IsTrue(pacer.CanShow());
        }

        [Test]
        public void AfterClose_BlockedUntilInterval()
        {
            var clock = new FakeClock { Now = 0f };
            var pacer = new InterstitialPacer(clock.Read, null, 60, 60);

            pacer.NotifyInterstitialClosed();

            clock.Now = 59f;
            Assert.IsFalse(pacer.CanShow());

            clock.Now = 60f;
            Assert.IsTrue(pacer.CanShow());
        }

        [Test]
        public void RewardedCompletion_BlocksForCooldown()
        {
            var clock = new FakeClock { Now = 0f };
            var pacer = new InterstitialPacer(clock.Read, null, 60, 60);

            pacer.NotifyRewardedCompleted();

            clock.Now = 59f;
            Assert.IsFalse(pacer.CanShow(), "cooldown should block even though no interstitial has ever shown");

            clock.Now = 60f;
            Assert.IsTrue(pacer.CanShow());
        }

        [Test]
        public void CancelledRewarded_NoCooldown()
        {
            var clock = new FakeClock { Now = 0f };
            var pacer = new InterstitialPacer(clock.Read, null, 60, 60);

            // Rewarded ad was shown but cancelled -> NotifyRewardedCompleted is never called.
            clock.Now = 5f;

            Assert.IsTrue(pacer.CanShow(), "a cancelled rewarded ad must not cool down interstitials");
        }

        [Test]
        public void Resolver_OverridesDefaults_AtCheckTime()
        {
            var clock = new FakeClock { Now = 0f };
            var resolver = new StubResolver();
            resolver.Values[AdsPacingKeys.InterstitialInterval] = 10;
            var pacer = new InterstitialPacer(clock.Read, resolver, 60, 0);

            pacer.NotifyInterstitialClosed();

            clock.Now = 10f;
            Assert.IsTrue(pacer.CanShow(), "resolver override of 10s should apply instead of the 60s default");

            // Mutate the resolver's value mid-test: this proves resolution happens at CHECK TIME,
            // not once at construction. The same pacer instance must pick up the new value.
            resolver.Values[AdsPacingKeys.InterstitialInterval] = 120;

            clock.Now = 100f;
            Assert.IsFalse(pacer.CanShow(), "the same pacer instance must re-resolve and now block under the new 120s value");
        }

        [Test]
        public void Resolver_NegativeValue_FallsBackToDefault()
        {
            var clock = new FakeClock { Now = 0f };
            var resolver = new StubResolver();
            resolver.Values[AdsPacingKeys.InterstitialInterval] = -1;
            var pacer = new InterstitialPacer(clock.Read, resolver, 60, 0);

            pacer.NotifyInterstitialClosed();

            clock.Now = 59f;
            Assert.IsFalse(pacer.CanShow(), "negative resolver value must fall back to the 60s default, not disable pacing");

            clock.Now = 60f;
            Assert.IsTrue(pacer.CanShow());
        }

        [Test]
        public void BothGates_MostRestrictiveWins()
        {
            var clock = new FakeClock { Now = 0f };
            var pacer = new InterstitialPacer(clock.Read, null, 60, 120);

            pacer.NotifyInterstitialClosed();
            pacer.NotifyRewardedCompleted();

            // Interval (60s) satisfied, but cooldown (120s) is not -> must still be blocked.
            clock.Now = 60f;
            Assert.IsFalse(pacer.CanShow());

            clock.Now = 120f;
            Assert.IsTrue(pacer.CanShow());
        }

        [Test]
        public void ZeroValues_AlwaysAllowed()
        {
            var clock = new FakeClock { Now = 0f };
            var pacer = new InterstitialPacer(clock.Read, null, 0, 0);

            pacer.NotifyInterstitialClosed();
            pacer.NotifyRewardedCompleted();

            Assert.IsTrue(pacer.CanShow());
        }
    }
}
