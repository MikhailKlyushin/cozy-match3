using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Resolve;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Publishes the cascade depth of the step about to replay. It draws nothing: StepBegin has no
    /// visual of its own, and the alternative to a player of its own is a special case for one
    /// event kind inside <see cref="TranscriptPlayer"/>, which is the central switch A11 exists to
    /// avoid. Sound reads the depth from the context to climb with the cascade.
    /// </summary>
    public sealed class CascadeDepthEventPlayer : ITurnEventPlayer
    {
        private static readonly TurnEventKind[] Handled = { TurnEventKind.StepBegin };

        public IReadOnlyList<TurnEventKind> Kinds => Handled;

        public UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            context.SetCascadeStep(context.Transcript.GetEvent(eventIndex).Step);
            return UniTask.CompletedTask;
        }
    }
}
