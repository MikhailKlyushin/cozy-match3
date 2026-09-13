using System;
using System.Threading;
using Match3.Levels;
using Match3.Resolve;

namespace Match3.Progression
{
    /// <summary>
    /// Everything one level attempt needs (A08). Built by <see cref="LevelSessionFactory"/> and
    /// handed to the LevelContext installer, which binds its parts into the attempt sub-container.
    /// </summary>
    public readonly struct LevelSessionRequest
    {
        public LevelSessionRequest(
            LevelData level,
            LevelRules rules,
            int seed,
            int attempt,
            CancellationToken token)
        {
            Level = level;
            Rules = rules;
            Seed = seed;
            Attempt = attempt;
            Token = token;
        }

        public LevelData Level { get; }

        public LevelRules Rules { get; }

        /// <summary>Random per attempt, then fixed for its whole duration (D12).</summary>
        public int Seed { get; }

        /// <summary>1 on a fresh level, +1 on every replay. Reported as <c>level_start.attempt</c> (§14).</summary>
        public int Attempt { get; }

        /// <summary>Cancelled before teardown, so nothing keeps writing into a dead board (I5).</summary>
        public CancellationToken Token { get; }

        public bool IsValid => Level != null;

        public static LevelSessionRequest For(LevelData level, int seed, int attempt, CancellationToken token)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var rules = new LevelRules(level.Id, level.ColorCount, level.MoveLimit, level.HintDelaySeconds);
            return new LevelSessionRequest(level, rules, seed, attempt, token);
        }
    }
}
