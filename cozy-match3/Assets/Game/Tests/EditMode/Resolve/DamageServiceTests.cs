using Match3.Board;
using Match3.Core;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Resolve
{
    public sealed class DamageServiceTests
    {
        [Test]
        public void MatchDamage_TouchingOneBoxWithSeveralCells_DealsOneDamage()
        {
            // Plus-shaped component; the b2 at (0,0) touches it from (0,1) and (1,0) (D07, E09).
            BoardModel board = BoardFixture.From(@"
                .. t1 ..
                t1 t1 t1
                b2 t1 ..");
            var matchCells = new[]
            {
                new GridPos(1, 0),
                new GridPos(0, 1),
                new GridPos(1, 1),
                new GridPos(2, 1),
                new GridPos(1, 2)
            };
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, new GridPos(0, 0)), "D07: one source deals one damage");
            Assert.AreEqual(1, CountDamage(transcript), "expected a single ElementDamaged event");
        }

        [Test]
        public void MatchDamage_FromTheSameSourceTwice_DealsOneDamage()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                b2");
            var matchCells = new[] { new GridPos(0, 1) };
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);
            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, new GridPos(0, 0)), "D07: one instance per source per step");
            Assert.AreEqual(1, CountDamage(transcript));
        }

        [Test]
        public void MatchDamage_HitsEveryTouchedObstacleOnce()
        {
            BoardModel board = BoardFixture.From("b2 t1 b2");
            var matchCells = new[] { new GridPos(1, 0) };
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, new GridPos(0, 0)));
            Assert.AreEqual(1, HealthAt(board, new GridPos(2, 0)));
            Assert.AreEqual(2, CountDamage(transcript), "de-duplication is per (source, element), not per source");
        }

        [Test]
        public void TwoMatchSources_InOneStep_DealTwoDamage()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                b2
                t1");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, new[] { new GridPos(0, 2) }, sourceId: 0, writer);
            damage.ApplyMatchDamage(ChipColor.C1, new[] { new GridPos(0, 0) }, sourceId: 1, writer);

            Assert.AreEqual(0, HealthAt(board, new GridPos(0, 1)), "E11: two sources in one step are two damage");
            Assert.AreEqual(2, CountDamage(transcript));
        }

        [Test]
        public void MatchAndBoosterWithTheSameId_AreDifferentSources()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                b2
                t1");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, new[] { new GridPos(0, 2) }, sourceId: 0, writer);
            damage.ApplyBoosterDamage(new[] { new GridPos(0, 0) }, sourceId: 0, writer);

            Assert.AreEqual(0, HealthAt(board, new GridPos(0, 1)), "the origin kind is part of the source identity");
            Assert.AreEqual(2, CountDamage(transcript));
        }

        [Test]
        public void BoosterDamage_CoveringManyCellsAroundOneBox_DealsOneDamage()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. b2 ..
                .. .. ..");
            var hitCells = new[]
            {
                new GridPos(0, 0),
                new GridPos(1, 0),
                new GridPos(2, 0),
                new GridPos(0, 1),
                new GridPos(1, 1),
                new GridPos(2, 1),
                new GridPos(0, 2),
                new GridPos(1, 2),
                new GridPos(2, 2)
            };
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyBoosterDamage(hitCells, sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, new GridPos(1, 1)), "E09: area damage from one source is one damage");
            Assert.AreEqual(1, CountDamage(transcript));
        }

        [Test]
        public void Overkill_BeyondTheRemainingHealth_IsDiscarded()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. b2 ..
                .. .. ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyBoosterDamage(new[] { new GridPos(1, 0) }, sourceId: 0, writer);
            damage.ApplyBoosterDamage(new[] { new GridPos(0, 1) }, sourceId: 1, writer);
            damage.ApplyBoosterDamage(new[] { new GridPos(2, 1) }, sourceId: 2, writer);

            Assert.AreEqual(0, HealthAt(board, new GridPos(1, 1)), "§7.1: the excess is dropped, never banked");
            Assert.AreEqual(2, CountDamage(transcript), "a destroyed obstacle takes no further damage");
        }

        [Test]
        public void BeginStep_ResetsThePerSourceLedger()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                b2");
            var matchCells = new[] { new GridPos(0, 1) };
            TranscriptWriter writer = NewWriter(out _);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);
            damage.BeginStep(1);
            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);

            Assert.AreEqual(0, HealthAt(board, new GridPos(0, 0)), "D07: the ledger is per step, not per turn");
        }

        [Test]
        public void Blocker_TakesNoDamageAndDoesNotStopTheBlast()
        {
            BoardModel board = BoardFixture.From("bx ## bx");
            var hitCells = new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) };
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyBoosterDamage(hitCells, sourceId: 0, writer);

            Assert.AreEqual(
                ElementDefinition.InfiniteHealth,
                HealthAt(board, new GridPos(1, 0)),
                "D11: the blocker takes no damage");
            Assert.AreEqual(0, HealthAt(board, new GridPos(0, 0)));
            Assert.AreEqual(0, HealthAt(board, new GridPos(2, 0)), "E08: the rocket passes through the blocker");
            Assert.AreEqual(0, CountDamageAt(transcript, new GridPos(1, 0)));
            Assert.AreEqual(2, CountDamage(transcript));
        }

        [Test]
        public void DiagonalMatch_DoesNotDamageABox()
        {
            BoardModel board = BoardFixture.From(@"
                .. bx
                t1 ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, new[] { new GridPos(0, 0) }, sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, new GridPos(1, 1)), "§7.1: diagonals are not adjacent");
            Assert.AreEqual(0, CountDamage(transcript));
        }

        [Test]
        public void ColoredBox_IsNotDamagedByAMatchOfAnotherColor()
        {
            BoardModel board = BoardFixture.From(@"
                t2
                c3");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C2, new[] { new GridPos(0, 1) }, sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, new GridPos(0, 0)), "§7.2: only its own colour breaks it");
            Assert.AreEqual(0, CountDamage(transcript));
        }

        [Test]
        public void ColoredBox_IsDamagedByAMatchOfItsOwnColor()
        {
            BoardModel board = BoardFixture.From(@"
                t3
                c3");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C3, new[] { new GridPos(0, 1) }, sourceId: 0, writer);

            Assert.AreEqual(0, HealthAt(board, new GridPos(0, 0)));
            Assert.AreEqual(1, CountDamage(transcript));
        }

        [Test]
        public void ColoredBox_IsDestroyedByBoosterAreaDamage()
        {
            BoardModel board = BoardFixture.From(@"
                ..
                c3");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyBoosterDamage(new[] { new GridPos(0, 1) }, sourceId: 0, writer);

            Assert.AreEqual(0, HealthAt(board, new GridPos(0, 0)), "Q4: a booster is colourless and still destroys it");
            Assert.AreEqual(1, CountDamage(transcript));
        }

        [Test]
        public void CyclingBox_IsDamagedByAMatchOfItsCurrentColor()
        {
            BoardModel board = BoardFixture.From(@"
                ..
                cx");
            var cell = new GridPos(0, 0);
            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance box));
            box.CurrentColor = ChipColor.C4;
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, new[] { new GridPos(0, 1) }, sourceId: 0, writer);
            Assert.AreEqual(1, box.Health, "§7.2: the colour comes from ElementInstance.CurrentColor");

            damage.ApplyMatchDamage(ChipColor.C4, new[] { new GridPos(0, 1) }, sourceId: 1, writer);

            Assert.AreEqual(0, box.Health);
            Assert.AreEqual(1, CountDamage(transcript));
        }

        [Test]
        public void ImmuneElement_TakesNoDamageUntilTheNextStep()
        {
            BoardModel board = BoardFixture.From(@"
                ..
                b2");
            var cell = new GridPos(0, 0);
            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance box));
            box.ImmuneUntilStep = 0;
            var matchCells = new[] { new GridPos(0, 1) };
            TranscriptWriter writer = NewWriter(out _);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);
            Assert.AreEqual(2, box.Health, "§7.1: immune until the end of the step that revealed it");

            damage.BeginStep(1);
            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);

            Assert.AreEqual(1, box.Health, "§7.1: the immunity expires with the step");
        }

        [Test]
        public void ElementDamaged_CarriesTheAmountAndTheRemainingHealth()
        {
            BoardModel board = BoardFixture.From(@"
                ..
                b3");
            Assert.IsTrue(board.Catalog.TryResolve(ElementTokens.Box3, out ElementDefinition definition));
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewService(board);

            damage.ApplyMatchDamage(ChipColor.C1, new[] { new GridPos(0, 1) }, sourceId: 0, writer);

            TurnEvent damaged = SingleDamage(transcript);
            Assert.AreEqual(new GridPos(0, 0), damaged.A);
            Assert.AreEqual(definition.Id, damaged.Element);
            Assert.AreEqual(1, damaged.Amount, "D07: always one per source");
            Assert.AreEqual(2, damaged.Value, "Value is the remaining hp");
        }

        private static TranscriptWriter NewWriter(out TurnTranscript transcript)
        {
            transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);
            writer.TurnBegin(seed: 1, movesLeft: 10);
            return writer;
        }

        private static DamageService NewService(BoardModel board)
        {
            var damage = new DamageService(board, DamageRules.CreateDefault(), NullLogger.Instance);
            damage.BeginStep(0);
            return damage;
        }

        private static int HealthAt(BoardModel board, GridPos cell)
        {
            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance element), "expected an element at the cell");
            return element.Health;
        }

        private static int CountDamage(TurnTranscript transcript)
        {
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                if (transcript.GetEvent(i).Kind == TurnEventKind.ElementDamaged)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDamageAt(TurnTranscript transcript, GridPos cell)
        {
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.ElementDamaged && e.A == cell)
                {
                    count++;
                }
            }

            return count;
        }

        private static TurnEvent SingleDamage(TurnTranscript transcript)
        {
            TurnEvent found = default;
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind != TurnEventKind.ElementDamaged)
                {
                    continue;
                }

                found = e;
                count++;
            }

            Assert.AreEqual(1, count, "expected exactly one ElementDamaged event");
            return found;
        }
    }
}
