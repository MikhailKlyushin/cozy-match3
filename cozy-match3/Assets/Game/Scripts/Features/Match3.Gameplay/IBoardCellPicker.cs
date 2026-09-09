using Match3.Core;
using UnityEngine;

namespace Match3.Gameplay
{
    /// <summary>
    /// Screen position to board cell. Declared here because the provider declares the contract:
    /// Match3.Cheats may reference Match3.Gameplay for interfaces only (§3.1).
    /// </summary>
    public interface IBoardCellPicker
    {
        /// <summary><see cref="GridPos.Invalid"/> when the position is outside the board.</summary>
        GridPos PickCell(Vector2 screenPosition);

        /// <summary>Cell centre in screen space, for cheat overlays and hint arrows.</summary>
        Vector2 CellToScreen(GridPos cell);

        int BoardWidth { get; }

        int BoardHeight { get; }
    }
}
