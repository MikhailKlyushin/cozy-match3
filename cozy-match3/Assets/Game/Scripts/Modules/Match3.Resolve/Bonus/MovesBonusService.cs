using System;
using System.Collections.Generic;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Leftover-move bonus (GDD §8.4): every remaining move becomes a rocket on a random chip
    /// cell. Capped, and it cannot lose the level - the result is already decided (E23).
    /// </summary>
    public sealed class MovesBonusService
    {
        private readonly BoardModel _board;
        private readonly IRandom _random;

        private readonly List<GridPos> _candidates = new List<GridPos>(96);

        public MovesBonusService(BoardModel board, IRandom random)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Appends up to <see cref="ResolveCaps.MaxMovesBonus"/> distinct chip cells, drawn from
        /// the attempt's RNG (RandomConsumer.MovesBonus). Returns how many were picked.
        /// </summary>
        public int PickCells(int movesLeft, CellBuffer result)
        {
            if (movesLeft <= 0)
            {
                return 0;
            }

            int wanted = movesLeft > ResolveCaps.MaxMovesBonus ? ResolveCaps.MaxMovesBonus : movesLeft;

            _candidates.Clear();
            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (_board.GetSlot(cell).Kind == SlotKind.Chip && _board.IsMovable(cell))
                    {
                        _candidates.Add(cell);
                    }
                }
            }

            int picked = 0;
            while (picked < wanted && _candidates.Count > 0)
            {
                int index = _random.NextInt(_candidates.Count);
                if (result.AddUnique(_candidates[index]))
                {
                    picked++;
                }

                // Swap-remove keeps the draw allocation-free and stays reproducible for a seed.
                int last = _candidates.Count - 1;
                _candidates[index] = _candidates[last];
                _candidates.RemoveAt(last);
            }

            return picked;
        }
    }
}
