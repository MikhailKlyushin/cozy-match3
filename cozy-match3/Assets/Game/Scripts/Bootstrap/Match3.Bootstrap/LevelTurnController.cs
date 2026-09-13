using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Core;
using Match3.Gameplay;
using Match3.Gameplay.Playback;
using Match3.Progression;
using Match3.Resolve;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Runs one turn end to end: input, TurnRule, playback, then the observable state the HUD
    /// reads. It lives in the attempt's container because everything it touches does, so tearing
    /// the attempt down takes the controller with it.
    /// </summary>
    public sealed class LevelTurnController : IInitializable, IDisposable
    {
        private readonly TurnRule _turnRule;
        private readonly TranscriptPlayer _player;
        private readonly BoardInputPresenter _input;
        private readonly HintPresenter _hint;
        private readonly LevelSessionState _session;
        private readonly LevelSessionRequest _request;
        private readonly IMatch3Logger _logger;

        private bool _running;
        private bool _finished;

        public LevelTurnController(
            TurnRule turnRule,
            TranscriptPlayer player,
            BoardInputPresenter input,
            HintPresenter hint,
            LevelSessionState session,
            LevelSessionRequest request,
            IMatch3Logger logger)
        {
            _turnRule = turnRule ?? throw new ArgumentNullException(nameof(turnRule));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _input = input != null ? input : throw new ArgumentNullException(nameof(input));
            _hint = hint ?? throw new ArgumentNullException(nameof(hint));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _request = request;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Last transcript, for the cheat panel's dump (§12 dev extension).</summary>
        public TurnTranscript LastTranscript { get; private set; }

        /// <summary>False while a turn plays or after the level ended; the cheat panel asks it.</summary>
        public bool CanRun => !_running && !_finished;

        public void Initialize()
        {
            _session.SetMovesLeft(_turnRule.MovesLeft);
            _input.IntentDetected += OnIntentDetected;
            _hint.Begin(_request.Token);
        }

        public void Dispose() => _input.IntentDetected -= OnIntentDetected;

        /// <summary>Runs a cheat that changes the board and replays it like an ordinary turn (A10).</summary>
        public void ExecuteCheat(in CheatCommand command)
        {
            if (_running)
            {
                return;
            }

            TurnTranscript transcript = _turnRule.ExecuteCheat(command);
            PlayAsync(transcript, _request.Token).Forget(OnTurnFailed);
        }

        private void OnIntentDetected(BoardInputIntent intent)
        {
            // D05, E17: input arriving while a turn plays is dropped, never queued.
            if (_running || _finished || _player.IsPlaying)
            {
                return;
            }

            TurnTranscript transcript = intent.Kind == BoardInputKind.Swap
                ? _turnRule.ExecuteSwap(intent.A, intent.B)
                : _turnRule.ExecuteTap(intent.A);

            PlayAsync(transcript, _request.Token).Forget(OnTurnFailed);
        }

        private async UniTask PlayAsync(TurnTranscript transcript, CancellationToken ct)
        {
            _running = true;
            LastTranscript = transcript;

            // D04: the counter drops as the swap animates, not when the turn ends.
            _session.SetMovesLeft(transcript.MovesLeft);
            _session.SetInputBlocked(true);

            try
            {
                await _player.PlayAsync(transcript, ct);
                _session.NotifyTurnPlayed(transcript);
                Finish(transcript);
            }
            finally
            {
                _running = false;
                if (!_finished)
                {
                    _session.SetInputBlocked(false);
                }
            }
        }

        /// <summary>
        /// A finished level keeps input blocked: the flow layer owns the screen from here, and the
        /// popup decides what happens next (§8.3).
        /// </summary>
        private void Finish(TurnTranscript transcript)
        {
            if (transcript.Outcome != TurnOutcome.Won && transcript.Outcome != TurnOutcome.Lost)
            {
                return;
            }

            _finished = true;
            _hint.SetSuspended(true);
            _session.NotifyFinished(ResultOf(transcript));
        }

        private static LevelResult ResultOf(TurnTranscript transcript)
        {
            if (transcript.Outcome == TurnOutcome.Won)
            {
                return LevelResult.Won;
            }

            for (int i = transcript.EventCount - 1; i >= 0; i--)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.LevelLost)
                {
                    return e.EndReason == LevelEndReason.Deadlock ? LevelResult.Deadlock : LevelResult.Lost;
                }
            }

            return LevelResult.Lost;
        }

        /// <summary>A silent Forget would leave the board frozen with no explanation (§13).</summary>
        private void OnTurnFailed(Exception exception)
        {
            _running = false;

            if (exception is OperationCanceledException)
            {
                return;
            }

            _logger.Error("Turn playback failed: " + exception);
            _session.SetInputBlocked(false);
        }
    }
}
