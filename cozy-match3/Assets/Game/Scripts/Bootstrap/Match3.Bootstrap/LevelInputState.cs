using System;
using Match3.Gameplay;
using Match3.Progression;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Adapts <see cref="LevelSessionState"/> to the interface the input and hint presenters see.
    /// It exists because §3.1 gives Match3.Gameplay no reference to Match3.Progression, and adding
    /// one would invert the layering.
    /// </summary>
    public sealed class LevelInputState : ILevelInputState
    {
        private readonly LevelSessionState _session;

        public LevelInputState(LevelSessionState session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool IsInputBlocked => _session.InputBlocked.CurrentValue;

        public int MovesLeft => _session.MovesLeft.CurrentValue;

        public float HintDelaySeconds => _session.HintDelaySeconds;
    }
}
