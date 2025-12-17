# Getting Started

Get your game integrated with Sorolla SDK in under 10 minutes.

---

## 1. Install the Package

**Unity Package Manager** → `+` → **Add package from git URL**:
```
https://github.com/LaCreArthur/sorolla-palette-upm.git
```

The Configuration window opens automatically.

---

## 2. Choose Your Mode

| | Prototype Mode | Full Mode |
|--|---------------|-----------|
| **Purpose** | Rapid UA testing | Production launch |
| **Analytics** | GameAnalytics | GameAnalytics |
| **Attribution** | Facebook SDK | Adjust |
| **Ads** | MAX (optional) | MAX (required) |
| **When to use** | CPI tests, soft launch | Live game |

**Click** `Prototype Mode` or `Full Mode` in the Configuration window.

Then follow the setup guide:
- [Prototype Mode Setup](prototype-setup.md)
- [Full Mode Setup](full-setup.md)

---

## 3. Track Your First Event

The SDK initializes automatically. Start tracking immediately:

```csharp
using Sorolla;

// Level tracking (critical for analytics)
SorollaSDK.TrackProgression(ProgressionStatus.Start, "Level_001");
SorollaSDK.TrackProgression(ProgressionStatus.Complete, "Level_001", score: 1500);
SorollaSDK.TrackProgression(ProgressionStatus.Fail, "Level_001");

// Custom events
SorollaSDK.TrackDesign("tutorial:completed");
SorollaSDK.TrackDesign("shop:opened", 1);

// Economy tracking
SorollaSDK.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");
SorollaSDK.TrackResource(ResourceFlowType.Sink, "coins", 50, "shop", "speed_boost");
```

> **Important**: Zero-pad level numbers (`Level_001` not `Level_1`) for correct dashboard sorting.

---

## 4. Show Ads (Optional)

```csharp
// Rewarded ad
if (SorollaSDK.IsRewardedAdReady)
{
    SorollaSDK.ShowRewardedAd(
        onComplete: () => GiveReward(),
        onFailed: () => Debug.Log("Ad not ready")
    );
}

// Interstitial ad
SorollaSDK.ShowInterstitialAd(onComplete: () => ContinueGame());
```

---

## 5. Remote Config (A/B Testing)

Tune values without app updates:

```csharp
SorollaSDK.FetchRemoteConfig(success =>
{
    float speed = SorollaSDK.GetRemoteConfigFloat("player_speed", 5.5f);
    bool feature = SorollaSDK.GetRemoteConfigBool("xmas_event", false);
    int reward = SorollaSDK.GetRemoteConfigInt("daily_reward", 100);
});
```

Remote Config uses Firebase (if enabled), then GameAnalytics as fallback.

---

## 6. Verify Integration

### Debug UI (On-Device Testing)

1. Import the Debug UI sample from Package Manager
2. Build to device
3. **Triple-tap** screen (mobile) or press **BackQuote** key (desktop)

The debug panel shows:
- SDK initialization status
- Ad loading/showing
- Event tracking
- ATT/consent status

---

## Pre-Launch Checklist

- [ ] Level tracking: Every `Start` has matching `Complete` or `Fail`
- [ ] Level names are zero-padded (`Level_001`)
- [ ] Tutorial steps tracked (`tutorial:step_01`, `step_02`...)
- [ ] SDK keys configured (see setup guide for your mode)
- [ ] Test on device with Debug UI

---

## Next Steps

- [Prototype Mode Setup](prototype-setup.md) - Configure GameAnalytics + Facebook
- [Full Mode Setup](full-setup.md) - Configure GameAnalytics + MAX + Adjust
- [Firebase Setup](firebase.md) - Add Analytics, Crashlytics, Remote Config
- [API Reference](api-reference.md) - Full API documentation
- [Troubleshooting](troubleshooting.md) - Common issues and fixes
