# GDPR & ATT Consent

Privacy compliance for EU users and iOS App Tracking Transparency.

> Required for **Full mode**. Optional but recommended for Prototype.

---

## Why You Need This

- **GDPR**: EU law requires user consent before collecting data
- **ATT**: iOS 14.5+ requires permission for cross-app tracking
- **App Store**: Required for approval in EU regions

---

## Analytics consent vs ad consent

Palette treats **analytics** and **ad** consent separately, using Firebase Consent Mode v2:

- **`analytics_storage` defaults to granted**, so installs (`first_open`) and core analytics are counted from the very first launch. The SDK ships this default in the Android manifest and iOS `Info.plist` (so it applies to the first native event, before the CMP resolves), and only downgrades it to *denied* for a user who explicitly declines in a GDPR region.
- **Ad signals** (`ad_storage`, `ad_personalization`, `ad_user_data`) default to *denied* and are granted only after the CMP resolves consent. Ad personalization is always gated by the CMP.

Practical effect: an EEA user emits one identified `first_open` before the CMP resolves; ad personalization is never enabled pre-consent. This keeps Firebase install counts in parity with Adjust / GameAnalytics (which count the install at SDK init). Studios with stricter EEA analytics requirements can adjust the posture in `FirebaseAdapterImpl.ApplyConsentSignals` and the injected Consent Mode defaults.

---

## 1. AdMob Setup (GDPR)

1. Create account at [admob.google.com](https://admob.google.com)
2. Add your app
3. Go to **Privacy & messaging** → **GDPR**
4. Click **Create message**
5. Configure:
   - Select your app
   - Customize consent form appearance
   - Enable **Custom ad partners**
   - Add: AppLovin, AdMob, Meta, Unity
6. Click **Publish**

## 2. Unity Setup

1. Open **AppLovin** → **Integration Manager**
2. Under **Mediated Networks**, install **Google Ad Manager** (or Google AdMob) and fill in its **App ID** for each platform. This is required: Palette's consent form is Google UMP, which reads that AdMob App ID to find your GDPR message, and AppLovin writes the id into the build only when a Google adapter is installed.
3. Leave **MAX Terms and Privacy Policy Flow** off. Palette shows the Google consent form and the iOS tracking prompt itself; AppLovin's flow would add its own "Terms and Privacy Policy" alert. Palette's build check turns it off if it is on.
4. iOS tracking prompt text: Palette adds this `NSUserTrackingUsageDescription` to `Info.plist` when the build has none. App Review (Guideline 5.1.1) wants the purpose plus a concrete example; vague strings get rejected:
   ```
   This identifier lets us show you more relevant ads and measure ad performance - for example, ads for games similar to this one.
   ```
   Palette also ships this text in German, Spanish, French, Japanese, Korean and Chinese (Simplified and Traditional). If you set your own text, Palette leaves it and adds no translations: provide your own `InfoPlist.strings` per language.

## What players see

Palette runs the consent flow at launch, before ads start:

| Player | iOS | Android |
|--------|-----|---------|
| Where Google UMP requires consent (EEA, UK, Switzerland) | Google consent form, then the ATT prompt | Google consent form |
| Everyone else | ATT prompt only | Nothing |

Each prompt appears once per install; later launches reuse the stored answers. If you configure Google's IDFA explainer message in AdMob, UMP shows it and the ATT prompt itself, and Palette does not ask again.

## 3. Add Privacy Button

GDPR requires users to change consent anytime. Add to your settings:

```csharp
using Sorolla.Palette;
using UnityEngine;
using UnityEngine.UI;

public class SettingsScreen : MonoBehaviour
{
    [SerializeField] Button privacyButton;

    void Start()
    {
        // Only show if user is in GDPR region
        privacyButton.gameObject.SetActive(Palette.PrivacyOptionsRequired);
        privacyButton.onClick.AddListener(OnPrivacyClicked);
    }

    void OnPrivacyClicked()
    {
        Palette.ShowPrivacyOptions(() => {
            Debug.Log("Privacy settings updated");
        });
    }
}
```

---

## Testing

1. Build to device
2. First launch should show consent dialog
3. Use Sorolla Vitals to verify consent status
4. To test again: Delete app and reinstall

### Reset Consent (Testing Only)

Delete and reinstall the app to test the first-run consent flow again.

---

## API Reference

```csharp
// Check if privacy button should be shown
bool showButton = Palette.PrivacyOptionsRequired;

// Current consent status
ConsentStatus status = Palette.ConsentStatus;

// Can ads be shown?
bool canShow = Palette.CanRequestAds;

// Show privacy options dialog
Palette.ShowPrivacyOptions(onComplete: () => { });
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Dialog not showing | Verify the GDPR message is **published** in AdMob, and the Google Ad Manager adapter is installed with its App ID set in the MAX Integration Manager |
| ATT not appearing | iOS 14.5+ only, shows once per install |
| Consent stays `Unknown` | The device was offline, or no GDPR message is published in AdMob for this app. Ad consent stays denied until Google UMP answers; relaunch online |
| AppLovin "Terms and Privacy Policy" alert appears | Untick **MAX Terms and Privacy Policy Flow** in the Integration Manager (Palette's build check does this), then rebuild |
| Ads not loading after consent | Wait for consent callback to complete |
