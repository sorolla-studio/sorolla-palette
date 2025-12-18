# Product Roadmap

> **Last Updated**: 2025-12-17
> **Current Version**: 2.1.0

Strategic feature planning and prioritization for Sorolla SDK.

---

## Vision

**"Zero to Revenue in 10 Minutes"**

Sorolla SDK enables game developers to integrate analytics, ads, and attribution with minimal effort, while maintaining flexibility and publisher independence.

---

## Version Roadmap

### v2.2.0 - Ad Format Completion (Next Release)

**Theme**: Complete ad format coverage for production readiness

| Feature | Priority | Effort | Status |
|---------|----------|--------|--------|
| Banner Ads (MAX) | P0 | 2-3 days | Planned |
| App Open Ads (MAX) | P0 | 2-3 days | Planned |
| Ad Frequency Capping | P1 | 1-2 days | Planned |
| Impression-Level Revenue | P1 | 2-3 days | Planned |
| Unity 6 Official Support | P1 | 1-2 days | Planned |

**API Additions**:
```csharp
// Banner Ads
SorollaSDK.ShowBanner(BannerPosition position = BannerPosition.Bottom);
SorollaSDK.HideBanner();
SorollaSDK.IsBannerVisible { get; }

// App Open Ads
SorollaSDK.ShowAppOpenAd(Action onComplete = null);
SorollaSDK.IsAppOpenAdReady { get; }

// Frequency Capping
SorollaSDK.SetInterstitialCooldown(float seconds);
SorollaSDK.SetMaxInterstitialsPerSession(int count);

// Revenue Callbacks
SorollaSDK.OnAdRevenue += (AdRevenueData data) => { };
```

**Target Date**: Q1 2025

---

### v2.3.0 - Privacy & Economy

**Theme**: Legal compliance and economy tracking

| Feature | Priority | Effort | Status |
|---------|----------|--------|--------|
| GDPR/UMP Consent Flow | P0 | 3-5 days | Planned |
| IAP Tracking | P0 | 2-3 days | Planned |
| TCF 2.0 Support | P1 | 2-3 days | Planned |
| Economy Dashboard Helpers | P2 | 3-5 days | Planned |

**API Additions**:
```csharp
// Consent
SorollaSDK.ShowConsentDialog(Action<ConsentStatus> onComplete);
SorollaSDK.ConsentStatus { get; }
SorollaSDK.HasConsentForAds { get; }
SorollaSDK.HasConsentForAnalytics { get; }

// IAP Tracking
SorollaSDK.TrackPurchase(string productId, decimal price, string currency, string receipt = null);
SorollaSDK.TrackSubscription(string productId, decimal price, string currency, SubscriptionPeriod period);
```

**Target Date**: Q2 2025

---

### v3.0.0 - Advanced Features

**Theme**: Close feature gap with competitors

| Feature | Priority | Effort | Status |
|---------|----------|--------|--------|
| A/B Test Assignment API | P0 | 3-5 days | Planned |
| User Properties/Segmentation | P0 | 1 week | Planned |
| Deep Linking (Adjust) | P1 | 3-5 days | Planned |
| Cross-Promotion Framework | P1 | 1 week | Planned |
| Interface-Based Adapters | P2 | 2 weeks | Planned |

**API Additions**:
```csharp
// A/B Testing
SorollaSDK.GetTestVariant(string testName, string defaultVariant);
SorollaSDK.IsInTestGroup(string testName, string variant);

// User Properties
SorollaSDK.SetUserProperty(string key, string value);
SorollaSDK.SetUserId(string userId);
SorollaSDK.GetUserSegment();

// Deep Linking
SorollaSDK.OnDeepLink += (DeepLinkData data) => { };
SorollaSDK.GetDeferredDeepLink(Action<DeepLinkData> callback);

// Cross-Promotion
SorollaSDK.ShowCrossPromo(string gameId, Action onComplete);
SorollaSDK.GetCrossPromoGames(Action<List<CrossPromoGame>> callback);
```

**Target Date**: Q3 2025

---

### v3.1.0 - Intelligence Layer

**Theme**: Data-driven insights and automation

| Feature | Priority | Effort | Status |
|---------|----------|--------|--------|
| LTV Prediction Helpers | P1 | 2 weeks | Planned |
| Retention Cohort Export | P2 | 1 week | Planned |
| Churn Risk API | P2 | 1 week | Planned |
| Smart Ad Placement Suggestions | P2 | 2 weeks | Planned |

**Target Date**: Q4 2025

---

## Feature Prioritization Framework

### Priority Levels

| Level | Criteria | Examples |
|-------|----------|----------|
| **P0** | Must-have for next release, blocking issue, or legal requirement | GDPR consent, critical bug fixes |
| **P1** | High value, clear user demand, competitive parity | Banner ads, IAP tracking |
| **P2** | Nice to have, differentiator, or tech debt | LTV prediction, interface refactor |
| **P3** | Future consideration, exploratory | Playable ads, ML features |

### Evaluation Criteria

| Criterion | Weight | Description |
|-----------|--------|-------------|
| User Impact | 30% | How many developers benefit |
| Revenue Impact | 25% | Direct/indirect revenue effect |
| Competitive Parity | 20% | Closing gap with competitors |
| Development Effort | 15% | Time and complexity |
| Strategic Alignment | 10% | Fits long-term vision |

---

## Current Sprint (v2.2.0 Development)

### In Progress
- [ ] Banner ads implementation
- [ ] App Open ads implementation

### Ready for Development
- [ ] Ad frequency capping
- [ ] Unity 6 compatibility testing

### Blocked
- None

### Completed This Sprint
- [x] Documentation reorganization (public/internal)
- [x] Competitive analysis research
- [x] Market research compilation

---

## Technical Debt Backlog

| Item | Impact | Effort | Notes |
|------|--------|--------|-------|
| Interface-based adapters | High | 2 weeks | Enables runtime SDK switching, testability |
| Unit tests for adapters | Medium | 1 week | Currently no automated tests |
| Async/await pattern adoption | Medium | 1 week | Modern C# patterns |
| Error message improvements | Low | 2-3 days | Better DX for misconfiguration |
| Firebase init timeout handling | Low | 1-2 days | Prevent infinite loading |

---

## Risks & Mitigations

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Unity 6 breaking changes | Medium | High | Early testing, compatibility layer |
| Firebase SDK conflicts | Medium | Medium | Version pinning, isolation |
| iOS privacy changes | High | Medium | Proactive ATT/consent updates |
| Android API requirements | Medium | Low | Track Google Play requirements |

### Market Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Competitor feature leapfrog | Medium | Medium | Focus on DX differentiator |
| Ad network consolidation | Low | Medium | Multi-network support |
| Privacy regulation changes | High | High | Modular consent architecture |

---

## Success Metrics

### v2.2.0 Goals
- [ ] All ad formats working (banner, app open)
- [ ] No regression in existing features
- [ ] Documentation updated
- [ ] < 5% increase in SDK size

### v2.3.0 Goals
- [ ] GDPR/UMP compliant
- [ ] IAP tracking with LTV calculation
- [ ] 90% of competitor table stakes features

### v3.0.0 Goals
- [ ] Feature parity with Homa Belly core features
- [ ] Interface-based architecture
- [ ] Community contributions enabled

---

## Release Checklist Template

### Pre-Release
- [ ] All features implemented and tested
- [ ] Documentation updated
- [ ] CHANGELOG.md updated
- [ ] Version bumped in package.json
- [ ] Breaking changes documented
- [ ] Migration guide (if needed)

### Release
- [ ] Tag created
- [ ] Release notes published
- [ ] Announcement prepared

### Post-Release
- [ ] Monitor GitHub issues
- [ ] Collect user feedback
- [ ] Update roadmap based on learnings
