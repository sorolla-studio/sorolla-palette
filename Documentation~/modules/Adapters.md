# Adapters Module - Third-Party SDK Integration

> **RAG Query**: `adapters MAX Adjust Firebase GameAnalytics Facebook`
> **Token Budget**: ~500 tokens | **Path**: `Runtime/Adapters/`

## Purpose

Static adapter classes wrapping third-party SDKs with conditional compilation. Each adapter handles initialization, event forwarding, and error handling.

## Adapter Overview

| Adapter | Symbol | Mode | Purpose |
|---------|--------|------|---------|
| `GameAnalyticsAdapter` | `GAMEANALYTICS_INSTALLED` | Both | Core analytics |
| `MaxAdapter` | `SOROLLA_MAX_ENABLED` | Both* | Ad mediation |
| `AdjustAdapter` | `SOROLLA_ADJUST_ENABLED` | Full | Attribution |
| `FacebookAdapter` | `SOROLLA_FACEBOOK_ENABLED` | Proto | FB analytics |
| `FirebaseAdapter` | `FIREBASE_ANALYTICS_INSTALLED` | Optional | Analytics |
| `FirebaseCrashlyticsAdapter` | `FIREBASE_CRASHLYTICS_INSTALLED` | Optional | Crash reports |
| `FirebaseRemoteConfigAdapter` | `FIREBASE_REMOTE_CONFIG_INSTALLED` | Optional | A/B testing |

*MAX optional in Prototype, required in Full

## GameAnalyticsAdapter

```csharp
// File: Runtime/GameAnalyticsAdapter.cs (94 LOC)
public static class GameAnalyticsAdapter {
    public static void Initialize();
    public static void TrackProgressionEvent(status, prog1, prog2, prog3, score);
    public static void TrackDesignEvent(eventId, value);
    public static void TrackResourceEvent(flow, currency, amount, type, id);
}
```
**Note**: Always enabled. Uses `GameAnalytics.NewProgressionEvent()` etc.

## MaxAdapter

```csharp
// File: Runtime/Adapters/MaxAdapter.cs (227 LOC)
public static class MaxAdapter {
    public static bool IsRewardedAdReady { get; }
    public static void Initialize();
    public static void ShowRewardedAd(onComplete, onFailed);
    public static void ShowInterstitialAd(onComplete);
}
```
**Flow**: OnAdRevenuePaidEvent → `AdjustAdapter.TrackAdRevenue()`

## AdjustAdapter

```csharp
// File: Runtime/Adapters/AdjustAdapter.cs (114 LOC)
public static class AdjustAdapter {
    public static void Initialize();
    public static void TrackAdRevenue(source, revenue, currency, adUnitId, ...);
}
```
**Note**: Triggered automatically by MAX ad revenue events.

## FirebaseRemoteConfigAdapter

```csharp
// File: Runtime/Adapters/FirebaseRemoteConfigAdapter.cs (290 LOC)
public static class FirebaseRemoteConfigAdapter {
    public static bool IsReady { get; }
    public static void Initialize();
    public static void FetchAndActivate(Action<bool> onComplete);
    public static string GetString(key, defaultValue);
    public static int GetInt(key, defaultValue);
    // ... float, bool variants
}
```
**Fallback**: If Firebase disabled, `SorollaSDK.GetRemoteConfig*` uses GA instead.

## Initialization Order

```
SorollaSDK.Initialize()
├─ GameAnalyticsAdapter.Initialize()      // Always first
├─ [Prototype] FacebookAdapter.Initialize()
├─ [Full] MaxAdapter.Initialize()
│     └─ OnSdkInitialized → AdjustAdapter.Initialize()
├─ FirebaseCoreManager.EnsureInitialized()
├─ FirebaseAdapter.Initialize()
├─ FirebaseCrashlyticsAdapter.Initialize()
└─ FirebaseRemoteConfigAdapter.Initialize()
```

## Mobile Considerations

- Adapters use stub implementations when SDK not installed
- Network errors handled with callbacks, not exceptions
- Ad loading is async - UI should show loading overlay

---
*Related: [Configuration.md](Configuration.md) for SDK key setup*
