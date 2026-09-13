using System.Collections.Generic;
using Match3.Board;
using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using Match3.Matching;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Turn
{
    /// <summary>
    /// Assembles the whole resolve pipeline over a board built from a token layout, so the turn
    /// cycle can be driven without a scene or a container.
    /// </summary>
    internal sealed class TurnFixture
    {
        private TurnFixture(BoardModel board, GoalTracker goals, TurnRule turnRule, RecordingLogger logger)
        {
            Board = board;
            Goals = goals;
            TurnRule = turnRule;
            Logger = logger;
        }

        public BoardModel Board { get; }

        public GoalTracker Goals { get; }

        public TurnRule TurnRule { get; }

        public RecordingLogger Logger { get; }

        public static TurnFixture Create(
            string layout,
            IReadOnlyList<GoalDefinition> goals = null,
            int moveLimit = 20,
            int colorCount = 4,
            int seed = 1,
            IChipSpawnPolicy spawnPolicy = null,
            float hintDelaySeconds = 5f,
            int levelId = 1)
        {
            BoardModel board = BoardFixture.From(layout);
            var logger = new RecordingLogger();
            var random = new DeterministicRandom(seed);
            var tracker = new GoalTracker(goals ?? new[] { GoalDefinition.CollectColor(ChipColor.C1, 999) });
            var rules = new LevelRules(levelId, colorCount, moveLimit, hintDelaySeconds);

            var detection = new MatchDetectionService(board);
            var validator = new SwapValidator(board, detection);
            var legalMoves = new LegalMoveService(board, validator);
            var targeting = new TargetingService(board, tracker, random);
            var catalog = new BoosterCatalog(new IBoosterEffect[]
            {
                new RocketEffect(BoosterType.RocketH),
                new RocketEffect(BoosterType.RocketV),
                new BombEffect(),
                new RainbowEffect(targeting),
                new AirplaneEffect(targeting),
            });
            var combos = new ComboResolver(board, targeting);

            IChipSpawnPolicy policy = spawnPolicy
                                      ?? new WeightedChipSpawnPolicy(random, colorCount, null);

            var spawn = new BoosterSpawnService(board);
            var activation = new ActivationService(board, catalog, tracker, logger);
            var damage = new DamageService(board, DamageRules.CreateDefault(), logger);
            var clear = new ClearService(board, tracker, logger);
            var gravity = new GravityService(board);
            var refill = new RefillService(board, policy);
            var loop = new ResolveLoopService(
                board, detection, spawn, activation, damage, clear, gravity, refill, logger);
            var shuffle = new ShuffleService(board, detection, legalMoves, policy, rules, random, logger);
            var bonus = new MovesBonusService(board, random);
            var hint = new HintService(board, legalMoves, detection, tracker, rules);

            var turnRule = new TurnRule(
                board, rules, validator, legalMoves, combos, activation, loop, tracker,
                shuffle, bonus, hint, random, logger, new TurnTranscript(), new ResolveContext());

            return new TurnFixture(board, tracker, turnRule, logger);
        }

        /// <summary>Index of the first event of a kind, or -1.</summary>
        public static int IndexOf(TurnTranscript transcript, TurnEventKind kind, int from = 0)
        {
            for (int i = from; i < transcript.EventCount; i++)
            {
                if (transcript.GetEvent(i).Kind == kind)
                {
                    return i;
                }
            }

            return -1;
        }

        public static int CountOf(TurnTranscript transcript, TurnEventKind kind)
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

        public static string Describe(TurnTranscript transcript)
        {
            var text = new System.Text.StringBuilder(512);
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                text.Append(i).Append(' ').Append(e.Kind)
                    .Append(" step=").Append(e.Step)
                    .Append(" wave=").Append(e.Wave)
                    .Append(" a=").Append(e.A.ToString())
                    .Append('\n');
            }

            return text.ToString();
        }
    }

    /// <summary>Captures logger calls so a test can assert an invariant was reported (E05, E19).</summary>
    internal sealed class RecordingLogger : IMatch3Logger
    {
        public List<string> Infos { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public List<string> Errors { get; } = new List<string>();

        public void Info(string message) => Infos.Add(message);

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message) => Errors.Add(message);
    }

    /// <summary>Refill with a fixed colour, so a cascade in a test is reproducible by hand.</summary>
    internal sealed class FixedColorSpawnPolicy : IChipSpawnPolicy
    {
        private readonly ChipColor _color;

        public FixedColorSpawnPolicy(ChipColor color) => _color = color;

        public ChipColor NextColor(int x) => _color;
    }

    /// <summary>Refill from a scripted sequence, cycling when it runs out.</summary>
    internal sealed class ScriptedSpawnPolicy : IChipSpawnPolicy
    {
        private readonly ChipColor[] _colors;
        private int _index;

        public ScriptedSpawnPolicy(params ChipColor[] colors) => _colors = colors;

        public ChipColor NextColor(int x) => _colors[_index++ % _colors.Length];
    }
}
