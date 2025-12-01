# Sorolla SDK - Architecture Guide

> **For AI Agents & Developers**: This document provides a complete overview of the Sorolla SDK codebase. Read this first to understand the architecture before making changes.

## Overview

**Sorolla SDK** (`com.sorolla.sdk`) is a plug-and-play mobile publisher SDK for Unity games. It wraps multiple third-party SDKs (GameAnalytics, Facebook, AppLovin MAX, Adjust, Firebase) behind a unified API.

### Key Design Principles

1. **Zero-Configuration**: Auto-initializes at runtime - developers don't write initialization code
2. **Conditional Compilation**: SDK adapters only compile when their SDK is installed (via `#if` directives)
3. **Mode-Based**: Two modes (Prototype/Full) determine which SDKs are required
4. **Single Source of Truth**: `SdkRegistry.cs` contains all SDK metadata and versions

---

## Package Structure

```
Packages/com.sorolla.sdk/
├── package.json                    # UPM package manifest (v2.1.0)
├── README.md                       # User-facing documentation
├── CHANGELOG.md                    # Version history
├── LICENSE.md
│
├── Documentation~/                 # Unity-hidden docs folder
│   ├── ARCHITECTURE.md            # THIS FILE - Agent/dev onboarding
│   ├── QuickStart.md              # User quick start guide
│   └── FirebaseSetup.md           # Firebase configuration guide
│
├── Runtime/                        # Runtime code (ships with builds)
│   ├── Sorolla.cs                 # ⭐ MAIN API - Static singleton
│   ├── SorollaConfig.cs           # ScriptableObject configuration
│   ├── SorollaBootstrapper.cs     # Auto-init MonoBehaviour
│   ├── SorollaLoadingOverlay.cs   # Loading UI helper
│   ├── GameAnalyticsAdapter.cs    # GA wrapper (always included)
│   │
│   ├── Adapters/                  # Third-party SDK wrappers
│   │   ├── FacebookAdapter.cs     # Facebook SDK (#if SOROLLA_FACEBOOK_ENABLED)
│   │   ├── MaxAdapter.cs          # AppLovin MAX (#if SOROLLA_MAX_ENABLED)
│   │   ├── AdjustAdapter.cs       # Adjust SDK (#if SOROLLA_ADJUST_ENABLED)
│   │   ├── FirebaseAdapter.cs     # Firebase Analytics
│   │   ├── FirebaseCoreManager.cs # Firebase init coordinator
│   │   ├── FirebaseCrashlyticsAdapter.cs
│   │   └── FirebaseRemoteConfigAdapter.cs
│   │
│   └── ATT/                       # iOS App Tracking Transparency
│       ├── ContextScreenView.cs   # Pre-ATT explanation screen
│       └── AutoSwitchLayout.cs    # Layout helper
│
└── Editor/                        # Editor-only code
    ├── SorollaWindow.cs           # ⭐ Main configuration window
    ├── SorollaSettings.cs         # Mode persistence (EditorPrefs)
    ├── SorollaSetup.cs            # Auto-setup on import
    ├── SorollaMode.cs             # Enum: None, Prototype, Full
    ├── SorollaTestingTools.cs     # Debug utilities
    ├── SorollaIOSPostProcessor.cs # Xcode post-processing
    ├── ManifestManager.cs         # manifest.json manipulation
    ├── MiniJson.cs                # Lightweight JSON parser
    │
    └── Sdk/                       # SDK management utilities
        ├── SdkRegistry.cs         # ⭐ SDK metadata & versions
        ├── SdkDetector.cs         # Check if SDK installed
        ├── SdkConfigDetector.cs   # Check if SDK configured
        ├── SdkInstaller.cs        # Install/uninstall SDKs
        └── DefineSymbols.cs       # Scripting define management
```

---

## Core Components

### 1. `Sorolla.cs` - Main API

The **public API** that game developers use. Static class with all SDK functionality.

```csharp
// Location: Runtime/Sorolla.cs
// Namespace: Sorolla

public static class Sorolla
{
    // State
    public static bool IsInitialized { get; }
    public static bool HasConsent { get; }
    public static SorollaConfig Config { get; }

    // Initialization (called by SorollaBootstrapper - NOT by users)
    public static void Initialize(bool consent);

    // Analytics
    public static void TrackProgression(ProgressionStatus, string, string?, string?, int);
    public static void TrackDesign(string eventName, float value = 0);
    public static void TrackResource(ResourceFlowType, string currency, float amount, string itemType, string itemId);

    // Remote Config (unified: Firebase → GameAnalytics → default)
    public static bool IsRemoteConfigReady();
    public static void FetchRemoteConfig(Action<bool> onComplete);
    public static string GetRemoteConfig(string key, string defaultValue);
    public static int GetRemoteConfigInt(string key, int defaultValue);
    public static float GetRemoteConfigFloat(string key, float defaultValue);
    public static bool GetRemoteConfigBool(string key, bool defaultValue);

    // Crashlytics
    public static void LogException(Exception exception);
    public static void LogCrashlytics(string message);
    public static void SetCrashlyticsKey(string key, string value);

    // Ads
    public static void ShowRewardedAd(Action onComplete, Action onFailed);
    public static void ShowInterstitialAd(Action onComplete);
}
```

**Key Pattern**: All methods check `EnsureInit()` before executing. Events dispatch to multiple backends (e.g., `TrackDesign` → GameAnalytics + Firebase if enabled).

### 2. `SorollaBootstrapper.cs` - Auto-Initialization

MonoBehaviour that auto-creates at runtime via `[RuntimeInitializeOnLoadMethod]`.

**Flow**:
```
App Start
    ↓
[RuntimeInitializeOnLoadMethod] creates "[Sorolla SDK]" GameObject
    ↓
Start() → Initialize() coroutine
    ↓
iOS: Check ATT status
    ├── NOT_DETERMINED → Show ContextScreen → Wait for decision
    └── Already decided → Continue
    ↓
Sorolla.Initialize(consent)
    ↓
Initialize all enabled SDKs
```

### 3. `SorollaConfig.cs` - Configuration

ScriptableObject stored at `Assets/Resources/SorollaConfig.asset`.

**Fields**:
- `isPrototypeMode` - Mode toggle
- `maxSdkKey`, `maxRewardedAdUnitId`, `maxInterstitialAdUnitId` - MAX config
- `adjustAppToken` - Adjust config
- `enableFirebaseAnalytics`, `enableCrashlytics`, `enableRemoteConfig` - Firebase toggles

**Loaded at runtime** via `Resources.Load<SorollaConfig>("SorollaConfig")`.

### 4. `SdkRegistry.cs` - SDK Metadata

**Single source of truth** for all SDK information.

```csharp
// Location: Editor/Sdk/SdkRegistry.cs

public enum SdkId { GameAnalytics, Facebook, AppLovinMAX, Adjust, FirebaseAnalytics, ... }

public enum SdkRequirement { Core, PrototypeOnly, FullOnly, Optional }

public class SdkInfo {
    public SdkId Id;
    public string Name;
    public string PackageId;           // UPM package ID
    public string Version;             // For OpenUPM packages
    public string InstallUrl;          // For Git URL packages
    public string Scope;               // OpenUPM scope
    public string[] DetectionAssemblies;
    public string[] DetectionTypes;
    public SdkRequirement Requirement;
}

public static class SdkRegistry {
    // Version constants - UPDATE THESE when upgrading SDKs
    public const string GA_VERSION = "7.10.6";
    public const string MAX_VERSION = "8.5.0";
    public const string FIREBASE_VERSION = "12.10.1";
    // ...

    public static readonly IReadOnlyDictionary<SdkId, SdkInfo> All;
}
```

### 5. `SorollaWindow.cs` - Editor UI

Main configuration window (`Sorolla > Configuration`).

**Sections**:
1. **Mode Selection** - Prototype/Full toggle
2. **Setup Checklist** - Shows SDK configuration status
3. **SDK Status** - Installed/missing indicators with Install buttons
4. **SDK Keys** - MAX/Adjust configuration fields
5. **Firebase** - Install button, module toggles, config file status

---

## Initialization Flow

### Editor-Time (Package Import)

```
Package imported
    ↓
[InitializeOnLoad] SorollaSetup
    ↓
Check SetupKey in EditorPrefs
    ├── Already run → Skip
    └── First time:
        ↓
        ManifestManager.AddOrUpdateRegistry() - Add OpenUPM registry
        ManifestManager.AddDependencies() - Add core SDKs
        ↓
        Unity Package Manager resolves dependencies
        ↓
        SorollaWindow auto-opens (if not configured)
```

### Runtime (App Start)

```
[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
    ↓
SorollaBootstrapper.AutoInit()
    ↓
Creates "[Sorolla SDK]" GameObject (DontDestroyOnLoad)
    ↓
Start() → Initialize() coroutine
    ↓
iOS ATT handling (if needed)
    ↓
Sorolla.Initialize(consent)
    ├── GameAnalyticsAdapter.Initialize() - ALWAYS
    ├── FacebookAdapter.Initialize() - If Prototype + FB installed
    ├── MaxAdapter.Initialize() - If MAX installed + configured
    ├── AdjustAdapter.Initialize() - If Full mode + Adjust installed
    ├── FirebaseAdapter.Initialize() - If enabled in config
    ├── FirebaseCrashlyticsAdapter.Initialize() - If enabled
    └── FirebaseRemoteConfigAdapter.Initialize() - If enabled
    ↓
IsInitialized = true
```

---

## Conditional Compilation

SDKs are wrapped with `#if` directives to prevent compile errors when not installed.

### Scripting Defines

| Define | Set When |
|--------|----------|
| `GAMEANALYTICS_INSTALLED` | GameAnalytics SDK detected |
| `SOROLLA_FACEBOOK_ENABLED` | Facebook SDK detected |
| `SOROLLA_MAX_ENABLED` | AppLovin MAX detected |
| `SOROLLA_ADJUST_ENABLED` | Adjust SDK detected |
| `FIREBASE_ANALYTICS_INSTALLED` | Firebase Analytics detected |
| `FIREBASE_CRASHLYTICS_INSTALLED` | Firebase Crashlytics detected |
| `FIREBASE_REMOTE_CONFIG_INSTALLED` | Firebase Remote Config detected |
| `UNITY_IOS_SUPPORT_INSTALLED` | Unity iOS Support package detected |

### Detection Logic

`SdkDetector.cs` checks for SDK presence:
```csharp
public static bool IsInstalled(SdkId id) {
    var sdk = SdkRegistry.All[id];
    // Check assemblies
    foreach (var asm in sdk.DetectionAssemblies)
        if (FindAssembly(asm)) return true;
    // Check types
    foreach (var type in sdk.DetectionTypes)
        if (Type.GetType(type) != null) return true;
    return false;
}
```

`DefineSymbols.cs` adds/removes defines based on detection.

---

## Mode System

### Prototype Mode
- **Purpose**: Rapid UA testing with minimal setup
- **Required SDKs**: GameAnalytics, Facebook SDK
- **Optional**: AppLovin MAX (for ad testing)
- **Use case**: CPI tests, soft launches

### Full Mode
- **Purpose**: Production monetization
- **Required SDKs**: GameAnalytics, AppLovin MAX, Adjust
- **Not used**: Facebook SDK (Adjust handles attribution)
- **Use case**: Live games with ads

### Switching Modes

```csharp
// Editor/SorollaSettings.cs
public static void SetMode(SorollaMode mode) {
    // 1. Save to EditorPrefs
    // 2. Install required SDKs for new mode
    // 3. Uninstall mode-specific SDKs from old mode
    // 4. Update SorollaConfig.isPrototypeMode
}
```

---

## SDK Adapters Pattern

All adapters follow the same pattern:

```csharp
#if SDK_DEFINE
using ThirdPartySDK;
#endif

namespace Sorolla.Adapters
{
    public static class XxxAdapter
    {
        private static bool s_initialized;

        public static void Initialize(/* params */)
        {
            if (s_initialized) return;
#if SDK_DEFINE
            // Initialize third-party SDK
            ThirdPartySDK.Init();
            s_initialized = true;
#else
            Debug.LogWarning("[Sorolla] SDK not installed");
#endif
        }

        public static void DoSomething()
        {
#if SDK_DEFINE
            ThirdPartySDK.DoSomething();
#endif
        }
    }
}
```

---

## Firebase Integration

Firebase uses a special initialization pattern due to async dependency checking.

### FirebaseCoreManager.cs

Coordinates Firebase initialization to prevent race conditions:
```csharp
public static void EnsureInitialized(Action<bool> onComplete)
{
    // Check dependencies
    // Initialize FirebaseApp
    // Track initialization state
    // Queue callbacks if init in progress
}
```

### Event Queuing

`FirebaseAdapter.cs` queues events until Firebase is ready:
```csharp
private static Queue<Action> s_pendingEvents;

public static void TrackEvent(string name, params)
{
    if (!IsReady) {
        s_pendingEvents.Enqueue(() => TrackEvent(name, params));
        return;
    }
    // Actually send event
}
```

---

## Manifest Management

`ManifestManager.cs` directly modifies `Packages/manifest.json`:

```csharp
// Add scoped registry
AddOrUpdateRegistry("package.openupm.com", "https://package.openupm.com", scopes);

// Add dependencies
AddDependencies(new Dictionary<string, string> {
    ["com.gameanalytics.sdk"] = "7.10.6",
    ["com.google.external-dependency-manager"] = "1.2.186"
});

// Remove dependencies
RemoveDependencies(new[] { "com.facebook.sdk" });
```

**Why not Client.Add()?** Direct manifest modification is more reliable and allows batch operations.

---

## Common Tasks

### Adding a New SDK

1. Add entry to `SdkRegistry.cs`:
   ```csharp
   [SdkId.NewSdk] = new SdkInfo {
       Id = SdkId.NewSdk,
       Name = "New SDK",
       PackageId = "com.example.newsdk",
       Version = "1.0.0",
       Scope = "com.example",
       DetectionAssemblies = new[] { "NewSdk" },
       DetectionTypes = new[] { "NewSdk.Main, NewSdk" },
       Requirement = SdkRequirement.Optional
   }
   ```

2. Create adapter in `Runtime/Adapters/NewSdkAdapter.cs`

3. Add initialization call in `Sorolla.Initialize()`

4. Add detection define in `DefineSymbols.cs`

5. Add UI section in `SorollaWindow.cs` if needed

### Updating SDK Versions

1. Update version constant in `SdkRegistry.cs`:
   ```csharp
   public const string GA_VERSION = "7.11.0"; // was 7.10.6
   ```

2. Update CHANGELOG.md

3. Bump version in package.json

### Adding New API Method

1. Add public method to `Sorolla.cs`
2. Call `EnsureInit()` at start
3. Dispatch to relevant adapters
4. Add XML documentation

---

## Testing

### SorollaTestingTools.cs

Menu items under `Sorolla > Testing`:
- Reset setup state
- Clear manifest changes
- Show session state
- Force reinstall

### Manual Testing Checklist

- [ ] Fresh import installs dependencies
- [ ] Mode switching installs/uninstalls correct SDKs
- [ ] Configuration window shows correct status
- [ ] Runtime initialization works on iOS/Android
- [ ] ATT dialog appears on iOS
- [ ] Events dispatch to enabled backends
- [ ] Remote config fallback works

---

## Troubleshooting

### SDK Not Detected
1. Check `SdkRegistry.DetectionAssemblies` matches actual assembly name
2. Verify package is in `manifest.json`
3. Check Unity console for import errors

### Compilation Errors
1. Verify scripting defines are set correctly
2. Check `#if` guards in adapter files
3. Ensure assembly references are correct in .asmdef

### Events Not Sending
1. Check `Sorolla.IsInitialized` is true
2. Verify SDK credentials in config
3. Check device logs for SDK-specific errors

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.1.0 | 2025-12-01 | Firebase Suite (Analytics, Crashlytics, Remote Config) |
| 2.0.0 | 2025-11-25 | Renamed to com.sorolla.sdk, restructured adapters |
| 1.0.0 | 2025-11-10 | Initial release |

---

## Links

- **Repository**: https://github.com/LaCreArthur/sorolla-palette-upm
- **Changelog**: [CHANGELOG.md](../CHANGELOG.md)
- **Quick Start**: [QuickStart.md](QuickStart.md)
- **Firebase Setup**: [FirebaseSetup.md](FirebaseSetup.md)
