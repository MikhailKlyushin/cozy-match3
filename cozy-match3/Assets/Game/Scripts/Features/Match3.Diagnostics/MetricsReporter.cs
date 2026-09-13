using System;
using System.Text;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;

namespace Match3.Diagnostics
{
    /// <summary>
    /// The eight events of GDD §14. Without them balance is guesswork, so the field schema matches
    /// the production one. wave_cap_hit and deadlock are reported as errors: both are always bugs.
    /// </summary>
    public sealed class MetricsReporter
    {
        private readonly IMatch3Logger _logger;
        private readonly StringBuilder _builder = new StringBuilder(256);

        public MetricsReporter(IMatch3Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LevelStart(int levelId, DifficultyTier tier, int attempt, int seed)
        {
            Begin("level_start");
            Field("id", levelId);
            Field("tier", tier.ToString());
            Field("attempt", attempt);
            Field("seed", seed);
            Flush();
        }

        public void LevelEnd(
            int levelId,
            LevelResultKind result,
            int movesUsed,
            int movesLeft,
            IGoalTracker goals,
            int seed)
        {
            Begin("level_end");
            Field("id", levelId);
            Field("result", ResultName(result));
            Field("movesUsed", movesUsed);
            Field("movesLeft", movesLeft);
            AppendGoalProgress(goals);
            Field("seed", seed);

            if (result == LevelResultKind.Deadlock)
            {
                _logger.Error(_builder.ToString());
                _builder.Clear();
                return;
            }

            Flush();
        }

        public void ShuffleTriggered(int levelId, int movesUsed, int attempt)
        {
            Begin("shuffle_triggered");
            Field("id", levelId);
            Field("movesUsed", movesUsed);
            Field("attempt", attempt);
            Flush();
        }

        public void HintShown(int levelId, int movesLeft, int rulePriority)
        {
            Begin("hint_shown");
            Field("id", levelId);
            Field("movesLeft", movesLeft);
            Field("rule", rulePriority);
            Flush();
        }

        public void HintFollowed(int levelId, bool followed)
        {
            Begin("hint_followed");
            Field("id", levelId);
            Field("followed", followed ? "true" : "false");
            Flush();
        }

        /// <summary>
        /// Derives booster_fired, cascade_depth and wave_cap_hit from one turn. Runs once per turn,
        /// never per cell, so building a string here is not a hot-path allocation (§14).
        /// </summary>
        public void TurnCompleted(int levelId, TurnTranscript transcript)
        {
            if (transcript == null)
            {
                throw new ArgumentNullException(nameof(transcript));
            }

            for (int i = 0; i < transcript.EventCount; i++)
            {
                ref readonly TurnEvent e = ref transcript.GetEvent(i);
                switch (e.Kind)
                {
                    case TurnEventKind.BoosterActivated:
                        Begin("booster_fired");
                        Field("id", levelId);
                        Field("type", e.Booster.ToString());
                        Field("source", e.ActivationSource.ToString());
                        Field("wave", e.Wave);
                        Flush();
                        break;

                    case TurnEventKind.ComboActivated:
                        Begin("booster_fired");
                        Field("id", levelId);
                        Field("type", e.Booster.ToString());
                        Field("source", BoosterActivationSource.Combo.ToString());
                        Field("comboWith", e.ComboBoosterB.ToString());
                        Flush();
                        break;

                    case TurnEventKind.CapHit:
                        Begin("wave_cap_hit");
                        Field("id", levelId);
                        Field("cap", e.Cap.ToString());
                        Field("step", e.Step);
                        _logger.Error(_builder.ToString());
                        _builder.Clear();
                        break;
                }
            }

            Begin("cascade_depth");
            Field("id", levelId);
            Field("maxDepth", transcript.MaxDepth);
            Flush();
        }

        private static string ResultName(LevelResultKind result)
        {
            switch (result)
            {
                case LevelResultKind.Win:
                    return "win";
                case LevelResultKind.Lose:
                    return "lose";
                default:
                    return "deadlock";
            }
        }

        private void AppendGoalProgress(IGoalTracker goals)
        {
            if (goals == null)
            {
                return;
            }

            for (int i = 0; i < goals.Count; i++)
            {
                GoalState state = goals.GetState(i);
                GoalDefinition definition = goals.GetDefinition(i);
                _builder.Append(" goal").Append(i).Append('=')
                    .Append(state.Value).Append('/').Append(definition.Target);
            }
        }

        private void Begin(string eventName)
        {
            _builder.Clear();
            _builder.Append(eventName);
        }

        private void Field(string name, int value)
            => _builder.Append(' ').Append(name).Append('=').Append(value);

        private void Field(string name, string value)
            => _builder.Append(' ').Append(name).Append('=').Append(value);

        private void Flush()
        {
            _logger.Info(_builder.ToString());
            _builder.Clear();
        }
    }

    /// <summary>Outcome reported to metrics; deadlock is separate because it is always a bug (§14).</summary>
    public enum LevelResultKind : byte
    {
        Win = 0,
        Lose = 1,
        Deadlock = 2
    }
}
