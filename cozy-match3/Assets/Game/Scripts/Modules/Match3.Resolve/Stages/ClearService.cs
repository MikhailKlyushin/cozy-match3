using System;
using Match3.Board;
using Match3.Core;
using Match3.Goals;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Stage CLEAR (GDD §5.1 stage 6): destroys the cleared chips, removes the obstacles left at
    /// 0 hp, reveals nested content with immunity until the end of the step (§7.1) and reports
    /// goal progress. A freed obstacle cell is passable for falling in the same step (E10).
    /// </summary>
    public sealed class ClearService
    {
        private readonly BoardModel _board;
        private readonly IGoalTracker _goals;
        private readonly IMatch3Logger _logger;

        public ClearService(BoardModel board, IGoalTracker goals, IMatch3Logger logger)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _goals = goals ?? throw new ArgumentNullException(nameof(goals));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// <paramref name="deltas"/> is the caller's buffer and is appended, never cleared; only
        /// the deltas produced here are reported as GoalProgress.
        /// </summary>
        public void Clear(int step, CellBuffer chipCells, TranscriptWriter writer, PooledList<GoalDelta> deltas)
        {
            if (chipCells == null)
            {
                throw new ArgumentNullException(nameof(chipCells));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (deltas == null)
            {
                throw new ArgumentNullException(nameof(deltas));
            }

            int deltaStart = deltas.Count;
            ClearChips(chipCells, writer, deltas);
            ClearElements(step, writer, deltas);
            WriteGoalProgress(deltaStart, writer, deltas);
        }

        private static void WriteGoalProgress(int deltaStart, TranscriptWriter writer, PooledList<GoalDelta> deltas)
        {
            for (int i = deltaStart; i < deltas.Count; i++)
            {
                ref GoalDelta delta = ref deltas[i];

                // A zero delta means the goal was already closed and the credit was dropped
                // (E18); an event saying "plus nothing" is noise in the transcript.
                if (delta.Delta == 0)
                {
                    continue;
                }

                writer.GoalProgress(delta.GoalIndex, delta.Delta, delta.NewValue);
            }
        }

        private void ClearChips(CellBuffer chipCells, TranscriptWriter writer, PooledList<GoalDelta> deltas)
        {
            for (int i = 0; i < chipCells.Count; i++)
            {
                GridPos cell = chipCells[i];
                if (!_board.Contains(cell))
                {
                    _logger.Error("ClearService: a cell outside the board reached CLEAR.");
                    continue;
                }

                ChipSlot slot = _board.GetSlot(cell);
                if (slot.IsEmpty)
                {
                    continue;
                }

                writer.ChipDestroyed(cell, slot.Color, slot.InstanceId);

                // A cleared booster is neither collected nor an activation (Q1, §8.1).
                if (slot.IsChip)
                {
                    _goals.CreditChip(slot.Color, deltas);
                }

                _board.ClearSlot(cell);
            }
        }

        private void ClearElements(int step, TranscriptWriter writer, PooledList<GoalDelta> deltas)
        {
            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);

                    // IsAlive covers infinite health too: -1 hp is not 0 hp and is never destroyed.
                    if (!_board.TryGetElement(cell, out ElementInstance element) || element.IsAlive)
                    {
                        continue;
                    }

                    ElementDefinition definition = _board.Catalog.Get(element.Definition);
                    writer.ElementDestroyed(cell, element.Definition);
                    _goals.CreditElement(definition, deltas);

                    int nested = element.NestedIndex;
                    _board.DetachElement(cell);
                    if (nested == ElementInstance.NoNested)
                    {
                        continue;
                    }

                    // Without the immunity a single bomb unwraps the whole chain in one step and
                    // nesting stops being a mechanic (§7.1).
                    ElementInstance revealed = _board.ElementAt(nested);
                    revealed.ImmuneUntilStep = step;
                    _board.AttachElement(cell, nested);
                    writer.ElementRevealed(cell, revealed.Definition);
                }
            }
        }
    }
}
