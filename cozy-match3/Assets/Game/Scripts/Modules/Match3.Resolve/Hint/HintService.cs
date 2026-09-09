using System;
using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Idle hint (GDD §5.5, D15). Deterministic and RNG-free so it can be covered by a test, and
    /// it never suggests spending a booster-plus-booster combination unless nothing else exists.
    /// Candidates come from the cached legal-move list, so there is no second board pass.
    /// </summary>
    public sealed class HintService
    {
        private readonly BoardModel _board;
        private readonly LegalMoveService _legalMoves;
        private readonly MatchDetectionService _detection;
        private readonly IGoalTracker _goals;
        private readonly LevelRules _rules;

        private readonly PooledList<MatchComponent> _components = new PooledList<MatchComponent>(8);
        private readonly GridPos[] _neighbours = new GridPos[4];

        private const int PriorityBooster = 1;
        private const int PriorityGoal = 2;
        private const int PriorityAny = 3;

        public HintService(
            BoardModel board,
            LegalMoveService legalMoves,
            MatchDetectionService detection,
            IGoalTracker goals,
            LevelRules rules)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _legalMoves = legalMoves ?? throw new ArgumentNullException(nameof(legalMoves));
            _detection = detection ?? throw new ArgumentNullException(nameof(detection));
            _goals = goals ?? throw new ArgumentNullException(nameof(goals));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>
        /// <see cref="HintPlan.None"/> when hints are disabled for the level or no move exists.
        /// A missing hint never triggers a shuffle: that is POST-TURN's job (§5.5).
        /// </summary>
        public HintPlan GetHint()
        {
            if (_rules.HintDelaySeconds <= 0f)
            {
                return HintPlan.None;
            }

            System.Collections.Generic.IReadOnlyList<LegalMove> moves = _legalMoves.Moves;
            if (moves.Count == 0)
            {
                // Priority 4: no legal swap at all, so the only advice left is a booster tap.
                return FindTappableBooster();
            }

            HintPlan best = HintPlan.None;
            ComponentRank bestRank = ComponentRank.None;
            HintPlan firstGoalMove = HintPlan.None;
            HintPlan firstAnyMove = HintPlan.None;
            HintPlan firstComboMove = HintPlan.None;

            int firstUnclosed = _goals.FirstUnclosedIndex;

            for (int i = 0; i < moves.Count; i++)
            {
                LegalMove move = moves[i];

                // Spending an accumulated combination on an idle timer is a bad suggestion, so it
                // is kept aside and only used when nothing else is available (D15).
                if (IsBoosterPair(move))
                {
                    if (!firstComboMove.HasHint)
                    {
                        firstComboMove = HintPlan.Swap(move.A, move.B, PriorityAny);
                    }

                    continue;
                }

                if (!firstAnyMove.HasHint)
                {
                    firstAnyMove = HintPlan.Swap(move.A, move.B, PriorityAny);
                }

                Probe(move, out ComponentRank rank, out bool advancesGoal, firstUnclosed);

                // Priority 1: the highest rank wins, and ranks are ordered 1 = best.
                if (rank != ComponentRank.None && (bestRank == ComponentRank.None || rank < bestRank))
                {
                    bestRank = rank;
                    best = HintPlan.Swap(move.A, move.B, PriorityBooster);
                }

                if (advancesGoal && !firstGoalMove.HasHint)
                {
                    firstGoalMove = HintPlan.Swap(move.A, move.B, PriorityGoal);
                }
            }

            if (best.HasHint)
            {
                return best;
            }

            if (firstGoalMove.HasHint)
            {
                return firstGoalMove;
            }

            if (firstAnyMove.HasHint)
            {
                return firstAnyMove;
            }

            if (firstComboMove.HasHint)
            {
                return firstComboMove;
            }

            return FindTappableBooster();
        }

        private bool IsBoosterPair(in LegalMove move)
            => _board.GetSlot(move.A).Kind == SlotKind.Booster
               && _board.GetSlot(move.B).Kind == SlotKind.Booster;

        /// <summary>
        /// Swaps, inspects, swaps back. The board is left exactly as it was, which the tests
        /// assert through the board hash.
        /// </summary>
        private void Probe(in LegalMove move, out ComponentRank rank, out bool advancesGoal, int firstUnclosed)
        {
            rank = ComponentRank.None;
            advancesGoal = false;

            _board.SwapSlots(move.A, move.B);

            _detection.Detect(_components, move.B);
            for (int i = 0; i < _components.Count; i++)
            {
                MatchComponent component = _components[i];

                if (component.Rank != ComponentRank.None
                    && (rank == ComponentRank.None || component.Rank < rank))
                {
                    rank = component.Rank;
                }

                if (!advancesGoal && firstUnclosed >= 0 && AdvancesGoal(component, firstUnclosed))
                {
                    advancesGoal = true;
                }
            }

            _board.SwapSlots(move.A, move.B);
        }

        /// <summary>
        /// Whether clearing this component moves the FIRST unclosed goal, which is the same goal
        /// booster targeting aims at (§6.1) - player and auto-aim look the same way (§5.5).
        /// </summary>
        private bool AdvancesGoal(MatchComponent component, int goalIndex)
        {
            GoalDefinition goal = _goals.GetDefinition(goalIndex);

            switch (goal.Type)
            {
                case GoalType.CollectColor:
                    return component.Color == goal.Color;

                case GoalType.DestroyElement:
                case GoalType.DestroyAnyColoredBox:
                    return TouchesDamageableElement(component, goal);

                case GoalType.ActivateBooster:
                    return component.Booster != BoosterType.None
                           && (component.Booster == goal.Booster
                               || (BoosterTypes.IsRocket(goal.Booster) && BoosterTypes.IsRocket(component.Booster)));

                default:
                    return false;
            }
        }

        private bool TouchesDamageableElement(MatchComponent component, in GoalDefinition goal)
        {
            for (int i = 0; i < component.Cells.Count; i++)
            {
                int count = _board.GetOrthogonalNeighbours(component.Cells[i], _neighbours);
                for (int n = 0; n < count; n++)
                {
                    if (!_board.TryGetElement(_neighbours[n], out ElementInstance element) || !element.IsAlive)
                    {
                        continue;
                    }

                    ElementDefinition definition = _board.Catalog.Get(element.Definition);
                    if (definition.GoalRole == GoalRole.NotCountable)
                    {
                        continue;
                    }

                    bool matchesGoal = goal.Type == GoalType.DestroyAnyColoredBox
                        ? definition.IsColoredBox
                        : string.Equals(definition.Token, goal.Token, StringComparison.Ordinal);

                    if (matchesGoal)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Priority 4: no legal swap at all, so point at a booster to tap (§5.5).</summary>
        private HintPlan FindTappableBooster()
        {
            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (_board.GetSlot(cell).Kind == SlotKind.Booster && _board.IsMovable(cell))
                    {
                        return HintPlan.TapBooster(cell);
                    }
                }
            }

            return HintPlan.None;
        }
    }
}
