using Match3.Core;
using Match3.Matching;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Matching
{
    public sealed class ComponentClassificationTests
    {
        [Test]
        public void Classify_LineOfSix_IsRainbowAndClearsAllSixCells()
        {
            MatchComponent component = Single(MatchLayouts.Line6);

            Assert.AreEqual(ComponentRank.Rainbow, component.Rank);
            Assert.AreEqual(BoosterType.Rainbow, component.Booster);
            Assert.AreEqual(6, component.Cells.Count);
        }

        [Test]
        public void Classify_LineOfFive_IsRainbow()
        {
            MatchComponent component = Single(MatchLayouts.Line5);

            Assert.AreEqual(ComponentRank.Rainbow, component.Rank);
            Assert.AreEqual(BoosterType.Rainbow, component.Booster);
            Assert.AreEqual(5, component.Cells.Count);
        }

        [Test]
        public void Classify_LineOfFourCrossedByLineOfThree_IsBombNotRocket()
        {
            MatchComponent component = Single(MatchLayouts.TShape);

            Assert.AreEqual(ComponentRank.Bomb, component.Rank);
            Assert.AreEqual(BoosterType.Bomb, component.Booster);
            Assert.AreEqual(6, component.Cells.Count);
        }

        [Test]
        public void Classify_TwoCrossingLinesOfThree_IsBomb()
        {
            MatchComponent component = Single(MatchLayouts.Plus);

            Assert.AreEqual(ComponentRank.Bomb, component.Rank);
            Assert.AreEqual(5, component.Cells.Count, "two lines of 3 are 5 cells together");
        }

        [Test]
        public void Classify_LShape_IsBomb()
        {
            MatchComponent component = Single(MatchLayouts.LShape);

            Assert.AreEqual(ComponentRank.Bomb, component.Rank);
            Assert.AreEqual(BoosterType.Bomb, component.Booster);
        }

        [Test]
        public void Classify_Block2x3_IsAirplaneAndClearsAllSixCells()
        {
            MatchComponent component = Single(MatchLayouts.Block2x3);

            Assert.AreEqual(ComponentRank.Airplane, component.Rank);
            Assert.AreEqual(BoosterType.Airplane, component.Booster);
            Assert.AreEqual(6, component.Cells.Count);
        }

        [Test]
        public void Classify_LoneSquare_IsAirplane()
        {
            MatchComponent component = Single(MatchLayouts.LoneSquare);

            Assert.AreEqual(ComponentRank.Airplane, component.Rank);
            Assert.AreEqual(BoosterType.Airplane, component.Booster);
            Assert.AreEqual(4, component.Cells.Count);
        }

        [Test]
        public void Classify_HorizontalLineOfFour_IsHorizontalRocket()
        {
            MatchComponent component = Single(MatchLayouts.Line4Horizontal);

            Assert.AreEqual(ComponentRank.Rocket, component.Rank);
            Assert.AreEqual(BoosterType.RocketH, component.Booster, "orientation comes from the line (§4.3)");
        }

        [Test]
        public void Classify_VerticalLineOfFour_IsVerticalRocket()
        {
            MatchComponent component = Single(MatchLayouts.Line4Vertical);

            Assert.AreEqual(ComponentRank.Rocket, component.Rank);
            Assert.AreEqual(BoosterType.RocketV, component.Booster);
        }

        [Test]
        public void Classify_LineOfThree_HasNoBooster()
        {
            MatchComponent component = Single(MatchLayouts.Line3);

            Assert.AreEqual(ComponentRank.None, component.Rank);
            Assert.AreEqual(BoosterType.None, component.Booster);
        }

        private static MatchComponent Single(string layout)
        {
            PooledList<MatchComponent> components = MatchDetectionFixture.Detect(layout);
            Assert.AreEqual(1, components.Count, "layout must contain exactly one component");
            return components[0];
        }
    }
}
