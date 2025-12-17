# Market Research

> **Last Updated**: 2025-12-17
> **Status**: Active research

Developer pain points, industry trends, and best practices for mobile game publishing SDKs.

---

## Developer Pain Points

### Critical Issues (High Impact, High Frequency)

| Pain Point | Impact | Developer Quotes/Evidence |
|------------|--------|---------------------------|
| **SDK dependency conflicts** | Build failures, days of debugging | Multiple SDK versions clash |
| **Android Gradle/manifest merging** | Platform-specific build issues | Third-party plugin conflicts |
| **Multiple SDK integration overhead** | Weeks of development time | Each SDK has unique patterns |
| **Documentation quality** | Onboarding friction, support tickets | Incomplete, outdated docs |

### Significant Issues (Medium-High Impact)

| Pain Point | Impact | Notes |
|------------|--------|-------|
| Version compatibility with Unity | Upgrade blocking | SDKs lag Unity releases |
| GDPR/ATT consent complexity | Legal compliance risk | Multiple consent flows |
| Testing without live environment | QA difficulty | Can't test ads in editor |
| App size bloat | User acquisition impact | Each SDK adds 2-10MB |
| Update fatigue | Maintenance burden | Monthly SDK updates |

### Developer Needs by Stage

**Prototype Stage**:
- Quick integration (< 1 hour)
- Basic analytics (retention, events)
- Optional ad testing
- No complex configuration

**Soft Launch Stage**:
- Full analytics with cohorts
- A/B testing capabilities
- Attribution tracking
- Remote config for tuning

**Production Stage**:
- Complete ad mediation
- Revenue optimization
- User segmentation
- LTV prediction
- Cross-promotion

---

## Industry Best Practices

### SDK Integration (AppLovin MAX)

**Initialization**:
> "Always initialize the AppLovin SDK on startup to give mediated networks time to cache ads."

**Ad Loading**:
> "Retry with exponentially higher delays, up to 64 seconds, when ads fail to load."

**App Open Ads**:
> "Use a frequency cap managed outside AppLovin for full control. Implement a cooldown period based on user behavior."

**Banner Ads**:
> "Minimum refresh interval is 10 seconds, maximum is 120 seconds."

**Source**: [AppLovin MAX Integration Guide](https://support.axon.ai/en/max/unity/overview/integration/)

### A/B Testing (Firebase)

**Hypothesis-Driven**:
> "Before starting an AB test, clearly define the hypotheses you want to test."

**Single Variable**:
> "Do not put config values for unrelated parameters in a variant."

**Metrics to Track**:
> "Track in-app purchases and ad revenue while also tracking stability and user retention."

**Sample Sizing**:
> "Start with a small percentage of your user base, and increase over time."

**Control Groups**:
> "Always run control groups alongside your experiments."

**Source**: [Firebase A/B Testing](https://firebase.google.com/docs/ab-testing/abtest-config)

### Analytics (GameAnalytics)

**Retention Tracking**:
- D1 to D30 retention curves
- Session length and frequency
- Churn prediction patterns

**Cohort Analysis**:
- Group by acquisition date
- Compare by source/campaign
- Track behavior changes over time

**LTV Calculation**:
- Revenue per user over lifetime
- Segment by spending habits
- Predict future value

**Source**: [GameAnalytics Mobile Use Cases](https://www.gameanalytics.com/use-cases/mobile)

### Privacy & Consent

**ATT Opt-In Rates** (2024):
- Industry average: 26%
- Gaming vertical: 32%
- Hyper-casual games: 39%
- Cross-promotion: 45%

**Key Insight**:
> "Users are significantly more likely to consent when referred to another app developed by the same company."

**IDFV Strategy**:
> "Following iOS 14 privacy changes, larger gaming studios started buying smaller ones to expand their catalog and gain access to first-party data via IDFV."

**Source**: [Adjust ATT Insights](https://www.adjust.com/blog/learning-from-hyper-casual-games-high-att-opt-in-rate/)

---

## Technology Trends

### Unity Ecosystem (2024-2025)

**Supported Versions**:
- Unity 2022.3 LTS (widely adopted)
- Unity 6 LTS (emerging)
- Unity 6.1 (cutting edge)

**Build Requirements**:
- IL2CPP becoming standard for mobile
- Android API 34+ target requirement
- iOS 13.0+ minimum

**Scripting**:
- Assembly definitions for modularity
- Async/await patterns for SDK operations
- Addressables for asset management

### Privacy Regulations

**Current**:
- iOS ATT (App Tracking Transparency)
- GDPR (EU)
- CCPA (California)

**Emerging**:
- Google Privacy Sandbox for Android
- Digital Markets Act (EU)
- State-level US privacy laws

**SDK Impact**:
- UMP (User Messaging Platform) becoming required
- TCF 2.0 (Transparency and Consent Framework)
- First-party data strategies

### Ad Mediation Evolution

**Bidding vs Waterfall**:
- In-app bidding now dominant
- Real-time auctions increase CPMs
- Waterfall being phased out

**Key Networks**:
- AppLovin (MAX)
- Google AdMob
- Meta Audience Network
- Unity Ads
- ironSource (Unity)

**Revenue Optimization**:
- Impression-level revenue data
- Ad placement optimization
- Frequency capping strategies

---

## What Developers Want

### Top Feature Requests (Industry-Wide)

1. **Simpler Integration** - One SDK for everything
2. **Better Documentation** - Up-to-date, with examples
3. **Testing Tools** - Test ads/analytics without live data
4. **Version Compatibility** - Work with latest Unity
5. **Smaller SDK Size** - Minimize app bloat
6. **Transparent Pricing** - No hidden fees or revenue share

### Developer Experience Expectations

**Integration Time**:
- Prototype: < 1 hour
- Full setup: < 1 day
- Updates: < 30 minutes

**Documentation**:
- Quick start guide
- API reference with examples
- Troubleshooting section
- Video tutorials (preferred)

**Support**:
- Discord/Slack community
- GitHub issues
- Stack Overflow tags
- Response time < 24 hours

---

## Implications for Sorolla SDK

### Must-Have Features (Table Stakes)
- [x] Analytics (GA integration)
- [x] Rewarded/Interstitial ads
- [x] Attribution (Adjust)
- [x] Remote Config
- [ ] Banner ads (partial)
- [ ] GDPR/UMP consent
- [ ] IAP tracking

### Differentiators to Maintain
- [x] Zero-config auto-init
- [x] Mode system (Prototype/Full)
- [x] Publisher independence
- [x] AI-agent optimization

### Growth Opportunities
- [ ] A/B test exposure API
- [ ] User segmentation
- [ ] Cross-promotion framework
- [ ] LTV prediction helpers

---

## Research Sources

- [AppLovin MAX Best Practices](https://support.axon.ai/en/max/unity/overview/integration/)
- [Firebase A/B Testing](https://firebase.google.com/docs/ab-testing/abtest-config)
- [GameAnalytics Mobile](https://www.gameanalytics.com/use-cases/mobile)
- [Adjust ATT Insights](https://www.adjust.com/blog/learning-from-hyper-casual-games-high-att-opt-in-rate/)
- [ThinkingData Analytics Tools](https://thinkingdata.io/blog/7-best-mobile-game-analytics-tools-for-data-driven-growth-in-2025/)
- [Mobile Game A/B Testing Strategies](https://docs.getjoystick.com/knowledge-mobile-game-ab-testing-strategies/)
- [Homa SDK Documentation](https://sdk.homagames.com/docs/main/main.html)
- [Voodoo Publishing](https://voodoo.io/publishing)
