using System.Collections.Generic;
using System.Threading;
using Match3.Core;
using Match3.Goals;
using Match3.Levels.Authoring;
using Match3.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Match3.Tests.EditMode.Progression
{
    /// <summary>
    /// The §8.3 flow over a catalog built inside the test. Its ids are deliberately sparse and
    /// unrelated to the shipped set, so the rule cannot pass by assuming either.
    /// </summary>
    public sealed class LevelFlowRuleTests
    {
        private const int FirstId = 3;
        private const int MiddleId = 5;
        private const int LastId = 8;
        private const int ExtraId = 11;
        private const int MissingId = 4;
        private const int FirstSeed = 1000;
        private const int ExplicitSeed = 4242;
        private const int MoveLimitOffset = 10;

        private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();

        private FakeProgressStorage _storage;
        private ProgressRepository _progress;
        private LevelCatalog _catalog;
        private FakeLevelContextFactory _contexts;
        private CountingSeedSource _seeds;
        private LevelSessionFactory _sessions;
        private LevelFlowRule _flow;

        [SetUp]
        public void SetUp()
        {
            _storage = new FakeProgressStorage();
            _progress = new ProgressRepository(_storage);
            _catalog = BuildCatalog(new[] { FirstId, MiddleId, LastId });
            _contexts = new FakeLevelContextFactory();
            _seeds = new CountingSeedSource(FirstSeed);
            _sessions = new LevelSessionFactory(_contexts, _seeds);
            _flow = new LevelFlowRule(_progress, _catalog, _sessions, NullLogger.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _flow.Dispose();
            _sessions.Dispose();

            for (int i = 0; i < _assets.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_assets[i]);
            }

            _assets.Clear();
        }

        [Test]
        public void Enter_StartsTheFirstCatalogLevelWhenNothingIsStored()
        {
            _flow.Enter();

            Assert.AreEqual(LevelFlowScreen.Level, _flow.Screen.CurrentValue);
            Assert.AreEqual(FirstId, _flow.CurrentLevelId);
            Assert.AreEqual(1, _contexts.CreateCount);
            Assert.AreEqual(FirstId, _contexts.Last.Level.Id);
            Assert.AreEqual(1, _contexts.Last.Attempt);
            Assert.AreEqual(FirstSeed, _contexts.Last.Seed);
            Assert.AreEqual(FirstId + MoveLimitOffset, _contexts.Last.Rules.MoveLimit, "rules come from the config");
        }

        [Test]
        public void Enter_StartsTheStoredLevel()
        {
            _progress.SetCurrentLevel(MiddleId);

            _flow.Enter();

            Assert.AreEqual(MiddleId, _flow.CurrentLevelId);
            Assert.AreEqual(MiddleId, _contexts.Last.Level.Id);
        }

        [Test]
        public void Enter_FallsBackToTheFirstLevelWhenTheStoredIdIsNotInTheCatalog()
        {
            _progress.SetCurrentLevel(MissingId);

            _flow.Enter();

            Assert.AreEqual(FirstId, _flow.CurrentLevelId);
        }

        [Test]
        public void Enter_OpensEndOfContentInsteadOfALevelWhenTheSetIsCompleted()
        {
            _progress.SetCurrentLevel(LastId);
            _progress.SetAllCompleted(true);

            _flow.Enter();

            Assert.AreEqual(LevelFlowScreen.EndOfContent, _flow.Screen.CurrentValue);
            Assert.AreEqual(0, _contexts.CreateCount, "End of content never loads a level (§8.3)");
            Assert.AreEqual(LastId, _flow.LastLevelId);
        }

        [Test]
        public void Win_WritesTheNextLevelAndContinueLoadsIt()
        {
            _flow.Enter();
            Finish(LevelResult.Won);

            Assert.AreEqual(MiddleId, _progress.CurrentLevel, "progress is written on the win itself (§13)");
            Assert.IsFalse(_progress.AllCompleted);

            _flow.Continue();

            Assert.AreEqual(LevelFlowScreen.Level, _flow.Screen.CurrentValue);
            Assert.AreEqual(MiddleId, _flow.CurrentLevelId);
            Assert.AreEqual(MiddleId, _contexts.Last.Level.Id);
            Assert.AreEqual(1, _contexts.Last.Attempt, "a new level starts at attempt 1");
        }

        [Test]
        public void Loss_KeepsTheLevelAndContinueReplaysItWithANewSeed()
        {
            _flow.Enter();
            int firstSeed = _contexts.Last.Seed;
            Finish(LevelResult.Lost);

            Assert.AreEqual(FirstId, _progress.CurrentLevel, "a loss keeps the same level (§8.3)");
            Assert.IsFalse(_progress.AllCompleted);

            _flow.Continue();

            Assert.AreEqual(FirstId, _flow.CurrentLevelId);
            Assert.AreEqual(2, _contexts.Last.Attempt);
            Assert.AreNotEqual(firstSeed, _contexts.Last.Seed, "a replay gets a new seed (§8.3)");
        }

        [Test]
        public void Deadlock_IsReplayedLikeALoss()
        {
            _flow.Enter();
            Finish(LevelResult.Deadlock);

            Assert.AreEqual(FirstId, _progress.CurrentLevel);

            _flow.Continue();

            Assert.AreEqual(FirstId, _flow.CurrentLevelId);
            Assert.AreEqual(2, _contexts.Last.Attempt);
        }

        [Test]
        public void WinOnTheLastLevel_SetsAllCompletedAndOpensEndOfContent()
        {
            _flow.GoToLevel(LastId);
            Finish(LevelResult.Won);

            Assert.IsTrue(_progress.AllCompleted);
            Assert.AreEqual(LastId, _progress.CurrentLevel, "currentLevel stays on the last id (§13)");

            _flow.Continue();

            Assert.AreEqual(LevelFlowScreen.EndOfContent, _flow.Screen.CurrentValue);
            Assert.AreEqual(1, _contexts.CreateCount, "nothing is loaded past the last level");
            Assert.AreEqual(1, _contexts.DestroyCount, "the finished attempt is torn down");
        }

        [Test]
        public void PlayAgain_ResetsProgressAndStartsTheFirstLevel()
        {
            _progress.SetCurrentLevel(LastId);
            _progress.SetAllCompleted(true);
            _flow.Enter();

            _flow.PlayAgain();

            Assert.AreEqual(FirstId, _progress.CurrentLevel);
            Assert.IsFalse(_progress.AllCompleted);
            Assert.AreEqual(LevelFlowScreen.Level, _flow.Screen.CurrentValue);
            Assert.AreEqual(FirstId, _contexts.Last.Level.Id);
            Assert.AreEqual(1, _contexts.Last.Attempt);
        }

        [Test]
        public void EveryProgressWrite_IsFollowedByASave()
        {
            _flow.Enter();
            Finish(LevelResult.Lost);
            _flow.Continue();
            Finish(LevelResult.Won);
            _flow.Continue();
            Finish(LevelResult.Won);
            _flow.Continue();
            Finish(LevelResult.Won);

            Assert.AreEqual(5, _storage.WriteCount, "four outcomes, the last of them writing both keys");
            Assert.AreEqual(_storage.WriteCount, _storage.SaveCount, "one flush per write (§13)");
            Assert.AreEqual(0, _storage.UnflushedWrites);
            Assert.IsTrue(_progress.AllCompleted);
        }

        [Test]
        public void Restart_ReplaysTheSameLevelWithoutTouchingProgress()
        {
            _flow.Enter();
            int seed = _contexts.Last.Seed;

            _flow.Restart();

            Assert.AreEqual(FirstId, _flow.CurrentLevelId);
            Assert.AreEqual(2, _flow.Attempt);
            Assert.AreNotEqual(seed, _contexts.Last.Seed);
            Assert.AreEqual(0, _storage.WriteCount, "restarting mid-level writes no progress");
        }

        [Test]
        public void GoToLevel_LoadsAnyCatalogLevelAndIgnoresUnknownIds()
        {
            Assert.IsTrue(_flow.CanGoToLevel(MiddleId));
            Assert.IsFalse(_flow.CanGoToLevel(MissingId));

            _flow.GoToLevel(MissingId);

            Assert.AreEqual(0, _contexts.CreateCount, "an unknown id is refused (§12)");

            _flow.GoToLevel(MiddleId);

            Assert.AreEqual(MiddleId, _flow.CurrentLevelId);
            Assert.AreEqual(0, _storage.WriteCount, "the cheat jump leaves stored progress alone");
        }

        [Test]
        public void ReplayWithSeed_ReusesTheGivenSeed()
        {
            _flow.Enter();

            _flow.ReplayWithSeed(ExplicitSeed);

            Assert.AreEqual(ExplicitSeed, _contexts.Last.Seed);
            Assert.AreEqual(ExplicitSeed, _sessions.Seed);
            Assert.AreEqual(2, _flow.Attempt);
            Assert.AreEqual(1, _seeds.Calls, "an explicit seed does not draw from the source (D12)");
        }

        [Test]
        public void StartingTheNextAttempt_CancelsTheTokenBeforeDestroyingTheContext()
        {
            _flow.Enter();
            CancellationToken first = _contexts.Last.Token;
            Assert.IsFalse(first.IsCancellationRequested);

            _flow.Restart();

            Assert.AreEqual(1, _contexts.DestroyCount);
            Assert.IsTrue(_contexts.TokenWasCancelledOnDestroy, "cancel first, then let the views die (I5)");
            Assert.IsTrue(first.IsCancellationRequested);
            Assert.AreNotEqual(first, _contexts.Last.Token, "each attempt owns its token");
        }

        [Test]
        public void DisposingTheFactory_CancelsAndDestroysTheLiveAttempt()
        {
            _flow.Enter();
            CancellationToken token = _contexts.Last.Token;

            _sessions.Dispose();

            Assert.AreEqual(1, _contexts.DestroyCount);
            Assert.IsTrue(token.IsCancellationRequested);
            Assert.IsFalse(_sessions.HasSession);
        }

        /// <summary>
        /// The whole point of §8.3: adding a level past the previous last id turns the same win
        /// into an advance instead of End of content, with no code change.
        /// </summary>
        [Test]
        public void TheLastLevel_ComesFromTheCatalogAndNotFromALiteral()
        {
            Assert.AreEqual(LastId, _flow.LastLevelId);

            LevelCatalog extended = BuildCatalog(new[] { FirstId, MiddleId, LastId, ExtraId });
            var contexts = new FakeLevelContextFactory();
            var sessions = new LevelSessionFactory(contexts, new CountingSeedSource(FirstSeed));
            var flow = new LevelFlowRule(_progress, extended, sessions, NullLogger.Instance);

            try
            {
                Assert.AreEqual(ExtraId, flow.LastLevelId);

                flow.GoToLevel(LastId);
                contexts.Current.NotifyFinished(LevelResult.Won);

                Assert.IsFalse(_progress.AllCompleted, "the old last level is no longer the last");
                Assert.AreEqual(ExtraId, _progress.CurrentLevel);

                flow.Continue();

                Assert.AreEqual(LevelFlowScreen.Level, flow.Screen.CurrentValue);
                Assert.AreEqual(ExtraId, flow.CurrentLevelId);
            }
            finally
            {
                flow.Dispose();
                sessions.Dispose();
            }
        }

        private void Finish(LevelResult result) => _contexts.Current.NotifyFinished(result);

        private LevelCatalog BuildCatalog(int[] ids)
        {
            var configs = new LevelConfig[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                LevelConfig config = ScriptableObject.CreateInstance<LevelConfig>();
                var editable = new SerializedObject(config);
                editable.FindProperty("_id").intValue = ids[i];
                editable.FindProperty("_moveLimit").intValue = ids[i] + MoveLimitOffset;
                editable.ApplyModifiedPropertiesWithoutUndo();

                _assets.Add(config);
                configs[i] = config;
            }

            LevelCatalog catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            catalog.SetLevels(configs);
            _assets.Add(catalog);
            return catalog;
        }

        /// <summary>Stands in for the bootstrap factory that instantiates the LevelContext prefab (A08).</summary>
        private sealed class FakeLevelContextFactory : ILevelContextFactory
        {
            private readonly List<LevelSessionRequest> _requests = new List<LevelSessionRequest>();

            private LevelSessionState _current;
            private CancellationToken _token;

            public int CreateCount => _requests.Count;

            public int DestroyCount { get; private set; }

            public bool TokenWasCancelledOnDestroy { get; private set; }

            public LevelSessionRequest Last => _requests[_requests.Count - 1];

            public LevelSessionState Current => _current;

            public LevelSessionState Create(LevelSessionRequest request)
            {
                Assert.IsTrue(request.IsValid, "the request must carry level data");
                _requests.Add(request);
                _token = request.Token;
                _current = new LevelSessionState(
                    request.Level.Id,
                    request.Level.Tier,
                    request.Seed,
                    request.Rules.MoveLimit,
                    new GoalTracker(request.Level.Goals),
                    request.Rules.HintDelaySeconds);

                return _current;
            }

            public void Destroy()
            {
                DestroyCount++;
                TokenWasCancelledOnDestroy = _token.IsCancellationRequested;

                if (_current == null)
                {
                    return;
                }

                _current.Dispose();
                _current = null;
            }
        }

        /// <summary>
        /// Attempt seeds without UnityEngine.Random or the clock: the source sits outside the
        /// gameplay RNG stream, and the attempt it seeds stays fully deterministic (D12).
        /// </summary>
        private sealed class CountingSeedSource : ILevelSeedSource
        {
            private int _next;

            public CountingSeedSource(int first)
            {
                _next = first;
            }

            public int Calls { get; private set; }

            public int NextSeed()
            {
                Calls++;
                return _next++;
            }
        }
    }
}
