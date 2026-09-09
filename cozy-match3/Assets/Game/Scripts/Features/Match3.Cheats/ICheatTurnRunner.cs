using Match3.Resolve;

namespace Match3.Cheats
{
    /// <summary>
    /// The attempt's single turn path, as the cheat panel sees it: a board cheat is executed by
    /// <c>TurnRule.ExecuteCheat</c> and replayed exactly like a player turn, so there is one board
    /// writer and one playback path (A10, §15).
    /// <para>
    /// Declared here because Match3.Cheats may reference Match3.Gameplay for the interfaces
    /// Gameplay itself declares and nothing else (§3.1), and the turn runner lives in the
    /// composition root. Its shape is the surface <c>LevelTurnController</c> already exposes.
    /// </para>
    /// </summary>
    public interface ICheatTurnRunner
    {
        /// <summary>False while a turn is replaying and once the level has ended (D05, E17).</summary>
        bool CanRun { get; }

        /// <summary>Last transcript this attempt produced; null before its first turn.</summary>
        TurnTranscript LastTranscript { get; }

        /// <summary>Runs one board cheat and replays the transcript it returns.</summary>
        void ExecuteCheat(in CheatCommand command);
    }
}
