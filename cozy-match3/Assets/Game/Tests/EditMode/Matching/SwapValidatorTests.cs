using Match3.Board;
using Match3.Core;
using Match3.Matching;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Matching
{
    public sealed class SwapValidatorTests
    {
        /// <summary>A rocket sits at (0, 0), so the swap with (1, 0) needs no match.</summary>
        private const string BoosterAtOrigin = @"
            t3 t2 t3 t2
            t1 t2 t1 t3
            t2 t1 t2 t1
            rh t2 t3 t2";

        /// <summary>A box occupies (1, 0): the cell holds nothing movable.</summary>
        private const string ObstacleAtNeighbour = @"
            t3 t2 t3 t2
            t1 t2 t1 t3
            t2 t1 t2 t1
            t1 bx t3 t2";

        private const string HoleAtNeighbour = @"
            t3 t2 t3 t2
            t1 t2 t1 t3
            t2 t1 t2 t1
            t1 __ t3 t2";

        private const string EmptyAtNeighbour = @"
            t3 t2 t3 t2
            t1 t2 t1 t3
            t2 t1 t2 t1
            t1 .. t3 t2";

        [Test]
        public void IsLegal_SwapCreatingALine_IsLegal()
        {
            SwapValidator validator = Validator(BoardFixture.From(MatchLayouts.SwapReady));

            Assert.IsTrue(validator.IsLegal(new GridPos(1, 1), new GridPos(1, 2)));
            Assert.IsTrue(validator.IsLegal(new GridPos(1, 1), new GridPos(2, 1)));
        }

        [Test]
        public void IsLegal_SwapWithoutMatchOrBooster_IsIllegal()
        {
            SwapValidator validator = Validator(BoardFixture.From(MatchLayouts.SwapReady));

            Assert.IsFalse(validator.IsLegal(new GridPos(0, 0), new GridPos(1, 0)));
        }

        [Test]
        public void IsLegal_BoosterInOneCell_IsLegalWithoutAMatch()
        {
            SwapValidator validator = Validator(BoardFixture.From(BoosterAtOrigin));

            Assert.IsTrue(validator.IsLegal(new GridPos(0, 0), new GridPos(1, 0)));
            Assert.IsFalse(
                validator.CreatesMatch(new GridPos(0, 0), new GridPos(1, 0)),
                "the hint must see that no match is created (§5.5)");
        }

        [Test]
        public void IsLegal_CellWithAnObstacle_IsIllegal()
        {
            SwapValidator validator = Validator(BoardFixture.From(ObstacleAtNeighbour));

            Assert.IsFalse(validator.IsLegal(new GridPos(0, 0), new GridPos(1, 0)));
        }

        [Test]
        public void IsLegal_CellWithAHole_IsIllegal()
        {
            SwapValidator validator = Validator(BoardFixture.From(HoleAtNeighbour));

            Assert.IsFalse(validator.IsLegal(new GridPos(0, 0), new GridPos(1, 0)));
        }

        [Test]
        public void IsLegal_EmptyCell_IsIllegal()
        {
            SwapValidator validator = Validator(BoardFixture.From(EmptyAtNeighbour));

            Assert.IsFalse(validator.IsLegal(new GridPos(0, 0), new GridPos(1, 0)));
        }

        [Test]
        public void IsLegal_CellsThatAreNotNeighbours_IsIllegal()
        {
            SwapValidator validator = Validator(BoardFixture.From(MatchLayouts.SwapReady));

            Assert.IsFalse(validator.IsLegal(new GridPos(0, 0), new GridPos(2, 0)));
            Assert.IsFalse(validator.IsLegal(new GridPos(1, 1), new GridPos(1, 1)));
        }

        [Test]
        public void IsLegal_CellOutsideTheBoard_IsIllegal()
        {
            SwapValidator validator = Validator(BoardFixture.From(MatchLayouts.SwapReady));

            Assert.IsFalse(validator.IsLegal(new GridPos(0, 0), new GridPos(-1, 0)));
        }

        [Test]
        public void IsLegal_LeavesTheBoardExactlyAsItWas()
        {
            BoardModel board = BoardFixture.From(MatchLayouts.SwapReady);
            SwapValidator validator = Validator(board);
            ulong hash = board.ComputeHash();
            ChipSlot before = board.GetSlot(new GridPos(1, 1));

            validator.IsLegal(new GridPos(1, 1), new GridPos(1, 2));
            validator.IsLegal(new GridPos(0, 0), new GridPos(1, 0));
            validator.CreatesMatch(new GridPos(1, 1), new GridPos(2, 1));

            Assert.AreEqual(hash, board.ComputeHash());
            ChipSlot after = board.GetSlot(new GridPos(1, 1));
            Assert.AreEqual(before.Color, after.Color);
            Assert.AreEqual(before.InstanceId, after.InstanceId, "the probe must not renumber chips");
        }

        private static SwapValidator Validator(BoardModel board)
            => new SwapValidator(board, new MatchDetectionService(board));
    }
}
