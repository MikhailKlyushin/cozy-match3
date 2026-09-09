using System;
using Match3.Gameplay;
using Match3.Progression;
using Match3.Resolve;

namespace Match3.Cheats
{
    /// <summary>
    /// Everything the panel needs from one level attempt. The panel lives in the scene and
    /// outlives attempts, exactly like the HUD presenters, so the per-attempt objects arrive
    /// through <c>CheatPanelPresenter.Bind</c> instead of the constructor.
    /// </summary>
    public sealed class CheatLevelBinding
    {
        public CheatLevelBinding(
            LevelSessionState session,
            TurnRule turnRule,
            IBoardCellPicker picker,
            ICheatTurnRunner runner,
            ICheatHintControl hints)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            TurnRule = turnRule ?? throw new ArgumentNullException(nameof(turnRule));
            Picker = picker ?? throw new ArgumentNullException(nameof(picker));
            Runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Hints = hints ?? throw new ArgumentNullException(nameof(hints));
        }

        public LevelSessionState Session { get; }

        /// <summary>Read for <see cref="TurnRule.CanAcceptInput"/> and the free-moves re-apply (A10).</summary>
        public TurnRule TurnRule { get; }

        public IBoardCellPicker Picker { get; }

        public ICheatTurnRunner Runner { get; }

        public ICheatHintControl Hints { get; }
    }
}
