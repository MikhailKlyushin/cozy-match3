using System;
using Match3.Board;
using Match3.Core;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Stage SPAWN (GDD §5.1 stage 3). Places the booster a component earned and marks it
    /// consumed for this step, which is how D06 is enforced: a booster created in a cascade
    /// appears but never fires in the same step (transcript rule T2).
    /// </summary>
    public sealed class BoosterSpawnService
    {
        private readonly BoardModel _board;

        public BoosterSpawnService(BoardModel board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
        }

        /// <summary>
        /// Spawns one booster per component that earned a rank. <paramref name="spawnCells"/> is
        /// appended with the occupied cells so CLEAR can skip them - the booster must survive the
        /// clear of its own component. Returns the number of boosters placed.
        /// </summary>
        public int Spawn(
            PooledList<MatchComponent> components,
            CellBuffer spawnCells,
            TranscriptWriter writer)
        {
            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            int spawned = 0;

            for (int i = 0; i < components.Count; i++)
            {
                MatchComponent component = components[i];
                if (component.Rank == ComponentRank.None || component.Booster == BoosterType.None)
                {
                    continue;
                }

                GridPos cell = component.SpawnCell;
                if (!cell.IsValid || !_board.Contains(cell))
                {
                    continue;
                }

                // Two components claiming one cell is resolved by the classifier (§4.3); this is
                // the last line of defence so a collision cannot overwrite a fresh booster.
                if (spawnCells != null && spawnCells.Contains(cell))
                {
                    continue;
                }

                // One of the matched chips BECOMES the booster, so its identity survives: the
                // spawn cell is excluded from CLEAR, and a fresh id would leave the view showing
                // the old chip forever while the model referred to an id it never saw.
                ChipSlot slot = _board.GetSlot(cell);
                int instanceId;
                if (slot.Kind == SlotKind.Empty)
                {
                    instanceId = _board.SetBooster(cell, component.Booster);
                }
                else
                {
                    instanceId = slot.InstanceId;
                    _board.TransformToBooster(cell, component.Booster);
                }

                _board.SetConsumed(cell, true);
                spawnCells?.Add(cell);

                writer.BoosterSpawned(cell, component.Booster, instanceId);
                spawned++;
            }

            return spawned;
        }
    }
}
