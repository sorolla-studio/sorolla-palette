# API Reference

Complete reference for the Sorolla SDK public API.

---

## Namespace

```csharp
using Sorolla.SDK;
```

---

## Initialization

The SDK initializes automatically via `SorollaBootstrapper`. No manual init required.

```csharp
// Check if SDK is ready
if (Palette.IsInitialized) { /* safe to call API */ }

// Check ATT consent status (iOS)
bool hasConsent = Palette.HasConsent;

// Get current config
SorollaConfig config = Palette.Config;

// Subscribe to init event
Palette.OnInitialized += () => Debug.Log("SDK ready");
```

---

## Analytics

### Level Tracking (Progression)

```csharp
Palette.TrackProgression(
    ProgressionStatus status,  // Start, Complete, Fail
    string progression01,       // e.g., "World_01" or "Level_001"
    string progression02 = null,// e.g., "Level_003"
    string progression03 = null,// e.g., "Stage_02"
    int score = 0               // Optional score
);
```

**Examples:**
```csharp
Palette.TrackProgression(ProgressionStatus.Start, "Level_001");
Palette.TrackProgression(ProgressionStatus.Complete, "Level_001", score: 1500);
Palette.TrackProgression(ProgressionStatus.Fail, "Level_001");

// With multiple progression levels
Palette.TrackProgression(ProgressionStatus.Complete, "World_01", "Level_05", "Boss");
```

### Custom Events (Design)

```csharp
Palette.TrackDesign(
    string eventId,     // Colon-delimited: "category:action:label"
    float value = 0     // Optional numeric value
);
```

**Examples:**
```csharp
Palette.TrackDesign("tutorial:completed");
Palette.TrackDesign("shop:purchase:sword", 100);
Palette.TrackDesign("ui:settings:sound_off");
Palette.TrackDesign("gameplay:powerup_used", 3);
```

### Economy Tracking (Resource)

```csharp
Palette.TrackResource(
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
Palette.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");
Palette.TrackResource(ResourceFlowType.Source, "gems", 10, "iap", "starter_pack");

// Player spent coins
Palette.TrackResource(ResourceFlowType.Sink, "coins", 50, "shop", "speed_boost");
Palette.TrackResource(ResourceFlowType.Sink, "gems", 100, "gacha", "premium_chest");
```

---

## Ads

### Rewarded Ads

```csharp
// Check if ad is loaded
bool ready = Palette.IsRewardedAdReady;

// Show rewarded ad
Palette.ShowRewardedAd(
    Action onComplete,  // Called when user earns reward
    Action onFailed     // Called if ad fails/skipped
);
```

**Example:**
```csharp
public void OnWatchAdClicked()
{
    if (Palette.IsRewardedAdReady)
    {
        Palette.ShowRewardedAd(
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
Palette.ShowInterstitialAd(
    Action onComplete  // Called when ad closes
);
```

**Example:**
```csharp
void OnLevelComplete()
{
    if (levelsCompleted % 3 == 0)  // Every 3 levels
    {
        Palette.ShowInterstitialAd(onComplete: ShowNextLevel);
    }
}
```

---

## Remote Config

Unified API supporting Firebase (primary) and GameAnalytics (fallback).

### Fetch Values

```csharp
// Check if remote config is available
bool ready = Palette.IsRemoteConfigReady();

// Fetch latest values from server
Palette.FetchRemoteConfig(Action<bool> onComplete);
```

### Get Values

```csharp
string Palette.GetRemoteConfig(string key, string defaultValue);
int Palette.GetRemoteConfigInt(string key, int defaultValue);
float Palette.GetRemoteConfigFloat(string key, float defaultValue);
bool Palette.GetRemoteConfigBool(string key, bool defaultValue);
```

**Example:**
```csharp
void Start()
{
    Palette.FetchRemoteConfig(success =>
    {
        if (success)
        {
            playerSpeed = Palette.GetRemoteConfigFloat("player_speed", 5.0f);
            enableXmasEvent = Palette.GetRemoteConfigBool("xmas_event", false);
            dailyReward = Palette.GetRemoteConfigInt("daily_reward", 100);
            welcomeMsg = Palette.GetRemoteConfig("welcome_message", "Hello!");
        }
    });
}
```

---

## Crashlytics

Available when Firebase Crashlytics is enabled.

```csharp
// Log non-fatal exception
Palette.LogException(Exception exception);

// Add breadcrumb log
Palette.LogCrashlytics(string message);

// Set custom key-value
Palette.SetCrashlyticsKey(string key, string value);
Palette.SetCrashlyticsKey(string key, int value);
Palette.SetCrashlyticsKey(string key, float value);
Palette.SetCrashlyticsKey(string key, bool value);
```

**Example:**
```csharp
void OnUserLogin(string playerId)
{
    Palette.SetCrashlyticsKey("player_id", playerId);
    Palette.LogCrashlytics($"User logged in: {playerId}");
}

void RiskyOperation()
{
    try
    {
        // risky code
    }
    catch (Exception ex)
    {
        Palette.LogException(ex);
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
