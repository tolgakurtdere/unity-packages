using NUnit.Framework;

namespace TK.Ads.Tests
{
    [TestFixture]
    public sealed class LoadRetryPolicyTests
    {
        [Test]
        public void Backoff_Progression_2_4_8_UpTo64Cap()
        {
            var policy = new LoadRetryPolicy();
            float[] expected = { 2f, 4f, 8f, 16f, 32f, 64f, 64f };

            foreach (var expectedDelay in expected)
            {
                var delay = policy.OnFailed();
                Assert.AreEqual(expectedDelay, delay);
            }
        }

        [Test]
        public void Success_ResetsCounter()
        {
            var policy = new LoadRetryPolicy();

            policy.OnFailed();
            policy.OnFailed();
            policy.OnFailed();

            policy.OnSucceeded();

            Assert.AreEqual(0, policy.FailedAttempts);

            var nextDelay = policy.OnFailed();
            Assert.AreEqual(2f, nextDelay);
        }

        [Test]
        public void AttemptsCounter_Tracks()
        {
            var policy = new LoadRetryPolicy();

            Assert.AreEqual(0, policy.FailedAttempts);

            policy.OnFailed();
            Assert.AreEqual(1, policy.FailedAttempts);

            policy.OnFailed();
            Assert.AreEqual(2, policy.FailedAttempts);

            policy.OnFailed();
            Assert.AreEqual(3, policy.FailedAttempts);
        }
    }
}
