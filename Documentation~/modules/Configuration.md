# Configuration Module - SorollaConfig & SDK Keys

> **RAG Query**: `configuration SorollaConfig MAX Adjust Firebase keys`
> **Token Budget**: ~400 tokens | **File**: `Runtime/SorollaConfig.cs`

## Purpose

Runtime configuration via ScriptableObject. Stores SDK keys, ad unit IDs, and feature toggles.

## SorollaConfig

```csharp
// Location: Assets/Resources/SorollaConfig.asset
[CreateAssetMenu(menuName = "SorollaSDK/Config")]
public class SorollaConfig : ScriptableObject {
    // Mode
    public bool isPrototypeMode = true;

    // AppLovin MAX
    public string maxSdkKey;
    public string maxRewardedAdUnitId;
    public string maxInterstitialAdUnitId;
    public string maxBannerAdUnitId;

    // Adjust
    public string adjustAppToken;
    public bool adjustSandboxMode = true;

    // Firebase Toggles
    public bool enableFirebaseAnalytics;
    public bool enableCrashlytics;
    public bool enableRemoteConfig;
}
```

## Configuration Checklist

### Prototype Mode
| Field | Required | Source |
|-------|----------|--------|
| GameAnalytics Keys | Yes | GA Dashboard |
| Facebook App ID | Yes | FB Developer Console |
| MAX SDK Key | No | AppLovin Dashboard |

### Full Mode
| Field | Required | Source |
|-------|----------|--------|
| GameAnalytics Keys | Yes | GA Dashboard |
| MAX SDK Key | Yes | AppLovin Dashboard |
| MAX Ad Unit IDs | Yes | AppLovin Dashboard |
| Adjust App Token | Yes | Adjust Dashboard |
| Firebase Config | No | Firebase Console |

## Firebase Config Files

| File | Platform | Location |
|------|----------|----------|
| `google-services.json` | Android | `Assets/` or `Assets/Plugins/Android/` |
| `GoogleService-Info.plist` | iOS | `Assets/` |

## Validation

```csharp
public bool IsValid() {
    if (isPrototypeMode) {
        return !string.IsNullOrEmpty(/* GA keys */);
    } else {
        return !string.IsNullOrEmpty(maxSdkKey)
            && !string.IsNullOrEmpty(adjustAppToken);
    }
}
```

## Editor Sync

`SorollaSettings.SetMode()` automatically updates `SorollaConfig.isPrototypeMode` when mode changes in Editor window.

## Access at Runtime

```csharp
// Loaded automatically by SorollaSDK.Initialize()
SorollaConfig config = Resources.Load<SorollaConfig>("SorollaConfig");

// Or via public property (after init)
SorollaConfig config = SorollaSDK.Config;
```

## Mobile Considerations

- Config loaded once at startup - no hot-reload
- Sandbox mode should be disabled for production builds
- Firebase config files must match bundle ID exactly

---
*Related: [Editor.md](Editor.md) for configuration window*
