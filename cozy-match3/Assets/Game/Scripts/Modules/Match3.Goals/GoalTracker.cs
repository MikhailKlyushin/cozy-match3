using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Core;

namespace Match3.Goals
{
    /// <summary>
    /// Goal progress of one level attempt (GDD §8.1-§8.3). Goals are kept in config order because
    /// that order is also the booster targeting priority (§8.2): sorting them would silently
    /// change aiming.
    /// </summary>
    public sealed class GoalTracker : IGoalTracker
    {
        private readonly GoalDefinition[] _definitions;
        private readonly int[] _values;

        private const int NoGoal = -1;

        public GoalTracker(IReadOnlyList<GoalDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _definitions = new GoalDefinition[definitions.Count];
            _values = new int[definitions.Count];

            for (int i = 0; i < definitions.Count; i++)
            {
                GoalDefinition definition = definitions[i];
                if (definition.Target <= 0)
                {
                    throw new ArgumentException("Goal target must be positive.", nameof(definitions));
                }

                _definitions[i] = definition;
            }
        }

        public int Count => _definitions.Length;

        public bool AllClosed => FirstUnclosedIndex == NoGoal;

        public int FirstUnclosedIndex
        {
            get
            {
                for (int i = 0; i < _definitions.Length; i++)
                {
                    if (_values[i] < _definitions[i].Target)
                    {
                        return i;
                    }
                }

                return NoGoal;
            }
        }

        public GoalDefinition GetDefinition(int index)
        {
            ValidateIndex(index);
            return _definitions[index];
        }

        public GoalState GetState(int index)
        {
            ValidateIndex(index);
            int value = _values[index];
            return new GoalState(value, value >= _definitions[index].Target);
        }

        public void CreditChip(ChipColor color, PooledList<GoalDelta> deltas)
        {
            if (color == ChipColor.None)
            {
                return;
            }

            for (int i = 0; i < _definitions.Length; i++)
            {
                GoalDefinition goal = _definitions[i];
                if (goal.Type == GoalType.CollectColor && goal.Color == color)
                {
                    Credit(i, 1, deltas);
                }
            }
        }

        public void CreditElement(ElementDefinition definition, PooledList<GoalDelta> deltas)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.GoalRole == GoalRole.NotCountable)
            {
                return;
            }

            for (int i = 0; i < _definitions.Length; i++)
            {
                GoalDefinition goal = _definitions[i];
                if (goal.Type == GoalType.DestroyElement)
                {
                    if (string.Equals(goal.Token, definition.Token, StringComparison.Ordinal))
                    {
                        Credit(i, 1, deltas);
                    }
                }
                else if (goal.Type == GoalType.DestroyAnyColoredBox && definition.IsColoredBox)
                {
                    Credit(i, 1, deltas);
                }
            }
        }

        public void CreditBoosterActivation(BoosterType booster, PooledList<GoalDelta> deltas)
        {
            if (booster == BoosterType.None)
            {
                return;
            }

            for (int i = 0; i < _definitions.Length; i++)
            {
                GoalDefinition goal = _definitions[i];
                if (goal.Type == GoalType.ActivateBooster && IsSameBooster(goal.Booster, booster))
                {
                    Credit(i, 1, deltas);
                }
            }
        }

        public void CloseAll(PooledList<GoalDelta> deltas)
        {
            for (int i = 0; i < _definitions.Length; i++)
            {
                Credit(i, _definitions[i].Target, deltas);
            }
        }

        /// <summary>A rocket goal counts both orientations: the player never picks one (§8.1, level 11).</summary>
        private static bool IsSameBooster(BoosterType goal, BoosterType activated)
            => goal == activated || (BoosterTypes.IsRocket(goal) && BoosterTypes.IsRocket(activated));

        private void Credit(int index, int amount, PooledList<GoalDelta> deltas)
        {
            int target = _definitions[index].Target;
            int value = _values[index];
            int newValue = value + amount;
            if (newValue > target)
            {
                newValue = target;
            }

            _values[index] = newValue;

            // E18: the reported delta is the part that actually counted, so it is 0 once closed.
            deltas.Add(new GoalDelta(index, newValue - value, newValue));
        }

        private void ValidateIndex(int index)
        {
            if ((uint)index >= (uint)_definitions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Goal index out of range.");
            }
        }
    }
}
