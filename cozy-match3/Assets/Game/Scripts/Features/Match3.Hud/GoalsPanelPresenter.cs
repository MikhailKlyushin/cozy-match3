using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Content;
using Match3.Core;
using Match3.Goals;
using Match3.Progression;
using Match3.Resolve;
using R3;

namespace Match3.Hud
{
    /// <summary>
    /// Ticks the goal counters from the transcript's GoalProgress events (rule T4): the delta is
    /// authoritative and the reported total is already clamped (E18), so nothing is recomputed from
    /// goal state while the model is a turn ahead (rule V1).
    /// </summary>
    public sealed class GoalsPanelPresenter : IDisposable
    {
        private readonly GoalsPanelView _view;
        private readonly GoalIconResolver _icons;
        private readonly TimingProfile _timings;
        private readonly ISfxPlayer _sfx;
        private readonly IMatch3Logger _logger;
        private readonly List<UniTask> _ticks = new List<UniTask>(4);

        private CompositeDisposable _subscriptions;
        private CancellationTokenSource _cts;
        private GoalProgressReader _reader;
        private bool _ticking;

        public GoalsPanelPresenter(
            GoalsPanelView view,
            GoalIconResolver icons,
            TimingProfile timings,
            ISfxPlayer sfx,
            IMatch3Logger logger)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _icons = icons ?? throw new ArgumentNullException(nameof(icons));
            _timings = timings != null ? timings : throw new ArgumentNullException(nameof(timings));
            _sfx = sfx ?? throw new ArgumentNullException(nameof(sfx));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsTicking => _ticking;

        /// <summary>Binds one level attempt and draws the starting values.</summary>
        public void Bind(LevelSessionState session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Unbind();

            IGoalTracker goals = session.Goals;
            if (goals.Count > _view.RowCapacity)
            {
                _logger.Error(
                    "Goals panel has " + _view.RowCapacity + " rows but the level declares " + goals.Count + " goals.");
            }

            _view.Render(goals, _icons);
            _reader = new GoalProgressReader(goals.Count);
            _cts = new CancellationTokenSource();
            _subscriptions = new CompositeDisposable();
            _subscriptions.Add(session.TurnPlayed.Subscribe(OnTurnPlayed));
        }

        public void Unbind()
        {
            if (_subscriptions != null)
            {
                _subscriptions.Dispose();
                _subscriptions = null;
            }

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            _ticking = false;
            _ticks.Clear();
            _reader = null;
        }

        public void Dispose()
        {
            Unbind();
        }

        /// <summary>
        /// Plays the whole turn's progress. Goals tick in parallel so the panel stays inside the
        /// 1.0 s cap of §11.3 even with three goals moving at once.
        /// </summary>
        public async UniTask PlayProgressAsync(TurnTranscript transcript, CancellationToken ct)
        {
            if (transcript == null)
            {
                throw new ArgumentNullException(nameof(transcript));
            }

            if (_reader == null)
            {
                return;
            }

            if (_ticking)
            {
                // Turns never overlap: input is blocked while one replays (D05, E17).
                _logger.Error("Goal tick re-entered while the previous one was still running.");
                return;
            }

            _reader.Read(transcript);
            if (_reader.SkippedEventCount > 0)
            {
                _logger.Error("Transcript reported progress for a goal index this level does not have.");
            }

            _ticks.Clear();
            for (int i = 0; i < _reader.GoalCount; i++)
            {
                if (!_reader.HasProgress(i))
                {
                    continue;
                }

                GoalTickSchedule schedule = GoalTickSchedule.Between(_reader.GetFrom(i), _reader.GetValue(i), _timings);
                _ticks.Add(TickGoalAsync(i, schedule, ct));
            }

            if (_ticks.Count == 0)
            {
                return;
            }

            _ticking = true;
            try
            {
                await UniTask.WhenAll(_ticks);
            }
            finally
            {
                _ticking = false;
                _ticks.Clear();
            }
        }

        private async UniTask TickGoalAsync(int goalIndex, GoalTickSchedule schedule, CancellationToken ct)
        {
            for (int tick = 0; tick < schedule.Units; tick++)
            {
                ct.ThrowIfCancellationRequested();

                _view.SetValue(goalIndex, schedule.ValueAt(tick));
                _view.PlayTick(goalIndex, schedule.Step);

                // Rung per tick, so a run of them reads as one counter climbing rather than the
                // same note repeated. Goals tick in parallel, so each keeps its own climb.
                _sfx.PlayLadder(SfxId.GoalTick, tick);

                await UniTask.Delay(
                    TimeSpan.FromSeconds(schedule.Step),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);
            }
        }

        private void OnTurnPlayed(TurnTranscript transcript)
        {
            RunProgressAsync(transcript).Forget();
        }

        /// <summary>Deliberate fire-and-forget adapter: it logs, so a failed tick is never silent (§13).</summary>
        private async UniTaskVoid RunProgressAsync(TurnTranscript transcript)
        {
            CancellationTokenSource cts = _cts;
            if (cts == null)
            {
                return;
            }

            try
            {
                await PlayProgressAsync(transcript, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Level teardown mid-tick: nothing to report.
            }
            catch (Exception e)
            {
                _logger.Error("Goal counter tick failed: " + e);
            }
        }
    }
}
