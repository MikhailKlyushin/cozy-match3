using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Resolve;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// The stage barriers of GDD §5.1. ANIMATE holds the board still until every destruction of
    /// the step has finished, StepEnd releases the batched fall and pauses before the next step,
    /// WaveBarrier spaces a chain so it reads as a series (§6.4).
    /// </summary>
    public sealed class BarrierEventPlayer : ITurnEventPlayer
    {
        private static readonly TurnEventKind[] Handled =
        {
            TurnEventKind.AnimateBarrier,
            TurnEventKind.WaveBarrier,
            TurnEventKind.StepEnd
        };

        public IReadOnlyList<TurnEventKind> Kinds => Handled;

        public async UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            TurnEvent e = context.Transcript.GetEvent(eventIndex);

            switch (e.Kind)
            {
                case TurnEventKind.AnimateBarrier:
                    // The client's acceptance criterion: nothing falls while chips are still
                    // collapsing (§5.1 stages 7-8, rule T1).
                    ct.ThrowIfCancellationRequested();
                    await context.AwaitDestructionAsync();
                    break;

                case TurnEventKind.StepEnd:
                    await context.FlushMovesAsync(ct);
                    await DelayAsync(context.Timings.StepPause, ct);
                    break;

                case TurnEventKind.WaveBarrier:
                    await DelayAsync(context.Timings.WaveBarrier, ct);
                    break;
            }
        }

        private static UniTask DelayAsync(float seconds, CancellationToken ct)
            => seconds <= 0f
                ? UniTask.CompletedTask
                : UniTask.Delay(
                    TimeSpan.FromSeconds(seconds),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);
    }
}
