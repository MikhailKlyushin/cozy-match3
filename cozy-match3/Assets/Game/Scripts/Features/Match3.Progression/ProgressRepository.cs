using System;

namespace Match3.Progression
{
    /// <summary>
    /// Persisted progress: the current level and whether the whole set is finished (GDD §13).
    /// Written on both a win and a loss, and flushed on every write.
    /// </summary>
    public sealed class ProgressRepository
    {
        private readonly IProgressStorage _storage;

        private const string CurrentLevelKey = "progress.currentLevel";
        private const string AllCompletedKey = "progress.allCompleted";
        private const int NoStoredLevel = 0;

        public ProgressRepository(IProgressStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>Stored level id, or 0 when nothing has been played yet.</summary>
        public int CurrentLevel => _storage.GetInt(CurrentLevelKey, NoStoredLevel);

        /// <summary>
        /// Set after winning the level with the largest id. Entering the game with it opens
        /// End of content instead of a level (§8.3).
        /// </summary>
        public bool AllCompleted => _storage.GetInt(AllCompletedKey, 0) != 0;

        public bool HasProgress => _storage.HasKey(CurrentLevelKey);

        public void SetCurrentLevel(int levelId)
        {
            _storage.SetInt(CurrentLevelKey, levelId);
            _storage.Save();
        }

        public void SetAllCompleted(bool completed)
        {
            _storage.SetInt(AllCompletedKey, completed ? 1 : 0);
            _storage.Save();
        }

        /// <summary>"Play again" from End of content: back to the first level (§8.3).</summary>
        public void Restart(int firstLevelId)
        {
            _storage.SetInt(CurrentLevelKey, firstLevelId);
            _storage.SetInt(AllCompletedKey, 0);
            _storage.Save();
        }

        public void Clear()
        {
            _storage.DeleteKey(CurrentLevelKey);
            _storage.DeleteKey(AllCompletedKey);
            _storage.Save();
        }
    }
}
