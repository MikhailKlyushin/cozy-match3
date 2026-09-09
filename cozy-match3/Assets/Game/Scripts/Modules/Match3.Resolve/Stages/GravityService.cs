using System;
using Match3.Board;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Stage GRAVITY (GDD §5.1 stage 8, §5.3): vertical fall plus diagonal slide off a shelf, in
    /// passes until nothing moves. Diagonals are checked upper-left before upper-right (D10).
    /// </summary>
    public sealed class GravityService
    {
        private readonly BoardModel _board;

        public GravityService(BoardModel board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
        }

        /// <summary>
        /// One pass over the empty cells in y-then-x order. Returns the number of moves and writes
        /// ChipMoved with Fall or Slide (§5.3).
        /// </summary>
        public int RunSinglePass(TranscriptWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            int moves = 0;

            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var target = new GridPos(x, y);
                    if (!CanHoldChip(_board, target))
                    {
                        continue;
                    }

                    if (TryFall(target, writer) || TrySlide(target, writer))
                    {
                        moves++;
                    }
                }
            }

            return moves;
        }

        /// <summary>
        /// Passes while a pass moves something (§5.3). Every move lowers a chip by one row, so the
        /// loop ends on its own — the pass count is never assumed.
        /// </summary>
        public int RunUntilStable(TranscriptWriter writer)
        {
            int total = 0;
            int moved;

            do
            {
                moved = RunSinglePass(writer);
                total += moved;
            }
            while (moved > 0);

            return total;
        }

        /// <summary>
        /// A cell takes a chip when it is playable, its slot is free and no live element occupies
        /// it (occupancy axis, §7.1). REFILL fills exactly this kind of cell.
        /// </summary>
        internal static bool CanHoldChip(IBoardReader board, GridPos p)
        {
            if (!board.Contains(p) || board.GetKind(p) != CellKind.Playable || !board.GetSlot(p).IsEmpty)
            {
                return false;
            }

            if (!board.TryGetElement(p, out ElementInstance element) || !element.IsAlive)
            {
                return true;
            }

            return !board.Catalog.Get(element.Definition).OccupiesCell;
        }

        /// <summary>§5.3 rule 1: the cell directly above holds a movable chip or booster.</summary>
        private bool TryFall(GridPos target, TranscriptWriter writer)
        {
            GridPos above = target.Up;
            if (!_board.IsMovable(above))
            {
                return false;
            }

            Move(above, target, ChipMoveFlags.Fall, writer);
            return true;
        }

        /// <summary>§5.3 rule 2: only under an impassable ceiling, upper-left before upper-right (D10).</summary>
        private bool TrySlide(GridPos target, TranscriptWriter writer)
        {
            if (_board.IsPassableForFall(target.Up))
            {
                return false;
            }

            return TrySlideFrom(target.UpLeft, target, writer)
                   || TrySlideFrom(target.UpRight, target, writer);
        }

        private bool TrySlideFrom(GridPos source, GridPos target, TranscriptWriter writer)
        {
            // A chip that can still fall vertically stays in its own column (§5.3).
            if (!_board.IsMovable(source) || CanHoldChip(_board, source.Down))
            {
                return false;
            }

            Move(source, target, ChipMoveFlags.Slide, writer);
            return true;
        }

        /// <summary>MoveSlot keeps the InstanceId — the view's chip identity (§6, rule T5).</summary>
        private void Move(GridPos from, GridPos to, ChipMoveFlags flags, TranscriptWriter writer)
        {
            int instanceId = _board.GetSlot(from).InstanceId;
            _board.MoveSlot(from, to);
            writer.ChipMoved(from, to, flags, instanceId);
        }
    }
}
