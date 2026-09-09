using Match3.Board;
using Match3.Core;

namespace Match3.Boosters
{
    public interface IBoosterEffect
    {
        BoosterType Type { get; }

        /// <summary>
        /// Cells hit by one activation (§6.2). Appends to <paramref name="hitCells"/> without
        /// clearing it; a cell covered twice by the same activation stays one entry.
        /// </summary>
        void Resolve(in BoosterActivation activation, IBoardReader board, CellBuffer hitCells);
    }

    /// <summary>Shared geometry: what a booster may add to its hit set.</summary>
    internal static class BoosterCells
    {
        /// <summary>Off-board cells and holes are never hit; live obstacles are (D11).</summary>
        internal static void AddIfHittable(IBoardReader board, CellBuffer cells, GridPos p)
        {
            if (board.Contains(p) && board.GetKind(p) == CellKind.Playable)
            {
                cells.AddUnique(p);
            }
        }

        /// <summary>Cell plus its four orthogonal neighbours, appended y-up then x-up (§6.2).</summary>
        internal static void AddNeighbourhood(IBoardReader board, CellBuffer cells, GridPos centre)
        {
            AddIfHittable(board, cells, centre.Down);
            AddIfHittable(board, cells, centre.Left);
            AddIfHittable(board, cells, centre);
            AddIfHittable(board, cells, centre.Right);
            AddIfHittable(board, cells, centre.Up);
        }
    }
}
