# Switching to Full Mode

Upgrade from Prototype to production-ready setup.

**What's added:**
- **Adjust** - Full attribution tracking
- **GDPR/ATT** - EU compliance and iOS consent
- **Required Ads** - MAX with mediation networks

**Time:** ~15 minutes (assuming Prototype already configured)

---

## 1. Switch Mode

1. Open **Palette > Configuration**
2. Click **Switch to Full**
3. Wait for packages to install

---

## 2. Configure Adjust

Adjust provides full attribution for your marketing campaigns.

### Setup

1. Create account at [adjust.com](https://www.adjust.com)
2. Add your app (iOS and/or Android)
3. Copy your **App Token** (12-character string)
4. In Unity: **Palette > Configuration** → Enter App Token under **SDK Keys**

### Optional: Campaign Tracking

Create tracking links in Adjust dashboard:
- **Campaign Lab** → **Trackers** → Create links for each campaign
- Share links with marketing team

[Full Adjust Guide](guides/adjust.md)

---

## 3. Configure GDPR/ATT Consent

Required for EU users and iOS App Tracking Transparency.

### AdMob Setup (GDPR)

1. Create account at [admob.google.com](https://admob.google.com)
2. Go to **Privacy & messaging** → **GDPR** → **Create message**
3. Configure consent form for your app
4. Enable **Custom ad partners**: AppLovin, AdMob, Meta, Unity
5. Click **Publish**

### Unity Setup

1. Open **AppLovin** → **Integration Manager**
2. Enable **MAX Terms and Privacy Policy Flow**
3. Set **Privacy Policy URL** (your privacy policy)
4. Set **User Tracking Usage Description**:
   ```
   This identifier will be used to deliver personalized ads to you.
   ```
5. Click **Save**

### Add Privacy Button

GDPR requires users to change consent anytime. Add to your settings screen:

```csharp
using Sorolla.Palette;

// In your settings UI
if (Palette.PrivacyOptionsRequired)
{
    // Show privacy button
    privacyButton.onClick.AddListener(() => {
        Palette.ShowPrivacyOptions(() => Debug.Log("Updated"));
    });
}
```

[Full GDPR Guide](guides/gdpr.md)

---

## 4. Configure Ads (Required)

In Full mode, MAX is required with mediation networks.

### Enable Mediation

1. Go to [MAX Dashboard](https://dash.applovin.com) → **Monetize** → **Mediation**
2. Enable networks: **AdMob**, **Meta Audience Network**, **Unity Ads**
3. Each requires API keys (follow MAX setup flow)

[Full Ads Guide](guides/ads.md)

---

## Production Checklist

Before launching:

**Adjust**
- [ ] App Token configured
- [ ] Tracking links created for campaigns

**GDPR/ATT**
- [ ] GDPR consent message published in AdMob
- [ ] Privacy Policy URL set in MAX Integration Manager
- [ ] User Tracking Usage Description set
- [ ] Privacy settings button added to game

**Ads**
- [ ] MAX SDK Key and Ad Unit IDs configured
- [ ] Mediation networks enabled (AdMob, Meta, Unity)

**Testing**
- [ ] Consent dialog appears on first launch (EU/iOS)
- [ ] All SDKs show green in Debug UI
- [ ] Ads load and display correctly

---

## Help

- [Troubleshooting](troubleshooting.md)
- [API Reference](api-reference.md)
- [GitHub Issues](https://github.com/sorolla-studio/sorolla-palette/issues)
