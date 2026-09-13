namespace Match3.Levels
{
    /// <summary>One finding of <see cref="LevelValidator"/>.</summary>
    public readonly struct ValidationIssue
    {
        public readonly ValidationSeverity Severity;
        public readonly string Message;

        internal ValidationIssue(ValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }
}
