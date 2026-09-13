using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// Colour source of the REFILL stage (GDD §5.1 stage 9). Consumes the RNG in the
    /// <see cref="RandomConsumer.SpawnerColor"/> slot of the fixed order (D12, §13).
    /// </summary>
    public interface IChipSpawnPolicy
    {
        /// <summary>Colour of the new chip created in column <paramref name="x"/>.</summary>
        ChipColor NextColor(int x);
    }
}
