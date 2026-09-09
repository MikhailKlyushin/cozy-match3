using Match3.Core;

namespace Match3.Board
{
    /// <summary>
    /// Read-only board access for matching, boosters, targeting, the view's initial build and
    /// tests. Mutation is internal and goes through the resolve pipeline only (A10).
    /// </summary>
    public interface IBoardReader
    {
        int Width { get; }

        int Height { get; }

        ElementCatalog Catalog { get; }

        bool Contains(GridPos p);

        CellKind GetKind(GridPos p);

        ChipSlot GetSlot(GridPos p);

        bool TryGetElement(GridPos p, out ElementInstance element);

        /// <summary>Blocked by holes and by any live element whose axis says so (GDD §5.3, E10).</summary>
        bool IsPassableForFall(GridPos p);

        /// <summary>Chip or booster that can be swapped or fall.</summary>
        bool IsMovable(GridPos p);

        /// <summary>GDD §3.3: default is every column with a non-blocker top cell, unless overridden.</summary>
        bool IsSpawnerColumn(int x);

        /// <summary>FNV-1a over board state; the basis of the replay determinism test (§8).</summary>
        ulong ComputeHash();
    }
}
