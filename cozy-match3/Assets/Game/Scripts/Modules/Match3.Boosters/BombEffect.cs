using System;
using Match3.Board;
using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>GDD §6.2: the 5x5 square centred on its own cell, clipped by the board edge.</summary>
    public sealed class BombEffect : IBoosterEffect
    {
        private const int Radius = 2;

        public BoosterType Type => BoosterType.Bomb;

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
            for (int y = cell.Y - Radius; y <= cell.Y + Radius; y++)
            {
                for (int x = cell.X - Radius; x <= cell.X + Radius; x++)
                {
                    BoosterCells.AddIfHittable(board, hitCells, new GridPos(x, y));
                }
            }
        }
    }
}
