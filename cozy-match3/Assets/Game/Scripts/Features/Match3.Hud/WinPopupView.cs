namespace Match3.Hud
{
    /// <summary>Win popup (§11.1): goals closed on POST-TURN, next level behind the button.</summary>
    public sealed class WinPopupView : LevelOutcomePopupView
    {
        public override PopupKind Kind => PopupKind.Win;

        protected override string Title => HudStrings.WinTitle;

        protected override string PrimaryButtonText => HudStrings.WinButton;
    }
}
