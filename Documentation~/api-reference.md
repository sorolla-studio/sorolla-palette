# API Reference

Complete reference for the Sorolla SDK public API.

---

## Namespace

```csharp
using Sorolla.Palette;
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

### Custom Events

The primary way to track game-specific events. Firebase receives full structured parameters; GameAnalytics receives a best-effort design event.

```csharp
Palette.TrackEvent(
    string eventName,                          // GA4 name: snake_case, max 40 chars
    Dictionary<string, object> parameters = null // Structured data (string, int, long, float, double, bool, enum)
);
```

**Examples:**
```csharp
// Player used a booster
Palette.TrackEvent("booster_used", new Dictionary<string, object>
{
    { "booster_id", "speed_2x" },
    { "level", 12 },
    { "game_mode", "classic" }
});

// Player posted a score
Palette.TrackEvent("post_score", new Dictionary<string, object>
{
    { "score", 1200 },
    { "level_name", "world1_level3" }
});

// Simple event, no params
Palette.TrackEvent("tutorial_complete");
```

**Naming rules:**
- Use `snake_case` (letters, digits, underscores)
- Max 40 characters
- No reserved prefixes: `firebase_`, `google_`, `ga_`
- Max 25 parameters per event
- Use [GA4 recommended event names](https://support.google.com/analytics/answer/9267735) where possible

> **Deprecated:** `Palette.TrackDesign(string, float)` still works but use `TrackEvent()` for new code.

### Level Tracking (Progression)

```csharp
Palette.TrackProgression(
    ProgressionStatus status,                    // Start, Complete, Fail
    string progression01,                         // e.g., "World_01" or "Level_001"
    string progression02 = null,                  // e.g., "Level_003"
    string progression03 = null,                  // e.g., "Stage_02"
    int score = 0,                                // Optional score
    Dictionary<string, object> extraParams = null  // Optional: extra context for Firebase
);
```

Firebase mapping: Start -> `level_start`, Complete -> `level_end`, Fail -> `level_fail`.

**Examples:**
```csharp
Palette.TrackProgression(ProgressionStatus.Start, "Level_001");
Palette.TrackProgression(ProgressionStatus.Complete, "Level_001", score: 1500);
Palette.TrackProgression(ProgressionStatus.Fail, "Level_001");

// With extra context for Firebase dashboards
Palette.TrackProgression(ProgressionStatus.Complete, "World_01", "Level_05", "Boss",
    score: 3200,
    extraParams: new Dictionary<string, object>
    {
        { "game_mode", "hard" },
        { "duration_sec", 92 }
    });
```

### Economy Tracking (Resource)

```csharp
Palette.TrackResource(
    ResourceFlowType flowType,                    // Source (earned) or Sink (spent)
    string currency,                               // e.g., "coins", "gems"
    float amount,                                  // Amount earned/spent
    string itemType,                               // Category: "reward", "shop", "iap"
    string itemId,                                 // Specific item: "level_complete", "blue_hat"
    Dictionary<string, object> extraParams = null   // Optional: extra context for Firebase
);
```

Firebase mapping: Source -> `earn_virtual_currency`, Sink -> `spend_virtual_currency`.

**Examples:**
```csharp
// Player earned coins
Palette.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");

// Player spent coins, with extra Firebase context
Palette.TrackResource(ResourceFlowType.Sink, "coins", 50, "shop", "speed_boost",
    extraParams: new Dictionary<string, object>
    {
        { "level", 12 },
        { "first_purchase", true }
    });
```

### User Identity

```csharp
// Set user ID across Firebase Analytics, Crashlytics, and Adjust
Palette.SetUserId(string userId);    // Pass null to clear

// Set user property for Firebase audience segmentation
Palette.SetUserProperty(string name, string value);
```

**Example:**
```csharp
Palette.SetUserId("player_abc123");
Palette.SetUserProperty("subscription_tier", "premium");
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

Unified API: checks Firebase first, then GameAnalytics, then in-app defaults.

### Set Defaults

```csharp
// Set fallback values used before Firebase loads (or when offline)
Palette.SetRemoteConfigDefaults(Dictionary<string, object> defaults);
```

### Fetch & Read Values

```csharp
bool ready = Palette.IsRemoteConfigReady();
Palette.FetchRemoteConfig(Action<bool> onComplete);

string Palette.GetRemoteConfig(string key, string defaultValue);
int    Palette.GetRemoteConfigInt(string key, int defaultValue);
float  Palette.GetRemoteConfigFloat(string key, float defaultValue);
bool   Palette.GetRemoteConfigBool(string key, bool defaultValue);
```

**Example:**
```csharp
void Start()
{
    // Set in-app defaults first
    Palette.SetRemoteConfigDefaults(new Dictionary<string, object>
    {
        { "daily_reward", 50 },
        { "xmas_event", false },
        { "welcome_message", "Hello!" }
    });

    Palette.FetchRemoteConfig(success =>
    {
        int reward = Palette.GetRemoteConfigInt("daily_reward", 50);
        bool xmas = Palette.GetRemoteConfigBool("xmas_event", false);
        string msg = Palette.GetRemoteConfig("welcome_message", "Hello!");
    });
}
```

### Real-Time Updates

Config changes pushed from Firebase Console arrive instantly without app restart.

```csharp
// Auto-activate (default: true) - values apply immediately
Palette.AutoActivateRemoteConfigUpdates = true;

// Listen for changes
Palette.OnRemoteConfigUpdated += (IReadOnlyCollection<string> changedKeys) =>
{
    if (changedKeys.Contains("sale_banner"))
        UpdateBannerUI();
};

// Manual activation (for games where mid-session changes would be jarring)
Palette.AutoActivateRemoteConfigUpdates = false;
// Values arrive but don't apply until:
await Palette.ActivateRemoteConfigAsync();
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

| Method | GameAnalytics | Firebase | Facebook | Adjust | TikTok |
|--------|--------------|----------|----------|--------|--------|
| `TrackEvent` | Best-effort design event | Full structured params | - | - | - |
| `TrackProgression` | Always | If enabled (+extraParams) | - | - | - |
| `TrackResource` | Always | If enabled (+extraParams) | - | - | - |
| `TrackPurchase` | - | If enabled | - | If configured | If enabled |
| `TrackDesign` *(deprecated)* | Always | If enabled | - | - | - |

---

## Best Practices

1. **Event names**: Use `snake_case`, max 40 chars (`tutorial_complete`, `booster_used`)
2. **Level names**: Zero-pad numbers (`Level_001` not `Level_1`)
3. **Resource tracking**: Track all currency flows for economy analysis
4. **Remote config**: Set in-app defaults via `SetRemoteConfigDefaults()`
5. **Ad timing**: Check `IsRewardedAdReady` before showing button
6. **Error handling**: Use try-catch with `LogException` for critical code
7. **GA4 recommended events**: Use [standard names](https://support.google.com/analytics/answer/9267735) where possible
