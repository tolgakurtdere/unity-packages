using NUnit.Framework;
using TK.Localization;

namespace TK.Localization.Tests
{
    public sealed class RtlTextTests
    {
        // Arabic "marhaba" (hello). Isolated code points on input; shaping + reversal must change it.
        private const string Arabic = "مرحبا";

        [Test]
        public void IsRtl_ArabicString_True()
        {
            Assert.IsTrue(RtlText.IsRtl(Arabic));
        }

        [Test]
        public void IsRtl_EnglishString_False()
        {
            Assert.IsFalse(RtlText.IsRtl("Hello"));
        }

        [Test]
        public void IsRtl_EmptyOrNull_False()
        {
            Assert.IsFalse(RtlText.IsRtl(string.Empty));
            Assert.IsFalse(RtlText.IsRtl(null));
        }

        [Test]
        public void Fix_LtrInput_Unchanged()
        {
            Assert.AreEqual("Hello 123", RtlText.Fix("Hello 123"));
        }

        [Test]
        public void Fix_Empty_ReturnsInput()
        {
            Assert.AreEqual(string.Empty, RtlText.Fix(string.Empty));
            Assert.IsNull(RtlText.Fix(null));
        }

        [Test]
        public void Fix_ArabicInput_NonEmpty_AndTransformed()
        {
            string result = RtlText.Fix(Arabic);

            Assert.IsFalse(string.IsNullOrEmpty(result), "Shaped Arabic output should be non-empty.");
            Assert.AreNotEqual(Arabic, result, "Shaping + reversal should change the raw Arabic input.");
        }

        [Test]
        public void Fix_PreservesRichTextTag()
        {
            string result = RtlText.Fix("<b>" + Arabic + "</b>");

            StringAssert.Contains("<b>", result);
            StringAssert.Contains("</b>", result);
        }

        [Test]
        public void Fix_IsDeterministic()
        {
            // Guards the shared static buffer against cross-call contamination:
            // two calls on the same input must produce identical output.
            string first = RtlText.Fix(Arabic);
            string second = RtlText.Fix(Arabic);

            Assert.AreEqual(first, second);
        }
    }
}
