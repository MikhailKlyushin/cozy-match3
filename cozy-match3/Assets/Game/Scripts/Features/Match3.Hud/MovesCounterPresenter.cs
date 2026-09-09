using System;
using Match3.Content;
using Match3.Progression;
using R3;

namespace Match3.Hud
{
    /// <summary>
    /// Drives <see cref="MovesCounterView"/> from <see cref="LevelSessionState.MovesLeft"/>, which
    /// already drops at COMMIT (D04) — the presenter never recomputes the count.
    /// </summary>
    public sealed class MovesCounterPresenter : IDisposable
    {
        private readonly MovesCounterView _view;
        private readonly TimingProfile _timings;

        private CompositeDisposable _subscriptions;

        /// <summary>The counter pulses at this many moves or fewer (§11.2).</summary>
        public const int LowMovesThreshold = 5;

        public MovesCounterPresenter(MovesCounterView view, TimingProfile timings)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _timings = timings != null ? timings : throw new ArgumentNullException(nameof(timings));
        }

        /// <summary>Binds one level attempt; the HUD outlives the attempt, the session does not.</summary>
        public void Bind(LevelSessionState session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Unbind();

            _subscriptions = new CompositeDisposable();
            _subscriptions.Add(session.MovesLeft.Subscribe(OnMovesLeftChanged));
        }

        public void Unbind()
        {
            if (_subscriptions != null)
            {
                _subscriptions.Dispose();
                _subscriptions = null;
            }

            if (_view != null)
            {
                _view.SetLowMoves(false, 0f);
            }
        }

        public void Dispose()
        {
            Unbind();
        }

        private void OnMovesLeftChanged(int movesLeft)
        {
            _view.SetValue(movesLeft);
            _view.SetLowMoves(movesLeft <= LowMovesThreshold, _timings.LowMovesPulseCycle);
        }
    }
}
