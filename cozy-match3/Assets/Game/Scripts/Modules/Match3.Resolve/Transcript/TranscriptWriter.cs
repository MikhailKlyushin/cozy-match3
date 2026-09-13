using System.Collections.Generic;
using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// The only way to write a <see cref="TurnTranscript"/>. Carries the current step and wave so
    /// callers cannot forget to stamp them, which is what keeps rules T1-T3 checkable.
    /// </summary>
    public sealed class TranscriptWriter
    {
        private readonly TurnTranscript _transcript;

        public TranscriptWriter(TurnTranscript transcript)
        {
            _transcript = transcript;
        }

        public TurnTranscript Transcript => _transcript;

        public byte CurrentStep { get; set; }

        public byte CurrentWave { get; set; }

        /// <summary>Resets the transcript and writes <see cref="TurnEventKind.TurnBegin"/>.</summary>
        public void TurnBegin(int seed, int movesLeft)
        {
            _transcript.Reset();
            _transcript.SetHeader(seed, movesLeft);
            CurrentStep = 0;
            CurrentWave = 0;
            Write(new TurnEvent(TurnEventKind.TurnBegin, value: movesLeft));
        }

        public void SwapRejected(GridPos a, GridPos b)
            => Write(new TurnEvent(TurnEventKind.SwapRejected, a: a, b: b));

        public void SwapPerformed(GridPos a, GridPos b)
            => Write(new TurnEvent(TurnEventKind.SwapPerformed, a: a, b: b));

        /// <summary>Must follow <see cref="SwapPerformed"/> immediately (D04, E15, rule T6).</summary>
        public void MoveCharged(int movesLeft)
        {
            _transcript.SetMovesLeft(movesLeft);
            Write(new TurnEvent(TurnEventKind.MoveCharged, value: movesLeft));
        }

        public void StepBegin(int step)
        {
            CurrentStep = (byte)step;
            CurrentWave = 0;
            _transcript.SetMaxDepth(step);
            Write(new TurnEvent(TurnEventKind.StepBegin));
        }

        public void StepEnd(int step, int depth)
        {
            CurrentStep = (byte)step;
            Write(new TurnEvent(TurnEventKind.StepEnd, value: depth));
        }

        /// <summary>
        /// <paramref name="instanceId"/> is the id Board.SetBooster allocated: without it the
        /// view cannot map the new booster to a visual, and every later ChipMoved for it names
        /// an id the view never registered.
        /// </summary>
        public void BoosterSpawned(GridPos cell, BoosterType booster, int instanceId)
            => Write(new TurnEvent(
                TurnEventKind.BoosterSpawned,
                a: cell,
                booster: booster,
                instanceId: instanceId));

        public void BoosterActivated(GridPos cell, BoosterType booster, BoosterActivationSource source)
            => Write(new TurnEvent(
                TurnEventKind.BoosterActivated,
                flags: (byte)source,
                a: cell,
                booster: booster));

        /// <summary>Epicentre is <paramref name="epicentre"/>, the swap target cell (§6.3).</summary>
        public void ComboActivated(GridPos epicentre, GridPos other, BoosterType a, BoosterType b)
            => Write(new TurnEvent(
                TurnEventKind.ComboActivated,
                a: epicentre,
                b: other,
                booster: a,
                value: (int)b));

        public void BoosterEffectCells(GridPos origin, BoosterType booster, CellBuffer cells)
        {
            int offset = _transcript.AppendCells(cells);
            Write(new TurnEvent(
                TurnEventKind.BoosterEffectCells,
                a: origin,
                booster: booster,
                cellsOffset: offset,
                cellsCount: cells.Count));
        }

        public void BoosterEffectCells(GridPos origin, BoosterType booster, IReadOnlyList<GridPos> cells)
        {
            int offset = _transcript.AppendCells(cells);
            Write(new TurnEvent(
                TurnEventKind.BoosterEffectCells,
                a: origin,
                booster: booster,
                cellsOffset: offset,
                cellsCount: cells.Count));
        }

        public void ChipDestroyed(GridPos cell, ChipColor color, int instanceId)
            => Write(new TurnEvent(TurnEventKind.ChipDestroyed, a: cell, color: color, instanceId: instanceId));

        /// <summary>
        /// <paramref name="delayMilliseconds"/> carries the ComboStep stagger (§6.3): mass
        /// transformations must read as a series, so the player offsets each one by it.
        /// </summary>
        public void ChipTransformed(GridPos cell, BoosterType booster, int instanceId, int delayMilliseconds = 0)
            => Write(new TurnEvent(
                TurnEventKind.ChipTransformed,
                a: cell,
                booster: booster,
                amount: delayMilliseconds,
                instanceId: instanceId));

        public void ElementDamaged(GridPos cell, ElementId element, int amount, int remainingHealth)
            => Write(new TurnEvent(
                TurnEventKind.ElementDamaged,
                a: cell,
                element: element,
                amount: amount,
                value: remainingHealth));

        public void ElementDestroyed(GridPos cell, ElementId element)
            => Write(new TurnEvent(TurnEventKind.ElementDestroyed, a: cell, element: element));

        public void ElementRevealed(GridPos cell, ElementId element)
            => Write(new TurnEvent(TurnEventKind.ElementRevealed, a: cell, element: element));

        public void ElementColorCycled(GridPos cell, ChipColor color)
            => Write(new TurnEvent(TurnEventKind.ElementColorCycled, a: cell, color: color));

        /// <summary><paramref name="newValue"/> is already clamped at the target (E18, rule T4).</summary>
        public void GoalProgress(int goalIndex, int delta, int newValue)
            => Write(new TurnEvent(
                TurnEventKind.GoalProgress,
                amount: delta,
                value: newValue,
                instanceId: goalIndex));

        /// <summary>Between CLEAR and GRAVITY of every step (rule T1).</summary>
        public void AnimateBarrier()
            => Write(new TurnEvent(TurnEventKind.AnimateBarrier));

        public void ChipMoved(GridPos from, GridPos to, ChipMoveFlags flags, int instanceId)
            => Write(new TurnEvent(
                TurnEventKind.ChipMoved,
                flags: (byte)flags,
                a: from,
                b: to,
                instanceId: instanceId));

        public void ChipSpawned(GridPos cell, ChipColor color, int instanceId)
            => Write(new TurnEvent(TurnEventKind.ChipSpawned, a: cell, color: color, instanceId: instanceId));

        /// <summary>Closes an ACTIVATE wave (§6.4, rule T3).</summary>
        public void WaveBarrier(int wave)
        {
            CurrentWave = (byte)wave;
            Write(new TurnEvent(TurnEventKind.WaveBarrier));
        }

        public void PostTurnBegin()
            => Write(new TurnEvent(TurnEventKind.PostTurnBegin));

        public void ShuffleBegin()
            => Write(new TurnEvent(TurnEventKind.ShuffleBegin));

        public void ShuffleEnd()
            => Write(new TurnEvent(TurnEventKind.ShuffleEnd));

        public void MovesBonusRocket(GridPos cell, BoosterType booster, int instanceId)
            => Write(new TurnEvent(
                TurnEventKind.MovesBonusRocket,
                a: cell,
                booster: booster,
                instanceId: instanceId));

        public void LevelWon(LevelEndReason reason)
        {
            _transcript.SetOutcome(TurnOutcome.Won);
            Write(new TurnEvent(TurnEventKind.LevelWon, flags: (byte)reason));
        }

        public void LevelLost(LevelEndReason reason)
        {
            _transcript.SetOutcome(TurnOutcome.Lost);
            Write(new TurnEvent(TurnEventKind.LevelLost, flags: (byte)reason));
        }

        public void CapHit(CapKind cap)
            => Write(new TurnEvent(TurnEventKind.CapHit, flags: (byte)cap));

        public void TurnEnd()
            => Write(new TurnEvent(TurnEventKind.TurnEnd));

        public void SetOutcome(TurnOutcome outcome) => _transcript.SetOutcome(outcome);

        public void SetMovesLeft(int movesLeft) => _transcript.SetMovesLeft(movesLeft);

        private void Write(in TurnEvent e)
        {
            _transcript.AddEvent(new TurnEvent(
                e.Kind,
                CurrentStep,
                CurrentWave,
                e.Flags,
                e.A,
                e.B,
                e.Color,
                e.Booster,
                e.Element,
                e.Amount,
                e.Value,
                e.InstanceId,
                e.CellsOffset,
                e.CellsCount));
        }
    }
}
