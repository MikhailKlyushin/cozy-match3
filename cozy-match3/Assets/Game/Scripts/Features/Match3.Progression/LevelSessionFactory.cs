using System;
using System.Threading;
using Match3.Levels;
using R3;

namespace Match3.Progression
{
    /// <summary>
    /// Instantiates and destroys the <c>LevelContext</c> prefab that owns one attempt (A08).
    /// Implemented by bootstrap, because the attempt lives in a Zenject GameObjectContext and
    /// progression must not know about prefabs.
    /// </summary>
    public interface ILevelContextFactory
    {
        /// <summary>Creates the attempt and returns the observable state its container binds.</summary>
        LevelSessionState Create(LevelSessionRequest request);

        /// <summary>Tears down the attempt and everything it created, including its session state.</summary>
        void Destroy();
    }

    /// <summary>
    /// Where an attempt seed comes from. Declared here and implemented outside the gameplay path:
    /// the seed is random per attempt, but the attempt itself stays fully deterministic (D12).
    /// </summary>
    public interface ILevelSeedSource
    {
        int NextSeed();
    }

    /// <summary>
    /// Owns the lifetime of one level attempt: its seed, its cancellation token and its context.
    /// The mid-turn board is never persisted, so a reload is always a fresh attempt (E24).
    /// </summary>
    public sealed class LevelSessionFactory : IDisposable
    {
        private readonly ILevelContextFactory _contextFactory;
        private readonly ILevelSeedSource _seedSource;

        private readonly Subject<LevelSessionRequest> _sessionCreated = new Subject<LevelSessionRequest>();
        private readonly Subject<LevelResult> _finished = new Subject<LevelResult>();

        private CancellationTokenSource _attemptCts;
        private IDisposable _finishedLink;
        private LevelSessionState _current;
        private int _seed;
        private int _attempt;
        private bool _disposed;

        public LevelSessionFactory(ILevelContextFactory contextFactory, ILevelSeedSource seedSource)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
        }

        public bool HasSession => _current != null;

        /// <summary>State of the live attempt, or null when none is alive.</summary>
        public LevelSessionState Current => _current;

        /// <summary>Seed of the last attempt, shown in the cheat panel (§12, D12).</summary>
        public int Seed => _seed;

        public int Attempt => _attempt;

        /// <summary>Cancelled before the attempt is torn down; <c>None</c> when none is alive.</summary>
        public CancellationToken AttemptToken => _attemptCts == null ? CancellationToken.None : _attemptCts.Token;

        /// <summary>Carries the §14 <c>level_start</c> fields: id, tier, attempt and seed.</summary>
        public Observable<LevelSessionRequest> SessionCreated => _sessionCreated;

        /// <summary>Republishes the live attempt's outcome, so subscribers outlive single attempts.</summary>
        public Observable<LevelResult> Finished => _finished;

        public LevelSessionState Create(LevelData level, int attempt)
            => CreateWithSeed(level, attempt, _seedSource.NextSeed());

        /// <summary>Replays an attempt bit for bit from an explicit seed (D12, cheat panel §12).</summary>
        public LevelSessionState CreateWithSeed(LevelData level, int attempt, int seed)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            Destroy();

            _attemptCts = new CancellationTokenSource();
            _seed = seed;
            _attempt = attempt;

            LevelSessionRequest request = LevelSessionRequest.For(level, seed, attempt, _attemptCts.Token);
            _current = _contextFactory.Create(request);
            if (_current == null)
            {
                throw new InvalidOperationException("Level context factory returned no session state.");
            }

            _finishedLink = _current.Finished.Subscribe(OnFinished);
            _sessionCreated.OnNext(request);
            return _current;
        }

        /// <summary>Cancels the attempt token first, so no continuation reaches the dying views (I5).</summary>
        public void Destroy()
        {
            if (_attemptCts == null)
            {
                return;
            }

            if (_finishedLink != null)
            {
                _finishedLink.Dispose();
                _finishedLink = null;
            }

            _attemptCts.Cancel();

            if (_current != null)
            {
                _current = null;
                _contextFactory.Destroy();
            }

            _attemptCts.Dispose();
            _attemptCts = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Destroy();
            _sessionCreated.Dispose();
            _finished.Dispose();
        }

        private void OnFinished(LevelResult result) => _finished.OnNext(result);
    }
}
