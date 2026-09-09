using System;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Stage REFILL (GDD §5.1 stage 9, §3.3): every spawner column releases one chip into its top
    /// cell. Columns without a spawner are fed by diagonal slide only (§5.3, levels 10 and 12).
    /// </summary>
    public sealed class RefillService
    {
        private readonly BoardModel _board;
        private readonly IChipSpawnPolicy _policy;

        public RefillService(BoardModel board, IChipSpawnPolicy policy)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        /// <summary>
        /// Fills the empty top cells of spawner columns, walking columns x-ascending. Returns the
        /// number of new chips and writes ChipSpawned.
        /// </summary>
        public int Run(TranscriptWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            int spawned = 0;
            int topRow = _board.Height - 1;

            for (int x = 0; x < _board.Width; x++)
            {
                if (!_board.IsSpawnerColumn(x))
                {
                    continue;
                }

                var cell = new GridPos(x, topRow);
                if (!GravityService.CanHoldChip(_board, cell))
                {
                    continue;
                }

                ChipColor color = _policy.NextColor(x);
                int instanceId = _board.SetChip(cell, color);
                writer.ChipSpawned(cell, color, instanceId);
                spawned++;
            }

            return spawned;
        }
    }
}
