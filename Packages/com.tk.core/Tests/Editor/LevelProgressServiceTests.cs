using System;
using NUnit.Framework;
using TK.Core.App;

namespace TK.Core.Tests
{
    [TestFixture]
    public class LevelProgressServiceTests
    {
        private FakeSaveSystem _saveSystem;

        [SetUp]
        public void SetUp()
        {
            _saveSystem = new FakeSaveSystem();
        }

        // ── Constructor ──────────────────────────────────────────────

        [Test]
        public void Constructor_NoSavedProgress_MaxUnlockedStartsAtZero()
        {
            var svc = CreateService(levelCount: 5);

            Assert.AreEqual(0, svc.MaxUnlockedIndex);
        }

        [Test]
        public void Constructor_WithSavedProgress_RestoresMaxUnlocked()
        {
            _saveSystem.Save("level_progress", 3);

            var svc = CreateService(levelCount: 5);

            Assert.AreEqual(3, svc.MaxUnlockedIndex);
        }

        [Test]
        public void Constructor_WithSavedProgress_CurrentStartsAtFrontier()
        {
            _saveSystem.Save("level_progress", 2);

            var svc = CreateService(levelCount: 5);

            Assert.AreEqual(2, svc.CurrentLevelIndex);
            Assert.AreEqual(2, svc.MaxUnlockedIndex);
        }

        [Test]
        public void Constructor_ZeroLevels_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = CreateService(levelCount: 0));
        }

        [Test]
        public void Constructor_SavedProgressBeyondLevelCount_IsClamped()
        {
            _saveSystem.Save("level_progress", 99);

            var svc = CreateService(levelCount: 3);

            Assert.AreEqual(2, svc.MaxUnlockedIndex);
        }

        // ── SetLevelIndex ────────────────────────────────────────────

        [Test]
        public void SetLevelIndex_DoesNotPersist()
        {
            _saveSystem.Save("level_progress", 2);
            var svc = CreateService(levelCount: 5);

            svc.SetLevelIndex(0);

            Assert.AreEqual(2, _saveSystem.Load<int>("level_progress", -1), "Persisted value must remain at 2 (MaxUnlockedIndex).");
        }

        [Test]
        public void SetLevelIndex_DoesNotLowerMaxUnlockedIndex()
        {
            _saveSystem.Save("level_progress", 3);
            var svc = CreateService(levelCount: 5);

            svc.SetLevelIndex(1);

            Assert.AreEqual(1, svc.CurrentLevelIndex);
            Assert.AreEqual(3, svc.MaxUnlockedIndex);
        }

        [Test]
        public void SetLevelIndex_ClampsToValidRange()
        {
            var svc = CreateService(levelCount: 3);

            svc.SetLevelIndex(99);
            Assert.AreEqual(2, svc.CurrentLevelIndex);

            svc.SetLevelIndex(-5);
            Assert.AreEqual(0, svc.CurrentLevelIndex);
        }

        // ── AdvanceToNextLevel ───────────────────────────────────────

        [Test]
        public void AdvanceToNextLevel_AtFrontier_IncreasesMaxUnlocked()
        {
            _saveSystem.Save("level_progress", 2);
            var svc = CreateService(levelCount: 5);

            svc.SetLevelIndex(2); // simulate playing the frontier level

            Assert.AreEqual(3, svc.AdvanceToNextLevel());
            Assert.AreEqual(3, svc.CurrentLevelIndex);
            Assert.AreEqual(3, svc.MaxUnlockedIndex);
            Assert.AreEqual(3, _saveSystem.Load<int>("level_progress", -1));
        }

        [Test]
        public void AdvanceToNextLevel_BelowFrontier_DoesNotChangeMaxUnlocked()
        {
            _saveSystem.Save("level_progress", 3);
            var svc = CreateService(levelCount: 5);

            svc.SetLevelIndex(1); // replaying level 1

            Assert.AreEqual(2, svc.AdvanceToNextLevel());
            Assert.AreEqual(2, svc.CurrentLevelIndex);
            Assert.AreEqual(3, svc.MaxUnlockedIndex, "MaxUnlockedIndex must not decrease when advancing from a replayed level.");
            Assert.AreEqual(3, _saveSystem.Load<int>("level_progress", -1));
        }

        [Test]
        public void AdvanceToNextLevel_WrapAround_DoesNotLowerMax()
        {
            _saveSystem.Save("level_progress", 2);
            var svc = CreateService(levelCount: 3); // indices 0,1,2

            svc.SetLevelIndex(2); // simulate playing the frontier level

            Assert.AreEqual(0, svc.AdvanceToNextLevel());
            Assert.AreEqual(0, svc.CurrentLevelIndex);
            Assert.AreEqual(2, svc.MaxUnlockedIndex, "MaxUnlockedIndex must not decrease on wrap-around.");
        }

        // ── Full replay scenario ─────────────────────────────────────

        [Test]
        public void FullReplayScenario_ProgressionSurvivesReplay()
        {
            var svc = CreateService(levelCount: 5);

            // Complete level 0 → advance to 1
            svc.SetLevelIndex(0);
            svc.AdvanceToNextLevel();
            Assert.AreEqual(1, svc.MaxUnlockedIndex);

            // Complete level 1 → advance to 2
            svc.AdvanceToNextLevel();
            Assert.AreEqual(2, svc.MaxUnlockedIndex);

            // Replay level 0 via level select (SetLevelIndex)
            svc.SetLevelIndex(0);
            Assert.AreEqual(0, svc.CurrentLevelIndex);
            Assert.AreEqual(2, svc.MaxUnlockedIndex, "Replay must not affect progression.");

            // Exit → Play button targets frontier directly
            Assert.AreEqual(2, svc.MaxUnlockedIndex);
        }

        // ── Helpers ──────────────────────────────────────────────────

        private LevelProgressService CreateService(int levelCount)
        {
            return new LevelProgressService(_saveSystem, levelCount);
        }
    }
}
