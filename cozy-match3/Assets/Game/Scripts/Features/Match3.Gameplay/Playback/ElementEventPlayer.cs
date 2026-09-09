using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Match3.Board;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Obstacle feedback: every hit point lost changes the visual (§7.1), a destroyed element
    /// leaves the board before anything falls through its cell, and a cx box morphs on POST-TURN
    /// (§7.2). The revealed element is rebuilt from the catalog, never read back from the board.
    /// </summary>
    public sealed class ElementEventPlayer : ITurnEventPlayer
    {
        private readonly ElementCatalog _catalog;
        private readonly LevelRules _rules;

        private static readonly TurnEventKind[] Handled =
        {
            TurnEventKind.ElementDamaged,
            TurnEventKind.ElementDestroyed,
            TurnEventKind.ElementRevealed,
            TurnEventKind.ElementColorCycled
        };

        public ElementEventPlayer(ElementCatalog catalog, LevelRules rules)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public IReadOnlyList<TurnEventKind> Kinds => Handled;

        public async UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            TurnEvent e = context.Transcript.GetEvent(eventIndex);

            switch (e.Kind)
            {
                case TurnEventKind.ElementDamaged:
                    Damage(context, e, ct);
                    break;

                case TurnEventKind.ElementDestroyed:
                    StartDestruction(context, eventIndex, e, ct);
                    break;

                case TurnEventKind.ElementRevealed:
                    Reveal(context, e, ct);
                    break;

                case TurnEventKind.ElementColorCycled:
                    await MorphAsync(context, e, ct);
                    break;
            }
        }

        /// <summary>Hit punch, out and back over DestroyPunchDuration.</summary>
        private static async UniTask PunchAsync(ElementView view, TimingProfile timings, CancellationToken ct)
        {
            view.Rect.DOScale(timings.DestroyPunchScale, timings.DestroyPunchDuration)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(view.gameObject);

            await UniTask.Delay(
                TimeSpan.FromSeconds(timings.DestroyPunchDuration * 2f),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);
        }

        private static async UniTask DestroyAsync(
            PlaybackContext context,
            ElementView view,
            GridPos cell,
            CancellationToken ct)
        {
            TimingProfile timings = context.Timings;

            view.Rect.DOScale(timings.DestroyPunchScale, timings.DestroyPunchDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(view.gameObject);
            await UniTask.Delay(
                TimeSpan.FromSeconds(timings.DestroyPunchDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            view.Rect.DOScale(0f, timings.DestroyFadeDuration)
                .SetEase(Ease.InQuad)
                .SetLink(view.gameObject);
            await UniTask.Delay(
                TimeSpan.FromSeconds(timings.DestroyFadeDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            context.Board.RemoveElement(cell);
        }

        /// <summary>Same rule the level builder applies: only a fixed-colour element knows its colour up front (§7.2).</summary>
        private static ChipColor InitialColorOf(ElementDefinition definition)
            => definition.ColorMode == ElementColorMode.Fixed ? definition.FixedColor : ChipColor.None;

        private static void Damage(PlaybackContext context, in TurnEvent e, CancellationToken ct)
        {
            ElementView view = context.Board.GetElement(e.A);
            if (view == null)
            {
                return;
            }

            // Value is the remaining hp; §7.1 requires the visual to change on every hit.
            view.SetHealth(e.Value);

            // A lethal hit needs no punch of its own: the death or the reveal that follows in the
            // same step carries the feedback, and two tweens on one transform would fight.
            if (e.Value > 0)
            {
                context.TrackDestruction(PunchAsync(view, context.Timings, ct));
            }
        }

        /// <summary>
        /// A nested element is uncovered by the very next event of the same cell (§7.1), and that
        /// reveal recycles the view - so the outer element only plays its own death when nothing
        /// takes its place.
        /// </summary>
        private static void StartDestruction(
            PlaybackContext context,
            int eventIndex,
            in TurnEvent e,
            CancellationToken ct)
        {
            if (IsFollowedByRevealOf(context.Transcript, eventIndex, e.A))
            {
                return;
            }

            ElementView view = context.Board.GetElement(e.A);
            if (view == null)
            {
                return;
            }

            context.TrackDestruction(DestroyAsync(context, view, e.A, ct));
        }

        private static bool IsFollowedByRevealOf(TurnTranscript transcript, int eventIndex, GridPos cell)
        {
            int next = eventIndex + 1;
            if (next >= transcript.EventCount)
            {
                return false;
            }

            TurnEvent candidate = transcript.GetEvent(next);
            return candidate.Kind == TurnEventKind.ElementRevealed && candidate.A == cell;
        }

        private void Reveal(PlaybackContext context, in TurnEvent e, CancellationToken ct)
        {
            // The transcript names the cell and the definition; everything else about a freshly
            // uncovered element is known from the catalog, so no board read is needed (rule V1).
            ElementDefinition definition = _catalog.Get(e.Element);
            var revealed = new ElementInstance(
                e.Element,
                definition.MaxHealth,
                InitialColorOf(definition),
                ElementInstance.NoNested);

            ElementView view = context.Board.RevealElement(e.A, revealed, _catalog);
            if (view != null)
            {
                context.TrackDestruction(PunchAsync(view, context.Timings, ct));
            }
        }

        /// <summary>
        /// cx flip: shrink, swap the colour at the midpoint, grow back over CyclingBoxMorph. Boxes
        /// morph one after another because POST-TURN has no barrier to join them on.
        /// </summary>
        private async UniTask MorphAsync(PlaybackContext context, TurnEvent e, CancellationToken ct)
        {
            ElementView view = context.Board.GetElement(e.A);
            if (view == null)
            {
                return;
            }

            float half = context.Timings.CyclingBoxMorph * 0.5f;

            view.Rect.DOScaleX(0f, half).SetEase(Ease.InQuad).SetLink(view.gameObject);
            await UniTask.Delay(
                TimeSpan.FromSeconds(half),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            view.SetColor(e.Color, ChipColors.NextCycling(e.Color, _rules.ColorCount));

            view.Rect.DOScaleX(1f, half).SetEase(Ease.OutQuad).SetLink(view.gameObject);
            await UniTask.Delay(
                TimeSpan.FromSeconds(half),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);
        }
    }
}
