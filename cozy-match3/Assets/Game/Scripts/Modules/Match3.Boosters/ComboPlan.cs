using System.Collections.Generic;
using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>
    /// Ordered plan of one combination (§6.3). Stage ACTIVATE executes it in order and needs no
    /// knowledge of the matrix itself.
    /// </summary>
    public sealed class ComboPlan
    {
        private readonly ComboStep[] _steps;
        private readonly GridPos[] _directCells;

        internal ComboPlan(
            GridPos epicentre,
            BoosterType a,
            BoosterType b,
            ComboStep[] steps,
            GridPos[] directCells,
            bool damagesEveryObstacle)
        {
            Epicentre = epicentre;
            A = a;
            B = b;
            _steps = steps;
            _directCells = directCells;
            DamagesEveryObstacle = damagesEveryObstacle;
        }

        /// <summary>The swap target cell - where the second booster lay (§6.1).</summary>
        public GridPos Epicentre { get; }

        public BoosterType A { get; }

        public BoosterType B { get; }

        public IReadOnlyList<ComboStep> Steps => _steps;

        /// <summary>
        /// Cells the plan destroys with no sub-activation: the chips past the 8-airplane cap and
        /// the whole board in rainbow plus rainbow (§6.3).
        /// </summary>
        public IReadOnlyList<GridPos> DirectCells => _directCells;

        /// <summary>Rainbow plus rainbow: 1 damage to every live obstacle on the board (§6.3).</summary>
        public bool DamagesEveryObstacle { get; }
    }
}
