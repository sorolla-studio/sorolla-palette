# SDK Setup Guide for External Developers

This guide will walk you through obtaining and configuring all the necessary API keys for the Sorolla SDK. The process differs slightly depending on which mode you choose:

- **Prototype Mode**: GameAnalytics + Facebook SDK + AppLovin MAX (optional) (for rapid UA testing)
- **Full Mode**: GameAnalytics + AppLovin MAX (required) + Adjust (for production)

## Table of Contents

1. [GameAnalytics Setup (Required for Both Modes)](#1-gameanalytics-setup-required-for-both-modes)
2. [Facebook SDK Setup (Prototype Mode & Full Mode)](#2-facebook-sdk-setup-prototype-mode--full-mode)
3. [AppLovin MAX Setup](#3-applovin-max-setup)
4. [Adjust Setup (Full Mode)](#4-adjust-setup-full-mode)
5. [Quick Reference](#5-quick-reference)

---

## 1. GameAnalytics Setup (Required for Both Modes)

GameAnalytics provides analytics and remote configuration for your game.

### Step 1: Create a GameAnalytics Account

1. Go to [https://gameanalytics.com](https://gameanalytics.com)
2. Click **"Sign Up"** in the top right corner
3. Complete the registration process (free tier available)

### Step 2: Create a Game Project

1. Once logged in, click **"Add Game"** or **"Create Game"**
2. Fill in your game details:
   - **Game Name**: Your game's name
   - **Platform**: Select iOS and/or Android
   - **Engine**: Select Unity
3. Click **"Create Game"**

### Step 3: Get Your API Keys

After creating the game, you'll be taken to the game dashboard.

1. Navigate to **Settings** → **Game Settings** (or click the gear icon)
2. You'll find two keys you need:
   - **Game Key**: A hexadecimal string (e.g., `a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4`)
   - **Secret Key**: Another hexadecimal string
3. **Keep these keys secure** - you'll enter them in Unity

### Step 4: Configure in Unity

1. In Unity, open **Window** → **GameAnalytics** → **Select Settings**
2. Click **"Setup"** if you see the setup wizard
3. Paste your **Game Key** and **Secret Key** into the respective fields
4. Click **"Save"**

**Alternative Method:**
- The Sorolla Configuration window provides a direct **"Open Settings"** button in the Setup Checklist
- Click it to jump directly to the GameAnalytics settings

### What You Need:
- ✅ **Game Key** (from GameAnalytics dashboard)
- ✅ **Secret Key** (from GameAnalytics dashboard)

---

## 2. Facebook SDK Setup (Prototype Mode & Full Mode)

Facebook SDK enables user acquisition testing and event tracking. **This is the most common point of failure** - the Unity "Edit Settings" menu alone is not enough for proper configuration.

### Step 1: Create a Facebook Developer Account

1. Go to [https://developers.facebook.com](https://developers.facebook.com)
2. Click **"Get Started"** in the top right
3. Log in with your Facebook account or create one
4. Complete the developer account registration

### Step 2: Create a New App

1. Once logged in, go to **"My Apps"** in the top menu
2. Click **"Create App"**
3. Choose an app type:
   - Select **"Other"** or **"Gaming"** depending on your use case
   - Click **"Next"**
4. Fill in app details:
   - **App Name**: Your game's name
   - **App Contact Email**: Your email
5. Click **"Create App"**

### Step 3: Get Your App ID and Client Token

After creating the app:

1. You'll be redirected to the app dashboard
2. Your **App ID** is displayed prominently at the top of the dashboard
   - It's a numeric string (e.g., `1234567890123456`)
3. You can also find it under **Settings** → **Basic** in the left sidebar

#### Get Client Token (CRITICAL)

**⚠️ CRITICAL**: Without the Client Token, your app will crash on launch (required for Graph API v13+)

1. Go to **Settings** → **Advanced**
2. Scroll down to the **Security** section
3. Copy your **Client Token**
   - It's an alphanumeric string (e.g., `a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6`)
4. **Keep this token secure** - you'll need it in Unity

### Step 4: Configure iOS and Android

For mobile games, you need to add platform configurations:

#### For iOS:
1. In your app dashboard, go to **Settings** → **Basic**
2. Scroll down and click **"+ Add Platform"**
3. Select **"iOS"**
4. Enter your **Bundle ID** (e.g., `com.yourcompany.yourgame`)
5. Click **"Save Changes"**

#### For Android:
1. Click **"+ Add Platform"** again
2. Select **"Android"**
3. Enter your **Package Name**: Must match Unity Bundle ID exactly (e.g., `com.yourcompany.yourgame`)
4. Enter your **Class Name**: `com.unity3d.player.UnityPlayerActivity`
5. Click **"Save Changes"**

#### Configure Key Hashes (CRITICAL for Android)

**⚠️ CRITICAL**: Without proper key hashes, Facebook Login will fail in your app.

##### Debug Key Hash (for development):
1. On your development machine, open a terminal/command prompt
2. Run this command (replace paths as needed):
   ```bash
   keytool -exportcert -alias androiddebugkey -keystore ~/.android/debug.keystore | openssl sha1 -binary | openssl base64
   ```
   - Default password is usually `android`
3. Copy the generated hash and paste it in the **Key Hashes** field on Facebook dashboard

##### Release Key Hash (CRITICAL for production):
1. Locate your release keystore file (the .keystore file used to sign production builds)
2. Run this command (replace YOUR_ALIAS and YOUR_KEYSTORE with your values):
   ```bash
   keytool -exportcert -alias YOUR_ALIAS -keystore YOUR_KEYSTORE | openssl sha1 -binary | openssl base64
   ```
   - Enter your keystore password when prompted
3. Copy the generated hash
4. In Facebook dashboard, add this hash to **Key Hashes** field (you can have multiple hashes)

**⚠️ Important**: If the release key hash is missing, Facebook Login will work in debug builds but fail in your live app!

### Step 5: Configure in Unity

1. In Unity, open **Facebook** → **Edit Settings**
2. Enter the following:
   - **App ID**: Your Facebook App ID
   - **Client Token**: Your Client Token from Settings → Advanced → Security
3. The SDK will automatically configure it for iOS and Android

**Alternative Method:**
- The Sorolla Configuration window has an **"Open Settings"** button for Facebook
- This takes you directly to the Facebook Settings asset

### Step 6: Verify Android Configuration

The Sorolla SDK automatically handles most AndroidManifest.xml entries for Facebook. However, verify these settings:

#### Unity Project Settings (Android)
1. Go to **Edit** → **Project Settings** → **Player** → **Android tab**
2. Ensure your **Package Name** matches exactly what you entered in Facebook dashboard
3. Under **Publishing Settings**, verify your keystore is configured for release builds

#### AndroidManifest.xml (Auto-handled by SDK)
The Facebook SDK Unity package automatically adds required manifest entries. If you need to manually verify or customize, the manifest should include:
- Facebook App ID as meta-data
- Facebook ContentProvider
- Facebook Activity declarations

**Note**: The Sorolla SDK and Facebook Unity SDK handle manifest patching automatically during build. You typically don't need to manually edit AndroidManifest.xml.

### What You Need:
- ✅ **Facebook App ID** (from Facebook Developer Dashboard → Settings → Basic)
- ✅ **Client Token** (from Settings → Advanced → Security) **[CRITICAL]**
- ✅ **Debug Key Hash** (generated from your debug keystore)
- ✅ **Release Key Hash** (generated from your release keystore) **[CRITICAL for production]**

---

## 3. AppLovin MAX Setup

AppLovin MAX is the mediation platform for ad monetization.

**Mode Requirements:**
- **Prototype Mode**: Optional (for testing ads during development)
- **Full Mode**: Required (for production monetization)

### Step 1: Create an AppLovin Account

1. Go to [https://dash.applovin.com/signup](https://dash.applovin.com/signup)
2. Complete the registration form
3. Verify your email address
4. Log in to the AppLovin dashboard

### Step 2: Get Your SDK Key

1. Once logged in to the AppLovin dashboard
2. Navigate to **Account** → **Keys** (top right corner, click your profile icon/dropdown)
3. Copy your **SDK Key**
   - It's a long alphanumeric string (e.g., `abcdefghijklmnopqrstuvwxyz123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ`)
4. **Keep this key secure** - it's your main AppLovin identifier

### Step 3: Create Ad Units

You need to create ad units for each ad type you want to show.

#### Create a Rewarded Ad Unit:
1. In the dashboard, go to **Monetize** → **Manage** → **Ad Units**
2. Click **"Create Ad Unit"**
3. Select your app (or create it first if needed)
4. Choose **"Rewarded"** as the ad type
5. Enter an **Ad Unit Name** (e.g., "Rewarded Video")
6. Click **"Create"**
7. Copy the **Ad Unit ID** (e.g., `a1b2c3d4e5f6g7h8`)

#### Create an Interstitial Ad Unit:
1. Click **"Create Ad Unit"** again
2. Select your app
3. Choose **"Interstitial"** as the ad type
4. Enter an **Ad Unit Name** (e.g., "Interstitial Ad")
5. Click **"Create"**
6. Copy the **Ad Unit ID**

#### Optional - Create a Banner Ad Unit:
1. Follow the same process but select **"Banner"** as the ad type
2. Copy the **Ad Unit ID** if you plan to use banners

### Step 4: Configure Mediation Networks (Optional but Recommended)

For better fill rates and revenue:

1. Go to **Monetize** → **Manage** → **Mediation**
2. Enable ad networks like **AdMob**, **Meta Audience Network**, **Unity Ads**, etc.
3. Each network requires its own API keys - follow their respective setup guides
4. AppLovin will automatically optimize between networks

### Step 5: Configure in Unity

1. In Unity, open the **Sorolla Configuration** window (Sorolla → Configuration)
2. Under the **SDK Keys** section, enter:
   - **SDK Key**: Your AppLovin SDK Key from Step 2
   - **Rewarded Ad Unit ID**: From Step 3
   - **Interstitial Ad Unit ID**: From Step 3
   - **Banner Ad Unit ID**: (Optional) From Step 3
3. Click **"Save"**

You can also access the **AppLovin Integration Manager** from the Sorolla window for additional configuration.

### What You Need:
- ✅ **AppLovin SDK Key** (from Account → Keys)
- ✅ **Rewarded Ad Unit ID** (from Monetize → Manage → Ad Units)
- ✅ **Interstitial Ad Unit ID** (from Monetize → Manage → Ad Units)
- ⚪ **Banner Ad Unit ID** (optional)

---

## 4. Adjust Setup (Full Mode)

Adjust provides attribution tracking and analytics for production apps.

### Step 1: Create an Adjust Account

1. Go to [https://www.adjust.com](https://www.adjust.com)
2. Click **"Get Started"** or **"Sign Up"**
3. Complete the registration process
4. Verify your email and log in

### Step 2: Create an App

1. In the Adjust dashboard, click **"+ Create App"** or go to **Apps**
2. Click **"New App"**
3. Fill in app details:
   - **App Name**: Your game's name
   - **Platform**: Select iOS or Android (create separate apps for each)
   - **Bundle ID / Package Name**: Your app's identifier
4. Click **"Create App"**

### Step 3: Get Your App Token

After creating the app:

1. You'll be taken to the app overview page
2. Your **App Token** is displayed at the top
   - It's a 12-character alphanumeric string (e.g., `abc123def456`)
3. You can also find it under **All Settings** → **App Token**

**Note:** If you have both iOS and Android versions, you'll typically have separate app tokens for each platform in Adjust.

### Step 4: Configure Attribution Links (Optional)

For tracking campaign performance:

1. Go to **Campaign Lab** → **Trackers**
2. Create tracking links for your marketing campaigns
3. These links will attribute installs to specific campaigns

### Step 5: Configure in Unity

1. In Unity, open the **Sorolla Configuration** window
2. Under the **SDK Keys** section (visible in Full Mode), enter:
   - **Adjust App Token**: Your app token from Step 3
3. Click **"Save"**

**Platform-Specific Configuration:**
- The Sorolla SDK handles platform detection automatically at runtime
- If you have separate iOS and Android apps in Adjust, enter the token for your primary build target
- For multi-platform projects, the token is read from the configuration at build time for the target platform
- You can maintain separate Unity configurations or use conditional compilation if needed

### What You Need:
- ✅ **Adjust App Token** (from Adjust Dashboard → App Settings)
- ⚪ iOS App Token (if different from Android)
- ⚪ Android App Token (if different from iOS)

---

## 5. Quick Reference

### Prototype Mode Checklist

| SDK | What You Need | Where to Find It |
|-----|---------------|------------------|
| **GameAnalytics**<br>(Required) | Game Key<br>Secret Key | [gameanalytics.com](https://gameanalytics.com) → Settings → Game Settings |
| **Facebook**<br>(Required) | App ID<br>**Client Token** ⚠️<br>Debug Key Hash<br>Release Key Hash ⚠️ | [developers.facebook.com](https://developers.facebook.com)<br>→ Settings → Basic (App ID)<br>→ Settings → Advanced → Security (Client Token)<br>→ Key Hashes: Generated via keytool command |
| **AppLovin MAX**<br>(Optional) | SDK Key<br>Rewarded Ad Unit ID<br>Interstitial Ad Unit ID | [dash.applovin.com](https://dash.applovin.com) → Account → Keys<br>Monetize → Manage → Ad Units |

### Full Mode Checklist

| SDK | What You Need | Where to Find It |
|-----|---------------|------------------|
| **GameAnalytics**<br>(Required) | Game Key<br>Secret Key | [gameanalytics.com](https://gameanalytics.com) → Settings → Game Settings |
| **Facebook**<br>(Required) | App ID<br>**Client Token** ⚠️<br>Debug Key Hash<br>Release Key Hash ⚠️ | [developers.facebook.com](https://developers.facebook.com)<br>→ Settings → Basic (App ID)<br>→ Settings → Advanced → Security (Client Token)<br>→ Key Hashes: Generated via keytool command |
| **AppLovin MAX**<br>(Required) | SDK Key<br>Rewarded Ad Unit ID<br>Interstitial Ad Unit ID | [dash.applovin.com](https://dash.applovin.com) → Account → Keys<br>Monetize → Manage → Ad Units |
| **Adjust**<br>(Required) | App Token | [adjust.com](https://www.adjust.com) → Apps → All Settings |

### Configuration Locations in Unity

- **GameAnalytics**: `Window → GameAnalytics → Select Settings` or via Sorolla Config window
- **Facebook**: `Facebook → Edit Settings` or via Sorolla Config window
- **AppLovin MAX**: Enter in `Sorolla → Configuration` window under **SDK Keys**
- **Adjust**: Enter in `Sorolla → Configuration` window under **SDK Keys**

---

## Need Help?

- 📖 [Main Documentation](../README.md)
- 🐙 [GitHub Repository](https://github.com/LaCreArthur/sorolla-palette-upm)
- 🐛 [Report Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)

### Support Resources

- **GameAnalytics**: [docs.gameanalytics.com](https://docs.gameanalytics.com)
- **Facebook SDK**: [developers.facebook.com/docs](https://developers.facebook.com/docs)
- **AppLovin MAX**: [dash.applovin.com/documentation](https://dash.applovin.com/documentation)
- **Adjust**: [help.adjust.com](https://help.adjust.com)
