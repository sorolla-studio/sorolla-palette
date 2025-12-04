# Prototype Mode Setup Guide

This guide covers SDK setup for **Prototype Mode** - designed for rapid UA testing during development.

## Required SDKs

- **GameAnalytics** - Analytics & Remote Config (Required)
- **Facebook SDK** - User Acquisition Testing (Required)
- **AppLovin MAX** - Ad Monetization (Optional)

---

## 1. GameAnalytics Setup (Required)

### Create Account & Game Project

1. Go to [https://gameanalytics.com](https://gameanalytics.com)
2. Click **"Sign Up"** and complete registration
3. Click **"Add Game"** or **"Create Game"**
4. Fill in game details:
   - **Game Name**: Your game's name
   - **Platform**: Select iOS and/or Android
   - **Engine**: Select Unity
5. Click **"Create Game"**

### Get API Keys

1. Navigate to **Settings** → **Game Settings** (gear icon)
2. Copy both keys:
   - **Game Key**: Hexadecimal string (e.g., `a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4`)
   - **Secret Key**: Another hexadecimal string
3. **Keep these keys secure**

### Configure in Unity

1. Open **Window** → **GameAnalytics** → **Select Settings**
2. Paste **Game Key** and **Secret Key**
3. Click **"Save"**

**Alternative:** Use the **"Open Settings"** button in the Sorolla Configuration window.

---

## 2. Facebook SDK Setup (Required)

⚠️ **This is the most common point of failure** - complete ALL steps carefully.

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

#### Get Client Token (CRITICAL)

⚠️ **Without this, your app will crash on launch**

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
2. Enter:
   - **Package Name**: Must match Unity Bundle ID exactly
   - **Class Name**: `com.unity3d.player.UnityPlayerActivity`
3. Click **"Save Changes"**

### Configure Key Hashes (CRITICAL for Android)

⚠️ **Without proper key hashes, Facebook Login will fail**

#### Debug Key Hash (for development):
```bash
keytool -exportcert -alias androiddebugkey -keystore ~/.android/debug.keystore | openssl sha1 -binary | openssl base64
```
- Default password: `android`
- Copy hash and paste in **Key Hashes** field on Facebook dashboard

#### Release Key Hash (CRITICAL for production):
```bash
keytool -exportcert -alias YOUR_ALIAS -keystore YOUR_KEYSTORE | openssl sha1 -binary | openssl base64
```
- Enter your keystore password when prompted
- Add this hash to **Key Hashes** field (can have multiple)

⚠️ **Important:** If release key hash is missing, Facebook Login works in debug but fails in production!

### Authorize Sorolla Ad Account

⚠️ **CRITICAL:** Without this step, UA campaigns cannot be launched.

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

**Alternative:** Use the **"Open Settings"** button in Sorolla Configuration window.

---

## 3. AppLovin MAX Setup (Optional)

Optional for prototype testing. Required for Full Mode.

### Create Account

1. Go to [https://dash.applovin.com/signup](https://dash.applovin.com/signup)
2. Complete registration and verify email
3. Log in to dashboard

### Get SDK Key

1. Navigate to **Account** → **Keys** (top right profile dropdown)
2. Copy your **SDK Key** (long alphanumeric string)

### Create Ad Units

#### Rewarded Ad Unit:
1. Go to **Monetize** → **Manage** → **Ad Units**
2. Click **"Create Ad Unit"**
3. Select your app (create if needed)
4. Choose **"Rewarded"** type
5. Enter **Ad Unit Name** (e.g., "Rewarded Video")
6. Click **"Create"** and copy **Ad Unit ID**

#### Interstitial Ad Unit:
1. Click **"Create Ad Unit"** again
2. Select **"Interstitial"** type
3. Enter **Ad Unit Name** (e.g., "Interstitial Ad")
4. Click **"Create"** and copy **Ad Unit ID**

### Configure in Unity

1. Open **Sorolla Configuration** window (Sorolla → Configuration)
2. Under **SDK Keys**, enter:
   - **SDK Key**
   - **Rewarded Ad Unit ID**
   - **Interstitial Ad Unit ID**
3. Click **"Save"**

---

## 4. GameAnalytics Admin Access (Required)

⚠️ **Required for publishing team to debug UA and analytics issues**

### Grant Admin Access

1. Log in to [GameAnalytics](https://gameanalytics.com)
2. Select your game from the top-left dropdown
3. Click **Settings (Gear Icon)** in bottom-left menu
4. Click **Users** tab
5. Click **Invite User** (top right)
6. Enter email: `studio@sorolla.io`
7. ⚠️ **IMPORTANT:** Set Role to **Admin** (Viewer is not enough)
8. Send Invite

---

## Prototype Mode Checklist

**Before launching UA campaigns, verify:**

- [ ] GameAnalytics: Game Key and Secret Key configured
- [ ] GameAnalytics: Admin access granted to `studio@sorolla.io`
- [ ] Facebook SDK: App ID configured
- [ ] Facebook SDK: Client Token configured ⚠️
- [ ] Facebook SDK: Debug Key Hash added
- [ ] Facebook SDK: Release Key Hash added ⚠️
- [ ] Facebook SDK: Sorolla Ad Account (`1130531078835118`) authorized ⚠️
- [ ] AppLovin MAX: SDK Key, Rewarded, and Interstitial IDs configured (if using ads)

---

## Quick Reference

| SDK | What You Need | Where to Find It |
|-----|---------------|------------------|
| **GameAnalytics** | Game Key<br>Secret Key | [gameanalytics.com](https://gameanalytics.com) → Settings → Game Settings |
| **Facebook SDK** | App ID<br>Client Token ⚠️<br>Debug Key Hash<br>Release Key Hash ⚠️<br>Ad Account ID ⚠️ | [developers.facebook.com](https://developers.facebook.com)<br>→ Settings → Basic (App ID)<br>→ Settings → Advanced (Client Token)<br>→ Settings → Advanced (Ad Account)<br>→ Key Hashes via keytool |
| **AppLovin MAX**<br>(Optional) | SDK Key<br>Rewarded ID<br>Interstitial ID | [dash.applovin.com](https://dash.applovin.com)<br>→ Account → Keys<br>→ Monetize → Ad Units |

---

## Need Help?

- 📖 [Documentation Index](index.md)
- 🐙 [GitHub Repository](https://github.com/LaCreArthur/sorolla-palette-upm)
- 🐛 [Report Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)

### Support Resources

- **GameAnalytics**: [docs.gameanalytics.com](https://docs.gameanalytics.com)
- **Facebook SDK**: [developers.facebook.com/docs](https://developers.facebook.com/docs)
- **AppLovin MAX**: [dash.applovin.com/documentation](https://dash.applovin.com/documentation)
