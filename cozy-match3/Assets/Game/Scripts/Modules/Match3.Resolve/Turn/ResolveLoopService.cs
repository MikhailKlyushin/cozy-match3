using System;
using Match3.Boosters;
using Match3.Core;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// The RESOLVE cycle of GDD §5.1: DETECT, CLASSIFY, SPAWN, ACTIVATE, DAMAGE, CLEAR,
    /// ANIMATE, GRAVITY, REFILL, STABLE? repeated until the board settles. The stage order is
    /// the transcript order (rule T1), which is what makes it checkable by a test.
    /// </summary>
    public sealed class ResolveLoopService
    {
        private readonly BoardModel _board;
        private readonly MatchDetectionService _detection;
        private readonly BoosterSpawnService _spawn;
        private readonly ActivationService _activation;
        private readonly DamageService _damage;
        private readonly ClearService _clear;
        private readonly GravityService _gravity;
        private readonly RefillService _refill;
        private readonly IMatch3Logger _logger;

        /// <summary>
        /// Holds the activation the turn queued before the loop started: BeginStep clears every
        /// context buffer, so the carry cannot live in one of them.
        /// </summary>
        private readonly PooledList<Boosters.BoosterActivation> _carry =
            new PooledList<Boosters.BoosterActivation>(4);

        public ResolveLoopService(
            BoardModel board,
            MatchDetectionService detection,
            BoosterSpawnService spawn,
            ActivationService activation,
            DamageService damage,
            ClearService clear,
            GravityService gravity,
            RefillService refill,
            IMatch3Logger logger)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _detection = detection ?? throw new ArgumentNullException(nameof(detection));
            _spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            _activation = activation ?? throw new ArgumentNullException(nameof(activation));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _clear = clear ?? throw new ArgumentNullException(nameof(clear));
            _gravity = gravity ?? throw new ArgumentNullException(nameof(gravity));
            _refill = refill ?? throw new ArgumentNullException(nameof(refill));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs the cascade until the board is stable. <paramref name="playerSwapTarget"/> is the
        /// cell the player swapped into, used by spawn-cell rule 1 in step 0 only and skipped in
        /// every cascade step (E03). Returns the deepest step index reached.
        /// </summary>
        public int Run(
            ResolveContext ctx,
            GridPos playerSwapTarget,
            ComboPlan plan,
            GridPos comboSourceCell,
            BoosterActivationSource firstWaveSource,
            TranscriptWriter writer)
        {
            int step = 0;
            int maxDepth = 0;

            while (true)
            {
                if (step >= ResolveCaps.MaxCascadeDepth)
                {
                    writer.CapHit(CapKind.DepthCap);
                    _logger.Error("Cascade depth cap reached: resolution did not stabilise (E05)");
                    break;
                }

                bool isFirstStep = step == 0;
                bool hasPendingActivations = ctx.Wave.Count > 0 || plan != null;

                // Preserve the queued player activation across the per-step reset.
                CarryPendingWave(ctx, isFirstStep);

                writer.StepBegin(step);
                _board.ResetConsumedFlags();
                _damage.BeginStep(step);

                // 1 DETECT and 2 CLASSIFY.
                _detection.Detect(ctx.Components, isFirstStep ? playerSwapTarget : GridPos.Invalid);

                if (ctx.Components.Count == 0 && !hasPendingActivations)
                {
                    writer.StepEnd(step, maxDepth);
                    break;
                }

                // 3 SPAWN: the booster is placed and marked, and does not fire this step (D06).
                _spawn.Spawn(ctx.Components, ctx.SpawnCells, writer);
                CollectMatchedChips(ctx);

                // 4 ACTIVATE, in waves (§6.4).
                _activation.Run(
                    ctx,
                    isFirstStep ? plan : null,
                    comboSourceCell,
                    isFirstStep ? firstWaveSource : BoosterActivationSource.Chain,
                    writer);

                // 5 DAMAGE: one instance per source per step (D07).
                ApplyDamage(ctx, writer);

                // 6 CLEAR.
                _clear.Clear(step, ctx.ChipCells, writer, ctx.GoalDeltas);

                // 7 ANIMATE: the barrier the client asked for - nothing falls until the
                // destruction FX finished.
                writer.AnimateBarrier();

                // 8 GRAVITY and 9 REFILL, alternating until no cell can be filled.
                Settle(writer);

                maxDepth = step;
                writer.StepEnd(step, step);

                // 10 STABLE? A new match means another step, which is where automatic matches
                // during the fall get resolved by the ordinary rules.
                step++;
                plan = null;

                if (!_detection.HasAnyMatch())
                {
                    break;
                }
            }

            return maxDepth;
        }

        /// <summary>Chips of every matched component clear, except the cell that took a booster.</summary>
        private void CollectMatchedChips(ResolveContext ctx)
        {
            for (int i = 0; i < ctx.Components.Count; i++)
            {
                MatchComponent component = ctx.Components[i];
                for (int c = 0; c < component.Cells.Count; c++)
                {
                    GridPos cell = component.Cells[c];
                    if (!ctx.SpawnCells.Contains(cell))
                    {
                        ctx.ChipCells.AddUnique(cell);
                    }
                }
            }
        }

        private void ApplyDamage(ResolveContext ctx, TranscriptWriter writer)
        {
            for (int i = 0; i < ctx.Components.Count; i++)
            {
                MatchComponent component = ctx.Components[i];
                _damage.ApplyMatchDamage(component.Color, component.Cells, component.Index, writer);
            }

            for (int i = 0; i < ctx.Hits.Count; i++)
            {
                BoosterHit hit = ctx.Hits[i];
                _damage.ApplyBoosterDamage(hit.Cells.Cells, hit.SourceId, writer);
            }
        }

        /// <summary>
        /// Gravity settles, then spawners release; a new chip has to fall too, so the pair repeats
        /// until a refill adds nothing (§5.1 stages 8-9).
        /// </summary>
        private void Settle(TranscriptWriter writer)
        {
            int guard = 0;

            while (true)
            {
                _gravity.RunUntilStable(writer);

                if (_refill.Run(writer) == 0)
                {
                    return;
                }

                if (++guard > ResolveCaps.MaxCascadeDepth)
                {
                    _logger.Error("Gravity and refill did not settle; aborting the fall loop");
                    return;
                }
            }
        }

        /// <summary>
        /// <see cref="ResolveContext.BeginStep"/> clears every buffer, including the wave the turn
        /// queued before the loop started, so the first step saves and restores it.
        /// </summary>
        private void CarryPendingWave(ResolveContext ctx, bool isFirstStep)
        {
            if (!isFirstStep)
            {
                ctx.BeginStep(ctx.Step + 1);
                return;
            }

            _carry.Clear();
            _carry.AddRange(ctx.Wave);
            ctx.BeginStep(0);
            ctx.Wave.AddRange(_carry);
            _carry.Clear();
        }
    }
}
