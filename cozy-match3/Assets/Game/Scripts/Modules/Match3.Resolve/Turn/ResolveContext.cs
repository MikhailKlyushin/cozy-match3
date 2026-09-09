using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using Match3.Matching;

namespace Match3.Resolve
{
    /// <summary>
    /// Per-turn buffers for the resolve pipeline. Services keep no turn state of their own, so a
    /// turn allocates nothing once the buffers are warm (§14).
    /// </summary>
    public sealed class ResolveContext
    {
        private readonly PooledList<CellBuffer> _hitBuffers = new PooledList<CellBuffer>(32);

        private int _rentedHitBuffers;
        private int _width;
        private int _height;

        public readonly PooledList<MatchComponent> Components = new PooledList<MatchComponent>(16);

        /// <summary>Cells where SPAWN placed a booster: CLEAR must not remove them.</summary>
        public readonly CellBuffer SpawnCells = new CellBuffer();

        /// <summary>Everything CLEAR removes this step: matched chips and every cell a booster hit.</summary>
        public readonly CellBuffer ChipCells = new CellBuffer();

        public readonly PooledList<GoalDelta> GoalDeltas = new PooledList<GoalDelta>(16);

        /// <summary>Activations of the current ACTIVATE wave (§6.4).</summary>
        public readonly PooledList<BoosterActivation> Wave = new PooledList<BoosterActivation>(16);

        /// <summary>Activations collected for the next wave: boosters caught in this wave's blast (E04).</summary>
        public readonly PooledList<BoosterActivation> NextWave = new PooledList<BoosterActivation>(16);

        /// <summary>Union of the cells hit by the current wave, used to find the next wave.</summary>
        public readonly CellBuffer WaveHitCells = new CellBuffer();

        /// <summary>One entry per booster activation: its damage source id and its hit cells.</summary>
        public readonly PooledList<BoosterHit> Hits = new PooledList<BoosterHit>(32);

        public int Step { get; private set; }

        /// <summary>Damage source ids for booster activations, unique within the turn (D07).</summary>
        public int NextSourceId { get; private set; }

        public void Configure(int width, int height)
        {
            _width = width;
            _height = height;

            SpawnCells.Configure(width, height);
            ChipCells.Configure(width, height);
            WaveHitCells.Configure(width, height);

            for (int i = 0; i < _hitBuffers.Count; i++)
            {
                _hitBuffers[i].Configure(width, height);
            }
        }

        public void BeginTurn()
        {
            Step = 0;
            NextSourceId = 0;
            GoalDeltas.Clear();
            BeginStep(0);
        }

        public void BeginStep(int step)
        {
            Step = step;
            Components.Clear();
            SpawnCells.Clear();
            ChipCells.Clear();
            WaveHitCells.Clear();
            Wave.Clear();
            NextWave.Clear();
            Hits.Clear();
            _rentedHitBuffers = 0;
        }

        /// <summary>
        /// A cleared buffer for one activation's hit cells. Buffers are recycled every step, so
        /// the slices handed to DAMAGE stay valid until the next <see cref="BeginStep"/>.
        /// </summary>
        public CellBuffer RentHitBuffer()
        {
            if (_rentedHitBuffers == _hitBuffers.Count)
            {
                var buffer = new CellBuffer();
                buffer.Configure(_width, _height);
                _hitBuffers.Add(buffer);
            }

            CellBuffer rented = _hitBuffers[_rentedHitBuffers++];
            rented.Clear();
            return rented;
        }

        public int TakeSourceId() => NextSourceId++;

        /// <summary>Swaps the wave buffers so the collected next wave becomes the current one.</summary>
        public void AdvanceWave()
        {
            Wave.Clear();
            Wave.AddRange(NextWave);
            NextWave.Clear();
        }
    }
}
