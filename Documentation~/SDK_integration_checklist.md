# SDK Integration Checklist

Step-by-step guide for integrating each SDK into Sorolla Palette.

---

## 🎮 **Package Modes**

- **Prototype Mode**: GA + Facebook SDK + optional MAX (monetization)
- **Full Mode**: GA + MAX + Adjust

---

## 🤖 **What's Auto-Installed**

When you install Sorolla Palette, Unity Package Manager automatically installs:

✅ **GameAnalytics** - Both modes (analytics + remote config)
✅ **AppLovin MAX** - Full Mode only (auto-installed when selecting Full Mode)
✅ **Adjust SDK** - Full Mode only (auto-installed when selecting Full Mode)
✅ **External Dependency Manager** - Both modes (Android/iOS dependencies)

**You don't need to manually download these!** Just wait 1-2 minutes after package installation.

---

## ⏳ **What You Install Manually**

These SDKs are distributed as `.unitypackage` files and must be imported manually:

⏳ **Facebook SDK** - Required for Prototype Mode only

Our Configuration Window will guide you with download links.

---

## 📦 **SDK Overview**

| SDK | Mode | Installation | Purpose |
|-----|------|--------------|---------|
| GameAnalytics | Both | ✅ Auto | Analytics + Remote Config |
| AppLovin MAX | Optional (Prototype), Required (Full) | ✅ Auto | Ad Mediation |
| Adjust SDK | Full Only | ✅ Auto | Attribution |
| External Dependency Manager | Both | ✅ Auto | Dependency Resolution |
| Facebook SDK | Prototype Only | ⏳ Manual | Facebook Events + UA Tracking |

---

## 1️⃣ **GameAnalytics** - Auto-Installed ✅

### Status: Already Installed
GameAnalytics is automatically installed via package.json dependencies. No action needed!

### Configuration

1. **Create Account**
   - Go to: https://gameanalytics.com/
   - Sign up for free account

2. **Create Game**
   - Dashboard → Add New Game
   - Select "Unity" platform
   - Copy **Game Key** (Android)
   - Copy **Secret Key** (Android)
   - Repeat for iOS if needed

3. **Configure in Unity**
   - Open: `Tools > Sorolla Palette > Configure`
   - GameAnalytics section (always visible)
   - Enter Game Key and Secret Key
   - Click Save

4. **Enable Features** (in GA Dashboard)
   - Settings → Enable "Send Events"
   - Settings → Enable "Remote Config" (optional)
   - Settings → Configure data retention

### Remote Config Setup (Optional)

1. **Create Parameters** (GA Dashboard)
   - Navigate to: Your Game → Remote Config
   - Click "Add Parameter"
   - Name: e.g., "daily_reward"
   - Type: **use backward-compatible types**
   - Value: e.g., 100
   - Click Save
   - Click "Publish"

2. **Use in Code**
```csharp
if (SorollaPalette.IsRemoteConfigReady()) {
    int reward = SorollaPalette.GetRemoteConfigInt("daily_reward", 100);
}
```

**⚠️ Important**: Use backward-compatible parameter type. New value type is not supported by Unity SDK.

---

## 2️⃣ **AppLovin MAX** - Auto-Installed ✅ (Optional for Prototype, Required for Full)

### Status: Already Installed
AppLovin MAX is automatically installed via package.json dependencies. No action needed!

**Usage:**
- **Prototype Mode**: Optional - Enable if you want to test ad monetization
- **Full Mode**: Required - Must be enabled for production

### Configuration

1. **Create Account**
   - Go to: https://dash.applovin.com/
   - Sign up for account

2. **Get SDK Key**
   - Dashboard → Account → Keys
   - Copy **SDK Key**

3. **Create Ad Units**
   - Dashboard → MAX → Manage → Ad Units
   - Create for **Android**:
     - Rewarded Ad Unit → Copy ID
     - Interstitial Ad Unit → Copy ID
     - Banner Ad Unit → Copy ID
   - Create for **iOS**:
     - Rewarded Ad Unit → Copy ID
     - Interstitial Ad Unit → Copy ID
     - Banner Ad Unit → Copy ID

4. **Configure in Unity**
   - Open: `Tools > Sorolla Palette > Configure`
   - Ensure you're in **Full Mode**
   - MAX section → Click **"Enable MAX Module"**
   - Enter SDK Key
   - Enter all Ad Unit IDs (Android + iOS)
   - Click Save

5. **Test Mode** (during development)
```csharp
MaxSdk.SetTestModeEnabled(true); // Shows test ads
```

### Integration Manager (Optional)

After enabling MAX module:
- Open: `AppLovin > Integration Manager`
- Install adapters for additional ad networks (optional):
  - AdMob
  - Facebook Audience Network
  - Unity Ads
  - IronSource

---

## 3️⃣ **Facebook SDK** - Manual Install ⏳ (Prototype Mode Only)

### Status: Requires Manual Import
**Required for:** Prototype Mode only
**Not used in:** Full Mode

1. **Download SDK**
   - Go to: https://developers.facebook.com/docs/unity/downloads/
   - Download latest **Facebook SDK for Unity**
   - Save `.unitypackage` file

2. **Import into Unity**
   - Unity → `Assets > Import Package > Custom Package`
   - Select downloaded `.unitypackage`
   - Click "Import" (import all files)
   - Wait for compilation

3. **Create Facebook App**
   - Go to: https://developers.facebook.com/apps/
   - Click "Create App"
   - Choose app type (Gaming, Business, etc.)
   - Enter app name
   - Add Unity platform
   - Configure Android/iOS settings
   - Copy **App ID**
   - Copy **App Name**

4. **Configure in Unity**
   - Unity → `Facebook > Edit Settings`
   - Enter **App ID**
   - Enter **App Name**
   - Configure platform-specific settings
   
   Then:
   - Open: `Tools > Sorolla Palette > Configure`
   - Facebook section → Click **"Enable Facebook Module"**
   - Confirm App ID and Name
   - Click Save

5. **Test**
```csharp
// Facebook auto-initializes with Sorolla Palette
// Just start logging events
SorollaPalette.TrackDesignEvent("test");
```

---

## 4️⃣ **Adjust SDK** - Auto-Installed ✅ (Full Mode Only)

### Status: Auto-Installed
**Not used in:** Prototype Mode (uses Facebook for UA tracking instead)
**Required for:** Full Mode

Adjust SDK is automatically installed via Unity Package Manager when you select **Full Mode** in the mode selector wizard. No manual download required!

### Configuration

1. **Create Adjust App**
    - Go to: https://www.adjust.com/ (or https://suite.adjust.com/)
    - Sign up for account (**~$2000+/month**)
    - Create new app
    - Copy **App Token**

2. **Configure in Unity**
    - Open: `Tools > Sorolla Palette > Configure`
    - Ensure you're in **Full Mode**
    - Adjust section → Click **"Enable Adjust Module"**
    - Enter **App Token**
    - Select **Environment**:
      - **Sandbox** - For testing
      - **Production** - For live app
    - Click Save

3. **Test**
```csharp
// Adjust auto-initializes with Sorolla Palette
// Events are tracked automatically
// Ad revenue is automatically forwarded to Adjust (from MAX)
```

### Note About Adjust
- **Prototype Mode**: Adjust is **not used**. Facebook SDK handles UA tracking (free).
- **Full Mode**: Adjust is **required** for full attribution tracking and ad revenue forwarding from MAX.
- Adjust subscription cost: ~$2000+/month.

---

## 5️⃣ **External Dependency Manager** - Auto-Installed ✅

### Status: Already Installed
External Dependency Manager (EDM) is automatically installed. Handles Android/iOS dependencies for all SDKs.

### Post-Installation

After all SDKs are installed:

1. **Resolve Dependencies**
   - Unity → `Assets > External Dependency Manager > Android Resolver > Resolve`
   - Wait for completion (2-5 minutes)
   - Check Console for any errors

2. **Force Resolve** (if issues)
   - `Assets > External Dependency Manager > Android Resolver > Force Resolve`

3. **Verify**
   - Check `Assets/Plugins/Android/` for `.aar` files
   - Each SDK should have its dependencies resolved

---

## ✅ **Post-Integration Checklist**

After installing all SDKs:

### Build Settings
- [ ] `File > Build Settings > Android`
  - Minimum API Level: 21 (Android 5.0)
  - Target API Level: 34+ (Android 14)
  - Scripting Backend: IL2CPP (for 64-bit)

### Player Settings
- [ ] `Edit > Project Settings > Player > Android`
  - Internet Access: **Require**
  - Write Permission: **External (SDCard)**

### SDK Verification
- [ ] GameAnalytics: Check events in dashboard after test
- [ ] MAX: Test ads in test mode
- [ ] Facebook: Verify events in Events Manager
- [ ] Adjust: Check install event in dashboard (sandbox)

### Scripting Defines (Automatic)
These are added automatically when you enable modules:
- `SOROLLA_FACEBOOK_ENABLED` - Facebook module
- `SOROLLA_MAX_ENABLED` - MAX module
- `SOROLLA_ADJUST_ENABLED` - Adjust module

Check: `Edit > Project Settings > Player > Scripting Define Symbols`

---

## 🆘 **Troubleshooting**

### EDM Not Resolving
- ✅ Check internet connection
- ✅ Try: `Force Resolve`
- ✅ Restart Unity
- ✅ Check Console for specific errors

### SDK Not Detected
- ✅ Ensure `.unitypackage` was imported completely
- ✅ Check for compilation errors in Console
- ✅ Restart Unity
- ✅ Click "Refresh" in Configuration Window

### Module Not Compiling
- ✅ Check SDK is installed (Configuration Window shows status)
- ✅ Check scripting define symbol was added
- ✅ Check `Edit > Project Settings > Player > Scripting Define Symbols`
- ✅ Reimport package if needed

### Build Errors
- ✅ Run EDM Force Resolve
- ✅ Clean build folder
- ✅ Check minimum API level (21+)
- ✅ Check all required permissions are set

---

## 🎯 **Quick Reference**

### Dashboard Links
- **GameAnalytics**: https://gameanalytics.com/
- **AppLovin MAX**: https://dash.applovin.com/
- **Facebook**: https://developers.facebook.com/
- **Adjust**: https://www.adjust.com/

### Download Links
- **Facebook SDK**: https://developers.facebook.com/docs/unity/downloads/
- **Adjust SDK**: Auto-installed (via UPM)
- **GameAnalytics**: Auto-installed
- **MAX**: Auto-installed

### Documentation
- **GameAnalytics**: https://docs.gameanalytics.com/integrations/sdk/unity/
- **AppLovin MAX**: https://dash.applovin.com/documentation/mediation/unity/getting-started/integration
- **Adjust**: https://help.adjust.com/en/article/get-started-unity
- **Facebook**: https://developers.facebook.com/docs/unity/

---

**All set! Your SDKs are integrated and ready to use.** 🎉

