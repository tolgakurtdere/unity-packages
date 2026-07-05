using System;
using NUnit.Framework;
using TK.Localization;
using UnityEngine;

namespace TK.Localization.Tests
{
    public sealed class PlayerPrefsLocalePersistenceTests
    {
        private const string TestKey = "TK.Localization.Tests.LocaleCode";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(TestKey);
        }

        [Test]
        public void Save_Then_Load_RoundTrips()
        {
            var persistence = new PlayerPrefsLocalePersistence(TestKey);

            persistence.Save("tr");

            Assert.AreEqual("tr", persistence.Load());
        }

        [Test]
        public void Load_WhenUnset_ReturnsNull()
        {
            // Ensure the key is absent before loading.
            PlayerPrefs.DeleteKey(TestKey);
            var persistence = new PlayerPrefsLocalePersistence(TestKey);

            Assert.IsNull(persistence.Load());
        }

        [Test]
        public void Ctor_EmptyKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PlayerPrefsLocalePersistence(""));
            Assert.Throws<ArgumentException>(() => new PlayerPrefsLocalePersistence(null));
        }
    }
}
