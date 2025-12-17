# Market Research

> **Last Updated**: 2025-12-17
> **Status**: Active research (expanded)

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
- Prototype: < 1 hour (industry benchmark: 10 minutes)
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

## SDK Documentation Best Practices

### Core Principles (Industry Research)

> "Thorough and well-structured documentation is the cornerstone of a user-friendly SDK."
> — Foresight Mobile

> "Ideally, you want your users to be able to integrate your SDK in less than 10 minutes."
> — Mike Smales, SDK Best Practices

### Documentation Requirements

1. **Installation** - Clear package manager or manual import steps
2. **Configuration** - All settings explained with defaults
3. **API Usage** - Every public method documented
4. **Troubleshooting** - Common issues with solutions

### DevEx Differentiation

> "The Developer Experience (DevEx) may make the difference between choosing your service or that of your competitor's."
> — Luciq AI

**Key DevEx Factors**:
- Intuitive, idiomatic experience
- Seamless integration with minimal code
- Platform conventions respected
- Cross-platform consistency

### SDK Design Principles

1. **Simple Initialization**: Should not take more than one line
2. **Platform Conventions**: Variable names, methods, design patterns should be consistent with what developers are familiar with
3. **Cross-Platform Consistency**: If Android has `getUser()`, iOS should too
4. **Sample Code**: Clear examples that demonstrate common use cases
5. **Error Handling**: Clear messages that explain cause and offer solutions

### Common Developer Frustrations

> "Common pain points include: having to sift through poorly organized or overly verbose documentation, being overwhelmed with options, and encountering inconsistencies in guides."
> — Foresight Mobile

**Anti-Patterns to Avoid**:
- Outdated documentation
- Missing examples
- Inconsistent naming across platforms
- No error message documentation
- Verbose setup procedures

### Privacy Documentation (Google Play Requirement)

> "If your SDK uses Personal and Sensitive user data, then you must ensure that you have made this clear in your public documentation."
> — Google Play SDK Best Practices

**Required Disclosures**:
- What user data the SDK collects
- Reason for data collection
- How apps should disclose to end users

---

## AppLovin MAX Best Practices (Detailed)

### Initialization

```csharp
// Recommended: Attach handler before init
MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdk.SdkConfiguration config) => {
    // SDK ready, start loading ads
};
MaxSdk.InitializeSdk();
```

**Key Rules**:
- Always initialize on startup (give networks time to cache)
- Call all MAX APIs on main thread
- Especially important for video ads

### Ad Format Specifics

**Banner Ads**:
- Minimum refresh interval: 10 seconds
- Maximum refresh interval: 120 seconds
- Set mute state BEFORE loading ads

**App Open Ads**:
- Implement frequency cap outside AppLovin
- Add cooldown based on user behavior

**Rewarded/Interstitial**:
- Retry with exponential backoff (up to 64 seconds)
- Pre-cache ads for instant availability

### Privacy Compliance

- MAX automates Google UMP integration
- SKAdNetwork: Plugin auto-updates Info.plist
- ATT: Notify App Store Connect reviewer that ATT is enabled for iOS 14.5+

### Technical Requirements

| Requirement | Details |
|-------------|---------|
| Unity | 2019.4 or later |
| Android | Jetifier enabled, Gradle 4.2.0+, compileSdkVersion 34+ |
| iOS | CocoaPods required, no bitcode (deprecated Xcode 14) |

---

## Firebase Best Practices (Detailed)

### Initialization

```csharp
// Use Firebase.Extensions for simpler callback handling
Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
    if (task.Result == Firebase.DependencyStatus.Available) {
        // Firebase ready
    }
});
```

### Crashlytics Setup

- Enable Google Analytics for breadcrumb logs
- iOS: Do NOT disable method swizzling
- Symbol upload required for native crash symbolication

**Symbol Upload**:
```bash
firebase crashlytics:symbols:upload --app=<FIREBASE_APP_ID> <PATH/TO/SYMBOLS>
```

### Remote Config

- Use `SetDefaultsAsync` for fallback values
- Call `FetchAndActivateAsync` explicitly
- Test with minimum fetch interval during development

### File Placement

- `google-services.json` → anywhere in Assets/
- `GoogleService-Info.plist` → anywhere in Assets/
- File names must be exact

---

## Adjust SDK Best Practices (Detailed)

### Deep Linking Setup

**iOS Universal Links**:
- Configure in Adjust dashboard
- Set up Associated Domains in Apple Developer Portal
- Add to Adjust prefab (remove protocol from URL)

**Android**:
- Add URI scheme to Adjust prefab

### Reattribution

```csharp
var deeplink = new AdjustDeeplink(url);
Adjust.ProcessDeeplink(deeplink);
```

### LinkMe (Deferred Deep Linking, iOS 15+)

- Reads deep link from pasteboard
- User sees permission dialog
- Enable via `IsLinkMeEnabled = true`

### SDK v5 Features

- Spoofing protection built-in
- `GetAttributionWithTimeout` method
- Android App Links support via prefab

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

### SDK Documentation & Best Practices
- [Foresight Mobile - SDK Best Practices](https://foresightmobile.com/blog/mobile-sdk-best-practices-for-developers)
- [Mike Smales - Building World Class SDKs](https://www.mikesmales.com/blog/best-practices-for-building-a-world-class-mobile-sdk)
- [Luciq AI - Mobile SDK Development](https://www.luciq.ai/blog/best-practices-for-developing-a-mobile-sdk)
- [Auth0 - Guiding Principles for SDKs](https://auth0.com/blog/guiding-principles-for-building-sdks/)
- [Google Play - SDK Best Practices](https://developer.android.com/guide/practices/sdk-best-practices)

### SDK Integration Guides
- [AppLovin MAX Unity Integration](https://support.axon.ai/en/max/unity/overview/integration/)
- [Firebase Crashlytics Unity](https://firebase.google.com/docs/crashlytics/unity/get-started)
- [Firebase Remote Config Codelab](https://firebase.google.com/codelabs/instrument-your-game-with-firebase-remote-config)
- [Adjust SDK Unity Guide](https://dev.adjust.com/en/sdk/unity/)
- [GameAnalytics Unity A/B Testing](https://docs.gameanalytics.com/integrations/sdk/unity/ab-testing/)

### Market Analysis
- [Mobio Group - SDK Leaders 2024](https://mobiogroup.com/android-and-ios-sdks-the-leaders-of-2024-mobio-group/)
- [Tenjin Ad Monetization Report 2025](https://tenjin.com/blog/ad-mon-gaming-2025/)
- [Statista - Attribution SDKs 2024](https://www.statista.com/statistics/1036027/leading-mobile-app-attribution-sdks-android/)
- [Appfigures - Top Monetization SDKs](https://appfigures.com/top-sdks/ads/games)

### Developer Pain Points
- [Embrace - Unity Developer Pain Points 2024](https://embrace.io/resources/unity-developer-pain-points/)
- [Embrace Blog - Understanding Unity Developer Pain Points](https://embrace.io/blog/qa-understanding-pain-points-unity-developers/)

### Industry Insights
- [Firebase A/B Testing](https://firebase.google.com/docs/ab-testing/abtest-config)
- [GameAnalytics Mobile Use Cases](https://www.gameanalytics.com/use-cases/mobile)
- [Adjust ATT Insights](https://www.adjust.com/blog/learning-from-hyper-casual-games-high-att-opt-in-rate/)
- [CrazyLabs 2024 Mobile Gaming Trends](https://www.crazylabs.com/blog/gaming-experts-reveal-mobile-gaming-trends/)
- [Homa SDK Documentation](https://sdk.homagames.com/docs/main/main.html)
- [Voodoo Publishing](https://voodoo.io/publishing)
- [ByteBrew Platform](https://bytebrew.io/)
