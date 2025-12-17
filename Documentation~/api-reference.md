# API Reference

Complete reference for the Sorolla SDK public API.

---

## Namespace

```csharp
using Sorolla;
```

---

## Initialization

The SDK initializes automatically via `SorollaBootstrapper`. No manual init required.

```csharp
// Check if SDK is ready
if (SorollaSDK.IsInitialized) { /* safe to call API */ }

// Check ATT consent status (iOS)
bool hasConsent = SorollaSDK.HasConsent;

// Get current config
SorollaConfig config = SorollaSDK.Config;

// Subscribe to init event
SorollaSDK.OnInitialized += () => Debug.Log("SDK ready");
```

---

## Analytics

### Level Tracking (Progression)

```csharp
SorollaSDK.TrackProgression(
    ProgressionStatus status,  // Start, Complete, Fail
    string progression01,       // e.g., "World_01" or "Level_001"
    string progression02 = null,// e.g., "Level_003"
    string progression03 = null,// e.g., "Stage_02"
    int score = 0               // Optional score
);
```

**Examples:**
```csharp
SorollaSDK.TrackProgression(ProgressionStatus.Start, "Level_001");
SorollaSDK.TrackProgression(ProgressionStatus.Complete, "Level_001", score: 1500);
SorollaSDK.TrackProgression(ProgressionStatus.Fail, "Level_001");

// With multiple progression levels
SorollaSDK.TrackProgression(ProgressionStatus.Complete, "World_01", "Level_05", "Boss");
```

### Custom Events (Design)

```csharp
SorollaSDK.TrackDesign(
    string eventId,     // Colon-delimited: "category:action:label"
    float value = 0     // Optional numeric value
);
```

**Examples:**
```csharp
SorollaSDK.TrackDesign("tutorial:completed");
SorollaSDK.TrackDesign("shop:purchase:sword", 100);
SorollaSDK.TrackDesign("ui:settings:sound_off");
SorollaSDK.TrackDesign("gameplay:powerup_used", 3);
```

### Economy Tracking (Resource)

```csharp
SorollaSDK.TrackResource(
    ResourceFlowType flowType, // Source (earned) or Sink (spent)
    string currency,           // e.g., "coins", "gems"
    float amount,              // Amount earned/spent
    string itemType,           // Category: "reward", "shop", "iap"
    string itemId              // Specific item: "level_complete", "blue_hat"
);
```

**Examples:**
```csharp
// Player earned coins
SorollaSDK.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");
SorollaSDK.TrackResource(ResourceFlowType.Source, "gems", 10, "iap", "starter_pack");

// Player spent coins
SorollaSDK.TrackResource(ResourceFlowType.Sink, "coins", 50, "shop", "speed_boost");
SorollaSDK.TrackResource(ResourceFlowType.Sink, "gems", 100, "gacha", "premium_chest");
```

---

## Ads

### Rewarded Ads

```csharp
// Check if ad is loaded
bool ready = SorollaSDK.IsRewardedAdReady;

// Show rewarded ad
SorollaSDK.ShowRewardedAd(
    Action onComplete,  // Called when user earns reward
    Action onFailed     // Called if ad fails/skipped
);
```

**Example:**
```csharp
public void OnWatchAdClicked()
{
    if (SorollaSDK.IsRewardedAdReady)
    {
        SorollaSDK.ShowRewardedAd(
            onComplete: () => {
                coins += 100;
                UpdateUI();
            },
            onFailed: () => {
                ShowMessage("Ad not available");
            }
        );
    }
}
```

### Interstitial Ads

```csharp
SorollaSDK.ShowInterstitialAd(
    Action onComplete  // Called when ad closes
);
```

**Example:**
```csharp
void OnLevelComplete()
{
    if (levelsCompleted % 3 == 0)  // Every 3 levels
    {
        SorollaSDK.ShowInterstitialAd(onComplete: ShowNextLevel);
    }
}
```

---

## Remote Config

Unified API supporting Firebase (primary) and GameAnalytics (fallback).

### Fetch Values

```csharp
// Check if remote config is available
bool ready = SorollaSDK.IsRemoteConfigReady();

// Fetch latest values from server
SorollaSDK.FetchRemoteConfig(Action<bool> onComplete);
```

### Get Values

```csharp
string SorollaSDK.GetRemoteConfig(string key, string defaultValue);
int SorollaSDK.GetRemoteConfigInt(string key, int defaultValue);
float SorollaSDK.GetRemoteConfigFloat(string key, float defaultValue);
bool SorollaSDK.GetRemoteConfigBool(string key, bool defaultValue);
```

**Example:**
```csharp
void Start()
{
    SorollaSDK.FetchRemoteConfig(success =>
    {
        if (success)
        {
            playerSpeed = SorollaSDK.GetRemoteConfigFloat("player_speed", 5.0f);
            enableXmasEvent = SorollaSDK.GetRemoteConfigBool("xmas_event", false);
            dailyReward = SorollaSDK.GetRemoteConfigInt("daily_reward", 100);
            welcomeMsg = SorollaSDK.GetRemoteConfig("welcome_message", "Hello!");
        }
    });
}
```

---

## Crashlytics

Available when Firebase Crashlytics is enabled.

```csharp
// Log non-fatal exception
SorollaSDK.LogException(Exception exception);

// Add breadcrumb log
SorollaSDK.LogCrashlytics(string message);

// Set custom key-value
SorollaSDK.SetCrashlyticsKey(string key, string value);
SorollaSDK.SetCrashlyticsKey(string key, int value);
SorollaSDK.SetCrashlyticsKey(string key, float value);
SorollaSDK.SetCrashlyticsKey(string key, bool value);
```

**Example:**
```csharp
void OnUserLogin(string playerId)
{
    SorollaSDK.SetCrashlyticsKey("player_id", playerId);
    SorollaSDK.LogCrashlytics($"User logged in: {playerId}");
}

void RiskyOperation()
{
    try
    {
        // risky code
    }
    catch (Exception ex)
    {
        SorollaSDK.LogException(ex);
        // handle gracefully
    }
}
```

---

## Enums

```csharp
public enum ProgressionStatus
{
    Start,
    Complete,
    Fail
}

public enum ResourceFlowType
{
    Source,  // Player earned/received
    Sink     // Player spent/used
}
```

---

## Events Dispatching

All analytics methods dispatch to multiple backends:

| Method | GameAnalytics | Firebase | Facebook |
|--------|--------------|----------|----------|
| `TrackProgression` | Always | If enabled | Prototype only |
| `TrackDesign` | Always | If enabled | Prototype only |
| `TrackResource` | Always | If enabled | Prototype only |
| `ShowRewardedAd` | Revenue via Adjust | - | - |

---

## Best Practices

1. **Level names**: Zero-pad numbers (`Level_001` not `Level_1`)
2. **Event IDs**: Use colon-delimited format (`category:action:label`)
3. **Resource tracking**: Track all currency flows for economy analysis
4. **Remote config**: Always provide sensible defaults
5. **Ad timing**: Check `IsRewardedAdReady` before showing button
6. **Error handling**: Use try-catch with `LogException` for critical code
