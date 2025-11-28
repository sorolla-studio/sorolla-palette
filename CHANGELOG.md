# Changelog

All notable changes to this project will be documented in this file.

## [2.1.0] - 2025-11-27

### Added
- **Firebase Suite**: Full Firebase integration with Analytics, Crashlytics, and Remote Config
- `FirebaseAdapter` with async-safe initialization and event queuing
- `FirebaseCrashlyticsAdapter` with automatic exception capture and custom logging
- `FirebaseRemoteConfigAdapter` with typed getters and fetch/activate support
- Firebase section in Configuration window with install button, module toggles, and config file checklist
- Detection for `google-services.json` and `GoogleService-Info.plist` config files
- `enableFirebaseAnalytics`, `enableCrashlytics`, `enableRemoteConfig` toggles in SorollaConfig
- New public APIs:
  - `Sorolla.LogException(Exception)` - Log non-fatal exceptions
  - `Sorolla.LogCrashlytics(string)` - Add breadcrumb logs
  - `Sorolla.SetCrashlyticsKey(string, string)` - Set custom keys
  - `Sorolla.IsFirebaseRemoteConfigReady()` - Check RC status
  - `Sorolla.FetchFirebaseRemoteConfig(Action<bool>)` - Fetch remote values
  - `Sorolla.GetFirebaseRemoteConfig*(key, default)` - Get typed values

### Changed
- All analytics events (`TrackProgression`, `TrackDesign`, `TrackResource`) now dispatch to Firebase when enabled
- Firebase SDK now installed via Git UPM from `github.com/LaCreArthur/unity-firebase-app`
- Single "Install Firebase" button installs all 4 packages (App, Analytics, Crashlytics, Remote Config)

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

