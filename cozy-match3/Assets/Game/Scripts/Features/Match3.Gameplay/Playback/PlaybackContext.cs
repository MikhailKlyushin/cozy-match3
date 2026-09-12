using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;
using UnityEngine;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Everything an event player may touch while a turn replays. Notably absent: the board. The
    /// model is already in its final state, so reading it here would draw the future (rule V1).
    /// </summary>
    public sealed class PlaybackContext
    {
        private readonly List<UniTask> _pendingDestruction = new List<UniTask>(64);
        private readonly List<PendingMove> _pendingMoves = new List<PendingMove>(96);
        private readonly Dictionary<int, int> _moveByInstance = new Dictionary<int, int>(96);

        /// <summary>Sideways steps of the step's moves, kept out of the coalesced From-To line.</summary>
        private readonly List<SlideLeg> _slideLegs = new List<SlideLeg>(16);

        /// <summary>Chips each column already queued above the board in the current step.</summary>
        private int[] _spawnStackByColumn;

        /// <summary>Cached so the per-chip tweens of a cascade allocate no delegate.</summary>
        private readonly EaseFunction _fallEase;

        public PlaybackContext(BoardView board, TimingProfile timings)
        {
            Board = board;
            Timings = timings;
            _fallEase = EvaluateFall;
        }

        public BoardView Board { get; }

        public TimingProfile Timings { get; }

        public TurnTranscript Transcript { get; private set; }

        public int PendingDestructionCount => _pendingDestruction.Count;

        public int PendingMoveCount => _pendingMoves.Count;

        public void BeginTurn(TurnTranscript transcript)
        {
            Transcript = transcript;
            _pendingDestruction.Clear();
            _pendingMoves.Clear();
            _moveByInstance.Clear();
            _slideLegs.Clear();
            ResetSpawnStack();
        }

        /// <summary>
        /// Registers a destruction animation the ANIMATE barrier must wait for. This is what makes
        /// "nothing falls until the destruction FX finished" structural (§5.1 stages 7-8).
        /// </summary>
        public void TrackDestruction(UniTask task) => _pendingDestruction.Add(task);

        public async UniTask AwaitDestructionAsync()
        {
            if (_pendingDestruction.Count == 0)
            {
                return;
            }

            await UniTask.WhenAll(_pendingDestruction);
            _pendingDestruction.Clear();
        }

        /// <summary>
        /// Claims the next free row above <paramref name="column"/> for a refilled chip. REFILL
        /// fills one top cell per pass, so a column that lost three cells spawns three times in
        /// the same step and each has to start one row higher than the last: they then fall as one
        /// solid stack instead of unfolding out of a single point (§11.3).
        /// </summary>
        public int NextSpawnStackOffset(int column)
        {
            if (_spawnStackByColumn == null || column < 0 || column >= _spawnStackByColumn.Length)
            {
                return 0;
            }

            return _spawnStackByColumn[column]++;
        }

        /// <summary>
        /// Records that a chip ended up in <paramref name="targetCell"/>. Gravity moves a chip one
        /// cell per pass, so a three-cell fall arrives as three events; coalescing them into one
        /// tween is what keeps the fall smooth instead of stepped.
        /// </summary>
        public void TrackMove(ChipView chip, GridPos targetCell, ChipMoveFlags flags)
        {
            GridPos fromCell = chip.Cell;
            chip.SetPosition(targetCell, chip.Rect.anchoredPosition);

            if (_moveByInstance.TryGetValue(chip.InstanceId, out int existing))
            {
                PendingMove move = _pendingMoves[existing];
                _pendingMoves[existing] = new PendingMove(
                    move.Chip, move.InstanceId, move.FromCell, targetCell, move.Flags | flags);
                TrackSlideLeg(existing, fromCell, targetCell);
                return;
            }

            int index = _pendingMoves.Count;
            _moveByInstance[chip.InstanceId] = index;
            _pendingMoves.Add(new PendingMove(chip, chip.InstanceId, fromCell, targetCell, flags));
            TrackSlideLeg(index, fromCell, targetCell);
        }

        /// <summary>
        /// Plays every coalesced move of the step, staggering neighbours of a falling column by
        /// 0.03 s and squashing on landing (§11.3). Awaits the longest one.
        /// </summary>
        public async UniTask FlushMovesAsync(CancellationToken ct)
        {
            if (_pendingMoves.Count == 0)
            {
                return;
            }

            float longest = 0f;

            for (int i = 0; i < _pendingMoves.Count; i++)
            {
                PendingMove move = _pendingMoves[i];

                // The view was pooled or recycled since the move was recorded: tweening it now
                // would drag whatever chip owns it out of its cell.
                if (move.Chip.InstanceId != move.InstanceId)
                {
                    continue;
                }

                float cells = move.FromCell.Y - move.To.Y;
                float duration = FallDuration(cells);
                float delay = StackRank(i) * Timings.NeighbourFallStagger;
                longest = Mathf.Max(longest, delay + duration + Timings.LandingSquashDuration);

                // The two axes are tweened apart: the vertical one carries the whole §11.3 fall
                // profile, the horizontal ones cross their row when the fall gets there. One
                // straight line from start to finish would cut the corner off a slide and take
                // the chip through the blocker it slid around.
                move.Chip.Rect
                    .DOAnchorPosY(Board.Layout.CellCenter(move.To).y, duration)
                    .SetEase(_fallEase)
                    .SetDelay(delay)
                    .SetLink(move.Chip.gameObject);

                PlaySlideLegs(i, move, delay);
                move.Chip.PlayLandingSquash(Timings.LandingSquashDuration, delay + duration);
            }

            _pendingMoves.Clear();
            _moveByInstance.Clear();
            _slideLegs.Clear();
            ResetSpawnStack();

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(longest),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);
        }

        /// <summary>
        /// Time to fall <paramref name="cells"/> under the §11.3 profile: accelerate at
        /// 40 cells/s^2 up to 18 cells/s, then travel at that speed.
        /// </summary>
        public float FallDuration(float cells)
        {
            if (cells <= 0f)
            {
                return 0f;
            }

            float acceleration = Timings.FallAcceleration;
            float maxSpeed = Timings.FallMaxSpeed;
            float timeToMaxSpeed = maxSpeed / acceleration;
            float distanceToMaxSpeed = 0.5f * acceleration * timeToMaxSpeed * timeToMaxSpeed;

            if (cells <= distanceToMaxSpeed)
            {
                return Mathf.Sqrt(2f * cells / acceleration);
            }

            return timeToMaxSpeed + (cells - distanceToMaxSpeed) / maxSpeed;
        }

        /// <summary>
        /// A slide changes column mid-fall (§5.3). Keeping the step out of the coalesced
        /// From-To line is what lets the chip go around the blocker instead of through it.
        /// </summary>
        private void TrackSlideLeg(int moveIndex, GridPos from, GridPos to)
        {
            if (from.X == to.X)
            {
                return;
            }

            _slideLegs.Add(new SlideLeg(moveIndex, to.Y, to.X));
        }

        /// <summary>
        /// Crosses each sideways step of a move at the moment its fall reaches that row, so the
        /// horizontal hop lasts exactly as long as the chip spends traversing the row.
        /// </summary>
        private void PlaySlideLegs(int moveIndex, in PendingMove move, float delay)
        {
            for (int i = 0; i < _slideLegs.Count; i++)
            {
                SlideLeg leg = _slideLegs[i];
                if (leg.MoveIndex != moveIndex)
                {
                    continue;
                }

                float above = move.FromCell.Y - (leg.Row + 1);
                float start = FallDuration(above);
                float end = FallDuration(above + 1f);
                float x = Board.Layout.CellCenter(new GridPos(leg.Column, leg.Row)).x;

                move.Chip.Rect
                    .DOAnchorPosX(x, end - start)
                    .SetEase(Ease.InOutQuad)
                    .SetDelay(delay + start)
                    .SetLink(move.Chip.gameObject);
            }
        }

        /// <summary>
        /// Normalised progress of the §11.3 fall: accelerate at 40 cells/s^2 up to 18 cells/s,
        /// then hold that speed. <see cref="Ease.InQuad"/> matches this only while the fall is
        /// short enough never to reach the cap - past that it keeps accelerating, and two chips
        /// of the same column falling different distances stop moving as one stack.
        /// The duration already encodes the distance, so no per-tween parameter is needed.
        /// </summary>
        private float EvaluateFall(float time, float duration, float overshoot, float period)
        {
            if (duration <= 0f || time >= duration)
            {
                return 1f;
            }

            if (time <= 0f)
            {
                return 0f;
            }

            float acceleration = Timings.FallAcceleration;
            float timeToMaxSpeed = Timings.FallMaxSpeed / acceleration;

            // The cap is never reached, so the whole fall is constant acceleration.
            if (duration <= timeToMaxSpeed)
            {
                float ratio = time / duration;
                return ratio * ratio;
            }

            float distanceToMaxSpeed = 0.5f * acceleration * timeToMaxSpeed * timeToMaxSpeed;
            float total = distanceToMaxSpeed + Timings.FallMaxSpeed * (duration - timeToMaxSpeed);
            float travelled = time <= timeToMaxSpeed
                ? 0.5f * acceleration * time * time
                : distanceToMaxSpeed + Timings.FallMaxSpeed * (time - timeToMaxSpeed);

            return travelled / total;
        }

        /// <summary>
        /// Rank of a move inside its destination column, counted from the bottom. The §11.3
        /// stagger is between neighbours of the falling stack - the lowest chip leaves first and
        /// the column reads as a line falling top-down. Staggering by column index instead starts
        /// a whole column in one frame and spreads the wave sideways, which is not the fall.
        /// </summary>
        private int StackRank(int index)
        {
            int column = _pendingMoves[index].To.X;
            int row = _pendingMoves[index].To.Y;
            int rank = 0;

            for (int i = 0; i < _pendingMoves.Count; i++)
            {
                PendingMove other = _pendingMoves[i];

                // A recycled view is skipped by the flush, so counting it would leave a hole in
                // the stagger.
                if (i != index
                    && other.To.X == column
                    && other.To.Y < row
                    && other.Chip.InstanceId == other.InstanceId)
                {
                    rank++;
                }
            }

            return rank;
        }

        private void ResetSpawnStack()
        {
            int width = Board.BoardWidth;

            if (_spawnStackByColumn == null || _spawnStackByColumn.Length < width)
            {
                _spawnStackByColumn = new int[width];
                return;
            }

            System.Array.Clear(_spawnStackByColumn, 0, _spawnStackByColumn.Length);
        }

        private readonly struct PendingMove
        {
            public readonly ChipView Chip;

            /// <summary>Identity the view carried when the move was recorded (§6, rule T5).</summary>
            public readonly int InstanceId;

            /// <summary>Cell the chip left; a refill starts above the top row.</summary>
            public readonly GridPos FromCell;

            public readonly GridPos To;
            public readonly ChipMoveFlags Flags;

            public PendingMove(
                ChipView chip,
                int instanceId,
                GridPos fromCell,
                GridPos to,
                ChipMoveFlags flags)
            {
                Chip = chip;
                InstanceId = instanceId;
                FromCell = fromCell;
                To = to;
                Flags = flags;
            }
        }

        /// <summary>One sideways step of a move: the row it happens on and the column it ends in.</summary>
        private readonly struct SlideLeg
        {
            public readonly int MoveIndex;
            public readonly int Row;
            public readonly int Column;

            public SlideLeg(int moveIndex, int row, int column)
            {
                MoveIndex = moveIndex;
                Row = row;
                Column = column;
            }
        }
    }
}
