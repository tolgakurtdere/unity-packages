using NUnit.Framework;
using UnityEngine;

namespace TK.Ads.Tests
{
    [TestFixture]
    public sealed class AdsSettingsTests
    {
        [Test]
        public void PlatformIds_ResolveToAndroidInEditor()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();
            settings.androidBannerAdUnitId = "android-banner";
            settings.androidInterstitialAdUnitId = "android-interstitial";
            settings.androidRewardedAdUnitId = "android-rewarded";
            settings.iosBannerAdUnitId = "ios-banner";
            settings.iosInterstitialAdUnitId = "ios-interstitial";
            settings.iosRewardedAdUnitId = "ios-rewarded";

            // Editor (like Android) runs the #else branch of SelectByPlatform.
            Assert.AreEqual("android-banner", settings.BannerAdUnitId);
            Assert.AreEqual("android-interstitial", settings.InterstitialAdUnitId);
            Assert.AreEqual("android-rewarded", settings.RewardedAdUnitId);
        }

        [Test]
        public void Defaults_AreSpecCompliant()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();

            Assert.AreEqual(AdsBannerPosition.BottomCenter, settings.bannerPosition);
            Assert.AreEqual(Color.clear, settings.bannerBackgroundColor);
            Assert.AreEqual(60, settings.interstitialMinIntervalSeconds);
            Assert.AreEqual(60, settings.cooldownAfterRewardedSeconds);
        }

        [Test]
        public void EmptyIds_PropertiesReturnEmptyNotCrash()
        {
            var settings = ScriptableObject.CreateInstance<AdsSettings>();

            Assert.DoesNotThrow(() =>
            {
                Assert.IsTrue(string.IsNullOrEmpty(settings.BannerAdUnitId));
                Assert.IsTrue(string.IsNullOrEmpty(settings.InterstitialAdUnitId));
                Assert.IsTrue(string.IsNullOrEmpty(settings.RewardedAdUnitId));
            });
        }
    }
}
