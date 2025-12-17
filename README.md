# Sorolla SDK

A **plug-and-play** mobile publisher SDK for Unity games. Zero-configuration initialization with automatic iOS ATT handling.

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)

## Features

- 🔌 **Plug & Play**: Auto-initializes on app start - no code required
- 📱 **iOS ATT Support**: Automatic App Tracking Transparency handling
- 📊 **Unified Analytics API**: Single interface for all analytics providers
- 🔥 **Firebase Suite**: Analytics, Crashlytics, and Remote Config
- 🛠️ **Debug UI**: In-game overlay for testing Ads, Analytics, and Privacy flows
- 💰 **Monetization Ready**: AppLovin MAX with Adjust attribution

### Two Modes

| Mode | SDKs | Use Case |
|------|------|----------|
| **Prototype** | GameAnalytics + Facebook + MAX (optional) | Rapid UA testing |
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
2. Click `+` → `Add package from git URL` → Enter:
   
   `https://github.com/LaCreArthur/sorolla-palette-upm.git`

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

// Remote Config (unified: Firebase → GameAnalytics → default)
Sorolla.FetchRemoteConfig(success => {
    if (success)
    {
        int difficulty = Sorolla.GetRemoteConfigInt("difficulty", 1);
        bool newFeature = Sorolla.GetRemoteConfigBool("enable_new_feature", false);
        string message = Sorolla.GetRemoteConfig("welcome_message", "Hello!");
    }
});

// Crashlytics: Log exceptions and custom data
try { /* risky code */ }
catch (Exception ex) { Sorolla.LogException(ex); }

Sorolla.LogCrashlytics("User reached level 5");
Sorolla.SetCrashlyticsKey("player_id", "12345");

// Show ads (requires MAX)
Sorolla.ShowRewardedAd(
    onComplete: () => GiveReward(),
    onFailed: () => Debug.Log("Ad not available")
);
```

### 4. Verify with Debug UI

The SDK includes a built-in Debug UI to verify your integration on-device without needing logs.

1. **Install Sample**: Open Package Manager > Sort by "In Project" > Select "Sorolla SDK" > Samples > Import "Debug UI".
2. **Setup**: Drag the `Debug UI` scene into your project or copy the `DebugPanelManager` prefab (once created) into your scene.
3. **Launch App**: Build to device (iOS/Android) or play in Editor.
4. **Open UI**: Triple-tap anywhere on the screen (Mobile) or press "Back Quote" key (PC/Mac) to toggle.
5. **features**:
    - Check "Identity" for Advertising IDs (GAID/IDFA)
    - Test Ad loading/showing (MAX)
    - Verify Adjust/GA initialization status
    - Reset/Test Consent flows

## SDK Configuration

📖 **[Complete SDK Setup Guide for External Developers →](Documentation~/SDK-Setup-Guide.md)**

For detailed step-by-step instructions on obtaining and configuring all API keys, including screenshots and dashboard navigation guides, see the full setup documentation.

### Quick Overview

#### GameAnalytics (Required)
1. Create account at [gameanalytics.com](https://gameanalytics.com/)
2. Create game → Copy Game Key & Secret Key
3. In Unity: `GameAnalytics > Setup Wizard`

#### Facebook (Prototype Mode Only)
1. Create app at [developers.facebook.com](https://developers.facebook.com/apps/)
2. Copy App ID + **Client Token** (Settings → Advanced → Security) ⚠️
3. Generate Key Hashes (debug & release) ⚠️
4. In Unity: `Facebook > Edit Settings`

#### AppLovin MAX (Optional in Prototype, Required in Full)
1. Create account at [dash.applovin.com](https://dash.applovin.com/)
2. Get SDK Key from Account → Keys
3. Create Ad Units (Rewarded, Interstitial)
4. Enter keys in Sorolla Configuration window

#### Adjust (Full Mode)
1. Create account at [adjust.com](https://www.adjust.com/)
2. Create app → Copy App Token
3. Enter token in Sorolla Configuration window

### Firebase (Optional)

Firebase provides Analytics, Crashlytics, and Remote Config.

**Quick Setup:**
1. Create project at [Firebase Console](https://console.firebase.google.com/)
2. Add your Unity app (Android and/or iOS)
3. Download config files → place in `Assets/`:
   - **Android**: `google-services.json`
   - **iOS**: `GoogleService-Info.plist`
4. In Unity: `Sorolla > Configuration` → Click "Install" under Firebase
5. Enable modules (Analytics, Crashlytics, Remote Config)

📖 **[Firebase Setup Guide](Documentation~/FirebaseSetup.md)** | **[Quick Start Guide](Documentation~/QuickStart.md)**

**Note**: All Firebase features work in parallel with GameAnalytics — no code changes required!

## Documentation

### For Developers
- [SDK Setup Guide](Documentation~/SDK-Setup-Guide.md) - Complete external SDK configuration
- [Firebase Setup](Documentation~/FirebaseSetup.md) - Firebase configuration guide
- [iOS Setup](Documentation~/iOS-Setup.md) - iOS-specific requirements
- [Architecture Deep-Dive](Documentation~/ARCHITECTURE.md) - Technical implementation details

### For AI Agents
- [AI Agent Quick Start](Documentation~/AI_AGENT_README.md) - Rapid codebase orientation
- [Architecture Summary](Documentation~/ARCHITECTURE_SUMMARY.md) - High-level overview (~400 tokens)
- [Plan & Tasks](Documentation~/plan.md) - Current sprint, backlog, ADRs
- [Development Log](Documentation~/devlog.md) - Change history with hindsight insights

### Module Documentation
| Module | Description | RAG Query |
|--------|-------------|-----------|
| [SorollaSDK](Documentation~/modules/SorollaSDK.md) | Public API reference | `analytics ads events` |
| [Adapters](Documentation~/modules/Adapters.md) | Third-party SDK integration | `MAX Adjust Firebase` |
| [ATT & Privacy](Documentation~/modules/ATT.md) | iOS App Tracking Transparency | `ATT consent IDFA` |
| [Editor Tools](Documentation~/modules/Editor.md) | Configuration window & SDK management | `editor installation` |
| [Debug UI](Documentation~/modules/DebugUI.md) | In-game debug panel | `debug testing` |
| [Configuration](Documentation~/modules/Configuration.md) | SorollaConfig & SDK keys | `config keys setup` |

## Support

- [GitHub Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
- [Changelog](CHANGELOG.md)
