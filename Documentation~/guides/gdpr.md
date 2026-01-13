# GDPR & ATT Consent

Privacy compliance for EU users and iOS App Tracking Transparency.

> Required for **Full mode**. Optional but recommended for Prototype.

---

## Why You Need This

- **GDPR**: EU law requires user consent before collecting data
- **ATT**: iOS 14.5+ requires permission for cross-app tracking
- **App Store**: Required for approval in EU regions

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
2. Enable **MAX Terms and Privacy Policy Flow**
3. Set **Privacy Policy URL** (your company's policy)
4. Set **User Tracking Usage Description**:
   ```
   This identifier will be used to deliver personalized ads to you.
   ```
5. Click **Save**

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
3. Use Debug UI to verify consent status
4. To test again: Delete app and reinstall

### Reset Consent (Testing Only)

Use Debug UI → **Privacy** → **Reset Consent** to test the flow again.

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
| Dialog not showing | Verify GDPR message is **published** in AdMob |
| ATT not appearing | iOS 14.5+ only, shows once per install |
| Consent always denied | Check Privacy Policy URL is valid |
| Ads not loading after consent | Wait for consent callback to complete |
