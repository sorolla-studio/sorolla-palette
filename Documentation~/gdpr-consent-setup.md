# GDPR/ATT Consent Setup Guide

**Required for Full Mode and EU/UK regions.**

This guide walks you through enabling GDPR consent collection using MAX's built-in UMP (User Messaging Platform) automation. Once configured, MAX automatically handles consent collection for EU/UK users.

> **Why This Matters**: Since January 2024, apps without proper GDPR consent show "Limited Ads" in EU/UK regions, significantly reducing ad revenue.

> **Note**: This is **required** for [Full Mode](full-setup.md). For Prototype Mode, GDPR is optional (only needed if using MAX for ads in EU).

---

## Prerequisites

Before starting, ensure you have:

- [ ] Sorolla SDK installed with MAX enabled
- [ ] AppLovin MAX SDK 8.5.0+ (included in Sorolla)
- [ ] Google AdMob account with your app registered
- [ ] Privacy Policy URL for your app/company

> **💡 Tip**: If you're using [Full Mode](full-setup.md), follow Step 4 of that guide which includes condensed GDPR setup. This guide provides detailed instructions and troubleshooting.

---

## Part 1: Google AdMob Dashboard Setup

### Step 1.1: Create GDPR Message

1. Go to [AdMob Dashboard](https://apps.admob.com)
2. Navigate to **Privacy & messaging** > **GDPR**
3. Click **Create message**
4. Select your app(s) from the list

### Step 1.2: Configure Message Options

1. For "User consent options", select **Consent or Manage options**
   - Do NOT select "Close" - this doesn't comply with GDPR
2. Target: **Countries subject to GDPR (EEA and UK)**

### Step 1.3: Style the Consent Form

Under **Styling**, set recommended colors for visibility:

| Setting | Value | Notes |
|---------|-------|-------|
| Global Secondary color | `#ffffff` (white) | Background color |
| Buttons Secondary color | `#6e6e6e` (gray) | Secondary button color |

### Step 1.4: Configure Ad Partners

1. Go to **GDPR settings** in AdMob
2. Enable the **Custom ad partners** toggle
3. Select all your integrated ad networks:
   - AppLovin
   - AdMob/Google
   - Meta Audience Network (if using)
   - Unity Ads (if using)
   - ironSource (if using)
   - **Note**: Select "Mobvista/Mintegral" not just "Mintegral"

4. Click **Confirm** and **Save**

### Step 1.5: Publish the Message

1. Review your GDPR message
2. Click **Publish**
3. Wait a few minutes for changes to propagate

---

## Part 2: Unity Integration Manager Setup

### Step 2.1: Open Integration Manager

In Unity, go to **AppLovin** > **Integration Manager**

### Step 2.2: Enable Privacy Flow

Scroll to find **Terms and Privacy Policy Flow** section and configure:

| Setting | Value | Required |
|---------|-------|----------|
| Enable MAX Terms and Privacy Policy Flow | **Checked** | Yes |
| Privacy Policy URL | Your privacy policy URL | Yes |
| Terms of Service URL | Your ToS URL | Optional |

### Step 2.3: Configure iOS ATT Description

Set the **User Tracking Usage Description** (iOS only):

```
This identifier will be used to deliver personalized ads to you.
```

Or customize for your app's context. This text appears in the iOS ATT popup.

### Step 2.4: Save and Apply

Click **Save** to apply changes. Unity may need to reimport.

---

## Part 3: Sorolla SDK Integration

The Sorolla SDK automatically exposes consent status. No additional code is required for basic operation.

### 3.1: Check Consent Status (Optional)

```csharp
using Sorolla;
using Sorolla.Adapters;

// After SDK initialization, check consent status
void OnSorollaReady()
{
    Debug.Log($"Consent Status: {SorollaSDK.ConsentStatus}");
    Debug.Log($"Can Request Ads: {SorollaSDK.CanRequestAds}");
}
```

### 3.2: Add Privacy Options to Settings (Required for GDPR)

GDPR requires you to let users change their consent. Add to your settings screen:

```csharp
using UnityEngine;
using UnityEngine.UI;
using Sorolla;

public class SettingsScreen : MonoBehaviour
{
    [SerializeField] Button privacyButton;

    void Start()
    {
        // Only show privacy button if user is in GDPR region
        privacyButton.gameObject.SetActive(SorollaSDK.PrivacyOptionsRequired);
        privacyButton.onClick.AddListener(OnPrivacyButtonClicked);
    }

    void OnPrivacyButtonClicked()
    {
        SorollaSDK.ShowPrivacyOptions(() =>
        {
            Debug.Log("Privacy options dismissed");
            // Optionally refresh UI based on new consent
        });
    }
}
```

### 3.3: Gate Ads Based on Consent (Recommended)

```csharp
void ShowRewardedAd()
{
    if (!SorollaSDK.CanRequestAds)
    {
        Debug.Log("Cannot show ads - consent not obtained");
        // Show alternative or inform user
        return;
    }

    SorollaSDK.ShowRewardedAd(
        onComplete: () => GiveReward(),
        onFailed: () => Debug.Log("Ad failed")
    );
}
```

### 3.4: React to Consent Changes (Optional)

```csharp
void OnEnable()
{
    SorollaSDK.OnConsentStatusChanged += HandleConsentChanged;
}

void OnDisable()
{
    SorollaSDK.OnConsentStatusChanged -= HandleConsentChanged;
}

void HandleConsentChanged(ConsentStatus status)
{
    Debug.Log($"Consent changed to: {status}");

    // Update UI or behavior based on new consent
    if (status == ConsentStatus.Obtained)
    {
        // User consented - full ad experience
    }
    else if (status == ConsentStatus.Denied)
    {
        // User denied - limited ads
    }
}
```

---

## Part 4: Testing

### 4.1: Testing Outside GDPR Regions

If you're not in EU/UK, you need to simulate GDPR geography:

1. In **AppLovin** > **Integration Manager**
2. Find **Debug User Geography**
3. Set to **GDPR**

Also add your test device ID before SDK initialization:

```csharp
// Add this BEFORE MaxSdk.InitializeSdk() is called
// You can find your test device hash in logcat/console
MaxSdk.SetExtraParameter("google_test_device_hashed_id", "YOUR_TEST_DEVICE_HASH");
```

> **Note**: The consent flow only shows to "new" users. Uninstall and reinstall your app to retest.

### 4.2: Using Mediation Debugger

1. In your debug build, call: `MaxSdk.ShowMediationDebugger()`
2. Navigate to **Privacy** section
3. Verify:
   - **CMP**: Shows "Google consent management solutions"
   - **IABTCF_gdprApplies**: `1` (in GDPR region)
   - **IABTCF_TCString**: Has a value (consent string)

### 4.3: Check for Missing Networks

1. Complete the CMP consent flow, granting all consents
2. Open Mediation Debugger
3. Check **CMP CONFIGURATION** section
4. Look for:
   - **MISSING ATP NETWORKS**
   - **MISSING TCF VENDORS**

If networks are missing, go back to AdMob and add them to your GDPR message.

---

## Verification Checklist

Use this checklist to confirm everything is set up correctly:

### AdMob Dashboard
- [ ] GDPR message created and published
- [ ] Message targets "Countries subject to GDPR (EEA and UK)"
- [ ] User consent options set to "Consent or Manage options"
- [ ] All ad networks added to custom ad partners list
- [ ] Styling applied for visibility

### Unity Integration Manager
- [ ] "Enable MAX Terms and Privacy Policy Flow" is checked
- [ ] Privacy Policy URL is set
- [ ] User Tracking Usage Description is set (iOS)

### Code Integration
- [ ] Settings screen has privacy options button
- [ ] Button visibility tied to `SorollaSDK.PrivacyOptionsRequired`
- [ ] Button calls `SorollaSDK.ShowPrivacyOptions()`

### Testing (in GDPR region or with debug geography)
- [ ] Consent form appears on first launch
- [ ] `SorollaSDK.ConsentStatus` returns expected value after consent
- [ ] `SorollaSDK.CanRequestAds` returns `true` after consent
- [ ] Mediation Debugger shows "Google consent management solutions" as CMP
- [ ] No missing networks in CMP CONFIGURATION
- [ ] Privacy options form opens from settings

### iOS Specific
- [ ] ATT popup appears after consent form
- [ ] User Tracking Usage Description displays correctly
- [ ] Review Notes mention ATT for App Store submission

---

## Troubleshooting

### Consent form doesn't appear

1. Verify GDPR message is published in AdMob (takes a few minutes)
2. Check you're in GDPR region or have debug geography set
3. Ensure "Enable MAX Terms and Privacy Policy Flow" is checked
4. Try uninstalling and reinstalling the app

### "Limited Ads" still showing

1. Verify all ad networks are added to custom ad partners
2. Check Mediation Debugger for missing networks
3. Ensure consent was actually granted (not just dismissed)

### ConsentStatus is Unknown

1. Wait for MAX SDK to fully initialize
2. Check `SorollaSDK.IsInitialized` is true
3. Subscribe to `OnConsentStatusChanged` for updates

### Privacy options button not appearing

1. Verify `SorollaSDK.PrivacyOptionsRequired` after SDK init
2. User must be in a consent region for this to be true
3. A CMP must be configured in AdMob

---

## API Reference

| Property/Method | Type | Description |
|-----------------|------|-------------|
| `SorollaSDK.ConsentStatus` | `ConsentStatus` | Current consent state |
| `SorollaSDK.CanRequestAds` | `bool` | Whether ads can be shown |
| `SorollaSDK.PrivacyOptionsRequired` | `bool` | Whether to show privacy button |
| `SorollaSDK.ShowPrivacyOptions(Action)` | `void` | Opens consent form |
| `SorollaSDK.OnConsentStatusChanged` | `event` | Fired when consent changes |

### ConsentStatus Values

| Value | Description |
|-------|-------------|
| `Unknown` | Not yet determined (SDK initializing) |
| `NotApplicable` | User is not in a consent region |
| `Required` | Consent needed but not yet obtained |
| `Obtained` | User has consented |
| `Denied` | User has denied consent |

---

## Sources

- [AppLovin MAX Terms and Privacy Policy Flow](https://support.axon.ai/en/max/unity/overview/terms-and-privacy-policy-flow)
- [Google UMP SDK Documentation](https://developers.google.com/admob/unity/privacy)
