# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## SDK Overview

**Sorolla SDK** is a plug-and-play mobile publisher SDK for Unity. It wraps GameAnalytics, Facebook, AppLovin MAX, Adjust, and Firebase behind a unified `Palette` API.

This is a **standalone Git repository** (not part of the parent Unity project):
- Repo: https://github.com/sorolla-studio/sorolla-palette
- Commits: `git add . && git commit -m "message"` (run from this directory)

## Editor Commands

| Menu Path | Purpose |
|-----------|---------|
| Palette > Configuration | Mode setup, SDK installation |
| Palette > Tools > Validate Build | Pre-build validation (also runs automatically) |
| Palette > Tools > Sanitize Android Manifest | Remove orphaned SDK entries |
| Palette > Tools > Check MAX Settings | Disable Quality Service |

## Validation & Build

`BuildValidator.cs` runs automatically before builds via `IPreprocessBuildWithReport`:
- Checks SDK version mismatches
- Validates mode consistency
- Ensures scoped registries configured
- Sanitizes AndroidManifest.xml (auto-fix for orphaned entries)
- Validates MAX SDK key in AppLovinSettings

## Architecture: Stub + Implementation Pattern

Optional SDK adapters use separate assemblies to avoid "assembly not found" errors:

```
Adapters/
├── Sorolla.Adapters.asmdef       # Stubs (no external refs, always compiles)
├── MaxAdapter.cs                  # Stub: static class with IMaxAdapter field
├── MAX/
│   ├── Sorolla.Adapters.MAX.asmdef   # defineConstraints + versionDefines
│   ├── AssemblyInfo.cs               # [AlwaysLinkAssembly]
│   └── MaxAdapterImpl.cs             # [RuntimeInitializeOnLoadMethod] registers impl
```

**Key insight**: `versionDefines` are **per-assembly only** (not project-wide). Each implementation asmdef must define its own symbols.

### Adding a New SDK Adapter

1. Add to `SdkRegistry.cs` (ID, package name, version, scope, requirement)
2. Create stub in `Adapters/XxxAdapter.cs` with `IXxxAdapter` interface
3. Create impl folder `Adapters/Xxx/` with:
   - `Sorolla.Adapters.Xxx.asmdef` (with defineConstraints + versionDefines)
   - `AssemblyInfo.cs` with `[assembly: AlwaysLinkAssembly]`
   - `XxxAdapterImpl.cs` with `[Preserve]` and `[RuntimeInitializeOnLoadMethod]`
4. Add initialization call in `Palette.Initialize()`
5. Add UI section in `SorollaWindow.cs`

### IL2CPP Stripping Protection

Three layers required for `[RuntimeInitializeOnLoadMethod]` to work in IL2CPP builds:
- `[assembly: AlwaysLinkAssembly]` - Forces linker to process assembly
- `[Preserve]` on class and Register method - Marks as roots
- `link.xml` in Assets/ - Fallback (NOT auto-included from packages)

## Critical Learnings (from devlog.md)

**Unity asmdef**:
- `versionDefines` + `defineConstraints` BOTH needed for optional assemblies
- `defineConstraints` prevents compilation when symbol not set

**MAX SDK**:
- SDK key is in AppLovinSettings (Integration Manager), NOT SorollaConfig
- `MaxSdk.SetSdkKey()` is deprecated - SDK reads from settings automatically
- Quality Service causes 401 build failures - auto-disabled by sanitizer

**EDM4U + Unity 6**:
- Bundles Gradle 5.1.1, incompatible with Java 17+ (Unity 6 default)
- First resolution may fail, works after mode selection triggers re-resolve

## Key Files

| File | Purpose |
|------|---------|
| `Runtime/Palette.cs` | Main public API (static class) |
| `Runtime/SorollaBootstrapper.cs` | Auto-init via [RuntimeInitializeOnLoadMethod] |
| `Runtime/SorollaConfig.cs` | ScriptableObject in Resources/ |
| `Editor/Sdk/SdkRegistry.cs` | Single source of truth for SDK metadata |
| `Editor/BuildValidator.cs` | Pre-build validation |
| `Editor/SorollaWindow.cs` | Configuration UI |
| `DEVLOG.md` | Validated learnings - check first |

## Namespaces

- `Sorolla.Palette` - Public API (`Palette` static class)
- `Sorolla.Palette.Adapters` - SDK wrappers (stubs + impls)
- `Sorolla.Palette.ATT` - iOS privacy (ContextScreenView, FakeATTDialog)
- `Sorolla.Palette.Editor` - Editor tools

## Mode System

| Mode | Required SDKs | Optional | Use Case |
|------|---------------|----------|----------|
| Prototype | GameAnalytics, Facebook | MAX | CPI tests, soft launch |
| Full | GameAnalytics, MAX, Adjust | Firebase | Production |

Mode stored in EditorPrefs, runtime config in `Resources/SorollaConfig.asset`.
