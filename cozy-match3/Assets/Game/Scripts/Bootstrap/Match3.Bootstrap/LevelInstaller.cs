using System.Collections.Generic;
using Match3.Board;
using Match3.Boosters;
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

            // The same wiring the determinism and bot harnesses use, so they cannot drift apart.
            LevelRuntime runtime = LevelRuntime.Create(level, request.Seed, elements, logger);

            logger.Info("level_start id=" + level.Id + " attempt=" + request.Attempt + " seed=" + request.Seed);

            var session = new LevelSessionState(
                level.Id,
                level.Tier,
                request.Seed,
                level.MoveLimit,
                runtime.Goals,
                level.HintDelaySeconds);

            InstallRuntime(request, runtime, session);
            InstallPresentation();
        }

        private void InstallRuntime(LevelSessionRequest request, LevelRuntime runtime, LevelSessionState session)
        {
            Container.BindInstance(request);
            Container.BindInstance(runtime);
            Container.BindInstance(runtime.Rules);
            Container.BindInstance(runtime.Random);
            Container.BindInstance(runtime.Board);
            Container.BindInstance<IBoardReader>(runtime.Board);
            Container.BindInstance(runtime.Goals);
            Container.Bind<IGoalTracker>().FromInstance(runtime.Goals).AsSingle();
            Container.BindInstance(runtime.Detection);
            Container.BindInstance(runtime.Validator);
            Container.BindInstance(runtime.LegalMoves);
            Container.Bind<ITargetingService>().FromInstance(runtime.Targeting).AsSingle();
            Container.BindInstance(runtime.Boosters);
            Container.BindInstance(runtime.Combos);
            Container.Bind<IChipSpawnPolicy>().FromInstance(runtime.SpawnPolicy).AsSingle();
            Container.BindInstance(runtime.Activation);
            Container.BindInstance(runtime.Hint);
            Container.BindInstance(runtime.TurnRule);

            // Bound as IDisposable too, so destroying the context closes its R3 subjects.
            Container.Bind(typeof(LevelSessionState), typeof(System.IDisposable))
                .FromInstance(session)
                .AsSingle();

            Container.Bind<ILevelInputState>().To<LevelInputState>().AsSingle();
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
