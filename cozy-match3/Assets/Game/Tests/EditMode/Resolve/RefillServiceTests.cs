using System.Collections.Generic;
using Match3.Core;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Resolve
{
    public sealed class RefillServiceTests
    {
        [Test]
        public void Run_FillsTopCellsOfEverySpawnerColumn_InAscendingX()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. .. ..");
            var policy = new RecordingSpawnPolicy();
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int spawned = new RefillService(board, policy).Run(writer);

            Assert.AreEqual(3, spawned);
            Assert.AreEqual(3, policy.Columns.Count);
            Assert.AreEqual(0, policy.Columns[0], "columns are visited x-ascending (§5.3)");
            Assert.AreEqual(1, policy.Columns[1]);
            Assert.AreEqual(2, policy.Columns[2]);
            Assert.AreEqual(ChipColor.C1, board.GetSlot(new GridPos(0, 1)).Color);
            Assert.AreEqual(ChipColor.C2, board.GetSlot(new GridPos(1, 1)).Color);
            Assert.AreEqual(ChipColor.C3, board.GetSlot(new GridPos(2, 1)).Color);
            Assert.AreEqual(3, CountSpawns(transcript));
        }

        [Test]
        public void Run_SkipsColumnsWithoutSpawner()
        {
            BoardModel board = BoardFixture.From(@"
                .. .. ..
                .. .. ..");
            board.SetSpawnerColumns(new[] { 0, 2 });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int spawned = new RefillService(board, new RecordingSpawnPolicy()).Run(writer);

            Assert.AreEqual(2, spawned);
            Assert.IsTrue(
                board.GetSlot(new GridPos(1, 1)).IsEmpty,
                "GDD §3.3: a column without a spawner is fed by diagonal slide only");
            Assert.AreEqual(2, CountSpawns(transcript));
        }

        [Test]
        public void Run_DoesNotRefillAnOccupiedTopCell()
        {
            BoardModel board = BoardFixture.From(@"
                t4 ..
                .. ..");
            int existing = board.GetSlot(new GridPos(0, 1)).InstanceId;
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int spawned = new RefillService(board, new RecordingSpawnPolicy()).Run(writer);

            Assert.AreEqual(1, spawned);
            Assert.AreEqual(existing, board.GetSlot(new GridPos(0, 1)).InstanceId, "the existing chip must stay");
            Assert.AreEqual(new GridPos(1, 1), FirstSpawn(transcript).A);
        }

        [Test]
        public void Run_SkipsSpawnerColumnBlockedByAnElement()
        {
            BoardModel board = BoardFixture.From(@"
                ## ..
                .. ..");
            board.SetSpawnerColumns(new[] { 0, 1 });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int spawned = new RefillService(board, new RecordingSpawnPolicy()).Run(writer);

            Assert.AreEqual(1, spawned);
            Assert.AreEqual(new GridPos(1, 1), FirstSpawn(transcript).A);
        }

        [Test]
        public void Run_SkipsSpawnerColumnWithAHoleOnTop()
        {
            BoardModel board = BoardFixture.From(@"
                __ ..
                .. ..");
            board.SetSpawnerColumns(new[] { 0, 1 });
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int spawned = new RefillService(board, new RecordingSpawnPolicy()).Run(writer);

            Assert.AreEqual(1, spawned);
            Assert.AreEqual(new GridPos(1, 1), FirstSpawn(transcript).A);
        }

        [Test]
        public void Run_ReturnsZeroWhenTheTopRowIsFull()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2
                .. ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            int spawned = new RefillService(board, new RecordingSpawnPolicy()).Run(writer);

            Assert.AreEqual(0, spawned);
            Assert.AreEqual(0, CountSpawns(transcript));
        }

        [Test]
        public void ChipSpawned_CarriesTheColourAndAFreshInstanceId()
        {
            BoardModel board = BoardFixture.From(@"
                .. ..
                .. ..");
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);

            new RefillService(board, new RecordingSpawnPolicy()).Run(writer);

            TurnEvent first = FirstSpawn(transcript);
            Assert.AreEqual(ChipColor.C1, first.Color);
            Assert.AreEqual(board.GetSlot(new GridPos(0, 1)).InstanceId, first.InstanceId);
            Assert.AreNotEqual(
                board.GetSlot(new GridPos(0, 1)).InstanceId,
                board.GetSlot(new GridPos(1, 1)).InstanceId,
                "each spawned chip needs its own identity");
        }

        [Test]
        public void NullWeights_SpawnUniformlyOverColorCount()
        {
            var random = new CountingRandom(11);
            var policy = new WeightedChipSpawnPolicy(random, colorCount: 5, weights: null);
            var counts = new int[ChipColors.MaxColorCount + 1];

            for (int i = 0; i < 1000; i++)
            {
                counts[ChipColors.ToIndex(policy.NextColor(0))]++;
            }

            Assert.AreEqual(1000, random.Draws, "one RNG draw per chip");
            Assert.AreEqual(0, counts[0], "ChipColor.None must never be spawned");
            Assert.AreEqual(0, counts[6], "colour 6 is outside colorCount = 5");
            for (int index = 1; index <= 5; index++)
            {
                Assert.Greater(counts[index], 0, "uniform spawning must reach every colour in [1, 5]");
            }
        }

        [Test]
        public void EmptyWeights_SpawnUniformlyOverColorCount()
        {
            var random = new CountingRandom(12);
            var policy = new WeightedChipSpawnPolicy(random, colorCount: 4, weights: new List<ColorWeight>());

            for (int i = 0; i < 200; i++)
            {
                int index = ChipColors.ToIndex(policy.NextColor(0));
                Assert.GreaterOrEqual(index, 1);
                Assert.LessOrEqual(index, 4);
            }

            Assert.AreEqual(200, random.Draws);
        }

        [Test]
        public void Weights_DistributeColoursByWeightAtAFixedSeed()
        {
            var random = new CountingRandom(2024);
            var weights = new List<ColorWeight>
            {
                new ColorWeight(ChipColor.C1, 3),
                new ColorWeight(ChipColor.C2, 1)
            };
            var policy = new WeightedChipSpawnPolicy(random, colorCount: 4, weights: weights);
            var counts = new int[ChipColors.MaxColorCount + 1];

            for (int i = 0; i < 1200; i++)
            {
                counts[ChipColors.ToIndex(policy.NextColor(0))]++;
            }

            Assert.AreEqual(1200, random.Draws, "one RNG draw per chip");
            Assert.AreEqual(0, counts[3], "a colour without weight is never spawned");
            Assert.AreEqual(0, counts[4], "a colour without weight is never spawned");
            Assert.That(counts[1], Is.InRange(820, 980), "weight 3 of 4 means about 900 of 1200 chips");
            Assert.That(counts[2], Is.InRange(220, 380), "weight 1 of 4 means about 300 of 1200 chips");
        }

        [Test]
        public void ZeroTotalWeight_FallsBackToUniform()
        {
            var random = new CountingRandom(13);
            var weights = new List<ColorWeight>
            {
                new ColorWeight(ChipColor.C1, 0),
                new ColorWeight(ChipColor.C2, 0)
            };
            var policy = new WeightedChipSpawnPolicy(random, colorCount: 4, weights: weights);
            var counts = new int[ChipColors.MaxColorCount + 1];

            for (int i = 0; i < 400; i++)
            {
                counts[ChipColors.ToIndex(policy.NextColor(0))]++;
            }

            Assert.AreEqual(400, random.Draws);
            for (int index = 1; index <= 4; index++)
            {
                Assert.Greater(counts[index], 0, "a zero total weight must degrade to uniform, not to one colour");
            }
        }

        [Test]
        public void SameSeedAndWeights_ProduceTheSameSequence()
        {
            var weights = new List<ColorWeight>
            {
                new ColorWeight(ChipColor.C1, 2),
                new ColorWeight(ChipColor.C3, 5),
                new ColorWeight(ChipColor.C4, 1)
            };
            var first = new WeightedChipSpawnPolicy(new DeterministicRandom(77), colorCount: 4, weights: weights);
            var second = new WeightedChipSpawnPolicy(new DeterministicRandom(77), colorCount: 4, weights: weights);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(first.NextColor(i % 4), second.NextColor(i % 4), "spawning must be deterministic (D12)");
            }
        }

        private static TranscriptWriter NewWriter(out TurnTranscript transcript)
        {
            transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);
            writer.TurnBegin(seed: 1, movesLeft: 10);
            return writer;
        }

        private static int CountSpawns(TurnTranscript transcript)
        {
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                if (transcript.GetEvent(i).Kind == TurnEventKind.ChipSpawned)
                {
                    count++;
                }
            }

            return count;
        }

        private static TurnEvent FirstSpawn(TurnTranscript transcript)
        {
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.ChipSpawned)
                {
                    return e;
                }
            }

            Assert.Fail("no ChipSpawned event was written");
            return default;
        }

        /// <summary>Maps a column to a colour so the x-ascending order is observable.</summary>
        private sealed class RecordingSpawnPolicy : IChipSpawnPolicy
        {
            public List<int> Columns { get; } = new List<int>();

            public ChipColor NextColor(int x)
            {
                Columns.Add(x);
                return ChipColors.FromIndex(x + 1);
            }
        }

        /// <summary>Counts draws to prove one RNG call per spawned chip (§13).</summary>
        private sealed class CountingRandom : IRandom
        {
            private readonly IRandom _inner;

            public CountingRandom(int seed)
            {
                _inner = new DeterministicRandom(seed);
            }

            public int Draws { get; private set; }

            public int Seed => _inner.Seed;

            public int NextInt(int maxExclusive)
            {
                Draws++;
                return _inner.NextInt(maxExclusive);
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                Draws++;
                return _inner.NextInt(minInclusive, maxExclusive);
            }

            public void Shuffle<T>(IList<T> list) => _inner.Shuffle(list);
        }
    }
}
