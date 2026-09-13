using System;

namespace Match3.Core
{
    /// <summary>Cell coordinate. Origin bottom-left, x right, y up, gravity towards -y (GDD D01, §3.1).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public static readonly GridPos Invalid = new GridPos(-1, -1);

        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsValid => X >= 0 && Y >= 0;

        public GridPos Down => new GridPos(X, Y - 1);

        public GridPos Up => new GridPos(X, Y + 1);

        public GridPos Left => new GridPos(X - 1, Y);

        public GridPos Right => new GridPos(X + 1, Y);

        /// <summary>Checked before <see cref="UpRight"/> during diagonal slide (GDD §5.3, D10).</summary>
        public GridPos UpLeft => new GridPos(X - 1, Y + 1);

        public GridPos UpRight => new GridPos(X + 1, Y + 1);

        /// <summary>Diagonals are not neighbours: AdjacentMatch damage spreads orthogonally only (GDD §7.1).</summary>
        public bool IsOrthogonalNeighbourOf(GridPos other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            return (dx == 0 && (dy == 1 || dy == -1)) || (dy == 0 && (dx == 1 || dx == -1));
        }

        /// <summary>Canonical y-then-x order used by every GDD tie-break (§4.3, §5.3, §6.1, §5.5).</summary>
        public static int CompareYThenX(GridPos a, GridPos b)
        {
            if (a.Y != b.Y)
            {
                return a.Y < b.Y ? -1 : 1;
            }

            if (a.X != b.X)
            {
                return a.X < b.X ? -1 : 1;
            }

            return 0;
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public static bool operator ==(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(GridPos a, GridPos b) => a.X != b.X || a.Y != b.Y;

        /// <summary>Allocates: logs and test messages only, never the hot path (§14).</summary>
        public override string ToString() => string.Concat("(", X.ToString(), ", ", Y.ToString(), ")");
    }
}
