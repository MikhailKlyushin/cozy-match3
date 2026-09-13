using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Core;
using Match3.Diagnostics;
using Match3.Hud;
using Match3.Progression;
using R3;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Scene-level glue: binds the HUD to each attempt, shows the outcome popup and lets the level
    /// flow decide what comes next (§8.3, §11.1). Turn execution belongs to the attempt itself.
    /// </summary>
    public sealed class GameFlowController : IInitializable, IDisposable
    {
        private readonly LevelFlowRule _flow;
        private readonly LevelSessionFactory _sessions;
        private readonly PopupService _popups;
        private readonly MovesCounterPresenter _movesCounter;
        private readonly GoalsPanelPresenter _goalsPanel;
        private readonly HudActionsView _hudActions;
        private readonly MetricsReporter _metrics;
        private readonly IMatch3Logger _logger;

        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        private Action _cheatsRequested;

        public GameFlowController(
            LevelFlowRule flow,
            LevelSessionFactory sessions,
            PopupService popups,
            MovesCounterPresenter movesCounter,
            GoalsPanelPresenter goalsPanel,
            HudActionsView hudActions,
            MetricsReporter metrics,
            IMatch3Logger logger)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _popups = popups ?? throw new ArgumentNullException(nameof(popups));
            _movesCounter = movesCounter ?? throw new ArgumentNullException(nameof(movesCounter));
            _goalsPanel = goalsPanel ?? throw new ArgumentNullException(nameof(goalsPanel));
            _hudActions = hudActions != null ? hudActions : throw new ArgumentNullException(nameof(hudActions));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Assigned by the cheats installer when the panel is compiled in (§15). Setting it also
        /// reveals the HUD button, so neither side depends on which installer runs first.
        /// </summary>
        public Action CheatsRequested
        {
            get => _cheatsRequested;
            set
            {
                _cheatsRequested = value;
                _hudActions.SetCheatsAvailable(value != null);
            }
        }

        public void Initialize()
        {
            // The button ships in the scene, so a build without the panel has to hide it here.
            _hudActions.SetCheatsAvailable(_cheatsRequested != null);

            _disposables.Add(_sessions.SessionCreated.Subscribe(OnSessionCreated));
            _disposables.Add(_sessions.Finished.Subscribe(OnLevelFinished));
            _disposables.Add(_flow.Screen.Subscribe(OnScreenChanged));

            _hudActions.RestartClicked += OnRestartClicked;
            _hudActions.CheatsClicked += OnCheatsClicked;

            _flow.Enter();
        }

        public void Dispose()
        {
            _hudActions.RestartClicked -= OnRestartClicked;
            _hudActions.CheatsClicked -= OnCheatsClicked;

            _cancellation.Cancel();
            _cancellation.Dispose();
            _disposables.Dispose();
        }

        private void OnSessionCreated(LevelSessionRequest request)
        {
            LevelSessionState session = _sessions.Current;
            if (session == null)
            {
                return;
            }

            _movesCounter.Bind(session);
            _goalsPanel.Bind(session);
            _popups.CloseAll();

            _metrics.LevelStart(request.Level.Id, request.Level.Tier, request.Attempt, request.Seed);
        }

        private void OnLevelFinished(LevelResult result)
        {
            ShowOutcomeAsync(result, _cancellation.Token).Forget(OnFlowFailed);
        }

        /// <summary>
        /// The popup is the gate between attempts: the flow only advances when the player presses
        /// its button, so a cascade is never cut short by an automatic reload (§8.3, E13).
        /// </summary>
        private async UniTask ShowOutcomeAsync(LevelResult result, CancellationToken ct)
        {
            LevelSessionState session = _sessions.Current;
            if (session == null)
            {
                return;
            }

            _metrics.LevelEnd(
                session.LevelId,
                ToMetricsResult(result),
                session.MoveLimit - session.MovesLeft.CurrentValue,
                session.MovesLeft.CurrentValue,
                session.Goals,
                session.Seed);

            if (result == LevelResult.Won)
            {
                await _popups.ShowWinAsync(session.Goals, ct);
            }
            else
            {
                await _popups.ShowLoseAsync(session.Goals, ct);
            }

            _flow.Continue();
        }

        private void OnScreenChanged(LevelFlowScreen screen)
        {
            if (screen == LevelFlowScreen.EndOfContent)
            {
                ShowEndOfContentAsync(_cancellation.Token).Forget(OnFlowFailed);
            }
        }

        private async UniTask ShowEndOfContentAsync(CancellationToken ct)
        {
            await _popups.ShowEndOfContentAsync(_flow.LastLevelId, ct);
            _flow.PlayAgain();
        }

        private void OnRestartClicked() => _flow.Restart();

        private void OnCheatsClicked()
        {
            Action handler = _cheatsRequested;
            if (handler == null)
            {
                // The button is hidden in that case, so this only guards a stray click (§15).
                _logger.Info("Cheat panel is not available in this build.");
                return;
            }

            handler();
        }

        private static LevelResultKind ToMetricsResult(LevelResult result)
        {
            switch (result)
            {
                case LevelResult.Won:
                    return LevelResultKind.Win;
                case LevelResult.Deadlock:
                    return LevelResultKind.Deadlock;
                default:
                    return LevelResultKind.Lose;
            }
        }

        private void OnFlowFailed(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            _logger.Error("Level flow failed: " + exception);
        }
    }
}
