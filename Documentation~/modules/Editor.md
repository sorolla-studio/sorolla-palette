# Editor Module - Configuration & SDK Management

> **RAG Query**: `editor window SDK installation configuration mode`
> **Token Budget**: ~450 tokens | **Path**: `Editor/`

## Purpose

Unity Editor tools for SDK configuration, mode selection, and automated package management. Provides zero-config setup experience.

## Components

| File | LOC | Purpose |
|------|-----|---------|
| `SorollaWindow.cs` | 598 | Main configuration EditorWindow |
| `SorollaSettings.cs` | 99 | Mode persistence & runtime config sync |
| `SorollaMode.cs` | 15 | Mode enum definition |
| `SorollaIOSPostProcessor.cs` | 43 | iOS build post-processor |
| `ManifestManager.cs` | 80 | Package.json manipulation |
| `Sdk/SdkRegistry.cs` | 223 | SDK metadata & versions |
| `Sdk/SdkDetector.cs` | 62 | Assembly-based SDK detection |
| `Sdk/SdkInstaller.cs` | ~100 | Manifest-based installation |
| `Sdk/SdkConfigDetector.cs` | ~80 | Config validation |
| `Sdk/DefineSymbols.cs` | 63 | Scripting define management |

## SorollaWindow

**Menu**: `SorollaSDK > Configuration`

**Sections**:
1. **Mode Selection** - Prototype/Full toggle
2. **Setup Checklist** - SDK status indicators
3. **SDK Keys** - MAX, Adjust, Firebase configuration
4. **Firebase Toggles** - Optional modules

```csharp
// Open programmatically
[MenuItem("SorollaSDK/Configuration")]
public static void Open() => GetWindow<SorollaWindow>();
```

## Mode System

```csharp
public enum SorollaMode { None, Prototype, Full }

// SorollaSettings.cs
public static void SetMode(SorollaMode mode) {
    // 1. Update EditorPrefs
    // 2. Update SorollaConfig.isPrototypeMode
    // 3. DefineSymbols.Apply() → SOROLLA_PROTOTYPE or SOROLLA_FULL
    // 4. SdkInstaller.InstallRequiredSdks()
    // 5. SdkInstaller.UninstallUnnecessarySdks()
}
```

## SDK Registry

Centralized metadata for all managed SDKs:

```csharp
// SdkRegistry.cs
public static SdkInfo Get(SdkId id) {
    // Returns version, package name, detection type, requirement
}

public enum SdkId {
    ExternalDependencyManager, GameAnalytics, IosSupport,
    Facebook, AppLovinMAX, Adjust,
    FirebaseApp, FirebaseAnalytics, FirebaseCrashlytics, FirebaseRemoteConfig
}

public enum SdkRequirement {
    Core,           // Always installed
    PrototypeOnly,  // Facebook
    FullOnly,       // Adjust
    FullRequired,   // MAX (optional in Proto, required in Full)
    Optional        // Firebase modules
}
```

## Installation Flow

```
User clicks "Switch to Full Mode"
    ↓
SorollaSettings.SetMode(Full)
    ├─ DefineSymbols.Apply(isPrototype: false)
    │     └─ Remove SOROLLA_PROTOTYPE, Add SOROLLA_FULL
    ├─ SdkInstaller.InstallRequiredSdks(isPrototype: false)
    │     └─ ManifestManager.AddDependencies(MAX, Adjust)
    ├─ SdkInstaller.UninstallUnnecessarySdks(isPrototype: false)
    │     └─ ManifestManager.RemoveDependencies(Facebook)
    └─ AssetDatabase.Refresh()
```

## Mobile Considerations

- Mode changes require Unity restart for full effect
- Firebase config files must exist before installation
- iOS post-processor runs automatically on build

---
*Related: [Configuration.md](Configuration.md) for SorollaConfig details*
