# Prototype Mode Setup Guide

This guide covers SDK setup for **Prototype Mode** - designed for rapid UA testing during development.

## 1. GameAnalytics Setup

### Create Account & Game Project

1. Go to [https://gameanalytics.com](https://gameanalytics.com)
2. Click **"Sign Up"** and complete registration
3. Click **"Add Game"** or **"Create Game"**
4. Fill in game details:
   - **Game Name**: Your game's name
   - **Platform**: Select iOS and/or Android
   - **Engine**: Select Unity
5. Click **"Create Game"**

### Set API Keys

1. Navigate to **Settings** → **Game Settings** (gear icon)
2. Get both keys:
   - **Game Key**: Hexadecimal string (e.g., `a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4`)
   - **Secret Key**: Another hexadecimal string
3. In Unity, open **Window** → **GameAnalytics** → **Select Settings**
4. You can log in to your account, or paste **Game Key** and **Secret Key** manually

### Grant Admin Access

1. Click **Settings (Gear Icon)** in bottom-left menu
2. Click **Users** tab
3. Click **Invite User** (top right)
4. Enter email: `studio@sorolla.io`
5. Set Role to **Admin**
6. Send Invite

---

## 2. Facebook SDK Setup

### Create Developer Account & App

1. Go to [https://developers.facebook.com](https://developers.facebook.com)
2. Click **"Get Started"** and complete registration
3. Go to **"My Apps"** → **"Create App"**
4. Select **"Other"** or **"Gaming"** → **"Next"**
5. Enter **App Name** and **App Contact Email**
6. Click **"Create App"**

### Get App ID and Client Token

1. Your **App ID** appears at the top of the app dashboard
2. Also visible under **Settings** → **Basic**

#### Get Client Token

1. Go to **Settings** → **Advanced**
2. Scroll to **Security** section
3. Copy your **Client Token** (e.g., `a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6`)

### Configure Platforms

#### iOS Platform:
1. Go to **Settings** → **Basic**
2. Click **"+ Add Platform"** → Select **"iOS"**
3. Enter your **Bundle ID** (e.g., `com.yourcompany.yourgame`)
4. Click **"Save Changes"**

#### Android Platform:
1. Click **"+ Add Platform"** → Select **"Android"**
2. Enter your **Package Name**: Must match Unity Bundle ID exactly
3. Click **"Save Changes"**

### Authorize Sorolla Ad Account

1. In **App Settings** → **Advanced**
2. Scroll down to **Advertising Accounts** section
3. In **Authorized Ad Account IDs** field, enter: `1130531078835118`
4. Click **"Save Changes"** at the bottom

### Configure in Unity

1. Open **Facebook** → **Edit Settings**
2. Enter:
   - **App ID**: Your Facebook App ID
   - **Client Token**: From Settings → Advanced → Security
3. SDK will auto-configure for iOS and Android

---

## 3. Analytics

### Track Your First Event

The SDK initializes automatically. Add these events to your game logic:
- The `TrackProgression` events are **mandatory**, others are optional

> 💡 **Tip:** Zero-pad level numbers (`Level_001` not `Level_1`) for better dashboard sorting.

```csharp
using Sorolla;

// Level tracking (⚠️ MANDATORY)
string lvlStr = $"Level_{[YOUR_LEVEL]:D3}";  // "Level_001" - zero-pad for dashboard sorting

// Start
SorollaSDK.TrackProgression(ProgressionStatus.Start, lvlStr);

// Completed (score is optional - use if your game has scores)
SorollaSDK.TrackProgression(ProgressionStatus.Complete, lvlStr, score: 1500);

// Failed
SorollaSDK.TrackProgression(ProgressionStatus.Fail, lvlStr);


// Custom events, track what you want
SorollaSDK.TrackDesign("tutorial:completed");
// Custom event with value
SorollaSDK.TrackDesign("settings:opened", 1);


// Economy tracking (if you have soft/hard currencies)
SorollaSDK.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");
SorollaSDK.TrackResource(ResourceFlowType.Sink, "coins", 50, "shop", "speed_boost");
```

---

## Prototype Mode Checklist

**Before launching UA campaigns, verify:**

- [ ] GameAnalytics: Game Key and Secret Key configured
- [ ] GameAnalytics: Admin access granted to `studio@sorolla.io`
- [ ] Facebook SDK: App ID configured
- [ ] Facebook SDK: Client Token configured
- [ ] Facebook SDK: Sorolla Ad Account (`1130531078835118`) authorized
- [ ] Analytics: Add level progression events `SorollaSDK.TrackProgression`

---

## Need Help?

- 📖 [Getting Started](getting-started.md) | [Troubleshooting](troubleshooting.md)
- 🐙 [GitHub Repository](https://github.com/LaCreArthur/sorolla-palette-upm)
- 🐛 [Report Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)

### Related Guides

- [Ads Setup](ads-setup.md) - Add rewarded and interstitial ads (optional)
- [Firebase Setup](firebase.md) - Add Analytics, Crashlytics, Remote Config
- [API Reference](api-reference.md) - Complete API documentation
