using System.Collections.Generic;
using Match3.Core;
using Match3.Matching;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using Match3.Tests.EditMode.Turn;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Resolve
{
    /// <summary>
    /// §5.4. The board below has no ready match and no legal swap: every colour sits on a strict
    /// 2x2 lattice, so moving a chip one cell can only ever pair it. That is the state POST-TURN
    /// hands to the shuffle.
    /// </summary>
    public sealed class ShuffleServiceTests
    {
        private const string Deadlocked =
            @"t3 t4 t3 t4
              t1 t2 t1 t2
              t3 t4 t3 t4
              t1 t2 t1 t2";

        [Test]
        public void Fixture_IsActuallyDeadlocked()
        {
            BoardModel board = BoardFixture.From(Deadlocked);
            var detection = new MatchDetectionService(board);
            var moves = new LegalMoveService(board, new SwapValidator(board, detection));

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new GridPos(x, y);

                    // An empty board also has no match and no move, and would make the test below
                    // pass without exercising anything.
                    Assert.AreEqual(SlotKind.Chip, board.GetSlot(cell).Kind, cell + " holds no chip");
                }
            }

            Assert.IsFalse(detection.HasAnyMatch(), "the fixture must start without a ready match");
            Assert.IsFalse(moves.HasAnyMove, "the fixture must start without a legal move");
        }

        [Test]
        public void Shuffle_RestoresAPlayableBoardWhenBothRandomPhasesFail()
        {
            BoardModel board = BoardFixture.From(Deadlocked);
            var detection = new MatchDetectionService(board);
            var moves = new LegalMoveService(board, new SwapValidator(board, detection));
            var logger = new RecordingLogger();

            // Permuting does nothing and every regenerated colour is the same, so the first two
            // phases of §5.4 cannot succeed and the third one has to.
            var service = new ShuffleService(
                board, detection, moves, new FixedColorSpawnPolicy(ChipColor.C1), Rules(),
                new StuckRandom(), logger);

            bool restored = service.Run(new TranscriptWriter(new TurnTranscript()));

            Assert.IsTrue(restored, "the shuffle gave up and reported a deadlock (E19)");
            Assert.IsFalse(detection.HasAnyMatch(), "the shuffle left a ready match on the board");

            moves.Invalidate();
            Assert.IsTrue(moves.HasAnyMove, "the shuffle left the board without a legal move");
            Assert.IsEmpty(logger.Errors, "the shuffle logged an error while succeeding");
        }

        [Test]
        public void Shuffle_ReportsDeadlockWhenTooFewChipsRemain()
        {
            BoardModel board = BoardFixture.From("t1 t2");
            var detection = new MatchDetectionService(board);
            var moves = new LegalMoveService(board, new SwapValidator(board, detection));
            var logger = new RecordingLogger();

            var service = new ShuffleService(
                board, detection, moves, new FixedColorSpawnPolicy(ChipColor.C1), Rules(),
                new DeterministicRandom(1), logger);

            Assert.IsFalse(service.Run(new TranscriptWriter(new TurnTranscript())));
            Assert.IsNotEmpty(logger.Errors, "a deadlock must be logged as a bug (E19)");
        }

        private static LevelRules Rules() => new LevelRules(1, 4, 20, 5f);

        /// <summary>Never permutes and always draws index 0, which starves both random phases.</summary>
        private sealed class StuckRandom : IRandom
        {
            public int Seed => 0;

            public int NextInt(int maxExclusive) => 0;

            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;

            public void Shuffle<T>(IList<T> list)
            {
            }
        }
    }
}
