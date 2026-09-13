using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Levels;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Levels
{
    public sealed class LayoutParserTests
    {
        /// <summary>Every row differs, so a wrong flip cannot pass unnoticed.</summary>
        private const string Asymmetric = @"
            t1 __ ##
            .. bx ..
            t2 .. rh";

        [Test]
        public void Parse_PutsTheFirstTextRowAtTheTopOfTheBoard()
        {
            TokenGrid grid = Parse(Asymmetric, 3, 3);

            Assert.AreEqual("t1", grid.TokenAt(0, 2), "the first text row is y = height - 1 (D01)");
            Assert.AreEqual("__", grid.TokenAt(1, 2));
            Assert.AreEqual("##", grid.TokenAt(2, 2));
        }

        [Test]
        public void Parse_PutsTheLastTextRowAtTheBottomOfTheBoard()
        {
            TokenGrid grid = Parse(Asymmetric, 3, 3);

            Assert.AreEqual("t2", grid.TokenAt(0, 0), "a second flip would land 't1' here");
            Assert.AreEqual("rh", grid.TokenAt(2, 0));
        }

        [Test]
        public void Parse_KeepsColumnsUnflipped()
        {
            TokenGrid grid = Parse(Asymmetric, 3, 3);

            Assert.AreEqual("bx", grid.TokenAt(1, 1));
            Assert.AreEqual("..", grid.TokenAt(0, 1));
        }

        [Test]
        public void Parse_ThrowsOnUnknownToken()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Parse(@"
                    .. .. ..
                    .. zz ..
                    .. .. ..", 3, 3));

            StringAssert.Contains("row 1", error.Message);
            StringAssert.Contains("y = 1", error.Message);
            StringAssert.Contains("column 1", error.Message);
            StringAssert.Contains("zz", error.Message);
        }

        [Test]
        public void Parse_ThrowsOnOneCharacterToken()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Parse(@"
                    .. .. ..
                    .. . ..
                    .. .. ..", 3, 3));

            StringAssert.Contains("column 1", error.Message);
            StringAssert.Contains("1 characters", error.Message);
        }

        [Test]
        public void Parse_ThrowsOnThreeCharacterToken()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Parse(@"
                    .. .. ..
                    .. bxx ..
                    .. .. ..", 3, 3));

            StringAssert.Contains("column 1", error.Message);
            StringAssert.Contains("3 characters", error.Message);
        }

        [Test]
        public void Parse_ThrowsOnTooManyRows()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Parse(@"
                    .. .. ..
                    .. .. ..
                    .. .. ..
                    .. .. ..", 3, 3));

            StringAssert.Contains("4 rows", error.Message);
            StringAssert.Contains("expected height 3", error.Message);
        }

        [Test]
        public void Parse_ThrowsOnTooFewRows()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Parse(@"
                    .. .. ..
                    .. .. ..", 3, 3));

            StringAssert.Contains("2 rows", error.Message);
            StringAssert.Contains("expected height 3", error.Message);
        }

        [Test]
        public void Parse_ThrowsOnShortRow()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Parse(@"
                    .. .. ..
                    .. ..
                    .. .. ..", 3, 3));

            StringAssert.Contains("row 1", error.Message);
            StringAssert.Contains("2 tokens", error.Message);
            StringAssert.Contains("expected width 3", error.Message);
        }

        [Test]
        public void Parse_ThrowsOnLongRow()
        {
            ArgumentException error = Assert.Throws<ArgumentException>(
                () => Parse(@"
                    .. .. ..
                    .. .. .. ..
                    .. .. ..", 3, 3));

            StringAssert.Contains("row 1", error.Message);
            StringAssert.Contains("4 tokens", error.Message);
        }

        [Test]
        public void TokenAt_ThrowsOutsideTheGrid()
        {
            TokenGrid grid = Parse(Asymmetric, 3, 3);

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.TokenAt(3, 0));
        }

        private static TokenGrid Parse(string layout, int width, int height)
        {
            List<string> rows = TestLevel.ReadRows(layout);
            return LayoutParser.Parse(rows, width, height, BuiltInElementCatalog.Create());
        }
    }
}
