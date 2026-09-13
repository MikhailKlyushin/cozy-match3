using Match3.Core;

namespace Match3.Matching
{
    /// <summary>One maximal line or one 2x2 square of a single colour (GDD §4.2, D02).</summary>
    internal readonly struct MatchPrimitive
    {
        /// <summary>Leftmost cell of a horizontal line, bottom cell of a vertical one, bottom-left of a square.</summary>
        public readonly GridPos Origin;

        public readonly PrimitiveKind Kind;
        public readonly ChipColor Color;

        /// <summary>Cells along the line; always 2 for a square.</summary>
        public readonly int Length;

        public MatchPrimitive(PrimitiveKind kind, GridPos origin, int length, ChipColor color)
        {
            Kind = kind;
            Origin = origin;
            Length = length;
            Color = color;
        }

        public bool IsLine => Kind != PrimitiveKind.Square;

        public int CellCount => Kind == PrimitiveKind.Square ? 4 : Length;

        public GridPos CellAt(int index)
        {
            switch (Kind)
            {
                case PrimitiveKind.Horizontal:
                    return new GridPos(Origin.X + index, Origin.Y);
                case PrimitiveKind.Vertical:
                    return new GridPos(Origin.X, Origin.Y + index);
                default:
                    return new GridPos(Origin.X + (index & 1), Origin.Y + (index >> 1));
            }
        }

        /// <summary>Cell shared with <paramref name="other"/> when a horizontal and a vertical line cross.</summary>
        public bool TryGetCrossing(in MatchPrimitive other, out GridPos crossing)
        {
            crossing = GridPos.Invalid;
            if (Kind != PrimitiveKind.Horizontal || other.Kind != PrimitiveKind.Vertical)
            {
                return false;
            }

            int x = other.Origin.X;
            int y = Origin.Y;
            bool insideRow = x >= Origin.X && x < Origin.X + Length;
            bool insideColumn = y >= other.Origin.Y && y < other.Origin.Y + other.Length;
            if (!insideRow || !insideColumn)
            {
                return false;
            }

            crossing = new GridPos(x, y);
            return true;
        }
    }
}
