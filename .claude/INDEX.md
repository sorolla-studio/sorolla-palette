# Index: Sorolla SDK

version: 3.4.0
type: unity-package
path: ~/unity-projects/unity-fastlane-ci/Packages/com.sorolla.sdk
github: https://github.com/sorolla-studio/sorolla-palette.git
keywords: [SDK, Unity, UPM, package, mobile, publisher, Palette, API, analytics, GameAnalytics, Facebook, Firebase, ads, AppLovin, MAX, mediation, rewarded, interstitial, banner, attribution, Adjust, privacy, ATT, GDPR, CMP, consent, Prototype, Full, stub, adapter, asmdef, assembly, IL2CPP, stripping, link.xml, Preserve, versionDefines, defineConstraints, SdkRegistry, SdkInstaller, BuildValidator, editor, Android, iOS, manifest, C#]

## Key Documents

### Runtime
- `Runtime/Palette.cs` - Main public API, static class [API, analytics, ads, attribution, events]
- `Runtime/SorollaBootstrapper.cs` - Auto-init via RuntimeInitializeOnLoadMethod [bootstrap, initialization, entry point]
- `Runtime/SorollaConfig.cs` - ScriptableObject configuration [config, settings, Resources]
- `Runtime/SorollaLoadingOverlay.cs` - Loading overlay UI [loading, overlay, UI]
- `Runtime/GameAnalyticsAdapter.cs` - Core analytics, always required [GameAnalytics, analytics, events]
- `Runtime/Adapters/MaxAdapter.cs` - MAX stub [ads, MAX, AppLovin, stub]
- `Runtime/Adapters/MAX/MaxAdapterImpl.cs` - MAX implementation [ads, MAX, rewarded, interstitial, banner]
- `Runtime/Adapters/AdjustAdapter.cs` - Adjust stub [attribution, Adjust, stub]
- `Runtime/Adapters/Adjust/AdjustAdapterImpl.cs` - Adjust implementation [attribution, Adjust, tracking]
- `Runtime/Adapters/FirebaseAdapter.cs` - Firebase Analytics stub [Firebase, analytics, stub]
- `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs` - Firebase implementation [Firebase, analytics, events]
- `Runtime/Adapters/Firebase/FirebaseCoreManagerImpl.cs` - Firebase core manager [Firebase, core, initialization]
- `Runtime/Adapters/Firebase/FirebaseCrashlyticsAdapterImpl.cs` - Crashlytics implementation [Firebase, Crashlytics, crashes]
- `Runtime/Adapters/Firebase/FirebaseRemoteConfigAdapterImpl.cs` - Remote Config implementation [Firebase, RemoteConfig, A/B]
- `Runtime/Adapters/FirebaseCrashlyticsAdapter.cs` - Crashlytics stub [Firebase, Crashlytics, stub]
- `Runtime/Adapters/FirebaseRemoteConfigAdapter.cs` - Remote Config stub [Firebase, RemoteConfig, stub]
- `Runtime/Adapters/FacebookAdapter.cs` - Facebook stub [Facebook, attribution, stub]
- `Runtime/Adapters/TikTokAdapter.cs` - TikTok stub [TikTok, attribution, stub]
- `Runtime/Adapters/TikTokAdapterImpl.cs` - TikTok native bridge implementation [TikTok, Android, iOS, JNI, DllImport]
- `Runtime/ATT/` - iOS App Tracking Transparency [ATT, iOS, privacy, consent, IDFA]

### Editor
- `Editor/Sdk/SdkRegistry.cs` - Single source of truth for SDK metadata [registry, versions, packages, scopes]
- `Editor/Sdk/SdkInstaller.cs` - Package installation via manifest.json [installer, UPM, manifest, packages]
- `Editor/Sdk/SdkDetector.cs` - Detect installed SDKs [detection, packages, installed]
- `Editor/Sdk/SdkConfigDetector.cs` - Detect SDK configuration state [config, detection]
- `Editor/Sdk/DefineSymbols.cs` - Global scripting defines based on installed packages [defines, symbols, conditional]
- `Editor/Sdk/SdkVersionSync.cs` - Auto-updates manifest versions on domain reload [sync, versions, manifest]
- `Editor/BuildValidator.cs` - Pre-build validation [validation, build, IPreprocessBuildWithReport]
- `Editor/SorollaWindow.cs` - Configuration UI [editor, window, UI, menu, Palette]
- `Editor/SorollaSettings.cs` - Editor settings [settings, editor]
- `Editor/SorollaSetup.cs` - Initial setup flow [setup, first-run]
- `Editor/SorollaTestingTools.cs` - Testing utilities [testing, debug]
- `Editor/SorollaIOSPostProcessor.cs` - iOS build post-processing [iOS, build, Xcode]
- `Editor/AndroidManifestSanitizer.cs` - Remove orphaned SDK entries [Android, manifest, cleanup]
- `Editor/MaxSettingsSanitizer.cs` - Disable Quality Service [MAX, settings, 401 error]
- `Editor/MaxVersionChecker.cs` - Auto-check for MAX SDK updates [MAX, auto-update, versions]
- `Editor/Edm4uSanitizer.cs` - EDM4U cleanup [EDM4U, dependencies, sanitizer]
- `Editor/ManifestManager.cs` - UPM manifest management [manifest, packages, scoped registries]
- `Editor/MigrationPopup.cs` - Version migration UI [migration, upgrade, popup]
- `Editor/MiniJson.cs` - Lightweight JSON parser [JSON, parser, utility]

### Documentation
- `LEARNINGS.md` - Greppable facts via `/reflect` [quick lookup, tagged]
- `DEVLOG.md` - Chronological session history [debugging, past decisions]
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
