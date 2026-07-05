using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TK.RemoteConfig.Tests
{
    [TestFixture]
    public sealed class RemoteConfigParsingTests
    {
        private static RemoteConfigService NewService(
            FakeRemoteConfigBackend backend, RemoteConfigOptions options = null)
            => new RemoteConfigService(backend, options);

        [TearDown]
        public void TearDown()
        {
            // Session overrides are process-static — clear between tests so they never leak.
            RemoteConfigDebug.ClearAll();
        }

        // Plain classes for the GetObject tests. Newtonsoft needs NO [Serializable] — that is the
        // whole point of GetObject over JsonUtility (which requires it and cannot do dictionaries).

        private sealed class Payload
        {
            public int N;
            public string S;
        }

        private sealed class Nested
        {
            public string Name;
            public int Level;
        }

        private sealed class RichConfig
        {
            public Dictionary<string, int> Amounts;
            public Nested Boss;
        }

        // 1
        [Test]
        public void ParseIntList_Basic()
        {
            var result = RemoteConfigParsing.ParseIntList("4,12,20");
            CollectionAssert.AreEqual(new[] { 4, 12, 20 }, result);
        }

        // 2
        [Test]
        public void ParseIntList_TrimsAndSkipsInvalid()
        {
            var result = RemoteConfigParsing.ParseIntList(" 4 , x, 12 ");
            CollectionAssert.AreEqual(new[] { 4, 12 }, result); // trims spaces, skips "x"
        }

        // 3
        [Test]
        public void ParseIntList_EmptyOrNull()
        {
            var fromEmpty = RemoteConfigParsing.ParseIntList("");
            var fromNull = RemoteConfigParsing.ParseIntList((string)null); // cast picks the string overload, not the ConfigParam extension

            Assert.IsNotNull(fromEmpty);
            Assert.IsEmpty(fromEmpty);
            Assert.IsNotNull(fromNull); // empty list, never null
            Assert.IsEmpty(fromNull);
        }

        // 4
        [Test]
        public void ParseStringList_Basic()
        {
            var result = RemoteConfigParsing.ParseStringList("a,b,c");
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result);
        }

        // 5
        [Test]
        public void ParseStringList_TrimsAndDropsEmpty()
        {
            var result = RemoteConfigParsing.ParseStringList("a, ,b,");
            CollectionAssert.AreEqual(new[] { "a", "b" }, result); // whitespace-only and trailing empty dropped
        }

        // 6
        [Test]
        public async Task ParseIntList_FromParam()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetString("k", "1,2");
            var rc = NewService(backend);

            var param = rc.String("k", "");

            await rc.InitializeAsync();

            var result = param.ParseIntList(); // extension over ConfigParam<string>.Value
            CollectionAssert.AreEqual(new[] { 1, 2 }, result);
        }

        // 7
        [Test]
        public async Task GetObject_ParsesJson()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetString("k", "{\"N\":5,\"S\":\"x\"}");
            var rc = NewService(backend);

            await rc.InitializeAsync();

            var payload = rc.GetObject<Payload>("k", null);

            Assert.IsNotNull(payload);
            Assert.AreEqual(5, payload.N);
            Assert.AreEqual("x", payload.S);
        }

        // 8
        [Test]
        public async Task GetObject_MissingOrInvalid_ReturnsDefault()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            await rc.InitializeAsync();

            var fallback = new Payload { N = -1, S = "def" };

            // Missing key → default, no warning (empty/missing is not a parse failure).
            Assert.AreSame(fallback, rc.GetObject("missing", fallback));

            // Invalid JSON → default, with a logged warning.
            backend.SetString("k", "{bad json");
            LogAssert.Expect(LogType.Warning, new Regex(@"\[RemoteConfig\] GetObject<Payload> failed for key 'k'"));

            Assert.AreSame(fallback, rc.GetObject("k", fallback));
        }

        // 9
        [Test]
        public async Task GetObject_NewtonsoftRichTypes()
        {
            // A Dictionary<string,int> plus a nested object — neither of which JsonUtility can
            // deserialize. Proves the Newtonsoft path handles the domain-config JSON shape.
            var backend = new FakeRemoteConfigBackend();
            backend.SetString("economy",
                "{\"Amounts\":{\"gold\":100,\"gems\":5},\"Boss\":{\"Name\":\"Dragon\",\"Level\":9}}");
            var rc = NewService(backend);

            await rc.InitializeAsync();

            var config = rc.GetObject<RichConfig>("economy", null);

            Assert.IsNotNull(config);

            // Dictionary deserialized with populated entries.
            Assert.IsNotNull(config.Amounts);
            Assert.AreEqual(2, config.Amounts.Count);
            Assert.AreEqual(100, config.Amounts["gold"]);
            Assert.AreEqual(5, config.Amounts["gems"]);

            // Nested object deserialized.
            Assert.IsNotNull(config.Boss);
            Assert.AreEqual("Dragon", config.Boss.Name);
            Assert.AreEqual(9, config.Boss.Level);
        }
    }
}
