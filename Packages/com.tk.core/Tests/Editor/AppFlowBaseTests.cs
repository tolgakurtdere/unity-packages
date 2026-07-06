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
        private sealed class TestFlow : AppFlowBase
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

        private GameObject _go;
        private TestFlow _flow;

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
    }
}
