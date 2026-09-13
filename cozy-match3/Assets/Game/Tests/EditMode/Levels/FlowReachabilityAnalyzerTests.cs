using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Levels;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Levels
{
    public sealed class FlowReachabilityAnalyzerTests
    {
        private static readonly int[] NoSpawners = Array.Empty<int>();

        /// <summary>Blocker wall: the column above it feeds the cells below it (§5.3).</summary>
        private const string BlockerWallWidthThree = @"
            .. .. .. .. ..
            .. ## ## ## ..
            .. .. .. .. ..";

        /// <summary>Hole block of width 3: the cell under its centre is a dead pocket (§3.4).</summary>
        private const string HoleWidthThree = @"
            .. .. .. .. ..
            .. __ __ __ ..
            .. .. .. .. ..";

        [Test]
        public void IsReachableFromSpawner_FeedsCellUnderABlockerFromItsOwnColumn()
        {
            TokenGrid grid = Parse(@"
                .. .. ..
                .. ## ..
                .. .. ..", 3, 3);

            Assert.IsTrue(
                Analyzer().IsReachableFromSpawner(grid, NoSpawners, 1, 0),
                "§5.3: the chip above the blocker drops through it");
        }

        [Test]
        public void IsReachableFromSpawner_AcceptsCellsUnderAWideBlockerWall()
        {
            TokenGrid grid = Parse(BlockerWallWidthThree, 5, 3);
            FlowReachabilityAnalyzer analyzer = Analyzer();

            for (int x = 1; x <= 3; x++)
            {
                Assert.IsTrue(
                    analyzer.IsReachableFromSpawner(grid, NoSpawners, x, 0),
                    "a blocker wall of any width is fed through, never a dead pocket (§3.4), column " + x);
            }
        }

        [Test]
        public void IsReachableFromSpawner_ReportsCellUnderAWideHoleBlockAsUnreachable()
        {
            TokenGrid grid = Parse(HoleWidthThree, 5, 3);
            FlowReachabilityAnalyzer analyzer = Analyzer();

            Assert.IsFalse(analyzer.IsReachableFromSpawner(grid, NoSpawners, 2, 0), "centre cell has no diagonal source");
            Assert.IsTrue(analyzer.IsReachableFromSpawner(grid, NoSpawners, 1, 0), "left edge slides in from (0, 1)");
            Assert.IsTrue(analyzer.IsReachableFromSpawner(grid, NoSpawners, 3, 0), "right edge slides in from (4, 1)");
        }

        [Test]
        public void IsReachableFromSpawner_TreatsDestructibleObstacleAsPassable()
        {
            TokenGrid grid = Parse(@"
                .. .. ..
                bx bx bx
                .. .. ..", 3, 3);

            Assert.IsTrue(
                Analyzer().IsReachableFromSpawner(grid, NoSpawners, 1, 0),
                "a box blocks the flow only until it is destroyed (§3.4 rule 1)");
        }

        [Test]
        public void IsReachableFromSpawner_TreatsHoleAsAPermanentObstruction()
        {
            TokenGrid grid = Parse(@"
                .. __ ..
                .. .. ..", 3, 2);
            FlowReachabilityAnalyzer analyzer = Analyzer();

            Assert.IsFalse(analyzer.IsReachableFromSpawner(grid, NoSpawners, 1, 1), "the hole itself is not a cell");
            Assert.IsTrue(analyzer.IsReachableFromSpawner(grid, NoSpawners, 1, 0), "fed diagonally from (0, 1)");
        }

        [Test]
        public void IsReachableFromSpawner_DefaultsToEveryColumnWithATopCell()
        {
            TokenGrid grid = Parse(@"
                .. ## ..
                .. .. ..
                .. .. ..", 3, 3);
            FlowReachabilityAnalyzer analyzer = Analyzer();

            Assert.IsTrue(analyzer.IsReachableFromSpawner(grid, NoSpawners, 0, 2), "§3.3 default spawner");
            Assert.IsTrue(
                analyzer.IsReachableFromSpawner(grid, NoSpawners, 1, 1),
                "nothing sits above the top-row blocker, so the diagonal slide feeds it");
        }

        [Test]
        public void IsReachableFromSpawner_HonoursAnExplicitSpawnerList()
        {
            TokenGrid grid = Parse(@"
                .. .. ..
                .. .. ..
                .. .. ..", 3, 3);
            FlowReachabilityAnalyzer analyzer = Analyzer();

            Assert.IsTrue(analyzer.IsReachableFromSpawner(grid, new[] { 0 }, 0, 0));
            Assert.IsFalse(
                analyzer.IsReachableFromSpawner(grid, new[] { 0 }, 1, 1),
                "an open column never slides sideways: chips only fall (§5.3)");
        }

        [Test]
        public void IsReachableFromSpawner_AcceptsEveryPlayableCellOfLevel5()
        {
            LevelData level = GddLevels.Level5();
            TokenGrid grid = Parse(level, 8, 8);
            FlowReachabilityAnalyzer analyzer = Analyzer();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    string token = grid.TokenAt(x, y);
                    if (token == ElementTokens.Hole || token == ElementTokens.Blocker)
                    {
                        continue;
                    }

                    Assert.IsTrue(
                        analyzer.IsReachableFromSpawner(grid, level.Spawners, x, y),
                        "level 5 has no dead pockets (§10.3), cell " + x + ", " + y);
                }
            }
        }

        private static FlowReachabilityAnalyzer Analyzer() => new FlowReachabilityAnalyzer(BuiltInElementCatalog.Create());

        private static TokenGrid Parse(string layout, int width, int height)
        {
            List<string> rows = TestLevel.ReadRows(layout);
            return LayoutParser.Parse(rows, width, height, BuiltInElementCatalog.Create());
        }

        private static TokenGrid Parse(LevelData level, int width, int height)
            => LayoutParser.Parse(level.LayoutRows, width, height, BuiltInElementCatalog.Create());
    }
}
