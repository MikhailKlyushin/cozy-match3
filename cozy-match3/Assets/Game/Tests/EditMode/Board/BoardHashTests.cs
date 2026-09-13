using Match3.Board;
using Match3.Core;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Boards
{
    public sealed class BoardHashTests
    {
        private const string Layout = @"
            t1 t2 t3
            t2 bx t1
            t3 t1 t2";

        [Test]
        public void IdenticallyBuiltBoards_ShareTheSameHash()
        {
            Assert.AreEqual(BoardFixture.From(Layout).ComputeHash(), BoardFixture.From(Layout).ComputeHash());
        }

        [Test]
        public void ChangingAChipColor_ChangesTheHash()
        {
            BoardModel board = BoardFixture.From(Layout);
            ulong before = board.ComputeHash();

            board.SetChip(new GridPos(0, 0), ChipColor.C4);

            Assert.AreNotEqual(before, board.ComputeHash());
        }

        [Test]
        public void ClearingASlot_ChangesTheHash()
        {
            BoardModel board = BoardFixture.From(Layout);
            ulong before = board.ComputeHash();

            board.ClearSlot(new GridPos(2, 2));

            Assert.AreNotEqual(before, board.ComputeHash());
        }

        [Test]
        public void DamagingAnObstacle_ChangesTheHash()
        {
            BoardModel board = BoardFixture.From(Layout);
            board.TryGetElement(new GridPos(1, 1), out ElementInstance element);
            ulong before = board.ComputeHash();

            element.Health -= 1;

            Assert.AreNotEqual(before, board.ComputeHash());
        }

        [Test]
        public void CyclingAnObstacleColor_ChangesTheHash()
        {
            BoardModel board = BoardFixture.From(@"
                cx ..
                .. ..");
            board.TryGetElement(new GridPos(0, 1), out ElementInstance element);
            ulong before = board.ComputeHash();

            element.CurrentColor = ChipColor.C3;

            Assert.AreNotEqual(before, board.ComputeHash());
        }

        [Test]
        public void PlacingABooster_ChangesTheHash()
        {
            BoardModel board = BoardFixture.From(Layout);
            ulong before = board.ComputeHash();

            board.SetBooster(new GridPos(0, 0), BoosterType.Bomb);

            Assert.AreNotEqual(before, board.ComputeHash());
        }

        [Test]
        public void NestedContent_ParticipatesInTheHash()
        {
            BoardModel plain = BoardFixture.From(@"
                bx ..
                .. ..");
            BoardModel nested = BoardFixture.From(@"
                bx ..
                .. ..");

            BoardFixture.Nest(nested, new GridPos(0, 1), ElementTokens.Box1);

            Assert.AreNotEqual(plain.ComputeHash(), nested.ComputeHash());
        }

        [Test]
        public void InstanceIdAndConsumedFlag_DoNotAffectTheHash()
        {
            BoardModel board = BoardFixture.From(Layout);
            ulong before = board.ComputeHash();

            board.SetChip(new GridPos(0, 0), board.GetSlot(new GridPos(0, 0)).Color, instanceId: 9999);
            board.SetConsumed(new GridPos(0, 0), true);

            Assert.AreEqual(before, board.ComputeHash(), "hash must describe rule state, not visual identity");
        }

        [Test]
        public void SwappingTwoDifferentChips_ChangesTheHash()
        {
            BoardModel board = BoardFixture.From(Layout);
            ulong before = board.ComputeHash();

            board.SwapSlots(new GridPos(0, 0), new GridPos(1, 0));

            Assert.AreNotEqual(before, board.ComputeHash());
        }

        [Test]
        public void ResetConsumedFlags_ClearsEveryCell()
        {
            BoardModel board = BoardFixture.From(@"
                rh rv
                bm rb");
            board.SetConsumed(new GridPos(0, 0), true);
            board.SetConsumed(new GridPos(1, 1), true);

            board.ResetConsumedFlags();

            Assert.IsFalse(board.GetSlot(new GridPos(0, 0)).Consumed);
            Assert.IsFalse(board.GetSlot(new GridPos(1, 1)).Consumed);
        }
    }
}
