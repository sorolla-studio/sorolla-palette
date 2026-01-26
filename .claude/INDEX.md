# Index: Sorolla SDK

type: unity-package
path: ~/unity-projects/unity-fastlane-ci/Packages/com.sorolla.sdk
github: https://github.com/sorolla-studio/sorolla-palette.git
keywords: [SDK, Unity, UPM, package, mobile, publisher, Palette, API, analytics, GameAnalytics, Facebook, Firebase, ads, AppLovin, MAX, mediation, rewarded, interstitial, banner, attribution, Adjust, privacy, ATT, GDPR, CMP, consent, Prototype, Full, stub, adapter, asmdef, assembly, IL2CPP, stripping, link.xml, Preserve, versionDefines, defineConstraints, SdkRegistry, SdkInstaller, BuildValidator, editor, Android, iOS, manifest, C#]

## Key Documents

### Runtime
- `Runtime/Palette.cs` - Main public API, static class [API, analytics, ads, attribution, events]
- `Runtime/SorollaBootstrapper.cs` - Auto-init via RuntimeInitializeOnLoadMethod [bootstrap, initialization, entry point]
- `Runtime/SorollaConfig.cs` - ScriptableObject configuration [config, settings, Resources]
- `Runtime/GameAnalyticsAdapter.cs` - Core analytics, always required [GameAnalytics, analytics, events]
- `Runtime/Adapters/MaxAdapter.cs` - MAX stub [ads, MAX, AppLovin, stub]
- `Runtime/Adapters/MAX/MaxAdapterImpl.cs` - MAX implementation [ads, MAX, rewarded, interstitial, banner]
- `Runtime/Adapters/AdjustAdapter.cs` - Adjust stub [attribution, Adjust, stub]
- `Runtime/Adapters/Adjust/AdjustAdapterImpl.cs` - Adjust implementation [attribution, Adjust, tracking]
- `Runtime/Adapters/FirebaseAdapter.cs` - Firebase Analytics stub [Firebase, analytics, stub]
- `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs` - Firebase implementation [Firebase, analytics, events]
- `Runtime/ATT/` - iOS App Tracking Transparency [ATT, iOS, privacy, consent, IDFA]

### Editor
- `Editor/Sdk/SdkRegistry.cs` - Single source of truth for SDK metadata [registry, versions, packages, scopes]
- `Editor/Sdk/SdkInstaller.cs` - Package installation via manifest.json [installer, UPM, manifest, packages]
- `Editor/Sdk/SdkDetector.cs` - Detect installed SDKs [detection, packages, installed]
- `Editor/BuildValidator.cs` - Pre-build validation [validation, build, IPreprocessBuildWithReport]
- `Editor/SorollaWindow.cs` - Configuration UI [editor, window, UI, menu, Palette]
- `Editor/AndroidManifestSanitizer.cs` - Remove orphaned SDK entries [Android, manifest, cleanup]
- `Editor/MaxSettingsSanitizer.cs` - Disable Quality Service [MAX, settings, 401 error]

### Documentation
- `DEVLOG.md` - Critical validated learnings [learnings, decisions, gotchas]
- `Documentation~/internal/architecture.md` - Complete technical reference [architecture, patterns, design]
- `CLAUDE.md` - AI agent guidance [context, conventions]

### Plans
- `.claude/plans/max-version-checker.md` - MAX SDK auto-update checker plan [MAX, auto-update, versions, AppLovin]

## Architecture Patterns

- Stub + Implementation - Separate assemblies for optional SDK adapters
- Per-assembly versionDefines - defineConstraints for conditional compilation
- RuntimeInitializeOnLoadMethod - Auto-registration with IL2CPP protection
- Single Source of Truth - SdkRegistry.cs for all SDK metadata

## Key Decisions

- versionDefines are per-assembly only, NOT project-wide
- link.xml in UPM packages is NOT auto-included by Unity
- MAX SDK key is in AppLovinSettings, NOT SorollaConfig
- Three-layer IL2CPP protection: AlwaysLinkAssembly + Preserve + link.xml
