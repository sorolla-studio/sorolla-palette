# Switching to Full Mode

Upgrade from Prototype to production-ready setup.

**What's added:**
- **AppLovin MAX** - Ads with mediation networks
- **Adjust** - Full attribution tracking
- **GDPR/ATT** - EU compliance and iOS consent

**Time:** ~30 minutes (assuming Prototype already configured)

---

## Who Does What

If Sorolla is handling your onboarding, check this first:

| Activity | Sorolla | Studio |
|----------|---------|--------|
| Provide package name / bundle ID | - | **R** |
| Provide test builds | - | **R** |
| Create vendor dashboards (GA, FB, Firebase, MAX, Adjust, TikTok) | **R** | I |
| Fill SorollaConfig with keys | **R** | - |
| Configure consent (GDPR/ATT) | **R** | C |
| Provide privacy policy URL | C | **R** |
| Set up `app-ads.txt` on store domain | C | **R** |
| QA on device (Android + iOS) | **R** | C |

R = Responsible, C = Consulted, I = Informed

If you received pre-filled keys from Sorolla, skip to [Unity Configuration](#3-unity-configuration).

---

## Identifier Cross-Reference

Before configuring anything, fill in this table and confirm every row points to the same app:

| Service | Android identifier | iOS identifier |
|---------|-------------------|----------------|
| Unity Player Settings | `com.yourcompany.yourgame` | `com.yourcompany.yourgame` |
| GameAnalytics | Game Key: _______ | Game Key: _______ |
| Facebook | App ID: _______ | App ID: _______ |
| Firebase | `google-services.json` pkg: _______ | `GoogleService-Info.plist` bundle: _______ |
| AppLovin MAX | Rewarded: _______ / Interstitial: _______ | Rewarded: _______ / Interstitial: _______ |
| Adjust | App Token: _______ | App Token: _______ |
| TikTok | App ID: _______ / EM App ID: _______ | App ID: _______ / EM App ID: _______ |

**If any package name or bundle ID differs from Unity Player Settings, stop and fix it.** Mismatched identifiers cause silent data loss - attribution goes to wrong apps, events disappear, paid UA gets wasted.

---

## 1. Switch Mode

1. Open **Palette > Configuration**
2. Click **Switch to Full**
3. Wait for packages to install

---

## 2. Configure SDKs

### Adjust

1. Enter **App Token** in SorollaConfig under Adjust
2. Set `adjustSandboxMode = true` for testing
3. Enter **Purchase Event Token** (from Adjust dashboard > Events)

[Full Adjust Guide](guides/adjust.md)

### Ads (AppLovin MAX)

1. Enter **Ad Unit IDs** (Rewarded, Interstitial, Banner) in SorollaConfig
2. SDK Key is account-level and already set if Sorolla configured it

[Full Ads Guide](guides/ads.md)

### TikTok (Optional, mode-independent)

If TikTok campaign tracking is needed, enter `tiktokAppId`, `tiktokEmAppId`, and `tiktokAccessToken` per platform. TikTok activates when fields are populated.

[Full TikTok Guide](guides/tiktok.md)

---

## 3. Configure Consent (GDPR + ATT)

Consent must be configured in the right order:

1. **First:** Publish GDPR consent message in AdMob (must be live before the app launches)
2. **Then:** Install **Google Ad Manager** (or Google AdMob) mediated network in MAX Integration Manager. MAX needs the Google Mobile Ads SDK to render the UMP consent form - without it, the CMP dialog silently won't appear.
3. **Then:** Configure MAX consent flow in Unity
4. **Then:** Build and test

Reversing this order or skipping step 2 causes a silent failure - the app runs, ads load, but the GDPR dialog never appears for EU users.

[Full GDPR/ATT Guide](guides/gdpr.md) - covers AdMob setup, MAX Integration Manager, privacy button code, and testing.

---

## 4. Unity Configuration

### Verify Scripting Defines

Open Player Settings > Other Settings > Scripting Define Symbols. Both Android and iOS need:
- `SOROLLA_MAX_ENABLED`
- `APPLOVIN_MAX_INSTALLED`
- `ADJUST_SDK_INSTALLED`
- `FIREBASE_ANALYTICS_INSTALLED`
- `FIREBASE_CRASHLYTICS_INSTALLED`
- `FIREBASE_REMOTE_CONFIG_INSTALLED`

If any are missing, run **Palette > Run Setup (Force)**.

### Build Health

Open **Palette > Configuration** > **Build Health**. All six checks must be green:
- SDK Versions
- Mode Consistency
- Scoped Registries
- Firebase Coherence
- Config Sync
- Android Manifest

---

## 5. QA Preflight

### Android

| Check | How | Pass |
|-------|-----|------|
| Build succeeds | Build AAB/APK from Unity | No errors |
| App launches | Install on device | No crash on startup |
| Consent dialog | First launch (EU locale) | UMP dialog appears |
| Debug UI green | Triple-tap screen | All SDKs show green |
| GA events | Play a level, check GA dashboard (5-10 min) | Progression events appear |
| Firebase events | Check Firebase Console (may take 24h) | Analytics events appear |
| Crashlytics | Force crash via Debug UI, relaunch | Crash appears in console |
| Ads load | Wait 30s after init, trigger rewarded ad | Ad displays and completes |
| Adjust sandbox | Check Adjust dashboard > Sandbox filter | Install + events appear |
| TikTok events | Check Events Manager > Test Events | Events appear |
| Mediation Debugger | AppLovin > Show Mediation Debugger | No red warnings |

### iOS

Same as Android, plus:

| Check | How | Pass |
|-------|-----|------|
| ATT dialog | First launch on iOS 14.5+ | Tracking prompt after consent |
| CocoaPods | Build to Xcode | No pod errors |
| Provisioning | Xcode > Unity-iPhone > Signing | Auto-signing succeeds |

---

## Before Release

Switch from test to production settings:

- [ ] `adjustSandboxMode` = **false**
- [ ] `tiktokDebugMode` = **false**
- [ ] Test mode off on all mediation networks
- [ ] GDPR consent message **published** in AdMob
- [ ] Privacy Policy URL set in MAX Integration Manager
- [ ] ATT Usage Description set
- [ ] Privacy settings button added to game ([code example](guides/gdpr.md#3-add-privacy-button))
- [ ] `app-ads.txt` live on developer website
- [ ] All SDKs green in Debug UI on device
- [ ] Identifier cross-reference table verified (above)

---

## Help

- [Troubleshooting](troubleshooting.md)
- [API Reference](api-reference.md)
- [Android Build Compatibility](guides/android-build-compatibility.md)
