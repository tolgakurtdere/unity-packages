using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace TK.RemoteConfig.Tests
{
    [TestFixture]
    public sealed class ConfigParamTests
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

        // 1
        [Test]
        public async Task Int_Value_ReadsThroughService()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("k", 9);
            var rc = NewService(backend);

            ConfigParam<int> p = rc.Int("k", 0);

            await rc.InitializeAsync();

            Assert.AreEqual(9, p.Value);
            int x = p; // implicit conversion goes through Value
            Assert.AreEqual(9, x);
        }

        // 2
        [Test]
        public void Param_BeforeInit_ReturnsDefault()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetBool("b", false); // backend has a value, but reads aren't safe yet
            var rc = NewService(backend);

            var p = rc.Bool("b", true);

            Assert.IsFalse(rc.IsSafeToRead);
            Assert.IsTrue(p.Value); // own default wins before init
        }

        // 3
        [Test]
        public async Task Param_MissingKey_ReturnsOwnDefault()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            var p = rc.Int("miss", 5);

            await rc.InitializeAsync();

            Assert.AreEqual(5, p.Value); // key absent from backend → default
        }

        // 4
        [Test]
        public async Task Factory_RegistersDefault()
        {
            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            rc.Int("k", 3);

            await rc.InitializeAsync();

            Assert.AreEqual(3L, backend.ReceivedDefaults["k"]); // int default boxed as long
        }

        // 5
        [Test]
        public async Task Float_RegistersDoubleDefault_AndReads()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetDouble("f", 1.5);
            var rc = NewService(backend);

            var p = rc.Float("f", 0f);

            await rc.InitializeAsync();

            Assert.AreEqual(1.5f, p.Value);
            Assert.AreEqual(1.5f, rc.GetFloat("f", 0f));
            Assert.IsInstanceOf<double>(backend.ReceivedDefaults["f"]); // float default boxed as double
        }

        // 6
        [Test]
        public async Task String_ReadsAndDefaults()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetString("s", "hello");
            var rc = NewService(backend);

            var present = rc.String("s", "fallback");
            var missing = rc.String("miss", "fallback");

            await rc.InitializeAsync();

            Assert.AreEqual("hello", present.Value);
            string implicitValue = present; // implicit conversion
            Assert.AreEqual("hello", implicitValue);
            Assert.AreEqual("fallback", missing.Value); // key absent → own default
        }

        // 7
        [Test]
        public async Task Long_Double_Bool_ReadThrough()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("l", 123456789012L);
            backend.SetDouble("d", 2.5);
            backend.SetBool("b", true);
            var rc = NewService(backend);

            var lp = rc.Long("l", 0L);
            var dp = rc.Double("d", 0d);
            var bp = rc.Bool("b", false);

            await rc.InitializeAsync();

            Assert.AreEqual(123456789012L, lp.Value);
            Assert.AreEqual(2.5, dp.Value);
            Assert.IsTrue(bp.Value);
        }

        // 8
        [Test]
        public async Task PerPlatform_Editor_SelectsAndroid()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("k_a", 11);
            var rc = NewService(backend);

            var p = rc.Int("k_a", 1, "k_i", 2); // editor compiles the #else (android) branch

            Assert.AreEqual("k_a", p.Key);
            Assert.AreEqual(1, p.Default);

            await rc.InitializeAsync();

            Assert.AreEqual(11, p.Value);
        }

        // 9
        [Test]
        public async Task EditorOverride_WinsOverBackendAndDefault()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("k", 9);
            var rc = NewService(backend);

            var p = rc.Int("k", 0);

            await rc.InitializeAsync();

            Assert.AreEqual(9, p.Value); // backend value before override

            p.SetDebugOverride(123);

            Assert.AreEqual(123, p.Value); // override wins on the param path
            Assert.AreEqual(123, rc.GetInt("k", 0)); // and on the raw path
        }

        // 10
        [Test]
        public async Task EditorOverride_Clear_RestoresValue()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("k", 9);
            var rc = NewService(backend);

            var p = rc.Int("k", 0);

            await rc.InitializeAsync();

            p.SetDebugOverride(123);
            Assert.IsTrue(p.HasDebugOverride);
            Assert.AreEqual(123, p.Value);

            p.ClearDebugOverride();

            Assert.IsFalse(p.HasDebugOverride);
            Assert.AreEqual(9, p.Value); // back to the backend value
        }

        // 11
        [Test]
        public async Task EditorOverride_WrongTypeStored_FallsThrough()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetLong("k", 9);
            var rc = NewService(backend);

            await rc.InitializeAsync();

            RemoteConfigDebug.Set("k", "str"); // wrong type for an int read

            Assert.AreEqual(9, rc.GetInt("k", 0)); // mismatch falls through to backend, no throw
        }

        // 12
        [Test]
        public async Task DuplicateKey_DifferentDefault_Warns_LastWins()
        {
            LogAssert.Expect(UnityEngine.LogType.Warning,
                new Regex("Key 'k' registered with a different default"));

            var backend = new FakeRemoteConfigBackend();
            var rc = NewService(backend);

            rc.Int("k", 1);
            rc.Int("k", 2); // second registration warns; last wins

            await rc.InitializeAsync();

            Assert.AreEqual(2L, backend.ReceivedDefaults["k"]);
        }

        // 13 (RIDER, routed from Task 2 review): pins double→float narrowing on BOTH paths.
        [Test]
        public async Task Float_NarrowsFromDouble()
        {
            var backend = new FakeRemoteConfigBackend();
            backend.SetDouble("f", 1.5); // exactly representable as float
            var rc = NewService(backend);

            var p = rc.Float("f", 0f);

            await rc.InitializeAsync();

            Assert.AreEqual(1.5f, rc.GetFloat("f", 0f)); // raw path narrows double→float
            Assert.AreEqual(1.5f, p.Value); // param path narrows too
        }
    }
}
