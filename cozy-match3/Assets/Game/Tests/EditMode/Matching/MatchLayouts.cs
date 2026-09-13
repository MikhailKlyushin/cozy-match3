namespace Match3.Tests.EditMode.Matching
{
    /// <summary>
    /// Token layouts for the GDD §4.2 figures. Every layout is free of unintended lines and
    /// squares, so each test sees exactly the shape it is named after.
    /// </summary>
    internal static class MatchLayouts
    {
        /// <summary>No run of 3, no 2x2 square and no legal chip swap.</summary>
        public const string NoMatch = @"
            t1 t2 t3
            t4 t1 t2
            t3 t4 t1";

        /// <summary>Line of 3 at y = 1: rank 5, no booster.</summary>
        public const string Line3 = @"
            t2 t3 t2 t3
            t1 t1 t1 t2
            t3 t2 t3 t2";

        /// <summary>Horizontal line of 4 at y = 1.</summary>
        public const string Line4Horizontal = @"
            t2 t3 t2 t3 t2
            t1 t1 t1 t1 t2
            t3 t2 t3 t2 t3";

        /// <summary>Vertical line of 4 at x = 1, y = 1..4.</summary>
        public const string Line4Vertical = @"
            t2 t1 t3 t2
            t3 t1 t2 t3
            t2 t1 t3 t2
            t3 t1 t2 t3
            t2 t3 t3 t2";

        /// <summary>Line of 5 at y = 1, x = 0..4.</summary>
        public const string Line5 = @"
            t2 t3 t2 t3 t2 t3 t2
            t1 t1 t1 t1 t1 t2 t3
            t3 t2 t3 t2 t3 t2 t3";

        /// <summary>Line of 6 at y = 1, x = 0..5.</summary>
        public const string Line6 = @"
            t2 t3 t2 t3 t2 t3 t2
            t1 t1 t1 t1 t1 t1 t2
            t3 t2 t3 t2 t3 t2 t3";

        /// <summary>Two lines of 3 crossing at (2, 2): 5 cells, shape +.</summary>
        public const string Plus = @"
            t2 t3 t2 t3 t2
            t3 t2 t1 t2 t3
            t2 t1 t1 t1 t3
            t3 t2 t1 t3 t2
            t2 t3 t2 t3 t2";

        /// <summary>Two lines of 3 crossing at (0, 1): 5 cells, shape L.</summary>
        public const string LShape = @"
            t2 t3 t2 t3 t2
            t1 t2 t3 t2 t3
            t1 t3 t2 t3 t2
            t1 t1 t1 t2 t3
            t2 t3 t2 t3 t2";

        /// <summary>Line of 4 at y = 2 crossed by a line of 3 at x = 1: 6 cells, shape T.</summary>
        public const string TShape = @"
            t2 t3 t2 t3 t2
            t3 t2 t3 t2 t3
            t1 t1 t1 t1 t2
            t2 t1 t3 t2 t3
            t3 t1 t2 t3 t2";

        /// <summary>Line of 4 at y = 2 crossed by lines of 3 at x = 0 and x = 3: two crossings.</summary>
        public const string DoubleCrossing = @"
            t2 t3 t2 t1 t3
            t3 t2 t3 t1 t2
            t1 t1 t1 t1 t3
            t1 t2 t3 t2 t2
            t1 t3 t2 t3 t2";

        /// <summary>Block of 3 by 2 at x = 0..2, y = 1..2: two lines of 3 plus two squares.</summary>
        public const string Block2x3 = @"
            t2 t3 t2 t3 t2
            t3 t2 t3 t2 t3
            t1 t1 t1 t2 t3
            t1 t1 t1 t3 t2
            t2 t3 t2 t3 t2";

        /// <summary>Single 2x2 square with its bottom-left cell at (1, 1) and no line at all.</summary>
        public const string LoneSquare = @"
            t2 t3 t2 t3
            t3 t1 t1 t2
            t2 t1 t1 t3
            t3 t2 t3 t2";

        /// <summary>Line of 4 at y = 0 and a line of 4 at x = 4 that share no cell (E01).</summary>
        public const string TwoComponents = @"
            t2 t3 t2 t3 t1
            t3 t2 t3 t2 t1
            t2 t3 t2 t3 t1
            t3 t2 t3 t2 t1
            t1 t1 t1 t1 t2";

        /// <summary>A booster between two chips of one colour: colourless, so no line (§4.2).</summary>
        public const string BoosterBetweenChips = @"
            t2 t3 t2
            t3 t2 t3
            t1 rh t1";

        /// <summary>
        /// No match yet; swapping (1,1) with (2,1) or with (1,2) creates one, swapping (0,0) with
        /// (1,0) does not.
        /// </summary>
        public const string SwapReady = @"
            t3 t2 t3 t2
            t1 t2 t1 t3
            t2 t1 t2 t1
            t1 t2 t3 t2";
    }
}
