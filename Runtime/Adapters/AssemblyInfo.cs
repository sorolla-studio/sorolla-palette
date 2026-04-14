using System.Runtime.CompilerServices;

// Allow implementation assemblies to access internal interfaces and adapter classes
[assembly: InternalsVisibleTo("Sorolla.Runtime")]
[assembly: InternalsVisibleTo("Sorolla.Adapters.MAX")]
[assembly: InternalsVisibleTo("Sorolla.Adapters.Adjust")]
[assembly: InternalsVisibleTo("Sorolla.Adapters.Firebase")]
