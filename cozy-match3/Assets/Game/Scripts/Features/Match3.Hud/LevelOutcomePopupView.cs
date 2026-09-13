using UnityEngine;

namespace Match3.Hud
{
    /// <summary>Win and lose share one body: title, goal progress, one action button (§11.1).</summary>
    public abstract class LevelOutcomePopupView : PopupView
    {
        [SerializeField] private GoalsPanelView _goalsPanel;

        public override void Present(PopupContent content, GoalIconResolver icons)
        {
            base.Present(content, icons);

            if (_goalsPanel != null && content.Goals != null)
            {
                _goalsPanel.Render(content.Goals, icons);
            }
        }
    }
}
