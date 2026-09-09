using Match3.Core;
using Match3.Matching;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Matching
{
    public sealed class MatchDetectionTests
    {
        [Test]
        public void Detect_LineOfThree_IsOneComponentWithoutBooster()
        {
            PooledList<MatchComponent> components = MatchDetectionFixture.Detect(MatchLayouts.Line3);

            Assert.AreEqual(1, components.Count);
            MatchComponent component = components[0];
            Assert.AreEqual(ChipColor.C1, component.Color);
            Assert.AreEqual(ComponentRank.None, component.Rank);
            Assert.AreEqual(BoosterType.None, component.Booster);
            Assert.AreEqual(GridPos.Invalid, component.SpawnCell);
            Assert.AreEqual(0, component.Index);
            MatchDetectionFixture.AssertCells(
                component.Cells,
                new GridPos(0, 1),
                new GridPos(1, 1),
                new GridPos(2, 1));
        }

        [Test]
        public void Detect_PrimitivesSharingACell_MergeIntoOneComponent()
        {
            PooledList<MatchComponent> components = MatchDetectionFixture.Detect(MatchLayouts.Plus);

            Assert.AreEqual(1, components.Count, "primitives sharing a cell form one component (D02)");
            MatchDetectionFixture.AssertCells(
                components[0].Cells,
                new GridPos(2, 1),
                new GridPos(1, 2),
                new GridPos(2, 2),
                new GridPos(3, 2),
                new GridPos(2, 3));
        }

        [Test]
        public void Detect_ChipInTwoFigures_ClearsTheWholeComponent()
        {
            PooledList<MatchComponent> components = MatchDetectionFixture.Detect(MatchLayouts.Block2x3);

            Assert.AreEqual(1, components.Count, "the chip belongs to the component, not to a figure (E02)");
            MatchDetectionFixture.AssertCells(
                components[0].Cells,
                new GridPos(0, 1),
                new GridPos(1, 1),
                new GridPos(2, 1),
                new GridPos(0, 2),
                new GridPos(1, 2),
                new GridPos(2, 2));
        }

        [Test]
        public void Detect_TwoUnconnectedComponents_CreateTwoBoosters()
        {
            PooledList<MatchComponent> components = MatchDetectionFixture.Detect(MatchLayouts.TwoComponents);

            Assert.AreEqual(2, components.Count, "one turn can create two boosters (E01)");
            Assert.AreEqual(BoosterType.RocketH, components[0].Booster);
            Assert.AreEqual(BoosterType.RocketV, components[1].Booster);
        }

        [Test]
        public void Detect_ComponentIndex_FollowsBoardTraversalOrder()
        {
            PooledList<MatchComponent> components = MatchDetectionFixture.Detect(MatchLayouts.TwoComponents);

            Assert.AreEqual(0, components[0].Index);
            Assert.AreEqual(1, components[1].Index);
            Assert.AreEqual(new GridPos(0, 0), components[0].Cells[0], "lowest cell of the first component");
            Assert.AreEqual(new GridPos(4, 1), components[1].Cells[0]);
        }

        [Test]
        public void Detect_BoosterInSlot_IsColourlessAndBreaksTheLine()
        {
            BoardModel board = BoardFixture.From(MatchLayouts.BoosterBetweenChips);
            var detection = new MatchDetectionService(board);

            Assert.IsFalse(detection.HasAnyMatch());
            Assert.AreEqual(0, MatchDetectionFixture.Detect(board, GridPos.Invalid).Count);
        }

        [Test]
        public void Detect_ClearsTheResultBuffer()
        {
            var result = new PooledList<MatchComponent>();
            new MatchDetectionService(BoardFixture.From(MatchLayouts.Line3)).Detect(result, GridPos.Invalid);
            Assert.AreEqual(1, result.Count);

            new MatchDetectionService(BoardFixture.From(MatchLayouts.NoMatch)).Detect(result, GridPos.Invalid);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void HasAnyMatch_BoardWithoutRunsOrSquares_IsFalse()
        {
            var detection = new MatchDetectionService(BoardFixture.From(MatchLayouts.NoMatch));

            Assert.IsFalse(detection.HasAnyMatch());
        }

        [Test]
        public void HasAnyMatch_LineOfThree_IsTrue()
        {
            var detection = new MatchDetectionService(BoardFixture.From(MatchLayouts.Line3));

            Assert.IsTrue(detection.HasAnyMatch());
        }

        [Test]
        public void HasAnyMatch_LoneSquare_IsTrue()
        {
            var detection = new MatchDetectionService(BoardFixture.From(MatchLayouts.LoneSquare));

            Assert.IsTrue(detection.HasAnyMatch(), "a 2x2 square is a primitive of its own (GDD §4.2)");
        }
    }
}
