# Sorolla SDK

A **plug-and-play** mobile publisher SDK for Unity games. Zero-configuration initialization with automatic iOS ATT handling.

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)

## Features

- 🔌 **Plug & Play**: Auto-initializes on app start - no code required
- 📱 **iOS ATT Support**: Automatic App Tracking Transparency handling
- 📊 **Unified Analytics API**: Single interface for all analytics providers
- 💰 **Monetization Ready**: AppLovin MAX with Adjust attribution

### Two Modes

| Mode | SDKs | Use Case |
|------|------|----------|
| **Prototype** | GameAnalytics + Facebook + MAX (optional) | Rapid UA testing |
| **Full** | GameAnalytics + Facebook + MAX + Adjust | Production |

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

// Remote config
if (Sorolla.IsRemoteConfigReady())
{
    int difficulty = Sorolla.GetRemoteConfigInt("difficulty", 1);
}

// Show ads (requires MAX)
Sorolla.ShowRewardedAd(
    onComplete: () => GiveReward(),
    onFailed: () => Debug.Log("Ad not available")
);
```

## SDK Configuration

📖 **[Complete SDK Setup Guide for External Developers →](Documentation~/SDK-Setup-Guide.md)**

For detailed step-by-step instructions on obtaining and configuring all API keys, including screenshots and dashboard navigation guides, see the full setup documentation.

### Quick Overview

#### GameAnalytics (Required)
1. Create account at [gameanalytics.com](https://gameanalytics.com/)
2. Create game → Copy Game Key & Secret Key
3. In Unity: `GameAnalytics > Setup Wizard`

#### Facebook (Both Modes)
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

## Support

- [GitHub Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
- [Changelog](CHANGELOG.md)
