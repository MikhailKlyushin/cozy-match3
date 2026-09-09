namespace Match3.Cheats
{
    /// <summary>
    /// The two hint controls §12 needs. Signature-compatible with <c>HintPresenter</c>, which is a
    /// class in Match3.Gameplay: the cheat assembly may use Gameplay's interfaces only (§3.1), so
    /// the composition root (T23) forwards these two calls to the live presenter.
    /// </summary>
    public interface ICheatHintControl
    {
        /// <summary>Parks the idle timer while the panel is open or hints are switched off (§5.5).</summary>
        void SetSuspended(bool suspended);

        /// <summary>Shows the suggestion now, without waiting out the idle delay.</summary>
        void ShowNow();
    }
}
