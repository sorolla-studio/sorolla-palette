using System.Runtime.CompilerServices;

// Sorolla.Health is a leaf contract assembly: all types are internal. Its consumers are declared
// explicitly here (no accidental studio API). Runtime + Editor gain their asmdef references in Cycle 4
// when Vitals / the greenlight adapter actually consume the model; the friend grants are declared now so
// that wiring needs no AssemblyInfo change.
[assembly: InternalsVisibleTo("Sorolla.Runtime")]
[assembly: InternalsVisibleTo("Sorolla.Editor")]
[assembly: InternalsVisibleTo("Sorolla.Editor.Tests")]
