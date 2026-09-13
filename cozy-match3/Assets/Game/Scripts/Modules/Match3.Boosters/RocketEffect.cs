using System;
using Match3.Board;
using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>
    /// GDD §6.2: the whole row (RocketH) or column (RocketV) including its own cell. The line is
    /// walked edge to edge - an obstacle takes 1 damage and does not stop the beam (D11, E08).
    /// </summary>
    public sealed class RocketEffect : IBoosterEffect
    {
        private readonly BoosterType _orientation;

        public RocketEffect(BoosterType orientation)
        {
            if (!BoosterTypes.IsRocket(orientation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orientation),
                    "RocketEffect needs RocketH or RocketV.");
            }

            _orientation = orientation;
        }

        public BoosterType Type => _orientation;

        public void Resolve(in BoosterActivation activation, IBoardReader board, CellBuffer hitCells)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (hitCells == null)
            {
                throw new ArgumentNullException(nameof(hitCells));
            }

            GridPos cell = activation.Cell;
            if (Orientation(in activation) == BoosterType.RocketH)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    BoosterCells.AddIfHittable(board, hitCells, new GridPos(x, cell.Y));
                }

                return;
            }

            for (int y = 0; y < board.Height; y++)
            {
                BoosterCells.AddIfHittable(board, hitCells, new GridPos(cell.X, y));
            }
        }

        /// <summary>The activation wins, so one effect instance can serve a combo of either axis.</summary>
        private BoosterType Orientation(in BoosterActivation activation)
            => BoosterTypes.IsRocket(activation.Booster) ? activation.Booster : _orientation;
    }
}
