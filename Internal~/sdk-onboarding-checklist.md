# Sorolla SDK Integration Runbook

Single-file guide for integrating the Sorolla SDK into a studio's Unity game.
Read this file, execute top to bottom, everything gets integrated.

Audited 2026-04-15 across hungrysnake, romba-clean, boat-runner.

---

## Mode System

Pick a mode before starting. This determines which SDKs get installed and which dashboards you need.

| | Prototype | Full |
|---|---|---|
| **Use case** | CPI tests, soft launch | Production |
| **Core SDKs** (always required) | EDM4U, GameAnalytics, Facebook | EDM4U, GameAnalytics, Facebook |
| **MAX + Firebase** | Optional (manual install, never auto-uninstalled) | Required (auto-installed) |
| **Adjust** | Not available (auto-uninstalled) | Required (auto-installed) |
| **Mediation adapters** | Only if MAX installed | Required |
| **Scripting define** | `SOROLLA_PROTOTYPE` | `SOROLLA_FULL` |

TikTok is mode-independent - enabled when `SorollaConfig` fields are populated, disabled when empty.

**Key distinction**: FullRequired SDKs (MAX, Firebase) are optional in Prototype but never auto-removed. Adjust is FullOnly - auto-uninstalled when switching to Prototype.

---

## Step 1: Prerequisites (human)

Before touching Unity, collect these. Without them, integration stalls.

### Required for both modes

| What | Where to get it | Docs |
|---|---|---|
| Android package name (final) | Studio decides | - |
| iOS bundle ID (final) | Studio decides | - |
| GameAnalytics game key + secret (per platform) | [gameanalytics.com](https://gameanalytics.com) > Game Settings | `Documentation~/guides/gameanalytics.md` |
| Facebook App ID + Client Token | [developers.facebook.com](https://developers.facebook.com) > Settings | `Documentation~/guides/facebook.md` |

### Required for Full mode (skip for Prototype)

| What | Where to get it | Docs |
|---|---|---|
| `google-services.json` (Android) | [Firebase Console](https://console.firebase.google.com) > Project Settings > Your Apps | `Documentation~/guides/firebase.md` |
| `GoogleService-Info.plist` (iOS) | Same | Same |
| MAX Ad Unit IDs (rewarded + interstitial, per platform) | [dash.applovin.com](https://dash.applovin.com) > Ad Units | `Documentation~/guides/ads.md` |
| AdMob App IDs (per platform) | MAX Dashboard > Mediation > Networks > Google | `Documentation~/guides/ads.md` |
| Adjust App Token + Purchase Event Token | [dash.adjust.com](https://dash.adjust.com) | `Documentation~/guides/adjust.md` |

**Full dashboard creation walkthrough:** `Documentation~/dashboard-setup.md`

### Dashboard creation order (for new game)

Prototype: (1) Meta Developer Dashboard, (2) GameAnalytics.
Full adds: (3) Firebase Console, (4) AppLovin + AdMob, (5) Adjust.
Optional: (6) TikTok Ads Manager.

---

## Step 2: Install SDK (automatable)

In the game project's `Packages/manifest.json`, add to `dependencies`:

```json
"com.sorolla.sdk": "https://github.com/sorolla-studio/sorolla-palette.git#v3.9.0"
```

SDK auto-opens the Configuration window on first install and runs setup.

If already installed, open **Palette > Configuration** to select mode.

---

## Step 3: Select mode (automatable)

Via Unity menu: **Palette > Configuration** > click Prototype or Full.

This triggers `SorollaSettings.SetMode()` which:
1. Sets `SOROLLA_PROTOTYPE` or `SOROLLA_FULL` define
2. Updates `SorollaConfig.isPrototypeMode`
3. Auto-installs required SDKs for the mode
4. Auto-uninstalls mode-incompatible SDKs (e.g., Adjust in Prototype)

All scripting defines are auto-managed from here. No manual define editing needed.

---

## Step 4: Configure per-game values (automatable once values collected)

### 4a. GameAnalytics (both modes)

**File:** `Assets/Resources/GameAnalytics/Settings.asset`

Set per platform: Game Key, Secret Key.

### 4b. Facebook (both modes)

**File:** `Assets/Resources/FacebookSettings.asset`
- App ID
- Client Token

**File:** `Assets/Plugins/Android/AndroidManifest.xml`
- `<meta-data android:name="com.facebook.sdk.ApplicationId" android:value="fb{APP_ID}"/>` (note `fb` prefix)
- `<meta-data android:name="com.facebook.sdk.ClientToken" android:value="{CLIENT_TOKEN}"/>`
- `<provider android:authorities="com.facebook.app.FacebookContentProvider{APP_ID}"/>` (raw ID, no prefix)

### 4c. Firebase (Full mode, optional in Prototype)

**Files:**
- `Assets/google-services.json` - download from Firebase Console (Android)
- `Assets/GoogleService-Info.plist` - download from Firebase Console (iOS)

Place with exact filenames. Firebase Editor DLL processes these on domain reload to generate `Assets/Plugins/Android/FirebaseApp.androidlib/`.

### 4d. MAX Ad Units (Full mode, optional in Prototype)

**File:** `Assets/Resources/SorollaConfig.asset`
- `rewardedAdUnitIdAndroid` / `rewardedAdUnitIdIos`
- `interstitialAdUnitIdAndroid` / `interstitialAdUnitIdIos`
- `bannerAdUnitIdAndroid` / `bannerAdUnitIdIos` (optional)

**File:** `Assets/MaxSdk/Resources/AppLovinSettings.asset`
- AdMob Android App ID
- AdMob iOS App ID
- SDK Key (shared: `***REDACTED***`)

### 4e. Adjust (Full mode only)

**File:** `Assets/Resources/SorollaConfig.asset`
- `adjustAppToken` (12-char string)
- `adjustPurchaseEventToken`
- `adjustSandboxMode` = 1 for testing, 0 for production

### 4f. TikTok (optional, mode-independent)

**File:** `Assets/Resources/SorollaConfig.asset`
- `enableTikTok` = true
- `tiktokAppId`, `tiktokEmAppId`, `tiktokAccessToken` (per platform)

Guide: `Documentation~/guides/tiktok.md`

---

## Step 5: Verify manifest.json packages (automatable)

Mode switch auto-installs required packages. Verify `Packages/manifest.json` contains:

### Core (both modes)

| Package | Value |
|---|---|
| `com.google.external-dependency-manager` | `1.2.187` |
| `com.gameanalytics.sdk` | `7.10.6` |
| `com.lacrearthur.facebook-sdk-for-unity` | `https://github.com/LaCreArthur/facebook-unity-sdk-upm.git` |
| `com.sorolla.sdk` | `https://github.com/sorolla-studio/sorolla-palette.git#v3.9.0` |

### Full mode required (optional in Prototype)

| Package | Value |
|---|---|
| `com.applovin.mediation.ads` | `8.6.2` |
| `com.google.firebase.app` | `https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseApp#13.7.0` |
| `com.google.firebase.analytics` | `https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseAnalytics#13.7.0` |
| `com.google.firebase.crashlytics` | `https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseCrashlytics#13.7.0` |
| `com.google.firebase.remote-config` | `https://github.com/LaCreArthur/unity-firebase-app.git?path=FirebaseRemoteConfig#13.7.0` |
| Mediation adapters | facebook 6210000, google 25010000/13020000, googleadmanager same, ironsource 904000000, unityads 4170000, vungle 7070100 |

### Full mode only (uninstalled in Prototype)

| Package | Value |
|---|---|
| `com.adjust.sdk` | `https://github.com/adjust/unity_sdk.git?path=Assets/Adjust` |

### Scoped registries (both modes)

```json
"scopedRegistries": [
  {
    "name": "OpenUPM",
    "url": "https://package.openupm.com",
    "scopes": ["com.google.external-dependency-manager", "com.gameanalytics"]
  },
  {
    "name": "AppLovin MAX",
    "url": "https://package.openupm.com",
    "scopes": ["com.applovin"]
  }
]
```

---

## Step 6: Verify (automatable)

### Both modes
- [ ] Mode define matches intent (`SOROLLA_PROTOTYPE` or `SOROLLA_FULL`)
- [ ] `SorollaConfig.isPrototypeMode` matches mode define (BuildValidator auto-fixes mismatches)
- [ ] AndroidManifest has `UnityPlayerGameActivity` (not `UnityPlayerActivity`)
- [ ] AndroidManifest has NO `debuggable="true"`
- [ ] Facebook App ID in manifest matches FacebookSettings.asset
- [ ] Build > no compile errors
- [ ] Run > SDK initializes (check Sorolla logs)

### Full mode only
- [ ] AdMob IDs non-empty in AppLovinSettings
- [ ] Adjust app token and purchase event token non-empty in SorollaConfig
- [ ] Bundle ID in google-services.json matches PlayerSettings
- [ ] Bundle ID in GoogleService-Info.plist matches PlayerSettings
- [ ] All mediation adapters present in manifest.json

**Note on `tools:replace`:** `AndroidManifestSanitizer` conditionally adds `tools:replace="android:theme"` when a custom launcher manifest exists (prevents Gradle merge conflicts). Expected in that case.

### On-device QA

Full QA checklist: `Documentation~/switching-to-full.md` > Section 5 (QA Preflight).

---

## Scripting Defines Reference

For debugging only. All defines are auto-managed - never set manually.

### Auto-managed by DefineSymbols.cs (global PlayerSettings)

| Define | Set when package installed |
|---|---|
| `APPLOVIN_MAX_INSTALLED` | `com.applovin.mediation.ads` |
| `SOROLLA_MAX_ENABLED` | `com.applovin.mediation.ads` |
| `ADJUST_SDK_INSTALLED` | `com.adjust.sdk` |
| `SOROLLA_ADJUST_ENABLED` | `com.adjust.sdk` |
| `FIREBASE_ANALYTICS_INSTALLED` | `com.google.firebase.analytics` |
| `FIREBASE_CRASHLYTICS_INSTALLED` | `com.google.firebase.crashlytics` |
| `FIREBASE_REMOTE_CONFIG_INSTALLED` | `com.google.firebase.remote-config` |

### Per-assembly versionDefines (NOT in PlayerSettings)

| Define | Assembly | Detects |
|---|---|---|
| `SOROLLA_FACEBOOK_ENABLED` | `Sorolla.Runtime`, `Sorolla.Adapters` | Facebook SDK package |
| `SOROLLA_*_ASMDEF_OK` | Implementation asmdefs | Used with `defineConstraints` to gate assembly compilation |

### Mode define (set by Palette > Configuration)

- `SOROLLA_PROTOTYPE` or `SOROLLA_FULL` (mutually exclusive, set by `SorollaSettings.SetMode()`)

---

## Existing Game Reference

Per-game values for copy-paste when onboarding from an existing game.

### Shared across all games
- **MAX SDK Key**: `***REDACTED***`
- **AdMob Publisher prefix**: `ca-app-pub-7005161511720605`

### Facebook
| Field | hungrysnake | romba | boat-runner |
|---|---|---|---|
| App ID | `***REDACTED***` | `***REDACTED***` | `***REDACTED***` |
| Client Token | `***REDACTED***` | `***REDACTED***` | `***REDACTED***` |

### AdMob
| Field | hungrysnake | romba | boat-runner |
|---|---|---|---|
| Android App ID | `***REDACTED***` | `***REDACTED***` | **EMPTY** |
| iOS App ID | `***REDACTED***` | `***REDACTED***` | **EMPTY** |

### MAX Ad Units
| Field | hungrysnake | romba | boat-runner |
|---|---|---|---|
| Rewarded Android | `***REDACTED***` | `***REDACTED***` | `***REDACTED***` |
| Rewarded iOS | `***REDACTED***` | `***REDACTED***` | `***REDACTED***` |
| Interstitial Android | `***REDACTED***` | `***REDACTED***` | `***REDACTED***` |
| Interstitial iOS | `***REDACTED***` | `***REDACTED***` | `***REDACTED***` |

### Adjust
| Field | hungrysnake | romba | boat-runner |
|---|---|---|---|
| App Token | `***REDACTED***` | `***REDACTED***` | `***REDACTED***` |
| Purchase Event Token | `***REDACTED***` | `***REDACTED***` | **EMPTY** |

### Firebase
| Field | hungrysnake | romba | boat-runner |
|---|---|---|---|
| Project ID | `happy-snake-d94af` | `sweep-collector` | `raft-evolution` |
| Android API Key | `AIzaSyAOsN9Q...` | `AIzaSyAzOuHu...` | `AIzaSyAKMS11...` |
| iOS API Key | `AIzaSyBf2tDp...` | `AIzaSyA2lZ6U...` | `AIzaSyDPhm0H...` |

### GameAnalytics
| Field | hungrysnake | romba | boat-runner |
|---|---|---|---|
| Android Game Key | `b81639fc...` | `bacc9a69...` | `30fba914...` |
| Android Secret | `d02b460c...` | `f0462321...` | `051be03f...` |
| iOS Game Key | `f28a0965...` | `ce202fbe...` | `9f786c1b...` |
| iOS Secret | `a1062a5c...` | `f6508b70...` | `b051bd17...` |
