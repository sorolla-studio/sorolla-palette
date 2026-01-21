# Session: sorolla-sdk
Updated: 2026-01-20T17:50:00Z

## Goal
Understanding the Sorolla SDK codebase structure, architecture patterns, and key implementation details for brownfield development.

## Constraints
- **Tech Stack**: Unity 6 LTS, C#, UPM Package
- **Framework**: Unity Package Manager (UPM), Assembly Definitions (asmdef)
- **Build**: Unity Editor workflows, IL2CPP stripping
- **Test**: Manual testing via DebugUI sample, no unit tests (Unity mobile best practice)
- **Patterns**:
  - Stub + Implementation pattern for optional SDK dependencies
  - Per-assembly versionDefines + defineConstraints
  - RuntimeInitializeOnLoadMethod with IL2CPP stripping protection
  - Single source of truth pattern (SdkRegistry.cs)

## Key Decisions

### Architecture Pattern: Stub + Implementation
- **Decision**: Use separate assemblies for optional SDK adapters (MAX, Adjust, Firebase)
- **Rationale**: Unity resolves assembly references before evaluating preprocessor directives. Without separate assemblies, compilation fails when optional SDKs are missing.
- **Implementation**:
  - Stub in `Adapters/` (e.g., `MaxAdapter.cs`) - static class with `IMaxAdapter` field
  - Implementation in `Adapters/MAX/` (e.g., `MaxAdapterImpl.cs`) - registers via RuntimeInitializeOnLoadMethod
  - Separate asmdef with `defineConstraints: ["APPLOVIN_MAX_INSTALLED"]`

### IL2CPP Stripping Protection (Critical)
- **Decision**: Three-layer protection for RuntimeInitializeOnLoadMethod in packages
- **Rationale**: link.xml in UPM packages is NOT auto-included by Unity
- **Implementation**:
  1. `[assembly: AlwaysLinkAssembly]` in AssemblyInfo.cs - forces linker to process assembly
  2. `[Preserve]` on class and methods - marks as roots
  3. `link.xml` in Assets/ (fallback for games using the SDK)

### Version Defines Scope
- **Decision**: Each implementation asmdef defines its own versionDefines
- **Rationale**: versionDefines are per-assembly only, NOT project-wide
- **Example**: `Sorolla.Adapters.MAX.asmdef` defines `APPLOVIN_MAX_INSTALLED`, separate from stub assembly

### Single Source of Truth: SdkRegistry.cs
- **Decision**: All SDK metadata (package IDs, versions, scopes, requirements) in one file
- **Rationale**: Prevents version drift, simplifies maintenance
- **Used By**: SdkInstaller, SdkDetector, BuildValidator, SorollaWindow

## State
- Now: [→] Codebase analyzed, continuity ledger created
- Next: Ready for feature development, bug fixes, or refactoring tasks

## Working Set

### Key Files - Runtime
- `Runtime/Palette.cs` - Main public API (static class)
- `Runtime/SorollaBootstrapper.cs` - Auto-init via RuntimeInitializeOnLoadMethod
- `Runtime/SorollaConfig.cs` - ScriptableObject configuration in Resources/
- `Runtime/GameAnalyticsAdapter.cs` - Core analytics (always required)
- `Runtime/Adapters/MaxAdapter.cs` - MAX stub
- `Runtime/Adapters/MAX/MaxAdapterImpl.cs` - MAX implementation
- `Runtime/Adapters/AdjustAdapter.cs` - Adjust stub
- `Runtime/Adapters/Adjust/AdjustAdapterImpl.cs` - Adjust implementation
- `Runtime/Adapters/FirebaseAdapter.cs` - Firebase Analytics stub
- `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs` - Firebase implementation
- `Runtime/ATT/` - iOS App Tracking Transparency flow

### Key Files - Editor
- `Editor/Sdk/SdkRegistry.cs` - Single source of truth for SDK metadata
- `Editor/Sdk/SdkInstaller.cs` - Package installation via manifest.json
- `Editor/Sdk/SdkDetector.cs` - Detect installed SDKs
- `Editor/BuildValidator.cs` - Pre-build validation (IPreprocessBuildWithReport)
- `Editor/SorollaWindow.cs` - Configuration UI (Palette menu)
- `Editor/AndroidManifestSanitizer.cs` - Remove orphaned SDK entries
- `Editor/MaxSettingsSanitizer.cs` - Disable Quality Service (causes 401 errors)

### Assembly Definitions
- `Runtime/Sorolla.Runtime.asmdef` - Core SDK assembly
- `Runtime/Adapters/Sorolla.Adapters.asmdef` - Stubs (no external refs)
- `Runtime/Adapters/MAX/Sorolla.Adapters.MAX.asmdef` - MAX impl (defineConstraints)
- `Runtime/Adapters/Adjust/Sorolla.Adapters.Adjust.asmdef` - Adjust impl
- `Runtime/Adapters/Firebase/Sorolla.Adapters.Firebase.asmdef` - Firebase impl
- `Editor/Sorolla.Editor.asmdef` - Editor tools

### Documentation
- `DEVLOG.md` - Critical validated learnings (READ THIS FIRST)
- `Documentation~/internal/architecture.md` - Complete technical reference
- `CLAUDE.md` - AI agent guidance
- `README.md` - Public-facing quickstart

### Test/Dev Commands
- Menu: `Palette > Configuration` - Mode setup, SDK installation
- Menu: `Palette > Tools > Validate Build` - Pre-build validation
- Menu: `Palette > Tools > Sanitize Android Manifest` - Remove orphaned entries
- Menu: `Palette > Tools > Check MAX Settings` - Disable Quality Service

## Open Questions
- UNCONFIRMED: Firebase cache cleanup fix (2026-01-20) - monitor for "Directory not empty" errors in production
- UNCONFIRMED: EDM4U Gradle compatibility with Unity 6 - first resolution may fail, works after mode selection

## Codebase Summary

**Sorolla SDK** is a Unity mobile publisher SDK (v3.1.0) that provides a unified `Palette` API for:
- Analytics: GameAnalytics (core), Facebook, Firebase Analytics
- Ads: AppLovin MAX (mediation)
- Attribution: Adjust
- Privacy: iOS ATT, GDPR/CMP via MAX

**Architecture**: Two-mode system (Prototype vs Full) with stub+implementation pattern for optional dependencies.

**Entry Points**:
1. `SorollaBootstrapper` auto-creates via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`
2. On iOS: Shows ATT context screen, waits for user consent
3. Calls `Palette.Initialize()` with consent status
4. Initializes SDKs based on mode and installed packages

**Key Components**:
- **Runtime/Palette.cs** (600 lines) - Public API with methods for analytics events, ad display, attribution
- **Runtime/Adapters/** (stubs) - Interface definitions, delegates to implementations
- **Runtime/Adapters/{MAX,Adjust,Firebase}/** (impls) - Actual SDK wrappers with [Preserve] and RuntimeInitializeOnLoadMethod
- **Editor/Sdk/SdkRegistry.cs** - Single source of truth (10 SDKs, version/scope/requirement metadata)
- **Editor/BuildValidator.cs** - Pre-build checks (version mismatches, mode consistency, manifest sanitization)

**Critical Learnings** (from DEVLOG.md):
- versionDefines are per-assembly only, NOT project-wide
- link.xml in UPM packages is NOT auto-included - must be in Assets/
- MAX SDK key is in AppLovinSettings, NOT SorollaConfig
- Firebase git packages cause "Directory not empty" errors - SDK auto-clears cache before install
- EDM4U bundles Gradle 5.1.1 (incompatible with Unity 6 Java 17+) - first resolution may fail

**File Count**: 72 C# files, ~30 editor tools, ~20 runtime components
