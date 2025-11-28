# Sorolla SDK

A **plug-and-play** mobile publisher SDK for Unity games. Zero-configuration initialization with automatic iOS ATT handling.

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)

## Features

- 🔌 **Plug & Play**: Auto-initializes on app start - no code required
- 📱 **iOS ATT Support**: Automatic App Tracking Transparency handling
- 📊 **Unified Analytics API**: Single interface for all analytics providers
- 🔥 **Firebase Suite**: Analytics, Crashlytics, and Remote Config
- 💰 **Monetization Ready**: AppLovin MAX with Adjust attribution

### Two Modes

| Mode | SDKs | Use Case |
|------|------|----------|
| **Prototype** | GameAnalytics + Facebook | Rapid UA testing |
| **Full** | GameAnalytics + MAX + Adjust | Production |

### Optional Add-ons

| Add-on | Description |
|--------|-------------|
| **Firebase Analytics** | Dual analytics backend (parallel with GameAnalytics) |
| **Firebase Crashlytics** | Automatic crash & exception reporting |
| **Firebase Remote Config** | A/B testing and feature flags |

## Installation

### Via Git URL

1. Open Unity Package Manager
2. Click `+` → `Add package from git URL`
3. Enter: `https://github.com/LaCreArthur/sorolla-palette-upm.git`

**That's it!** The package automatically installs dependencies.

## Quick Start

### 1. Select Your Mode
The Configuration window opens automatically. Select **Prototype** or **Full** mode.

### 2. Configure SDKs
The Setup Checklist guides you through configuration:
- **GameAnalytics**: Click "Open Settings" → Setup Wizard
- **Facebook**: Click "Open Settings" → Edit Settings  
- **MAX/Adjust**: Enter keys directly in Sorolla Configuration

### 3. Done! 🎉

## Usage

```csharp
using Sorolla;

// Track level completion
Sorolla.TrackProgression(ProgressionStatus.Complete, "World_01", "Level_03");

// Track custom events
Sorolla.TrackDesign("tutorial:completed");

// Track resources
Sorolla.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");

// Remote config (GameAnalytics)
if (Sorolla.IsRemoteConfigReady())
{
    int difficulty = Sorolla.GetRemoteConfigInt("difficulty", 1);
}

// Firebase Remote Config (A/B testing)
if (Sorolla.IsFirebaseRemoteConfigReady())
{
    bool newFeature = Sorolla.GetFirebaseRemoteConfigBool("enable_new_feature", false);
}

// Log exceptions to Crashlytics
try { /* risky code */ }
catch (Exception ex) { Sorolla.LogException(ex); }

// Show ads (requires MAX)
Sorolla.ShowRewardedAd(
    onComplete: () => GiveReward(),
    onFailed: () => Debug.Log("Ad not available")
);
```

## SDK Configuration

### GameAnalytics
1. Create account at [gameanalytics.com](https://gameanalytics.com/)
2. Create game → Copy Game Key & Secret Key
3. In Unity: `GameAnalytics > Setup Wizard`

### Facebook (Prototype Mode)
1. Create app at [developers.facebook.com](https://developers.facebook.com/apps/)
2. Copy App ID
3. In Unity: `Facebook > Edit Settings`

### AppLovin MAX
1. Create account at [dash.applovin.com](https://dash.applovin.com/)
2. Get SDK Key from Account → Keys
3. Create Ad Units (Rewarded, Interstitial)
4. Enter keys in Sorolla Configuration window

### Adjust (Full Mode)
1. Create account at [adjust.com](https://www.adjust.com/)
2. Create app → Copy App Token
3. Enter token in Sorolla Configuration window

### Firebase Analytics (Optional)
1. Create project at [Firebase Console](https://console.firebase.google.com/)
2. Add your Unity app (Android and/or iOS)
3. Download config files:
   - **Android**: `google-services.json` → place in `Assets/`
   - **iOS**: `GoogleService-Info.plist` → place in `Assets/`
4. In Unity: `Sorolla > Configuration` → Click "Install" under Firebase
5. Enable desired modules in the Firebase section:
   - **Analytics**: Track custom events alongside GameAnalytics
   - **Crashlytics**: Automatic crash & exception reporting
   - **Remote Config**: A/B testing and feature flags

**Note**: All Firebase features work in parallel — no code changes required!

## Support

- [GitHub Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
- [Changelog](CHANGELOG.md)
