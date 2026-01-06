# Product Roadmap

> **Last Updated**: 2026-01-06
> **Current Version**: 2.3.3

Strategic feature planning and prioritization for Sorolla SDK.

---

## Vision

**"Zero to Revenue in 10 Minutes"**

Sorolla SDK enables game developers to integrate analytics, ads, and attribution with minimal effort, while maintaining flexibility and publisher independence.

---

## Completed Features (v2.0 - v2.3)

### v2.3.x - Stub + Implementation Architecture
- **Namespace refactor**: `SorollaSDK` → `Palette` class, `Sorolla.Palette` namespace
- **Assembly pattern**: Stub + Implementation with separate assemblies for optional SDKs
- **IL2CPP protection**: `[AlwaysLinkAssembly]` + `[Preserve]` attributes
- **Facebook SDK**: Required for prototype mode UA campaigns
- **Debug UI as sample**: Moved to `Samples~/DebugUI`
- **Unity 6 LTS**: Fully supported and tested

### v2.2.x - Privacy & Compliance
- **UMP Consent Integration**: GDPR/ATT consent via MAX SDK
- **Consent API**: `Palette.ConsentStatus`, `CanRequestAds`, `ShowPrivacyOptions()`
- **Build Validator**: Pre-build checks with auto-fix for manifest issues
- **App Store Privacy Guide**: Documentation for prototype submissions

### v2.1.x - Firebase Integration
- **Firebase Analytics**: Event tracking forwarding
- **Firebase Crashlytics**: Crash reporting with breadcrumbs
- **Firebase Remote Config**: With GA fallback chain

### v2.0.x - Foundation
- **Prototype/Full modes**: Clean separation for different workflows
- **GameAnalytics integration**: Always included
- **AppLovin MAX**: Ad mediation (rewarded, interstitial)
- **Adjust attribution**: Production attribution tracking
- **Auto-initialization**: `[RuntimeInitializeOnLoadMethod]`

---

## Version Roadmap

### v2.4.0 - Ad Format Completion (Next)

**Theme**: Complete ad format coverage

| Feature | Priority | Status |
|---------|----------|--------|
| Banner Ads (MAX) | P0 | Planned |
| App Open Ads (MAX) | P1 | Planned |
| Ad Frequency Capping API | P1 | Planned |
| Impression-Level Revenue Callbacks | P2 | Planned |

**API Additions**:
```csharp
// Banner Ads
Palette.ShowBanner(BannerPosition position = BannerPosition.Bottom);
Palette.HideBanner();
Palette.IsBannerVisible { get; }

// App Open Ads
Palette.ShowAppOpenAd(Action onComplete = null);
Palette.IsAppOpenAdReady { get; }

// Frequency Capping
Palette.SetInterstitialCooldown(float seconds);
Palette.SetMaxInterstitialsPerSession(int count);

// Revenue Callbacks
Palette.OnAdRevenue += (AdRevenueInfo info) => { };
```

---

### v2.5.0 - Economy Tracking

**Theme**: IAP and economy analytics

| Feature | Priority | Status |
|---------|----------|--------|
| IAP Tracking | P0 | Planned |
| Purchase Validation | P1 | Planned |
| Virtual Currency Tracking | P2 | Planned |

**API Additions**:
```csharp
// IAP Tracking
Palette.TrackPurchase(string productId, decimal price, string currency, string receipt = null);
Palette.TrackSubscription(string productId, decimal price, string currency, SubscriptionPeriod period);
```

---

### v3.0.0 - Advanced Features

**Theme**: Close feature gap with competitors

| Feature | Priority | Status |
|---------|----------|--------|
| A/B Test Assignment API | P0 | Planned |
| User Properties/Segmentation | P1 | Planned |
| Deep Linking (Adjust) | P1 | Planned |
| Interface-Based Adapters | P2 | Planned |

**API Additions**:
```csharp
// A/B Testing
Palette.GetTestVariant(string testName, string defaultVariant);
Palette.IsInTestGroup(string testName, string variant);

// User Properties
Palette.SetUserProperty(string key, string value);
Palette.SetUserId(string userId);

// Deep Linking
Palette.OnDeepLink += (DeepLinkData data) => { };
```

---

## Feature Prioritization Framework

### Priority Levels

| Level | Criteria | Examples |
|-------|----------|----------|
| **P0** | Must-have for next release, blocking issue, legal requirement | Banner ads, critical bugs |
| **P1** | High value, clear user demand, competitive parity | App Open ads, IAP tracking |
| **P2** | Nice to have, differentiator, or tech debt | Interface refactor, LTV prediction |
| **P3** | Future consideration, exploratory | Cross-promotion, ML features |

---

## Technical Debt Backlog

| Item | Impact | Effort | Notes |
|------|--------|--------|-------|
| Interface-based adapters | High | 2 weeks | Enables testability, runtime switching |
| Async/await pattern adoption | Medium | 1 week | Modern C# patterns |
| Error message improvements | Low | 2-3 days | Better DX for misconfiguration |

---

## Architecture Decisions

### ADR-001: Stub + Implementation Pattern for SDKs
- **Status**: Accepted (v2.3.0)
- **Context**: Unity resolves assembly references before `#if` preprocessor evaluation
- **Decision**: Separate assemblies with `defineConstraints` + `versionDefines`
- **Consequences**: SDK compiles cleanly without optional dependencies installed

### ADR-002: ScriptableObject Config
- **Status**: Accepted
- **Context**: Need persistent, editable configuration
- **Decision**: SorollaConfig as ScriptableObject in Resources
- **Consequences**: Easy editor integration, loaded once at startup

### ADR-003: Namespace Refactor (Palette)
- **Status**: Accepted (v2.3.3)
- **Context**: `SorollaSDK` class name was verbose for common API calls
- **Decision**: Rename to `Palette` class in `Sorolla.Palette` namespace
- **Consequences**: Cleaner API (`Palette.ShowRewardedAd()` vs `SorollaSDK.ShowRewardedAd()`)

---

## Risks & Mitigations

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Unity version breaking changes | Medium | High | Test on LTS releases early |
| Firebase SDK conflicts | Medium | Medium | Version pinning, stub pattern |
| iOS privacy changes | High | Medium | Proactive ATT/consent updates |

### Market Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Competitor feature leapfrog | Medium | Medium | Focus on DX differentiator |
| Privacy regulation changes | High | High | Modular consent architecture |

---

## Release Checklist Template

### Pre-Release
- [ ] All features implemented and tested
- [ ] Documentation updated (public + internal)
- [ ] CHANGELOG.md updated
- [ ] Version bumped in package.json
- [ ] Breaking changes documented

### Release
- [ ] Git tag created
- [ ] GitHub release published

### Post-Release
- [ ] Monitor GitHub issues
- [ ] Update roadmap based on learnings
