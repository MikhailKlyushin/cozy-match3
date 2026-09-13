using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Core
{
    public sealed class CoreCollectionsTests
    {
        [Test]
        public void Grid_IndexerMutatesStructsInPlace()
        {
            var grid = new Grid<int>(4, 3);
            grid[new GridPos(2, 1)] = 42;

            Assert.AreEqual(42, grid[2, 1]);
            Assert.AreEqual(12, grid.Length);
        }

        [Test]
        public void Grid_ContainsRejectsOutOfBounds()
        {
            var grid = new Grid<int>(4, 3);

            Assert.IsTrue(grid.Contains(new GridPos(3, 2)));
            Assert.IsFalse(grid.Contains(new GridPos(4, 2)));
            Assert.IsFalse(grid.Contains(new GridPos(0, -1)));
        }

        [Test]
        public void Grid_PositionOfRoundTripsWithIndex()
        {
            var grid = new Grid<int>(5, 4);

            for (int i = 0; i < grid.Length; i++)
            {
                GridPos p = grid.PositionOf(i);
                grid[p] = i;
            }

            for (int i = 0; i < grid.Length; i++)
            {
                Assert.AreEqual(i, grid.AtIndex(i));
            }
        }

        [Test]
        public void PooledList_ClearKeepsCapacity()
        {
            var list = new PooledList<int>(4);
            for (int i = 0; i < 100; i++)
            {
                list.Add(i);
            }

            int capacity = list.Capacity;
            list.Clear();

            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(capacity, list.Capacity);
        }

        [Test]
        public void PooledList_RemoveAtPreservesOrder()
        {
            var list = new PooledList<int>();
            for (int i = 0; i < 5; i++)
            {
                list.Add(i);
            }

            list.RemoveAt(1);

            Assert.AreEqual(4, list.Count);
            Assert.AreEqual(0, list[0]);
            Assert.AreEqual(2, list[1]);
            Assert.AreEqual(3, list[2]);
            Assert.AreEqual(4, list[3]);
        }

        [Test]
        public void CellBuffer_AddUniqueDeduplicates()
        {
            var buffer = new CellBuffer();
            buffer.Configure(8, 8);

            Assert.IsTrue(buffer.AddUnique(new GridPos(1, 2)));
            Assert.IsFalse(buffer.AddUnique(new GridPos(1, 2)));
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void CellBuffer_SortsByYThenX()
        {
            var buffer = new CellBuffer();
            buffer.Configure(8, 8);
            buffer.Add(new GridPos(5, 3));
            buffer.Add(new GridPos(1, 3));
            buffer.Add(new GridPos(7, 0));
            buffer.Add(new GridPos(2, 0));

            buffer.SortByYThenX();

            Assert.AreEqual(new GridPos(2, 0), buffer[0]);
            Assert.AreEqual(new GridPos(7, 0), buffer[1]);
            Assert.AreEqual(new GridPos(1, 3), buffer[2]);
            Assert.AreEqual(new GridPos(5, 3), buffer[3]);
        }

        [Test]
        public void CellBuffer_ClearResetsMask()
        {
            var buffer = new CellBuffer();
            buffer.Configure(8, 8);
            buffer.AddUnique(new GridPos(4, 4));

            buffer.Clear();

            Assert.IsFalse(buffer.Contains(new GridPos(4, 4)));
            Assert.IsTrue(buffer.AddUnique(new GridPos(4, 4)));
        }

        [Test]
        public void GridPos_CompareYThenXOrdersRowsBeforeColumns()
        {
            Assert.Less(GridPos.CompareYThenX(new GridPos(9, 0), new GridPos(0, 1)), 0);
            Assert.Less(GridPos.CompareYThenX(new GridPos(1, 5), new GridPos(2, 5)), 0);
            Assert.AreEqual(0, GridPos.CompareYThenX(new GridPos(3, 3), new GridPos(3, 3)));
        }

        [Test]
        public void GridPos_OrthogonalNeighboursExcludeDiagonals()
        {
            var origin = new GridPos(4, 4);

            Assert.IsTrue(origin.IsOrthogonalNeighbourOf(new GridPos(4, 5)));
            Assert.IsTrue(origin.IsOrthogonalNeighbourOf(new GridPos(3, 4)));
            Assert.IsFalse(origin.IsOrthogonalNeighbourOf(new GridPos(5, 5)));
            Assert.IsFalse(origin.IsOrthogonalNeighbourOf(origin));
        }

        [Test]
        public void ChipColors_NextCyclingWrapsWithinColorCount()
        {
            Assert.AreEqual(ChipColor.C2, ChipColors.NextCycling(ChipColor.C1, 4));
            Assert.AreEqual(ChipColor.C1, ChipColors.NextCycling(ChipColor.C4, 4));
            Assert.AreEqual(ChipColor.C1, ChipColors.NextCycling(ChipColor.C5, 5));
        }
    }
}
