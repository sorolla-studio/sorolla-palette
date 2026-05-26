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
2. Under **Mediated Networks**, install **Google Ad Manager** (or Google AdMob). This is required - MAX uses the Google Mobile Ads SDK to render the UMP consent form. Without it, only the MAX privacy popup appears, not the GDPR CMP dialog.
3. Enable **MAX Terms and Privacy Policy Flow**
4. Set **Privacy Policy URL** (your company's policy)
5. Set **User Tracking Usage Description**:
   ```
   This identifier will be used to deliver personalized ads to you.
   ```
6. Click **Save**

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
| Dialog not showing | Verify GDPR message is **published** in AdMob AND Google Ad Manager adapter is installed in MAX Integration Manager |
| ATT not appearing | iOS 14.5+ only, shows once per install |
| Consent always denied | Check Privacy Policy URL is valid |
| Ads not loading after consent | Wait for consent callback to complete |
