using TK.Core.Save;
using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Tracks level progression by index: MaxUnlockedIndex (persisted) vs CurrentLevelIndex
    /// (transient). Level CONTENT loading is the game's job — this service only owns indices,
    /// so it works with any level source (JSON, scenes, prefabs, remote).
    /// Indices wrap around to 0 after the last level.
    /// </summary>
    public class LevelProgressService
    {
        private const string PROGRESS_KEY = "level_progress";

        private readonly ISaveSystem _saveSystem;

        public int LevelCount { get; }

        /// <summary>The level currently being played. Transient — not persisted.</summary>
        public int CurrentLevelIndex { get; private set; }

        /// <summary>The highest unlocked level (progression frontier). Persisted.</summary>
        public int MaxUnlockedIndex { get; private set; }

        public LevelProgressService(ISaveSystem saveSystem, int levelCount)
        {
            if (saveSystem == null) throw new System.ArgumentNullException(nameof(saveSystem));
            if (levelCount < 1) throw new System.ArgumentOutOfRangeException(nameof(levelCount), "At least one level is required.");

            _saveSystem = saveSystem;
            LevelCount = levelCount;
            MaxUnlockedIndex = LoadProgress();
            CurrentLevelIndex = MaxUnlockedIndex;
        }

        /// <summary>Sets the active level index (e.g. from level select) without affecting progression.</summary>
        public void SetLevelIndex(int index)
        {
            CurrentLevelIndex = Mathf.Clamp(index, 0, LevelCount - 1);
        }

        /// <summary>
        /// Advances to the next level (wrapping after the last). If the new index passes
        /// MaxUnlockedIndex, the frontier is updated and persisted. Returns the new current index.
        /// </summary>
        public int AdvanceToNextLevel()
        {
            CurrentLevelIndex++;
            if (CurrentLevelIndex >= LevelCount)
            {
                CurrentLevelIndex = 0;
            }

            if (CurrentLevelIndex > MaxUnlockedIndex)
            {
                MaxUnlockedIndex = CurrentLevelIndex;
                SaveProgress();
            }

            return CurrentLevelIndex;
        }

        private int LoadProgress()
        {
            if (!_saveSystem.HasKey(PROGRESS_KEY)) return 0;

            var index = _saveSystem.Load<int>(PROGRESS_KEY, 0);
            return Mathf.Clamp(index, 0, LevelCount - 1);
        }

        private void SaveProgress()
        {
            _saveSystem.Save(PROGRESS_KEY, MaxUnlockedIndex);
        }
    }
}
