# Sorolla SDK - Architecture Summary

> **RAG Query**: `architecture overview mobile publishing SDK`
> **Token Budget**: ~400 tokens | **Last Updated**: 2025-12-17

## Overview

**Sorolla SDK** (v2.1.0) is a plug-and-play mobile game publishing SDK for Unity iOS/Android. Similar to Voodoo Sauce or Homa Belly, it provides:

- Zero-config auto-initialization at app startup
- Two operating modes: **Prototype** (rapid UA testing) | **Full** (production)
- Unified analytics API (GameAnalytics, Firebase, Facebook)
- Monetization via AppLovin MAX with Adjust attribution
- iOS ATT handling with native support + editor testing

## Core Components

```
SorollaSDK.cs          → Public API facade (static class)
SorollaBootstrapper.cs → Auto-init via [RuntimeInitializeOnLoadMethod]
SorollaConfig.cs       → ScriptableObject at Resources/SorollaConfig
```

## Data Flow

```
App Launch → Bootstrapper → ATT Check → SorollaSDK.Initialize(consent)
                                              ↓
                              ┌───────────────┼───────────────┐
                              ↓               ↓               ↓
                      GameAnalytics    MaxAdapter      FirebaseAdapter
                              ↓               ↓               ↓
                         Events       Ads → Adjust      Analytics
```

## Mode Architecture

| Mode | Required SDKs | Purpose |
|------|--------------|---------|
| **Prototype** | GA + Facebook | Rapid UA testing, no attribution |
| **Full** | GA + MAX + Adjust | Production with ad revenue tracking |

## Namespace Map

| Namespace | Purpose |
|-----------|---------|
| `Sorolla` | Public API (SorollaSDK, Config) |
| `Sorolla.Adapters` | Third-party SDK wrappers |
| `Sorolla.ATT` | iOS App Tracking Transparency |
| `Sorolla.Editor` | Configuration window, SDK management |
| `Sorolla.DebugUI` | In-game debug panel (sample) |

## Key Patterns

1. **Adapter Pattern** - Each SDK wrapped in static adapter class
2. **Conditional Compilation** - `#if GAMEANALYTICS_INSTALLED` guards
3. **Event-Driven** - Debug UI uses `SorollaDebugEvents` hub
4. **Fallback Chain** - Remote Config: Firebase → GA → default

## File Counts

| Category | Scripts | LOC |
|----------|---------|-----|
| Runtime | 15 | ~2,100 |
| Editor | 13 | ~1,800 |
| Debug UI | 31 | ~1,800 |
| **Total** | **59** | **~3,900** |

## Module Index

- [SorollaSDK API](modules/SorollaSDK.md)
- [Adapters](modules/Adapters.md)
- [ATT & Privacy](modules/ATT.md)
- [Editor Tools](modules/Editor.md)
- [Debug UI](modules/DebugUI.md)
- [Configuration](modules/Configuration.md)

---
*For agent quick-start: Load `modules/SorollaSDK.md` for public API, `modules/Adapters.md` for SDK integrations.*
