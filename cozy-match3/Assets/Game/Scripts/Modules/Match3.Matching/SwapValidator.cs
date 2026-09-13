using System;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Matching
{
    /// <summary>
    /// VALIDATE (§5.1): a swap is legal when both cells are playable and movable AND (the swap
    /// creates a match OR at least one of the cells holds a booster).
    /// </summary>
    public sealed class SwapValidator
    {
        private readonly BoardModel _board;
        private readonly MatchDetectionService _detection;

        public SwapValidator(BoardModel board, MatchDetectionService detection)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _detection = detection ?? throw new ArgumentNullException(nameof(detection));
        }

        public bool IsLegal(GridPos a, GridPos b)
        {
            if (!CanBeSwapped(a, b))
            {
                return false;
            }

            if (HasBooster(a) || HasBooster(b))
            {
                return true;
            }

            return ProbeCreatesMatch(a, b);
        }

        /// <summary>Match only, boosters ignored: the hint needs it (§5.5, priority 1).</summary>
        public bool CreatesMatch(GridPos a, GridPos b) => CanBeSwapped(a, b) && ProbeCreatesMatch(a, b);

        private bool CanBeSwapped(GridPos a, GridPos b)
        {
            if (!_board.Contains(a) || !_board.Contains(b))
            {
                return false;
            }

            // The player swaps two neighbouring cells (§5.1).
            if (!a.IsOrthogonalNeighbourOf(b))
            {
                return false;
            }

            if (_board.GetKind(a) != CellKind.Playable || _board.GetKind(b) != CellKind.Playable)
            {
                return false;
            }

            return _board.IsMovable(a) && _board.IsMovable(b);
        }

        private bool HasBooster(GridPos p) => _board.GetSlot(p).Kind == SlotKind.Booster;

        /// <summary>Swaps, detects and swaps back: the board is left exactly as it was.</summary>
        private bool ProbeCreatesMatch(GridPos a, GridPos b)
        {
            _board.SwapSlots(a, b);
            bool createsMatch = _detection.HasAnyMatch();
            _board.SwapSlots(a, b);
            return createsMatch;
        }
    }
}
