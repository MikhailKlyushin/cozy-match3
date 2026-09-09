using Match3.Core;
using Match3.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Match3.Tests.EditMode.Gameplay
{
    /// <summary>Swipe geometry of §5.1 input and D03: drag to swap, short drag to tap.</summary>
    public sealed class SwipeDirectionTests
    {
        private const float Threshold = 20f;

        private static readonly GridPos Origin = new GridPos(3, 4);

        [Test]
        public void DragShorterThanThreshold_IsTap()
        {
            Assert.IsFalse(
                SwipeDirection.TryResolveSwap(Origin, new Vector2(Threshold - 1f, 0f), Threshold, out GridPos target),
                "A drag below the threshold must stay a tap (D03)");
            Assert.AreEqual(GridPos.Invalid, target);
        }

        [Test]
        public void ZeroDrag_IsTap_EvenWithoutThreshold()
        {
            Assert.IsFalse(SwipeDirection.TryResolveSwap(Origin, Vector2.zero, 0f, out GridPos target));
            Assert.AreEqual(GridPos.Invalid, target);
        }

        [Test]
        public void DragAtExactlyThreshold_IsSwap()
        {
            Assert.IsTrue(
                SwipeDirection.TryResolveSwap(Origin, new Vector2(Threshold, 0f), Threshold, out GridPos target));
            Assert.AreEqual(Origin.Right, target);
        }

        [Test]
        public void DragFromInvalidCell_IsNeverASwap()
        {
            Assert.IsFalse(
                SwipeDirection.TryResolveSwap(GridPos.Invalid, new Vector2(100f, 0f), Threshold, out GridPos target));
            Assert.AreEqual(GridPos.Invalid, target);
        }

        [TestCase(100f, 0f, 4, 4, TestName = "Right")]
        [TestCase(-100f, 0f, 2, 4, TestName = "Left")]
        [TestCase(0f, 100f, 3, 5, TestName = "Up")]
        [TestCase(0f, -100f, 3, 3, TestName = "Down")]
        public void LongDrag_ResolvesToTheAxisNeighbour(float dx, float dy, int expectedX, int expectedY)
        {
            Assert.IsTrue(
                SwipeDirection.TryResolveSwap(Origin, new Vector2(dx, dy), Threshold, out GridPos target));
            Assert.AreEqual(new GridPos(expectedX, expectedY), target);
        }

        [TestCase(100f, 60f, 4, 4, TestName = "UpRightDragGoesRight")]
        [TestCase(60f, 100f, 3, 5, TestName = "UpRightDragGoesUp")]
        [TestCase(-100f, -60f, 2, 4, TestName = "DownLeftDragGoesLeft")]
        [TestCase(-60f, -100f, 3, 3, TestName = "DownLeftDragGoesDown")]
        [TestCase(100f, -60f, 4, 4, TestName = "DownRightDragGoesRight")]
        public void DiagonalDrag_ResolvesToTheDominantAxis(float dx, float dy, int expectedX, int expectedY)
        {
            Assert.IsTrue(
                SwipeDirection.TryResolveSwap(Origin, new Vector2(dx, dy), Threshold, out GridPos target));
            Assert.AreEqual(new GridPos(expectedX, expectedY), target);
        }

        /// <summary>A tie is resolved horizontally, matching the §5.5 tie-break order.</summary>
        [Test]
        public void ExactDiagonal_ResolvesHorizontally()
        {
            Assert.IsTrue(
                SwipeDirection.TryResolveSwap(Origin, new Vector2(100f, 100f), Threshold, out GridPos target));
            Assert.AreEqual(Origin.Right, target);
        }

        [Test]
        public void AnyDragDirection_YieldsExactlyOneOrthogonalNeighbour()
        {
            for (int degrees = 0; degrees < 360; degrees++)
            {
                float radians = degrees * Mathf.Deg2Rad;
                var delta = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 100f;

                Assert.IsTrue(
                    SwipeDirection.TryResolveSwap(Origin, delta, Threshold, out GridPos target),
                    "A 100 px drag is always a swap");
                Assert.IsTrue(
                    target.IsOrthogonalNeighbourOf(Origin),
                    "Drag at " + degrees.ToString() + " deg resolved to " + target.ToString());
            }
        }

        [Test]
        public void NeighbourOutsideTheBoard_IsRejected()
        {
            SwipeDirection.TryResolveSwap(new GridPos(0, 0), new Vector2(-100f, 0f), Threshold, out GridPos target);

            Assert.AreEqual(new GridPos(-1, 0), target);
            Assert.IsFalse(SwipeDirection.IsInsideBoard(target, 7, 7));
        }

        [Test]
        public void NeighbourInsideTheBoard_IsAccepted()
        {
            SwipeDirection.TryResolveSwap(new GridPos(0, 0), new Vector2(0f, 100f), Threshold, out GridPos target);

            Assert.AreEqual(new GridPos(0, 1), target);
            Assert.IsTrue(SwipeDirection.IsInsideBoard(target, 7, 7));
        }

        [Test]
        public void TopEdgeDragUp_IsRejected()
        {
            SwipeDirection.TryResolveSwap(new GridPos(3, 6), new Vector2(0f, 100f), Threshold, out GridPos target);

            Assert.AreEqual(new GridPos(3, 7), target);
            Assert.IsFalse(SwipeDirection.IsInsideBoard(target, 7, 7));
        }
    }
}
