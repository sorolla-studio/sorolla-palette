# Troubleshooting

Common issues and solutions for Sorolla SDK integration.

---

## Quick Fixes

| Issue | Solution |
|-------|----------|
| SDK not initializing | Check `SorollaConfig` exists in `Assets/Resources/` |
| Events not appearing | Verify SDK keys in Configuration window |
| Ads not loading | Check MAX SDK Key and Ad Unit IDs |
| Remote config returns defaults | Ensure values are **published** in console |
| Firebase errors | Verify config files match bundle ID |
| Build failing | Check **Build Health** section in Configuration window |
| Runtime crash on Android | Run `SorollaSDK > Tools > Sanitize Android Manifest` |

---

## Build Health

The Configuration window includes a **Build Health** section that validates your SDK setup before building. It runs 6 technical checks:

| Check | What it validates |
|-------|-------------------|
| SDK Versions | Installed versions meet minimum requirements |
| Mode Consistency | Installed SDKs match current mode (Prototype/Full) |
| Scoped Registries | UPM registries are properly configured |
| Firebase Coherence | Firebase modules have FirebaseApp installed |
| Config Sync | SorollaConfig matches installed packages |
| Android Manifest | No orphaned SDK entries that cause crashes |

**Note**: SDK installation status is shown in the **SDK Overview** section above Build Health.

**Auto-fix**: The validator automatically fixes AndroidManifest issues when the window opens or before builds.

**Pre-build validation**: Errors block builds automatically via `IPreprocessBuildWithReport`.

---

## SDK Initialization

### "SorollaConfig not found"

**Cause**: Config asset missing from Resources folder.

**Fix**:
1. Open `Sorolla > Configuration`
2. Click "Create Config" if prompted
3. Verify `Assets/Resources/SorollaConfig.asset` exists

### SDK not auto-initializing

**Cause**: `SorollaBootstrapper` not creating.

**Fix**:
1. Check for compile errors in Console
2. Verify `Sorolla` namespace accessible
3. Ensure no other `[RuntimeInitializeOnLoadMethod]` conflicts

---

## Analytics

### Events not appearing in GameAnalytics

1. Verify Game Key and Secret Key are correct
2. Check Unity Editor console for errors
3. Events may take 5-10 minutes to appear in dashboard
4. Ensure GA account has Admin access

### Events not appearing in Firebase

1. Verify `google-services.json` (Android) or `GoogleService-Info.plist` (iOS) exists
2. Enable "Google Analytics" when creating Firebase project
3. Enable `enableFirebaseAnalytics` in SorollaConfig
4. Firebase events have ~24 hour delay in console

---

## Ads (AppLovin MAX)

### Ads not loading

1. Verify SDK Key in Configuration window
2. Check Ad Unit IDs (Rewarded, Interstitial)
3. Wait 30 seconds after init for first ad load
4. Check device has internet connection

### Low fill rate

1. Enable mediation networks in MAX dashboard
2. Add AdMob, Meta, Unity Ads for better fill
3. Test on real device (not editor/simulator)

### Ad revenue not tracking

1. Verify Adjust App Token (Full mode)
2. Check Adjust is initialized (Debug UI > Health)
3. Revenue reports may take 24 hours

---

## Remote Config

### Always returns default values

**Firebase:**
1. Verify parameters are **published** in Firebase Console
2. Call `FetchRemoteConfig()` before getting values
3. Check Firebase config files present

**GameAnalytics:**
1. Configure A/B tests in GA dashboard
2. Ensure user is in test group

### Fetch always fails

1. Check internet connection
2. Verify Firebase project setup
3. Check Console for initialization errors

---

## iOS-Specific Issues

### CocoaPods/Ruby Errors

**Error**: `gem install activesupport` fails, `ruby/config.h not found`

**Cause**: System Ruby (2.6) missing headers.

**Fix**:
```bash
# Install CocoaPods via Homebrew
brew install cocoapods

# Verify installation
pod --version

# In Unity: switch platform to Android, then back to iOS
# This forces environment reload
```

### ATT Dialog Not Showing

1. Verify iOS 14.5+ target
2. Check `ContextScreen` prefab in Resources
3. ATT only shows once per app install
4. Use Debug UI to reset consent for testing

### Missing Provisioning Profile

1. Open Xcode project
2. Select Unity-iPhone target
3. Enable "Automatically manage signing"
4. Select your Apple Developer Team
5. Connect device and let Xcode register it

### Facebook SDK Swift Errors

**Error**: `TournamentUpdater has no member 'update'`

**Cause**: Facebook iOS SDK 18.0.2 breaking changes.

**Fix**: Pin Facebook pods to exactly 18.0.1 (not `~> 18.0.1`).

### Firebase "GoogleService-Info.plist not found"

1. Download from Firebase Console > Project Settings > iOS app
2. Place in `Assets/` folder
3. Bundle ID must match exactly

---

## Android-Specific Issues

### Build Health - AndroidManifest Errors

**Error**: `ClassNotFoundException` at runtime (e.g., `com.facebook.FacebookContentProvider`)

**Cause**: AndroidManifest.xml has entries for SDKs that are no longer installed.

**How it happens**: Switching modes (Prototype ↔ Full) can leave orphaned entries.

**Fix**:
1. Open `SorollaSDK > Configuration` window
2. Check the **Build Health** section
3. If "Android Manifest" shows an error, it will be auto-fixed on next validation
4. Or manually run: `SorollaSDK > Tools > Sanitize Android Manifest`

The Build Health validator automatically detects and removes orphaned manifest entries before builds.

### Gradle Version Mismatch

**Error**: `Incompatible Gradle version`

**Fix**:
1. Export to Android Studio
2. Update `gradle-wrapper.properties` to Gradle 8.0+
3. Build from Android Studio

### Missing google-services.json

1. Download from Firebase Console > Project Settings > Android app
2. Place in `Assets/` or `Assets/Plugins/Android/`
3. Package name must match exactly

---

## Firebase Issues

### "Firebase not initialized"

1. Check config files present and matching bundle ID
2. Verify internet connection on first launch
3. Check Console for async init errors

### Crashlytics not reporting

1. Crashes report on next app launch
2. Wait 5-10 minutes for dashboard update
3. Verify `enableCrashlytics` is true in config

### Crashes in development not showing

Crashlytics requires:
1. Release build (not debug)
2. App restart after crash
3. Internet connection

---

## Editor Issues

### Configuration window not opening

1. Menu: `Sorolla > Configuration`
2. If missing, check for compile errors
3. Try `Window > Package Manager > Sorolla SDK > Reimport`

### SDK detection failing

1. Close Unity completely
2. Delete `Library/` folder
3. Reopen project
4. Let packages reimport

### Mode switch not working

1. Confirm dialog appears
2. Wait for package manager to finish
3. Unity may need restart for scripting defines

---

## Debug UI

### Debug panel not appearing

1. Import sample from Package Manager
2. Add `DebugPanelManager` prefab to scene
3. Triple-tap (mobile) or BackQuote key (desktop)
4. Check prefab is DontDestroyOnLoad

### Health indicators red

- **GA**: Check Game Key/Secret Key
- **MAX**: Check SDK Key and Ad Unit IDs
- **Adjust**: Check App Token (Full mode only)
- **Firebase**: Check config files present

---

## Getting Help

1. Check [GitHub Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
2. Review error messages in Unity Console
3. Use Debug UI for on-device diagnostics
4. File new issue with:
   - Unity version
   - SDK version
   - Platform (iOS/Android)
   - Full error message
   - Steps to reproduce
