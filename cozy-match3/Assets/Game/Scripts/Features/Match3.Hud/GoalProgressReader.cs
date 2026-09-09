using System;
using Match3.Resolve;

namespace Match3.Hud
{
    /// <summary>
    /// Per-goal progress of one turn, taken from the transcript's GoalProgress events (rule T4).
    /// The HUD ticks from these deltas instead of reading goal state, because the model is already
    /// a turn ahead of the picture (rule V1).
    /// </summary>
    public sealed class GoalProgressReader
    {
        private readonly int[] _from;
        private readonly int[] _to;
        private readonly int[] _delta;
        private readonly bool[] _reported;

        public GoalProgressReader(int goalCount)
        {
            if (goalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(goalCount), "Goal count cannot be negative.");
            }

            _from = new int[goalCount];
            _to = new int[goalCount];
            _delta = new int[goalCount];
            _reported = new bool[goalCount];
        }

        public int GoalCount => _from.Length;

        /// <summary>Events naming a goal index this level does not have. Always a transcript bug.</summary>
        public int SkippedEventCount { get; private set; }

        public void Read(TurnTranscript transcript)
        {
            if (transcript == null)
            {
                throw new ArgumentNullException(nameof(transcript));
            }

            Clear();

            for (int i = 0; i < transcript.EventCount; i++)
            {
                ref readonly TurnEvent e = ref transcript.GetEvent(i);
                if (e.Kind != TurnEventKind.GoalProgress)
                {
                    continue;
                }

                int index = e.InstanceId;
                if ((uint)index >= (uint)_from.Length)
                {
                    SkippedEventCount++;
                    continue;
                }

                if (!_reported[index])
                {
                    _reported[index] = true;
                    _from[index] = e.Value - e.Amount;
                }

                _to[index] = e.Value;
                _delta[index] += e.Amount;
            }
        }

        public void Clear()
        {
            Array.Clear(_from, 0, _from.Length);
            Array.Clear(_to, 0, _to.Length);
            Array.Clear(_delta, 0, _delta.Length);
            Array.Clear(_reported, 0, _reported.Length);
            SkippedEventCount = 0;
        }

        /// <summary>True when the goal gained at least one unit, so it has something to tick.</summary>
        public bool HasProgress(int goalIndex)
        {
            ValidateIndex(goalIndex);
            return _delta[goalIndex] > 0;
        }

        /// <summary>Value the counter held before this turn. Valid only when <see cref="HasProgress"/>.</summary>
        public int GetFrom(int goalIndex)
        {
            ValidateIndex(goalIndex);
            return _from[goalIndex];
        }

        /// <summary>Last clamped total reported by the turn (E18). Valid only when <see cref="HasProgress"/>.</summary>
        public int GetValue(int goalIndex)
        {
            ValidateIndex(goalIndex);
            return _to[goalIndex];
        }

        public int GetDelta(int goalIndex)
        {
            ValidateIndex(goalIndex);
            return _delta[goalIndex];
        }

        private void ValidateIndex(int goalIndex)
        {
            if ((uint)goalIndex >= (uint)_from.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(goalIndex), "Goal index out of range.");
            }
        }
    }
}
