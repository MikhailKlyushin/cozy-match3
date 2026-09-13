using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// Cheat vocabulary of GDD §12. Board-affecting kinds go through TurnRule.ExecuteCheat and
    /// return a transcript (A10); <see cref="CheatCommandKind.GoToLevel"/> and
    /// <see cref="CheatCommandKind.SetSeed"/> recreate the level attempt instead of mutating the
    /// board, so the cheat presenter routes them to LevelFlowRule.
    /// </summary>
    public enum CheatCommandKind : byte
    {
        None = 0,

        /// <summary>Routed to LevelFlowRule: loads the level from scratch.</summary>
        GoToLevel = 1,

        /// <summary>Closes every goal and plays the win sequence, leftover-move bonus included.</summary>
        WinLevel = 2,

        LoseLevel = 3,

        AddMoves = 4,

        /// <summary>Does not charge a move (§12).</summary>
        PlaceBooster = 5,

        /// <summary>Routed to LevelFlowRule: restarts the attempt with the given seed (D12).</summary>
        SetSeed = 6,

        /// <summary>Toggle: the move counter stops decreasing.</summary>
        FreeMoves = 7
    }

    public readonly struct CheatCommand
    {
        public readonly CheatCommandKind Kind;

        /// <summary>Level number, added moves or seed, depending on <see cref="Kind"/>.</summary>
        public readonly int IntValue;

        public readonly bool BoolValue;
        public readonly GridPos Cell;
        public readonly BoosterType Booster;

        public CheatCommand(
            CheatCommandKind kind,
            int intValue = 0,
            bool boolValue = false,
            GridPos cell = default,
            BoosterType booster = BoosterType.None)
        {
            Kind = kind;
            IntValue = intValue;
            BoolValue = boolValue;
            Cell = cell;
            Booster = booster;
        }

        /// <summary>True for kinds TurnRule executes; the rest belong to the level flow.</summary>
        public bool IsBoardCommand
            => Kind == CheatCommandKind.WinLevel
               || Kind == CheatCommandKind.LoseLevel
               || Kind == CheatCommandKind.AddMoves
               || Kind == CheatCommandKind.PlaceBooster
               || Kind == CheatCommandKind.FreeMoves;

        public static CheatCommand GoToLevel(int levelId)
            => new CheatCommand(CheatCommandKind.GoToLevel, levelId);

        public static CheatCommand WinLevel()
            => new CheatCommand(CheatCommandKind.WinLevel);

        public static CheatCommand LoseLevel()
            => new CheatCommand(CheatCommandKind.LoseLevel);

        public static CheatCommand AddMoves(int amount)
            => new CheatCommand(CheatCommandKind.AddMoves, amount);

        public static CheatCommand PlaceBooster(GridPos cell, BoosterType booster)
            => new CheatCommand(CheatCommandKind.PlaceBooster, cell: cell, booster: booster);

        public static CheatCommand SetSeed(int seed)
            => new CheatCommand(CheatCommandKind.SetSeed, seed);

        public static CheatCommand FreeMoves(bool enabled)
            => new CheatCommand(CheatCommandKind.FreeMoves, boolValue: enabled);
    }
}
