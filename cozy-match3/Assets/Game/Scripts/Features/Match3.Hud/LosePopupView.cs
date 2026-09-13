namespace Match3.Hud
{
    /// <summary>Lose popup (§11.1): the same level is replayed with a new seed (§8.3).</summary>
    public sealed class LosePopupView : LevelOutcomePopupView
    {
        public override PopupKind Kind => PopupKind.Lose;

        protected override string Title => HudStrings.LoseTitle;

        protected override string PrimaryButtonText => HudStrings.LoseButton;
    }
}
