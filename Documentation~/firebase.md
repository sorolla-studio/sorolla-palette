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
SorollaSDK.TrackDesign("tutorial:complete");
SorollaSDK.TrackProgression(ProgressionStatus.Complete, "World_01", "Level_03");
SorollaSDK.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");
```

### Crashlytics

Crashes are captured automatically. For manual logging:

```csharp
SorollaSDK.LogCrashlytics("User started level 5");
SorollaSDK.SetCrashlyticsKey("level", 5);
SorollaSDK.LogException(ex); // Log handled exceptions
```

### Remote Config

Sorolla provides a **unified Remote Config API** that automatically uses Firebase (if enabled) with GameAnalytics as fallback.

1. Create parameters in Firebase Console → **Remote Config**
2. Click **Publish changes**

```csharp
SorollaSDK.FetchRemoteConfig(success => {
    float difficulty = SorollaSDK.GetRemoteConfigFloat("difficulty", 1.0f);
    bool feature = SorollaSDK.GetRemoteConfigBool("new_feature", false);
    int coins = SorollaSDK.GetRemoteConfigInt("reward_amount", 100);
    string msg = SorollaSDK.GetRemoteConfig("welcome_message", "Hello!");
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
SorollaSDK.TrackDesign(string eventId, float value = 0);
SorollaSDK.TrackProgression(ProgressionStatus status, string p1, string p2 = null, string p3 = null, int score = 0);
SorollaSDK.TrackResource(ResourceFlowType flow, string currency, float amount, string itemType, string itemId);

// Crashlytics
SorollaSDK.LogCrashlytics(string message);
SorollaSDK.LogException(Exception ex);
SorollaSDK.SetCrashlyticsKey(string key, string/int/float/bool value);

// Remote Config (unified: Firebase → GameAnalytics → default)
SorollaSDK.IsRemoteConfigReady();
SorollaSDK.FetchRemoteConfig(Action<bool> callback);
SorollaSDK.GetRemoteConfig(string key, string defaultValue);
SorollaSDK.GetRemoteConfigInt(string key, int defaultValue);
SorollaSDK.GetRemoteConfigFloat(string key, float defaultValue);
SorollaSDK.GetRemoteConfigBool(string key, bool defaultValue);
```
