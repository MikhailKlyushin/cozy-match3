using System.Collections.Generic;
using Match3.Board;
using Match3.Boosters;
using Match3.Content;
using Match3.Core;
using Match3.Gameplay;
using Match3.Gameplay.Playback;
using Match3.Goals;
using Match3.Levels;
using Match3.Matching;
using Match3.Progression;
using Match3.Resolve;
using UnityEngine;
using Zenject;
using BoardModel = Match3.Board.Board;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Composition root of ONE level attempt, installed into the LevelContext's sub-container
    /// (A08). Destroying that object closes the R3 subscriptions, the tweens and the container in
    /// one go, so a restart is simply a new context.
    /// </summary>
    public sealed class LevelInstaller : MonoInstaller
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private BoardInputPresenter _input;
        [SerializeField] private HintView _hintView;

        public override void InstallBindings()
        {
            LevelSessionRequest request = Container.Resolve<LevelSessionRequestHolder>().Take();
            LevelData level = request.Level;

            var logger = Container.Resolve<IMatch3Logger>();
            var elements = Container.Resolve<ElementCatalog>();

            IRandom random = new DeterministicRandom(request.Seed);
            logger.Info("level_start id=" + level.Id + " attempt=" + request.Attempt + " seed=" + request.Seed);

            BoardModel board = new BoardBuilder(elements, logger).Build(level, random);
            var goals = new GoalTracker(level.Goals);

            var session = new LevelSessionState(
                level.Id,
                level.Tier,
                request.Seed,
                level.MoveLimit,
                goals,
                level.HintDelaySeconds);

            Container.BindInstance(request);
            Container.BindInstance(request.Rules);
            Container.BindInstance(random);
            Container.BindInstance(board);
            Container.BindInstance<IBoardReader>(board);
            Container.Bind<IGoalTracker>().FromInstance(goals).AsSingle();
            Container.BindInstance(goals);
            Container.Bind(typeof(LevelSessionState), typeof(System.IDisposable))
                .FromInstance(session)
                .AsSingle();
            Container.Bind<ILevelInputState>().To<LevelInputState>().AsSingle();

            InstallRules(board, goals, random, request.Rules, level, logger);
            InstallPresentation();
        }

        private void InstallRules(
            BoardModel board,
            GoalTracker goals,
            IRandom random,
            LevelRules rules,
            LevelData level,
            IMatch3Logger logger)
        {
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
            var shuffle = new ShuffleService(board, detection, legalMoves, spawnPolicy, random, logger);
            var bonus = new MovesBonusService(board, random);
            var hint = new HintService(board, legalMoves, detection, goals, rules);

            var turnRule = new TurnRule(
                board, rules, validator, legalMoves, combos, activation, loop, goals,
                shuffle, bonus, hint, random, logger, new TurnTranscript(), new ResolveContext());

            Container.BindInstance(detection);
            Container.BindInstance(validator);
            Container.BindInstance(legalMoves);
            Container.Bind<ITargetingService>().FromInstance(targeting).AsSingle();
            Container.BindInstance(boosters);
            Container.BindInstance(combos);
            Container.Bind<IChipSpawnPolicy>().FromInstance(spawnPolicy).AsSingle();
            Container.BindInstance(spawn);
            Container.BindInstance(activation);
            Container.BindInstance(damage);
            Container.BindInstance(clear);
            Container.BindInstance(gravity);
            Container.BindInstance(refill);
            Container.BindInstance(loop);
            Container.BindInstance(shuffle);
            Container.BindInstance(bonus);
            Container.BindInstance(hint);
            Container.BindInstance(turnRule);
        }

        private void InstallPresentation()
        {
            Container.BindInstance(_boardView);
            Container.Bind<IBoardCellPicker>().FromInstance(_boardView).AsSingle();
            Container.BindInstance(_input);
            Container.BindInstance(_hintView);

            Container.Bind<FxRegistry>().AsSingle();
            Container.Bind<CameraShake>().AsSingle();

            // One player per event family (A11): a new event kind is a new player, never an edit
            // to a central switch.
            Container.Bind<ITurnEventPlayer>().To<BarrierEventPlayer>().AsSingle();
            Container.Bind<ITurnEventPlayer>().To<SwapEventPlayer>().AsSingle();
            Container.Bind<ITurnEventPlayer>().To<ChipLifecycleEventPlayer>().AsSingle();
            Container.Bind<ITurnEventPlayer>().To<MovementEventPlayer>().AsSingle();
            Container.Bind<ITurnEventPlayer>().To<ElementEventPlayer>().AsSingle();
            Container.Bind<ITurnEventPlayer>().To<ShuffleEventPlayer>().AsSingle();
            Container.Bind<ITurnEventPlayer>().To<BoosterEventPlayer>().AsSingle();
            Container.Bind<ITurnEventPlayer>().To<ComboEventPlayer>().AsSingle();

            // Zenject fills List<T> and T[] from multiple bindings, but not IReadOnlyList<T>,
            // which is what TranscriptPlayer asks for.
            Container.Bind<IReadOnlyList<ITurnEventPlayer>>()
                .FromMethod(context => context.Container.ResolveAll<ITurnEventPlayer>())
                .AsSingle();

            Container.Bind<TranscriptPlayer>().AsSingle();
            Container.BindInterfacesAndSelfTo<HintPresenter>().AsSingle();
            Container.BindInterfacesAndSelfTo<LevelTurnController>().AsSingle();

#if MATCH3_CHEATS
            Container.BindInterfacesTo<CheatAttemptBinder>().AsSingle();
#endif

            // Runs after injection, which is when BoardView finally has its profiles.
            Container.BindInterfacesTo<LevelViewBootstrap>().AsSingle();
        }
    }
}
