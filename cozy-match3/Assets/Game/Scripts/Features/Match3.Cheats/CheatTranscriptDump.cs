using System;
using System.Globalization;
using System.Text;
using Match3.Core;
using Match3.Resolve;

namespace Match3.Cheats
{
    /// <summary>
    /// Prints a <see cref="TurnTranscript"/> event by event. Dev extension beyond §12: the model
    /// runs a whole turn ahead of the view (A01), so the only way to tell a model bug from a
    /// playback bug is to read what the model actually recorded. Allocates freely - it runs on a
    /// button press, never in a turn.
    /// </summary>
    public static class CheatTranscriptDump
    {
        private const string Header = "[cheat] transcript";

        public static string Format(TurnTranscript transcript)
        {
            var builder = new StringBuilder(512);
            Append(builder, transcript);
            return builder.ToString();
        }

        public static void Append(StringBuilder builder, TurnTranscript transcript)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (transcript == null)
            {
                throw new ArgumentNullException(nameof(transcript));
            }

            builder.Append(Header)
                .Append(" seed=").Append(transcript.Seed)
                .Append(" outcome=").Append(transcript.Outcome.ToString())
                .Append(" movesLeft=").Append(transcript.MovesLeft)
                .Append(" maxDepth=").Append(transcript.MaxDepth)
                .Append(" events=").Append(transcript.EventCount)
                .Append(" cells=").Append(transcript.CellCount);

            for (int i = 0; i < transcript.EventCount; i++)
            {
                builder.Append('\n');
                AppendEvent(builder, transcript.GetEvent(i), i);
            }
        }

        private static void AppendEvent(StringBuilder builder, in TurnEvent e, int index)
        {
            builder.Append("  #").Append(index.ToString("D3", CultureInfo.InvariantCulture))
                .Append(' ').Append(e.Kind.ToString());

            if (e.Step != 0)
            {
                builder.Append(" step=").Append(e.Step);
            }

            if (e.Wave != 0)
            {
                builder.Append(" wave=").Append(e.Wave);
            }

            AppendPayload(builder, e);
        }

        /// <summary>
        /// One case per kind rather than "print every non-default field": the field meaning
        /// depends on the kind (§6), and a dump that mislabels Value is worse than no dump.
        /// </summary>
        private static void AppendPayload(StringBuilder builder, in TurnEvent e)
        {
            switch (e.Kind)
            {
                case TurnEventKind.TurnBegin:
                case TurnEventKind.MoveCharged:
                    builder.Append(" movesLeft=").Append(e.Value);
                    break;

                case TurnEventKind.SwapRejected:
                case TurnEventKind.SwapPerformed:
                    AppendCell(builder, " a=", e.A);
                    AppendCell(builder, " b=", e.B);
                    break;

                case TurnEventKind.StepEnd:
                    builder.Append(" depth=").Append(e.Value);
                    break;

                case TurnEventKind.BoosterSpawned:
                case TurnEventKind.MovesBonusRocket:
                    AppendCell(builder, " at=", e.A);
                    builder.Append(" booster=").Append(e.Booster.ToString());
                    builder.Append(" id=").Append(e.InstanceId);
                    break;

                case TurnEventKind.BoosterActivated:
                    AppendCell(builder, " at=", e.A);
                    builder.Append(" booster=").Append(e.Booster.ToString());
                    builder.Append(" source=").Append(e.ActivationSource.ToString());
                    break;

                case TurnEventKind.ComboActivated:
                    AppendCell(builder, " at=", e.A);
                    AppendCell(builder, " other=", e.B);
                    builder.Append(" boosterA=").Append(e.Booster.ToString());
                    builder.Append(" boosterB=").Append(e.ComboBoosterB.ToString());
                    break;

                case TurnEventKind.BoosterEffectCells:
                    AppendCell(builder, " from=", e.A);
                    builder.Append(" booster=").Append(e.Booster.ToString());
                    builder.Append(" cells=").Append(e.CellsCount);
                    break;

                case TurnEventKind.ChipDestroyed:
                case TurnEventKind.ChipSpawned:
                    AppendCell(builder, " at=", e.A);
                    builder.Append(" color=").Append(e.Color.ToString());
                    builder.Append(" id=").Append(e.InstanceId);
                    break;

                case TurnEventKind.ChipTransformed:
                    AppendCell(builder, " at=", e.A);
                    builder.Append(" booster=").Append(e.Booster.ToString());
                    builder.Append(" id=").Append(e.InstanceId);
                    builder.Append(" delayMs=").Append(e.Amount);
                    break;

                case TurnEventKind.ElementDamaged:
                    AppendCell(builder, " at=", e.A);
                    builder.Append(" element=").Append(e.Element.Value);
                    builder.Append(" damage=").Append(e.Amount);
                    builder.Append(" hpLeft=").Append(e.Value);
                    break;

                case TurnEventKind.ElementDestroyed:
                case TurnEventKind.ElementRevealed:
                    AppendCell(builder, " at=", e.A);
                    builder.Append(" element=").Append(e.Element.Value);
                    break;

                case TurnEventKind.ElementColorCycled:
                    AppendCell(builder, " at=", e.A);
                    builder.Append(" color=").Append(e.Color.ToString());
                    break;

                case TurnEventKind.GoalProgress:
                    builder.Append(" goal=").Append(e.InstanceId);
                    builder.Append(" delta=").Append(e.Amount);
                    builder.Append(" total=").Append(e.Value);
                    break;

                case TurnEventKind.ChipMoved:
                    AppendCell(builder, " from=", e.A);
                    AppendCell(builder, " to=", e.B);
                    builder.Append(" id=").Append(e.InstanceId);
                    builder.Append(" flags=").Append(e.MoveFlags.ToString());
                    break;

                case TurnEventKind.LevelWon:
                case TurnEventKind.LevelLost:
                    builder.Append(" reason=").Append(e.EndReason.ToString());
                    break;

                case TurnEventKind.CapHit:
                    builder.Append(" cap=").Append(e.Cap.ToString());
                    break;

                default:
                    // Barriers and stage markers carry no payload beyond step and wave.
                    break;
            }
        }

        private static void AppendCell(StringBuilder builder, string caption, GridPos cell)
        {
            builder.Append(caption)
                .Append('(').Append(cell.X).Append(", ").Append(cell.Y).Append(')');
        }
    }
}
