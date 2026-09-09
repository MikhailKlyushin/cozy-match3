using System.Runtime.CompilerServices;

// Board mutation is internal: only the resolve pipeline, the level builder and the match
// validator (which probes a swap and reverts it) may change the board (A10).
[assembly: InternalsVisibleTo("Match3.Matching")]
[assembly: InternalsVisibleTo("Match3.Resolve")]
[assembly: InternalsVisibleTo("Match3.Levels")]
[assembly: InternalsVisibleTo("Match3.Tests.EditMode")]
[assembly: InternalsVisibleTo("Match3.Tests.PlayMode")]
