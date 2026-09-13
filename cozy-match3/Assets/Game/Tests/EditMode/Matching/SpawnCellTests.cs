using Match3.Core;
using Match3.Matching;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Matching
{
    public sealed class SpawnCellTests
    {
        [Test]
        public void SpawnCell_PlayerSwapTarget_WinsOverTheCrossing()
        {
            MatchComponent component = Single(MatchLayouts.TShape, new GridPos(3, 2));

            Assert.AreEqual(new GridPos(3, 2), component.SpawnCell, "rule 1 of §4.3");
        }

        [Test]
        public void SpawnCell_InCascade_IgnoresTheSwapTargetRule()
        {
            MatchComponent component = Single(MatchLayouts.TShape, GridPos.Invalid);

            Assert.AreEqual(new GridPos(1, 2), component.SpawnCell, "rule 1 is skipped in a cascade (E03)");
        }

        [Test]
        public void SpawnCell_SwapTargetOutsideTheComponent_UsesTheNextRule()
        {
            MatchComponent component = Single(MatchLayouts.TShape, new GridPos(4, 0));

            Assert.AreEqual(new GridPos(1, 2), component.SpawnCell);
        }

        [Test]
        public void SpawnCell_Bomb_UsesTheLowestCrossing()
        {
            MatchComponent component = Single(MatchLayouts.DoubleCrossing, GridPos.Invalid);

            Assert.AreEqual(ComponentRank.Bomb, component.Rank);
            Assert.AreEqual(new GridPos(0, 2), component.SpawnCell, "lowest y, then lowest x");
        }

        [Test]
        public void SpawnCell_Airplane_UsesTheBottomLeftCellOfTheSquare()
        {
            MatchComponent lone = Single(MatchLayouts.LoneSquare, GridPos.Invalid);
            MatchComponent block = Single(MatchLayouts.Block2x3, GridPos.Invalid);

            Assert.AreEqual(new GridPos(1, 1), lone.SpawnCell);
            Assert.AreEqual(new GridPos(0, 1), block.SpawnCell, "leftmost square wins the tie");
        }

        [Test]
        public void SpawnCell_Rainbow_UsesTheMedianCell()
        {
            MatchComponent component = Single(MatchLayouts.Line6, GridPos.Invalid);

            Assert.AreEqual(new GridPos(2, 1), component.SpawnCell, "index floor((n - 1) / 2)");
        }

        [Test]
        public void SpawnCell_Rocket_UsesTheMedianCell()
        {
            MatchComponent component = Single(MatchLayouts.Line4Horizontal, GridPos.Invalid);

            Assert.AreEqual(new GridPos(1, 1), component.SpawnCell);
        }

        [Test]
        public void SpawnCell_RankNone_IsInvalidEvenOnTheSwapTarget()
        {
            MatchComponent component = Single(MatchLayouts.Line3, new GridPos(1, 1));

            Assert.AreEqual(ComponentRank.None, component.Rank);
            Assert.AreEqual(GridPos.Invalid, component.SpawnCell);
        }

        [Test]
        public void SpawnCell_TwoComponents_DoNotShareTheSameCell()
        {
            PooledList<MatchComponent> components =
                MatchDetectionFixture.Detect(MatchLayouts.TwoComponents, GridPos.Invalid);

            Assert.AreEqual(2, components.Count);
            Assert.AreEqual(new GridPos(1, 0), components[0].SpawnCell);
            Assert.AreEqual(new GridPos(4, 2), components[1].SpawnCell);
            Assert.AreNotEqual(components[0].SpawnCell, components[1].SpawnCell);
        }

        private static MatchComponent Single(string layout, GridPos playerSwapTarget)
        {
            PooledList<MatchComponent> components = MatchDetectionFixture.Detect(layout, playerSwapTarget);
            Assert.AreEqual(1, components.Count, "layout must contain exactly one component");
            return components[0];
        }
    }
}
