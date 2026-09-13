namespace Match3.Core
{
    /// <summary>
    /// Cell kind. The indestructible blocker is not a cell kind but an element with
    /// damageSource=None, health=Infinite, goalRole=NotCountable (A03).
    /// </summary>
    public enum CellKind : byte
    {
        /// <summary>Token __: occupies nothing, blocks falling (GDD §3.2).</summary>
        Hole = 0,

        Playable = 1
    }
}
