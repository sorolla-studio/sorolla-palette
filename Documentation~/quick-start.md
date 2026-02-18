# Quick Start

Get your prototype running in 10 minutes.

---

## 1. Install

1. **Package Manager** → `+` → **Add package from git URL**
2. Paste: `https://github.com/sorolla-studio/sorolla-palette.git#v3.4.0`
3. **Configuration window** opens automatically

---

## 2. Configure

Open **Palette > Configuration** if not already open.

### Checklist

| SDK | Action | Guide |
|-----|--------|-------|
| **GameAnalytics** | Add Game Key + Secret Key | [Setup](guides/gameanalytics.md) |
| **Facebook** | Add App ID + Client Token | [Setup](guides/facebook.md) |
| **Firebase** | Add config files to `Assets/` | [Setup](guides/firebase.md) |

The Configuration window shows your progress:
- **SDK Overview**: Green checkmarks = configured
- **Build Health**: All green = ready to build

---

## 3. Add Analytics

Add level tracking to your game (required for analytics):

```csharp
using Sorolla.Palette;

// Format: "Level_001" (zero-pad for sorting)
string level = $"Level_{currentLevel:D3}";

Palette.TrackProgression(ProgressionStatus.Start, level);    // Level started
Palette.TrackProgression(ProgressionStatus.Complete, level); // Level won
Palette.TrackProgression(ProgressionStatus.Fail, level);     // Level lost
```

---

## 4. Build & Test

1. Build to iOS/Android device
2. Verify in dashboards:
   - [GameAnalytics](https://gameanalytics.com) - Events in 5-10 min
   - [Facebook Events Manager](https://business.facebook.com) - Install data
   - [Firebase Console](https://console.firebase.google.com) - Analytics, Crashlytics

### Optional: Debug UI

Import the Debug UI sample for on-device testing:
1. **Package Manager** → Sorolla SDK → **Samples** → Import "Debug UI"
2. Add `DebugPanelManager` prefab to your scene
3. **Triple-tap** screen to open panel

---

## Release Checklist

Before launching UA campaigns:

- [ ] GameAnalytics configured (green in SDK Overview)
- [ ] Facebook SDK configured (green in SDK Overview)
- [ ] Firebase config files added (`google-services.json`, `GoogleService-Info.plist`)
- [ ] `TrackProgression` calls added to game code
- [ ] Build succeeds (Build Health all green)
- [ ] Admin access granted to `studio@sorolla.io` in GameAnalytics

---

## Next Steps

| Goal | Link |
|------|------|
| Add monetization (ads) | [Ads Guide](guides/ads.md) |
| Go to production | [Switch to Full Mode](switching-to-full.md) |
| Track more events | [API Reference](api-reference.md) |
| Fix issues | [Troubleshooting](troubleshooting.md) |
