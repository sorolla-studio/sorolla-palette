# Sorolla Palette SDK

Unity SDK for mobile game studios working with Sorolla. Start with Prototype mode, prove the core analytics path, then migrate to Full mode when the build is ready for soft launch.

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-blue)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)

## Start Here

### Prototype Mode

Fastest path to a useful studio build:

1. Install Palette.
2. Create GameAnalytics, Facebook, and Firebase apps.
3. Paste the keys and config files.
4. Add three level analytics calls.
5. Build once on device.

[Follow the Prototype quick start](https://sorolla-studio.github.io/sorolla-palette/quick-start.html)

### Full Mode

Use this when Prototype analytics are already working and the game is ready for ads, attribution, consent, and revenue validation.

[Migrate to Full mode](https://sorolla-studio.github.io/sorolla-palette/switching-to-full.html)

## Install

1. In Unity, open **Window > Package Manager**.
2. Click `+` and choose **Add package from git URL**.
3. Paste:

```text
https://github.com/sorolla-studio/sorolla-palette.git
```

Palette auto-initializes at runtime. Studios do not add a bootstrap prefab or call manual init.

## Core Prototype API

```csharp
using Sorolla.Palette;

Palette.Level.Start(level: 1);
Palette.Level.Complete(level: 1, score: 1500);
Palette.Level.Fail(level: 1);
```

## What Palette Handles

| Mode | Included SDKs | Best for |
|------|---------------|----------|
| Prototype | GameAnalytics, Facebook, Firebase | Publisher review builds, CPI tests, gameplay iteration |
| Full | Prototype SDKs plus AppLovin MAX and Adjust | Soft launch, monetization tests, paid UA |

## Documentation

| Need | Go to |
|------|-------|
| First integration | [Prototype Mode Quick Start](https://sorolla-studio.github.io/sorolla-palette/quick-start.html) |
| Soft-launch migration | [Full Mode Soft Launch Migration](https://sorolla-studio.github.io/sorolla-palette/switching-to-full.html) |
| Full-mode validation | [Full Mode Validation](https://sorolla-studio.github.io/sorolla-palette/validation.html) |
| API signatures | [API Reference](https://sorolla-studio.github.io/sorolla-palette/api-reference.html) |
| Build or dashboard issue | [Troubleshooting](https://sorolla-studio.github.io/sorolla-palette/troubleshooting.html) |
