# SorollaSDK - Public API Module

> **RAG Query**: `SorollaSDK public API analytics ads events`
> **Token Budget**: ~450 tokens | **File**: `Runtime/SorollaSDK.cs`

## Purpose

Main public API facade for Sorolla SDK. Static class providing unified access to analytics, ads, remote config, and crash reporting.

## Quick Reference

```csharp
using Sorolla;

// Check state
bool ready = SorollaSDK.IsInitialized;
bool consent = SorollaSDK.HasConsent;

// Analytics
SorollaSDK.TrackProgression(ProgressionStatus.Complete, "world1", "level5");
SorollaSDK.TrackDesign("ui:shop:opened", 1.5f);
SorollaSDK.TrackResource(ResourceFlowType.Sink, "gold", 100, "shop", "sword");

// Ads
if (SorollaSDK.IsRewardedAdReady) {
    SorollaSDK.ShowRewardedAd(
        onComplete: () => Debug.Log("Rewarded!"),
        onFailed: () => Debug.Log("Failed")
    );
}
SorollaSDK.ShowInterstitialAd(onComplete: () => {});

// Remote Config
SorollaSDK.FetchRemoteConfig(success => {
    int diff = SorollaSDK.GetRemoteConfigInt("difficulty", 1);
});

// Crashlytics
SorollaSDK.LogException(exception);
SorollaSDK.LogCrashlytics("Custom breadcrumb");
SorollaSDK.SetCrashlyticsKey("user_level", "5");
```

## API Table

| Method | Signature | Description | Notes |
|--------|-----------|-------------|-------|
| `TrackProgression` | `(status, prog1, prog2?, prog3?, score?)` | Level/world progress | GA + Firebase |
| `TrackDesign` | `(eventId, value?)` | Custom events | Colon-delimited ID |
| `TrackResource` | `(flow, currency, amount, type, id)` | Economy tracking | Source/Sink enum |
| `ShowRewardedAd` | `(onComplete, onFailed)` | Show MAX rewarded | Checks IsRewardedAdReady |
| `ShowInterstitialAd` | `(onComplete)` | Show MAX interstitial | Auto-reloads |
| `IsRemoteConfigReady` | `() → bool` | Check RC availability | Firebase or GA |
| `FetchRemoteConfig` | `(Action<bool>)` | Fetch latest values | Async operation |
| `GetRemoteConfig*` | `(key, default) → T` | Get string/int/float/bool | Fallback chain |
| `LogException` | `(Exception)` | Report to Crashlytics | If enabled |

## Events

```csharp
SorollaSDK.OnInitialized += () => Debug.Log("SDK ready");
```

## Dependencies

- **Inputs**: `SorollaConfig` from Resources
- **Outputs**: Routes to GA, MAX, Adjust, Firebase adapters
- **Requires**: Auto-initialized by `SorollaBootstrapper`

## Mobile Considerations

- ATT consent affects IDFA availability on iOS
- Rewarded ads may have load delays on slow networks
- Remote config fetch is async - cache locally for offline

---
*Related: [Adapters.md](Adapters.md) for SDK integration details*
