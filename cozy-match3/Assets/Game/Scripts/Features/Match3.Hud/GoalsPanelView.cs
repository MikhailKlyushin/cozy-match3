using System;
using Match3.Goals;
using UnityEngine;

namespace Match3.Hud
{
    /// <summary>
    /// Goal rows in config order, never sorted: that order is also the booster targeting priority
    /// (§8.2), so reordering the display would silently change aiming. Rows are pre-placed in the
    /// prefab (§8.2 caps a level at three goals), so the panel never instantiates anything.
    /// </summary>
    public sealed class GoalsPanelView : MonoBehaviour
    {
        [SerializeField] private GoalRowView[] _rows = Array.Empty<GoalRowView>();

        private int _visibleRows;

        public int RowCapacity => _rows != null ? _rows.Length : 0;

        public int VisibleRowCount => _visibleRows;

        /// <summary>
        /// Initial render from goal state. Legal outside playback only: the level build and the
        /// win/lose popup (rule V1); during a turn the values come from the transcript.
        /// </summary>
        public void Render(IGoalTracker goals, GoalIconResolver icons)
        {
            if (goals == null)
            {
                throw new ArgumentNullException(nameof(goals));
            }

            if (icons == null)
            {
                throw new ArgumentNullException(nameof(icons));
            }

            _visibleRows = Mathf.Min(goals.Count, RowCapacity);

            for (int i = 0; i < RowCapacity; i++)
            {
                GoalRowView row = _rows[i];
                if (row == null)
                {
                    continue;
                }

                bool used = i < _visibleRows;
                if (row.gameObject.activeSelf != used)
                {
                    row.gameObject.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                GoalDefinition goal = goals.GetDefinition(i);
                row.Show(icons.Resolve(goal), goals.GetState(i).Value, goal.Target);
            }
        }

        public void SetValue(int goalIndex, int value)
        {
            GoalRowView row = GetRow(goalIndex);
            if (row != null)
            {
                row.SetValue(value);
            }
        }

        public void PlayTick(int goalIndex, float duration)
        {
            GoalRowView row = GetRow(goalIndex);
            if (row != null)
            {
                row.PlayTick(duration);
            }
        }

        public void KillTweens()
        {
            for (int i = 0; i < RowCapacity; i++)
            {
                GoalRowView row = _rows[i];
                if (row != null)
                {
                    row.KillTweens();
                }
            }
        }

        private GoalRowView GetRow(int goalIndex)
        {
            return (uint)goalIndex < (uint)_visibleRows ? _rows[goalIndex] : null;
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
