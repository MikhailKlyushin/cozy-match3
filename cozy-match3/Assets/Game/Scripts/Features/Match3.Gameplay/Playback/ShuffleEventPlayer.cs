using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Match3.Core;
using Match3.Resolve;
using UnityEngine;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Board shuffle (§5.4): chips fly apart over ShuffleScatter and gather into their new cells
    /// over ShuffleGather (§11.3). The relocations are one contiguous block of the transcript, so
    /// the whole cast is known when the block opens.
    /// </summary>
    public sealed class ShuffleEventPlayer : ITurnEventPlayer
    {
        private static readonly TurnEventKind[] Handled =
        {
            TurnEventKind.ShuffleBegin,
            TurnEventKind.ShuffleEnd
        };

        private readonly List<Participant> _participants = new List<Participant>(96);

        /// <summary>Doubling the offset from the board centre pushes a chip out under the mask.</summary>
        private const float ScatterDistanceFactor = 2f;

        public IReadOnlyList<TurnEventKind> Kinds => Handled;

        public async UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            TurnEvent e = context.Transcript.GetEvent(eventIndex);

            if (e.Kind == TurnEventKind.ShuffleBegin)
            {
                await ScatterAsync(context, eventIndex, ct);
                return;
            }

            await GatherAsync(context, ct);
        }

        private static UniTask DelayAsync(float seconds, CancellationToken ct)
            => seconds <= 0f
                ? UniTask.CompletedTask
                : UniTask.Delay(
                    TimeSpan.FromSeconds(seconds),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);

        private async UniTask ScatterAsync(PlaybackContext context, int beginIndex, CancellationToken ct)
        {
            Collect(context.Transcript, beginIndex);

            int scattered = 0;
            for (int i = 0; i < _participants.Count; i++)
            {
                if (!context.Board.TryGetChip(_participants[i].InstanceId, out ChipView chip))
                {
                    continue;
                }

                // Deterministic by construction: no RNG anywhere in the visual (I4).
                Vector2 outward = chip.Rect.anchoredPosition * ScatterDistanceFactor;
                chip.Rect.DOAnchorPos(outward, context.Timings.ShuffleScatter)
                    .SetEase(Ease.InQuad)
                    .SetLink(chip.gameObject);
                scattered++;
            }

            if (scattered > 0)
            {
                await DelayAsync(context.Timings.ShuffleScatter, ct);
            }
        }

        private async UniTask GatherAsync(PlaybackContext context, CancellationToken ct)
        {
            // The regeneration phase (§5.4 step 3) recolours in place, which the model reports as
            // a destroy plus a spawn: those animations finish before the board settles.
            await context.AwaitDestructionAsync();

            int gathered = 0;
            for (int i = 0; i < _participants.Count; i++)
            {
                Participant participant = _participants[i];
                if (!participant.Target.IsValid
                    || !context.Board.TryGetChip(participant.InstanceId, out ChipView chip))
                {
                    continue;
                }

                Vector2 target = context.Board.Layout.CellCenter(participant.Target);
                chip.SetPosition(participant.Target, chip.Rect.anchoredPosition);
                chip.Rect.DOAnchorPos(target, context.Timings.ShuffleGather)
                    .SetEase(Ease.OutQuad)
                    .SetLink(chip.gameObject);
                gathered++;
            }

            _participants.Clear();

            if (gathered > 0)
            {
                await DelayAsync(context.Timings.ShuffleGather, ct);
            }

            // Chips the regeneration phase respawned still have to drop in from above the board.
            await context.FlushMovesAsync(ct);
        }

        /// <summary>
        /// Reads the shuffle block ahead: permuted chips carry their target cell, recoloured ones
        /// only scatter and are then replaced by a spawn.
        /// </summary>
        private void Collect(TurnTranscript transcript, int beginIndex)
        {
            _participants.Clear();

            for (int i = beginIndex + 1; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);

                if (e.Kind == TurnEventKind.ShuffleEnd)
                {
                    return;
                }

                if (e.Kind == TurnEventKind.ChipMoved && (e.MoveFlags & ChipMoveFlags.Shuffle) != 0)
                {
                    _participants.Add(new Participant(e.InstanceId, e.B));
                }
                else if (e.Kind == TurnEventKind.ChipDestroyed)
                {
                    _participants.Add(new Participant(e.InstanceId, GridPos.Invalid));
                }
            }
        }

        private readonly struct Participant
        {
            public readonly int InstanceId;

            /// <summary>Cell to gather into, or <see cref="GridPos.Invalid"/> for a chip that dies.</summary>
            public readonly GridPos Target;

            public Participant(int instanceId, GridPos target)
            {
                InstanceId = instanceId;
                Target = target;
            }
        }
    }
}
