using System;
using System.Collections.Generic;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Matching
{
    /// <summary>
    /// Legal move cache: built once on POST-TURN and reused by shuffling (§5.4) and the hint
    /// (§5.5), so there is no second pass over the board.
    /// </summary>
    public sealed class LegalMoveService
    {
        private readonly BoardModel _board;
        private readonly SwapValidator _validator;
        private readonly PooledList<LegalMove> _moves = new PooledList<LegalMove>(32);

        private bool _dirty = true;

        public LegalMoveService(BoardModel board, SwapValidator validator)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        /// <summary>Built lazily; order y-up x-up, the horizontal swap of a cell before its vertical one.</summary>
        public IReadOnlyList<LegalMove> Moves
        {
            get
            {
                Rebuild();
                return _moves;
            }
        }

        public bool HasAnyMove
        {
            get
            {
                Rebuild();
                return _moves.Count > 0;
            }
        }

        /// <summary>Must be called on any board change.</summary>
        public void Invalidate() => _dirty = true;

        private void Rebuild()
        {
            if (!_dirty)
            {
                return;
            }

            _moves.Clear();
            int width = _board.Width;
            int height = _board.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new GridPos(x, y);

                    if (x + 1 < width)
                    {
                        var right = new GridPos(x + 1, y);
                        if (_validator.IsLegal(cell, right))
                        {
                            _moves.Add(new LegalMove(cell, right));
                        }
                    }

                    if (y + 1 < height)
                    {
                        var up = new GridPos(x, y + 1);
                        if (_validator.IsLegal(cell, up))
                        {
                            _moves.Add(new LegalMove(cell, up));
                        }
                    }
                }
            }

            _dirty = false;
        }
    }
}
