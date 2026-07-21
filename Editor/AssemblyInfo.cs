using System.Runtime.CompilerServices;

// Allow test assembly to access internal methods
[assembly: InternalsVisibleTo("Sorolla.Editor.Tests")]

// Allow the testbed-local internal harness (Assets/SorollaInternal/Editor/, gitignored,
// non-shipping) to reuse SorollaWindow's internal row-building/evaluator surface directly instead
// of duplicating it (editor-window-simplification-2026-07-21 ruling 4).
[assembly: InternalsVisibleTo("SorollaInternal.Editor")]
