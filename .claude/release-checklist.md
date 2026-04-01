# Full Mode Release Checklist

Reference checklist for verifying a game's Palette SDK integration before release.
Use this to know **what to check** and **where to find it** in any game project.

## How to use

For each new game, create a local `.claude/palette-status.md` that tracks progress
against this checklist. This file is the reference; the status file is per-game.

---

## 1. App Identity

| Check | Where to verify |
|-------|-----------------|
| Android package name finalized | `ProjectSettings/ProjectSettings.asset` grep `bundleIdentifier` |
| iOS bundle ID finalized | Same file, iPhone section |
| Package name matches across all dashboards | Compare against each service below |

## 2. GameAnalytics

| Check | Where to verify |
|-------|-----------------|
| Settings asset exists | `Assets/Resources/GameAnalytics/Settings.asset` |
| Game keys set (per platform) | `gameKey` array in Settings.asset |
| Secret keys set (per platform) | `secretKey` array in Settings.asset |
| GA GameObject in starting scene | Check LoaderScene hierarchy for GameAnalytics object |

## 3. Facebook

| Check | Where to verify |
|-------|-----------------|
| App ID in AndroidManifest | `Assets/Plugins/Android/AndroidManifest.xml` grep `com.facebook.sdk.ApplicationId` |
| Client Token in AndroidManifest | Same file, grep `com.facebook.sdk.ClientToken` |
| `SOROLLA_FACEBOOK_ENABLED` define set (if using Palette Facebook adapter) | `ProjectSettings/ProjectSettings.asset` grep `SOROLLA_FACEBOOK_ENABLED` |
| Bundle ID matches Facebook dashboard | Compare Unity Player Settings vs developers.facebook.com |

## 4. Firebase

| Check | Where to verify |
|-------|-----------------|
| `google-services.json` present | `Assets/google-services.json` |
| `GoogleService-Info.plist` present | `Assets/GoogleService-Info.plist` |
| Package name matches in google-services.json | `package_name` field in the JSON |
| Bundle ID matches in GoogleService-Info.plist | `BUNDLE_ID` key in the plist |
| Firebase UPM packages installed | `Packages/manifest.json` grep `com.google.firebase` |
| `FIREBASE_ANALYTICS_INSTALLED` define | `ProjectSettings/ProjectSettings.asset` |
| `FIREBASE_CRASHLYTICS_INSTALLED` define | Same file |
| `FIREBASE_REMOTE_CONFIG_INSTALLED` define | Same file |
| EDM4U installed | `Packages/manifest.json` grep `com.google.external-dependency-manager` |

## 5. AppLovin MAX

| Check | Where to verify |
|-------|-----------------|
| MAX package installed | `Packages/manifest.json` grep `com.applovin.mediation.ads` |
| SDK key set in AppLovinSettings | `Assets/MaxSdk/Resources/AppLovinSettings.asset` field `sdkKey` |
| AdMob Android App ID set | Same file, `adMobAndroidAppId` |
| AdMob iOS App ID set | Same file, `adMobIosAppId` |
| Rewarded ad unit IDs (Android + iOS) | `Assets/Resources/SorollaConfig.asset` field `rewardedAdUnit` |
| Interstitial ad unit IDs (Android + iOS) | Same file, `interstitialAdUnit` |
| Banner ad unit IDs (optional) | Same file, `bannerAdUnit` |
| `APPLOVIN_MAX_INSTALLED` define | `ProjectSettings/ProjectSettings.asset` |
| `SOROLLA_MAX_ENABLED` define | Same file |
| Consent flow enabled | `ProjectSettings/AppLovinInternalSettings.json` field `ConsentFlowEnabled` |
| Privacy Policy URL set | Same file, `ConsentFlowPrivacyPolicyUrl` |
| ATT usage description set (iOS) | Same file or `ProjectSettings/ProjectSettings.asset` |
| AdMob GDPR consent message published | AdMob dashboard (manual check) |
| `app-ads.txt` on studio domain | `https://<studio-domain>/app-ads.txt` (manual check) |

## 6. Adjust

| Check | Where to verify |
|-------|-----------------|
| App token set | `Assets/Resources/SorollaConfig.asset` field `adjustAppToken` |
| Purchase event token set | Same file, `adjustPurchaseEventToken` |
| Sandbox mode correct for build type | Same file, `adjustSandboxMode` (true=QA, false=release) |
| `ADJUST_SDK_INSTALLED` define | `ProjectSettings/ProjectSettings.asset` |
| `SOROLLA_ADJUST_ENABLED` define | Same file |

## 7. TikTok (Optional)

| Check | Where to verify |
|-------|-----------------|
| `enableTikTok` toggled on | `Assets/Resources/SorollaConfig.asset` field `enableTikTok` |
| App IDs set (Android + iOS) | Same file, `tiktokAppId` |
| EM App IDs set (Android + iOS) | Same file, `tiktokEmAppId` |
| Access tokens set (Android + iOS) | Same file, `tiktokAccessToken` |
| Debug mode off for release | Same file, `tiktokDebugMode` = 0 |

## 8. SDK Mode & Defines

| Check | Where to verify |
|-------|-----------------|
| `isPrototypeMode` = false | `Assets/Resources/SorollaConfig.asset` |
| `SOROLLA_FULL` define set (both platforms) | `ProjectSettings/ProjectSettings.asset` |
| All defines present on both Android AND iOS | Same file, compare both `scriptingDefineSymbols` entries |

## 9. Android Build

| Check | Where to verify |
|-------|-----------------|
| Gradle templates present | `Assets/Plugins/Android/mainTemplate.gradle`, `baseProjectTemplate.gradle`, `launcherTemplate.gradle`, `gradleTemplate.properties` |
| AndroidX + Jetifier enabled | `gradleTemplate.properties` grep `useAndroidX` and `enableJetifier` |
| AndroidManifest.xml valid | `Assets/Plugins/Android/AndroidManifest.xml` |
| link.xml present | `Assets/Sorolla.link.xml` |
| EDM4U Android resolved | `Assets/GeneratedLocalRepo/` should have Firebase/MAX artifacts |

## 10. iOS Build

| Check | Where to verify |
|-------|-----------------|
| CocoaPods available on build machine | `which pod` |
| Privacy manifest present | `Assets/Plugins/iOS/PrivacyInfo.xcprivacy` |
| Bitcode disabled | Check editor script or Xcode project |

## 11. Build Health (Palette Window)

| Check | Where to verify |
|-------|-----------------|
| SDK Versions - green | Palette > Configuration > Build Health |
| Mode Consistency - green | Same |
| Scoped Registries - green | Same |
| Firebase Coherence - green | Same |
| Config Sync - green | Same |
| Android Manifest - green | Same |

## 12. Pre-Release Toggles

| Toggle | QA value | Release value | Where |
|--------|----------|---------------|-------|
| `adjustSandboxMode` | true | **false** | SorollaConfig.asset |
| `tiktokDebugMode` | true | **false** | SorollaConfig.asset |
| MAX test mode | enabled | **disabled** | Per-network in MAX dashboard |
| Debug UI | accessible | **removed or gated** | Scene hierarchy |

## 13. On-Device QA

| Check | Platform | How |
|-------|----------|-----|
| App launches without crash | Both | Install and run |
| Consent dialog appears | Both (EU locale / iOS) | First launch |
| SDKs show green in Debug UI | Both | Triple-tap screen |
| GA progression events appear | Both | Play a level, check GA dashboard |
| Firebase events appear | Both | Check Firebase Console |
| Crashlytics crash appears | Both | Force crash via Debug UI, relaunch |
| Rewarded ad loads and completes | Both | Trigger rewarded ad |
| Interstitial loads | Both | Trigger interstitial |
| Adjust install event appears | Both | Check Adjust dashboard (sandbox filter) |
| TikTok events appear | Both | Check TikTok Events Manager |
| MAX Mediation Debugger clean | Both | AppLovin Integration Manager |
| ATT dialog appears | iOS only | First launch on iOS 14.5+ |
