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
| API usage | `../api-reference.md` | `../prototype-setup.md` |
| SDK setup | `../prototype-setup.md` or `../full-setup.md` | - |
| Firebase | `../firebase.md` | - |
| Debug issue | `../troubleshooting.md` | - |
| Code changes | `architecture.md` | - |
| Competitive intel | `competitive-analysis.md` | `market-research.md` |
| Product planning | `product-roadmap.md` | - |
| GDPR/Consent | `../gdpr-consent-setup.md` | - |

### By Code Area

| Area | File | Key Classes |
|------|------|-------------|
| Public API | `Runtime/Palette.cs` | `Palette` |
| Auto-init | `Runtime/SorollaBootstrapper.cs` | `SorollaBootstrapper` |
| Ads (stub) | `Runtime/Adapters/MaxAdapter.cs` | `MaxAdapter` |
| Ads (impl) | `Runtime/Adapters/MAX/MaxAdapterImpl.cs` | `MaxAdapterImpl` |
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
Palette.TrackDesign() → GameAnalyticsAdapter + FirebaseAdapter

Ad Flow:
Palette.ShowRewardedAd() → MaxAdapter → OnRevenue → AdjustAdapter.TrackAdRevenue()

Remote Config:
Palette.GetRemoteConfig() → Firebase (if ready) → GA (fallback) → default

GDPR Consent:
MaxAdapter.Initialize() → CmpService → UMP consent form (EU/UK)
```

---

## Key Patterns

1. **Stub + Implementation**: Optional SDKs use separate assemblies with `defineConstraints`
2. **Static Adapters**: No DI, static classes wrap SDKs
3. **Mode-Based**: Prototype (GA only) or Full (GA + MAX + Adjust)
4. **Auto-Init**: `[RuntimeInitializeOnLoadMethod]`
5. **ScriptableObject Config**: `Resources/SorollaConfig.asset`

---

## Assembly Pattern (Critical)

```
Sorolla.Adapters.asmdef     ← Stubs (no external refs, always compiles)
Sorolla.Adapters.MAX.asmdef ← Impl (defineConstraints: APPLOVIN_MAX_INSTALLED)
```

**Why?** Unity resolves assembly refs before `#if` preprocessor evaluation.

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

// GDPR/Consent
Palette.ConsentStatus          // Unknown, NotApplicable, Required, Obtained, Denied
Palette.CanRequestAds          // True if ads allowed
Palette.ShowPrivacyOptions()   // Opens UMP form
```

---

## Common RAG Queries

```
"TrackProgression API"        → ../api-reference.md, Palette.cs
"MAX ad not loading"          → ../troubleshooting.md
"Adjust attribution"          → ../full-setup.md, AdjustAdapter.cs
"Firebase crashlytics"        → ../firebase.md
"iOS ATT consent"             → ../troubleshooting.md, ContextScreenView.cs
"GDPR consent"                → ../gdpr-consent-setup.md, MaxAdapter.cs
"mode switching"              → architecture.md
"SDK versions"                → SdkRegistry.cs
"competitor features"         → competitive-analysis.md
"roadmap priorities"          → product-roadmap.md
```

---

## Memory Buffers

| File | Purpose |
|------|---------|
| `devlog.md` | Change history, validated learnings |
| `competitive-analysis.md` | Competitor features, market position |
| `market-research.md` | Developer pain points, trends |
| `product-roadmap.md` | Feature prioritization, version plans, ADRs |
