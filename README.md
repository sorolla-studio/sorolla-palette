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

**That's it!** The package automatically:
- Installs in **Prototype Mode** (perfect for most users)
- Sets up GameAnalytics + Facebook SDK
- Creates the config file
- No manual configuration needed!

**Want Full Mode instead?** Open `Palette > Configuration` to switch modes.

## Quick Start

### Automatic Setup (Recommended)

The SDK **automatically installs in Prototype Mode** when you import it - no setup required!

**Prototype Mode includes:**
- ✅ GameAnalytics (analytics)
- ✅ Facebook SDK (attribution for UA)
- ⚡ Optional: AppLovin MAX (ads)

**To switch to Full Mode:** Go to `Palette > Configuration` and select Full Mode.

### Manual Mode Selection

If you need to manually select or switch modes, open `Palette > Configuration`:

| | **🚀 Prototype Mode** | **🏭 Full Mode** |
|---|---|---|
| **Best for** | Testing UA campaigns<br>Soft launch<br>Rapid iteration | Production launch<br>Live games<br>Full monetization |
| **Auto-installed** | ✅ On package import | ⚡ Manual switch required |
| **Analytics** | ✅ GameAnalytics | ✅ GameAnalytics |
| **Attribution** | ✅ Facebook SDK | ✅ Adjust (full attribution) |
| **Ads** | ⚡ Optional (MAX) | ✅ Required (MAX + mediation) |
| **GDPR/ATT** | ⚡ Optional | ✅ Required for EU/production |
| **Firebase** | ⚡ Optional add-on | ⚡ Recommended add-on |

### Setup Guides

- 📖 [**Prototype Setup Guide**](Documentation~/prototype-setup.md) - You're already in Prototype mode!
- 📖 [**Full Mode Setup Guide**](Documentation~/full-setup.md) - Switch to Full mode for production

## Usage

The SDK auto-initializes on app start. Just call the API:

```csharp
using Sorolla.Palette;

// Track level progression (required for analytics)
Palette.TrackProgression(ProgressionStatus.Complete, "Level_001");

// Track custom events
Palette.TrackDesign("tutorial:completed");

// Track economy
Palette.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");

// Show rewarded ad (requires MAX)
if (Palette.IsRewardedAdReady)
{
    Palette.ShowRewardedAd(
        onComplete: () => GiveReward(),
        onFailed: () => Debug.Log("Ad not available")
    );
}
```

📖 **[Complete API Reference](Documentation~/api-reference.md)**

## Documentation

### 📚 Setup Guides
| Path | Guide | Description |
|------|-------|-------------|
| 🚀 **Start Here** | [**Prototype Setup**](Documentation~/prototype-setup.md) | Complete guide for UA testing (10 min) |
| 🏭 **Production** | [**Full Mode Setup**](Documentation~/full-setup.md) | Complete guide for live games (30 min) |
| 🔥 **Optional** | [Firebase](Documentation~/firebase.md) | Analytics, Crashlytics, Remote Config |
| 📱 **Optional** | [Ads Setup](Documentation~/ads-setup.md) | AppLovin MAX monetization |

### 📖 Reference & Support
| Document | Description |
|----------|-------------|
| [API Reference](Documentation~/api-reference.md) | Complete API documentation with examples |
| [Troubleshooting](Documentation~/troubleshooting.md) | Common issues and solutions |
| [Contributing](Documentation~/contributing.md) | How to contribute to the SDK |
| [Changelog](CHANGELOG.md) | Version history and updates |
