#if MATCH3_CHEATS
using System;
using Match3.Cheats;
using Match3.Content;
using Match3.Core;
using Match3.Gameplay;
using Match3.Goals;
using Match3.Levels.Authoring;
using Match3.Progression;
using Match3.Resolve;
using Match3.Tests.EditMode.Progression;
using Match3.Tests.EditMode.Turn;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Match3.Tests.EditMode.Cheats
{
    /// <summary>
    /// Which attempt the panel holds (§12). The order matters and is not the obvious one: Zenject
    /// initialises a dynamically created GameObjectContext immediately, while Unity destroys the
    /// previous one at the end of the frame, so a new attempt binds itself before the old one
    /// disposes. An unconditional unbind in that teardown left the panel with no attempt at all,
    /// which greyed out every board cheat from the first restart onwards.
    /// </summary>
    public sealed class CheatPanelBindingTests
    {
        private const string Layout = @"
            t3 t4 t3 t4 t3
            t4 t3 t4 t3 t4
            t3 t4 t1 t4 t3
            t1 t1 t2 t3 t4";

        private GameObject _rootGo;
        private LevelCatalog _catalog;
        private ChipVisualProfile _visuals;
        private LevelSessionFactory _sessions;
        private LevelFlowRule _flow;
        private CheatPanelPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            _visuals = ScriptableObject.CreateInstance<ChipVisualProfile>();
            _sessions = new LevelSessionFactory(new UnusedContextFactory(), new FixedSeedSource());
            _flow = new LevelFlowRule(
                new ProgressRepository(new FakeProgressStorage()),
                _catalog,
                _sessions,
                NullLogger.Instance);

            _presenter = new CheatPanelPresenter(CreateRoot(), _flow, _visuals, NullLogger.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _flow.Dispose();
            _sessions.Dispose();

            UnityEngine.Object.DestroyImmediate(_rootGo);
            UnityEngine.Object.DestroyImmediate(_visuals);
            UnityEngine.Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void AFreshPanelHoldsNoAttempt()
        {
            Assert.IsFalse(_presenter.HasAttempt);
        }

        [Test]
        public void TheLateTeardownOfAReplacedAttemptLeavesTheLiveOneBound()
        {
            CheatLevelBinding previous = CreateBinding();
            CheatLevelBinding current = CreateBinding();

            _presenter.Bind(previous);
            _presenter.Bind(current);

            // The order a level change actually produces: the successor binds, then the dead
            // attempt's container disposes.
            _presenter.Unbind(previous);

            Assert.IsTrue(_presenter.HasAttempt);
        }

        [Test]
        public void TheTeardownOfTheLiveAttemptReleasesIt()
        {
            CheatLevelBinding binding = CreateBinding();
            _presenter.Bind(binding);

            _presenter.Unbind(binding);

            Assert.IsFalse(_presenter.HasAttempt);
        }

        [Test]
        public void AnAttemptThatNeverBoundReleasesNothing()
        {
            _presenter.Bind(CreateBinding());

            _presenter.Unbind(null);

            Assert.IsTrue(_presenter.HasAttempt);
        }

        [Test]
        public void ThePanelTeardownReleasesWhateverIsBound()
        {
            _presenter.Bind(CreateBinding());

            _presenter.Unbind();

            Assert.IsFalse(_presenter.HasAttempt);
        }

        private CheatsRootView CreateRoot()
        {
            _rootGo = new GameObject("CheatsRoot", typeof(RectTransform));

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(_rootGo.transform, false);

            var overlayGo = new GameObject("Overlay", typeof(RectTransform));
            overlayGo.transform.SetParent(_rootGo.transform, false);

            var catcherGo = new GameObject("TapCatcher", typeof(RectTransform));
            catcherGo.transform.SetParent(_rootGo.transform, false);

            var root = _rootGo.AddComponent<CheatsRootView>();
            var so = new SerializedObject(root);
            so.FindProperty("_panel").objectReferenceValue = panelGo.AddComponent<CheatPanelView>();
            so.FindProperty("_gridOverlay").objectReferenceValue = overlayGo.AddComponent<CheatGridOverlayView>();
            so.FindProperty("_tapCatcher").objectReferenceValue = catcherGo.AddComponent<CheatBoardTapCatcher>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static CheatLevelBinding CreateBinding()
        {
            TurnFixture fixture = TurnFixture.Create(Layout, moveLimit: 10);
            var session = new LevelSessionState(
                1,
                DifficultyTier.Easy,
                1,
                10,
                new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 5) }),
                5f);

            return new CheatLevelBinding(
                session,
                fixture.TurnRule,
                new IdleCellPicker(),
                new IdleTurnRunner(),
                new IdleHintControl());
        }

        /// <summary>The flow is a constructor dependency of the panel; no test here starts a level.</summary>
        private sealed class UnusedContextFactory : ILevelContextFactory
        {
            public LevelSessionState Create(LevelSessionRequest request)
                => throw new NotSupportedException("These tests never start a level.");

            public void Destroy()
            {
            }
        }

        private sealed class FixedSeedSource : ILevelSeedSource
        {
            public int NextSeed() => 1;
        }

        private sealed class IdleCellPicker : IBoardCellPicker
        {
            public int BoardWidth => 5;

            public int BoardHeight => 4;

            public GridPos PickCell(Vector2 screenPosition) => GridPos.Invalid;

            public Vector2 CellToScreen(GridPos cell) => Vector2.zero;
        }

        private sealed class IdleTurnRunner : ICheatTurnRunner
        {
            public bool CanRun => true;

            public TurnTranscript LastTranscript => null;

            public void ExecuteCheat(in CheatCommand command)
            {
            }
        }

        private sealed class IdleHintControl : ICheatHintControl
        {
            public void SetSuspended(bool suspended)
            {
            }

            public void ShowNow()
            {
            }
        }
    }
}
#endif
