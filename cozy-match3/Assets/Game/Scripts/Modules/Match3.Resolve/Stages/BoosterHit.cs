using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// One booster activation recorded during ACTIVATE so DAMAGE can charge it as a single
    /// source. Area damage covering several cells of one obstacle is still 1 damage (D07, E09).
    /// </summary>
    public readonly struct BoosterHit
    {
        public readonly int SourceId;
        public readonly GridPos Origin;
        public readonly BoosterType Booster;

        /// <summary>Cells this activation hit; owned by the turn's buffer pool.</summary>
        public readonly CellBuffer Cells;

        public BoosterHit(int sourceId, GridPos origin, BoosterType booster, CellBuffer cells)
        {
            SourceId = sourceId;
            Origin = origin;
            Booster = booster;
            Cells = cells;
        }
    }
}
