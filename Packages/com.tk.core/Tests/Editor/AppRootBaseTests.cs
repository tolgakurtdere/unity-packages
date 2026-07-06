using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.App;
using TK.Core.Save;
using UnityEngine;
using AppContext = TK.Core.App.AppContext; // the documented System.AppContext collision (README Gotchas)
using Object = UnityEngine.Object;         // same class of collision, for Object.DestroyImmediate

namespace TK.Core.Tests
{
    [TestFixture]
    public class AppRootBaseTests
    {
        // A root-only game: no level API anywhere — proves the root tier works standalone.
        private sealed class TestRoot : AppRootBase
        {
            public readonly FakeSaveSystem Save = new();
            public int BootCalls;
            public int RegisterServicesCalls;
            public AppContext ContextDuringRegister;
            public GameEndResult? LastGameEnd;

            public AppContext ExposedContext => Context;

            public void InvokeAwake() => Awake();
            public void InvokeStart() => Start();
            public Awaitable RunPublic(Func<Awaitable> operation) => RunTransitionAsync(operation);

            protected override ISaveSystem CreateSaveSystem() => Save;

            protected override void RegisterServices(AppContext context)
            {
                RegisterServicesCalls++;
                ContextDuringRegister = context;
            }

            protected override Awaitable OnBootAsync()
            {
                BootCalls++;
                return TestAwaitables.Completed();
            }

            protected override void OnGameEnded(GameEndResult result) => LastGameEnd = result;
        }

        private GameObject _go;
        private TestRoot _root;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestRoot");
            _root = _go.AddComponent<TestRoot>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void Awake_BuildsContextRelayAndRegistersServices()
        {
            _root.InvokeAwake();

            Assert.IsNotNull(_root.ExposedContext);
            Assert.AreSame(_root.Save, _root.ExposedContext.SaveSystem, "Context must use the save system from CreateSaveSystem().");
            Assert.AreEqual(1, _root.RegisterServicesCalls);
            Assert.AreSame(_root.ExposedContext, _root.ContextDuringRegister);
            Assert.IsTrue(_go.GetComponent<AppLifecycleRelay>(), "Awake must add the lifecycle relay.");
        }

        [Test]
        public void Start_RunsBootPolicy()
        {
            _root.InvokeAwake();

            _root.InvokeStart();

            Assert.AreEqual(1, _root.BootCalls);
        }

        [Test]
        public void RaiseGameEnded_FiresOnGameEndedHook()
        {
            _root.InvokeAwake();

            _root.ExposedContext.RaiseGameEnded(GameEndResult.Win);

            Assert.AreEqual(GameEndResult.Win, _root.LastGameEnd);
        }

        [Test]
        public async Task RunTransitionAsync_DropsReentrantCalls()
        {
            var source = new AwaitableCompletionSource();
            var secondRan = false;

            var first = _root.RunPublic(() => source.Awaitable);
            Assert.IsTrue(_root.IsTransitioning);

            await _root.RunPublic(() =>
            {
                secondRan = true;
                return TestAwaitables.Completed();
            });

            Assert.IsFalse(secondRan, "A verb spammed during a transition must be dropped.");

            source.SetResult();
            await first;
            Assert.IsTrue(_root.CanNavigate);
        }
    }
}
