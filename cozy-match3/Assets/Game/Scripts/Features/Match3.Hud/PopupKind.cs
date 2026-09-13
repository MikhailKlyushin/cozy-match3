namespace Match3.Hud
{
    /// <summary>The three popups of §11.1.</summary>
    public enum PopupKind : byte
    {
        Win = 0,

        Lose = 1,

        /// <summary>Shown instead of auto-loading after the last level of the set (§8.3).</summary>
        EndOfContent = 2
    }
}
