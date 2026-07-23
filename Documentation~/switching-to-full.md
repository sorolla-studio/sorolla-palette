# Full Mode Soft Launch Migration

<div class="srl-journey">
  <span class="srl-journey-step"><a href="quick-start.html">Prototype</a></span>
  <span class="srl-journey-step srl-journey-current">Full migration</span>
  <span class="srl-journey-step"><a href="validation.html">Validation</a></span>
  <span class="srl-journey-step">Soft launch</span>
</div>

Move to Full mode when your Prototype build is already reporting level analytics and the game is ready for soft-launch monetization, attribution, consent, and revenue validation.

**Best for:** soft launch, paid UA tests, ad monetization tests, store-submitted builds.

Full mode adds:

| Area | What changes | What you do |
|------|--------------|-------------|
| Ads | AppLovin MAX with mediation | Add ad placements and validate fill |
| Attribution | Adjust install, session, ad revenue, and purchase attribution | Use sandbox during QA, production before launch |
| Consent | GDPR CMP and iOS ATT flow | Publish consent message, add privacy settings access |
| Firebase | Analytics, Crashlytics, Remote Config become required | Add platform config files |
| Revenue | Purchases can fan out to Adjust, Firebase, and GameAnalytics (and TikTok only if a parked TikTok config is already present) | Wire Unity IAP once if the game sells IAP |

Start from [Prototype Mode](quick-start.md). Do not migrate a build that has not already passed Prototype validation.

---

## 1. Lock Identifiers First

Before switching modes, confirm every dashboard points at the same app identifiers as Unity Player Settings.

| Service | Android identifier | iOS identifier |
|---------|--------------------|----------------|
| Unity Player Settings | `com.yourcompany.yourgame` | `com.yourcompany.yourgame` |
| Google Play / App Store Connect | _______ | _______ |
| GameAnalytics | Game Key: _______ | Game Key: _______ |
| Facebook | App ID: _______ | App ID: _______ |
| Firebase | `google-services.json` package: _______ | `GoogleService-Info.plist` bundle: _______ |
| AppLovin MAX | Rewarded: _______ / Interstitial: _______ | Rewarded: _______ / Interstitial: _______ |
| Adjust | App Token: _______ | App Token: _______ |
| TikTok (parked, only if already configured) | App ID: _______ / EM App ID: _______ | App ID: _______ / EM App ID: _______ |

If any package name, bundle ID, app token, or ad unit belongs to a different app, stop and fix the dashboard before continuing. Identifier mismatches cause missing attribution, missing events, wrong revenue routing, and wasted paid UA.

---

## 2. Switch Unity to Full Mode

1. Open **Tools > Sorolla Palette SDK**.
2. Click **Switch to Full**.
3. Let Unity install and resolve the Full-mode packages.
4. Reopen **Tools > Sorolla Palette SDK** and check the **Launch Readiness** verdict.

Launch Readiness blocks a build when it has proved definite active-platform data loss: missing or
rejected GameAnalytics/Facebook credentials or platform registration, missing Full-mode AppLovin MAX
ad units, the Adjust app token, or the active platform's Firebase config. A vendor endpoint that
cannot be reached remains incomplete rather than blocking or passing.

Launch Readiness judges the platform your build target is set to. Rows for the other platform are
excluded from the verdict and return when you switch target, so a game shipping one platform can
read HEALTHY without configuring the other.

Do not hand-edit `Packages/manifest.json` unless Sorolla support asks you to. The mode switch owns required package changes.

If defines or packages look stale after switching, run **Palette > Run Setup (Force)**, then let Unity finish a domain reload.

---

## 3. Configure Full-Mode Keys

The Prototype keys stay in place. Full mode needs these additional values:

| SDK | Required | What to enter |
|-----|----------|---------------|
| Firebase | Yes | `google-services.json` and `GoogleService-Info.plist` in `Assets/` |
| AppLovin MAX | Yes | SDK Key plus Rewarded, Interstitial, and optional Banner ad unit IDs |
| Adjust | Yes | App Token and Purchase Event Token. Use sandbox only in a Development Build for QA; turn it off and rebuild for release |
| TikTok (parked) | Optional | `tiktokAppId`, `tiktokEmAppId`, `tiktokAccessToken` per platform. Parked vendor; configure only for an existing TikTok setup (see [TikTok Setup](guides/tiktok.md)) |

Use the setup guides only when you need dashboard-level detail:

- [Ads Setup](guides/ads.md)
- [Adjust Setup](guides/adjust.md)
- [Firebase Setup](guides/firebase.md)
- [TikTok Setup](guides/tiktok.md) (parked)

---

## 4. Add Soft-Launch Game Code

Keep the Prototype level calls. Full mode usually adds ads, privacy settings access, and purchase tracking.

### Rewarded Ads

Gate the watch-ad button on readiness and always handle failure:

```csharp
using Sorolla.Palette;

public void RefreshRewardedButton()
{
    watchAdButton.interactable = Palette.IsRewardedAdReady;
}

public void OnWatchAdClicked()
{
    if (!Palette.IsRewardedAdReady)
    {
        ShowAdUnavailableMessage();
        return;
    }

    Palette.ShowRewardedAd(
        onComplete: GrantReward,
        onFailed: ShowAdUnavailableMessage);
}
```

### Interstitial Ads

Interstitials can fail because of no fill, consent state, network issues, or display errors. Always continue game flow from both callbacks.

```csharp
public void OnLevelComplete()
{
    if (ShouldShowInterstitial())
    {
        Palette.ShowInterstitialAd(
            onComplete: ShowNextLevel,
            onFailed: ShowNextLevel);
    }
    else
    {
        ShowNextLevel();
    }
}
```

### Privacy Settings

GDPR requires users to reopen privacy options when the CMP says the option is required:

```csharp
privacyButton.gameObject.SetActive(Palette.PrivacyOptionsRequired);
privacyButton.onClick.AddListener(() => Palette.ShowPrivacyOptions());
```

Full consent setup details are in [GDPR and ATT Consent](guides/gdpr.md).

### Unity IAP Purchases

If the game uses Unity IAP v5, wire Palette once when creating the store controller. This API is available when the Unity IAP package is installed:

```csharp
_store = UnityIAPServices.StoreController();
Palette.AttachPurchaseTracking(_store);

_store.OnPurchasePending += order =>
{
    GrantRewards(order.CartOrdered);
    _store.ConfirmPurchase(order);
};

await _store.Connect();
```

Palette subscribes to purchase events for analytics. Your game still owns reward fulfillment and purchase confirmation.

Firebase `purchase` is client-side telemetry, not verified revenue. iOS purchases include a
`store_environment` label (`production`, `sandbox`, `xcode`, or `unknown`) decoded from the
StoreKit JWS so TestFlight can be filtered with `store_environment == "sandbox"`. Android and
legacy purchase paths are labelled `unknown`; do not treat `unknown` as production revenue.

If the game has no IAP, skip this section.

---

## 5. Configure Consent and Store Privacy

Consent setup has an order. Follow it exactly:

1. Publish the GDPR consent message in AdMob.
2. In AppLovin Integration Manager, install **Google Ad Manager** or **Google AdMob** as a mediated network.
3. Configure the MAX Terms and Privacy Policy Flow.
4. Set the Privacy Policy URL.
5. Set the iOS ATT usage description.
6. Add the privacy settings button in game UI.
7. Build and test on device.

MAX uses the Google Mobile Ads SDK to render the UMP consent form. If the Google mediated network is missing, the app may run and ads may load, but the GDPR CMP dialog will not appear correctly for EU users.

Also complete the store-facing items:

- App Store privacy questionnaire: [App Store Privacy](app-store-privacy.md)
- `app-ads.txt` on your developer website.
- Privacy policy URL live and reachable.

---

## Soft Launch Definition of Done

Before uploading a soft-launch build:

- [ ] Prototype analytics were already validated.
- [ ] Identifier cross-reference table is complete and correct.
- [ ] Full mode is selected in **Tools > Sorolla Palette SDK**.
- [ ] Rewarded and interstitial placements are wired and failure-safe.
- [ ] Privacy settings button is present when `Palette.PrivacyOptionsRequired` is true.
- [ ] GDPR consent message is published.
- [ ] Privacy Policy URL is set in MAX and live on the web.
- [ ] `app-ads.txt` is live on your developer website.
- [ ] Full-mode validation passes in [Full Mode Validation](validation.md#full-mode-soft-launch-validation).

---

## Help

- [Troubleshooting](troubleshooting.md)
- [Ads Setup](guides/ads.md)
- [Adjust Setup](guides/adjust.md)
- [GDPR and ATT Consent](guides/gdpr.md)
- [Android Build Compatibility](guides/android-build-compatibility.md)
