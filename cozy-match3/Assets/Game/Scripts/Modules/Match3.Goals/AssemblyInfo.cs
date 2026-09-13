using System.Runtime.CompilerServices;

// GoalState and GoalDelta are produced by the tracker only; tests need the constructors to build
// IGoalTracker fakes.
[assembly: InternalsVisibleTo("Match3.Tests.EditMode")]
[assembly: InternalsVisibleTo("Match3.Tests.PlayMode")]
