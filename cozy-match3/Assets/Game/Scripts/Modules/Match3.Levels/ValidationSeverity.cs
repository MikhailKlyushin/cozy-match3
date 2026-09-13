namespace Match3.Levels
{
    /// <summary>
    /// GDD §3.4: a broken level fails loudly on load. A warning is an authoring smell that still
    /// loads, such as the wall-width rule of §10.3.
    /// </summary>
    public enum ValidationSeverity : byte
    {
        Warning = 0,
        Error = 1
    }
}
