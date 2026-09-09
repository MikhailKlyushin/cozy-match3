using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Resolve;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Gravity and slide moves. Gravity relocates a chip one cell per pass, so a three-cell fall
    /// arrives as three events for the same chip: they are batched here and flushed once per step
    /// by <see cref="BarrierEventPlayer"/>, which is what makes the fall one smooth accelerating
    /// tween instead of three stepped hops (§11.3).
    /// </summary>
    public sealed class MovementEventPlayer : ITurnEventPlayer
    {
        private static readonly TurnEventKind[] Handled = { TurnEventKind.ChipMoved };

        public IReadOnlyList<TurnEventKind> Kinds => Handled;

        public UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            TurnEvent e = context.Transcript.GetEvent(eventIndex);

            // A shuffle relocation is gathered by ShuffleEventPlayer over ShuffleGather, not
            // dropped with the gravity batch (§5.4, §11.3).
            if ((e.MoveFlags & ChipMoveFlags.Shuffle) != 0)
            {
                return UniTask.CompletedTask;
            }

            if (context.Board.TryGetChip(e.InstanceId, out ChipView chip))
            {
                context.TrackMove(chip, e.B, e.MoveFlags);
            }

            return UniTask.CompletedTask;
        }
    }
}
