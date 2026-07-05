using NUnit.Framework;
using TK.Analytics;

namespace TK.Analytics.Tests
{
    [TestFixture]
    public sealed class AnalyticsParamTests
    {
        // 1
        [Test]
        public void String_Factory_RoundTrips()
        {
            var p = AnalyticsParam.String("k", "v");

            Assert.AreEqual(AnalyticsParamType.String, p.Type);
            Assert.AreEqual("k", p.Key);
            Assert.AreEqual("v", p.StringValue);
        }

        // 2
        [Test]
        public void Long_Factory_RoundTrips()
        {
            var p = AnalyticsParam.Long("n", 5);

            Assert.AreEqual(AnalyticsParamType.Long, p.Type);
            Assert.AreEqual(5, p.LongValue);
        }

        // 3
        [Test]
        public void Double_Factory_RoundTrips()
        {
            var p = AnalyticsParam.Double("d", 1.5);

            Assert.AreEqual(AnalyticsParamType.Double, p.Type);
            Assert.AreEqual(1.5, p.DoubleValue, 1e-9);
        }

        // 4
        [Test]
        public void Bool_Factory_RoundTrips()
        {
            var p = AnalyticsParam.Bool("b", true);

            Assert.AreEqual(AnalyticsParamType.Bool, p.Type);
            Assert.IsTrue(p.BoolValue);
        }

        // 5
        [Test]
        public void ToString_FormatsKeyEqualsValue()
        {
            Assert.AreEqual("k=v", AnalyticsParam.String("k", "v").ToString());
            Assert.AreEqual("n=3", AnalyticsParam.Long("n", 3).ToString());
        }
    }
}
