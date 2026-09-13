using System;
using Match3.Board;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Stage GRAVITY (GDD §5.1 stage 8, §5.3): vertical fall - straight down, or through a cell
    /// whose element occupies it without stopping falls - plus diagonal slide off a shelf, in
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

                    if (TryFall(target, writer)
                        || TryFallThrough(target, writer)
                        || TrySlide(target, writer))
                    {
                        moves++;
                    }
                }
            }

            return moves;
        }

        /// <summary>
        /// Passes while a pass moves something (§5.3). Every move lowers a chip by at least one
        /// row, so the loop ends on its own — the pass count is never assumed.
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

        /// <summary>
        /// §5.3 rule 2: the cell above the target holds a live element that does not stop falls,
        /// so the column is not cut - the first movable chip above that run drops through it. A
        /// pocket above a blocker feeds its own column instead of the neighbour lending a chip.
        /// </summary>
        private bool TryFallThrough(GridPos target, TranscriptWriter writer)
        {
            GridPos source = target.Up;
            if (!_board.IsFallThrough(source))
            {
                return false;
            }

            do
            {
                source = source.Up;
            }
            while (_board.IsFallThrough(source));

            // A hole, a blocking element or an empty cell ends the run; an empty one takes an
            // ordinary fall first and drops through on a later pass.
            if (!_board.IsMovable(source))
            {
                return false;
            }

            Move(source, target, ChipMoveFlags.Fall, writer);
            return true;
        }

        /// <summary>§5.3 rule 3: only when nothing arrives vertically, upper-left first (D10).</summary>
        private bool TrySlide(GridPos target, TranscriptWriter writer)
        {
            // The cell above can still take a chip, so the column is only mid-settle: waiting for
            // it keeps the chip in its own column.
            if (CanHoldChip(_board, target.Up))
            {
                return false;
            }

            return TrySlideFrom(target.UpLeft, target, writer)
                   || TrySlideFrom(target.UpRight, target, writer);
        }

        private bool TrySlideFrom(GridPos source, GridPos target, TranscriptWriter writer)
        {
            // A chip that can still descend its own column stays in it (§5.3).
            if (!_board.IsMovable(source) || CanDescendOwnColumn(source))
            {
                return false;
            }

            Move(source, target, ChipMoveFlags.Slide, writer);
            return true;
        }

        /// <summary>Whether a chip has somewhere to land below, dropping through what it may.</summary>
        private bool CanDescendOwnColumn(GridPos source)
        {
            GridPos below = source.Down;
            while (_board.IsFallThrough(below))
            {
                below = below.Down;
            }

            return CanHoldChip(_board, below);
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
