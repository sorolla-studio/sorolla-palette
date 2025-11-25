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
| **Prototype** | GameAnalytics + Facebook | Rapid UA testing |
| **Full** | GameAnalytics + MAX + Adjust | Production |

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

## Documentation

- [Configuration Guide](Documentation~/CONFIGURATION.md)

## Support

- [GitHub Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
- [Changelog](CHANGELOG.md)
