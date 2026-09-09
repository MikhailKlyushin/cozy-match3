using System;
using Match3.Board;
using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using Match3.Levels;
using Match3.Matching;
using Match3.Resolve;
using BoardModel = Match3.Board.Board;

namespace Match3.Bootstrap
{
    /// <summary>
    /// The rules half of one level attempt, wired without Unity or a container. The installer
    /// binds these instances and the determinism and bot harnesses build them directly, so there
    /// is exactly one description of how the pipeline fits together.
    /// </summary>
    public sealed class LevelRuntime
    {
        private LevelRuntime(
            LevelData level,
            LevelRules rules,
            IRandom random,
            BoardModel board,
            GoalTracker goals,
            MatchDetectionService detection,
            SwapValidator validator,
            LegalMoveService legalMoves,
            ITargetingService targeting,
            BoosterCatalog boosters,
            ComboResolver combos,
            IChipSpawnPolicy spawnPolicy,
            ActivationService activation,
            HintService hint,
            TurnRule turnRule)
        {
            Level = level;
            Rules = rules;
            Random = random;
            Board = board;
            Goals = goals;
            Detection = detection;
            Validator = validator;
            LegalMoves = legalMoves;
            Targeting = targeting;
            Boosters = boosters;
            Combos = combos;
            SpawnPolicy = spawnPolicy;
            Activation = activation;
            Hint = hint;
            TurnRule = turnRule;
        }

        public LevelData Level { get; }

        public LevelRules Rules { get; }

        public IRandom Random { get; }

        public BoardModel Board { get; }

        public GoalTracker Goals { get; }

        public MatchDetectionService Detection { get; }

        public SwapValidator Validator { get; }

        public LegalMoveService LegalMoves { get; }

        public ITargetingService Targeting { get; }

        public BoosterCatalog Boosters { get; }

        public ComboResolver Combos { get; }

        public IChipSpawnPolicy SpawnPolicy { get; }

        public ActivationService Activation { get; }

        public HintService Hint { get; }

        public TurnRule TurnRule { get; }

        /// <summary>
        /// Builds one attempt. <paramref name="random"/> lets a harness wrap the stream, for
        /// instance in a RecordingRandom to assert the §13 consumption order; pass null for the
        /// ordinary DeterministicRandom over the seed.
        /// </summary>
        public static LevelRuntime Create(
            LevelData level,
            int seed,
            ElementCatalog catalog,
            IMatch3Logger logger,
            IRandom random = null)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            logger = logger ?? NullLogger.Instance;
            random = random ?? new DeterministicRandom(seed);

            var rules = new LevelRules(level.Id, level.ColorCount, level.MoveLimit, level.HintDelaySeconds);
            BoardModel board = new BoardBuilder(catalog, logger).Build(level, random);
            var goals = new GoalTracker(level.Goals);

            var detection = new MatchDetectionService(board);
            var validator = new SwapValidator(board, detection);
            var legalMoves = new LegalMoveService(board, validator);
            var targeting = new TargetingService(board, goals, random);

            var boosters = new BoosterCatalog(new IBoosterEffect[]
            {
                new RocketEffect(BoosterType.RocketH),
                new RocketEffect(BoosterType.RocketV),
                new BombEffect(),
                new RainbowEffect(targeting),
                new AirplaneEffect(targeting),
            });

            IChipSpawnPolicy spawnPolicy =
                new WeightedChipSpawnPolicy(random, level.ColorCount, level.SpawnWeights);

            var combos = new ComboResolver(board, targeting);
            var spawn = new BoosterSpawnService(board);
            var activation = new ActivationService(board, boosters, goals, logger);
            var damage = new DamageService(board, DamageRules.CreateDefault(), logger);
            var clear = new ClearService(board, goals, logger);
            var gravity = new GravityService(board);
            var refill = new RefillService(board, spawnPolicy);
            var loop = new ResolveLoopService(
                board, detection, spawn, activation, damage, clear, gravity, refill, logger);
            var shuffle = new ShuffleService(board, detection, legalMoves, spawnPolicy, rules, random, logger);
            var bonus = new MovesBonusService(board, random);
            var hint = new HintService(board, legalMoves, detection, goals, rules);

            var turnRule = new TurnRule(
                board, rules, validator, legalMoves, combos, activation, loop, goals,
                shuffle, bonus, hint, random, logger, new TurnTranscript(), new ResolveContext());

            return new LevelRuntime(
                level, rules, random, board, goals, detection, validator, legalMoves, targeting,
                boosters, combos, spawnPolicy, activation, hint, turnRule);
        }
    }
}
