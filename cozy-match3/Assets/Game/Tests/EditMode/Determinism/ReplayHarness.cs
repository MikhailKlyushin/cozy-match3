using System.Collections.Generic;
using Match3.Bootstrap;
using Match3.Core;
using Match3.Levels;
using Match3.Matching;
using Match3.Resolve;

namespace Match3.Tests.EditMode.Determinism
{
    public enum ReplayInputKind : byte
    {
        Swap = 0,
        Tap = 1,
        Cheat = 2
    }

    /// <summary>One recorded player action. A log of these plus a seed reproduces a whole game.</summary>
    public readonly struct ReplayInput
    {
        public readonly ReplayInputKind Kind;
        public readonly GridPos A;
        public readonly GridPos B;
        public readonly CheatCommand Command;

        private ReplayInput(ReplayInputKind kind, GridPos a, GridPos b, CheatCommand cheat)
        {
            Kind = kind;
            A = a;
            B = b;
            Command = cheat;
        }

        public static ReplayInput Swap(GridPos a, GridPos b)
            => new ReplayInput(ReplayInputKind.Swap, a, b, default);

        public static ReplayInput Tap(GridPos cell)
            => new ReplayInput(ReplayInputKind.Tap, cell, GridPos.Invalid, default);

        public static ReplayInput Cheat(CheatCommand command)
            => new ReplayInput(ReplayInputKind.Cheat, GridPos.Invalid, GridPos.Invalid, command);
    }

    /// <summary>Outcome of one replay: the board hash after every turn, plus what went wrong.</summary>
    public sealed class ReplayResult
    {
        /// <summary>
        /// The transcript counts cascade steps from zero, so a turn that resolves in a single
        /// wave reports 0. §14 counts that turn as depth 1, and this is where the two meet.
        /// </summary>
        internal const int DepthBase = 1;

        public List<ulong> Hashes { get; } = new List<ulong>();

        public List<TurnOutcome> Outcomes { get; } = new List<TurnOutcome>();

        public int Turns => Hashes.Count;

        public int MaxCascadeDepth { get; internal set; }

        /// <summary>Summed over turns; divided by <see cref="Turns"/> gives the average (§14).</summary>
        public int CascadeDepthSum { get; internal set; }

        public int CapHits { get; internal set; }

        public int Deadlocks { get; internal set; }

        public int Shuffles { get; internal set; }

        public int Rejected { get; internal set; }

        /// <summary>
        /// Moves left after the last charged move. Read from the transcript rather than from the
        /// rule, because a win spends the remainder as bonus rockets and leaves the counter at
        /// zero (§8.4, E23).
        /// </summary>
        public int MovesLeft { get; internal set; }

        public TurnOutcome Final { get; internal set; }
    }

    /// <summary>
    /// Runs a seed plus an input log and reports the board hash after each turn (§8, §13). Two
    /// runs of the same pair must agree exactly - that is the whole determinism claim, reduced to
    /// comparing numbers.
    /// </summary>
    public static class ReplayRunner
    {
        public static ReplayResult Run(LevelData level, int seed, IReadOnlyList<ReplayInput> inputs, IRandom random = null)
        {
            LevelRuntime runtime = Create(level, seed, random);
            var result = new ReplayResult { Final = TurnOutcome.Resolved };

            for (int i = 0; i < inputs.Count; i++)
            {
                if (!runtime.TurnRule.CanAcceptInput)
                {
                    break;
                }

                if (Play(result, runtime, inputs[i]))
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// The T26 bot plays the level out and records what it did. The recorded log replays
        /// through <see cref="Run"/>, which is how the bot doubles as a determinism fixture.
        /// </summary>
        public static ReplayResult RunGreedy(
            LevelData level,
            int seed,
            List<ReplayInput> recordedInputs = null,
            IRandom random = null)
        {
            LevelRuntime runtime = Create(level, seed, random);
            var bot = new GreedyBot(runtime);

            return Drive(runtime, recordedInputs, (LevelRuntime _, out ReplayInput move) => bot.TryChooseMove(out move));
        }

        /// <summary>
        /// Fuzz policy of T24: a uniformly random legal input. <paramref name="policySeed"/> feeds
        /// a stream of its own, so choosing a move never draws from the attempt's RNG and the §13
        /// consumption order stays exactly what the game itself consumed.
        /// </summary>
        public static ReplayResult RunRandom(
            LevelData level,
            int seed,
            int policySeed,
            List<ReplayInput> recordedInputs = null,
            IRandom random = null)
        {
            LevelRuntime runtime = Create(level, seed, random);
            var policy = new DeterministicRandom(policySeed);
            var boosters = new List<GridPos>(8);

            return Drive(runtime, recordedInputs, (LevelRuntime r, out ReplayInput move)
                => TryChooseRandom(r, policy, boosters, out move));
        }

        private delegate bool MovePolicy(LevelRuntime runtime, out ReplayInput move);

        private static LevelRuntime Create(LevelData level, int seed, IRandom random)
            => LevelRuntime.Create(
                level, seed, Board.BuiltInElementCatalog.Create(), NullLogger.Instance, random);

        private static ReplayResult Drive(
            LevelRuntime runtime,
            List<ReplayInput> recordedInputs,
            MovePolicy policy)
        {
            var result = new ReplayResult { Final = TurnOutcome.Resolved };

            while (runtime.TurnRule.CanAcceptInput && runtime.TurnRule.MovesLeft > 0)
            {
                if (!policy(runtime, out ReplayInput input))
                {
                    break;
                }

                recordedInputs?.Add(input);

                if (Play(result, runtime, input))
                {
                    break;
                }
            }

            return result;
        }

        private static bool TryChooseRandom(
            LevelRuntime runtime,
            IRandom policy,
            List<GridPos> boosters,
            out ReplayInput move)
        {
            IReadOnlyList<LegalMove> moves = runtime.LegalMoves.Moves;

            boosters.Clear();
            for (int y = 0; y < runtime.Board.Height; y++)
            {
                for (int x = 0; x < runtime.Board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (runtime.Board.GetSlot(cell).Kind == SlotKind.Booster && runtime.Board.IsMovable(cell))
                    {
                        boosters.Add(cell);
                    }
                }
            }

            int total = moves.Count + boosters.Count;
            if (total == 0)
            {
                move = default;
                return false;
            }

            int pick = policy.NextInt(total);
            move = pick < moves.Count
                ? ReplayInput.Swap(moves[pick].A, moves[pick].B)
                : ReplayInput.Tap(boosters[pick - moves.Count]);

            return true;
        }

        /// <summary>Executes one input and folds it into the result; true means the level ended.</summary>
        private static bool Play(ReplayResult result, LevelRuntime runtime, in ReplayInput input)
        {
            TurnTranscript transcript = Execute(runtime, input);
            Record(result, runtime, transcript);

            return transcript.Outcome == TurnOutcome.Won || transcript.Outcome == TurnOutcome.Lost;
        }

        private static TurnTranscript Execute(LevelRuntime runtime, in ReplayInput input)
        {
            switch (input.Kind)
            {
                case ReplayInputKind.Swap:
                    return runtime.TurnRule.ExecuteSwap(input.A, input.B);
                case ReplayInputKind.Tap:
                    return runtime.TurnRule.ExecuteTap(input.A);
                default:
                    return runtime.TurnRule.ExecuteCheat(input.Command);
            }
        }

        private static void Record(ReplayResult result, LevelRuntime runtime, TurnTranscript transcript)
        {
            result.Hashes.Add(runtime.Board.ComputeHash());
            result.Outcomes.Add(transcript.Outcome);
            result.Final = transcript.Outcome;

            int depth = transcript.MaxDepth + ReplayResult.DepthBase;
            result.CascadeDepthSum += depth;

            if (depth > result.MaxCascadeDepth)
            {
                result.MaxCascadeDepth = depth;
            }

            if (transcript.Outcome == TurnOutcome.Rejected)
            {
                result.Rejected++;
            }

            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                switch (e.Kind)
                {
                    case TurnEventKind.MoveCharged:
                        result.MovesLeft = e.Value;
                        break;
                    case TurnEventKind.CapHit:
                        result.CapHits++;
                        break;
                    case TurnEventKind.ShuffleBegin:
                        result.Shuffles++;
                        break;
                    case TurnEventKind.LevelLost when e.EndReason == LevelEndReason.Deadlock:
                        result.Deadlocks++;
                        break;
                }
            }
        }
    }
}
