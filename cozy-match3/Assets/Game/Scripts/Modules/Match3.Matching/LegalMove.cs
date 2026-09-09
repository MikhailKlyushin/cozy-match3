using Match3.Core;

namespace Match3.Matching
{
    /// <summary>One legal swap of two neighbouring cells (§5.1 VALIDATE).</summary>
    public readonly struct LegalMove
    {
        public readonly GridPos A;
        public readonly GridPos B;

        public LegalMove(GridPos a, GridPos b)
        {
            A = a;
            B = b;
        }
    }
}
