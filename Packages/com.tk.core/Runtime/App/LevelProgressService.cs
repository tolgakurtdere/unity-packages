using TK.Core.Save;
using UnityEngine;

namespace TK.Core.App
{
    /// <summary>
    /// Decides the next current index after completing <paramref name="completedIndex"/>.
    /// See <see cref="LevelAdvancePolicies"/> for the built-ins; results are clamped to the
    /// valid index range by the service.
    /// </summary>
    public delegate int LevelAdvancePolicy(int completedIndex, int levelCount);

    /// <summary>Built-in advance policies for <see cref="LevelProgressService"/>.</summary>
    public static class LevelAdvancePolicies
    {
        /// <summary>Next index, wrapping to 0 after the last level (the default).</summary>
        public static int Wrap(int completedIndex, int levelCount)
        {
            return completedIndex + 1 >= levelCount ? 0 : completedIndex + 1;
        }

        /// <summary>Next index, staying on the last level once it is reached.</summary>
        public static int Clamp(int completedIndex, int levelCount)
        {
            return Mathf.Min(completedIndex + 1, levelCount - 1);
        }
    }

    /// <summary>
    /// Tracks level progression by index: MaxUnlockedIndex (persisted) vs CurrentLevelIndex
    /// (transient). Level CONTENT loading is the game's job — this service only owns indices,
    /// so it works with any level source (JSON, scenes, prefabs, remote).
    /// Advancing wraps to 0 after the last level by default; pass a LevelAdvancePolicy (e.g.
    /// LevelAdvancePolicies.Clamp) to change that. Multiple instances can track separate
    /// progressions (Main + Master + Category tracks, ...) via distinct saveKey values.
    /// </summary>
    public class LevelProgressService
    {
        private const string PROGRESS_KEY = "level_progress";

        private readonly ISaveSystem _saveSystem;
        private readonly string _saveKey;
        private readonly LevelAdvancePolicy _advancePolicy;

        public int LevelCount { get; }

        /// <summary>The level currently being played. Transient — not persisted.</summary>
        public int CurrentLevelIndex { get; private set; }

        /// <summary>The highest unlocked level (progression frontier). Persisted.</summary>
        public int MaxUnlockedIndex { get; private set; }

        public LevelProgressService(ISaveSystem saveSystem, int levelCount,
            string saveKey = PROGRESS_KEY, LevelAdvancePolicy advancePolicy = null)
        {
            if (saveSystem == null) throw new System.ArgumentNullException(nameof(saveSystem));
            if (levelCount < 1) throw new System.ArgumentOutOfRangeException(nameof(levelCount), "At least one level is required.");
            if (string.IsNullOrWhiteSpace(saveKey)) throw new System.ArgumentException("A non-empty save key is required.", nameof(saveKey));

            _saveSystem = saveSystem;
            _saveKey = saveKey;
            _advancePolicy = advancePolicy ?? LevelAdvancePolicies.Wrap;
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
        /// Advances to the next level per the advance policy (default: wrap after the last). If
        /// the new index passes MaxUnlockedIndex, the frontier is updated and persisted. Returns
        /// the new current index.
        /// </summary>
        public int AdvanceToNextLevel()
        {
            CurrentLevelIndex = Mathf.Clamp(_advancePolicy(CurrentLevelIndex, LevelCount), 0, LevelCount - 1);

            if (CurrentLevelIndex > MaxUnlockedIndex)
            {
                MaxUnlockedIndex = CurrentLevelIndex;
                SaveProgress();
            }

            return CurrentLevelIndex;
        }

        private int LoadProgress()
        {
            if (!_saveSystem.HasKey(_saveKey)) return 0;

            var index = _saveSystem.Load<int>(_saveKey, 0);
            return Mathf.Clamp(index, 0, LevelCount - 1);
        }

        private void SaveProgress()
        {
            _saveSystem.Save(_saveKey, MaxUnlockedIndex);
        }
    }
}
