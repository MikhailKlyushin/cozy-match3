using System;
using Match3.Core;
using Match3.Levels;
using Match3.Levels.Authoring;
using R3;

namespace Match3.Progression
{
    /// <summary>What the player is looking at between attempts (§11.1). There is no level menu.</summary>
    public enum LevelFlowScreen : byte
    {
        None = 0,
        Level = 1,
        EndOfContent = 2
    }

    /// <summary>
    /// The §8.3 level flow: entry point, win advances, loss replays with a new seed, and a win on
    /// the largest catalog id opens End of content. The last level always comes from
    /// <see cref="LevelCatalog.LastLevelId"/>, never from a literal.
    /// </summary>
    public sealed class LevelFlowRule : IDisposable
    {
        private readonly ProgressRepository _progress;
        private readonly LevelCatalog _catalog;
        private readonly LevelSessionFactory _sessions;
        private readonly IMatch3Logger _logger;

        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        private readonly ReactiveProperty<LevelFlowScreen> _screen =
            new ReactiveProperty<LevelFlowScreen>(LevelFlowScreen.None);

        private const int FirstAttempt = 1;

        private int _currentLevelId;
        private int _attempt;
        private bool _hasResult;
        private LevelResult _lastResult;

        public LevelFlowRule(
            ProgressRepository progress,
            LevelCatalog catalog,
            LevelSessionFactory sessions,
            IMatch3Logger logger)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _catalog = catalog;
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _disposables.Add(_sessions.Finished.Subscribe(OnLevelFinished));
        }

        public ReadOnlyReactiveProperty<LevelFlowScreen> Screen => _screen;

        public int CurrentLevelId => _currentLevelId;

        /// <summary>Shown on the End of content screen (§11.1); derived from the catalog data (§8.3).</summary>
        public int LastLevelId => _catalog.LastLevelId;

        public int Attempt => _attempt;

        /// <summary>
        /// Entering the game (§8.3): a completed set opens End of content, never a level. The
        /// stored id is used only while the catalog still holds it, otherwise the set restarts.
        /// </summary>
        public void Enter()
        {
            if (_progress.AllCompleted)
            {
                ShowEndOfContent();
                return;
            }

            StartLevel(ResolveStoredLevelId(), FirstAttempt);
        }

        /// <summary>
        /// The Win "Next" and Lose "Retry" buttons (§11.1). A win advances by id, anything else
        /// replays the same level with a new seed (§8.3).
        /// </summary>
        public void Continue()
        {
            if (_hasResult && _lastResult == LevelResult.Won)
            {
                AdvanceAfterWin();
                return;
            }

            StartLevel(_currentLevelId, _attempt + 1);
        }

        /// <summary>The HUD "Restart" button: same level, new seed, stored progress untouched.</summary>
        public void Restart() => StartLevel(_currentLevelId, _attempt + 1);

        /// <summary>"Play again" from End of content: first level, flag cleared (§8.3).</summary>
        public void PlayAgain()
        {
            int first = _catalog.FirstLevelId;
            _progress.Restart(first);
            StartLevel(first, FirstAttempt);
        }

        public bool CanGoToLevel(int levelId) => _catalog.ContainsId(levelId);

        /// <summary>Cheat §12: loads any level from scratch; stored progress is left alone.</summary>
        public void GoToLevel(int levelId)
        {
            if (!CanGoToLevel(levelId))
            {
                _logger.Warn("Level " + levelId.ToString() + " is not in the catalog.");
                return;
            }

            StartLevel(levelId, FirstAttempt);
        }

        /// <summary>Cheat §12: replays the current level from an explicit seed (D12).</summary>
        public void ReplayWithSeed(int seed) => StartLevelWithSeed(_currentLevelId, _attempt + 1, seed);

        public void Dispose()
        {
            _disposables.Dispose();
            _screen.Dispose();
        }

        private void StartLevel(int levelId, int attempt)
        {
            LevelData level = LoadLevel(levelId);
            BeginAttempt(levelId, attempt);
            _sessions.Create(level, attempt);
            _screen.Value = LevelFlowScreen.Level;
        }

        private void StartLevelWithSeed(int levelId, int attempt, int seed)
        {
            LevelData level = LoadLevel(levelId);
            BeginAttempt(levelId, attempt);
            _sessions.CreateWithSeed(level, attempt, seed);
            _screen.Value = LevelFlowScreen.Level;
        }

        private LevelData LoadLevel(int levelId)
        {
            if (!_catalog.TryGetById(levelId, out LevelConfig config))
            {
                throw new InvalidOperationException("Level " + levelId.ToString() + " is missing from the catalog.");
            }

            return LevelConfigConverter.ToLevelData(config);
        }

        private void BeginAttempt(int levelId, int attempt)
        {
            _currentLevelId = levelId;
            _attempt = attempt;
            _hasResult = false;
        }

        private void AdvanceAfterWin()
        {
            if (_currentLevelId >= _catalog.LastLevelId)
            {
                ShowEndOfContent();
                return;
            }

            StartLevel(NextLevelId(_currentLevelId), FirstAttempt);
        }

        private void ShowEndOfContent()
        {
            _sessions.Destroy();
            _screen.Value = LevelFlowScreen.EndOfContent;
        }

        private void OnLevelFinished(LevelResult result)
        {
            _hasResult = true;
            _lastResult = result;
            WriteProgress(result);
        }

        /// <summary>
        /// Written the moment the level ends rather than when the popup button is pressed: the
        /// page can close in between, and every write is flushed by the repository (§13).
        /// </summary>
        private void WriteProgress(LevelResult result)
        {
            if (result != LevelResult.Won)
            {
                _progress.SetCurrentLevel(_currentLevelId);
                return;
            }

            int last = _catalog.LastLevelId;
            if (_currentLevelId >= last)
            {
                _progress.SetCurrentLevel(last);
                _progress.SetAllCompleted(true);
                return;
            }

            _progress.SetCurrentLevel(NextLevelId(_currentLevelId));
        }

        private int ResolveStoredLevelId()
        {
            int stored = _progress.CurrentLevel;
            return _catalog.ContainsId(stored) ? stored : _catalog.FirstLevelId;
        }

        /// <summary>Smallest catalog id above <paramref name="afterId"/>; ids need not be contiguous.</summary>
        private int NextLevelId(int afterId)
        {
            int next = int.MaxValue;
            for (int i = 0; i < _catalog.Count; i++)
            {
                LevelConfig config = _catalog.GetByIndex(i);
                if (config != null && config.Id > afterId && config.Id < next)
                {
                    next = config.Id;
                }
            }

            return next == int.MaxValue ? afterId : next;
        }
    }
}
