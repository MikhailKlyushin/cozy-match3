using System.Collections;
using Cysharp.Threading.Tasks;
using Match3.Bootstrap;
using Match3.Core;
using Match3.Gameplay;
using Match3.Gameplay.Playback;
using Match3.Hud;
using Match3.Progression;
using Match3.Resolve;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Zenject;

namespace Match3.Tests.PlayMode
{
    /// <summary>
    /// The one play-mode smoke of §19: the scene composes, the first level builds, a real turn
    /// plays through the transcript player, and the win cheat opens its popup. Everything else is
    /// covered without a scene.
    /// </summary>
    public sealed class GameSmokeTests
    {
        private const string GameScene = "Game";

        [UnityTest]
        public IEnumerator GameScene_BuildsTheFirstLevel()
        {
            yield return LoadGameScene();

            BoardView board = Object.FindFirstObjectByType<BoardView>();
            Assert.IsNotNull(board, "the level context did not create a BoardView");
            Assert.Greater(board.BoardWidth, 0, "the board was never built");
            Assert.Greater(board.Layout.CellSize, 0f, "the layout has no cell size");

            var levelLabel = Object.FindFirstObjectByType<LevelLabelView>();
            Assert.IsNotNull(levelLabel, "the HUD has no level label wired to the installer");
        }

        [UnityTest]
        public IEnumerator FirstLevel_PlaysAHintedTurnWithoutExceptions()
        {
            yield return LoadGameScene();

            DiContainer level = ResolveLevelContainer();
            var turnRule = level.Resolve<TurnRule>();
            var player = level.Resolve<TranscriptPlayer>();
            var session = level.Resolve<LevelSessionState>();

            HintPlan hint = turnRule.GetHint();
            Assert.IsTrue(hint.HasHint, "a freshly built level must offer a legal move (E20)");
            Assert.AreEqual(HintKind.Swap, hint.Kind);

            int movesBefore = session.MovesLeft.CurrentValue;

            TurnTranscript transcript = turnRule.ExecuteSwap(hint.A, hint.B);
            Assert.AreNotEqual(TurnOutcome.Rejected, transcript.Outcome, "the hinted swap must be legal");

            // ToCoroutine is the supported bridge; polling UniTask.Status would consume the
            // awaiter more than once.
            var token = level.Resolve<LevelSessionRequest>().Token;
            yield return UniTask.ToCoroutine(() => player.PlayAsync(transcript, token));

            Assert.IsFalse(player.IsPlaying, "playback did not finish");
            Assert.Less(turnRule.MovesLeft, movesBefore, "the move must be charged (D04)");
        }

        [UnityTest]
        public IEnumerator WinCheat_FinishesTheLevel()
        {
            yield return LoadGameScene();

            DiContainer level = ResolveLevelContainer();
            var turnRule = level.Resolve<TurnRule>();

            TurnTranscript transcript = turnRule.ExecuteCheat(CheatCommand.WinLevel());

            Assert.AreEqual(TurnOutcome.Won, transcript.Outcome);
            Assert.IsTrue(level.Resolve<Match3.Goals.IGoalTracker>().AllClosed);
            Assert.IsFalse(turnRule.CanAcceptInput, "a finished level stops taking input");

            // Let the flow react so a broken popup path shows up here rather than in the build.
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator LoadGameScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            Assert.IsNotNull(load, "Game scene is not in the build settings");

            while (!load.isDone)
            {
                yield return null;
            }

            // The scene context installs on Awake and the attempt is created from Initialize, so
            // the board exists only from the next frame.
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
        }

        private static DiContainer ResolveLevelContainer()
        {
            GameObjectContext[] contexts = Object.FindObjectsByType<GameObjectContext>(FindObjectsSortMode.None);
            Assert.AreEqual(1, contexts.Length, "expected exactly one live level context");
            return contexts[0].Container;
        }
    }
}
