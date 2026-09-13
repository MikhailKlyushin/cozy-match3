using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Resolve
{
    public sealed class ClearServiceTests
    {
        private PooledList<GoalDelta> _deltas;

        [SetUp]
        public void SetUp()
        {
            _deltas = new PooledList<GoalDelta>();
        }

        [Test]
        public void Clear_DestroysChipsAndCreditsTheColorGoal()
        {
            BoardModel board = BoardFixture.From("t1 t1 t2");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 5) });
            int firstId = board.GetSlot(new GridPos(0, 0)).InstanceId;
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board, new GridPos(0, 0), new GridPos(1, 0)), writer, _deltas);

            Assert.IsTrue(board.GetSlot(new GridPos(0, 0)).IsEmpty);
            Assert.IsTrue(board.GetSlot(new GridPos(1, 0)).IsEmpty);
            Assert.AreEqual(ChipColor.C2, board.GetSlot(new GridPos(2, 0)).Color, "an uncleared chip must stay");
            Assert.AreEqual(2, Count(transcript, TurnEventKind.ChipDestroyed));
            Assert.AreEqual(ChipColor.C1, First(transcript, TurnEventKind.ChipDestroyed).Color);
            Assert.AreEqual(firstId, First(transcript, TurnEventKind.ChipDestroyed).InstanceId);
            Assert.AreEqual(2, goals.GetState(0).Value);
            Assert.AreEqual(2, Count(transcript, TurnEventKind.GoalProgress));
        }

        [Test]
        public void Clear_ClearedBooster_IsNeitherCollectedNorAnActivation()
        {
            BoardModel board = BoardFixture.From("rh t1");
            var goals = new GoalTracker(new[]
            {
                GoalDefinition.ActivateBooster(BoosterType.RocketH, 1),
                GoalDefinition.CollectColor(ChipColor.C1, 3)
            });
            int boosterId = board.GetSlot(new GridPos(0, 0)).InstanceId;
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board, new GridPos(0, 0)), writer, _deltas);

            TurnEvent destroyed = First(transcript, TurnEventKind.ChipDestroyed);
            Assert.AreEqual(new GridPos(0, 0), destroyed.A);
            Assert.AreEqual(boosterId, destroyed.InstanceId, "the view needs the identity of the cleared booster");
            Assert.AreEqual(ChipColor.None, destroyed.Color, "a booster is colourless");
            Assert.IsTrue(board.GetSlot(new GridPos(0, 0)).IsEmpty);
            Assert.AreEqual(0, goals.GetState(0).Value, "Q1: clearing a booster is not an activation");
            Assert.AreEqual(0, goals.GetState(1).Value);
            Assert.AreEqual(0, Count(transcript, TurnEventKind.GoalProgress));
            Assert.AreEqual(0, _deltas.Count);
        }

        [Test]
        public void Clear_SkipsAnAlreadyEmptyCell()
        {
            BoardModel board = BoardFixture.From(".. t1");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 3) });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board, new GridPos(0, 0)), writer, _deltas);

            Assert.AreEqual(0, Count(transcript, TurnEventKind.ChipDestroyed));
            Assert.AreEqual(0, _deltas.Count);
        }

        [Test]
        public void Clear_RemovesTheObstacleAtZeroHealthAndCreditsTheGoal()
        {
            BoardModel board = BoardFixture.From(@"
                ..
                bx");
            var cell = new GridPos(0, 0);
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box1, 2) });
            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance box));
            box.Health = 0;
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board), writer, _deltas);

            Assert.IsFalse(board.TryGetElement(cell, out _), "the destroyed obstacle must leave the cell");
            Assert.AreEqual(1, Count(transcript, TurnEventKind.ElementDestroyed));
            Assert.AreEqual(box.Definition, First(transcript, TurnEventKind.ElementDestroyed).Element);
            Assert.AreEqual(1, goals.GetState(0).Value);
            Assert.AreEqual(1, Count(transcript, TurnEventKind.GoalProgress));
            Assert.AreEqual(1, First(transcript, TurnEventKind.GoalProgress).Amount);
        }

        [Test]
        public void IndestructibleElement_IsNeverTreatedAsDestroyed()
        {
            BoardModel board = BoardFixture.From("##");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 3) });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board), writer, _deltas);

            Assert.IsTrue(board.TryGetElement(new GridPos(0, 0), out ElementInstance blocker));
            Assert.AreEqual(ElementDefinition.InfiniteHealth, blocker.Health, "-1 hp is not 0 hp");
            Assert.AreEqual(0, Count(transcript, TurnEventKind.ElementDestroyed));
        }

        [Test]
        public void Clear_FreesTheObstacleCellForFallingInTheSameStep()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                bx
                ..");
            var boxCell = new GridPos(0, 1);
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box1, 1) });
            TranscriptWriter writer = NewWriter(out _);
            var damage = new DamageService(board, DamageRules.CreateDefault(), NullLogger.Instance);
            damage.BeginStep(0);

            damage.ApplyBoosterDamage(new[] { boxCell }, sourceId: 0, writer);
            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board), writer, _deltas);

            Assert.IsTrue(board.IsPassableForFall(boxCell), "E10: the freed cell is passable in this very step");

            new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(
                ChipColor.C1,
                board.GetSlot(new GridPos(0, 0)).Color,
                "the chip falls through the freed cell in the same step");
        }

        [Test]
        public void Clear_RevealsTheNestedElementWithImmunityForTheCurrentStep()
        {
            BoardModel board = BoardFixture.From("bx");
            var cell = new GridPos(0, 0);
            BoardFixture.Nest(board, cell, ElementTokens.Box2);
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box1, 1) });
            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance outer));
            outer.Health = 0;
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 3, ChipCells(board), writer, _deltas);

            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance revealed));
            Assert.IsTrue(board.Catalog.TryResolve(ElementTokens.Box2, out ElementDefinition box2));
            Assert.AreEqual(box2.Id, revealed.Definition, "§7.1: the nested element takes the same cell");
            Assert.AreEqual(2, revealed.Health, "the revealed element starts at its own MaxHealth");
            Assert.AreEqual(3, revealed.ImmuneUntilStep, "§7.1: immune until the end of the revealing step");
            Assert.AreEqual(1, Count(transcript, TurnEventKind.ElementRevealed));
            Assert.AreEqual(box2.Id, First(transcript, TurnEventKind.ElementRevealed).Element);
        }

        [Test]
        public void RevealedElement_TakesNoDamageInTheSameStepAndTakesItInTheNext()
        {
            BoardModel board = BoardFixture.From(@"
                ..
                bx");
            var cell = new GridPos(0, 0);
            var matchCells = new[] { new GridPos(0, 1) };
            BoardFixture.Nest(board, cell, ElementTokens.Box2);
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box1, 1) });
            TranscriptWriter writer = NewWriter(out _);
            var damage = new DamageService(board, DamageRules.CreateDefault(), NullLogger.Instance);
            var clear = new ClearService(board, goals, NullLogger.Instance);

            damage.BeginStep(0);
            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);
            clear.Clear(step: 0, ChipCells(board), writer, _deltas);
            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 1, writer);

            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance revealed));
            Assert.AreEqual(2, revealed.Health, "§7.1: one bomb must not unwrap the whole chain in one step");

            damage.BeginStep(1);
            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);

            Assert.AreEqual(1, revealed.Health, "§7.1: the revealed element is damageable from the next step");
        }

        [Test]
        public void Clear_UnwrapsOneNestingLevelPerStepAndCreditsEachElement()
        {
            BoardModel board = BoardFixture.From("bx");
            var cell = new GridPos(0, 0);
            BoardFixture.Nest(board, cell, ElementTokens.Box1);
            BoardFixture.Nest(board, cell, ElementTokens.ColoredBox(1));
            var goals = new GoalTracker(new[]
            {
                GoalDefinition.DestroyElement(ElementTokens.Box1, 2),
                GoalDefinition.DestroyAnyColoredBox(1)
            });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            var clear = new ClearService(board, goals, NullLogger.Instance);

            for (int step = 0; step < 3; step++)
            {
                Assert.IsTrue(board.TryGetElement(cell, out ElementInstance element), "a nesting level must remain");
                element.Health = 0;
                clear.Clear(step, ChipCells(board), writer, _deltas);
            }

            Assert.IsFalse(board.TryGetElement(cell, out _), "the innermost element leaves the cell empty");
            Assert.AreEqual(2, Count(transcript, TurnEventKind.ElementRevealed), "§3.4: nesting depth 3 unwraps twice");
            Assert.AreEqual(3, Count(transcript, TurnEventKind.ElementDestroyed));
            Assert.AreEqual(2, goals.GetState(0).Value);
            Assert.AreEqual(1, goals.GetState(1).Value, "§8.1: a nested element counts towards its own goal");
        }

        [Test]
        public void Clear_SkipsGoalProgressForAClosedGoal()
        {
            BoardModel board = BoardFixture.From("t1 t1");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 1) });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board, new GridPos(0, 0), new GridPos(1, 0)), writer, _deltas);

            Assert.AreEqual(2, Count(transcript, TurnEventKind.ChipDestroyed));
            Assert.AreEqual(2, _deltas.Count, "the tracker still reports the dropped credit");
            Assert.AreEqual(
                1,
                Count(transcript, TurnEventKind.GoalProgress),
                "E18: a zero delta is not written to the transcript");
        }

        [Test]
        public void Clear_AppendsToTheCallerBufferAndReportsOnlyItsOwnDeltas()
        {
            BoardModel board = BoardFixture.From("t1");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 5) });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            // A credit an earlier stage already reported (ACTIVATE) must not be written twice.
            goals.CreditChip(ChipColor.C1, _deltas);

            new ClearService(board, goals, NullLogger.Instance)
                .Clear(step: 0, ChipCells(board, new GridPos(0, 0)), writer, _deltas);

            Assert.AreEqual(2, _deltas.Count, "the caller's buffer is appended, never cleared");
            Assert.AreEqual(1, Count(transcript, TurnEventKind.GoalProgress));
            Assert.AreEqual(2, First(transcript, TurnEventKind.GoalProgress).Value);
        }

        private static TranscriptWriter NewWriter(out TurnTranscript transcript)
        {
            transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);
            writer.TurnBegin(seed: 1, movesLeft: 10);
            return writer;
        }

        private static CellBuffer ChipCells(BoardModel board, params GridPos[] cells)
        {
            var buffer = new CellBuffer();
            buffer.Configure(board.Width, board.Height);
            for (int i = 0; i < cells.Length; i++)
            {
                buffer.AddUnique(cells[i]);
            }

            return buffer;
        }

        private static int Count(TurnTranscript transcript, TurnEventKind kind)
        {
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                if (transcript.GetEvent(i).Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static TurnEvent First(TurnTranscript transcript, TurnEventKind kind)
        {
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == kind)
                {
                    return e;
                }
            }

            Assert.Fail("no event of the expected kind in the transcript");
            return default;
        }
    }
}
