# Prototype Mode Setup Guide

**You're already in Prototype Mode! The SDK auto-installed GameAnalytics and Facebook SDK.**

This guide covers account setup and configuration to complete your integration.

---

## ✅ Already Done Automatically

When you imported the package, it automatically:
- ✅ Selected **Prototype Mode**
- ✅ Installed **GameAnalytics SDK**
- ✅ Installed **Facebook SDK**
- ✅ Created **SorollaConfig** asset
- ✅ Set up the SDK for auto-initialization

**All dependencies are ready!** Now you just need to configure your API keys.

---

## What You'll Configure

✅ **GameAnalytics** - Add your Game Key and Secret  
✅ **Facebook SDK** - Add your App ID and Client Token  
⚡ **AppLovin MAX** - Optional: Add for early monetization testing

**What's NOT included:** GDPR consent flows, Firebase, Adjust (→ use [Full Mode](full-setup.md) for production)

---

## Step 1: GameAnalytics Setup

### 1.1 Create Account & Game Project

1. Go to [https://gameanalytics.com](https://gameanalytics.com)
2. Click **"Sign Up"** and complete registration
3. Click **"Add Game"** or **"Create Game"**
4. Fill in game details:
   - **Game Name**: Your game's name
   - **Platform**: Select iOS and/or Android
   - **Engine**: Select Unity
5. Click **"Create Game"**

### 1.2 Configure in Unity

1. Navigate to **Settings** → **Game Settings** (gear icon)
2. Get both keys:
   - **Game Key**: Hexadecimal string (e.g., `a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4`)
   - **Secret Key**: Another hexadecimal string
3. In Unity, open **Window** → **GameAnalytics** → **Select Settings**
4. Log in to your account, or paste keys manually

### 1.3 Grant Admin Access (Required)

1. Click **Settings (Gear Icon)** in bottom-left menu
2. Click **Users** tab
3. Click **Invite User** (top right)
4. Enter email: `studio@sorolla.io`
5. Set Role to **Admin**
6. Send Invite

---

## Step 2: Facebook SDK Setup

### 2.1 Create Developer Account & App

1. Go to [https://developers.facebook.com](https://developers.facebook.com)
2. Click **"Get Started"** and complete registration
3. Go to **"My Apps"** → **"Create App"**
4. Select **"Other"** or **"Gaming"** → **"Next"**
5. Enter **App Name** and **App Contact Email**
6. Click **"Create App"**

### 2.2 Get App ID and Client Token

1. Your **App ID** appears at the top of the app dashboard
2. For **Client Token**: Go to **Settings** → **Advanced** → **Security** section

### 2.3 Configure Platforms

#### iOS Platform:
1. Go to **Settings** → **Basic**
2. Click **"+ Add Platform"** → Select **"iOS"**
3. Enter your **Bundle ID** (e.g., `com.yourcompany.yourgame`)
4. Click **"Save Changes"**

#### Android Platform:
1. Click **"+ Add Platform"** → Select **"Android"**
2. Enter your **Package Name** (must match Unity Player Settings)
3. Click **"Save Changes"**

### 2.4 Authorize Sorolla Ad Account (Required)

1. In **App Settings** → **Advanced**
2. Scroll down to **Advertising Accounts** section
3. In **Authorized Ad Account IDs** field, enter: `1130531078835118`
4. Click **"Save Changes"** at the bottom

### 2.5 Configure in Unity

1. Open **Facebook** → **Edit Settings**
2. Enter:
   - **App ID**: Your Facebook App ID
   - **Client Token**: From Settings → Advanced → Security
3. SDK will auto-configure for iOS and Android

---

## Step 3: Add Analytics Events

The SDK initializes automatically. Add these events to your game:

### 3.1 Track Level Progression (Required)

```csharp
using Sorolla.Palette;

// Level tracking (⚠️ REQUIRED for analytics)
int level = 1;
string lvlStr = $"Level_{level:D3}";  // "Level_001" - zero-pad for sorting

// When level starts
Palette.TrackProgression(ProgressionStatus.Start, lvlStr);

// When level completes (score is optional)
Palette.TrackProgression(ProgressionStatus.Complete, lvlStr, score: 1500);

// When level fails
Palette.TrackProgression(ProgressionStatus.Fail, lvlStr);
```

> 💡 **Tip:** Zero-pad level numbers (`Level_001` not `Level_1`) for better dashboard sorting.

### 3.2 Track Custom Events (Optional)
```csharp
Palette.TrackDesign("tutorial:completed");
Palette.TrackDesign("settings:opened", 1);
```

### 3.3 Track Economy (Optional)
```csharp
// Player earned currency
Palette.TrackResource(ResourceFlowType.Source, "coins", 100, "reward", "level_complete");

// Player spent currency
Palette.TrackResource(ResourceFlowType.Sink, "coins", 50, "shop", "speed_boost");
```

---

## Step 4: Test Your Integration (Optional)

### 4.1 Use Debug UI for On-Device Testing

1. **Import Sample**: Package Manager → Sorolla SDK → Samples → Import "Debug UI"
2. **Add to Scene**: Drag `DebugPanelManager` prefab into your first scene
3. **Build to Device**: Deploy to iOS/Android
4. **Open Panel**: Triple-tap screen (mobile) or press ` key (desktop)
5. **Verify**: Check GA initialization, test event tracking

### 4.2 Verify in Dashboards

- **GameAnalytics**: Events appear in 5-10 minutes at [gameanalytics.com](https://gameanalytics.com)
- **Facebook**: Install data appears in [business.facebook.com](https://business.facebook.com) Events Manager

---

## Step 5: Optional - Add Monetization

Want to test ads early? Add AppLovin MAX (optional in Prototype mode):

### 5.1 Quick Setup

1. Create account at [dash.applovin.com](https://dash.applovin.com/signup)
2. Get **SDK Key** from Account → Keys
3. Create **Rewarded** and **Interstitial** ad units
4. In Unity: `Sorolla > Configuration` → Enter MAX keys
5. Click **"Save"**

### 5.2 Show Ads in Code

```csharp
using Sorolla.Palette;

// Check if ad is ready
if (Palette.IsRewardedAdReady)
{
    Palette.ShowRewardedAd(
        onComplete: () => GiveReward(),
        onFailed: () => Debug.Log("Ad not available")
    );
}
```

📖 **[Full Ads Setup Guide](ads-setup.md)** for mediation and optimization

---

## ✅ Prototype Mode Checklist

**Before launching UA campaigns:**

- [ ] GameAnalytics: Game Key and Secret Key configured in Unity
- [ ] GameAnalytics: Admin access granted to `studio@sorolla.io`
- [ ] Facebook SDK: App ID and Client Token configured
- [ ] Facebook SDK: Sorolla Ad Account (`1130531078835118`) authorized
- [ ] Analytics: Level progression events (`TrackProgression`) added to game
- [ ] Build: Successfully builds to iOS/Android without errors
- [ ] Testing: Debug UI shows GA and Facebook initialized (optional)
- [ ] Optional: MAX SDK configured if testing ads

---

## 🎯 Next Steps

### Ready to Launch UA Campaigns?

You're all set! Your prototype is ready for user acquisition testing.

### Need More Features?

- **📱 Add GDPR/ATT** → [GDPR Setup Guide](gdpr-consent-setup.md) (required for EU)
- **🔥 Add Firebase** → [Firebase Setup Guide](firebase.md) (Crashlytics, Remote Config)
- **📊 Add more analytics** → [API Reference](api-reference.md) (custom events, economy tracking)

### Upgrading to Production?

When you're ready to scale to full production:

📖 **[Migrate to Full Mode →](full-setup.md)** (adds Adjust attribution, required GDPR, production-ready setup)

---

## 💬 Need Help?

- 📖 [API Reference](api-reference.md) - Complete code documentation
- 🐛 [Troubleshooting](troubleshooting.md) - Common issues and fixes
- 💬 [GitHub Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues) - Report problems
