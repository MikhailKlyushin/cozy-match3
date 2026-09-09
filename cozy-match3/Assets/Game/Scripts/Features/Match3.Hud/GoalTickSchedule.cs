using System;
using Match3.Content;

namespace Match3.Hud
{
    /// <summary>
    /// Tick plan for one goal counter: 0.15 s per unit, compressed so a large jump still fits the
    /// 1.0 s cap (GDD §11.3). Pure arithmetic, so it is covered without a scene.
    /// </summary>
    public readonly struct GoalTickSchedule
    {
        public readonly int FromValue;
        public readonly int ToValue;

        /// <summary>Seconds between two consecutive ticks.</summary>
        public readonly float Step;

        public GoalTickSchedule(int fromValue, int toValue, float step)
        {
            if (toValue < fromValue)
            {
                throw new ArgumentOutOfRangeException(nameof(toValue), "A goal counter never ticks backwards.");
            }

            if (step < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(step), "Tick step cannot be negative.");
            }

            FromValue = fromValue;
            ToValue = toValue;
            Step = step;
        }

        public int Units => ToValue - FromValue;

        public float Total => Step * Units;

        /// <summary>Value shown by tick <paramref name="tickIndex"/>, counted from 0.</summary>
        public int ValueAt(int tickIndex)
        {
            if ((uint)tickIndex >= (uint)Units)
            {
                throw new ArgumentOutOfRangeException(nameof(tickIndex), "Tick index outside the schedule.");
            }

            return FromValue + tickIndex + 1;
        }

        public static GoalTickSchedule Between(int fromValue, int toValue, TimingProfile timings)
        {
            if (timings == null)
            {
                throw new ArgumentNullException(nameof(timings));
            }

            int units = toValue - fromValue;
            if (units <= 0)
            {
                return new GoalTickSchedule(fromValue, fromValue, 0f);
            }

            return new GoalTickSchedule(fromValue, toValue, timings.GoalTickStep(units));
        }
    }
}
