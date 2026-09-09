using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Levels;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Levels
{
    public sealed class LevelValidatorTests
    {
        /// <summary>Wall block of width 3: the cell under its centre is never refilled (§10.3).</summary>
        private const string WideWallBlock = @"
            .. .. .. .. ..
            .. ## ## ## ..
            .. .. .. .. ..";

        private const string Open4X4 = @"
            .. .. .. ..
            .. .. .. ..
            .. .. .. ..
            .. .. .. ..";

        [Test]
        public void Validate_AcceptsLevel5FromTheGdd()
        {
            ValidationReport report = Validate(GddLevels.Level5());

            Assert.AreEqual(0, report.Issues.Count, report.Describe());
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void Validate_AcceptsLevel11WhoseMoveLimitDeviatesFromTheFormula()
        {
            LevelData level = GddLevels.Level11();

            ValidationReport report = Validate(level);

            Assert.IsFalse(report.HasErrors, report.Describe());
            Assert.AreNotEqual(
                MoveLimitCalculator.Compute(level, GddLevels.Level11Throughput),
                level.MoveLimit,
                "level 11 keeps the recorded +1 deviation (§10.4)");
        }

        [Test]
        public void Validate_AcceptsNestingOfDepthThree()
        {
            LevelData level = TestLevel.From(
                @"
                .. .. .. ..
                .. bx .. ..
                .. .. .. ..
                .. .. .. ..",
                goals: new[] { GoalDefinition.DestroyElement("bx", 3) },
                contents: new[] { new NestedContent(1, 2, "bx"), new NestedContent(1, 2, "bx") });

            ValidationReport report = Validate(level);

            Assert.IsFalse(report.HasErrors, report.Describe());
        }

        [Test]
        public void Validate_RejectsDeadPocketUnderAWideWallBlock()
        {
            ValidationReport report = Validate(TestLevel.From(WideWallBlock));

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "dead pocket"), report.Describe());
            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "(2, 0)"), report.Describe());
        }

        [Test]
        public void Validate_WarnsOnSolidWallBlockWiderThanTwo()
        {
            ValidationReport report = Validate(TestLevel.From(WideWallBlock));

            Assert.IsTrue(Contains(report, ValidationSeverity.Warning, "solid wall block of width 3"), report.Describe());
        }

        [Test]
        public void Validate_DoesNotWarnOnWallBlocksOfWidthTwo()
        {
            ValidationReport report = Validate(TestLevel.From(@"
                .. .. .. .. ..
                .. ## ## .. ..
                .. .. .. .. .."));

            Assert.IsFalse(Contains(report, ValidationSeverity.Warning, "solid wall block"), report.Describe());
            Assert.IsFalse(report.HasErrors, report.Describe());
        }

        [Test]
        public void Validate_RejectsColorCountOutsideTheAllowedRange()
        {
            ValidationReport report = Validate(TestLevel.From(Open4X4, colorCount: 3));

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "colorCount"), report.Describe());
        }

        [Test]
        public void Validate_RejectsLayoutSizeThatDisagreesWithTheHeader()
        {
            ValidationReport report = Validate(TestLevel.From(Open4X4, height: 5));

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "expected height 5"), report.Describe());
        }

        [Test]
        public void Validate_RejectsUnknownToken()
        {
            ValidationReport report = Validate(TestLevel.From(@"
                .. .. ..
                .. zz ..
                .. .. .."));

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "unknown token 'zz'"), report.Describe());
        }

        [Test]
        public void Validate_RejectsNestingDeeperThanThree()
        {
            LevelData level = TestLevel.From(
                @"
                .. .. .. ..
                .. bx .. ..
                .. .. .. ..
                .. .. .. ..",
                goals: new[] { GoalDefinition.DestroyElement("bx", 4) },
                contents: new[]
                {
                    new NestedContent(1, 2, "bx"),
                    new NestedContent(1, 2, "bx"),
                    new NestedContent(1, 2, "bx")
                });

            ValidationReport report = Validate(level);

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "nesting depth 4"), report.Describe());
        }

        [Test]
        public void Validate_RejectsNestedContentWithoutAnOuterElement()
        {
            LevelData level = TestLevel.From(Open4X4, contents: new[] { new NestedContent(1, 2, "bx") });

            ValidationReport report = Validate(level);

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "no outer element"), report.Describe());
        }

        [Test]
        public void Validate_RejectsGoalWithFewerElementsThanItsTarget()
        {
            LevelData level = TestLevel.From(
                @"
                .. .. .. ..
                .. bx .. ..
                .. .. bx ..
                .. .. .. ..",
                goals: new[] { GoalDefinition.DestroyElement("bx", 3) });

            ValidationReport report = Validate(level);

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "holds 2"), report.Describe());
        }

        [Test]
        public void Validate_CountsNestedElementsTowardsGoalSupply()
        {
            ValidationReport report = Validate(GddLevels.Level9());

            Assert.IsFalse(report.HasErrors, report.Describe());
        }

        [Test]
        public void Validate_RejectsGoalOnTheIndestructibleBlocker()
        {
            LevelData level = TestLevel.From(
                @"
                .. .. .. ..
                .. ## .. ..
                .. .. .. ..
                .. .. .. ..",
                goals: new[] { GoalDefinition.DestroyElement(ElementTokens.Blocker, 1) });

            ValidationReport report = Validate(level);

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "can never be a goal"), report.Describe());
        }

        [Test]
        public void Validate_RejectsCollectColorAboveColorCount()
        {
            LevelData level = TestLevel.From(
                Open4X4,
                colorCount: 4,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C5, 10) });

            ValidationReport report = Validate(level);

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "outside colorCount 4"), report.Describe());
        }

        [Test]
        public void Validate_RejectsSpawnerColumnOutsideTheBoard()
        {
            ValidationReport report = Validate(TestLevel.From(Open4X4, spawners: new[] { 0, 9 }));

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "spawner column 9"), report.Describe());
        }

        [Test]
        public void Validate_RejectsReadyMadeMatchInTheInitialBoard()
        {
            LevelData level = TestLevel.From(@"
                .. .. .. ..
                .. .. .. ..
                t1 t1 t1 ..
                .. .. .. ..");

            ValidationReport report = Validate(level);

            Assert.IsTrue(Contains(report, ValidationSeverity.Error, "ready-made match"), report.Describe());
        }

        private static ValidationReport Validate(LevelData level)
        {
            var validator = new LevelValidator(BuiltInElementCatalog.Create());
            return validator.Validate(level, new DeterministicRandom(20260909));
        }

        private static bool Contains(ValidationReport report, ValidationSeverity severity, string fragment)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ValidationIssue issue = report.Issues[i];
                if (issue.Severity == severity && issue.Message.Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
