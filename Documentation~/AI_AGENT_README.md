# Sorolla SDK - AI Agent Quick Start

> **Purpose**: Rapid codebase orientation for AI agents
> **Token Budget**: ~300 tokens | **Updated**: 2025-12-17

---

## Orientation (30 seconds)

**What is this?** Mobile game publishing SDK for Unity (iOS/Android)
**Similar to**: Voodoo Sauce, Homa Belly
**Core function**: Analytics + Ads + Attribution in one package

## File Navigation

### Start Here
```
ARCHITECTURE_SUMMARY.md    → High-level overview (400 tokens)
modules/SorollaSDK.md      → Public API reference
modules/Configuration.md   → SDK keys & setup
```

### By Task Type

| Task | Load First | Then |
|------|------------|------|
| Add analytics event | `modules/SorollaSDK.md` | - |
| Fix ad issue | `modules/Adapters.md` | `modules/Configuration.md` |
| iOS ATT problem | `modules/ATT.md` | - |
| Editor window bug | `modules/Editor.md` | - |
| Debug UI issue | `modules/DebugUI.md` | - |
| Find a feature | `plan.md` | - |
| Understand change | `devlog.md` | - |

### RAG Query Patterns

```
"SorollaSDK TrackProgression" → modules/SorollaSDK.md
"MAX adapter ad revenue"      → modules/Adapters.md
"ATT consent iOS"             → modules/ATT.md
"Firebase remote config"      → modules/Adapters.md
"editor SDK installation"     → modules/Editor.md
"debug panel triple tap"      → modules/DebugUI.md
```

## Critical Paths

### Initialization
```
SorollaBootstrapper.AutoInit() → ATT Check → SorollaSDK.Initialize()
```

### Event Tracking
```
SorollaSDK.TrackDesign() → GameAnalyticsAdapter + FirebaseAdapter
```

### Ad Flow
```
SorollaSDK.ShowRewardedAd() → MaxAdapter → AdjustAdapter.TrackAdRevenue()
```

## Key Files (Runtime)

| File | LOC | Purpose |
|------|-----|---------|
| `Runtime/SorollaSDK.cs` | 484 | Main API |
| `Runtime/SorollaBootstrapper.cs` | 140 | Auto-init |
| `Runtime/Adapters/MaxAdapter.cs` | 227 | Ads |
| `Runtime/Adapters/AdjustAdapter.cs` | 114 | Attribution |

## Conventions

- **Namespace**: `Sorolla` (public), `Sorolla.Adapters` (internal)
- **Config**: `Resources/SorollaConfig.asset`
- **Conditional**: `#if GAMEANALYTICS_INSTALLED`, etc.
- **Mode**: `SOROLLA_PROTOTYPE` or `SOROLLA_FULL` define

## Quick Facts

- 59 C# scripts, ~3,900 LOC
- 10 managed SDKs via manifest.json
- Static adapter classes, no DI
- Event-driven Debug UI

---

## Memory Buffer Links

- **Tasks**: [plan.md](plan.md) - Current sprint, backlog
- **History**: [devlog.md](devlog.md) - Changes, learnings, insights

---

*For human developers: See main [README.md](../README.md)*
