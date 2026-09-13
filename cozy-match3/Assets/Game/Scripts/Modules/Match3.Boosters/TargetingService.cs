using System;
using Match3.Board;
using Match3.Core;
using Match3.Goals;

namespace Match3.Boosters
{
    /// <summary>
    /// GDD §6.1 targeting, the single implementation (D09, A07). Every candidate list is built by
    /// walking the board y-up then x-up, so both the tie-breaks and the RNG fallback are
    /// reproducible for a fixed seed (§13).
    /// </summary>
    public sealed class TargetingService : ITargetingService
    {
        private readonly IBoardReader _board;
        private readonly IGoalTracker _goals;
        private readonly IRandom _random;

        private readonly CellBuffer _excluded = new CellBuffer();
        private readonly PooledList<GridPos> _candidates = new PooledList<GridPos>(32);
        private readonly int[] _chipsByColor = new int[ChipColors.MaxColorCount + 1];

        public TargetingService(IBoardReader board, IGoalTracker goals, IRandom random)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _goals = goals ?? throw new ArgumentNullException(nameof(goals));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _excluded.Configure(board.Width, board.Height);
        }

        public GridPos PickGoalTarget()
        {
            _excluded.Clear();
            return PickBest();
        }

        /// <summary>Distinctness covers the cells added by this call, not what result already held.</summary>
        public int PickGoalTargets(int count, CellBuffer result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _excluded.Clear();

            int added = 0;
            for (int i = 0; i < count; i++)
            {
                GridPos target = PickBest();
                if (!target.IsValid)
                {
                    break;
                }

                result.Add(target);
                _excluded.Add(target);
                added++;
            }

            return added;
        }

        public ChipColor PickNeededColor()
        {
            for (int i = 0; i < _goals.Count; i++)
            {
                GoalDefinition goal = _goals.GetDefinition(i);
                if (goal.Type == GoalType.CollectColor && !_goals.GetState(i).Closed)
                {
                    return goal.Color;
                }
            }

            return MostCommonChipColor();
        }

        /// <summary>D07: one source deals 1 damage per step, so only a 1-hp obstacle scores this turn.</summary>
        private static int UnitsFromOneHit(ElementInstance element) => element.Health == 1 ? 1 : 0;

        /// <summary>
        /// Unclosed goals in config order, never sorted (§8.2). ActivateBooster is served by an
        /// activation and by no cell, so it always falls through to the next unclosed goal, and to
        /// the RNG fallback when there is none.
        /// </summary>
        private GridPos PickBest()
        {
            for (int index = _goals.FirstUnclosedIndex; index >= 0 && index < _goals.Count; index++)
            {
                if (_goals.GetState(index).Closed)
                {
                    continue;
                }

                GoalDefinition goal = _goals.GetDefinition(index);
                GridPos target = PickForGoal(in goal);
                if (target.IsValid)
                {
                    return target;
                }
            }

            return PickRandomChipCell();
        }

        private GridPos PickForGoal(in GoalDefinition goal)
        {
            GridPos best = GridPos.Invalid;
            int bestUnits = -1;

            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (_excluded.Contains(cell) || !TryGetUnits(in goal, cell, out int units))
                    {
                        continue;
                    }

                    // The y-up, x-up walk plus a strict comparison is step 3: of the equally
                    // useful cells the lowest y, then the lowest x, is kept.
                    if (units > bestUnits)
                    {
                        best = cell;
                        bestUnits = units;
                    }
                }
            }

            return best;
        }

        /// <summary>Goal units gained by destroying the cell; false when it does not serve the goal.</summary>
        private bool TryGetUnits(in GoalDefinition goal, GridPos cell, out int units)
        {
            units = 0;

            switch (goal.Type)
            {
                case GoalType.CollectColor:
                    ChipSlot slot = _board.GetSlot(cell);
                    if (!slot.IsChip || slot.Color != goal.Color)
                    {
                        return false;
                    }

                    units = 1;
                    return true;

                case GoalType.DestroyElement:
                    if (!TryGetCountableElement(cell, out ElementInstance element, out ElementDefinition definition)
                        || !string.Equals(definition.Token, goal.Token, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    units = UnitsFromOneHit(element);
                    return true;

                case GoalType.DestroyAnyColoredBox:
                    if (!TryGetCountableElement(cell, out ElementInstance box, out ElementDefinition boxDefinition)
                        || !boxDefinition.IsColoredBox)
                    {
                        return false;
                    }

                    units = UnitsFromOneHit(box);
                    return true;

                default:
                    return false;
            }
        }

        private bool TryGetCountableElement(
            GridPos cell,
            out ElementInstance element,
            out ElementDefinition definition)
        {
            definition = null;
            if (!_board.TryGetElement(cell, out element) || !element.IsAlive)
            {
                return false;
            }

            definition = _board.Catalog.Get(element.Definition);
            return definition.GoalRole == GoalRole.Countable;
        }

        /// <summary>Step 4: candidates collected y-up then x-up, then one index from the RNG.</summary>
        private GridPos PickRandomChipCell()
        {
            _candidates.Clear();

            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (!_excluded.Contains(cell) && _board.GetSlot(cell).IsChip)
                    {
                        _candidates.Add(cell);
                    }
                }
            }

            if (_candidates.Count == 0)
            {
                return GridPos.Invalid;
            }

            return _candidates[_random.NextInt(_candidates.Count)];
        }

        private ChipColor MostCommonChipColor()
        {
            Array.Clear(_chipsByColor, 0, _chipsByColor.Length);

            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    ChipSlot slot = _board.GetSlot(new GridPos(x, y));
                    if (!slot.IsChip)
                    {
                        continue;
                    }

                    int index = ChipColors.ToIndex(slot.Color);
                    if (index > 0 && index < _chipsByColor.Length)
                    {
                        _chipsByColor[index]++;
                    }
                }
            }

            ChipColor best = ChipColor.None;
            int bestCount = 0;

            // Ascending index with a strict comparison: a tie goes to the lowest colour index.
            for (int index = 1; index < _chipsByColor.Length; index++)
            {
                if (_chipsByColor[index] > bestCount)
                {
                    bestCount = _chipsByColor[index];
                    best = ChipColors.FromIndex(index);
                }
            }

            return best;
        }
    }
}
