using Match3.Resolve;

namespace Match3.Cheats
{
    /// <summary>Where a <see cref="CheatCommand"/> has to be executed (architecture §15).</summary>
    public enum CheatRoute : byte
    {
        /// <summary>No executor owns this kind; the panel logs it instead of guessing.</summary>
        Unsupported = 0,

        /// <summary>Mutates the board: TurnRule.ExecuteCheat, and a transcript comes back (A10).</summary>
        Board = 1,

        /// <summary>Recreates the attempt instead of touching the board: LevelFlowRule.</summary>
        LevelFlow = 2
    }

    /// <summary>
    /// The §15 split, as data. TurnRule rejects <see cref="CheatCommandKind.GoToLevel"/> and
    /// <see cref="CheatCommandKind.SetSeed"/>, so the panel must not hand them to it.
    /// </summary>
    public static class CheatCommandRouting
    {
        public static CheatRoute Route(in CheatCommand command)
        {
            if (command.IsBoardCommand)
            {
                return CheatRoute.Board;
            }

            switch (command.Kind)
            {
                case CheatCommandKind.GoToLevel:
                case CheatCommandKind.SetSeed:
                    return CheatRoute.LevelFlow;

                default:
                    return CheatRoute.Unsupported;
            }
        }

        /// <summary>
        /// True for the kinds that hand the screen to something else - a WIN/LOSE sequence, its
        /// popup, or a freshly built level, which the panel must not cover. It closes once the
        /// command has actually run, so a press the board refused leaves the panel open.
        /// </summary>
        public static bool ClosesPanel(in CheatCommand command)
        {
            switch (command.Kind)
            {
                case CheatCommandKind.WinLevel:
                case CheatCommandKind.LoseLevel:
                case CheatCommandKind.GoToLevel:
                case CheatCommandKind.SetSeed:
                    return true;

                default:
                    return false;
            }
        }
    }
}
