using System.Collections.Generic;
using Match3.Core;
using Match3.Matching;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Matching
{
    public sealed class LegalMoveServiceTests
    {
        /// <summary>Deadlocked chips plus a rocket at (0, 0): only the two swaps of that cell are legal.</summary>
        private const string BoosterOnDeadlock = @"
            t1 t2 t3
            t4 t1 t2
            rh t4 t1";

        [Test]
        public void Moves_ListTheHorizontalSwapBeforeTheVerticalOne()
        {
            LegalMoveService service = Service(BoardFixture.From(BoosterOnDeadlock));

            IReadOnlyList<LegalMove> moves = service.Moves;
            Assert.AreEqual(2, moves.Count);
            Assert.AreEqual(new GridPos(0, 0), moves[0].A);
            Assert.AreEqual(new GridPos(1, 0), moves[0].B, "right neighbour first (§5.5)");
            Assert.AreEqual(new GridPos(0, 0), moves[1].A);
            Assert.AreEqual(new GridPos(0, 1), moves[1].B);
        }

        [Test]
        public void Moves_AreOrderedByCellYThenX()
        {
            LegalMoveService service = Service(BoardFixture.From(MatchLayouts.SwapReady));

            IReadOnlyList<LegalMove> moves = service.Moves;
            Assert.Greater(moves.Count, 0);
            for (int i = 1; i < moves.Count; i++)
            {
                LegalMove previous = moves[i - 1];
                LegalMove current = moves[i];
                Assert.LessOrEqual(GridPos.CompareYThenX(previous.A, current.A), 0);
                if (previous.A == current.A)
                {
                    Assert.AreEqual(previous.A.Right, previous.B);
                    Assert.AreEqual(current.A.Up, current.B);
                }
            }
        }

        [Test]
        public void Moves_EnumerateEveryUnorderedPairOnce()
        {
            LegalMoveService service = Service(BoardFixture.From(MatchLayouts.SwapReady));

            IReadOnlyList<LegalMove> moves = service.Moves;
            int index = IndexOf(moves, new GridPos(1, 1), new GridPos(2, 1));
            Assert.GreaterOrEqual(index, 0, "the horizontal swap of (1, 1) is legal");
            Assert.AreEqual(-1, IndexOf(moves, new GridPos(2, 1), new GridPos(1, 1)), "no mirrored duplicate");
            Assert.Greater(moves.Count, index + 1);
            Assert.AreEqual(new GridPos(1, 1), moves[index + 1].A);
            Assert.AreEqual(new GridPos(1, 2), moves[index + 1].B);
        }

        [Test]
        public void HasAnyMove_BoardWithoutLegalSwaps_IsFalse()
        {
            LegalMoveService service = Service(BoardFixture.From(MatchLayouts.NoMatch));

            Assert.IsFalse(service.HasAnyMove);
        }

        [Test]
        public void Moves_StayCachedUntilInvalidate()
        {
            BoardModel board = BoardFixture.From(MatchLayouts.NoMatch);
            LegalMoveService service = Service(board);
            Assert.IsFalse(service.HasAnyMove);

            board.SetChip(new GridPos(0, 0), ChipColor.C1);
            Assert.IsFalse(service.HasAnyMove, "the cache is not rebuilt on its own");

            service.Invalidate();
            Assert.IsTrue(service.HasAnyMove);
        }

        [Test]
        public void Moves_EnumerationLeavesTheBoardUnchanged()
        {
            BoardModel board = BoardFixture.From(MatchLayouts.SwapReady);
            LegalMoveService service = Service(board);
            ulong hash = board.ComputeHash();

            Assert.Greater(service.Moves.Count, 0);

            Assert.AreEqual(hash, board.ComputeHash());
        }

        private static LegalMoveService Service(BoardModel board)
            => new LegalMoveService(board, new SwapValidator(board, new MatchDetectionService(board)));

        private static int IndexOf(IReadOnlyList<LegalMove> moves, GridPos a, GridPos b)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].A == a && moves[i].B == b)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
