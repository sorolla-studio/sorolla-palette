# Prototype Mode Quick Start

Follow this page from top to bottom to prepare a Prototype build for Sorolla review, CPI tests, and early gameplay iteration.

Fresh installs start in **Prototype** mode automatically. Do not switch to Full mode until Prototype analytics are working.

Prototype mode uses:

| Area | SDK | Studio task |
|------|-----|-------------|
| Level analytics | GameAnalytics | Create the game, paste Game Key + Secret Key, add level calls |
| Acquisition signal | Facebook SDK | Create the app, paste App ID + Client Token |
| Firebase | Analytics, Crashlytics, Remote Config | Create the project, add config files |

Prototype mode does **not** need MAX ads, Adjust, GDPR/ATT consent setup, `app-ads.txt`, store privacy answers, or purchase attribution. Add those later in [Full Mode Soft Launch Migration](switching-to-full.md).

---

## 1. Install Palette

1. In Unity, open **Window > Package Manager**.
2. Click `+` and choose **Add package from git URL**.
3. Paste:

```text
https://github.com/sorolla-studio/sorolla-palette.git
```

4. Wait for Unity to import the package and resolve dependencies.
5. Open **Palette > Configuration** if the Palette window does not open automatically.

You do not need a bootstrap prefab or a manual init call. Palette auto-initializes at runtime through `SorollaBootstrapper`.

---

## 2. Match App Identifiers

Use the same identifiers in Unity, GameAnalytics, Facebook, and Firebase.

In Unity, check:

- **Android package name:** **Project Settings > Player > Android > Other Settings > Package Name**
- **iOS bundle ID:** **Project Settings > Player > iOS > Other Settings > Bundle Identifier**

If a dashboard app uses a different package name or bundle ID, fix the dashboard before building. Identifier mismatches are the fastest way to lose install and event data.

---

## 3. Create GameAnalytics Keys

1. Go to [gameanalytics.com](https://gameanalytics.com).
2. Create a game for Android and/or iOS with **Engine = Unity**.
3. Open **Settings > Game Settings**.
4. Copy the **Game Key** and **Secret Key** for each platform.
5. In Unity, open **Window > GameAnalytics > Select Settings** and paste the keys.

Grant Sorolla dashboard access if Sorolla will review analytics:

1. In GameAnalytics, open **Settings > Users**.
2. Invite `studio@sorolla.io` as **Admin**.

Full details: [GameAnalytics Setup](guides/gameanalytics.md).

---

## 4. Create Facebook Keys

1. Go to [developers.facebook.com](https://developers.facebook.com).
2. Create an app.
3. Copy the **App ID** from the app dashboard header.
4. Copy the **Client Token** from **Settings > Advanced > Security**.
5. Add the iOS platform with the Unity bundle ID.
6. Add the Android platform with the Unity package name.
7. In Unity, open **Facebook > Edit Settings** and paste the App ID and Client Token.

Authorize Sorolla's ad account if Sorolla will run UA:

1. In Facebook, open **Settings > Advanced**.
2. In **Authorized Ad Account IDs**, add `1294030715889860`.

Full details: [Facebook SDK Setup](guides/facebook.md).

---

## 5. Create Firebase Configs

1. Go to [Firebase Console](https://console.firebase.google.com/).
2. Create a project and enable **Google Analytics**.
3. Add an Android app with the Unity package name, then download `google-services.json`.
4. Add an iOS app with the Unity bundle ID, then download `GoogleService-Info.plist`.
5. Place both files in `Assets/`:

```text
Assets/google-services.json
Assets/GoogleService-Info.plist
```

In **Palette > Configuration**, install Firebase from the Firebase row if it is not already installed. Then refresh **Build Health** and confirm Firebase config files are found.

Full details: [Firebase Setup](guides/firebase.md).

---

## 6. Check Palette Configuration

Open **Palette > Configuration**.

Confirm:

- The current mode is **Prototype**.
- GameAnalytics is configured.
- Facebook SDK is configured.
- Firebase is installed and config files are present.
- **Build Health** has no blocking issues.

Keep Prototype mode lean. Do not configure MAX, Adjust, TikTok, IAP, consent, or store privacy yet.

---

## 7. Add Level Analytics

Add the level calls where your game already knows a level started, completed, or failed:

```csharp
using Sorolla.Palette;

public void OnLevelStarted(int level)
{
    Palette.Level.Start(level);
}

public void OnLevelWon(int level, int score)
{
    Palette.Level.Complete(level, score: score);
}

public void OnLevelLost(int level)
{
    Palette.Level.Fail(level);
}
```

If your game has worlds, pass both values:

```csharp
Palette.Level.Start(level: 4, world: 2);
Palette.Level.Complete(level: 4, world: 2, score: 1500);
```

Rules:

- Call `Start` when gameplay begins, not when a menu opens.
- Call either `Complete` or `Fail` once per run.
- Do not manually initialize Palette.
- Keep any existing studio analytics you still need.

---

## 8. Build Once

Build to a real Android or iOS device and play one complete level.

Dashboard timing:

| Dashboard | What should appear |
|-----------|--------------------|
| GameAnalytics | Level start, complete, and fail progression events |
| Facebook Events Manager | App install and app activity |
| Firebase Console | Analytics events; Crashlytics configured |

GameAnalytics usually updates within minutes. Firebase Analytics can take longer.

---

## Prototype Checklist

- [ ] Unity package name and bundle ID match the dashboard apps.
- [ ] GameAnalytics Game Key and Secret Key are configured.
- [ ] Facebook App ID and Client Token are configured.
- [ ] Firebase `google-services.json` and `GoogleService-Info.plist` are in `Assets/`.
- [ ] **Palette > Configuration** shows Prototype mode.
- [ ] **Build Health** has no blocking issues.
- [ ] `Palette.Level.Start`, `Palette.Level.Complete`, and `Palette.Level.Fail` are wired in game code.
- [ ] A real-device build launches and one complete level can be played.
- [ ] Sorolla has the build and dashboard access needed for review.

When this checklist passes, move to [Full Mode Soft Launch Migration](switching-to-full.md) only if the build is ready for ads, attribution, consent, and soft-launch revenue validation.
