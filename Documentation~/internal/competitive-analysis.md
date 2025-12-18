# Competitive Analysis

> **Last Updated**: 2025-12-17
> **Status**: Active research (expanded)

Analysis of competing mobile game publishing SDKs and market positioning.

---

## Executive Summary

Sorolla SDK competes in the mobile game publishing SDK market alongside established players like VoodooSauce, Homa Belly, CrazyLabs CLIK, and ByteBrew. Our differentiation lies in **zero-config simplicity** and **publisher-agnostic** design.

**Current Position**: Solid "80% solution" - covers core needs well but lacks advanced features.

**Key Insight**: The market is bifurcating between publisher-locked SDKs (Voodoo, Homa, CrazyLabs) and independent all-in-one platforms (ByteBrew). Sorolla occupies a unique middle ground: publisher-agnostic but not all-in-one.

---

## Competitor Profiles

### VoodooSauce (Voodoo)

**Company**: Voodoo - #10 on Pocket Gamer's Top 50 Mobile Game Developers 2024
**Scale**: 7+ billion downloads, 100+ games managed

**SDK Features**:
- Combined multiple ad network SDKs into one master package
- Video walkthrough support for integration
- Weekly prototyping with UA testing metrics
- Cross-promotion between Voodoo game portfolio
- Deep publisher integration

**Developer Experience**:
- SDK integration support via video calls
- Publishing OPS team helps troubleshoot technical issues
- Tight coupling to Voodoo publishing partnership

**Strengths**:
- Massive scale and data
- End-to-end publishing support
- Cross-promotion network

**Weaknesses**:
- Publisher lock-in required
- Not available for independent developers
- Less flexibility for custom workflows

**Source**: [Voodoo Publishing](https://voodoo.io/publishing)

---

### Homa Belly (Homa Games)

**Company**: Homa Games - Founded 2018
**Scale**: 80+ titles, 2+ billion downloads

**SDK Features**:
- No-code configuration via Unity Inspector
- N-testing (multi-variant A/B testing) for any game aspect
- Analytics, Ads, In-app Purchases, A/B Testing
- Assembly definitions for modular feature access
- Point-and-click configuration

**Technical Requirements**:
- Unity 2022.3 LTS, Unity 6 LTS, Unity 6.1
- Android API 24+ (target 34+), IL2CPP only
- iOS 13.0+, CocoaPods, Xcode 15.4+
- Stripping level must be "low" or below

**Strengths**:
- No-code configuration UI
- Strong A/B testing capabilities
- Modern Unity version support

**Weaknesses**:
- Strict technical requirements (IL2CPP only, low stripping)
- Publisher partnership model
- Complex assembly definition requirements

**Source**: [Homa SDK Documentation](https://sdk.homagames.com/docs/main/main.html)

---

### CrazyLabs CLIK

**Company**: CrazyLabs - #3 mobile game publisher globally
**Scale**: 6.5+ billion downloads, 250+ million MAU

**SDK Features (CLIK Plugin)**:
- 10-minute integration promise
- Seamless attribution, analytics, ads, IAP, A/B testing
- Privacy compliance built-in
- Cloud-based build system (connects to Git, builds remotely)
- Comprehensive testing: iOS, Google Play, Facebook, TikTok, Google Ads, SDK networks

**Unique Offerings**:
- **Publishing For All (PFA)**: Developers profit from games even if they fail to scale
- Access to 300+ gaming professionals (product, design, data analytics)
- Metrics beyond CPI: Day 1 retention, 24-hour playtime, CVR, CTR

**Strengths**:
- Robust testing infrastructure
- Developer-friendly revenue sharing
- Cloud build system

**Weaknesses**:
- Publisher partnership still required
- Less documentation publicly available

**Source**: [CrazyLabs Technology](https://www.crazylabs.com/technology/)

---

### ByteBrew (Independent All-in-One)

**Company**: ByteBrew - Free all-in-one platform
**Founded**: 2019, San Diego by Kian & Cameron Hozouri (former game developers)
**Funding**: $4M Seed (October 2022)
**Investors**: Konvoy Ventures (lead), Valhalla Ventures, Node Ventures
**Scale**: 10,000+ game developers

**SDK Features**:
- Real-time analytics (DAU, session length, retention)
- Monetization tracking (IAP + ad revenue)
- Attribution (integrated with top ad networks + SKAdNetwork)
- Remote configs (single values + grouped/rotating configs)
- A/B testing
- Push notifications (one-line integration)
- ATT handling built-in

**Technical Details**:
- Android 5.1+, iOS 9.0+
- Server-side purchase validation (fraud prevention)
- Impression-level ad tracking
- Creative-level campaign attribution

**Unique Features**:
- "Incredibly lightweight" - claims no other 3rd party SDK needed
- Rotating shop items via grouped configs
- Custom dashboard builders

**Business Model (Land & Expand)**:

*Current State*: 100% free, all features permanently free
> "ByteBrew was founded by previous game developers that experienced firsthand the struggle of having to both pay and integrate for multiple platforms."

*Future State*: Paid products coming
> "In the future we will be building new product lines that will be paid solutions, but none of our current platform technology will ever be charged for or restricted."

*Likely Future Paid Products*:
- User acquisition tools (ad buying/campaign management)
- Advanced attribution (deeper SKAN/privacy solutions)
- Predictive analytics (LTV prediction, churn modeling)
- Enterprise SLAs and custom dashboards

*Strategy*: Classic VC-backed "Slack/Figma playbook" - give away core product, become indispensable, monetize through expansion or acquisition.

**Strengths**:
- Completely free (for now)
- All-in-one (no additional SDKs)
- Publisher-independent

**Weaknesses**:
- VC-funded = eventual monetization pressure
- Less established brand
- Smaller data network than publishers
- No dedicated publishing support
- **Data ownership concern**: ByteBrew controls your analytics data layer

**Competitive Insight for Sorolla**:
ByteBrew validates "free SDK" as viable market position. Key differentiator: Sorolla wraps *your* accounts (GameAnalytics, MAX, Adjust) - you own the data and vendor relationships. ByteBrew owns the data layer, creating future lock-in risk.

**Source**: [ByteBrew SDK](https://bytebrew.io/), [ByteBrew Docs](https://docs.bytebrew.io/)

---

### LevelPlay/ironSource (Unity)

**Company**: Unity (acquired ironSource 2022)
**Scale**: Merged ecosystem, one of largest monetization platforms

**SDK Features**:
- Ad mediation across multiple networks
- Waterfall and bidding optimization
- Real-time analytics and reporting
- All ad formats (rewarded, interstitial, banner, offerwall)
- A/B testing capabilities

**Technical Details**:
- Deep Unity integration
- Supersonic Studios publishing arm
- Self-serve publishing platform

**Strengths**:
- Native Unity integration
- Massive scale
- Self-serve options available

**Weaknesses**:
- Complex ecosystem post-merger
- Primarily monetization-focused

---

### GameAnalytics (Foundation Layer)

**Company**: GameAnalytics - Free analytics platform
**Model**: Free core, premium features available

**SDK Features**:
- D1-D30 retention tracking
- Cohort analysis
- Remote configs with A/B testing
- Custom event tracking
- Level progression analytics

**A/B Testing API**:
```csharp
GameAnalytics.GetABTestingId();
GameAnalytics.GetABTestingVariantId();
GameAnalytics.IsRemoteConfigsReady();
GameAnalytics.OnRemoteConfigsUpdatedEvent += handler;
```

**Key Insight**: GA's remote config is available when `IsRemoteConfigsReady()` returns true. A/B test IDs depend on remote config readiness.

**Strengths**:
- Free and widely adopted
- Strong retention analytics
- Good remote config system

**Weaknesses**:
- No monetization features
- No attribution
- Must be paired with other SDKs

**Source**: [GameAnalytics Docs](https://docs.gameanalytics.com/)

---

### Attribution SDK Landscape

#### AppsFlyer (Market Leader)
- **Android**: 48% integration reach (Sep 2024)
- **iOS**: Leading position
- **Gaming**: 40% of gaming apps
- Features: Attribution, deep linking, fraud prevention

#### Adjust
- **Android**: ~30% market share
- **iOS**: 19% market share
- Features: Attribution, deep linking (LinkMe), reattribution, SKAN support
- SDK v5: Spoofing protection built-in

#### Branch Metrics
- **iOS**: 28% market share (higher than Adjust on iOS)
- Focus: Deep linking specialist

#### AppMetrica (Yandex)
- ~24% of apps use it
- Popular in certain regions

**Sorolla uses Adjust** - solid choice at #2 market position with strong features.

---

## Feature Comparison Matrix

### Publisher SDKs vs Sorolla

| Feature | VoodooSauce | Homa Belly | CrazyLabs CLIK | Sorolla SDK |
|---------|-------------|------------|----------------|-------------|
| **Zero-Config Init** | ❌ | ❌ | ❌ | ✅ Best-in-class |
| **Analytics** | ✅ | ✅ | ✅ | ✅ |
| **Rewarded Ads** | ✅ | ✅ | ✅ | ✅ |
| **Interstitial Ads** | ✅ | ✅ | ✅ | ✅ |
| **Banner Ads** | ✅ | ✅ | ✅ | ⚠️ Partial |
| **App Open Ads** | ✅ | ✅ | ✅ | ❌ |
| **Attribution** | ✅ | ✅ | ✅ | ✅ |
| **Remote Config** | ✅ | ✅ | ✅ | ✅ |
| **A/B Testing UI** | ✅ | ✅ (N-testing) | ✅ | ❌ |
| **Cross-Promotion** | ✅ | ✅ | ✅ | ❌ |
| **User Segmentation** | ✅ | ✅ | ✅ | ❌ |
| **LTV Prediction** | ✅ | ✅ | ✅ | ❌ |
| **GDPR/UMP Consent** | ✅ | ✅ | ✅ | ⚠️ ATT only |
| **IAP Tracking** | ✅ | ✅ | ✅ | ❌ |
| **Deep Linking** | ✅ | ✅ | ✅ | ❌ |
| **Cloud Build** | ❌ | ❌ | ✅ | ❌ |
| **Publisher-Agnostic** | ❌ | ❌ | ❌ | ✅ |
| **Open Source** | ❌ | ❌ | ❌ | ✅ |

### Independent SDKs vs Sorolla

| Feature | ByteBrew | GameAnalytics | Sorolla SDK |
|---------|----------|---------------|-------------|
| **Zero-Config Init** | ❌ | ❌ | ✅ Best-in-class |
| **Analytics** | ✅ | ✅ | ✅ |
| **Ads Integration** | ❌ (tracking only) | ❌ | ✅ |
| **Attribution** | ✅ | ❌ | ✅ |
| **Remote Config** | ✅ (grouped configs) | ✅ | ✅ |
| **A/B Testing** | ✅ | ✅ | ❌ |
| **IAP Tracking** | ✅ (with validation) | ❌ | ❌ |
| **Push Notifications** | ✅ | ❌ | ❌ |
| **Crashlytics** | ❌ | ❌ | ✅ (Firebase) |
| **Free** | ✅ | ✅ | ✅ |
| **Publisher-Agnostic** | ✅ | ✅ | ✅ |
| **Open Source** | ❌ | ❌ | ✅ |

### Technical Requirements Comparison

| Requirement | Homa Belly | Sorolla SDK | Notes |
|-------------|------------|-------------|-------|
| Unity Version | 2022.3 LTS+ | 2022.3 LTS+ | Same |
| Android Min | API 24 | API 21 | Sorolla more permissive |
| Android Target | API 34+ | API 34+ | Same |
| iOS Min | 13.0+ | 12.0+ | Sorolla more permissive |
| Scripting Backend | IL2CPP only | Mono or IL2CPP | Sorolla more flexible |
| Stripping Level | Low or below | Any | Sorolla more flexible |
| CocoaPods | Required | Required | Same |

---

## Market Positioning

### Current Sorolla Position
```
                    Feature Richness
                          ↑
                          │
    VoodooSauce ●─────────┼─────────● Homa Belly
                          │
                          │
                          │    ● Sorolla (current)
                          │
    ──────────────────────┼────────────────────→
    Publisher Lock-in                  Independence
```

### Target Position
```
                    Feature Richness
                          ↑
                          │
    VoodooSauce ●─────────┼─────────● Homa Belly
                          │
                          │         ● Sorolla (target)
                          │
                          │
    ──────────────────────┼────────────────────→
    Publisher Lock-in                  Independence
```

---

## Competitive Advantages

### What We Do Better

1. **Zero-Config Auto-Init**
   - No competitor matches our "install and done" experience
   - RuntimeInitializeOnLoadMethod pattern is unique

2. **Mode System (Prototype/Full)**
   - Clean separation for studio workflows
   - Easy switching between UA testing and production

3. **Publisher Independence**
   - Works with any publisher or self-publishing
   - No revenue share or partnership requirements

4. **Open Source Transparency**
   - Full source code visibility
   - Community contributions welcome

5. **AI-Agent Optimized**
   - Documentation designed for automated development
   - RAG-ready structure

### What Competitors Do Better

1. **A/B Testing UI** - Homa's no-code N-testing is superior
2. **Cross-Promotion** - VoodooSauce's portfolio leverage
3. **User Segmentation** - Both have advanced targeting
4. **LTV Prediction** - Data science capabilities
5. **Scale & Data** - Billions of data points for optimization

---

## Strategic Recommendations

### Short-Term (v2.2-2.3)
1. Close feature gaps: Banner ads, App Open ads, GDPR/UMP
2. Add IAP tracking for LTV calculation
3. Maintain DX advantage with better error messages

### Medium-Term (v3.0)
1. Add A/B test assignment API
2. User properties/segmentation
3. Cross-promotion framework

### Long-Term Differentiation
1. **"Zero to Revenue in 10 Minutes"** - Own the simplicity narrative
2. **AI-First Development** - Best tooling for AI-assisted game dev
3. **Community-Driven** - Open source contributions

---

## Market Statistics (2024)

### Publisher Scale
| Publisher | Downloads | MAU | Games |
|-----------|-----------|-----|-------|
| Voodoo | 7B+ | - | 100+ |
| CrazyLabs | 6.5B+ | 250M+ | - |
| Homa Games | 2B+ | - | 80+ |

### Market Share (Hyper-Casual)
- Hyper-casual games: ~33% of all mobile game downloads (2024)
- **Rollic** leading hybrid-casual transition
- **Abi Game, Boombit, Lion, Voodoo** following
- **Tapnation, Supersonic, Supercent, Kwalee** holding ad revenue positions

### SDK Market Share (Analytics)
| SDK | Android Gaming | Notes |
|-----|----------------|-------|
| Facebook Analytics | 25% | 1 in 4 gaming apps |
| GameAnalytics | 11% | Gaming-focused |
| Flurry | <1% Android, 19% iOS | Platform variance |
| Others | <3% each | Fragmented |

### Monetization Trends
- **Hybrid monetization** (ads + IAP + subscriptions) growing
- **eCPM increasing** due to hybrid-casual games
- **In-app bidding** now dominant over waterfall

---

## Monitoring

### Competitors to Watch
- [ ] VoodooSauce new features (monthly check)
- [ ] Homa SDK changelog (monthly check)
- [ ] CrazyLabs CLIK updates (monthly check)
- [ ] ByteBrew new features (monthly check)
- [ ] Unity Gaming Services changes
- [ ] AppLovin MAX SDK updates

### Industry Trends
- [ ] Privacy regulations (GDPR, CCPA, DMA)
- [ ] Apple ATT changes
- [ ] Google Privacy Sandbox for Android
- [ ] Ad mediation consolidation
- [ ] Hybrid-casual game growth
- [ ] AI integration in SDKs

---

## Sources

- [Voodoo Publishing](https://voodoo.io/publishing)
- [Homa SDK Documentation](https://sdk.homagames.com/docs/main/main.html)
- [CrazyLabs Technology](https://www.crazylabs.com/technology/)
- [ByteBrew SDK](https://bytebrew.io/)
- [AppLovin MAX Unity](https://support.axon.ai/en/max/unity/overview/integration/)
- [GameAnalytics Docs](https://docs.gameanalytics.com/)
- [Adjust Developer Hub](https://dev.adjust.com/en/sdk/unity/)
- [Mobio Group SDK Analysis 2024](https://mobiogroup.com/android-and-ios-sdks-the-leaders-of-2024-mobio-group/)
- [Gamigion Top Publishers 2024](https://www.gamigion.com/2024-top-hypercasual-mobile-game-publishers/)
