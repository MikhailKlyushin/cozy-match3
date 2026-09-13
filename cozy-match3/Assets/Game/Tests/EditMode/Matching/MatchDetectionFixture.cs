using System.Collections.Generic;
using Match3.Core;
using Match3.Matching;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Matching
{
    /// <summary>Runs DETECT over a board built from a token layout; the D01 flip lives in BoardFixture.</summary>
    internal static class MatchDetectionFixture
    {
        /// <summary>Cascade detection: rule 1 of §4.3 does not apply (E03).</summary>
        public static PooledList<MatchComponent> Detect(string layout) => Detect(layout, GridPos.Invalid);

        public static PooledList<MatchComponent> Detect(string layout, GridPos playerSwapTarget)
            => Detect(BoardFixture.From(layout), playerSwapTarget);

        public static PooledList<MatchComponent> Detect(BoardModel board, GridPos playerSwapTarget)
        {
            var detection = new MatchDetectionService(board);
            var result = new PooledList<MatchComponent>();
            detection.Detect(result, playerSwapTarget);
            return result;
        }

        /// <summary>Asserts the exact cell list, which is ordered y-up then x-up.</summary>
        public static void AssertCells(IReadOnlyList<GridPos> actual, params GridPos[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count, "cell count");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], "cell at index " + i);
            }
        }
    }
}
