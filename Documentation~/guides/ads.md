# Ads Setup (AppLovin MAX)

Monetization with automatic mediation.

---

## 1. Create Account

1. Sign up at [dash.applovin.com](https://dash.applovin.com/signup)
2. Go to **Account** → **Keys**
3. Copy your **SDK Key**

## 2. Create Ad Units

1. Go to **Monetize** → **Manage** → **Ad Units**
2. Create ad units and copy IDs:
   - **Rewarded Video** (required)
   - **Interstitial** (required)
   - **Banner** (optional)

## 3. Configure in Unity

1. Open **Palette > Configuration**
2. Under **SDK Keys**, enter:
   - SDK Key (in MAX Integration Manager)
   - Rewarded Ad Unit ID
   - Interstitial Ad Unit ID
   - Banner Ad Unit ID (optional)

## 4. Enable Mediation (Recommended)

Higher fill rates with multiple ad networks:

1. Go to **Monetize** → **Manage** → **Mediation**
2. Enable: **AdMob**, **Meta Audience Network**, **Unity Ads**
3. Each network requires API keys (follow MAX setup flow)

---

## Usage

```csharp
using Sorolla.Palette;

// Rewarded ad
if (Palette.IsRewardedAdReady)
{
    Palette.ShowRewardedAd(
        onComplete: () => GiveReward(),
        onFailed: () => Debug.Log("Ad not available")
    );
}

// Interstitial ad
Palette.ShowInterstitialAd(onComplete: () => LoadNextLevel());

// Banner
Palette.ShowBanner();
Palette.HideBanner();
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Ads not loading | Wait 30 sec after init, check SDK Key and Ad Unit IDs |
| Low fill rate | Enable mediation networks |
| Test ads only | Use real device, not simulator |
| Revenue not tracking | Verify Adjust token (Full mode) |
