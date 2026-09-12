using Match3.Board;
using Match3.Core;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Boards
{
    public sealed class BoardTests
    {
        [Test]
        public void Blocker_IsImmovableFallenThroughAndUndamaged()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. ## ..
                .. .. ..");
            var blocker = new GridPos(1, 1);

            Assert.IsFalse(board.IsMovable(blocker));
            Assert.IsTrue(
                board.IsPassableForFall(blocker),
                "§5.3: the blocker takes the cell but a fall drops through it");
            Assert.IsTrue(board.IsFallThrough(blocker));
            Assert.IsTrue(board.TryGetElement(blocker, out ElementInstance element));
            Assert.IsTrue(element.IsIndestructible);
            Assert.AreEqual(DamageSourceKind.None, board.Catalog.Get(element.Definition).DamageSource);
        }

        [Test]
        public void Hole_IsNotPassableAndNotPlayable()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. __ ..
                .. .. ..");
            var hole = new GridPos(1, 1);

            Assert.AreEqual(CellKind.Hole, board.GetKind(hole));
            Assert.IsFalse(board.IsPassableForFall(hole));
            Assert.IsFalse(board.IsMovable(hole));
        }

        [Test]
        public void LiveObstacle_BlocksFall_DeadOneDoesNot()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. bx ..
                .. .. ..");
            var cell = new GridPos(1, 1);

            Assert.IsFalse(board.IsPassableForFall(cell), "live obstacle must block");

            board.TryGetElement(cell, out ElementInstance element);
            element.Health = 0;
            Assert.IsTrue(board.IsPassableForFall(cell), "dead obstacle must not block (E10)");
        }

        [Test]
        public void DetachedObstacleCell_IsPassableInTheSameStep()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. bx ..
                .. .. ..");
            var cell = new GridPos(1, 1);

            board.DetachElement(cell);

            Assert.IsTrue(board.IsPassableForFall(cell));
        }

        [Test]
        public void EmptyPlayableCell_IsPassableButNotMovable()
        {
            BoardModel board = BoardFixture.From(@"
                .. ..
                .. ..");
            var cell = new GridPos(0, 0);

            Assert.IsTrue(board.IsPassableForFall(cell));
            Assert.IsFalse(board.IsMovable(cell));
        }

        [Test]
        public void ChipAndBooster_AreMovable()
        {
            BoardModel board = BoardFixture.From(@"
                t1 rh
                .. ..");

            Assert.IsTrue(board.IsMovable(new GridPos(0, 1)));
            Assert.IsTrue(board.IsMovable(new GridPos(1, 1)));
            Assert.AreEqual(BoosterType.RocketH, board.GetSlot(new GridPos(1, 1)).Booster);
        }

        [Test]
        public void OutOfBoundsQueries_AreSafe()
        {
            BoardModel board = BoardFixture.From(@"
                .. ..
                .. ..");

            Assert.IsFalse(board.Contains(new GridPos(2, 0)));
            Assert.IsFalse(board.IsPassableForFall(new GridPos(-1, 0)));
            Assert.IsFalse(board.IsMovable(new GridPos(0, 9)));
            Assert.IsFalse(board.IsSpawnerColumn(-1));
            Assert.IsFalse(board.IsSpawnerColumn(2));
        }

        [Test]
        public void LayoutFirstRow_LandsOnTopRow()
        {
            BoardModel board = BoardFixture.From(@"
                t1 .. ..
                .. .. ..
                .. .. t2");

            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 2)).Color);
            Assert.AreEqual(ChipColor.C2, board.GetSlot(new GridPos(2, 0)).Color);
        }

        [Test]
        public void DefaultSpawners_SkipHolesAndBlockersInTheTopRow()
        {
            BoardModel board = BoardFixture.From(@"
                .. __ ## ..
                .. .. .. ..
                .. .. .. ..");

            Assert.IsTrue(board.IsSpawnerColumn(0));
            Assert.IsFalse(board.IsSpawnerColumn(1), "hole in the top row");
            Assert.IsFalse(board.IsSpawnerColumn(2), "blocker in the top row");
            Assert.IsTrue(board.IsSpawnerColumn(3));
        }

        [Test]
        public void DefaultSpawners_AllowDestructibleObstacleInTheTopRow()
        {
            BoardModel board = BoardFixture.From(@"
                bx ..
                .. ..");

            Assert.IsTrue(board.IsSpawnerColumn(0), "a destructible box is a temporary obstruction");
        }

        [Test]
        public void ExplicitSpawnerList_OverridesTheDefault()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. .. ..
                .. .. .. ..");

            board.SetSpawnerColumns(new[] { 0, 3 });

            Assert.IsTrue(board.IsSpawnerColumn(0));
            Assert.IsFalse(board.IsSpawnerColumn(1));
            Assert.IsFalse(board.IsSpawnerColumn(2));
            Assert.IsTrue(board.IsSpawnerColumn(3));
        }

        [Test]
        public void InstanceIds_AreUniqueAndDeterministic()
        {
            BoardModel first = BoardFixture.From(@"
                t1 t2
                t3 t4");
            BoardModel second = BoardFixture.From(@"
                t1 t2
                t3 t4");

            Assert.AreEqual(
                first.GetSlot(new GridPos(0, 1)).InstanceId,
                second.GetSlot(new GridPos(0, 1)).InstanceId);
            Assert.AreNotEqual(
                first.GetSlot(new GridPos(0, 1)).InstanceId,
                first.GetSlot(new GridPos(1, 1)).InstanceId);
        }

        [Test]
        public void MoveSlot_PreservesInstanceId()
        {
            BoardModel board = BoardFixture.From(@"
                t1 ..
                .. ..");
            var from = new GridPos(0, 1);
            var to = new GridPos(0, 0);
            int instanceId = board.GetSlot(from).InstanceId;

            board.MoveSlot(from, to);

            Assert.AreEqual(instanceId, board.GetSlot(to).InstanceId);
            Assert.AreEqual(SlotKind.Empty, board.GetSlot(from).Kind);
        }

        [Test]
        public void TransformToBooster_KeepsInstanceId()
        {
            BoardModel board = BoardFixture.From(@"
                t1 ..
                .. ..");
            var cell = new GridPos(0, 1);
            int instanceId = board.GetSlot(cell).InstanceId;

            board.TransformToBooster(cell, BoosterType.Bomb);

            ChipSlot slot = board.GetSlot(cell);
            Assert.AreEqual(instanceId, slot.InstanceId);
            Assert.AreEqual(SlotKind.Booster, slot.Kind);
            Assert.AreEqual(BoosterType.Bomb, slot.Booster);
        }

        [Test]
        public void Neighbours_AreClippedToTheBoard()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. .. ..
                .. .. ..");
            var buffer = new GridPos[4];

            Assert.AreEqual(4, board.GetOrthogonalNeighbours(new GridPos(1, 1), buffer));
            Assert.AreEqual(2, board.GetOrthogonalNeighbours(new GridPos(0, 0), buffer));
            Assert.AreEqual(4, board.GetDiagonalNeighbours(new GridPos(1, 1), buffer));
            Assert.AreEqual(1, board.GetDiagonalNeighbours(new GridPos(0, 0), buffer));
        }

        [Test]
        public void Nesting_ChainsUpToDepthThree()
        {
            BoardModel board = BoardFixture.From(@"
                bx ..
                .. ..");
            var cell = new GridPos(0, 1);

            BoardFixture.Nest(board, cell, ElementTokens.Box1);
            BoardFixture.Nest(board, cell, "c2");

            board.TryGetElement(cell, out ElementInstance outer);
            Assert.AreNotEqual(ElementInstance.NoNested, outer.NestedIndex);

            ElementInstance middle = board.ElementAt(outer.NestedIndex);
            Assert.AreNotEqual(ElementInstance.NoNested, middle.NestedIndex);

            ElementInstance inner = board.ElementAt(middle.NestedIndex);
            Assert.AreEqual(ElementInstance.NoNested, inner.NestedIndex);
            Assert.AreEqual(ChipColor.C2, inner.CurrentColor);
        }

        [Test]
        public void Immunity_IsScopedToAStep()
        {
            var element = new ElementInstance(new ElementId(1), 1, ChipColor.None, ElementInstance.NoNested);

            Assert.IsFalse(element.IsImmuneAtStep(0));

            element.ImmuneUntilStep = 2;
            Assert.IsTrue(element.IsImmuneAtStep(2));
            Assert.IsFalse(element.IsImmuneAtStep(3));
        }
    }
}
