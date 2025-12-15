# SDK Setup Overview

The Sorolla SDK supports two operational modes, each with different SDK requirements. Choose the guide that matches your development stage.

## Choose Your Setup Guide

### 🚀 [Prototype Mode Setup](Prototype-Mode-Setup.md)

**Best for:** Rapid UA testing during early development

**Required SDKs:**
- GameAnalytics (analytics & remote config)
- Facebook SDK (user acquisition testing)

**Optional SDKs:**
- AppLovin MAX (ad monetization testing)

**Key Features:**
- Quick setup for UA campaign testing
- Facebook attribution for prototype testing
- Minimal configuration required

[→ Follow Prototype Mode Setup Guide](Prototype-Mode-Setup.md)

---

### 🎯 [Full Mode Setup](Full-Mode-Setup.md)

**Best for:** Production apps ready for launch

**Required SDKs:**
- GameAnalytics (analytics & remote config)
- AppLovin MAX (ad monetization)
- Adjust (attribution tracking)

**Key Features:**
- Full attribution tracking with Adjust
- Production-grade ad monetization
- Complete analytics and remote config

**Note:** Full Mode does NOT use Facebook SDK - it uses Adjust for attribution.

[→ Follow Full Mode Setup Guide](Full-Mode-Setup.md)

---

### 🔥 [Firebase Add-on Setup](FirebaseSetup.md)

**Compatible with:** Both Prototype and Full Mode

**Includes:**
- Google Analytics for Firebase
- Firebase Crashlytics
- Firebase Remote Config

[→ Follow Firebase Setup Guide](FirebaseSetup.md)

---

## Quick Comparison

| Feature | Prototype Mode | Full Mode |
|---------|---------------|-----------|
| **GameAnalytics** | ✅ Required | ✅ Required |
| **Facebook SDK** | ✅ Required | ❌ Not used |
| **AppLovin MAX** | ⚪ Optional | ✅ Required |
| **Adjust** | ❌ Not used | ✅ Required |
| **Firebase** | ⚪ Optional | ⚪ Optional |
| **Best For** | Early UA testing | Production launch |
| **Attribution** | Facebook | Adjust |

---

## Need Help?

- 📖 [Documentation Index](index.md)
- 🐙 [GitHub Repository](https://github.com/LaCreArthur/sorolla-palette-upm)
- 🐛 [Report Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)

---

**Tip:** Follow the setup guide for your chosen mode step-by-step. Both guides include critical warnings and production-ready checklists to ensure proper configuration.
