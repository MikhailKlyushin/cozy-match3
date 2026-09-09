using System.Collections.Generic;
using System.Text;

namespace Match3.Levels
{
    /// <summary>
    /// Result of <see cref="LevelValidator"/>. In the editor errors fail the load, in a build
    /// they are logged (GDD §3.4).
    /// </summary>
    public sealed class ValidationReport
    {
        private readonly ValidationIssue[] _issues;

        internal ValidationReport(IReadOnlyList<ValidationIssue> issues)
        {
            _issues = new ValidationIssue[issues.Count];
            for (int i = 0; i < _issues.Length; i++)
            {
                _issues[i] = issues[i];
                if (_issues[i].Severity == ValidationSeverity.Error)
                {
                    HasErrors = true;
                }
            }
        }

        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public bool HasErrors { get; }

        /// <summary>Allocates: for logs and test failure messages, never the hot path (§14).</summary>
        public string Describe()
        {
            if (_issues.Length == 0)
            {
                return "Level is valid.";
            }

            var text = new StringBuilder();
            for (int i = 0; i < _issues.Length; i++)
            {
                if (i > 0)
                {
                    text.Append('\n');
                }

                text.Append(_issues[i].Severity == ValidationSeverity.Error ? "ERROR: " : "WARNING: ");
                text.Append(_issues[i].Message);
            }

            return text.ToString();
        }
    }
}
