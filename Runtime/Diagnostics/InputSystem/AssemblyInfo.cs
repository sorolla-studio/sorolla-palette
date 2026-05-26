using UnityEngine.Scripting;

// Forces IL2CPP to keep this assembly even though nothing references it directly — the backend
// self-registers via [RuntimeInitializeOnLoadMethod]. Without this the linker strips the assembly
// on device builds, so the diagnostics console never receives touch input (works in the Mono
// editor, fails on device). Mirrors the adapter assemblies' stripping protection.
[assembly: AlwaysLinkAssembly]
