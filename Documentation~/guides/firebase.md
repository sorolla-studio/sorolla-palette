# Firebase Setup

Analytics, Crashlytics, and Remote Config.

> Firebase is required in Full mode and optional in Prototype. Install it from **Tools > Sorolla Palette SDK** when a Prototype needs Firebase Analytics, Crashlytics, or Remote Config.

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

The Palette window shows status under the **Firebase** group.

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

// Level progression with extra Firebase context.
// Complete/Fail auto-include duration when Start was called.
Palette.Level.Start(level: 1);
Palette.Level.Complete(level: 1, score: 1500,
    extraParams: new Dictionary<string, object> { { "difficulty", "hard" } });
```

### User Identity

Set a user ID once — it propagates to Firebase Analytics, Firebase Crashlytics, and Adjust in a single call.

```csharp
// Set (or clear, by passing null)
Palette.SetUserId("player_abc123");

// Firebase audience segmentation — register custom properties in
// Firebase Console > Analytics > Custom definitions first.
Palette.SetUserProperty("subscription_tier", "premium");
Palette.SetUserProperty("tutorial_variant", "B");
```

### Crashlytics

Crashes are captured automatically. For manual logging, breadcrumbs, and non-fatal exceptions:

```csharp
// Breadcrumb log (attached to next crash report)
Palette.LogCrashlytics("User started level 5");

// Custom keys — all 4 overloads available (string, int, float, bool)
Palette.SetCrashlyticsKey("player_level", 12);
Palette.SetCrashlyticsKey("has_subscription", true);
Palette.SetCrashlyticsKey("coins", 250.5f);
Palette.SetCrashlyticsKey("build_flavor", "beta");

// Log handled exceptions without crashing
try { RiskyOperation(); }
catch (Exception ex) { Palette.LogException(ex); }
```

### Remote Config

For authoring and publishing the dashboard-side template (CLI setup, deploy, rollback, schema, CI tokens), see the dedicated [Firebase Remote Config CLI guide](firebase-remote-config.md). The runtime API below is what consumes those values inside the game.

The SDK owns the fetch lifecycle: it fetches automatically at init, retries on failure
(short backoff, then on every app-foreground), and applies real-time updates. The game
declares defaults, reads values, and reacts to changes - there is nothing to fetch manually.

```csharp
// Optional: register in-app defaults (any time, before or after init)
Palette.SetRemoteConfigDefaults(new Dictionary<string, object>
{
    { "difficulty", 1.0f },
    { "new_feature", false }
});

// React to value changes. Fires on the first load too, and immediately at
// subscribe time if values are already readable - no ordering to get right.
Palette.OnRemoteConfigChanged += changedKeys => ReloadTuning();

// Read anywhere, any time. Resolution: Firebase -> GameAnalytics -> registered
// defaults -> call-site default. Identical for every type.
float difficulty = Palette.GetRemoteConfigFloat("difficulty", 1.0f);
bool feature = Palette.GetRemoteConfigBool("new_feature", false);
```

Create parameters in Firebase Console -> **Remote Config** -> **Publish changes**.

### Freshness: status and gating

`Palette.RemoteConfigStatus` tells you which generation of values the getters serve:
`Defaults` (no fetch ever succeeded on this device) -> `Cached` (last session's values,
served from disk) -> `Live` (fetched this session). Monotonic within a session.

Anything that must not run on stale balance (A/B bucketing, gameplay start behind a
network wall) should gate on it:

```csharp
// Hold the loading screen briefly for values; devices that fetched before pass instantly.
bool ready = await Palette.WaitForRemoteConfig(timeoutSeconds: 5f);
if (!ready) { /* proceeding on shipped defaults - keep them sane */ }
```

### Real-Time Remote Config

Config changes stream to the app instantly, no restart needed - they activate
automatically and `OnRemoteConfigChanged` fires with the updated keys:

```csharp
Palette.OnRemoteConfigChanged += changedKeys =>
{
    if (changedKeys.Contains("difficulty"))
        UpdateDifficulty();
};

// For games where mid-session flips would be jarring: defer activation.
Palette.AutoActivateRemoteConfigUpdates = false;
Palette.OnRemoteConfigUpdateAvailable += keys => _pendingConfigUpdate = true;
// ...then between rounds:
await Palette.ActivateRemoteConfigAsync();   // OnRemoteConfigChanged fires after this
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Events not appearing | Enable Google Analytics when creating project |
| Crashes not showing | Crashes report on next app launch, wait 5-10 min |
| Remote Config returns defaults | Ensure parameters are **published** in console |
| "Firebase not initialized" | Config files must match your bundle ID |
