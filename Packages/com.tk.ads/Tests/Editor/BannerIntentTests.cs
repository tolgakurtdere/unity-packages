using NUnit.Framework;

namespace TK.Ads.Tests
{
    [TestFixture]
    public sealed class BannerIntentTests
    {
        [Test]
        public void ShowBeforeLoad_NoImmediateShow_ThenAutoShowOnLoad()
        {
            var intent = new BannerIntent();

            var showedImmediately = intent.RequestShow();
            Assert.IsFalse(showedImmediately, "banner has not loaded yet, so it cannot show now");

            var showedOnLoad = intent.OnLoaded();
            Assert.IsTrue(showedOnLoad, "intent survived load latency, so it should auto-show once loaded");
            Assert.IsTrue(intent.IsVisible);
        }

        [Test]
        public void ShowAfterLoad_ImmediateShow()
        {
            var intent = new BannerIntent();

            intent.OnLoaded();
            var showedImmediately = intent.RequestShow();

            Assert.IsTrue(showedImmediately);
            Assert.IsTrue(intent.IsVisible);
        }

        [Test]
        public void Hide_ClearsVisibilityAndIntent()
        {
            var intent = new BannerIntent();
            intent.OnLoaded();
            intent.RequestShow();

            intent.RequestHide();

            Assert.IsFalse(intent.IsVisible);
            Assert.IsFalse(intent.WantsVisible);

            // A subsequent load event must not auto-show, since intent was cleared by the hide.
            var showedOnLoad = intent.OnLoaded();
            Assert.IsFalse(showedOnLoad);
        }

        [Test]
        public void RepeatedLoadedEvents_NoDoubleShow()
        {
            var intent = new BannerIntent();
            intent.RequestShow();

            var firstLoad = intent.OnLoaded();
            Assert.IsTrue(firstLoad);

            // Auto-refresh fires OnLoaded again while already visible -> must not report a second show.
            var secondLoad = intent.OnLoaded();
            Assert.IsFalse(secondLoad);
            Assert.IsTrue(intent.IsVisible);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var intent = new BannerIntent();
            intent.OnLoaded();
            intent.RequestShow();

            intent.Reset();

            Assert.IsFalse(intent.WantsVisible);
            Assert.IsFalse(intent.IsLoaded);
            Assert.IsFalse(intent.IsVisible);
        }

        [Test]
        public void RequestShow_WhileVisible_NoDuplicate()
        {
            var intent = new BannerIntent();
            intent.OnLoaded();
            intent.RequestShow();

            var secondRequest = intent.RequestShow();

            Assert.IsFalse(secondRequest, "already visible -> gateway should not be told to show again");
            Assert.IsTrue(intent.IsVisible);
        }
    }
}
