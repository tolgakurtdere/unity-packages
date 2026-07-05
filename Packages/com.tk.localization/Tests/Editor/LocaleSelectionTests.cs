using System.Collections.Generic;
using NUnit.Framework;
using TK.Localization;

namespace TK.Localization.Tests
{
    public sealed class LocaleSelectionTests
    {
        private static IReadOnlyList<string> Codes(params string[] codes) => new List<string>(codes);

        [Test]
        public void Choose_SavedAvailable_ReturnsSaved()
        {
            var result = LocaleSelection.Choose("tr", "de", Codes("en", "de", "tr"));
            Assert.AreEqual("tr", result);
        }

        [Test]
        public void Choose_SavedUnavailable_DeviceAvailable_ReturnsDevice()
        {
            var result = LocaleSelection.Choose("ja", "de", Codes("en", "de", "tr"));
            Assert.AreEqual("de", result);
        }

        [Test]
        public void Choose_NeitherAvailable_ReturnsFirst()
        {
            var result = LocaleSelection.Choose("ja", "ko", Codes("en", "de", "tr"));
            Assert.AreEqual("en", result);
        }

        [Test]
        public void Choose_EmptyAvailable_ReturnsNull()
        {
            Assert.IsNull(LocaleSelection.Choose("tr", "de", Codes()));
            Assert.IsNull(LocaleSelection.Choose("tr", "de", null));
        }

        [Test]
        public void Choose_NullSaved_FallsToDeviceThenFirst()
        {
            // Null saved, device available -> device.
            Assert.AreEqual("de", LocaleSelection.Choose(null, "de", Codes("en", "de", "tr")));
            // Null saved, device unavailable -> first.
            Assert.AreEqual("en", LocaleSelection.Choose(null, "ko", Codes("en", "de", "tr")));
        }
    }
}
