using Match3.Board;
using Match3.Core;
using Match3.Levels;
using Match3.Matching;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Levels
{
    public sealed class BoardBuilderTests
    {
        private const int SeedCount = 100;

        private const string Open8X8 = @"
            .. .. .. .. .. .. .. ..
            .. .. .. .. .. .. .. ..
            .. .. .. .. .. .. .. ..
            .. .. .. .. .. .. .. ..
            .. .. .. .. .. .. .. ..
            .. .. .. .. .. .. .. ..
            .. .. .. .. .. .. .. ..
            .. .. .. .. .. .. .. ..";

        /// <summary>
        /// No two orthogonal neighbours share a colour, so no swap can ever create a match: the
        /// generation loop runs out of attempts and the repair pass has to fix it (E20).
        /// </summary>
        private const string Deadlocked5X5 = @"
            t1 t2 t3 t4 t1
            t2 t3 t4 t1 t2
            t3 t4 t1 t2 t3
            t4 t1 t2 t3 t4
            t1 t2 t3 t4 t1";

        private const string NestingLayout = @"
            .. .. .. ..
            .. bx .. ..
            .. .. .. ..
            .. .. .. ..";

        [Test]
        public void Build_KeepsTheFirstLayoutRowAtTheTopOfTheBoard()
        {
            BoardModel board = Build(TestLevel.From(@"
                t1 t2 t3
                .. .. ..
                bx .. .."));

            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 2)).Color, "first row is y = height - 1 (D01)");
            Assert.IsFalse(board.TryGetElement(new GridPos(0, 2), out ElementInstance _), "no box at the top");
            Assert.IsTrue(board.TryGetElement(new GridPos(0, 0), out ElementInstance _), "the box stays at the bottom");
        }

        [Test]
        public void Build_LeavesNoReadyMadeMatchForAnySeed()
        {
            var builder = new BoardBuilder(BuiltInElementCatalog.Create(), NullLogger.Instance);
            LevelData level = TestLevel.From(Open8X8, colorCount: 5);

            for (int seed = 0; seed < SeedCount; seed++)
            {
                BoardModel board = builder.Build(level, new DeterministicRandom(seed));
                Assert.IsFalse(new MatchDetectionService(board).HasAnyMatch(), "seed " + seed + " produced a match");
            }
        }

        [Test]
        public void Build_LeavesALegalMoveForAnySeed()
        {
            var builder = new BoardBuilder(BuiltInElementCatalog.Create(), NullLogger.Instance);
            LevelData level = TestLevel.From(Open8X8, colorCount: 5);

            for (int seed = 0; seed < SeedCount; seed++)
            {
                BoardModel board = builder.Build(level, new DeterministicRandom(seed));
                Assert.IsTrue(HasLegalMove(board), "seed " + seed + " produced a deadlocked board");
            }
        }

        [Test]
        public void Build_LeavesNoReadyMatchAndALegalMoveOnLevel5ForAnySeed()
        {
            var builder = new BoardBuilder(BuiltInElementCatalog.Create(), NullLogger.Instance);
            LevelData level = GddLevels.Level5();

            for (int seed = 0; seed < SeedCount; seed++)
            {
                BoardModel board = builder.Build(level, new DeterministicRandom(seed));
                Assert.IsFalse(new MatchDetectionService(board).HasAnyMatch(), "seed " + seed + " produced a match");
                Assert.IsTrue(HasLegalMove(board), "seed " + seed + " produced a deadlocked board");
            }
        }

        [Test]
        public void Build_IsDeterministicForTheSameSeed()
        {
            LevelData level = TestLevel.From(Open8X8, colorCount: 5);

            ulong first = Build(level, 4242).ComputeHash();
            ulong second = Build(level, 4242).ComputeHash();

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Build_KeepsExplicitChipColors()
        {
            LevelData level = TestLevel.From(@"
                .. .. .. ..
                .. t3 .. ..
                .. .. .. ..
                .. .. .. ..");

            for (int seed = 0; seed < 8; seed++)
            {
                BoardModel board = Build(level, seed);
                Assert.AreEqual(ChipColor.C3, board.GetSlot(new GridPos(1, 2)).Color, "t3 is authored, never re-rolled");
            }
        }

        [Test]
        public void Build_StartsBoosterTokensAsBoosters()
        {
            BoardModel board = Build(TestLevel.From(@"
                .. .. .. ..
                .. rh .. bm
                .. .. .. ..
                .. .. .. .."));

            ChipSlot rocket = board.GetSlot(new GridPos(1, 2));
            Assert.AreEqual(SlotKind.Booster, rocket.Kind);
            Assert.AreEqual(BoosterType.RocketH, rocket.Booster);
            Assert.AreEqual(BoosterType.Bomb, board.GetSlot(new GridPos(3, 2)).Booster);
        }

        [Test]
        public void Build_NestsElementsFromTheContentsList()
        {
            LevelData level = TestLevel.From(
                NestingLayout,
                contents: new[] { new NestedContent(1, 2, "bx"), new NestedContent(1, 2, "b2") });

            BoardModel board = Build(level);

            Assert.IsTrue(board.TryGetElement(new GridPos(1, 2), out ElementInstance outer));
            Assert.AreEqual(ElementTokens.Box1, board.Catalog.Get(outer.Definition).Token);
            Assert.AreNotEqual(ElementInstance.NoNested, outer.NestedIndex);

            ElementInstance middle = board.ElementAt(outer.NestedIndex);
            Assert.AreEqual(ElementTokens.Box1, board.Catalog.Get(middle.Definition).Token, "contents order is outer to inner");

            ElementInstance inner = board.ElementAt(middle.NestedIndex);
            Assert.AreEqual(ElementTokens.Box2, board.Catalog.Get(inner.Definition).Token);
            Assert.AreEqual(ElementInstance.NoNested, inner.NestedIndex);
        }

        [Test]
        public void Build_LeavesGridElementsUnnestedWithoutContents()
        {
            BoardModel board = Build(TestLevel.From(NestingLayout));

            Assert.IsTrue(board.TryGetElement(new GridPos(1, 2), out ElementInstance outer));
            Assert.AreEqual(ElementInstance.NoNested, outer.NestedIndex, "the grid never encodes nesting (§10.2)");
        }

        [Test]
        public void Build_RepairsADeadlockedLayoutBySwappingTwoChips()
        {
            BoardModel board = Build(TestLevel.From(Deadlocked5X5));

            Assert.IsFalse(new MatchDetectionService(board).HasAnyMatch());
            Assert.IsTrue(HasLegalMove(board), "the repair pass must produce a legal move (E20)");
            Assert.GreaterOrEqual(
                CountChipsDifferingFromLayout(board, Deadlocked5X5),
                2,
                "the authored layout is deadlocked, so the repair pass must have swapped chips");
        }

        [Test]
        public void Build_RepairPassPreservesTheAuthoredColors()
        {
            BoardModel board = Build(TestLevel.From(Deadlocked5X5));

            Assert.AreEqual(7, CountColor(board, ChipColor.C1), "the repair swaps chips, it never recolours them");
            Assert.AreEqual(6, CountColor(board, ChipColor.C2));
            Assert.AreEqual(6, CountColor(board, ChipColor.C3));
            Assert.AreEqual(6, CountColor(board, ChipColor.C4));
        }

        [Test]
        public void Build_DrawsBoardColorsBeforeTheCyclingBoxColor()
        {
            LevelData level = TestLevel.From(@"
                t1 t2 t3 cx
                t2 t1 t1 t4
                t3 t4 t2 ..");
            var random = new ScriptedRandom(0, 3);

            BoardModel board = new BoardBuilder(BuiltInElementCatalog.Create(), NullLogger.Instance)
                .Build(level, random);

            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(3, 0)).Color, "the first draw fills the board (§13)");
            Assert.IsTrue(board.TryGetElement(new GridPos(3, 2), out ElementInstance cycling));
            Assert.AreEqual(ChipColor.C4, cycling.CurrentColor, "the cx phase comes after board generation (§13)");
            Assert.AreEqual(2, random.Draws);
        }

        [Test]
        public void Build_UsesTheDefaultSpawnersWhenTheListIsEmpty()
        {
            BoardModel board = Build(TestLevel.From(@"
                .. ## ..
                .. .. ..
                .. .. .."));

            Assert.IsTrue(board.IsSpawnerColumn(0));
            Assert.IsFalse(board.IsSpawnerColumn(1), "the blocker on top removes the spawner (§3.3)");
            Assert.IsTrue(board.IsSpawnerColumn(2));
        }

        [Test]
        public void Build_HonoursAnExplicitSpawnerList()
        {
            BoardModel board = Build(TestLevel.From(
                @"
                .. .. ..
                .. .. ..
                .. .. ..",
                spawners: new[] { 1 }));

            Assert.IsFalse(board.IsSpawnerColumn(0));
            Assert.IsTrue(board.IsSpawnerColumn(1));
            Assert.IsFalse(board.IsSpawnerColumn(2));
        }

        private static BoardModel Build(LevelData level, int seed = 20260909)
        {
            var builder = new BoardBuilder(BuiltInElementCatalog.Create(), NullLogger.Instance);
            return builder.Build(level, new DeterministicRandom(seed));
        }

        private static bool HasLegalMove(BoardModel board)
        {
            var detection = new MatchDetectionService(board);
            return new LegalMoveService(board, new SwapValidator(board, detection)).HasAnyMove;
        }

        /// <summary>Only valid for layouts made of t1..t6 tokens.</summary>
        private static int CountChipsDifferingFromLayout(BoardModel board, string layout)
        {
            TokenGrid grid = LayoutParser.Parse(
                TestLevel.ReadRows(layout),
                board.Width,
                board.Height,
                board.Catalog);

            int different = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    int authored = grid.TokenAt(x, y)[1] - '0';
                    if (ChipColors.ToIndex(board.GetSlot(new GridPos(x, y)).Color) != authored)
                    {
                        different++;
                    }
                }
            }

            return different;
        }

        private static int CountColor(BoardModel board, ChipColor color)
        {
            int count = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.GetSlot(new GridPos(x, y)).Color == color)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
