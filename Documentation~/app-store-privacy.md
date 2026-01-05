# App Store Privacy & Compliance Guide

> **Target Audience**: Developers releasing games on the App Store.
> **Scope**: App Store Connect "App Privacy" section.

To avoid being overwhelmed, choose the track that matches your **Sorolla SDK Mode**.

## 📍 Choose Your Track

| **[Track A: Prototype Mode](#track-a-prototype-mode)** | **[Track B: Full Mode](#track-b-full-mode)** |
|--------------------------------------------------|--------------------------------------------|
| 🎯 **Goal**: UA Testing (CPI/Retention) | 🚀 **Goal**: Global Launch / Monetization |
| 📦 **SDKs**: GameAnalytics + Facebook | 📦 **SDKs**: All (GA, FB, MAX, Adjust, Firebase) |
| ⏱️ **Time**: ~5 minutes | ⏱️ **Time**: ~15 minutes |

---

<br>

## Track A: Prototype Mode
**For limited UA testing with Facebook Ads & GameAnalytics.**

### 1. App Store Connect Questionnaire

**"Do you or your third-party partners collect data from this app?"**  
👉 **Yes**

### 2. Data Types to Declare

#### **A. Identifiers**
| Data Type | Collected? | Linked to User? | Used for Tracking? | Purposes | Service |
|-----------|------------|-----------------|--------------------|----------|---------|
| **Device ID** | **Yes** | **Yes** | **Yes** | Third-Party Advertising, Analytics | Facebook (Tracking), GA |
| **User ID** | **Yes** | **Yes** | **Yes** | Analytics, App Functionality | GameAnalytics |

> **Why Tracking?** Facebook uses Device ID (IDFA) to attribute installs to ads. This constitutes "Tracking".

#### **B. Usage Data**
| Data Type | Collected? | Linked to User? | Used for Tracking? | Purposes | Service |
|-----------|------------|-----------------|--------------------|----------|---------|
| **Product Interaction** | **Yes** | **Yes** | **No*** | Analytics, App Functionality | GameAnalytics |
| **Advertising Data** | **Yes** | **Yes** | **Yes** | Third-Party Advertising | Facebook |

> *\*Product Interaction is generally NOT used for tracking in Prototype mode unless you are sharing level data with Facebook.*

---

<br>

## Track B: Full Mode
**For production-ready games with Ads (MAX), Attribution (Adjust), and Crashlytics.**

### 1. App Store Connect Questionnaire

**"Do you or your third-party partners collect data from this app?"**  
👉 **Yes**

### 2. Data Types to Declare

#### **A. Identifiers**
| Data Type | Collected? | Linked to User? | Used for Tracking? | Purposes | Service |
|-----------|------------|-----------------|--------------------|----------|---------|
| **Device ID** | **Yes** | **Yes** | **Yes** | Third-Party Advertising, Analytics, App Functionality | MAX, Adjust, FB, GA |
| **User ID** | **Yes** | **Yes** | **Yes** | Analytics, App Functionality, Product Personalization | Adjust, GA |

#### **B. Usage Data**
| Data Type | Collected? | Linked to User? | Used for Tracking? | Purposes | Service |
|-----------|------------|-----------------|--------------------|----------|---------|
| **Product Interaction** | **Yes** | **Yes** | **No** | Analytics, App Functionality | GameAnalytics |
| **Advertising Data** | **Yes** | **Yes** | **Yes** | Third-Party Advertising, Analytics | MAX, Adjust |

#### **C. Diagnostics (If using Firebase)**
| Data Type | Collected? | Linked to User? | Used for Tracking? | Purposes | Service |
|-----------|------------|-----------------|--------------------|----------|---------|
| **Crash Data** | **Yes** | **Yes** | **No** | Analytics, App Functionality | Firebase / GA |
| **Performance Data** | **Yes** | **Yes** | **No** | Analytics, App Functionality | Firebase / GA |

---

<br>

## 🌍 Shared Guidelines (Both Tracks)

### 1. Privacy Manifests (iOS 17+)
**You do NOT need to create a manual manifest.**  
Sorolla SDK automatically handles this. When you build, Unity merges manifests from:
*   `com.facebook` (Facebook)
*   `com.gameanalytics` (GameAnalytics)
*   `com.applovin` (MAX - *Full Mode*)
*   `com.adjust` (Adjust - *Full Mode*)

### 2. App Tracking Transparency (ATT)
To track installs (Facebook/Adjust) or show personalized ads (MAX), you **must** use the ATT popup.

*   **How to Trigger**: Sorolla SDK calls this automatically on app start.
*   **Info.plist Text**: Set `NSUserTrackingUsageDescription` in SorollaConfig.
    *   *Example*: "This identifier will be used to deliver personalized ads to you."



---

## FAQ

**Q: I am only testing on TestFlight, do I need this?**
A: **Yes.** App Store Connect requires this section to be filled out before any external distribution (including TestFlight External).

**Q: Does Sorolla collect Location?**
A: **No.** We do not access GPS. Ad networks use IP addresses for coarse location, which typically doesn't require the "Precise Location" declaration.
