# Firebase Setup Guide

Quick guide for setting up Firebase Analytics, Crashlytics, and Remote Config.

---

## 1. Firebase Console Setup

### Create Project

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create a new project
3. **Enable Google Analytics** ✅ (required for Analytics to work)

### Add Your Apps

**Android:**
1. Click **Add app** → **Android**
2. Enter your package name (from `Player Settings > Android`)
3. Download `google-services.json`

**iOS:**
1. Click **Add app** → **iOS**
2. Enter your bundle ID (from `Player Settings > iOS`)
3. Download `GoogleService-Info.plist`

---

## 2. Unity Setup

### Install Firebase

1. Open `Sorolla > Configuration`
2. Click **Install** next to **Firebase (optional)**
3. Wait for packages to import

### Add Config Files

Place the downloaded files in your `Assets/` folder:
```
Assets/
├── google-services.json        ← Android
└── GoogleService-Info.plist    ← iOS
```

### Enable Modules

In Sorolla Configuration, enable the modules you need:
- **Analytics**: Events tracked to both Firebase and GameAnalytics
- **Crashlytics**: Automatic crash reporting
- **Remote Config**: A/B testing and feature flags

Click **Save**.

---

## 3. Usage

### Analytics

Events are automatically sent to both Firebase and GameAnalytics:

```csharp
Palette.TrackDesign("tutorial:complete");
Palette.TrackProgression(ProgressionStatus.Complete, "World_01", "Level_03");
Palette.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");
```

### Crashlytics

Crashes are captured automatically. For manual logging:

```csharp
Palette.LogCrashlytics("User started level 5");
Palette.SetCrashlyticsKey("level", 5);
Palette.LogException(ex); // Log handled exceptions
```

### Remote Config

Sorolla provides a **unified Remote Config API** that automatically uses Firebase (if enabled) with GameAnalytics as fallback.

1. Create parameters in Firebase Console → **Remote Config**
2. Click **Publish changes**

```csharp
Palette.FetchRemoteConfig(success => {
    float difficulty = Palette.GetRemoteConfigFloat("difficulty", 1.0f);
    bool feature = Palette.GetRemoteConfigBool("new_feature", false);
    int coins = Palette.GetRemoteConfigInt("reward_amount", 100);
    string msg = Palette.GetRemoteConfig("welcome_message", "Hello!");
});
```

**Priority:** Firebase (if installed & enabled) → GameAnalytics → default value

---

## 4. Troubleshooting

| Issue | Solution |
|-------|----------|
| Events not appearing | Enable Google Analytics when creating Firebase project |
| Crashes not showing | Crashes report on next app launch, wait 5-10 min |
| Remote Config returns defaults | Ensure parameters are **published** in console |
| "Firebase not initialized" | Config files must match your package name/bundle ID |

---

## API Reference

```csharp
// Analytics (sent to Firebase + GameAnalytics)
Palette.TrackDesign(string eventId, float value = 0);
Palette.TrackProgression(ProgressionStatus status, string p1, string p2 = null, string p3 = null, int score = 0);
Palette.TrackResource(ResourceFlowType flow, string currency, float amount, string itemType, string itemId);

// Crashlytics
Palette.LogCrashlytics(string message);
Palette.LogException(Exception ex);
Palette.SetCrashlyticsKey(string key, string/int/float/bool value);

// Remote Config (unified: Firebase → GameAnalytics → default)
Palette.IsRemoteConfigReady();
Palette.FetchRemoteConfig(Action<bool> callback);
Palette.GetRemoteConfig(string key, string defaultValue);
Palette.GetRemoteConfigInt(string key, int defaultValue);
Palette.GetRemoteConfigFloat(string key, float defaultValue);
Palette.GetRemoteConfigBool(string key, bool defaultValue);
```
