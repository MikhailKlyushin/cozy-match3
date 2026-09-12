using Match3.Board;
using Match3.Core;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Resolve
{
    public sealed class GravityServiceTests
    {
        /// <summary>Safety net for the "until stable" loop; the layouts settle in far fewer rounds.</summary>
        private const int MaxFillRounds = 200;

        [Test]
        public void RunUntilStable_DropsChipThroughSeveralEmptyCells()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                ..
                ..
                ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            int instanceId = board.GetSlot(new GridPos(0, 3)).InstanceId;

            int moves = new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(3, moves);
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 0)).Color);
            Assert.IsTrue(board.GetSlot(new GridPos(0, 3)).IsEmpty);
            Assert.AreEqual(3, CountMoves(transcript, ChipMoveFlags.Fall));
            Assert.AreEqual(
                instanceId,
                board.GetSlot(new GridPos(0, 0)).InstanceId,
                "MoveSlot must preserve the chip identity used by the view");
        }

        [Test]
        public void RunSinglePass_MovesChipOneRowDown()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                ..
                ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int moves = new GravityService(board).RunSinglePass(writer);

            Assert.AreEqual(1, moves);
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 1)).Color);
            TurnEvent move = SingleMove(transcript);
            Assert.AreEqual(ChipMoveFlags.Fall, move.MoveFlags);
            Assert.AreEqual(new GridPos(0, 2), move.A);
            Assert.AreEqual(new GridPos(0, 1), move.B);
        }

        [Test]
        public void RunUntilStable_RepeatsPassesUntilNothingMoves()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                ..
                ..
                ..
                ..
                ..");
            TranscriptWriter writer = NewWriter(out _);
            var gravity = new GravityService(board);

            int firstPass = gravity.RunSinglePass(writer);
            int remaining = gravity.RunUntilStable(writer);

            Assert.AreEqual(1, firstPass, "a single pass moves the chip by one row only");
            Assert.AreEqual(4, remaining, "the remaining passes must run until the chip rests");
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 0)).Color);
            Assert.AreEqual(0, gravity.RunUntilStable(writer), "a settled board must report no moves");
        }

        [Test]
        public void DiagonalSlide_FeedsCellUnderAShelf()
        {
            BoardModel board = BoardFixture.From(@"
                .. t1 ## ..
                .. ## .. ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int moves = new GravityService(board).RunSinglePass(writer);

            Assert.AreEqual(1, moves);
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(2, 0)).Color);
            Assert.IsTrue(board.GetSlot(new GridPos(1, 1)).IsEmpty);
            Assert.AreEqual(ChipMoveFlags.Slide, SingleMove(transcript).MoveFlags);
        }

        [Test]
        public void DiagonalSlide_TakesUpperLeftBeforeUpperRight()
        {
            BoardModel board = BoardFixture.From(@"
                .. t1 ## t2 ..
                .. ## .. ## ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new GravityService(board).RunSinglePass(writer);

            TurnEvent move = SingleMove(transcript);
            Assert.AreEqual(
                new GridPos(1, 1),
                move.A,
                "GDD §5.3/D10: the upper-left chip (1,1) slides into (2,0); the upper-right chip (3,1) must stay");
            Assert.AreEqual(new GridPos(2, 0), move.B);
            Assert.AreEqual(ChipMoveFlags.Slide, move.MoveFlags);
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(2, 0)).Color, "expected the t1 chip from (1,1)");
            Assert.AreEqual(ChipColor.C2, board.GetSlot(new GridPos(3, 1)).Color, "the t2 chip at (3,1) must not move");
        }

        [Test]
        public void ChipThatCanFallVertically_DoesNotSlideDiagonally()
        {
            BoardModel board = BoardFixture.From(@"
                ## ## t1
                .. .. ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new GravityService(board).RunSinglePass(writer);

            TurnEvent move = SingleMove(transcript);
            Assert.AreEqual(ChipMoveFlags.Fall, move.MoveFlags);
            Assert.AreEqual(new GridPos(2, 0), move.B);
            Assert.IsTrue(
                board.GetSlot(new GridPos(1, 0)).IsEmpty,
                "GDD §5.3: (2,1) can fall into its own column, so it must not slide into (1,0)");
        }

        [Test]
        public void LiveObstacle_BlocksFall()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                bx
                ..");
            TranscriptWriter writer = NewWriter(out _);

            int moves = new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(0, moves, "E10: a live obstacle blocks the fall");
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 2)).Color);
            Assert.IsTrue(board.GetSlot(new GridPos(0, 0)).IsEmpty);
        }

        [Test]
        public void DestroyedObstacle_DoesNotBlockFallInTheSameStep()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                bx
                ..");
            board.TryGetElement(new GridPos(0, 1), out ElementInstance element);
            element.Health = 0;
            TranscriptWriter writer = NewWriter(out _);

            int moves = new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(2, moves, "E10: the cell of a destroyed obstacle is passable in the same step");
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 0)).Color);
        }

        [Test]
        public void Blocker_LetsTheColumnAboveItFallThrough()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t3
                ## ## ##
                .. .. ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 0)).Color);
            Assert.AreEqual(ChipColor.C2, board.GetSlot(new GridPos(1, 0)).Color);
            Assert.AreEqual(ChipColor.C3, board.GetSlot(new GridPos(2, 0)).Color);

            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind != TurnEventKind.ChipMoved)
                {
                    continue;
                }

                Assert.AreEqual(
                    ChipMoveFlags.Fall,
                    e.MoveFlags,
                    "GDD §5.3: a cell under a blocker is fed by its own column, never by a slide");
                Assert.AreEqual(e.A.X, e.B.X, "the chip must not change column");
            }
        }

        [Test]
        public void PocketAboveABlocker_FeedsItsOwnColumnBeforeTheNeighbour()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t3
                ## t4 t5
                ## t6 ..
                .. .. ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(
                ChipColor.C1,
                board.GetSlot(new GridPos(0, 0)).Color,
                "the chip above the blocker fills the cell under it, not the neighbour column");

            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind != TurnEventKind.ChipMoved || e.B.X != 0)
                {
                    continue;
                }

                Assert.AreEqual(ChipMoveFlags.Fall, e.MoveFlags, "column 0 is fed vertically");
                Assert.AreEqual(0, e.A.X, "column 0 is fed from column 0");
            }
        }

        [Test]
        public void LiveObstacle_StillBlocksFall_AndIsNotFallenThrough()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                bx
                ..");
            TranscriptWriter writer = NewWriter(out _);

            int moves = new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(0, moves, "only the blocker is on the pass-through gravity axis (§7.1)");
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 2)).Color);
            Assert.IsTrue(board.GetSlot(new GridPos(0, 0)).IsEmpty);
        }

        [Test]
        public void Hole_BlocksFall()
        {
            BoardModel board = BoardFixture.From(@"
                t1
                __
                ..");
            TranscriptWriter writer = NewWriter(out _);

            int moves = new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(0, moves);
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 2)).Color);
        }

        [Test]
        public void Booster_FallsLikeAChip()
        {
            BoardModel board = BoardFixture.From(@"
                rh
                ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            int instanceId = board.GetSlot(new GridPos(0, 1)).InstanceId;

            int moves = new GravityService(board).RunUntilStable(writer);

            Assert.AreEqual(1, moves);
            ChipSlot landed = board.GetSlot(new GridPos(0, 0));
            Assert.AreEqual(SlotKind.Booster, landed.Kind);
            Assert.AreEqual(BoosterType.RocketH, landed.Booster);
            Assert.AreEqual(instanceId, landed.InstanceId);
            Assert.AreEqual(ChipMoveFlags.Fall, SingleMove(transcript).MoveFlags);
        }

        [Test]
        public void Level10Layout_FillsSpawnerlessColumnsByDiagonalSlideOnly()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. .. __ __ .. .. ..
                .. cx .. __ __ .. cx ..
                .. .. .. ## ## .. .. ..
                .. cx cx .. .. cx cx ..
                .. .. .. .. .. .. .. ..
                .. .. .. .. .. .. .. ..
                .. cx .. .. .. .. cx ..
                .. .. .. .. .. .. .. ..");
            board.SetSpawnerColumns(new[] { 0, 1, 2, 5, 6, 7 });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            var gravity = new GravityService(board);
            var refill = new RefillService(
                board,
                new WeightedChipSpawnPolicy(new DeterministicRandom(2024), colorCount: 5, weights: null));

            int rounds = 0;
            while (rounds < MaxFillRounds)
            {
                int moved = gravity.RunUntilStable(writer);
                int spawned = refill.Run(writer);
                rounds++;
                if (moved == 0 && spawned == 0)
                {
                    break;
                }
            }

            Assert.Less(rounds, MaxFillRounds, "gravity plus refill must reach a stable board");

            for (int y = 0; y <= 4; y++)
            {
                Assert.IsFalse(
                    board.GetSlot(new GridPos(3, y)).IsEmpty,
                    "column 3 of level 10 has no spawner and must be fed by diagonal slide (§3.3, §5.3)");
                Assert.IsFalse(
                    board.GetSlot(new GridPos(4, y)).IsEmpty,
                    "column 4 of level 10 has no spawner and must be fed by diagonal slide (§3.3, §5.3)");
            }

            Assert.Greater(CountSlidesInto(transcript, new GridPos(3, 4)), 0, "expected slides from (2,5) into (3,4)");
            Assert.Greater(CountSlidesInto(transcript, new GridPos(4, 4)), 0, "expected slides from (5,5) into (4,4)");

            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind != TurnEventKind.ChipSpawned)
                {
                    continue;
                }

                Assert.AreNotEqual(3, e.A.X, "column 3 is not a spawner column of level 10");
                Assert.AreNotEqual(4, e.A.X, "column 4 is not a spawner column of level 10");
            }
        }

        private static TranscriptWriter NewWriter(out TurnTranscript transcript)
        {
            transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);
            writer.TurnBegin(seed: 1, movesLeft: 10);
            return writer;
        }

        private static int CountMoves(TurnTranscript transcript, ChipMoveFlags flags)
        {
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.ChipMoved && e.MoveFlags == flags)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSlidesInto(TurnTranscript transcript, GridPos target)
        {
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.ChipMoved && e.MoveFlags == ChipMoveFlags.Slide && e.B == target)
                {
                    count++;
                }
            }

            return count;
        }

        private static TurnEvent SingleMove(TurnTranscript transcript)
        {
            int found = 0;
            TurnEvent move = default;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind != TurnEventKind.ChipMoved)
                {
                    continue;
                }

                move = e;
                found++;
            }

            Assert.AreEqual(1, found, "expected exactly one ChipMoved event");
            return move;
        }
    }
}
