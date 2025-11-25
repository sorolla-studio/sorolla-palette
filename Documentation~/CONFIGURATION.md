# Sorolla SDK - Configuration Guide

Complete guide for configuring each SDK in Sorolla.

---

## 📦 Package Modes

| Mode | SDKs | Use Case |
|------|------|----------|
| **Prototype** | GameAnalytics + Facebook | Rapid UA testing |
| **Full** | GameAnalytics + MAX + Adjust | Production |

---

## 🤖 Auto-Installed SDKs

When you install Sorolla SDK, these are handled automatically:

| SDK | Mode | Installation |
|-----|------|--------------|
| GameAnalytics | Both | ✅ Auto |
| AppLovin MAX | Optional (Prototype) / Required (Full) | ✅ Auto |
| Adjust | Full only | ✅ Auto |
| Facebook SDK | Prototype only | ✅ Auto |
| External Dependency Manager | Both | ✅ Auto |

---

## 1️⃣ GameAnalytics

### Setup Steps

1. **Create Account**: https://gameanalytics.com/
2. **Create Game** in dashboard:
   - Add New Game → Select Unity
   - Copy **Game Key** and **Secret Key**
3. **Configure in Unity**:
   - Menu: `GameAnalytics > Setup Wizard`
   - Enter Game Key and Secret Key
   - Select platforms (Android/iOS)

### Remote Config (Optional)

1. In GA Dashboard: Your Game → Remote Config
2. Add parameters (use backward-compatible types)
3. Publish changes
4. Use in code:
```csharp
if (Sorolla.IsRemoteConfigReady())
{
    int reward = Sorolla.GetRemoteConfigInt("daily_reward", 100);
}
```

---

## 2️⃣ Facebook SDK (Prototype Mode)

### Setup Steps

1. **Create Facebook App**: https://developers.facebook.com/apps/
   - Create App → Gaming
   - Add Unity platform
   - Copy **App ID**
2. **Configure in Unity**:
   - Menu: `Facebook > Edit Settings`
   - Enter App ID and App Name

---

## 3️⃣ AppLovin MAX

### Setup Steps

1. **Create Account**: https://dash.applovin.com/
2. **Get SDK Key**: Account → Keys
3. **Create Ad Units**: MAX → Manage → Ad Units
   - Rewarded Ad Unit
   - Interstitial Ad Unit (optional)
4. **Configure in Unity**:
   - Open: `Sorolla > Configuration`
   - Enter SDK Key and Ad Unit IDs

### Optional: Additional Networks

- Menu: `AppLovin > Integration Manager`
- Install adapters for AdMob, Unity Ads, etc.

---

## 4️⃣ Adjust (Full Mode)

### Setup Steps

1. **Create Account**: https://www.adjust.com/
2. **Create App** in dashboard → Copy **App Token**
3. **Configure in Unity**:
   - Open: `Sorolla > Configuration`
   - Enter App Token

> **Note**: Adjust requires a paid subscription (~$2000+/month).

---

## ✅ Post-Configuration Checklist

### Build Settings
- [ ] Minimum API Level: 21 (Android 5.0)
- [ ] Target API Level: 34+ (Android 14)
- [ ] Scripting Backend: IL2CPP

### Player Settings
- [ ] Internet Access: **Require**

### Verify Setup
- Open `Sorolla > Configuration`
- Check "Setup Checklist" shows ✓ Ready

---

## 🔗 Dashboard Links

| SDK | Dashboard |
|-----|-----------|
| GameAnalytics | https://gameanalytics.com/ |
| AppLovin MAX | https://dash.applovin.com/ |
| Facebook | https://developers.facebook.com/ |
| Adjust | https://www.adjust.com/ |

---

## 🔗 Documentation Links

| SDK | Docs |
|-----|------|
| GameAnalytics | https://docs.gameanalytics.com/integrations/sdk/unity/ |
| AppLovin MAX | https://developers.applovin.com/en/unity/overview/integration |
| Facebook | https://developers.facebook.com/docs/unity/ |
| Adjust | https://help.adjust.com/en/article/get-started-unity |
