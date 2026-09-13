using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Core;
using Match3.Gameplay.Playback;
using Match3.Resolve;

namespace Match3.Gameplay
{
    /// <summary>
    /// The idle timer of §5.5. Timing goes through UniTask with DelayType.DeltaTime, never an R3
    /// timer: only the NuGet core of R3 is installed, so its timers run on the wall clock and
    /// ignore pause (A09). The timer never triggers a shuffle - that is POST-TURN's job.
    /// </summary>
    public sealed class HintPresenter : IDisposable
    {
        private readonly HintView _view;
        private readonly TurnRule _turnRule;
        private readonly ILevelInputState _session;
        private readonly TranscriptPlayer _player;
        private readonly BoardInputPresenter _input;
        private readonly IMatch3Logger _logger;

        private CancellationTokenSource _stageCts;
        private HintPlan _followTarget;
        private bool _suspended;
        private bool _showNowRequested;
        private bool _running;
        private bool _disposed;

        public HintPresenter(
            HintView view,
            TurnRule turnRule,
            ILevelInputState session,
            TranscriptPlayer player,
            BoardInputPresenter input,
            IMatch3Logger logger)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _turnRule = turnRule ?? throw new ArgumentNullException(nameof(turnRule));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _input = input != null ? input : throw new ArgumentNullException(nameof(input));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _input.Interacted += OnInteracted;
            _input.IntentDetected += OnIntentDetected;
        }

        /// <summary>Carries the rule priority 1-4 for the hint_shown metric (§14).</summary>
        public event Action<HintPlan> HintShown;

        /// <summary>The next player action matched the suggestion: hint_followed (§14).</summary>
        public event Action<HintPlan> HintFollowed;

        /// <summary>Starts the idle watch for the level attempt; exceptions are logged (§13).</summary>
        public void Begin(CancellationToken ct)
        {
            if (_running || _disposed)
            {
                return;
            }

            _running = true;
            RunAsync(ct).Forget(OnLoopFailed);
        }

        /// <summary>
        /// Waits out <see cref="ILevelInputState.HintDelaySeconds"/> of idle time, shows the hint,
        /// and starts over on any input. Returns immediately when hints are disabled (§5.5).
        /// </summary>
        public async UniTask RunAsync(CancellationToken ct)
        {
            if (_session.HintDelaySeconds <= 0f)
            {
                return;
            }

            while (!_disposed)
            {
                ct.ThrowIfCancellationRequested();

                if (_showNowRequested)
                {
                    _showNowRequested = false;
                    await ShowAsync(ct);
                    continue;
                }

                if (!CanCountDown())
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    continue;
                }

                if (await CountDownAsync(ct))
                {
                    await ShowAsync(ct);
                }
            }
        }

        /// <summary>The cheat panel and any other owner of the screen stops the timer (§5.5, §12).</summary>
        public void SetSuspended(bool suspended)
        {
            _suspended = suspended;

            if (suspended)
            {
                Interrupt();
            }
        }

        /// <summary>Cheat "hint now": skips the remaining idle wait (§12).</summary>
        public void ShowNow()
        {
            _showNowRequested = true;
            Interrupt();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_input != null)
            {
                _input.Interacted -= OnInteracted;
                _input.IntentDetected -= OnIntentDetected;
            }

            Interrupt();
            DisposeStage();

            if (_view != null)
            {
                _view.HideImmediate();
            }
        }

        /// <summary>
        /// True when a full idle window elapsed. The wait restarts on any input and whenever the
        /// level leaves IDLE, so the countdown never runs during resolution or a popup (§5.5).
        /// </summary>
        private async UniTask<bool> CountDownAsync(CancellationToken ct)
        {
            _stageCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_session.HintDelaySeconds),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    _stageCts.Token);
            }
            catch (OperationCanceledException)
            {
                ct.ThrowIfCancellationRequested();
                return false;
            }
            finally
            {
                DisposeStage();
            }

            return CanCountDown();
        }

        private async UniTask ShowAsync(CancellationToken ct)
        {
            // Reached only from IDLE, so asking the model here does not break rule V1: no turn is
            // replaying. Executing intents stays with the flow layer.
            HintPlan plan = _turnRule.GetHint();
            if (!plan.HasHint)
            {
                // No legal move: stay silent. A missing hint is never a shuffle trigger (§5.5).
                return;
            }

            _followTarget = plan;
            _stageCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            HintShown?.Invoke(plan);

            try
            {
                await _view.ShowAsync(plan, _stageCts.Token);
            }
            catch (OperationCanceledException)
            {
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                DisposeStage();
            }

            await _view.HideAsync(ct);
        }

        private bool CanCountDown()
            => !_disposed
               && !_suspended
               && !_player.IsPlaying
               && !_session.IsInputBlocked
               && _session.MovesLeft > 0;

        private void Interrupt() => _stageCts?.Cancel();

        private void DisposeStage()
        {
            _stageCts?.Dispose();
            _stageCts = null;
        }

        /// <summary>Any input resets the timer, an illegal swap and an empty cell included (§5.5).</summary>
        private void OnInteracted() => Interrupt();

        private void OnIntentDetected(BoardInputIntent intent)
        {
            if (!_followTarget.HasHint)
            {
                return;
            }

            HintPlan plan = _followTarget;
            _followTarget = HintPlan.None;

            if (Matches(plan, intent))
            {
                HintFollowed?.Invoke(plan);
            }
        }

        private static bool Matches(in HintPlan plan, in BoardInputIntent intent)
        {
            if (plan.Kind == HintKind.TapBooster)
            {
                return intent.Kind == BoardInputKind.Tap && intent.A == plan.A;
            }

            return intent.Kind == BoardInputKind.Swap
                   && ((intent.A == plan.A && intent.B == plan.B)
                       || (intent.A == plan.B && intent.B == plan.A));
        }

        private void OnLoopFailed(Exception error)
        {
            if (error is OperationCanceledException)
            {
                // Level teardown mid-wait: expected, the attempt owns the token.
                return;
            }

            _logger.Error("Hint presenter failed: " + error);
        }
    }
}
