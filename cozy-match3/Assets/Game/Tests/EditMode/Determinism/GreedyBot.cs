using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Bootstrap;
using Match3.Core;
using Match3.Goals;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Determinism
{
    /// <summary>
    /// Level bot of T26: advance the first unclosed goal, else build the highest-rank booster,
    /// else any legal swap, and spend a booster as soon as one is on the board. It deliberately
    /// does not call HintService: the hint ranks booster creation first because it is teaching
    /// (§5.5, D15), while the bot plays to close goals.
    /// </summary>
    internal sealed class GreedyBot
    {
        private readonly LevelRuntime _runtime;
        private readonly BoardModel _board;
        private readonly PooledList<MatchComponent> _components = new PooledList<MatchComponent>(8);
        private readonly GridPos[] _neighbours = new GridPos[4];

        public GreedyBot(LevelRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _board = runtime.Board;
        }

        public bool TryChooseMove(out ReplayInput move)
        {
            IReadOnlyList<LegalMove> moves = _runtime.LegalMoves.Moves;
            int firstUnclosed = _runtime.Goals.FirstUnclosedIndex;

            LegalMove goalMove = default;
            ComponentRank goalRank = ComponentRank.None;
            bool hasGoalMove = false;

            LegalMove rankMove = default;
            ComponentRank bestRank = ComponentRank.None;

            LegalMove anyMove = default;
            bool hasAnyMove = false;

            for (int i = 0; i < moves.Count; i++)
            {
                LegalMove candidate = moves[i];

                if (!hasAnyMove)
                {
                    anyMove = candidate;
                    hasAnyMove = true;
                }

                Probe(candidate, firstUnclosed, out ComponentRank rank, out bool advancesGoal);

                // Among the moves that serve the goal, take the one that also builds the best
                // booster: same priority, free upside.
                if (advancesGoal && (!hasGoalMove || IsBetter(rank, goalRank)))
                {
                    goalMove = candidate;
                    goalRank = rank;
                    hasGoalMove = true;
                }

                if (IsBetter(rank, bestRank))
                {
                    bestRank = rank;
                    rankMove = candidate;
                }
            }

            if (hasGoalMove)
            {
                move = ReplayInput.Swap(goalMove.A, goalMove.B);
                return true;
            }

            if (TryFindBooster(out GridPos booster))
            {
                move = ReplayInput.Tap(booster);
                return true;
            }

            if (bestRank != ComponentRank.None)
            {
                move = ReplayInput.Swap(rankMove.A, rankMove.B);
                return true;
            }

            if (hasAnyMove)
            {
                move = ReplayInput.Swap(anyMove.A, anyMove.B);
                return true;
            }

            move = default;
            return false;
        }

        /// <summary>Ranks are ordered 1 = best, and None means the move builds nothing (§4.2).</summary>
        private static bool IsBetter(ComponentRank candidate, ComponentRank current)
            => candidate != ComponentRank.None && (current == ComponentRank.None || candidate < current);

        /// <summary>Swaps, looks, swaps back; the board is left byte-identical.</summary>
        private void Probe(in LegalMove move, int firstUnclosed, out ComponentRank rank, out bool advancesGoal)
        {
            rank = ComponentRank.None;
            advancesGoal = false;

            _board.SwapSlots(move.A, move.B);

            _runtime.Detection.Detect(_components, move.B);

            for (int i = 0; i < _components.Count; i++)
            {
                MatchComponent component = _components[i];

                if (IsBetter(component.Rank, rank))
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

        private bool AdvancesGoal(MatchComponent component, int goalIndex)
        {
            GoalDefinition goal = _runtime.Goals.GetDefinition(goalIndex);

            switch (goal.Type)
            {
                case GoalType.CollectColor:
                    return component.Color == goal.Color;

                case GoalType.DestroyElement:
                case GoalType.DestroyAnyColoredBox:
                    return TouchesGoalElement(component, goal);

                case GoalType.ActivateBooster:
                    // Building one is the only way to activate one later, so it counts here.
                    return component.Booster != BoosterType.None
                           && (component.Booster == goal.Booster
                               || (BoosterTypes.IsRocket(goal.Booster) && BoosterTypes.IsRocket(component.Booster)));

                default:
                    return false;
            }
        }

        private bool TouchesGoalElement(MatchComponent component, in GoalDefinition goal)
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

                    bool matches = goal.Type == GoalType.DestroyAnyColoredBox
                        ? definition.IsColoredBox
                        : string.Equals(definition.Token, goal.Token, StringComparison.Ordinal);

                    if (matches)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryFindBooster(out GridPos cell)
        {
            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var candidate = new GridPos(x, y);
                    if (_board.GetSlot(candidate).Kind == SlotKind.Booster && _board.IsMovable(candidate))
                    {
                        cell = candidate;
                        return true;
                    }
                }
            }

            cell = GridPos.Invalid;
            return false;
        }
    }
}
