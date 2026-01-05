# AI Agent Quick Reference

Optimized for RAG retrieval and minimal token usage.

---

## 30-Second Orientation

**What**: Mobile game publishing SDK for Unity (iOS/Android)
**Like**: Voodoo Sauce, Homa Belly
**Function**: Analytics + Ads + Attribution unified API

---

## File Navigation

### By Task

| Task | Primary File | Secondary |
|------|-------------|-----------|
| API usage | `../getting-started.md` | `../api-reference.md` |
| SDK setup | `../prototype-setup.md` or `../full-setup.md` | - |
| Firebase | `../firebase.md` | - |
| Debug issue | `../troubleshooting.md` | - |
| Code changes | `architecture.md` | - |
| Task status | `plan.md` | `devlog.md` |
| Competitive intel | `competitive-analysis.md` | `market-research.md` |
| Product planning | `product-roadmap.md` | `plan.md` |

### By Code Area

| Area | File | Key Classes |
|------|------|-------------|
| Public API | `Runtime/Palette.cs` | `Palette` |
| Auto-init | `Runtime/SorollaBootstrapper.cs` | `SorollaBootstrapper` |
| Ads | `Runtime/Adapters/MaxAdapter.cs` | `MaxAdapter` |
| Attribution | `Runtime/Adapters/AdjustAdapter.cs` | `AdjustAdapter` |
| Config | `Runtime/SorollaConfig.cs` | `SorollaConfig` |
| Editor UI | `Editor/SorollaWindow.cs` | `SorollaWindow` |
| SDK mgmt | `Editor/Sdk/SdkRegistry.cs` | `SdkRegistry` |

---

## Critical Paths

```
Initialization:
App Start → SorollaBootstrapper.AutoInit() → ATT Check → Palette.Initialize(consent)

Event Tracking:
Palette.TrackDesign() → GameAnalyticsAdapter + FirebaseAdapter + FacebookAdapter

Ad Flow:
Palette.ShowRewardedAd() → MaxAdapter → OnRevenue → AdjustAdapter.TrackAdRevenue()

Remote Config:
Palette.GetRemoteConfig() → Firebase (if ready) → GA (fallback) → default
```

---

## Key Patterns

1. **Conditional Compilation**: `#if GAMEANALYTICS_INSTALLED`
2. **Static Adapters**: No DI, static classes wrap SDKs
3. **Mode-Based**: `SOROLLA_PROTOTYPE` or `SOROLLA_FULL` define
4. **Auto-Init**: `[RuntimeInitializeOnLoadMethod]`
5. **ScriptableObject Config**: `Resources/SorollaConfig.asset`

---

## Codebase Stats

| Category | Files | LOC |
|----------|-------|-----|
| Runtime | 15 | ~2,100 |
| Editor | 13 | ~1,800 |
| Debug UI | 31 | ~1,800 |
| **Total** | **59** | **~3,900** |

---

## Common RAG Queries

```
"TrackProgression API"        → ../api-reference.md, Palette.cs
"MAX ad not loading"          → ../troubleshooting.md
"Facebook SDK setup"          → ../prototype-setup.md
"Adjust attribution"          → ../full-setup.md, AdjustAdapter.cs
"Firebase crashlytics"        → ../firebase.md
"iOS ATT consent"             → ../troubleshooting.md
"mode switching"              → architecture.md
"SDK versions"                → SdkRegistry.cs
"competitor features"         → competitive-analysis.md
"roadmap priorities"          → product-roadmap.md
"developer pain points"       → market-research.md
```

---

## Memory Buffers

| File | Purpose |
|------|---------|
| `plan.md` | Current tasks, backlog, ADRs |
| `devlog.md` | Change history, learnings, hindsight |
| `competitive-analysis.md` | Competitor features, market position |
| `market-research.md` | Developer pain points, trends |
| `product-roadmap.md` | Feature prioritization, version plans |

---

## Quick Code Reference

```csharp
// Namespace
using Sorolla.Palette;

// Check state
Palette.IsInitialized
Palette.HasConsent
Palette.IsRewardedAdReady

// Analytics
Palette.TrackProgression(ProgressionStatus.Complete, "Level_001", score: 100);
Palette.TrackDesign("event:name", value);
Palette.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "item");

// Ads
Palette.ShowRewardedAd(onComplete, onFailed);
Palette.ShowInterstitialAd(onComplete);

// Remote Config
Palette.FetchRemoteConfig(success => { });
Palette.GetRemoteConfigInt("key", defaultValue);

// Crashlytics
Palette.LogException(ex);
Palette.LogCrashlytics("message");
```
