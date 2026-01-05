# Full Mode Setup Guide

**Complete production setup with attribution, monetization, and compliance (20-30 minutes).**

This guide covers everything for a production-ready mobile game with full analytics, attribution, ads, GDPR compliance, and crash reporting.

---

## Switching from Prototype Mode

**The SDK auto-installed in Prototype Mode.** To switch to Full Mode:

1. Open Unity: `Palette > Configuration`
2. Click **"🚀 Full Mode"** button
3. The SDK will automatically:
   - Install AppLovin MAX
   - Install Adjust SDK
   - Keep GameAnalytics (already installed)
   - Remove Facebook SDK (replaced by Adjust)

**All dependencies install automatically!** You just need to configure API keys below.

---

## What You'll Set Up

### ✅ Required for Production
- **GameAnalytics** - Analytics and event tracking (already installed)
- **AppLovin MAX** - Ad monetization with mediation
- **Adjust** - Full attribution tracking
- **GDPR/ATT Consent** - EU compliance and iOS tracking

### ⚡ Highly Recommended
- **Firebase Crashlytics** - Crash reporting
- **Firebase Remote Config** - A/B testing and feature flags
- **Firebase Analytics** - Dual analytics backend

**Estimated time:** 20-30 minutes for configuration

---

1. Get your API keys from [GameAnalytics](https://gameanalytics.com):
   - Navigate to **Settings** → **Game Settings** (gear icon)
   - Copy **Game Key** and **Secret Key** (long hexadecimal strings)
2. In Unity: **Window** → **GameAnalytics** → **Select Settings**
3. Paste both keys and click **"Save"**

### 1.3 Grant Admin Access (Required)

⚠️ **Required for Sorolla team to support your integration**

1. In [GameAnalytics](https://gameanalytics.com), go to **Settings** → **Users**
2. Click **Invite User** → Enter email: `studio@sorolla.io`
3. Set Role to **Admin** (not Viewer)
4. Send Invite

---

## Step 2: AppLovin MAX Setup

MAX handles ad monetization with automatic mediation across multiple ad networks.

### 2.1 Create Account & Get SDK Key

1. Sign up at [dash.applovin.com](https://dash.applovin.com/signup)
2. Navigate to **Account** → **Keys**
3. Copy your **SDK Key**

### 2.2 Create Ad Units

1. Go to **Monetize** → **Manage** → **Ad Units**
2. Create **Rewarded Video** ad unit → Copy **Ad Unit ID**
3. Create **Interstitial** ad unit → Copy **Ad Unit ID**
4. (Optional) Create **Banner** ad unit → Copy **Ad Unit ID**

### 2.3 Enable Mediation Networks (Recommended)

1. Go to **Monetize** → **Manage** → **Mediation**
2. Enable networks: **AdMob**, **Meta Audience Network**, **Unity Ads**
3. Each network requires API keys (follow MAX's setup flow)
4. MAX automatically optimizes between networks

### 2.4 Configure in Unity

1. Open **Sorolla Configuration** (Sorolla → Configuration)
2. Enter **SDK Key**, **Rewarded ID**, **Interstitial ID**
3. Click **"Save"**

📖 **[Detailed Ads Setup Guide](ads-setup.md)** for advanced configuration

---

## Step 3: Adjust Setup

### 3.1 Create Account & App

1. Sign up at [adjust.com](https://www.adjust.com)
2. Create app for iOS and/or Android
3. Copy **App Token** (12-character string, e.g., `abc123def456`)

### 3.2 Configure in Unity

1. Open **Sorolla Configuration**
2. Enter **Adjust App Token** (automatically used for correct platform)
3. Click **"Save"**

### 3.3 Optional: Create Attribution Links

1. In Adjust dashboard, go to **Campaign Lab** → **Trackers**
2. Create tracking links for each marketing campaign
3. Links will attribute installs to campaigns

---

## Step 4: GDPR & ATT Consent (Required for EU/iOS)

### 4.1 Setup GDPR Consent (EU Requirement)

1. Create **AdMob account** at [admob.google.com](https://admob.google.com)
2. Go to **Privacy & messaging** → **GDPR** → **Create message**
3. Select your app and configure consent form
4. Enable **Custom ad partners** and select: AppLovin, AdMob, Meta, Unity
5. Click **Publish**

### 4.2 Configure in Unity

1. In Unity, go to **AppLovin** → **Integration Manager**
2. Enable **MAX Terms and Privacy Policy Flow**
3. Set **Privacy Policy URL** (your company's privacy policy)
4. Set **User Tracking Usage Description**: 
   ```
   This identifier will be used to deliver personalized ads to you.
   ```
5. Click **Save**

### 4.3 Add Privacy Settings Button (Required)

GDPR requires users to change their consent at any time:

```csharp
using UnityEngine;
using UnityEngine.UI;
using Sorolla.Palette;

public class SettingsScreen : MonoBehaviour
{
    [SerializeField] Button privacyButton;

    void Start()
    {
        // Show button only if user is in GDPR region
        privacyButton.gameObject.SetActive(Palette.PrivacyOptionsRequired);
        privacyButton.onClick.AddListener(OnPrivacyClicked);
    }

    void OnPrivacyClicked()
    {
        Palette.ShowPrivacyOptions(() => {
            Debug.Log("Privacy settings updated");
        });
    }
}
```

📖 **[Complete GDPR Setup Guide](gdpr-consent-setup.md)** with testing instructions

---

## Step 5: Firebase Setup (Highly Recommended)

### 5.1 Create Firebase Project

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create new project (**enable Google Analytics** when prompted)
3. Add iOS and/or Android apps with your bundle IDs

### 5.2 Download Config Files

- **Android**: Download `google-services.json` → Place in `Assets/`
- **iOS**: Download `GoogleService-Info.plist` → Place in `Assets/`

### 5.3 Install Firebase in Unity

1. Open **Sorolla Configuration**
2. Click **"Install"** next to Firebase (optional)
3. Wait for packages to import
4. Enable modules: **Analytics**, **Crashlytics**, **Remote Config**
5. Click **"Save"**

### 5.4 Use Firebase Features

All Firebase features work automatically with no code changes:

- **Analytics**: Events sent to both Firebase & GameAnalytics
- **Crashlytics**: Automatic crash reporting on next app launch
- **Remote Config**: Unified API (Firebase → GameAnalytics → default)

```csharp
using Sorolla.Palette;

// Crashlytics - log exceptions
try { /* risky code */ }
catch (Exception ex) { Palette.LogException(ex); }

// Remote Config - A/B testing
Palette.FetchRemoteConfig(success => {
    if (success)
    {
        float difficulty = Palette.GetRemoteConfigFloat("difficulty", 1.0f);
        bool newFeature = Palette.GetRemoteConfigBool("new_ui", false);
    }
});
```

📖 **[Complete Firebase Guide](firebase.md)** with all features

---

## Step 6: Add Analytics Events

### 6.1 Track Level Progression (Required)

```csharp
using Sorolla.Palette;

int level = 1;
string lvlStr = $"Level_{level:D3}";  // Zero-pad: "Level_001"

// When level starts
Palette.TrackProgression(ProgressionStatus.Start, lvlStr);

// When level completes
Palette.TrackProgression(ProgressionStatus.Complete, lvlStr, score: 1500);

// When level fails
Palette.TrackProgression(ProgressionStatus.Fail, lvlStr);
```

### 6.2 Show Ads

```csharp
// Rewarded ad
if (Palette.IsRewardedAdReady)
{
    Palette.ShowRewardedAd(
        onComplete: () => GiveReward(),
        onFailed: () => Debug.Log("Ad not available")
    );
}

// Interstitial ad (e.g., between levels)
Palette.ShowInterstitialAd(onComplete: () => LoadNextLevel());
```

📖 **[Complete API Reference](api-reference.md)**

---

## Step 7: Test with Debug UI

### 7.1 Import Debug UI Sample

1. **Package Manager** → Sorolla SDK → Samples → Import "Debug UI"
2. Add `DebugPanelManager` prefab to your first scene
3. Build to iOS/Android device

### 7.2 Verify Integration

1. Launch app on device
2. **Triple-tap** screen to open debug panel
3. Verify all indicators are green:
   - ✅ GameAnalytics initialized
   - ✅ MAX initialized
   - ✅ Adjust initialized
   - ✅ Consent status (iOS: ATT granted, Android: GDPR consented)
4. Test ad loading and showing
5. Check event tracking

---

## ✅ Full Mode Production Checklist

**Before launching to production:**

### Core SDKs
- [ ] GameAnalytics: Game Key and Secret Key configured
- [ ] GameAnalytics: Admin access granted to `studio@sorolla.io`
- [ ] AppLovin MAX: SDK Key, Rewarded ID, Interstitial ID configured
- [ ] AppLovin MAX: Mediation networks enabled (AdMob, Meta, Unity)
- [ ] Adjust: App Token configured

### Compliance (EU/iOS)
- [ ] GDPR: Consent message created and published in AdMob
- [ ] GDPR: Custom ad partners added (AppLovin, AdMob, Meta, Unity)
- [ ] GDPR: Privacy policy URL set in MAX Integration Manager
- [ ] GDPR: Privacy settings button added to game settings
- [ ] iOS: User Tracking Usage Description set

### Firebase (Recommended)
- [ ] Firebase: Config files (`google-services.json`, `GoogleService-Info.plist`) added
- [ ] Firebase: Analytics, Crashlytics, Remote Config enabled in Sorolla Config

### Integration
- [ ] Analytics: Level progression events added (`TrackProgression`)
- [ ] Ads: Rewarded and interstitial ad calls integrated
- [ ] Build: Successfully builds to iOS/Android
- [ ] Testing: Debug UI shows all SDKs initialized
- [ ] Testing: Ads load and show correctly
- [ ] Testing: Consent flow appears on first launch (EU/iOS)

---

## 🎯 You're Production Ready!

Your game is now fully set up with:
- ✅ Complete analytics (GameAnalytics + Firebase)
- ✅ Full attribution (Adjust)
- ✅ Ad monetization (MAX with mediation)
- ✅ GDPR/ATT compliance
- ✅ Crash reporting (Firebase Crashlytics)

### Useful Resources

- 📖 [API Reference](api-reference.md) - Complete code documentation
- 📖 [GDPR Guide](gdpr-consent-setup.md) - Detailed compliance setup
- 📖 [Firebase Guide](firebase.md) - All Firebase features
- 📖 [Ads Guide](ads-setup.md) - Advanced ad configuration
- 🐛 [Troubleshooting](troubleshooting.md) - Common issues and fixes

---

## 💬 Need Help?

- 📧 **Email**: studio@sorolla.io (for setup assistance)
- 💬 **GitHub Issues**: [Report problems](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
- 📊 **Analytics**: Check your [GameAnalytics dashboard](https://gameanalytics.com)
- 💰 **Revenue**: Monitor [AppLovin MAX dashboard](https://dash.applovin.com)
