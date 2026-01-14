# Sorolla SDK

Plug-and-play mobile publisher SDK for Unity. Zero-config initialization with automatic iOS ATT handling.

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)

## Install

1. **Package Manager** → `+` → **Add package from git URL**
2. Paste: `https://github.com/sorolla-studio/sorolla-palette.git#v3.1.0`
3. Configuration window opens → Follow setup

## Quick Start

**[Get Started](Documentation~/quick-start.md)** - 10 minute setup for UA testing

## Usage

The SDK auto-initializes. Just call the API:

```csharp
using Sorolla.Palette;

// Track level progression (required)
Palette.TrackProgression(ProgressionStatus.Complete, "Level_001");

// Track custom events
Palette.TrackDesign("tutorial:completed");

// Show rewarded ad
if (Palette.IsRewardedAdReady)
    Palette.ShowRewardedAd(onComplete: () => GiveReward());
```

## Documentation

| | |
|---|---|
| [Quick Start](Documentation~/quick-start.md) | Get running in 10 minutes |
| [Switch to Full Mode](Documentation~/switching-to-full.md) | Production setup (Adjust + GDPR) |
| [API Reference](Documentation~/api-reference.md) | Complete API documentation |
| [Troubleshooting](Documentation~/troubleshooting.md) | Common issues and fixes |

### SDK Guides

| | |
|---|---|
| [GameAnalytics](Documentation~/guides/gameanalytics.md) | Analytics setup |
| [Facebook](Documentation~/guides/facebook.md) | Attribution setup |
| [Firebase](Documentation~/guides/firebase.md) | Analytics, Crashlytics, Remote Config |
| [Ads (MAX)](Documentation~/guides/ads.md) | Monetization setup |
| [Adjust](Documentation~/guides/adjust.md) | Full attribution (Full mode) |
| [GDPR/ATT](Documentation~/guides/gdpr.md) | Privacy compliance |

## What's Included

| Mode | SDKs | Use Case |
|------|------|----------|
| **Prototype** | GameAnalytics, Facebook, Firebase | UA testing, soft launch |
| **Full** | GameAnalytics, MAX, Adjust, Firebase | Production |
