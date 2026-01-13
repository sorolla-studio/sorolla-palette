# GameAnalytics Setup

Analytics and event tracking for your game.

---

## 1. Create Account

1. Go to [gameanalytics.com](https://gameanalytics.com)
2. Sign up and create a new game
3. Select **iOS** and/or **Android**, Engine: **Unity**

## 2. Get API Keys

1. Open your game in GameAnalytics dashboard
2. Go to **Settings** → **Game Settings** (gear icon)
3. Copy:
   - **Game Key** (hexadecimal string)
   - **Secret Key** (hexadecimal string)

## 3. Configure in Unity

1. Open **Window** → **GameAnalytics** → **Select Settings**
2. Log in or paste keys manually
3. Save

## 4. Grant Admin Access

Required for Sorolla team support:

1. Go to **Settings** → **Users**
2. Click **Invite User**
3. Enter: `studio@sorolla.io`
4. Set Role: **Admin**
5. Send Invite

---

## Verify

Events appear in the dashboard within 5-10 minutes.

Use the [Debug UI](../quick-start.md#optional-debug-ui) to verify initialization on device.
