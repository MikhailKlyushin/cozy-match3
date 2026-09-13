#if MATCH3_CHEATS
using Match3.Cheats;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Cheats
{
    /// <summary>
    /// "Go" is disabled unless the typed text is a number the catalog holds. Parsing answers
    /// only the first half: existence is <c>LevelFlowRule.CanGoToLevel</c>'s call, because ids are
    /// data and need not be contiguous (§8.3).
    /// </summary>
    public sealed class CheatLevelInputTests
    {
        [TestCase("1", 1)]
        [TestCase("12", 12)]
        [TestCase(" 7 ", 7)]
        [TestCase("+4", 4)]
        [TestCase("007", 7)]
        public void ALevelNumberIsAnInteger(string text, int expected)
        {
            Assert.IsTrue(CheatLevelInput.TryParseLevelNumber(text, out int levelId));
            Assert.AreEqual(expected, levelId);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("abc")]
        [TestCase("3.5")]
        [TestCase("1 2")]
        [TestCase("99999999999999999999")]
        public void GarbageIsRejected_AndTheButtonStaysDisabled(string text)
        {
            Assert.IsFalse(CheatLevelInput.TryParseLevelNumber(text, out int levelId));
            Assert.AreEqual(0, levelId);
        }

        /// <summary>
        /// A number outside the catalog still parses: the panel asks the flow rule instead of
        /// guessing a range, so the parser must not invent an upper bound of its own.
        /// </summary>
        [Test]
        public void ANumberOutsideTheCatalogStillParses()
        {
            Assert.IsTrue(CheatLevelInput.TryParseLevelNumber("999", out int levelId));
            Assert.AreEqual(999, levelId);
        }

        [TestCase("0", 0)]
        [TestCase("-1", -1)]
        [TestCase("4242", 4242)]
        [TestCase("-2147483648", int.MinValue)]
        [TestCase("2147483647", int.MaxValue)]
        public void AnySignedIntIsALegalSeed(string text, int expected)
        {
            Assert.IsTrue(CheatLevelInput.TryParseSeed(text, out int seed));
            Assert.AreEqual(expected, seed);
        }

        [TestCase("")]
        [TestCase("seed")]
        [TestCase("2147483648")]
        public void ASeedThatIsNotAnInt_IsRejected(string text)
        {
            Assert.IsFalse(CheatLevelInput.TryParseSeed(text, out int seed));
            Assert.AreEqual(0, seed);
        }
    }
}
#endif
