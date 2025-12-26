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
│   ├── Sorolla.Adapters.asmdef     ← Core stubs (no external refs)
│   ├── MaxAdapter.cs               ← Stub (delegates to impl)
│   ├── AdjustAdapter.cs            ← Stub (delegates to impl)
│   ├── FacebookAdapter.cs          ← Facebook (#if SOROLLA_FACEBOOK_ENABLED)
│   ├── FirebaseAdapter.cs          ← Stub (delegates to impl)
│   ├── FirebaseCrashlyticsAdapter.cs  ← Stub
│   ├── FirebaseRemoteConfigAdapter.cs ← Stub
│   ├── FirebaseCoreManager.cs      ← Stub
│   ├── Firebase/                   ← Separate assembly (defineConstraints)
│   │   ├── Sorolla.Adapters.Firebase.asmdef
│   │   ├── FirebaseAdapterImpl.cs
│   │   ├── FirebaseCoreManagerImpl.cs
│   │   ├── FirebaseCrashlyticsAdapterImpl.cs
│   │   └── FirebaseRemoteConfigAdapterImpl.cs
│   ├── MAX/                        ← Separate assembly (defineConstraints)
│   │   ├── Sorolla.Adapters.MAX.asmdef
│   │   └── MaxAdapterImpl.cs
│   └── Adjust/                     ← Separate assembly (defineConstraints)
│       ├── Sorolla.Adapters.Adjust.asmdef
│       └── AdjustAdapterImpl.cs
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

Optional SDK adapters use a **Stub + Implementation** pattern with separate assemblies:

### Why This Pattern?
Unity resolves assembly references **before** evaluating `#if` preprocessor blocks. This means `#if SDK_DEFINE` guards don't prevent "assembly not found" errors when the SDK isn't installed.

**Solution**: Use separate assembly definitions with direct SDK references. The assembly reference itself acts as the constraint - if the referenced SDK assembly doesn't exist, Unity silently skips compiling the dependent assembly.

> **Note**: We intentionally do NOT use `defineConstraints`. See devlog entry "2025-12-26: defineConstraints Removed" for why - they were unreliable and caused build failures.

### Structure

**Stub (always compiles)** - `Adapters/XxxAdapter.cs`:
```csharp
namespace Sorolla.Adapters
{
    internal interface IXxxAdapter
    {
        void Initialize();
        void DoSomething();
    }

    public static class XxxAdapter
    {
        private static IXxxAdapter s_impl;

        internal static void RegisterImpl(IXxxAdapter impl)
        {
            s_impl = impl;
        }

        public static void Initialize()
        {
            if (s_impl != null) s_impl.Initialize();
            else Debug.LogWarning("[Sorolla] SDK not installed");
        }

        public static void DoSomething() => s_impl?.DoSomething();
    }
}
```

**Implementation (separate assembly)** - `Adapters/Xxx/XxxAdapterImpl.cs`:
```csharp
using ThirdPartySDK;
using UnityEngine.Scripting;

namespace Sorolla.Adapters
{
    [Preserve]
    internal class XxxAdapterImpl : IXxxAdapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            XxxAdapter.RegisterImpl(new XxxAdapterImpl());
        }

        public void Initialize() { ThirdPartySDK.Init(); }
        public void DoSomething() { ThirdPartySDK.DoSomething(); }
    }
}
```

**Assembly Info** - `Adapters/Xxx/AssemblyInfo.cs`:
```csharp
using UnityEngine.Scripting;

// Force linker to process this assembly for RuntimeInitializeOnLoadMethod
[assembly: AlwaysLinkAssembly]
```

**Assembly Definition** - `Adapters/Xxx/Sorolla.Adapters.Xxx.asmdef`:
```json
{
    "name": "Sorolla.Adapters.Xxx",
    "references": ["ThirdPartySDK", "Sorolla.Adapters"],
    "defineConstraints": []
}
```

### How It Works
1. If SDK not installed → SDK assembly doesn't exist → Impl assembly skipped entirely by Unity
2. If SDK installed → Impl compiles → `AlwaysLinkAssembly` forces linker to process it
3. `[RuntimeInitializeOnLoadMethod]` auto-registers impl at runtime
4. Stub delegates to impl if registered, otherwise gracefully handles missing SDK

### IL2CPP Code Stripping Protection

For IL2CPP builds, we use a belt-and-suspenders approach:

| Mechanism | Purpose |
|-----------|---------|
| `[assembly: AlwaysLinkAssembly]` | Forces linker to process assembly (required for packages) |
| `[Preserve]` on class | Marks class as root, prevents stripping |
| `[Preserve]` on Register() | Marks method as root |
| `link.xml` (fallback) | Manual override if users have issues |

> **IMPORTANT**: Unity does NOT auto-include `link.xml` from UPM packages. The `AlwaysLinkAssembly` + `[Preserve]` approach is required for packages.

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
