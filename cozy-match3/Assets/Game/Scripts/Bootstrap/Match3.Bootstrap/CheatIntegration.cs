#if MATCH3_CHEATS
using System;
using Match3.Cheats;
using Match3.Gameplay;
using Match3.Progression;
using Match3.Resolve;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Adapts the attempt's objects to the ports the cheat panel declares. The whole file is
    /// compiled out without MATCH3_CHEATS, so nothing here can leak into a release build (§15).
    /// </summary>
    public sealed class CheatTurnRunner : ICheatTurnRunner
    {
        private readonly LevelTurnController _turns;

        public CheatTurnRunner(LevelTurnController turns)
        {
            _turns = turns ?? throw new ArgumentNullException(nameof(turns));
        }

        public bool CanRun => _turns.CanRun;

        public TurnTranscript LastTranscript => _turns.LastTranscript;

        public void ExecuteCheat(in CheatCommand command) => _turns.ExecuteCheat(command);
    }

    public sealed class CheatHintControl : ICheatHintControl
    {
        private readonly HintPresenter _hints;

        public CheatHintControl(HintPresenter hints)
        {
            _hints = hints ?? throw new ArgumentNullException(nameof(hints));
        }

        public void SetSuspended(bool suspended) => _hints.SetSuspended(suspended);

        public void ShowNow() => _hints.ShowNow();
    }

    /// <summary>
    /// Hands the live attempt to the scene-scoped panel and takes it back on teardown - the same
    /// lifetime rule the HUD presenters follow.
    /// </summary>
    public sealed class CheatAttemptBinder : IInitializable, IDisposable
    {
        private readonly CheatPanelPresenter _presenter;
        private readonly LevelSessionState _session;
        private readonly TurnRule _turnRule;
        private readonly IBoardCellPicker _picker;
        private readonly LevelTurnController _turns;
        private readonly HintPresenter _hints;

        public CheatAttemptBinder(
            CheatPanelPresenter presenter,
            LevelSessionState session,
            TurnRule turnRule,
            IBoardCellPicker picker,
            LevelTurnController turns,
            HintPresenter hints)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _turnRule = turnRule ?? throw new ArgumentNullException(nameof(turnRule));
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _turns = turns ?? throw new ArgumentNullException(nameof(turns));
            _hints = hints ?? throw new ArgumentNullException(nameof(hints));
        }

        public void Initialize()
        {
            _presenter.Bind(new CheatLevelBinding(
                _session,
                _turnRule,
                _picker,
                new CheatTurnRunner(_turns),
                new CheatHintControl(_hints)));
        }

        public void Dispose() => _presenter.Unbind();
    }

    /// <summary>Points the HUD's bug button at the panel; without the define the button is inert.</summary>
    public sealed class CheatButtonBinder : IInitializable
    {
        private readonly GameFlowController _flow;
        private readonly CheatPanelPresenter _presenter;

        public CheatButtonBinder(GameFlowController flow, CheatPanelPresenter presenter)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        }

        public void Initialize() => _flow.CheatsRequested = _presenter.Toggle;
    }
}
#endif
