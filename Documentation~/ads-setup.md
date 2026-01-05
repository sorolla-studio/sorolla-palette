# Ads Setup (AppLovin MAX)

This guide covers setting up AppLovin MAX for ad monetization.

- **Prototype Mode**: Ads are optional (for testing ad UX early)
- **Full Mode**: Ads are required

---

## 1. Create AppLovin Account

1. Go to [https://dash.applovin.com/signup](https://dash.applovin.com/signup)
2. Complete registration and verify email
3. Log in to dashboard

---

## 2. Get SDK Key

1. Navigate to **Account** → **Keys** (top right profile dropdown)
2. Copy your **SDK Key** (long alphanumeric string)

---

## 3. Create Ad Units

### Rewarded Ad Unit

1. Go to **Monetize** → **Manage** → **Ad Units**
2. Click **"Create Ad Unit"**
3. Select your app (or create a new one)
4. Choose **"Rewarded"** type
5. Enter **Ad Unit Name** (e.g., "Rewarded Video")
6. Click **"Create"** and copy **Ad Unit ID**

### Interstitial Ad Unit

1. Click **"Create Ad Unit"** again
2. Select **"Interstitial"** type
3. Enter **Ad Unit Name** (e.g., "Interstitial Ad")
4. Click **"Create"** and copy **Ad Unit ID**

### Banner Ad Unit (Optional)

1. Click **"Create Ad Unit"**
2. Select **"Banner"** type
3. Enter **Ad Unit Name**
4. Click **"Create"** and copy **Ad Unit ID**

---

## 4. Configure in Unity

1. Open **Sorolla Configuration** window (Sorolla → Configuration)
2. Under **SDK Keys**, enter:
   - **SDK Key**
   - **Rewarded Ad Unit ID**
   - **Interstitial Ad Unit ID**
   - **Banner Ad Unit ID** (optional)
3. Click **"Save"**

---

## 5. Show Ads in Code

```csharp
using Sorolla;

// Rewarded ad
if (SorollaSDK.IsRewardedAdReady)
{
    SorollaSDK.ShowRewardedAd(
        onComplete: () => GiveReward(),
        onFailed: () => Debug.Log("Ad failed")
    );
}

// Interstitial ad
SorollaSDK.ShowInterstitialAd(onComplete: () => ContinueGame());
```

---

## 6. Configure Mediation Networks (Recommended)

For better fill rates and revenue:

1. Go to **Monetize** → **Manage** → **Mediation**
2. Enable ad networks: **AdMob**, **Meta Audience Network**, **Unity Ads**, etc.
3. Each network requires its own API keys
4. AppLovin automatically optimizes between networks

---

## Ads Checklist

- [ ] AppLovin MAX: SDK Key configured
- [ ] AppLovin MAX: Rewarded Ad Unit ID configured
- [ ] AppLovin MAX: Interstitial Ad Unit ID configured
- [ ] AppLovin MAX: Mediation networks enabled (recommended)
- [ ] Test ads on device with Debug UI

---

## Need Help?

- 📖 [Prototype Setup](prototype-setup.md) - Quick UA testing setup
- 📖 [Full Mode Setup](full-setup.md) - Complete production setup
- 📖 [Troubleshooting](troubleshooting.md) - Common issues and fixes
- 🐛 [Report Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
