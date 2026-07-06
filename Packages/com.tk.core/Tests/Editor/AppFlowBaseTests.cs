using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TK.Core.App;
using TK.Core.Save;
using UnityEngine;

namespace TK.Core.Tests
{
    [TestFixture]
    public class AppFlowBaseTests
    {
        // A game subclass written against the 0.1.x AppFlowBase surface — it compiling and
        // behaving identically after the AppRootBase split is the zero-break proof in code.
        // Deliberately does NOT override the new lifecycle hooks: its tests double as proof the
        // default hooks change nothing.
        private class TestFlow : AppFlowBase
        {
            public readonly FakeSaveSystem Save = new();
            public readonly List<string> Calls = new();
            public bool HasResume;
            public int ResumeIndex;
            public bool LevelProgressReadyDuringRegister;

            public LevelProgressService ExposedProgress => LevelProgress;

            public void InvokeAwake() => Awake();
            public void InvokeStart() => Start();
            public void RaiseGameEnd(GameEndResult result) => Context.RaiseGameEnded(result);

            protected override int LevelCount => 5;
            protected override ISaveSystem CreateSaveSystem() => Save;

            protected override void RegisterServices(AppContext context)
            {
                LevelProgressReadyDuringRegister = LevelProgress != null;
            }

            protected override Awaitable ShowMenuAsync()
            {
                Calls.Add("menu");
                return TestAwaitables.Completed();
            }

            protected override Awaitable StartLevelAsync(int levelIndex)
            {
                Calls.Add($"start:{levelIndex}");
                return TestAwaitables.Completed();
            }

            protected override void OnGameEnded(GameEndResult result)
            {
                Calls.Add($"ended:{result}");
            }

            protected override bool TryGetResumeState(out int levelIndex)
            {
                levelIndex = ResumeIndex;
                return HasResume;
            }
        }

        // TestFlow plus the lifecycle hooks — used by the hook tests below.
        private sealed class HookedFlow : TestFlow
        {
            public bool VetoNextStart;

            protected override Awaitable<bool> OnBeforeLevelStartAsync(int levelIndex)
            {
                Calls.Add($"before:{levelIndex}");
                return TestAwaitables.Completed(!VetoNextStart);
            }

            protected override Awaitable OnAfterLevelEndAsync(GameEndResult result)
            {
                Calls.Add($"after:{result}");
                return TestAwaitables.Completed();
            }
        }

        private GameObject _go;
        private TestFlow _flow;
        private GameObject _hookedGo;
        private HookedFlow _hooked;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestFlow");
            _flow = _go.AddComponent<TestFlow>();
            _flow.InvokeAwake();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            _go = null;
            if (_hookedGo) Object.DestroyImmediate(_hookedGo);
            _hookedGo = null;
        }

        private HookedFlow CreateHookedFlow()
        {
            _hookedGo = new GameObject("HookedFlow");
            _hooked = _hookedGo.AddComponent<HookedFlow>();
            _hooked.InvokeAwake();
            return _hooked;
        }

        [Test]
        public void Awake_CreatesLevelProgressBeforeRegisterServices()
        {
            Assert.IsNotNull(_flow.ExposedProgress);
            Assert.IsTrue(_flow.LevelProgressReadyDuringRegister,
                "RegisterServices must still see LevelProgress initialized (0.1.x contract).");
        }

        [Test]
        public void Boot_NoResume_ShowsMenuOnly()
        {
            _flow.InvokeStart();

            CollectionAssert.AreEqual(new[] { "menu" }, _flow.Calls);
        }

        [Test]
        public void GameEnd_FiresOnGameEndedHook()
        {
            _flow.RaiseGameEnd(GameEndResult.Lose);

            CollectionAssert.AreEqual(new[] { "ended:Lose" }, _flow.Calls);
        }

        [Test]
        public async Task Verbs_DriveLevelProgressAndStartLevel()
        {
            await _flow.PlayLevelAsync(3);
            await _flow.ReturnToMenuAsync();
            await _flow.RetryLevelAsync();
            await _flow.PlayNextLevelAsync();

            CollectionAssert.AreEqual(new[] { "start:3", "menu", "start:3", "start:4" }, _flow.Calls);
            Assert.AreEqual(4, _flow.ExposedProgress.CurrentLevelIndex);
        }

        // ── Level lifecycle hooks ────────────────────────────────────

        [Test]
        public async Task PlayLevelAsync_RunsBeforeHookInsideLock_ThenLevel()
        {
            var flow = CreateHookedFlow();

            await flow.PlayLevelAsync(3);

            CollectionAssert.AreEqual(new[] { "before:3", "start:3" }, flow.Calls);
        }

        [Test]
        public async Task PlayNextLevelAsync_AdvancesThenHooksIntoNextLevel()
        {
            var flow = CreateHookedFlow();
            await flow.PlayLevelAsync(0);
            flow.Calls.Clear();

            await flow.PlayNextLevelAsync();

            CollectionAssert.AreEqual(new[] { "before:1", "start:1" }, flow.Calls);
            Assert.AreEqual(1, flow.ExposedProgress.CurrentLevelIndex);
        }

        [Test]
        public void Boot_WithResume_RunsBeforeHookThenResume()
        {
            var flow = CreateHookedFlow();
            flow.HasResume = true;
            flow.ResumeIndex = 2;

            flow.InvokeStart();

            CollectionAssert.AreEqual(new[] { "before:2", "start:2" }, flow.Calls);
        }

        [Test]
        public async Task BeforeHook_Veto_BlocksStartAndReleasesLock()
        {
            var flow = CreateHookedFlow();
            flow.VetoNextStart = true;

            await flow.PlayLevelAsync(2);

            CollectionAssert.AreEqual(new[] { "before:2" }, flow.Calls, "A veto must skip StartLevelAsync.");
            Assert.IsTrue(flow.CanNavigate, "A vetoed start must release the transition lock.");

            flow.VetoNextStart = false;
            flow.Calls.Clear();
            await flow.RetryLevelAsync();
            CollectionAssert.AreEqual(new[] { "before:2", "start:2" }, flow.Calls,
                "After a veto the current index stays where it was set; retry starts it normally.");
        }

        [Test]
        public void GameEnd_FiresSyncHookThenAfterHook()
        {
            var flow = CreateHookedFlow();

            flow.RaiseGameEnd(GameEndResult.Win);

            CollectionAssert.AreEqual(new[] { "ended:Win", "after:Win" }, flow.Calls,
                "OnGameEnded (sync) must fire first, then OnAfterLevelEndAsync.");
        }

        [Test]
        public async Task ReturnToMenuAsync_DoesNotRunLevelHooks()
        {
            var flow = CreateHookedFlow();

            await flow.ReturnToMenuAsync();

            CollectionAssert.AreEqual(new[] { "menu" }, flow.Calls);
        }
    }
}
