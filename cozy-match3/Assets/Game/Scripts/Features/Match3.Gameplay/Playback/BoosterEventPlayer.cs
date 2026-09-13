using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;
using UnityEngine;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Booster FX (§6.2, §11.3): the wind-up on BoosterActivated, the effect itself on
    /// BoosterEffectCells. That event's cell slice plus its origin and booster are everything the
    /// FX needs, so the board is never read (rule V1).
    /// </summary>
    public sealed class BoosterEventPlayer : ITurnEventPlayer
    {
        private readonly FxRegistry _fx;
        private readonly CameraShake _shake;
        private readonly ChipVisualProfile _chipProfile;

        private readonly List<UniTask> _beams = new List<UniTask>(96);

        private static readonly TurnEventKind[] _kinds =
        {
            TurnEventKind.BoosterSpawned,
            TurnEventKind.BoosterActivated,
            TurnEventKind.BoosterEffectCells,
            TurnEventKind.MovesBonusRocket
        };

        /// <summary>Beam and trail thickness, in cells.</summary>
        private const float BeamThickness = 0.34f;

        /// <summary>Size of a beam head, flare or burst before it expands, in cells.</summary>
        private const float HeadSize = 0.8f;

        /// <summary>The airplane destroys a cell plus its four neighbours: three cells across.</summary>
        private const float ImpactCells = 3f;

        /// <summary>Height of the airplane's Bezier arc, in cells.</summary>
        private const float ArcHeight = 1.5f;

        private const float Half = 0.5f;

        public BoosterEventPlayer(FxRegistry fx, CameraShake shake, ChipVisualProfile chipProfile)
        {
            _fx = fx ?? throw new ArgumentNullException(nameof(fx));
            _shake = shake ?? throw new ArgumentNullException(nameof(shake));
            _chipProfile = chipProfile ?? throw new ArgumentNullException(nameof(chipProfile));
        }

        public IReadOnlyList<TurnEventKind> Kinds => _kinds;

        public UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ref readonly TurnEvent e = ref context.Transcript.GetEvent(eventIndex);

            switch (e.Kind)
            {
                case TurnEventKind.BoosterSpawned:
                    return PlaySpawnAsync(
                        context, e.A, e.Booster, e.InstanceId, context.Timings.LandingSquashDuration, ct);
                case TurnEventKind.BoosterActivated:
                    return PlayWindUpAsync(context, e.A, e.Booster, ct);
                case TurnEventKind.BoosterEffectCells:
                    return PlayEffectAsync(context, e.A, e.Booster, e.CellsOffset, e.CellsCount, ct);
                case TurnEventKind.MovesBonusRocket:
                    // The rocket takes over the chip that stood there (§8.4), so it resolves the
                    // same way a spawned booster does instead of being FX only.
                    return PlaySpawnAsync(
                        context, e.A, e.Booster, e.InstanceId, context.Timings.MovesBonusStagger, ct);
                default:
                    return UniTask.CompletedTask;
            }
        }

        /// <summary>
        /// D06: the booster is marked, not fired. InstanceId is the new booster's identity, so a
        /// chip the view already knows morphs in place instead of being replaced.
        /// </summary>
        private UniTask PlaySpawnAsync(
            PlaybackContext context,
            GridPos cell,
            BoosterType booster,
            int instanceId,
            float popDuration,
            CancellationToken ct)
        {
            if (instanceId != 0)
            {
                if (context.Board.TryGetChip(instanceId, out ChipView chip))
                {
                    chip.MorphToBooster(booster);
                }
                else
                {
                    context.Board.SpawnBoosterAt(instanceId, booster, cell);
                }
            }

            return PlayPopAsync(context, cell, booster, popDuration, ct);
        }

        /// <summary>Flare where a booster appears; §11.3 gives it no duration of its own.</summary>
        private async UniTask PlayPopAsync(
            PlaybackContext context,
            GridPos cell,
            BoosterType booster,
            float duration,
            CancellationToken ct)
        {
            FxView view = _fx.Rent(FxFor(booster));

            try
            {
                if (view != null)
                {
                    float cellSize = context.Board.Layout.CellSize;
                    view.Prepare(context.Board.Layout.CellCenter(cell), CellSquare(cellSize, HeadSize), Color.white);
                    view.ScaleTo(context.Timings.BombWindUpScale, duration);
                    view.FadeOut(duration);
                }

                await Wait(duration, ct);
            }
            finally
            {
                _fx.Release(view);
            }
        }

        /// <summary>§11.3 wind-up: 0.10 s bomb, 0.15 s rainbow, 0.10 s airplane; a rocket has none.</summary>
        private async UniTask PlayWindUpAsync(
            PlaybackContext context,
            GridPos cell,
            BoosterType booster,
            CancellationToken ct)
        {
            float windUp = WindUpOf(context.Timings, booster);
            if (windUp <= 0f)
            {
                return;
            }

            FxView view = _fx.Rent(FxFor(booster));

            try
            {
                if (view != null)
                {
                    float cellSize = context.Board.Layout.CellSize;
                    view.Prepare(context.Board.Layout.CellCenter(cell), CellSquare(cellSize, HeadSize), Color.white);
                    view.ScaleTo(context.Timings.BombWindUpScale, windUp);
                }

                await Wait(windUp, ct);
            }
            finally
            {
                _fx.Release(view);
            }
        }

        private UniTask PlayEffectAsync(
            PlaybackContext context,
            GridPos origin,
            BoosterType booster,
            int offset,
            int count,
            CancellationToken ct)
        {
            if (count <= 0)
            {
                return UniTask.CompletedTask;
            }

            switch (booster)
            {
                case BoosterType.RocketH:
                case BoosterType.RocketV:
                    return PlayRocketAsync(context, origin, booster, offset, count, ct);
                case BoosterType.Bomb:
                    return PlayBombAsync(context, origin, booster, offset, count, ct);
                case BoosterType.Rainbow:
                    return PlayRainbowAsync(context, origin, booster, offset, count, ct);
                case BoosterType.Airplane:
                    return PlayAirplaneAsync(context, origin, booster, offset, count, ct);
                default:
                    return UniTask.CompletedTask;
            }
        }

        /// <summary>
        /// §11.3: 0.03 s per cell with a trail, plus a 0.05 s / 4 px shake. The beam splits at the
        /// origin and runs to the furthest hit cell on each side - obstacles do not stop it (D11).
        /// </summary>
        private async UniTask PlayRocketAsync(
            PlaybackContext context,
            GridPos origin,
            BoosterType booster,
            int offset,
            int count,
            CancellationToken ct)
        {
            bool horizontal = booster != BoosterType.RocketV;
            int originAxis = horizontal ? origin.X : origin.Y;
            int min = originAxis;
            int max = originAxis;

            for (int i = 0; i < count; i++)
            {
                GridPos cell = context.Transcript.GetCell(offset + i);
                int axis = horizontal ? cell.X : cell.Y;

                if (axis < min)
                {
                    min = axis;
                }

                if (axis > max)
                {
                    max = axis;
                }
            }

            GridPos negativeEnd = horizontal ? new GridPos(min, origin.Y) : new GridPos(origin.X, min);
            GridPos positiveEnd = horizontal ? new GridPos(max, origin.Y) : new GridPos(origin.X, max);

            _shake.Shake(
                context.Timings.RocketShakeDuration,
                context.Timings.RocketShakeAmplitude,
                horizontal ? Vector2.up : Vector2.right);

            await UniTask.WhenAll(
                PlayRocketArmAsync(context, booster, origin, negativeEnd, originAxis - min, ct),
                PlayRocketArmAsync(context, booster, origin, positiveEnd, max - originAxis, ct));
        }

        private async UniTask PlayRocketArmAsync(
            PlaybackContext context,
            BoosterType booster,
            GridPos origin,
            GridPos end,
            int cells,
            CancellationToken ct)
        {
            if (cells <= 0)
            {
                // The rocket sat on the board edge: this arm has no length to travel.
                return;
            }

            FxView head = _fx.Rent(FxFor(booster));
            FxView trail = _fx.Rent(BeamFxFor(booster));

            try
            {
                BoardLayout layout = context.Board.Layout;
                Vector2 from = layout.CellCenter(origin);
                Vector2 to = layout.CellCenter(end);
                float cellSize = layout.CellSize;
                float thickness = cellSize * BeamThickness;
                float travel = cells * context.Timings.RocketCellDuration;

                if (head != null)
                {
                    head.Prepare(from, CellSquare(cellSize, HeadSize), Color.white);
                    head.MoveTo(to, travel);
                }

                if (trail != null)
                {
                    trail.PrepareBeam(from, to, thickness, Color.white);
                    trail.ExtendBeam(from, to, thickness, travel);
                }

                await Wait(travel, ct);

                // The trail burns out one cell's worth of time behind the beam.
                float fade = context.Timings.RocketCellDuration;

                if (head != null)
                {
                    head.FadeOut(fade);
                }

                if (trail != null)
                {
                    trail.FadeOut(fade);
                }

                await Wait(fade, ct);
            }
            finally
            {
                _fx.Release(head);
                _fx.Release(trail);
            }
        }

        /// <summary>
        /// §11.3: flash, a shockwave over 0.25 s and a 0.12 s / 10 px shake. The wave is sized
        /// from the slice, so the 5x5 bomb and the 7x7 combo blast both fit (§6.3).
        /// </summary>
        private async UniTask PlayBombAsync(
            PlaybackContext context,
            GridPos origin,
            BoosterType booster,
            int offset,
            int count,
            CancellationToken ct)
        {
            TimingProfile timings = context.Timings;
            BoardLayout layout = context.Board.Layout;
            float cellSize = layout.CellSize;

            SliceExtent(context, offset, count, out int minX, out int maxX, out int minY, out int maxY);
            Vector2 blastCentre =
                (layout.CellCenter(new GridPos(minX, minY)) + layout.CellCenter(new GridPos(maxX, maxY))) * Half;
            var blastSize = new Vector2((maxX - minX + 1) * cellSize, (maxY - minY + 1) * cellSize);

            FxView flash = _fx.Rent(FxFor(booster));
            FxView wave = _fx.Rent(BurstFxFor(booster));

            try
            {
                if (flash != null)
                {
                    flash.Prepare(layout.CellCenter(origin), CellSquare(cellSize, HeadSize), Color.white);
                    flash.ScaleTo(timings.BombWindUpScale, timings.BombShockwave);
                    flash.FadeOut(timings.BombShockwave);
                }

                if (wave != null)
                {
                    wave.Prepare(blastCentre, CellSquare(cellSize, HeadSize), Color.white);
                    wave.SizeTo(blastSize, timings.BombShockwave);
                    wave.FadeOut(timings.BombShockwave);
                }

                _shake.Shake(timings.BombShakeDuration, timings.BombShakeAmplitude, Vector2.one);
                await Wait(timings.BombShockwave, ct);
            }
            finally
            {
                _fx.Release(flash);
                _fx.Release(wave);
            }
        }

        /// <summary>
        /// §11.3: beams leave with a 0.04 s stagger, each chip dying 0.10 s after its beam lands.
        /// A whole-board volley (rainbow + rainbow, §6.3) compresses the stagger the way the goal
        /// counter does, so the flourish stays inside one board crossing.
        /// </summary>
        private async UniTask PlayRainbowAsync(
            PlaybackContext context,
            GridPos origin,
            BoosterType booster,
            int offset,
            int count,
            CancellationToken ct)
        {
            BoardLayout layout = context.Board.Layout;
            Vector2 from = layout.CellCenter(origin);
            float stagger = BeamStagger(context, count);
            GameObject prefab = BeamFxFor(booster);

            _beams.Clear();

            for (int i = 0; i < count; i++)
            {
                GridPos cell = context.Transcript.GetCell(offset + i);
                _beams.Add(PlayRainbowBeamAsync(context, prefab, from, layout.CellCenter(cell), stagger * i, ct));
            }

            try
            {
                await UniTask.WhenAll(_beams);
            }
            finally
            {
                _beams.Clear();
            }
        }

        private async UniTask PlayRainbowBeamAsync(
            PlaybackContext context,
            GameObject prefab,
            Vector2 from,
            Vector2 to,
            float delay,
            CancellationToken ct)
        {
            await Wait(delay, ct);

            TimingProfile timings = context.Timings;
            float thickness = context.Board.Layout.CellSize * BeamThickness;
            FxView beam = _fx.Rent(prefab);

            try
            {
                if (beam != null)
                {
                    beam.PrepareBeam(from, to, thickness, Color.white);

                    // Flight equals the stagger, so the volley reads as one continuous stream.
                    beam.ExtendBeam(from, to, thickness, timings.RainbowBeamStagger);
                }

                await Wait(timings.RainbowBeamStagger, ct);

                if (beam != null)
                {
                    beam.FadeOut(timings.RainbowHitDelay);
                }

                await Wait(timings.RainbowHitDelay, ct);
            }
            finally
            {
                _fx.Release(beam);
            }
        }

        /// <summary>§11.3: 0.10 s wind-up (already played), 0.40 s Bezier flight, then the hit.</summary>
        private async UniTask PlayAirplaneAsync(
            PlaybackContext context,
            GridPos origin,
            BoosterType booster,
            int offset,
            int count,
            CancellationToken ct)
        {
            BoardLayout layout = context.Board.Layout;
            Vector2 from = layout.CellCenter(origin);
            Vector2 to = layout.CellCenter(ImpactCell(context, offset, count));
            float cellSize = layout.CellSize;

            FxView plane = _fx.Rent(FxFor(booster));

            try
            {
                if (plane != null)
                {
                    plane.Prepare(from, CellSquare(cellSize, HeadSize), Color.white);
                }

                await FlyAsync(plane, from, ControlPoint(from, to, cellSize), to, context.Timings.AirplaneFlight, ct);
            }
            finally
            {
                _fx.Release(plane);
            }

            await PlayImpactAsync(context, booster, to, cellSize, ct);
        }

        /// <summary>The hit itself: §11.3 names it without a duration, so it takes the squash beat.</summary>
        private async UniTask PlayImpactAsync(
            PlaybackContext context,
            BoosterType booster,
            Vector2 centre,
            float cellSize,
            CancellationToken ct)
        {
            float duration = context.Timings.LandingSquashDuration;
            FxView burst = _fx.Rent(ImpactFxFor(booster));

            try
            {
                if (burst != null)
                {
                    burst.Prepare(centre, CellSquare(cellSize, HeadSize), Color.white);
                    burst.SizeTo(CellSquare(cellSize, ImpactCells), duration);
                    burst.FadeOut(duration);
                }

                await Wait(duration, ct);
            }
            finally
            {
                _fx.Release(burst);
            }
        }

        /// <summary>Quadratic Bezier flight; the view faces its own velocity so the plane banks.</summary>
        private static async UniTask FlyAsync(
            FxView view,
            Vector2 from,
            Vector2 control,
            Vector2 to,
            float duration,
            CancellationToken ct)
        {
            if (view == null || duration <= 0f)
            {
                await Wait(duration, ct);
                return;
            }

            float elapsed = 0f;
            Vector2 previous = from;

            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                if (view == null)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                Vector2 position = Bezier(from, control, to, Mathf.Clamp01(elapsed / duration));
                view.SetPosition(position);

                Vector2 step = position - previous;
                if (step.sqrMagnitude > Mathf.Epsilon)
                {
                    view.SetRotation(Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg);
                }

                previous = position;
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float inverse = 1f - t;
            return (inverse * inverse * a) + (2f * inverse * t * b) + (t * t * c);
        }

        /// <summary>Arc perpendicular to the flight, so the plane curves instead of sliding.</summary>
        private static Vector2 ControlPoint(Vector2 from, Vector2 to, float cellSize)
        {
            Vector2 delta = to - from;
            Vector2 perpendicular = delta.sqrMagnitude > Mathf.Epsilon
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;

            return ((from + to) * Half) + (perpendicular * (cellSize * ArcHeight));
        }

        /// <summary>
        /// The airplane hits a cell plus its four orthogonal neighbours (§6.2), so the target is
        /// the cell adjacent to most of the others - which also survives clipping at the edge.
        /// </summary>
        private static GridPos ImpactCell(PlaybackContext context, int offset, int count)
        {
            TurnTranscript transcript = context.Transcript;
            GridPos best = transcript.GetCell(offset);
            int bestScore = -1;

            for (int i = 0; i < count; i++)
            {
                GridPos cell = transcript.GetCell(offset + i);
                int score = 0;

                for (int j = 0; j < count; j++)
                {
                    if (i != j && cell.IsOrthogonalNeighbourOf(transcript.GetCell(offset + j)))
                    {
                        score++;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = cell;
                }
            }

            return best;
        }

        private static void SliceExtent(
            PlaybackContext context,
            int offset,
            int count,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY)
        {
            GridPos first = context.Transcript.GetCell(offset);
            minX = first.X;
            maxX = first.X;
            minY = first.Y;
            maxY = first.Y;

            for (int i = 1; i < count; i++)
            {
                GridPos cell = context.Transcript.GetCell(offset + i);

                if (cell.X < minX)
                {
                    minX = cell.X;
                }

                if (cell.X > maxX)
                {
                    maxX = cell.X;
                }

                if (cell.Y < minY)
                {
                    minY = cell.Y;
                }

                if (cell.Y > maxY)
                {
                    maxY = cell.Y;
                }
            }
        }

        private static float BeamStagger(PlaybackContext context, int count)
        {
            float stagger = context.Timings.RainbowBeamStagger;
            int budget = context.Board.BoardWidth + context.Board.BoardHeight;

            return budget <= 0 || count <= budget ? stagger : stagger * budget / count;
        }

        private static float WindUpOf(TimingProfile timings, BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Bomb:
                    return timings.BombWindUp;
                case BoosterType.Rainbow:
                    return timings.RainbowWindUp;
                case BoosterType.Airplane:
                    return timings.AirplaneWindUp;
                default:
                    return 0f;
            }
        }

        private static Vector2 CellSquare(float cellSize, float cells)
            => new Vector2(cellSize * cells, cellSize * cells);

        private static UniTask Wait(float seconds, CancellationToken ct)
            => seconds <= 0f
                ? UniTask.CompletedTask
                : UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

        private GameObject FxFor(BoosterType booster) => _chipProfile.GetBoosterFx(booster);

        private GameObject BeamFxFor(BoosterType booster) => _chipProfile.GetBoosterBeamFx(booster);

        private GameObject BurstFxFor(BoosterType booster) => _chipProfile.GetBoosterBurstFx(booster);

        private GameObject ImpactFxFor(BoosterType booster) => _chipProfile.GetBoosterImpactFx(booster);
    }
}
