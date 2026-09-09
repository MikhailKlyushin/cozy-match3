using System.Runtime.CompilerServices;

// The level asset generator fills catalogs and configs that are read-only at runtime.
[assembly: InternalsVisibleTo("Match3.EditorTools")]
[assembly: InternalsVisibleTo("Match3.Levels.Authoring.Editor")]
[assembly: InternalsVisibleTo("Match3.Tests.EditMode")]
