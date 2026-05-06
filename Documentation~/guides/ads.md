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
```

### Rewarded Ads

```csharp
// Check readiness before showing the Watch Ad button
bool ready = Palette.IsRewardedAdReady;

// Show
Palette.ShowRewardedAd(
    onComplete: () => { /* user earned reward */ },
    onFailed:   () => { /* ad failed or was skipped */ }
);
```

**Typical button handler:**
```csharp
public void OnWatchAdClicked()
{
    if (!Palette.IsRewardedAdReady)
    {
        ShowMessage("Ad not available, try again in a moment");
        return;
    }

    Palette.ShowRewardedAd(
        onComplete: () =>
        {
            coins += 100;
            UpdateUI();
        },
        onFailed: () => ShowMessage("Ad not available")
    );
}
```

### Interstitial Ads

```csharp
// Check readiness before choosing whether to interrupt flow
bool ready = Palette.IsInterstitialAdReady;

Palette.ShowInterstitialAd(
    onComplete: LoadNextLevel,
    onFailed: LoadNextLevel);
```

**Frequency-capped example:**
```csharp
void OnLevelComplete()
{
    levelsCompleted++;
    if (levelsCompleted % 3 == 0)  // Every 3 levels
    {
        Palette.ShowInterstitialAd(
            onComplete: ShowNextLevel,
            onFailed: ShowNextLevel);
    }
    else
    {
        ShowNextLevel();
    }
}
```

### Banner Ads

Banner ads are configured via `SorollaConfig.bannerAdUnit` but the `ShowBanner`/`HideBanner` API is planned and not yet exposed. See `Documentation~/api-reference.md` for the current public surface.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Ads not loading | Wait 30 sec after init, check SDK Key and Ad Unit IDs, disable VPN/ad-blocking DNS |
| `NetworkError` / `ms.applvn.com` cannot resolve | Disable VPN, threat protection, ad blocker, or custom/private DNS on the test device |
| Low fill rate | Enable mediation networks |
| Test ads only | Use real device, not simulator |
| Revenue not tracking | Verify Adjust token (Full mode) |

On Android QA devices, VPN/ad-blocking features can block AppLovin while the rest of the internet still appears healthy. If MAX initializes but rewarded/interstitial loads fail with `Unable to resolve host "ms.applvn.com"`, turn off VPN/private DNS/ad blocking, relaunch the app, and retry before changing SDK or dashboard config.
