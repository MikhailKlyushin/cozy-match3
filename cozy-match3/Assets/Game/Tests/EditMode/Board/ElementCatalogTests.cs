using Match3.Board;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Boards
{
    public sealed class ElementCatalogTests
    {
        private ElementCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = BuiltInElementCatalog.Create();

        [Test]
        public void Box2_DiffersFromBox1_OnlyByHealth()
        {
            Assert.IsTrue(_catalog.TryResolve(ElementTokens.Box1, out ElementDefinition bx));
            Assert.IsTrue(_catalog.TryResolve(ElementTokens.Box2, out ElementDefinition b2));

            Assert.AreEqual(1, bx.MaxHealth);
            Assert.AreEqual(2, b2.MaxHealth);
            Assert.AreEqual(bx.DamageSource, b2.DamageSource);
            Assert.AreEqual(bx.ColorMode, b2.ColorMode);
            Assert.AreEqual(bx.GoalRole, b2.GoalRole);
            Assert.AreEqual(bx.Gravity, b2.Gravity);
            Assert.AreEqual(bx.Occupancy, b2.Occupancy);
        }

        [Test]
        public void Box3_HasThreeHitPoints()
        {
            Assert.IsTrue(_catalog.TryResolve(ElementTokens.Box3, out ElementDefinition b3));
            Assert.AreEqual(3, b3.MaxHealth);
        }

        [Test]
        public void ColoredBox_IsFixedColorAndOwnColorMatchOnly()
        {
            Assert.IsTrue(_catalog.TryResolve("c3", out ElementDefinition c3));

            Assert.AreEqual(ElementColorMode.Fixed, c3.ColorMode);
            Assert.AreEqual(ChipColor.C3, c3.FixedColor);
            Assert.AreEqual(DamageSourceKind.AdjacentMatchOfColor, c3.DamageSource);
            Assert.IsTrue(c3.IsColoredBox);
        }

        [Test]
        public void CyclingBox_CarriesThePerTurnAxis()
        {
            Assert.IsTrue(_catalog.TryResolve(ElementTokens.ColoredBoxCycling, out ElementDefinition cx));

            Assert.AreEqual(PerTurnBehaviour.CycleColor, cx.PerTurn);
            Assert.AreEqual(ElementColorMode.Cycling, cx.ColorMode);
            Assert.IsTrue(cx.IsColoredBox);
        }

        [Test]
        public void Blocker_IsInfiniteHealthAndNotCountable()
        {
            Assert.IsTrue(_catalog.TryResolve(ElementTokens.Blocker, out ElementDefinition blocker));

            Assert.AreEqual(ElementDefinition.InfiniteHealth, blocker.MaxHealth);
            Assert.AreEqual(GoalRole.NotCountable, blocker.GoalRole);
            Assert.AreEqual(DamageSourceKind.None, blocker.DamageSource);
            Assert.IsTrue(blocker.IsIndestructible);
            Assert.IsFalse(blocker.IsColoredBox);
        }

        [Test]
        public void PlainBoxes_AreNotColoredBoxes()
        {
            Assert.IsTrue(_catalog.TryResolve(ElementTokens.Box1, out ElementDefinition bx));
            Assert.IsFalse(bx.IsColoredBox);
        }

        [Test]
        public void AllSixColoredBoxes_AreRegistered()
        {
            for (int colorIndex = 1; colorIndex <= ChipColors.MaxColorCount; colorIndex++)
            {
                string token = ElementTokens.ColoredBox(colorIndex);
                Assert.IsTrue(_catalog.TryResolve(token, out ElementDefinition definition), token);
                Assert.AreEqual(ChipColors.FromIndex(colorIndex), definition.FixedColor);
            }
        }

        [Test]
        public void UnknownToken_IsRejected()
        {
            Assert.IsFalse(_catalog.TryResolve("zz", out _));
        }

        [Test]
        public void GetById_RoundTripsEveryDefinition()
        {
            foreach (ElementDefinition definition in BuiltInElementCatalog.CreateDefinitions())
            {
                Assert.AreEqual(definition.Token, _catalog.Get(definition.Id).Token);
            }
        }
    }
}
