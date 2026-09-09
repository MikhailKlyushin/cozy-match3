using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Hud
{
    /// <summary>
    /// End of content (§11.1, §8.3): shows the last level number and restarts the set from level 1.
    /// The cheat button stays reachable behind it, so this popup never covers the whole screen.
    /// </summary>
    public sealed class EndOfContentPopupView : PopupView
    {
        [SerializeField] private Text _lastLevelLabel;

        public override PopupKind Kind => PopupKind.EndOfContent;

        protected override string Title => HudStrings.EndOfContentTitle;

        protected override string PrimaryButtonText => HudStrings.EndOfContentButton;

        public override void Present(PopupContent content, GoalIconResolver icons)
        {
            base.Present(content, icons);

            if (_lastLevelLabel != null)
            {
                _lastLevelLabel.raycastTarget = false;
                _lastLevelLabel.text = string.Format(
                    CultureInfo.InvariantCulture,
                    HudStrings.LastLevelFormat,
                    content.LastLevelId);
            }
        }
    }
}
