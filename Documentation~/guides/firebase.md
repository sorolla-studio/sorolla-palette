# Firebase Setup

Analytics, Crashlytics, and Remote Config.

> Firebase is **required in Full mode** and **optional in Prototype mode**. In Full mode, packages are auto-installed.

---

## 1. Create Project

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create a new project
3. **Enable Google Analytics** when prompted (required for Analytics to work)

## 2. Add Your Apps

### Android
1. Click **Add app** → **Android**
2. Enter your package name (from Unity Player Settings)
3. Download `google-services.json`

### iOS
1. Click **Add app** → **iOS**
2. Enter your bundle ID (from Unity Player Settings)
3. Download `GoogleService-Info.plist`

## 3. Add Config Files

Place both files in your `Assets/` folder:

```
Assets/
├── google-services.json        ← Android
└── GoogleService-Info.plist    ← iOS
```

The Configuration window shows status under **Build Health** → **Firebase Coherence**.

---

## Usage

### Analytics

Events are automatically sent to both Firebase and GameAnalytics:

```csharp
// Structured custom event (Firebase gets full params, GA gets best-effort)
Palette.TrackEvent("booster_used", new Dictionary<string, object>
{
    { "booster_id", "speed_2x" },
    { "level", 12 }
});

// Progression with extra Firebase context
Palette.TrackProgression(ProgressionStatus.Complete, "Level_01", score: 1500,
    extraParams: new Dictionary<string, object> { { "duration_sec", 45 } });
```

### Crashlytics

Crashes are captured automatically. For manual logging:

```csharp
Palette.LogCrashlytics("User started level 5");
Palette.SetCrashlyticsKey("level", "5");
Palette.LogException(ex); // Log handled exceptions
```

### Remote Config

Unified API that checks Firebase first, then GameAnalytics, then in-app defaults:

```csharp
// Set fallback values
Palette.SetRemoteConfigDefaults(new Dictionary<string, object>
{
    { "difficulty", 1.0f },
    { "new_feature", false }
});

Palette.FetchRemoteConfig(success => {
    float difficulty = Palette.GetRemoteConfigFloat("difficulty", 1.0f);
    bool feature = Palette.GetRemoteConfigBool("new_feature", false);
});
```

Create parameters in Firebase Console -> **Remote Config** -> **Publish changes**.

### Real-Time Remote Config

Config changes stream to the app instantly, no restart needed:

```csharp
// React to live changes
Palette.OnRemoteConfigUpdated += (changedKeys) =>
{
    if (changedKeys.Contains("difficulty"))
        UpdateDifficulty();
};

// For competitive games: defer activation to between rounds
Palette.AutoActivateRemoteConfigUpdates = false;
await Palette.ActivateRemoteConfigAsync();
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Events not appearing | Enable Google Analytics when creating project |
| Crashes not showing | Crashes report on next app launch, wait 5-10 min |
| Remote Config returns defaults | Ensure parameters are **published** in console |
| "Firebase not initialized" | Config files must match your bundle ID |
