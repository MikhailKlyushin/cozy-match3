namespace Match3.EditorTools
{
    internal readonly struct GoalSeed
    {
        public readonly string Type;
        public readonly int Target;
        public readonly int Color;
        public readonly string Token;
        public readonly string Booster;

        private GoalSeed(string type, int target, int color, string token, string booster)
        {
            Type = type;
            Target = target;
            Color = color;
            Token = token;
            Booster = booster;
        }

        public static GoalSeed Color1To6(int color, int target)
            => new GoalSeed("CollectColor", target, color, null, null);

        public static GoalSeed Element(string token, int target)
            => new GoalSeed("DestroyElement", target, 0, token, null);

        public static GoalSeed AnyColoredBox(int target)
            => new GoalSeed("DestroyAnyColoredBox", target, 0, null, null);

        public static GoalSeed BoosterGoal(string booster, int target)
            => new GoalSeed("ActivateBooster", target, 0, null, booster);
    }

    internal readonly struct NestedSeed
    {
        public readonly int X;
        public readonly int Y;
        public readonly string Token;

        public NestedSeed(int x, int y, string token)
        {
            X = x;
            Y = y;
            Token = token;
        }
    }

    internal sealed class LevelSeed
    {
        public int Id;
        public int Width;
        public int Height;
        public int ColorCount;
        public int MoveLimit;
        public string Tier;
        public GoalSeed[] Goals;

        /// <summary>Top-down, first row is y = Height - 1 (D01). The flip happens in LayoutParser.</summary>
        public string[] Layout;

        public NestedSeed[] Contents;

        /// <summary>Empty means the §3.3 default.</summary>
        public int[] Spawners;

        public float HintDelaySeconds;

        /// <summary>Throughput used to derive MoveLimit, for the §9.3 cross-check.</summary>
        public float Throughput;

        /// <summary>Set only where MoveLimit deliberately differs from the §9.3 formula.</summary>
        public string MoveLimitNote;
    }

    /// <summary>
    /// The 12 levels of GDD §10.4, transcribed once. Plain data: no UnityEngine and no LevelConfig,
    /// so it stays the single source the asset generator reads.
    /// </summary>
    internal static class LevelSeedData
    {
        private static readonly int[] NoSpawners = new int[0];
        private static readonly NestedSeed[] NoContents = new NestedSeed[0];

        public static LevelSeed[] CreateAll()
        {
            return new[]
            {
                new LevelSeed
                {
                    Id = 1, Width = 7, Height = 7, ColorCount = 4, MoveLimit = 18,
                    Tier = "Easy", Throughput = 4.5f, HintDelaySeconds = 3f,
                    Goals = new[] { GoalSeed.Color1To6(1, 15) },
                    Layout = OpenField(7),
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 2, Width = 7, Height = 7, ColorCount = 4, MoveLimit = 21,
                    Tier = "Easy", Throughput = 4.5f, HintDelaySeconds = 3f,
                    Goals = new[] { GoalSeed.Color1To6(1, 18), GoalSeed.Color1To6(2, 18) },
                    Layout = OpenField(7),
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 3, Width = 7, Height = 7, ColorCount = 4, MoveLimit = 22,
                    Tier = "Easy", Throughput = 3.8f, HintDelaySeconds = 3f,
                    Goals = new[] { GoalSeed.Element("bx", 8), GoalSeed.Color1To6(1, 8) },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. ..",
                        ".. bx .. bx .. bx ..",
                        ".. .. .. .. .. .. ..",
                        ".. bx .. bx .. bx ..",
                        ".. .. .. .. .. .. ..",
                        ".. .. bx .. bx .. ..",
                        ".. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 4, Width = 7, Height = 7, ColorCount = 4, MoveLimit = 22,
                    Tier = "Easy", Throughput = 3.8f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.Element("b2", 8) },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. ..",
                        ".. .. b2 b2 b2 .. ..",
                        ".. .. b2 .. b2 .. ..",
                        ".. .. b2 b2 b2 .. ..",
                        ".. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 5, Width = 8, Height = 8, ColorCount = 4, MoveLimit = 26,
                    Tier = "Medium", Throughput = 3.4f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.Element("bx", 10), GoalSeed.Color1To6(2, 10) },
                    Layout = new[]
                    {
                        "__ .. .. .. .. .. .. __",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. ## .. .. ## .. ..",
                        ".. bx ## .. .. ## bx ..",
                        ".. bx ## bx bx ## bx ..",
                        ".. bx ## bx bx ## bx ..",
                        ".. .. ## .. .. ## .. ..",
                        ".. .. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 6, Width = 8, Height = 8, ColorCount = 4, MoveLimit = 15,
                    Tier = "Easy", Throughput = 4.2f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.Element("c1", 6) },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. c1 .. .. c1 .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. c1 .. .. .. .. c1 ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. c1 .. .. c1 .. ..",
                        ".. .. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 7, Width = 8, Height = 8, ColorCount = 5, MoveLimit = 27,
                    Tier = "Medium", Throughput = 4.2f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.AnyColoredBox(5), GoalSeed.Color1To6(2, 10) },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. ## ## .. .. ..",
                        ".. c2 .. .. .. .. c3 ..",
                        ".. .. .. c1 .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. c4 .. .. .. .. c5 ..",
                        ".. .. ## .. .. ## .. ..",
                        ".. .. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 8, Width = 8, Height = 8, ColorCount = 5, MoveLimit = 25,
                    Tier = "Easy", Throughput = 3.8f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.Element("bx", 8), GoalSeed.Color1To6(1, 8) },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. bx bx bx bx .. ..",
                        ".. .. ## ## .. .. .. ..",
                        ".. .. .. .. ## ## .. ..",
                        ".. .. bx bx bx bx .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 9, Width = 8, Height = 8, ColorCount = 5, MoveLimit = 14,
                    Tier = "Easy", Throughput = 4.2f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.Element("bx", 10) },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. bx .. .. bx .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. bx .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. bx .. .. bx .. ..",
                        ".. .. .. .. .. .. .. ..",
                    },
                    Contents = new[]
                    {
                        new NestedSeed(2, 5, "bx"),
                        new NestedSeed(5, 5, "bx"),
                        new NestedSeed(3, 3, "bx"),
                        new NestedSeed(2, 1, "bx"),
                        new NestedSeed(5, 1, "bx"),
                    },
                    Spawners = NoSpawners,
                },
                new LevelSeed
                {
                    Id = 10, Width = 8, Height = 8, ColorCount = 5, MoveLimit = 19,
                    Tier = "Easy", Throughput = 3.4f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.Element("cx", 8) },
                    Layout = new[]
                    {
                        ".. .. .. __ __ .. .. ..",
                        ".. cx .. __ __ .. cx ..",
                        ".. .. .. ## ## .. .. ..",
                        ".. cx cx .. .. cx cx ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                        ".. cx .. .. .. .. cx ..",
                        ".. .. .. .. .. .. .. ..",
                    },
                    Contents = NoContents,
                    Spawners = new[] { 0, 1, 2, 5, 6, 7 },
                },
                new LevelSeed
                {
                    Id = 11, Width = 8, Height = 8, ColorCount = 5, MoveLimit = 18,
                    Tier = "Medium", Throughput = 4.2f, HintDelaySeconds = 5f,
                    Goals = new[] { GoalSeed.BoosterGoal("Rocket", 3), GoalSeed.BoosterGoal("Bomb", 1) },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. .. ..",
                        ".. .. .. .. .. .. .. ..",
                        "## .. .. .. .. .. .. ##",
                        "## .. .. .. .. .. .. ##",
                        ".. .. .. .. .. .. .. ..",
                        ".. .. ## .. .. ## .. ..",
                        ".. .. ## .. .. ## .. ..",
                        ".. .. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                    MoveLimitNote = "Formula yields 17; +1 kept deliberately. A booster goal has high "
                                    + "variance and one spare move is cheaper than an unlucky loss (§10.4).",
                },
                new LevelSeed
                {
                    Id = 12, Width = 9, Height = 9, ColorCount = 6, MoveLimit = 27,
                    Tier = "Hard", Throughput = 4.2f, HintDelaySeconds = 5f,
                    Goals = new[]
                    {
                        GoalSeed.Element("b2", 4),
                        GoalSeed.AnyColoredBox(3),
                        GoalSeed.Color1To6(1, 8),
                    },
                    Layout = new[]
                    {
                        ".. .. .. .. .. .. .. .. ..",
                        ".. .. .. .. ## .. .. .. ..",
                        ".. b2 .. .. .. .. .. b2 ..",
                        ".. .. .. c1 .. c4 .. .. ..",
                        ".. .. ## .. cx .. ## .. ..",
                        ".. .. .. c2 .. .. .. .. ..",
                        ".. b2 .. .. .. .. .. b2 ..",
                        ".. .. .. .. ## .. .. .. ..",
                        ".. .. .. .. .. .. .. .. ..",
                    },
                    Contents = NoContents, Spawners = NoSpawners,
                },
            };
        }

        private static string[] OpenField(int size)
        {
            var row = new string[size];
            for (int i = 0; i < size; i++)
            {
                row[i] = "..";
            }

            string line = string.Join(" ", row);

            var rows = new string[size];
            for (int y = 0; y < size; y++)
            {
                rows[y] = line;
            }

            return rows;
        }
    }
}
