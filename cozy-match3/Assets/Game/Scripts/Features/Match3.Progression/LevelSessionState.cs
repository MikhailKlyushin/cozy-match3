using System;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using R3;

namespace Match3.Progression
{
    public enum LevelResult : byte
    {
        Won = 0,
        Lost = 1,

        /// <summary>Shuffle could not restore a legal move (E19). Always a bug.</summary>
        Deadlock = 2
    }

    /// <summary>
    /// Observable state of one level attempt. The single channel the HUD subscribes to, so
    /// presenters never reach into the model (§13: R3 carries state, never timing).
    /// </summary>
    public sealed class LevelSessionState : IDisposable
    {
        private readonly ReactiveProperty<int> _movesLeft;
        private readonly ReactiveProperty<bool> _inputBlocked = new ReactiveProperty<bool>(false);
        private readonly Subject<TurnTranscript> _turnPlayed = new Subject<TurnTranscript>();
        private readonly Subject<LevelResult> _finished = new Subject<LevelResult>();

        public LevelSessionState(
            int levelId,
            DifficultyTier tier,
            int seed,
            int moveLimit,
            IGoalTracker goals,
            float hintDelaySeconds)
        {
            LevelId = levelId;
            Tier = tier;
            Seed = seed;
            MoveLimit = moveLimit;
            Goals = goals ?? throw new ArgumentNullException(nameof(goals));
            HintDelaySeconds = hintDelaySeconds;
            _movesLeft = new ReactiveProperty<int>(moveLimit);
        }

        public int LevelId { get; }

        public DifficultyTier Tier { get; }

        /// <summary>Attempt seed, shown in the cheat panel and logged on start (D12).</summary>
        public int Seed { get; }

        public int MoveLimit { get; }

        /// <summary>Read model for the initial HUD render; progress deltas arrive by transcript.</summary>
        public IGoalTracker Goals { get; }

        public float HintDelaySeconds { get; }

        /// <summary>Decreases at COMMIT, not at the end of the turn (D04).</summary>
        public ReadOnlyReactiveProperty<int> MovesLeft => _movesLeft;

        /// <summary>True while a turn is replaying or a popup is open; input is dropped (D05, E17).</summary>
        public ReadOnlyReactiveProperty<bool> InputBlocked => _inputBlocked;

        /// <summary>Fires once a turn has finished replaying, so the HUD can tick from it.</summary>
        public Observable<TurnTranscript> TurnPlayed => _turnPlayed;

        public Observable<LevelResult> Finished => _finished;

        public void SetMovesLeft(int value) => _movesLeft.Value = value;

        public void SetInputBlocked(bool value) => _inputBlocked.Value = value;

        public void NotifyTurnPlayed(TurnTranscript transcript) => _turnPlayed.OnNext(transcript);

        public void NotifyFinished(LevelResult result) => _finished.OnNext(result);

        public void Dispose()
        {
            _movesLeft.Dispose();
            _inputBlocked.Dispose();
            _turnPlayed.Dispose();
            _finished.Dispose();
        }
    }
}
