namespace Match3.Board
{
    /// <summary>Grid tokens of GDD §10.2. Every token is exactly two characters.</summary>
    public static class ElementTokens
    {
        public const int TokenLength = 2;

        public const string PlayableCell = "..";
        public const string Hole = "__";

        /// <summary>Indestructible blocker: an element, not a cell kind (A03).</summary>
        public const string Blocker = "##";

        public const string Box1 = "bx";
        public const string Box2 = "b2";
        public const string Box3 = "b3";

        /// <summary>Colour-changing box; suffix-free because its colour is runtime state (§7.2).</summary>
        public const string ColoredBoxCycling = "cx";

        /// <summary>Prefix of the fixed-colour boxes c1-c6.</summary>
        public const char ColoredBoxPrefix = 'c';

        /// <summary>Prefix of the fixed-colour chip tokens t1-t6.</summary>
        public const char FixedChipPrefix = 't';

        public const string RocketH = "rh";
        public const string RocketV = "rv";
        public const string Bomb = "bm";
        public const string Rainbow = "rb";
        public const string Airplane = "pl";

        /// <summary>Token of a fixed-colour box, colour index 1-6.</summary>
        public static string ColoredBox(int colorIndex) => "c" + colorIndex.ToString();

        /// <summary>Token of a fixed-colour chip, colour index 1-6.</summary>
        public static string FixedChip(int colorIndex) => "t" + colorIndex.ToString();
    }
}
