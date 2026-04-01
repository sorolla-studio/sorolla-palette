# External Studio Dashboard Setup (Sorolla Internal)

Step-by-step for Sorolla team members setting up vendor dashboards for an external studio game.

For the studio-facing guide, see [Switching to Full Mode](../switching-to-full.md).

---

## Phase 0: Collect from Studio

Before touching any dashboard, get these from the studio:

| Item | Example | Notes |
|------|---------|-------|
| Android package name | `com.gembirdstudios.raftevolution` | Final. Cannot change after any vendor app is created. |
| iOS bundle ID | `com.gembirdstudios.raftevolution` | Final. Usually matches Android but not always. |
| Test APK/AAB (Android) | - | Signed with debug or release key |
| Test IPA / TestFlight build (iOS) | - | For on-device QA |
| Android debug key hash | SHA-1 from `keytool` | Needed for Facebook Android |
| Studio contact for dashboard invites | email | They'll receive admin invites |

**Lock the package name and bundle ID before proceeding.** Firebase, Facebook, AppLovin ad units, and Play-linked Crashlytics all depend on exact identifier matches. Changing it later means recreating every vendor app.

---

## Phase 1: Dashboard Setup

### 1. GameAnalytics

**Dashboard:** [gameanalytics.com](https://gameanalytics.com)

1. Create Organization (or use existing Sorolla org)
2. Add Game - enter exact package name (Android) and bundle ID (iOS)
3. Copy **Game Key** and **Secret Key** (per platform)
4. Grant studio `studio@sorolla.io`-level admin access

**Into Unity:**
- Window > GameAnalytics > Select Settings > enter Game Key + Secret Key

---

### 2. Facebook

**Variant A - Studio has no existing Meta app:**

1. Create Meta App (type: Business) at [developers.facebook.com](https://developers.facebook.com)
2. Settings > Basic: copy **App ID**
3. Settings > Advanced: copy **Client Token**
4. Add Android platform:
   - Package Name: exact match
   - Key Hash: from studio's debug signing key
   - Class Name: from Unity Facebook settings
5. Add iOS platform:
   - Bundle ID: exact match

**Variant B - Studio already has a Meta app registered:**

Do NOT create a duplicate app. Facebook deduplicates by package name/bundle ID - a second app causes event routing conflicts.

1. Studio grants Sorolla **Admin** role:
   - App Dashboard > App Roles > Roles > Add People > enter Sorolla email > Admin
2. Copy **App ID** (Settings > Basic) and **Client Token** (Settings > Advanced)
3. Verify platform settings match Unity (Package Name, Key Hash, Class Name, Bundle ID)

**Into Unity:**
- Facebook > Edit Settings > App ID + Client Token
- Also in `AndroidManifest.xml` as `<meta-data>` entries

---

### 3. Firebase

**Dashboard:** [console.firebase.google.com](https://console.firebase.google.com)

1. Create Firebase project with **Google Analytics enabled**
2. Register Android app with exact package name
3. Register iOS app with exact bundle ID
4. Download `google-services.json` (Android) and `GoogleService-Info.plist` (iOS)
5. Place both in Unity `Assets/` with exact filenames (no suffixes like `(2)`)

---

### 4. AppLovin MAX

**Dashboard:** [dash.applovin.com](https://dash.applovin.com)

1. **SDK Key** is account-level (shared across games) - already in Sorolla's account
2. Monetize > Manage > Ad Units: create per-platform units:
   - Rewarded (Android + iOS)
   - Interstitial (Android + iOS)
   - Banner (Android + iOS, optional)
3. Enter the exact package name / bundle ID when creating (app doesn't need to be live)
4. Copy each **Ad Unit ID**
5. Set up mediation: AdMob, Meta Audience Network, Unity Ads
6. Coordinate `app-ads.txt` with studio (must be on their developer website domain)

---

### 5. Adjust

**Dashboard:** [dash.adjust.com](https://dash.adjust.com)

1. Create App (one per platform, or unified)
2. Copy **App Token** (12-character string)
3. Create Purchase event > copy **Event Token**
4. Set environment: **Sandbox** for testing, **Production** for release

**Note:** Event tokens must belong to the same app token. Do not copy from another game.

---

### 6. TikTok

**Dashboard:** TikTok Events Manager ([ads.tiktok.com](https://ads.tiktok.com))

1. Create App in Events Manager (or attach to existing MMP-connected app)
2. Collect three values per platform:

| TikTok Dashboard field | SorollaConfig field | Maps to SDK call |
|------------------------|---------------------|------------------|
| App ID (Events Manager) | `tiktokEmAppId` | `TTConfig` constructor `setAppId` |
| TikTok App ID (long numeric) | `tiktokAppId` | `setTTAppId` |
| Access Token (App Secret) | `tiktokAccessToken` | `TTConfig` constructor |

---

## Consent Setup Order

This order matters. Reversing it causes silent failure (app works, GDPR dialog never shows):

1. **AdMob:** Privacy & Messaging > GDPR > create message > enable ad partners (AppLovin, AdMob, Meta, Unity) > **Publish**
2. **MAX Integration Manager:** install **Google Ad Manager** (or Google AdMob) under Mediated Networks (required for UMP consent form to render)
3. **MAX Integration Manager:** enable Terms and Privacy Policy Flow > set Privacy Policy URL > set ATT Usage Description > Save
4. **Build and test**

---

## Initialization Order (Reference)

How Palette.cs initializes in Full mode:

1. MAX SDK initializes (handles CMP consent first)
2. On MAX initialized callback:
   - Adjust initializes (reads consent state set by MAX)
   - MAX ad loading begins (rewarded, interstitial, banner)
   - ILRD callback registered (ad revenue -> Adjust + TikTok)
3. Firebase initializes (Analytics, Crashlytics, Remote Config)
4. TikTok initializes (if config fields populated)
5. GameAnalytics initializes
6. Facebook initializes (if `SOROLLA_FACEBOOK_ENABLED`)

Consent is resolved before attribution starts. Do not reorder.
