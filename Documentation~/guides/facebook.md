# Facebook SDK Setup

Attribution for UA campaigns.

---

## 1. Create App

1. Go to [developers.facebook.com](https://developers.facebook.com)
2. **My Apps** → **Create App**
3. Select **Other** or **Gaming** → **Next**
4. Enter app name and email

## 2. Get Credentials

1. **App ID** is shown at the top of your app dashboard
2. **Client Token**: Go to **Settings** → **Advanced** → **Security** section

## 3. Add Platforms

### iOS
1. **Settings** → **Basic** → **Add Platform** → **iOS**
2. Enter your **Bundle ID** (from Unity Player Settings)
3. Save

### Android
1. **Add Platform** → **Android**
2. Enter your **Package Name** (must match Unity Player Settings)
3. Save

## 4. Authorize Sorolla Ad Account

Required for UA campaign attribution:

1. Go to **Settings** → **Advanced**
2. Scroll to **Advertising Accounts**
3. In **Authorized Ad Account IDs**, enter: `1130531078835118`
4. Save

## 5. Configure in Unity

1. Open **Facebook** → **Edit Settings**
2. Enter **App ID** and **Client Token**
3. Save

---

## Verify

Install data appears in [Facebook Events Manager](https://business.facebook.com) after building and running on device.
