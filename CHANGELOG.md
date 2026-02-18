# Changelog

All notable changes to this project will be documented in this file.

## [3.4.0] - 2026-02-18

### Added
- **TikTok Business SDK integration**: Native Bridge pattern — config-driven init, no compilation impact when unconfigured. Android (JNI) + iOS (Objective-C runtime). Supports `identify`, `track`, and custom events.
- **ProGuard rules for TikTok**: `-keep class com.tiktok.**` shipped in SDK package for release Android builds
- **SdkVersionSync editor utility**: Auto-updates stale manifest entries on domain reload

### Fixed
- **Firebase asmdef references**: Switched to `precompiledReferences` for custom Firebase UPM compatibility
- **GameActivityTheme.androidlib**: Fixes Unity 6000.3.x Android build failures
- **iOS ATT regression**: Restored `com.unity.ads.ios-support` dependency and correct namespace
- **SorollaTikTok.mm.meta**: Added explicit PluginImporter with iOS-only platform settings

## [3.3.1] - 2026-02-18

### Fixed
- **iOS ATT prompt in Prototype mode**: Standalone ATT dialog now shows automatically when MAX is not installed. Fixes App Store rejection for apps without AppLovin MAX.

## [3.3.0] - 2026-02-10

### Changed
- **Firebase is now optional in Prototype mode**: Only required in Full mode, can be manually installed in Prototype via UI
- **Mode table updated**: Prototype mode lists Firebase as optional, Full mode still requires it
- **SDK Overview UI**: Firebase row now shows Install button in Prototype mode, mode-aware required/optional labels

### Added
- **SdkVersionSync**: Auto-syncs installed SDK versions with `SdkRegistry` constants on domain reload
- **MAX version checker**: Queries AppLovin registry for latest MAX version, prompts Update/Skip/Later once per session
- **Firebase config sub-rows**: Build Health shows individual google-services.json / GoogleService-Info.plist status

### Fixed
- **Build validator**: Improved Firebase coherence checks for optional Firebase in Prototype mode
- **Migration popup**: Updated messaging for Firebase-optional flow
- **EDM4U sanitizer**: Better handling of dependency resolution edge cases

## [3.2.2] - 2026-01-26

### Added
- **GameAnalytics ILRD**: Automatic impression-level revenue tracking via `GameAnalyticsILRD.SubscribeMaxImpressions()`
- **Firebase `ad_impression` events**: Ad revenue now logged to Firebase Analytics with full parameters (`ad_platform`, `ad_source`, `ad_format`, `ad_unit_name`, `value`, `currency`)
- `FirebaseAdapter.TrackAdImpression()` public API for ad revenue tracking

### Changed
- Ad revenue now tracked to all three platforms: GameAnalytics (ILRD), Firebase (`ad_impression`), and Adjust
- `TrackAdRevenue` now includes ad format (INTERSTITIAL/REWARDED) for better analytics segmentation

## [3.2.1] - 2026-01-26

### Fixed
- **Build Health now shows missing required SDKs**: Error displayed when SDKs like Facebook are not installed
- **Improved Adjust Settings messages**: Clearer status when Adjust is not required or not installed

## [3.2.0] - 2026-01-26

### Added
- **Auto-install missing SDKs**: Clicking "Refresh" in Build Health or switching modes now auto-installs missing required SDKs
  - Fixes edge case where Full mode was active but Adjust SDK was missing
  - Only triggers on explicit user action (not on window open)
- **MAX SDK version checker**: Build Health now validates MAX SDK version against expected version
- **EDM4U duplicate detection**: Warns about duplicate External Dependency Manager installations

### Changed
- **Facebook SDK now Core**: Always installed in both Prototype and Full modes (was FullRequired)
- **Improved SDK installation UX**: Better feedback during SDK install/uninstall operations
- **Simplified editor UI**: Removed welcome screen, streamlined SorollaWindow

### Fixed
- **Unity 6 DontDestroyOnLoad**: Added robust handling to prevent assertion failures
- **Code preservation**: Added `[Preserve]` attributes to prevent IL2CPP stripping
- **Standardized log tags**: Consistent `[Palette]` prefix across all adapters

## [3.1.0] - 2026-01-13

### Changed
- **Firebase is now required** in all modes (Prototype + Full)
- Firebase packages auto-install on SDK import/upgrade
- SetupVersion bumped to v7 (triggers setup for upgrading users)
- **SdkInfo refactored to immutable struct** - prevents accidental mutation
- **SorollaWindow editor cleanup**:
  - Cached GUIStyles and SerializedObject to avoid GC pressure
  - Removed style mutation in OnGUI (uses DrawIcon helper)
  - Merged Firebase Configuration into Build Health as sub-rows
  - Moved MAX Ad Units to SDK Keys section
  - Simplified SDK overview rendering

### Added
- **Migration popup**: One-time guide for Firebase setup after SDK upgrade
- Links to Firebase Console and configuration window

### Fixed
- Removed module toggles for Firebase (no longer optional)
- Cleaned up stale Firebase references throughout codebase

### Documentation
- Updated `firebase.md` to reflect required status
- Added Firebase config step to `prototype-setup.md`
- Updated `full-setup.md` to show Firebase as required

## [3.0.0] - 2025-01-12

### Breaking Changes
- **Ad Unit IDs**: Replaced single fields with `PlatformAdUnitId` class containing Android/iOS variants
  - `maxRewardedAdUnitId` → `rewardedAdUnit.android` / `rewardedAdUnit.ios`
  - `maxInterstitialAdUnitId` → `interstitialAdUnit.android` / `interstitialAdUnit.ios`
  - `maxBannerAdUnitId` → `bannerAdUnit.android` / `bannerAdUnit.ios`
  - **Migration**: Re-enter ad unit IDs in the new platform-specific fields

### Added
- **Platform-Specific Ad Units**: `PlatformAdUnitId` class with `.Current` property for automatic platform selection
- **AppLovin Quality Service Auto-Fix**: Build validator auto-disables Quality Service to prevent 401 build failures
- **Duplicate Activity Detection**: AndroidManifestSanitizer now detects and removes duplicate SDK activities

### Fixed
- Setup version key updated from v3 to v6
- `s_config` → `Config` reference in `TrackDesign` method

### Changed
- Merged `FirebaseCoreManager` into `FirebaseAdapter.cs`
- Merged `BuildValidationWindow` menu into `SorollaWindow.cs`
- Merged `SorollaMode` enum into `SorollaSettings.cs`
- Removed unused `GetCrashlyticsStatus`/`GetRemoteConfigStatus` methods
- Removed unused `IsValid()` from `SorollaConfig`
- Various code simplifications and refactoring

## [2.3.3] - 2025-12-29

### Fixed
- **Fresh Import Compilation Errors**: Re-added `defineConstraints` to implementation assemblies
  - v2.3.2 incorrectly removed constraints, causing CS0246 errors on fresh imports without SDKs
  - Unity compiles C# files before checking assembly references, so constraints are required
- **EDM4U Gradle Java 17+ Compatibility**: Auto-configures EDM4U to use Unity's Gradle templates
  - Mitigates `java.lang.NoClassDefFoundError` on Unity 6+ (initial resolution may still show error)
  - EDM4U bundles Gradle 5.1.1 which is incompatible with Java 17+ (Unity 6 default JDK)
  - Automatically sets `PatchMainTemplateGradle`, `PatchPropertiesTemplateGradle`, `PatchSettingsTemplateGradle`
  - Error is transient: re-resolving after mode selection works correctly

### Technical Details
The v2.3.2 "assembly references as constraints" approach was wrong. Unity still attempts to compile C# files before checking if referenced assemblies exist.

**Correct pattern (now implemented):**
```
versionDefines: SDK installed → sets DEFINE (e.g., APPLOVIN_MAX_INSTALLED)
defineConstraints: ["DEFINE"] → assembly only compiles if DEFINE is set
```

**EDM4U Gradle fix:**
EDM4U's bundled Gradle 5.1.1 doesn't support Java 17+ (used by Unity 6). By enabling template patching, EDM4U integrates with Unity's Gradle version instead of using its bundled one.

## [2.3.2] - 2025-12-26

### Fixed
- **IL2CPP Stripping Protection**: Complete overhaul of code preservation strategy for Unity 6 compatibility
  - Added `[assembly: AlwaysLinkAssembly]` to all implementation assemblies (MAX, Adjust, Firebase)
  - Added `[Preserve]` attributes to Register() methods
  - Auto-copies `link.xml` to Assets folder on setup (Unity doesn't process link.xml from packages)
- **Loading Overlay**: Removed built-in Arial font dependency for cross-platform compatibility

### Added
- `SorollaSetup.CopyLinkXmlToAssets()` - Auto-copies stripping protection on package setup
- Three-layer code preservation: AlwaysLinkAssembly → [Preserve] → link.xml fallback

## [2.3.1] - 2025-12-24

### Fixed
- **Firebase Uninstall Config Sync**: Config flags now disabled **before** package removal to prevent domain reload interruption
- **Build Health Auto-Fix**: Refresh button now auto-fixes config sync issues (Firebase flags, mode mismatch)
  - Shows "AUTO-FIXED: Synced SorollaConfig with installed SDKs" when fixes applied

### Added
- `BuildValidator.FixConfigSync()` method for programmatic config repair

## [2.3.0] - 2025-12-24

### Changed
- **Adapter Architecture Overhaul**: Refactored all optional SDK adapters to use Interface + Registration pattern
  - Stubs always compile (no external dependencies)
  - Implementations in separate assemblies with `defineConstraints`
  - Runtime registration via `RuntimeInitializeOnLoadMethod`
- **Assembly Structure**: Split adapters into isolated assemblies
  - `Sorolla.Adapters` - Core stubs (always compiles)
  - `Sorolla.Adapters.Firebase` - Firebase implementations (compiles only when Firebase installed)
  - `Sorolla.Adapters.MAX` - MAX implementation (compiles only when MAX installed)
  - `Sorolla.Adapters.Adjust` - Adjust implementation (compiles only when Adjust installed)

### Added
- `AdRevenueInfo` struct for cross-SDK ad revenue tracking
- `IMaxAdapter`, `IAdjustAdapter`, `IFirebaseAdapter` internal interfaces

### Fixed
- **Prototype Mode Compilation**: SDK now compiles cleanly without Firebase/MAX/Adjust installed
  - Root cause: Unity resolves assembly references BEFORE evaluating `#if` preprocessor blocks
  - Solution: `defineConstraints` in child asmdefs prevent assembly compilation entirely when SDKs missing
- Removed unused `s_consentStatusChanged` field in SorollaSDK.cs

### Technical Details
The previous approach used `#if/#else` blocks with assembly references in a single asmdef. This failed because Unity's assembly resolution happens before C# compilation, causing "assembly not found" errors even for code inside `#if false` blocks.

New pattern:
```
Adapters/
├── Sorolla.Adapters.asmdef      (no external refs)
├── MaxAdapter.cs                 (stub → delegates to impl)
├── Firebase/
│   ├── Sorolla.Adapters.Firebase.asmdef  (defineConstraints: FIREBASE_*_INSTALLED)
│   └── FirebaseAdapterImpl.cs            (registers at runtime)
├── MAX/
│   └── ...
└── Adjust/
    └── ...
```

## [2.2.1] - 2025-12-23

### Fixed
- **MAX SDK Initialization**: Added missing `MaxSdk.SetSdkKey()` call - SDK key was passed but never used
- **Duplicate Registry Scope**: Fixed "com.applovin defined in multiple registries" error
  - Added `RemoveScopeFromRegistry()` to clean up duplicate scopes
  - Future installs now prevent `com.applovin` from being added to OpenUPM (should only be in AppLovin registry)
- **Prototype Mode Compilation**: Fixed "MaxAdapter does not exist" errors when MAX not installed
  - Added `#if` guards around all MaxAdapter references in SorollaSDK.cs
  - Removed hard assembly references from asmdef files (relies on auto-referencing + versionDefines)
  - All optional SDK adapters now compile cleanly when their packages aren't installed
- **SorollaSDK.cs**: Fixed `s_config` typo → `Config` on line 275

### Upgrade Notes
If you encounter errors after updating to 2.2.1, manual manifest fixes may be required **before opening Unity**:

**"com.applovin defined in multiple registries" error:**
1. Open `Packages/manifest.json` in a text editor
2. Find the `scopedRegistries` section with `"url": "https://package.openupm.com"`
3. Remove `"com.applovin"` from its `scopes` array (keep it only in AppLovin MAX registry)
4. Save and reopen Unity

**"AdjustSdk could not be found" error (Prototype mode only):**
1. Open `Packages/manifest.json`
2. Remove `"com.adjust.sdk"` from dependencies (not needed in Prototype mode)
3. Delete `Library/PackageCache/com.adjust.sdk*` folder if present
4. Save and reopen Unity

## [2.2.0] - 2025-12-18

### Added
- **SDK Overview Section**: Unified view combining install status + config status per SDK
  - Shows all SDKs with: ✓/✗/○ install icon, config status, single action button
  - Firebase shows nested module status (Analytics, Crashlytics, Remote Config)
  - Replaces previous "Setup Checklist" and "SDK Status" sections
- **Build Health Validator**: Pre-build validation integrated into Configuration window
  - 6 validation checks: SDK Versions, Mode Consistency, Scoped Registries, Firebase Coherence, Config Sync, Android Manifest
  - Visual display of all checks with status icons (✓/⚠/✗)
  - Auto-runs on window open and after mode switch
  - Pre-build hook via `IPreprocessBuildWithReport` - errors block builds
- **AndroidManifest Sanitizer**: Auto-detects and removes orphaned SDK entries
  - Fixes `ClassNotFoundException` crashes when switching modes
  - Creates backup before modifying manifest
  - Menu item: `SorollaSDK > Tools > Sanitize Android Manifest`
- **UMP Consent Integration**: GDPR/ATT consent API via MAX (see `gdpr-consent-setup.md`)
  - `SorollaSDK.ConsentStatus` - Current consent state
  - `SorollaSDK.CanRequestAds` - Whether ads can be shown
  - `SorollaSDK.ShowPrivacyOptions()` - Opens UMP privacy form
  - `OnConsentStatusChanged` event for UI updates

### Changed
- **UI Consolidation**: Merged 3 sections into 2 for cleaner, less redundant interface
  - SDK Overview: per-SDK install + config status (was: Setup Checklist + SDK Status)
  - Build Health: technical validation checks (removed "Required SDKs" - now in SDK Overview)
- Version mismatch warnings only trigger for outdated versions (newer is OK)

### Fixed
- Fixed runtime crash when switching from Prototype to Full mode due to orphaned Facebook SDK entries in AndroidManifest.xml

## [2.1.0] - 2025-12-01

### Added
- **Firebase Suite**: Full Firebase integration with Analytics, Crashlytics, and Remote Config
- `FirebaseAdapter` with async-safe initialization and event queuing
- `FirebaseCrashlyticsAdapter` with automatic exception capture and custom logging
- `FirebaseRemoteConfigAdapter` with typed getters and fetch/activate support
- `FirebaseCoreManager` for centralized Firebase initialization (prevents race conditions)
- Firebase section in Configuration window with install button, module toggles, and config file checklist
- Firebase in Setup Checklist (shown when installed, as optional item)
- Detection for `google-services.json` and `GoogleService-Info.plist` config files
- `enableFirebaseAnalytics`, `enableCrashlytics`, `enableRemoteConfig` toggles in SorollaConfig
- **[Firebase Setup Guide](Documentation~/FirebaseSetup.md)**: Documentation for Firebase Console setup and usage
- New public APIs:
  - **Crashlytics**:
    - `Sorolla.LogException(Exception)` - Log non-fatal exceptions
    - `Sorolla.LogCrashlytics(string)` - Add breadcrumb logs
    - `Sorolla.SetCrashlyticsKey(string, value)` - Set custom keys
  - **Unified Remote Config** (Firebase → GameAnalytics → default fallback):
    - `Sorolla.IsRemoteConfigReady()` - Check if Remote Config is available
    - `Sorolla.FetchRemoteConfig(Action<bool>)` - Fetch remote values
    - `Sorolla.GetRemoteConfig(key, default)` - Get string value
    - `Sorolla.GetRemoteConfigInt(key, default)` - Get int value
    - `Sorolla.GetRemoteConfigFloat(key, default)` - Get float value
    - `Sorolla.GetRemoteConfigBool(key, default)` - Get bool value

### Changed
- All analytics events (`TrackProgression`, `TrackDesign`, `TrackResource`) now dispatch to Firebase when enabled
- Firebase SDK installed via Git UPM from `github.com/LaCreArthur/unity-firebase-app`
- Single "Install Firebase" button installs all 4 packages (App, Analytics, Crashlytics, Remote Config)
- **Unified Remote Config API**: Single set of methods that checks Firebase first, then falls back to GameAnalytics

### SDK Versions
- Firebase App: 12.10.1 (Git UPM)
- Firebase Analytics: 12.10.1 (Git UPM)
- Firebase Crashlytics: 12.10.1 (Git UPM)
- Firebase Remote Config: 12.10.1 (Git UPM)
- GameAnalytics: 7.10.6
- External Dependency Manager: 1.2.186
- AppLovin MAX: 8.5.0
- Facebook SDK: 18.0.1 (Git URL)

## [2.0.1] - 2025-11-26

### Changed
- **Refactored**: SDK installation now uses manifest.json directly instead of `Client.Add()` queue
- **Refactored**: Core dependencies (GA, EDM, iOS Support) installed via OpenUPM scoped registry
- SDK versions now centralized in `SdkRegistry` for easy updates

### Fixed
- Fixed GameAnalytics installation failing due to EDM dependency order
- Fixed potential infinite loading when Package Manager was busy during installation

### SDK Versions
- GameAnalytics: 7.10.6
- External Dependency Manager: 1.2.186
- AppLovin MAX: 8.5.0
- Facebook SDK: 18.0.1 (Git URL)

## [2.0.0] - 2025-11-25

### Changed
- **Renamed**: Package from `com.sorolla.palette` to `com.sorolla.sdk`
- **Renamed**: Namespace from `SorollaPalette` to `Sorolla`
- **Renamed**: Main API class from `SorollaPalette` to `Sorolla`
- **Renamed**: Config asset from `SorollaPaletteConfig` to `SorollaConfig`
- **Refactored**: Mode system now uses `enum SorollaMode { None, Prototype, Full }`
- **Moved**: Adapters from `Modules/` to `Runtime/Adapters/`
- **Moved**: SDK utilities to `Editor/Sdk/` subfolder

### Added
- Setup Checklist in Configuration window with SDK status detection
- "Open Settings" buttons for quick access to GA/FB/MAX configuration
- Links section with Documentation, GitHub, and Issue tracker
- `SdkConfigDetector` for detecting SDK configuration status
- `SdkRegistry` as single source of truth for SDK metadata

### Improved
- Configuration window UX with cleaner layout
- SDK detection with better error handling
- Mode switching with confirmation dialog

## [1.0.0] - 2025-11-10

### Added
- Initial release
- Prototype Mode support (GA + Facebook + optional MAX)
- Full Mode support (GA + MAX + Adjust)
- Configuration window with DRY refactoring
- Mode selection wizard with auto-MAX and auto-Adjust installation for Full Mode
- SDK adapters for Facebook, MAX, and Adjust
- Auto-installation of GameAnalytics SDK
- On-demand AppLovin MAX SDK installation
- On-demand Adjust SDK installation via UPM
- DRY refactored codebase with reusable helpers
- Generic SDK detection pattern
- Reusable manifest modification helpers
- Modular config section rendering

