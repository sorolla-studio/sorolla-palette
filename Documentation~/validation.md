# Full Mode Validation Checklist

Use this page for Full-mode soft-launch validation.

For Prototype mode, use the embedded checklist at the end of [Prototype Mode Quick Start](quick-start.md#prototype-checklist). Keeping Prototype setup and Prototype validation on one page avoids a second path for studios.

Run Full-mode validation on real Android and iOS devices. Simulators are not enough for ads, consent, ATT, attribution, or store purchase behavior.

---

## Prototype Mode

Prototype validation lives in the [Prototype Checklist](quick-start.md#prototype-checklist).

---

## Full Mode Soft Launch Validation

Full-mode validation proves the build is ready for soft-launch ads, attribution, consent, crash reporting, and revenue tracking.

Start only after the [Prototype Checklist](quick-start.md#prototype-checklist) passes.

### Identifier Lock

- [ ] Unity Android package name matches Google Play, Firebase, Facebook, AppLovin MAX, Adjust, and TikTok if used.
- [ ] Unity iOS bundle ID matches App Store Connect, Firebase, Facebook, AppLovin MAX, Adjust, and TikTok if used.
- [ ] Ad unit IDs belong to the same app and platform as the Unity build.
- [ ] Adjust app tokens belong to the same app and platform as the Unity build.

If any identifier points to a different app, stop and fix the dashboard before testing. Mismatches cause missing attribution, missing events, wrong revenue routing, and wasted UA spend.

### Unity

- [ ] **Palette > Configuration** is set to **Full** mode.
- [ ] **Build Health** is green for Android and iOS.
- [ ] Firebase config files match the app identifiers.
- [ ] AppLovin MAX SDK Key and ad unit IDs are configured.
- [ ] Adjust App Token and Purchase Event Token are configured.
- [ ] `adjustSandboxMode` is **true** during QA.
- [ ] TikTok fields are configured only if TikTok campaign tracking is needed.

### Game Code

- [ ] Prototype level calls are still present.
- [ ] Rewarded ad placements check `Palette.IsRewardedAdReady` before showing.
- [ ] Rewarded ad placements handle both `onComplete` and `onFailed`.
- [ ] Interstitial placements handle both `onComplete` and `onFailed` so game flow cannot stall on no-fill.
- [ ] Privacy settings button is shown when `Palette.PrivacyOptionsRequired` is true.
- [ ] If the game uses Unity IAP v5, `Palette.AttachPurchaseTracking(store)` is called once before `store.Connect()`.

### Consent And Store Privacy

- [ ] GDPR consent message is published in AdMob.
- [ ] Google Ad Manager or Google AdMob mediated network is installed in AppLovin Integration Manager.
- [ ] MAX Terms and Privacy Policy Flow is configured.
- [ ] Privacy Policy URL is live and set in MAX.
- [ ] iOS ATT usage description is set.
- [ ] `app-ads.txt` is live on the developer website.
- [ ] App Store privacy answers match the enabled SDKs.

### Android Device

| Check | Pass |
|-------|------|
| Build | AAB/APK builds without Unity, Gradle, or dependency errors. |
| Launch | App starts without a crash. |
| Sorolla Vitals | Required SDKs are green. |
| Level events | GameAnalytics progression events arrive. |
| Firebase | Analytics events arrive and Crashlytics is configured. |
| Consent | GDPR CMP appears and resolves from an EU test state. |
| Rewarded ad | Ad shows or failure UI handles no-fill cleanly. |
| Interstitial | Ad shows or game flow continues on failure. |
| Adjust sandbox | Install, session, and events appear with the sandbox filter. |
| Mediation Debugger | No unresolved required networks. |

For Android ad or attribution failures, first disable VPN, private DNS, ad blockers, and device-level threat protection. Those can block AppLovin or Adjust domains while normal browser traffic still works.

### iOS Device

Run the Android checks, plus:

| Check | Pass |
|-------|------|
| CocoaPods | Xcode project builds without pod install or linker errors. |
| Signing | Device build installs successfully. |
| ATT | Prompt appears after consent when applicable on iOS 14.5+. |
| Store privacy | App Store Connect metadata matches enabled SDKs. |

### Upload Build

Before uploading a soft-launch build:

- [ ] `adjustSandboxMode` is **false**.
- [ ] `verboseLogging` is **false** (auto-forced off in release builds regardless).
- [ ] Mediation network test modes are off for production ad units.
- [ ] Sorolla Vitals is green on real Android and iOS devices.
