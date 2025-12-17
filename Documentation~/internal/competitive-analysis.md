# Competitive Analysis

> **Last Updated**: 2025-12-17
> **Status**: Active research

Analysis of competing mobile game publishing SDKs and market positioning.

---

## Executive Summary

Sorolla SDK competes in the mobile game publishing SDK market alongside established players like VoodooSauce and Homa Belly. Our differentiation lies in **zero-config simplicity** and **publisher-agnostic** design.

**Current Position**: Solid "80% solution" - covers core needs well but lacks advanced features.

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

### Other Players

#### Supersonic (Unity)
- Part of Unity/ironSource ecosystem
- Focus on hyper-casual publishing
- Strong UA and monetization optimization

#### GameAnalytics (Standalone)
- Free analytics platform
- D1-D30 retention, cohort analysis
- Remote config capabilities
- Widely used as foundation

#### Adjust (MMP)
- Attribution tracking leader
- Fraud prevention
- Deep linking
- Often paired with other SDKs

---

## Feature Comparison Matrix

| Feature | VoodooSauce | Homa Belly | Sorolla SDK |
|---------|-------------|------------|-------------|
| **Zero-Config Init** | ❌ | ❌ | ✅ Best-in-class |
| **Analytics** | ✅ | ✅ | ✅ |
| **Rewarded Ads** | ✅ | ✅ | ✅ |
| **Interstitial Ads** | ✅ | ✅ | ✅ |
| **Banner Ads** | ✅ | ✅ | ⚠️ Partial |
| **App Open Ads** | ✅ | ✅ | ❌ |
| **Attribution** | ✅ | ✅ | ✅ |
| **Remote Config** | ✅ | ✅ | ✅ |
| **A/B Testing UI** | ✅ | ✅ | ❌ |
| **Cross-Promotion** | ✅ | ✅ | ❌ |
| **User Segmentation** | ✅ | ✅ | ❌ |
| **LTV Prediction** | ✅ | ✅ | ❌ |
| **GDPR/UMP Consent** | ✅ | ✅ | ⚠️ ATT only |
| **IAP Tracking** | ✅ | ✅ | ❌ |
| **Deep Linking** | ✅ | ✅ | ❌ |
| **No-Code Config** | ⚠️ | ✅ | ⚠️ Partial |
| **Publisher-Agnostic** | ❌ | ❌ | ✅ |
| **Open Source** | ❌ | ❌ | ✅ |

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

## Monitoring

### Competitors to Watch
- [ ] VoodooSauce new features (monthly check)
- [ ] Homa SDK changelog (monthly check)
- [ ] Unity Gaming Services changes
- [ ] AppLovin MAX SDK updates

### Industry Trends
- [ ] Privacy regulations (GDPR, CCPA, DMA)
- [ ] Apple ATT changes
- [ ] Google Privacy Sandbox
- [ ] Ad mediation consolidation
