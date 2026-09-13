using System;
using Match3.Board;
using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>
    /// GDD §6.2: flies to the cell chosen by <see cref="ITargetingService.PickGoalTarget"/> and
    /// destroys it together with its four orthogonal neighbours.
    /// </summary>
    public sealed class AirplaneEffect : IBoosterEffect
    {
        private readonly ITargetingService _targeting;

        public AirplaneEffect(ITargetingService targeting)
        {
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        }

        public BoosterType Type => BoosterType.Airplane;

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

            GridPos target = _targeting.PickGoalTarget();
            if (!target.IsValid)
            {
                return;
            }

            BoosterCells.AddNeighbourhood(board, hitCells, target);
        }
    }
}
