using System.Collections.Generic;
using Match3.Board;
using Match3.Content;
using Match3.Core;
using Match3.Diagnostics;
using Match3.Hud;
using Match3.Levels.Authoring;
using Match3.Progression;
using R3;
using UnityEngine;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Game scene composition root: the long-lived services, the HUD and the level flow. One
    /// attempt lives in its own sub-container, created by <see cref="LevelContextFactory"/> (A08).
    /// </summary>
    public sealed class GameInstaller : MonoInstaller
    {
        [Header("Content")]
        [SerializeField] private LevelCatalog _levelCatalog;
        [SerializeField] private TimingProfile _timings;
        [SerializeField] private ChipVisualProfile _chipProfile;
        [SerializeField] private ElementVisualProfile _elementProfile;

        [Header("Level attempt")]
        [SerializeField] private GameObject _levelContextPrefab;
        [SerializeField] private Transform _levelContextParent;

        [Header("HUD")]
        [SerializeField] private MovesCounterView _movesCounter;
        [SerializeField] private GoalsPanelView _goalsPanel;
        [SerializeField] private HudActionsView _hudActions;
        [SerializeField] private PopupView[] _popups = new PopupView[0];

        [Header("Audio")]
        [SerializeField] private AudioProfile _audioProfile;
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private SoundToggleView _soundToggle;

#if MATCH3_CHEATS
        [Header("Cheats (development builds only)")]
        [SerializeField] private Match3.Cheats.CheatsRootView _cheatsPrefab;
#endif

        public override void InstallBindings()
        {
            // Without this an exception inside an R3 chain disappears silently (A09).
            var logger = new UnityMatch3Logger();
            ObservableSystem.RegisterUnhandledExceptionHandler(
                e => logger.Error("Unhandled R3 exception: " + e));

            Container.Bind<IMatch3Logger>().FromInstance(logger).AsSingle();

            InstallContent();
            InstallProgression();
            InstallHud();
            InstallAudio();

            Container.BindInterfacesAndSelfTo<GameFlowController>().AsSingle();
            InstallCheats();
        }

        private void InstallContent()
        {
            Container.BindInstance(_levelCatalog);
            Container.BindInstance(_timings);
            Container.BindInstance(_chipProfile);
            Container.BindInstance(_elementProfile);

            // The §7.2 catalogue is data, so one instance serves every attempt (A04).
            Container.BindInstance(BuiltInElementCatalog.Create());

            Container.Bind<GoalIconResolver>().AsSingle();
            Container.Bind<MetricsReporter>().AsSingle();
        }

        private void InstallProgression()
        {
            Container.Bind<IProgressStorage>().To<PlayerPrefsProgressStorage>().AsSingle();
            Container.Bind<ProgressRepository>().AsSingle();
            Container.Bind<ILevelSeedSource>().To<SystemSeedSource>().AsSingle();
            Container.Bind<LevelSessionRequestHolder>().AsSingle();

            Container.Bind<ILevelContextFactory>()
                .To<LevelContextFactory>()
                .AsSingle()
                .WithArguments(_levelContextPrefab, _levelContextParent);

            Container.BindInterfacesAndSelfTo<LevelSessionFactory>().AsSingle();
            Container.BindInterfacesAndSelfTo<LevelFlowRule>().AsSingle();
        }

        private void InstallHud()
        {
            Container.BindInstance(_movesCounter);
            Container.BindInstance(_goalsPanel);
            Container.BindInstance(_hudActions);

            Container.BindInterfacesAndSelfTo<MovesCounterPresenter>().AsSingle();
            Container.BindInterfacesAndSelfTo<GoalsPanelPresenter>().AsSingle();

            IReadOnlyList<PopupView> popups = _popups;
            Container.Bind<PopupService>().AsSingle().WithArguments(popups);
        }

        /// <summary>
        /// Every part is optional by design and independent of the others: a missing one is a
        /// content gap, not a broken invariant, and the toggle still mutes whatever else plays.
        /// The effect player is always bound, as a no-op when there is no profile, so presentation
        /// never has to ask whether sound exists.
        /// </summary>
        private void InstallAudio()
        {
            if (_audioProfile == null)
            {
                Debug.LogWarning("[Match3] No audio profile; the scene stays silent.");
                Container.Bind<ISfxPlayer>().To<NullSfxPlayer>().AsSingle();
            }
            else
            {
                Container.BindInstance(_audioProfile);
                Container.BindInterfacesAndSelfTo<SfxPlayer>().AsSingle();

                if (_ambientSource != null)
                {
                    Container.BindInterfacesAndSelfTo<AmbientAudioService>()
                        .AsSingle()
                        .WithArguments(_ambientSource);
                }
                else
                {
                    Debug.LogWarning("[Match3] No ambience source; the game plays without its loop.");
                }
            }

            if (_soundToggle != null)
            {
                Container.BindInterfacesAndSelfTo<AudioMuteService>()
                    .AsSingle()
                    .WithArguments(_soundToggle);
            }
            else
            {
                Debug.LogWarning("[Match3] No sound toggle; the player cannot mute the game.");
            }
        }

        private void InstallCheats()
        {
#if MATCH3_CHEATS
            if (_cheatsPrefab == null)
            {
                Debug.LogWarning("[Match3] MATCH3_CHEATS is on but no cheat panel prefab is assigned.");
                return;
            }

            Match3.Cheats.CheatsRootView root =
                Container.InstantiatePrefabForComponent<Match3.Cheats.CheatsRootView>(
                    _cheatsPrefab, transform.parent);

            Container.BindInstance(root);
            Container.BindInterfacesAndSelfTo<Match3.Cheats.CheatPanelPresenter>().AsSingle();
            Container.BindInterfacesTo<CheatButtonBinder>().AsSingle();
#endif
        }
    }
}
