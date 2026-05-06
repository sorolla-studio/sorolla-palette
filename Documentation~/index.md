# Sorolla Palette SDK

Unity SDK for mobile game studios working with Sorolla. Start with Prototype mode, prove the core analytics path, then migrate to Full mode when the build is ready for soft launch.

## Start Here

### Prototype Mode

Fastest path to a useful studio build:

1. Install Palette.
2. Create GameAnalytics, Facebook, and Firebase apps.
3. Paste the keys and config files.
4. Add three level analytics calls.
5. Build once on device.

[Follow the Prototype quick start](quick-start.md)

### Full Mode

Use this when Prototype analytics are already working and the game is ready for ads, attribution, consent, and revenue validation.

[Migrate to Full mode](switching-to-full.md)

## What Palette Handles

| Mode | Included SDKs | Best for |
|------|---------------|----------|
| Prototype | GameAnalytics, Facebook, Firebase | Publisher review builds, CPI tests, gameplay iteration |
| Full | Prototype SDKs plus AppLovin MAX and Adjust | Soft launch, monetization tests, paid UA |

Palette auto-initializes at runtime. Studios only wire game events and game-specific placements.

## Common Paths

| Need | Go to |
|------|-------|
| First integration | [Prototype Mode Quick Start](quick-start.md) |
| Soft-launch migration | [Full Mode Soft Launch Migration](switching-to-full.md) |
| Full-mode validation | [Full Mode Validation](validation.md) |
| API signatures | [API Reference](api-reference.md) |
| Build or dashboard issue | [Troubleshooting](troubleshooting.md) |
| App Store privacy answers | [App Store Privacy](app-store-privacy.md) |

## SDK Setup Guides

Use these only when you need dashboard-level detail:

- [GameAnalytics](guides/gameanalytics.md)
- [Facebook](guides/facebook.md)
- [Firebase](guides/firebase.md)
- [AppLovin MAX Ads](guides/ads.md)
- [Adjust Attribution](guides/adjust.md)
- [TikTok](guides/tiktok.md)
- [GDPR / ATT Consent](guides/gdpr.md)
