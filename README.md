# Sorolla SDK

Plug-and-play mobile publisher SDK for Unity. Zero-config initialization with automatic iOS ATT handling.

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)

## Install

1. **Package Manager** → `+` → **Add package from git URL**
2. Paste: `https://github.com/sorolla-studio/sorolla-palette.git`
3. Configuration window opens → Follow setup

## Quick Start

**[Get Started](https://sorolla-studio.github.io/sorolla-palette/quick-start.html)** - 10 minute setup for UA testing

## Usage

The SDK auto-initializes. Just call the API:

```csharp
using Sorolla.Palette;

// Track level progression (required)
Palette.Level.Complete(1, score: 1500);

// Track custom events with structured data
Palette.TrackEvent("tutorial_complete", new() { { "step", "crafting_intro" } });

// Show rewarded ad
if (Palette.IsRewardedAdReady)
    Palette.ShowRewardedAd(onComplete: () => GiveReward());
```

## Documentation

Full docs site: **[sorolla-studio.github.io/sorolla-palette](https://sorolla-studio.github.io/sorolla-palette/)**

| | |
|---|---|
| [Quick Start](https://sorolla-studio.github.io/sorolla-palette/quick-start.html) | Get running in 10 minutes |
| [Switch to Full Mode](https://sorolla-studio.github.io/sorolla-palette/switching-to-full.html) | Production setup (Adjust + GDPR) |
| [API Reference](https://sorolla-studio.github.io/sorolla-palette/api-reference.html) | Complete API documentation |
| [Troubleshooting](https://sorolla-studio.github.io/sorolla-palette/troubleshooting.html) | Common issues and fixes |

### SDK Guides

| | |
|---|---|
| [GameAnalytics](https://sorolla-studio.github.io/sorolla-palette/guides/gameanalytics.html) | Analytics setup |
| [Facebook](https://sorolla-studio.github.io/sorolla-palette/guides/facebook.html) | Attribution setup |
| [Firebase](https://sorolla-studio.github.io/sorolla-palette/guides/firebase.html) | Analytics, Crashlytics, Remote Config |
| [Ads (MAX)](https://sorolla-studio.github.io/sorolla-palette/guides/ads.html) | Monetization setup |
| [Adjust](https://sorolla-studio.github.io/sorolla-palette/guides/adjust.html) | Full attribution (Full mode) |
| [GDPR/ATT](https://sorolla-studio.github.io/sorolla-palette/guides/gdpr.html) | Privacy compliance |

## What's Included

| Mode | SDKs | Use Case |
|------|------|----------|
| **Prototype** | GameAnalytics, Facebook | UA testing, soft launch |
| **Full** | GameAnalytics, Facebook, MAX, Adjust, Firebase | Production |

> Firebase is optional in Prototype mode. Install it manually if needed for analytics/crashlytics.
