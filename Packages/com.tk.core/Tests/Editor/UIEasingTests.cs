using NUnit.Framework;
using TK.Core.UI;

namespace TK.Core.Tests
{
    [TestFixture]
    public class UIEasingTests
    {
        [Test]
        public void OutBack_StartsAtZeroEndsAtOne()
        {
            Assert.AreEqual(0f, UIEasing.OutBack(0f), 1e-4f);
            Assert.AreEqual(1f, UIEasing.OutBack(1f), 1e-4f);
        }

        [Test]
        public void InBack_StartsAtZeroEndsAtOne()
        {
            Assert.AreEqual(0f, UIEasing.InBack(0f), 1e-4f);
            Assert.AreEqual(1f, UIEasing.InBack(1f), 1e-4f);
        }

        [Test]
        public void OutBack_OvershootsPastOne()
        {
            Assert.Greater(UIEasing.OutBack(0.7f), 1f);
        }

        [Test]
        public void InBack_DipsBelowZero()
        {
            Assert.Less(UIEasing.InBack(0.3f), 0f);
        }
    }
}
