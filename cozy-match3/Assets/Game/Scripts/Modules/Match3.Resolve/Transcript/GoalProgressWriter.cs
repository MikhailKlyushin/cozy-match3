using Match3.Core;
using Match3.Goals;

namespace Match3.Resolve
{
    /// <summary>
    /// Writes GoalProgress for a range of goal deltas. Zero deltas are skipped: the tracker
    /// reports them so callers can see a credit was discarded (E18), but a "nothing changed"
    /// tick is noise in the HUD (rule T4).
    /// </summary>
    internal static class GoalProgressWriter
    {
        internal static void Write(PooledList<GoalDelta> deltas, int from, TranscriptWriter writer)
        {
            for (int i = from; i < deltas.Count; i++)
            {
                GoalDelta delta = deltas[i];
                if (delta.Delta != 0)
                {
                    writer.GoalProgress(delta.GoalIndex, delta.Delta, delta.NewValue);
                }
            }
        }
    }
}
