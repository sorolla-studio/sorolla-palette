# Architecture Guide

Technical reference for contributors and AI agents working on the Sorolla SDK.

> **AI Agents**: For quick orientation, skip to [Quick Reference](#quick-reference) at the end.

---

## Overview

**Sorolla SDK** is a plug-and-play mobile publisher SDK for Unity. It wraps GameAnalytics, Facebook, AppLovin MAX, Adjust, and Firebase behind a unified API.

### Design Principles

1. **Zero-Configuration** - Auto-initializes at runtime
2. **Conditional Compilation** - Adapters only compile when SDK installed
3. **Mode-Based** - Prototype/Full modes determine required SDKs
4. **Single Source of Truth** - `SdkRegistry.cs` contains all SDK metadata

---

## Package Structure

```
Runtime/
├── SorollaSDK.cs              ← Main public API (static class)
├── SorollaBootstrapper.cs     ← Auto-init via [RuntimeInitializeOnLoadMethod]
├── SorollaConfig.cs           ← ScriptableObject configuration
├── SorollaLoadingOverlay.cs   ← Ad loading UI helper
├── GameAnalyticsAdapter.cs    ← GA wrapper (always included)
├── Adapters/
│   ├── MaxAdapter.cs          ← AppLovin MAX (#if SOROLLA_MAX_ENABLED)
│   ├── AdjustAdapter.cs       ← Adjust (#if SOROLLA_ADJUST_ENABLED)
│   ├── FacebookAdapter.cs     ← Facebook (#if SOROLLA_FACEBOOK_ENABLED)
│   ├── FirebaseAdapter.cs     ← Firebase Analytics
│   ├── FirebaseCrashlyticsAdapter.cs
│   ├── FirebaseRemoteConfigAdapter.cs
│   └── FirebaseCoreManager.cs ← Firebase init coordinator
└── ATT/
    ├── ContextScreenView.cs   ← Pre-ATT explanation screen
    └── FakeATTDialog.cs       ← Editor testing

Editor/
├── SorollaWindow.cs           ← Main configuration window
├── SorollaSettings.cs         ← Mode persistence (EditorPrefs)
├── SorollaMode.cs             ← Enum: None, Prototype, Full
├── SorollaIOSPostProcessor.cs ← Xcode post-processing
├── ManifestManager.cs         ← manifest.json manipulation
└── Sdk/
    ├── SdkRegistry.cs         ← SDK metadata & versions
    ├── SdkDetector.cs         ← Check if SDK installed
    ├── SdkInstaller.cs        ← Install/uninstall SDKs
    └── DefineSymbols.cs       ← Scripting define management
```

---

## Initialization Flow

```
[RuntimeInitializeOnLoadMethod]
    ↓
SorollaBootstrapper.AutoInit()
    ↓
Creates "[Sorolla SDK]" GameObject (DontDestroyOnLoad)
    ↓
iOS: Check ATT status → Show ContextScreen if needed
    ↓
SorollaSDK.Initialize(consent)
    ├── GameAnalyticsAdapter.Initialize()      ← Always
    ├── FacebookAdapter.Initialize()           ← Prototype only
    ├── MaxAdapter.Initialize()                ← If configured
    │   └── OnSdkInitialized → AdjustAdapter.Initialize()
    ├── FirebaseAdapter.Initialize()           ← If enabled
    ├── FirebaseCrashlyticsAdapter.Initialize()
    └── FirebaseRemoteConfigAdapter.Initialize()
    ↓
IsInitialized = true
```

---

## Adapter Pattern

All adapters follow this structure:

```csharp
#if SDK_DEFINE
using ThirdPartySDK;
#endif

namespace Sorolla.Adapters
{
    public static class XxxAdapter
    {
        private static bool s_initialized;

        public static void Initialize()
        {
            if (s_initialized) return;
#if SDK_DEFINE
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

---

## Mode System

### Prototype Mode
- **Required**: GameAnalytics, Facebook SDK
- **Optional**: AppLovin MAX
- **Use case**: CPI tests, soft launches

### Full Mode
- **Required**: GameAnalytics, AppLovin MAX, Adjust
- **Not used**: Facebook SDK (Adjust handles attribution)
- **Use case**: Production with monetization

### Switching Modes

```csharp
// Editor/SorollaSettings.cs
public static void SetMode(SorollaMode mode) {
    // 1. Save to EditorPrefs
    // 2. Install required SDKs
    // 3. Uninstall mode-specific SDKs
    // 4. Update SorollaConfig.isPrototypeMode
    // 5. Apply scripting defines
}
```

---

## SDK Registry

Single source of truth for all SDK information:

```csharp
// Editor/Sdk/SdkRegistry.cs
public enum SdkId {
    GameAnalytics, Facebook, AppLovinMAX, Adjust,
    FirebaseApp, FirebaseAnalytics, FirebaseCrashlytics, FirebaseRemoteConfig
}

public enum SdkRequirement { Core, PrototypeOnly, FullOnly, Optional }

// Version constants
public const string GA_VERSION = "7.10.6";
public const string MAX_VERSION = "8.5.0";
public const string FIREBASE_VERSION = "12.10.1";
```

---

## Data Flow

### Event Tracking
```
SorollaSDK.TrackDesign("event:name")
    ├── GameAnalyticsAdapter.TrackDesignEvent()  ← Always
    ├── FirebaseAdapter.TrackDesignEvent()       ← If enabled
    └── FacebookAdapter.TrackEvent()             ← Prototype only
```

### Ad Revenue
```
SorollaSDK.ShowRewardedAd()
    └── MaxAdapter.ShowRewardedAd()
        └── OnAdRevenuePaidEvent
            └── AdjustAdapter.TrackAdRevenue()
```

### Remote Config
```
SorollaSDK.GetRemoteConfig(key, default)
    ├── Firebase ready? → return Firebase value
    ├── GA ready? → return GA value
    └── else → return default
```

---

## Common Tasks

### Adding a New SDK

1. Add entry to `SdkRegistry.cs`
2. Create adapter in `Runtime/Adapters/`
3. Add initialization call in `SorollaSDK.Initialize()`
4. Add detection define in `DefineSymbols.cs`
5. Add UI section in `SorollaWindow.cs`

### Updating SDK Versions

1. Update version in `SdkRegistry.cs`
2. Update `CHANGELOG.md`
3. Bump version in `package.json`

### Adding New API Method

1. Add public method to `SorollaSDK.cs`
2. Call relevant adapters
3. Add XML documentation

---

## Quick Reference

**For AI agents working on this codebase:**

### Key Files
| File | Purpose | LOC |
|------|---------|-----|
| `Runtime/SorollaSDK.cs` | Main public API | 484 |
| `Runtime/SorollaBootstrapper.cs` | Auto-initialization | 140 |
| `Runtime/Adapters/MaxAdapter.cs` | Ad mediation | 227 |
| `Editor/SorollaWindow.cs` | Configuration UI | 598 |
| `Editor/Sdk/SdkRegistry.cs` | SDK metadata | 223 |

### Namespaces
| Namespace | Purpose |
|-----------|---------|
| `Sorolla` | Public API |
| `Sorolla.Adapters` | SDK wrappers |
| `Sorolla.ATT` | iOS privacy |
| `Sorolla.Editor` | Editor tools |

### Critical Paths
```
Init:   Bootstrapper → ATT → SorollaSDK.Initialize()
Events: SorollaSDK → Adapters → Third-party SDKs
Ads:    ShowRewardedAd → MaxAdapter → AdjustAdapter.TrackAdRevenue
Config: GetRemoteConfig → Firebase → GA → default
```

### Codebase Stats
- **Total**: 59 scripts, ~3,900 LOC
- **Runtime**: 15 scripts, ~2,100 LOC
- **Editor**: 13 scripts, ~1,800 LOC
- **Debug UI**: 31 scripts (sample)

### RAG Query Patterns
```
"public API methods"         → SorollaSDK.cs
"ad revenue tracking"        → MaxAdapter.cs, AdjustAdapter.cs
"SDK installation"           → SdkInstaller.cs, ManifestManager.cs
"ATT iOS consent"            → ContextScreenView.cs
"configuration window"       → SorollaWindow.cs
"remote config fallback"     → FirebaseRemoteConfigAdapter.cs
```
