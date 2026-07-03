using NUnit.Framework;
using TK.Core.Save;
using UnityEngine;

namespace TK.Core.Tests
{
    [TestFixture]
    public class PlayerPrefsJsonSaveSystemTests
    {
        private const string KeyPrefix = "tk_core_tests_";
        private PlayerPrefsJsonSaveSystem _save;

        [SetUp]
        public void SetUp() => _save = new PlayerPrefsJsonSaveSystem();

        [TearDown]
        public void TearDown()
        {
            _save.Delete(KeyPrefix + "obj");
            _save.Delete(KeyPrefix + "int");
            _save.Delete(KeyPrefix + "corrupt");
        }

        private class Payload
        {
            public int Number;
            public string Text;
        }

        [Test]
        public void SaveLoad_RoundTripsComplexType()
        {
            _save.Save(KeyPrefix + "obj", new Payload { Number = 42, Text = "hello" });

            var loaded = _save.Load<Payload>(KeyPrefix + "obj");

            Assert.IsNotNull(loaded);
            Assert.AreEqual(42, loaded.Number);
            Assert.AreEqual("hello", loaded.Text);
        }

        [Test]
        public void Load_MissingKey_ReturnsDefault()
        {
            Assert.AreEqual(7, _save.Load(KeyPrefix + "missing", 7));
        }

        [Test]
        public void Load_CorruptJson_ReturnsDefaultAndWarns()
        {
            PlayerPrefs.SetString(KeyPrefix + "corrupt", "{not valid json");
            var loaded = _save.Load<Payload>(KeyPrefix + "corrupt");
            Assert.IsNull(loaded);
        }

        [Test]
        public void HasKey_And_Delete_Work()
        {
            _save.Save(KeyPrefix + "int", 1);
            Assert.IsTrue(_save.HasKey(KeyPrefix + "int"));

            _save.Delete(KeyPrefix + "int");
            Assert.IsFalse(_save.HasKey(KeyPrefix + "int"));
        }
    }
}
