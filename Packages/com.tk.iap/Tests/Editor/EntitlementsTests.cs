using NUnit.Framework;

namespace TK.IAP.Tests
{
    [TestFixture]
    public sealed class EntitlementsTests
    {
        [Test]
        public void Grant_RaisesChangedOnce_PersistsAcrossInstances()
        {
            var save = new FakeSaveSystem();
            var entitlements = new Entitlements(save);
            var changedCount = 0;
            string changedKey = null;
            entitlements.Changed += key =>
            {
                changedCount++;
                changedKey = key;
            };

            entitlements.Grant("remove_ads");

            Assert.AreEqual(1, changedCount);
            Assert.AreEqual("remove_ads", changedKey);
            Assert.IsTrue(entitlements.Has("remove_ads"));

            var reloaded = new Entitlements(save);
            Assert.IsTrue(reloaded.Has("remove_ads"));
        }

        [Test]
        public void Grant_SameKeyTwice_RaisesChangedOnce()
        {
            var save = new FakeSaveSystem();
            var entitlements = new Entitlements(save);
            var changedCount = 0;
            entitlements.Changed += _ => changedCount++;

            entitlements.Grant("vip");
            entitlements.Grant("vip");

            Assert.AreEqual(1, changedCount);
            Assert.IsTrue(entitlements.Has("vip"));
        }

        [Test]
        public void Subscribe_AlreadyGranted_InvokesImmediately()
        {
            var save = new FakeSaveSystem();
            var entitlements = new Entitlements(save);
            entitlements.Grant("vip");

            var invokeCount = 0;
            entitlements.Subscribe("vip", () => invokeCount++);

            Assert.AreEqual(1, invokeCount);
        }

        [Test]
        public void Subscribe_NotGranted_InvokesOnGrant_Once()
        {
            var save = new FakeSaveSystem();
            var entitlements = new Entitlements(save);

            var invokeCount = 0;
            entitlements.Subscribe("vip", () => invokeCount++);

            Assert.AreEqual(0, invokeCount);

            entitlements.Grant("vip");
            Assert.AreEqual(1, invokeCount);

            entitlements.Grant("vip");
            Assert.AreEqual(1, invokeCount);
        }

        [Test]
        public void Unsubscribe_RemovesCallback()
        {
            var save = new FakeSaveSystem();
            var entitlements = new Entitlements(save);
            var invokeCount = 0;
            void OnGranted() => invokeCount++;

            entitlements.Subscribe("vip", OnGranted);
            entitlements.Unsubscribe("vip", OnGranted);
            entitlements.Grant("vip");

            Assert.AreEqual(0, invokeCount);
        }
    }
}
