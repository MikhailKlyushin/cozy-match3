using System;
using Match3.Board;
using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Stage ACTIVATE (GDD §5.1 stage 4, §6.4). Works in waves: collect the list first, then
    /// fire. Activating mid-traversal would mutate the board while iterating it, which §6.4
    /// forbids outright.
    /// </summary>
    public sealed class ActivationService
    {
        private readonly BoardModel _board;
        private readonly BoosterCatalog _catalog;
        private readonly IGoalTracker _goals;
        private readonly IMatch3Logger _logger;

        public ActivationService(
            BoardModel board,
            BoosterCatalog catalog,
            IGoalTracker goals,
            IMatch3Logger logger)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _goals = goals ?? throw new ArgumentNullException(nameof(goals));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Queues the player's own activation: a tap, or a booster landing in the swap target
        /// cell (D03). <paramref name="colorHint"/> is the swapped chip's colour for a rainbow.
        /// </summary>
        public void QueuePlayerActivation(ResolveContext ctx, GridPos cell, ChipColor colorHint)
        {
            ChipSlot slot = _board.GetSlot(cell);
            if (slot.Kind != SlotKind.Booster)
            {
                return;
            }

            ctx.Wave.Add(new BoosterActivation(cell, slot.Booster, colorHint));
        }

        /// <summary>
        /// Fires wave 0 (the player's activation or the combination plan), then every booster
        /// caught in the blast (E04), until nothing is left or the wave cap trips. Returns the
        /// number of waves played.
        /// </summary>
        public int Run(
            ResolveContext ctx,
            ComboPlan plan,
            GridPos comboSourceCell,
            BoosterActivationSource firstWaveSource,
            TranscriptWriter writer)
        {
            int wave = 0;

            if (plan != null)
            {
                writer.CurrentWave = 0;
                ctx.WaveHitCells.Clear();
                ExecutePlan(ctx, plan, comboSourceCell, writer);
                CollectNextWave(ctx);
                writer.WaveBarrier(0);
                ctx.AdvanceWave();
                wave = 1;
            }

            while (ctx.Wave.Count > 0)
            {
                if (wave >= ResolveCaps.MaxWaves)
                {
                    writer.CapHit(CapKind.WaveCap);
                    _logger.Error("Wave cap reached: a booster chain did not terminate (E05)");
                    break;
                }

                writer.CurrentWave = (byte)wave;
                ctx.WaveHitCells.Clear();

                BoosterActivationSource source = wave == 0
                    ? firstWaveSource
                    : BoosterActivationSource.Chain;

                for (int i = 0; i < ctx.Wave.Count; i++)
                {
                    FireFromBoard(ctx, ctx.Wave[i], source, credit: true, writer);
                }

                CollectNextWave(ctx);
                writer.WaveBarrier(wave);
                wave++;
                ctx.AdvanceWave();
            }

            writer.CurrentWave = 0;
            return wave;
        }

        private void ExecutePlan(ResolveContext ctx, ComboPlan plan, GridPos sourceCell, TranscriptWriter writer)
        {
            writer.ComboActivated(plan.Epicentre, sourceCell, plan.A, plan.B);

            // §6.1: a combination counts as ONE activation of each participant. The
            // sub-activations it spawns do not credit again, otherwise one rainbow combination
            // would close a booster goal outright.
            CreditActivation(ctx, plan.A, writer);
            CreditActivation(ctx, plan.B, writer);

            ConsumeParticipant(ctx, plan.Epicentre);
            ConsumeParticipant(ctx, sourceCell);

            for (int i = 0; i < plan.DirectCells.Count; i++)
            {
                ctx.ChipCells.AddUnique(plan.DirectCells[i]);
            }

            if (plan.DamagesEveryObstacle)
            {
                // Rainbow plus rainbow: one source over the whole board, so every live obstacle
                // takes exactly 1 damage (§6.3, D07).
                AddWholeBoardDamage(ctx, plan.Epicentre, writer);
            }
            else if (plan.DirectCells.Count > 0)
            {
                // Cells the plan destroys without a sub-activation still need a damage source,
                // or the obstacles inside a 7x7 bomb blast would take none.
                AddDirectDamage(ctx, plan, writer);
            }

            int delayMilliseconds = 0;
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                ComboStep step = plan.Steps[i];
                delayMilliseconds += (int)(step.Delay * 1000f);

                if (step.TransformFirst)
                {
                    PlaceComboBooster(step, delayMilliseconds, writer);
                    FireFromBoard(
                        ctx,
                        new BoosterActivation(step.Cell, step.Booster, ChipColor.None),
                        BoosterActivationSource.Combo,
                        credit: false,
                        writer);
                    continue;
                }

                // Geometry-only sub-activation: the booster never exists on the board, so it is
                // resolved directly and leaves the slot alone.
                FireVirtual(ctx, step.Cell, step.Booster, writer);
            }
        }

        /// <summary>
        /// Puts a combination's booster into the cell. A chip there morphs and keeps its identity
        /// so the view can animate the transformation; an empty cell gets a fresh booster.
        /// </summary>
        private void PlaceComboBooster(in ComboStep step, int delayMilliseconds, TranscriptWriter writer)
        {
            ChipSlot slot = _board.GetSlot(step.Cell);

            if (slot.Kind == SlotKind.Empty)
            {
                int instanceId = _board.SetBooster(step.Cell, step.Booster);
                writer.BoosterSpawned(step.Cell, step.Booster, instanceId);
                _board.SetConsumed(step.Cell, false);
                return;
            }

            _board.TransformToBooster(step.Cell, step.Booster);
            _board.SetConsumed(step.Cell, false);
            writer.ChipTransformed(step.Cell, step.Booster, slot.InstanceId, delayMilliseconds);
        }

        private void FireFromBoard(
            ResolveContext ctx,
            in BoosterActivation activation,
            BoosterActivationSource source,
            bool credit,
            TranscriptWriter writer)
        {
            GridPos cell = activation.Cell;
            ChipSlot slot = _board.GetSlot(cell);

            // D08: a consumed booster never fires twice in a step, which is what stops two
            // rockets from triggering each other forever.
            if (slot.Kind != SlotKind.Booster || slot.Consumed)
            {
                return;
            }

            _board.SetConsumed(cell, true);
            ctx.ChipCells.AddUnique(cell);

            Fire(ctx, cell, slot.Booster, activation.ColorHint, source, credit, writer);
        }

        /// <summary>A sub-activation with no booster on the board: the cross rockets of §6.3.</summary>
        private void FireVirtual(ResolveContext ctx, GridPos cell, BoosterType booster, TranscriptWriter writer)
            => Fire(ctx, cell, booster, ChipColor.None, BoosterActivationSource.Combo, credit: false, writer);

        private void Fire(
            ResolveContext ctx,
            GridPos cell,
            BoosterType booster,
            ChipColor colorHint,
            BoosterActivationSource source,
            bool credit,
            TranscriptWriter writer)
        {
            CellBuffer hitCells = ctx.RentHitBuffer();
            var activation = new BoosterActivation(cell, booster, colorHint);
            _catalog.Get(booster).Resolve(activation, _board, hitCells);

            ctx.Hits.Add(new BoosterHit(ctx.TakeSourceId(), cell, booster, hitCells));

            writer.BoosterActivated(cell, booster, source);
            writer.BoosterEffectCells(cell, booster, hitCells);

            if (credit)
            {
                CreditActivation(ctx, booster, writer);
            }

            for (int i = 0; i < hitCells.Count; i++)
            {
                GridPos hit = hitCells[i];
                ctx.WaveHitCells.AddUnique(hit);
                ctx.ChipCells.AddUnique(hit);
            }
        }

        /// <summary>A booster inside the blast fires as a link of the chain, not as debris (E04).</summary>
        private void CollectNextWave(ResolveContext ctx)
        {
            ctx.NextWave.Clear();

            for (int i = 0; i < ctx.WaveHitCells.Count; i++)
            {
                GridPos cell = ctx.WaveHitCells[i];
                ChipSlot slot = _board.GetSlot(cell);
                if (slot.Kind == SlotKind.Booster && !slot.Consumed)
                {
                    ctx.NextWave.Add(new BoosterActivation(cell, slot.Booster, ChipColor.None));
                }
            }
        }

        private void AddWholeBoardDamage(ResolveContext ctx, GridPos origin, TranscriptWriter writer)
        {
            CellBuffer cells = ctx.RentHitBuffer();

            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (_board.GetKind(cell) == CellKind.Playable)
                    {
                        cells.AddUnique(cell);
                    }
                }
            }

            ctx.Hits.Add(new BoosterHit(ctx.TakeSourceId(), origin, BoosterType.Rainbow, cells));
            writer.BoosterEffectCells(origin, BoosterType.Rainbow, cells);
        }

        private void AddDirectDamage(ResolveContext ctx, ComboPlan plan, TranscriptWriter writer)
        {
            CellBuffer cells = ctx.RentHitBuffer();
            for (int i = 0; i < plan.DirectCells.Count; i++)
            {
                cells.AddUnique(plan.DirectCells[i]);
            }

            ctx.Hits.Add(new BoosterHit(ctx.TakeSourceId(), plan.Epicentre, plan.A, cells));
            writer.BoosterEffectCells(plan.Epicentre, plan.A, cells);
        }

        private void ConsumeParticipant(ResolveContext ctx, GridPos cell)
        {
            if (!_board.Contains(cell))
            {
                return;
            }

            if (_board.GetSlot(cell).Kind == SlotKind.Booster)
            {
                _board.SetConsumed(cell, true);
            }

            ctx.ChipCells.AddUnique(cell);
        }

        private void CreditActivation(ResolveContext ctx, BoosterType booster, TranscriptWriter writer)
        {
            if (booster == BoosterType.None)
            {
                return;
            }

            int from = ctx.GoalDeltas.Count;
            _goals.CreditBoosterActivation(booster, ctx.GoalDeltas);
            GoalProgressWriter.Write(ctx.GoalDeltas, from, writer);
        }
    }
}
