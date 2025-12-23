# Changelog

All notable changes to this project will be documented in this file.

## [2.2.1] - 2025-12-23

### Fixed
- **MAX SDK Initialization**: Added missing `MaxSdk.SetSdkKey()` call - SDK key was passed but never used
- **Duplicate Registry Scope**: Fixed "com.applovin defined in multiple registries" error
  - Added `RemoveScopeFromRegistry()` to clean up duplicate scopes
  - **Manual fix required**: Remove `com.applovin` from OpenUPM scopes in `Packages/manifest.json` before opening Unity
  - Future installs now prevent `com.applovin` from being added to OpenUPM (should only be in AppLovin registry)
- **SorollaSDK.cs**: Fixed `s_config` typo → `Config` on line 275

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

