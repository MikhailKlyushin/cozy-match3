using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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

        public PlaybackContext(BoardView board, TimingProfile timings)
        {
            Board = board;
            Timings = timings;
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
        /// Records that a chip ended up in <paramref name="targetCell"/>. Gravity moves a chip one
        /// cell per pass, so a three-cell fall arrives as three events; coalescing them into one
        /// tween is what keeps the fall smooth instead of stepped.
        /// </summary>
        public void TrackMove(ChipView chip, GridPos targetCell, ChipMoveFlags flags)
        {
            chip.SetPosition(targetCell, chip.Rect.anchoredPosition);

            if (_moveByInstance.TryGetValue(chip.InstanceId, out int existing))
            {
                PendingMove move = _pendingMoves[existing];
                _pendingMoves[existing] = new PendingMove(
                    move.Chip, move.InstanceId, move.From, targetCell, move.Flags | flags);
                return;
            }

            _moveByInstance[chip.InstanceId] = _pendingMoves.Count;
            _pendingMoves.Add(new PendingMove(
                chip, chip.InstanceId, chip.Rect.anchoredPosition, targetCell, flags));
        }

        /// <summary>
        /// Plays every coalesced move of the step at once, staggering neighbours by 0.03 s and
        /// squashing on landing (§11.3). Awaits the longest one.
        /// </summary>
        public async UniTask FlushMovesAsync(CancellationToken ct)
        {
            if (_pendingMoves.Count == 0)
            {
                return;
            }

            float cellSize = Board.Layout.CellSize;
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

                Vector2 target = Board.Layout.CellCenter(move.To);
                float distanceCells = cellSize > 0f
                    ? Vector2.Distance(move.From, target) / cellSize
                    : 0f;

                float duration = FallDuration(distanceCells);
                float delay = move.To.X * Timings.NeighbourFallStagger;
                longest = Mathf.Max(longest, delay + duration + Timings.LandingSquashDuration);

                Tween tween = move.Chip.Rect
                    .DOAnchorPos(target, duration)
                    .SetEase(Ease.InQuad)
                    .SetDelay(delay)
                    .SetLink(move.Chip.gameObject);

                if ((move.Flags & ChipMoveFlags.Slide) != 0)
                {
                    tween.SetEase(Ease.InOutQuad);
                }

                move.Chip.PlayLandingSquash(Timings.LandingSquashDuration, delay + duration);
            }

            _pendingMoves.Clear();
            _moveByInstance.Clear();

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

        private readonly struct PendingMove
        {
            public readonly ChipView Chip;

            /// <summary>Identity the view carried when the move was recorded (§6, rule T5).</summary>
            public readonly int InstanceId;

            public readonly Vector2 From;
            public readonly GridPos To;
            public readonly ChipMoveFlags Flags;

            public PendingMove(
                ChipView chip,
                int instanceId,
                Vector2 from,
                GridPos to,
                ChipMoveFlags flags)
            {
                Chip = chip;
                InstanceId = instanceId;
                From = from;
                To = to;
                Flags = flags;
            }
        }
    }
}
