# Full Mode Setup Guide

This guide covers SDK setup for **Full Mode** - designed for production apps with full attribution and monetization.

## Required SDKs

- **GameAnalytics** - Analytics & Remote Config (Required)
- **AppLovin MAX** - Ad Monetization (Required)
- **Adjust** - Attribution Tracking (Required)

**Note:** Full Mode does NOT use Facebook SDK - it uses Adjust for attribution instead.

---

## 1. GameAnalytics Setup (Required)

### Create Account & Game Project

1. Go to [https://gameanalytics.com](https://gameanalytics.com)
2. Click **"Sign Up"** and complete registration
3. Click **"Add Game"** or **"Create Game"**
4. Fill in game details:
   - **Game Name**: Your game's name
   - **Platform**: Select iOS and/or Android
   - **Engine**: Select Unity
5. Click **"Create Game"**

### Get API Keys

1. Navigate to **Settings** → **Game Settings** (gear icon)
2. Copy both keys:
   - **Game Key**: Hexadecimal string (e.g., `a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4`)
   - **Secret Key**: Another hexadecimal string
3. **Keep these keys secure**

### Configure in Unity

1. Open **Window** → **GameAnalytics** → **Select Settings**
2. Paste **Game Key** and **Secret Key**
3. Click **"Save"**

**Alternative:** Use the **"Open Settings"** button in the Sorolla Configuration window.

---

## 2. AppLovin MAX Setup (Required)

AppLovin MAX provides ad mediation and monetization for production apps.

**Follow the full guide:** [Ads Setup (AppLovin MAX)](ads-setup.md)

---

## 3. Adjust Setup (Required)

Adjust provides attribution tracking and campaign analytics for production.

### Create Account

1. Go to [https://www.adjust.com](https://www.adjust.com)
2. Click **"Get Started"** or **"Sign Up"**
3. Complete registration and verify email
4. Log in to dashboard

### Create App

1. Click **"+ Create App"** or go to **Apps** → **"New App"**
2. Fill in app details:
   - **App Name**: Your game's name
   - **Platform**: Select iOS or Android (create separate apps for each)
   - **Bundle ID / Package Name**: Your app's identifier
3. Click **"Create App"**

### Get App Token

1. On the app overview page, your **App Token** is displayed at the top
2. It's a 12-character alphanumeric string (e.g., `abc123def456`)
3. Also available under **All Settings** → **App Token**

**Note:** If you have both iOS and Android, you'll have separate tokens for each platform.

### Configure Attribution Links (Optional)

For tracking campaign performance:

1. Go to **Campaign Lab** → **Trackers**
2. Create tracking links for marketing campaigns
3. Links will attribute installs to specific campaigns

### Configure in Unity

1. Open **Sorolla Configuration** window
2. Under **SDK Keys** (visible in Full Mode), enter:
   - **Adjust App Token**: From Step 3 above
3. Click **"Save"**

**Platform-Specific Notes:**
- Sorolla SDK handles platform detection automatically
- If you have separate iOS/Android tokens, enter the primary build target token
- For multi-platform projects, token is read at build time for target platform

---

## 4. GameAnalytics Admin Access (Required)

⚠️ **Required for publishing team to debug UA and analytics issues**

### Grant Admin Access

1. Log in to [GameAnalytics](https://gameanalytics.com)
2. Select your game from the top-left dropdown
3. Click **Settings (Gear Icon)** in bottom-left menu
4. Click **Users** tab
5. Click **Invite User** (top right)
6. Enter email: `studio@sorolla.io`
7. ⚠️ **IMPORTANT:** Set Role to **Admin** (Viewer is not enough)
8. Send Invite

---

## Full Mode Checklist

**Before production launch, verify:**

- [ ] GameAnalytics: Game Key and Secret Key configured
- [ ] GameAnalytics: Admin access granted to `studio@sorolla.io`
- [ ] AppLovin MAX: SDK Key configured
- [ ] AppLovin MAX: Rewarded Ad Unit ID configured
- [ ] AppLovin MAX: Interstitial Ad Unit ID configured
- [ ] AppLovin MAX: Mediation networks enabled (recommended)
- [ ] Adjust: App Token configured for target platform(s)
- [ ] Adjust: Attribution links created (optional)

---

## Quick Reference

| SDK | What You Need | Where to Find It |
|-----|---------------|------------------|
| **GameAnalytics** | Game Key<br>Secret Key | [gameanalytics.com](https://gameanalytics.com) → Settings → Game Settings |
| **AppLovin MAX** | SDK Key<br>Rewarded ID<br>Interstitial ID | [dash.applovin.com](https://dash.applovin.com)<br>→ Account → Keys<br>→ Monetize → Ad Units |
| **Adjust** | App Token<br>(iOS/Android) | [adjust.com](https://www.adjust.com)<br>→ Apps → All Settings |

---

## Need Help?

- 📖 [Getting Started](getting-started.md) | [Troubleshooting](troubleshooting.md)
- 🐙 [GitHub Repository](https://github.com/LaCreArthur/sorolla-palette-upm)
- 🐛 [Report Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)

### Related Guides

- [Ads Setup](ads-setup.md) - AppLovin MAX configuration details
- [Prototype Mode Setup](prototype-setup.md) - Quick UA testing setup
- [Firebase Setup](firebase.md) - Add Analytics, Crashlytics, Remote Config
- [API Reference](api-reference.md) - Complete API documentation
